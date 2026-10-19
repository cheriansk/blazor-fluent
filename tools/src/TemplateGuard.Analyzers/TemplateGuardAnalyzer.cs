using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TemplateGuard.Analyzers.Models;

namespace TemplateGuard.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TemplateGuardAnalyzer : DiagnosticAnalyzer
    {
        private const string HelpBaseUrl = "docs/08-Extension-and-Customization-Guide.md";

        public static readonly DiagnosticDescriptor RuleTg001 = new(
            id: "TG001",
            title: "Cannot add members directly to locked base file",
            messageFormat: "Member '{0}' was added directly inside locked base file '{1}'. Direct modifications cause upstream merge conflicts. ALLOWED APPROACH: Create a partial class in '{2}.Custom.cs' alongside this file.",
            category: "Governance",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Direct modifications to base files cause upstream merge conflicts. Use .Custom.cs partial classes instead.",
            helpLinkUri: HelpBaseUrl + "#tg001");

        public static readonly DiagnosticDescriptor RuleTg002 = new(
            id: "TG002",
            title: "Modified base method is missing [BaseModification] attribute",
            messageFormat: "Base method '{0}' in locked file '{1}' was modified without the required [BaseModification] attribute. (Actual hash: {2}, Expected: {3}) ALLOWED APPROACH: Override this method in a derived service subclass or apply [BaseModification].",
            category: "Governance",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Modifications to base methods must be audited via [BaseModification].",
            helpLinkUri: HelpBaseUrl + "#tg002");

        public static readonly DiagnosticDescriptor RuleTg003 = new(
            id: "TG003",
            title: "Member name contains underscore (CA1707 violation)",
            messageFormat: "Member '{0}' in '{1}' contains an underscore, violating .NET Framework Design Guidelines (CA1707). ALLOWED APPROACH: Use standard PascalCase without underscores.",
            category: "Governance",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Do not use Hungarian notation or underscores in identifier names.",
            helpLinkUri: HelpBaseUrl + "#tg003");

        public static readonly DiagnosticDescriptor RuleTg004 = new(
            id: "TG004",
            title: "Modified base Razor component is missing [BaseModification] annotation",
            messageFormat: "Base Razor component in locked file '{0}' was modified without the required [BaseModification] annotation. ALLOWED APPROACH: Compose in a custom component, or apply '@attribute [BaseModification(Author = \"...\", Reason = \"...\", Ticket = \"...\")]' at the top of the file.",
            category: "Governance",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Direct modifications to locked template Razor files must be audited via [BaseModification].",
            helpLinkUri: HelpBaseUrl + "#tg004");

        public static readonly DiagnosticDescriptor RuleTg005 = new(
            id: "TG005",
            title: "Razor page route missing explicit clearance or authorization attribute",
            messageFormat: "Page component '{0}' with route '{1}' must declare an explicit security attribute ([RequiredClearance], [Authorize], or whitelisted [AllowAnonymous]). Route '{1}' is prohibited from using [AllowAnonymous].",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All @page routes must explicitly declare their required clearance or authorization tier. Anonymous access is strictly limited to whitelisted utility pages.",
            helpLinkUri: HelpBaseUrl + "#tg005");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RuleTg001, RuleTg002, RuleTg003, RuleTg004, RuleTg005);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var manifestFile = compilationContext.Options.AdditionalFiles
                    .FirstOrDefault(f => Path.GetFileName(f.Path).Equals(".basefiles.json", StringComparison.OrdinalIgnoreCase));

                if (manifestFile == null) return;

                var text = manifestFile.GetText(compilationContext.CancellationToken)?.ToString();
                if (string.IsNullOrEmpty(text)) return;

                BaseManifestModel? manifest;
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    manifest = JsonSerializer.Deserialize<BaseManifestModel>(text!, options);
                }
                catch
                {
                    return;
                }

                if (manifest?.Files == null) return;

                compilationContext.RegisterSymbolAction(ctx => AnalyzeMember(ctx, manifest),
                    SymbolKind.Method, SymbolKind.Property, SymbolKind.Field);

                compilationContext.RegisterAdditionalFileAction(fileCtx => AnalyzeAdditionalFile(fileCtx, manifest));
            });
        }

        private void AnalyzeMember(SymbolAnalysisContext context, BaseManifestModel manifest)
        {
            var symbol = context.Symbol;
            var location = symbol.Locations.FirstOrDefault();
            if (location == null || !location.IsInSource) return;

            var fullPath = location.SourceTree?.FilePath;
            if (string.IsNullOrEmpty(fullPath)) return;

            var fileName = Path.GetFileName(fullPath);

            if (symbol.IsImplicitlyDeclared || symbol.Name.StartsWith("<")) return;

            if (symbol is IMethodSymbol method && 
                (method.MethodKind == MethodKind.PropertyGet || 
                 method.MethodKind == MethodKind.PropertySet || 
                 method.MethodKind == MethodKind.EventAdd || 
                 method.MethodKind == MethodKind.EventRemove ||
                 method.MethodKind == MethodKind.Constructor ||
                 method.MethodKind == MethodKind.StaticConstructor))
            {
                return;
            }

            // Rule TG003: Check for forbidden Hungarian '_Ext' suffix in public or internal identifiers
            if ((symbol.DeclaredAccessibility == Accessibility.Public || symbol.DeclaredAccessibility == Accessibility.Internal) &&
                symbol.Name.EndsWith("_Ext", StringComparison.OrdinalIgnoreCase))
            {
                context.ReportDiagnostic(Diagnostic.Create(RuleTg003, location, symbol.Name, fileName));
            }

            var layerRelPath = NormalizeToLayerRelativePath(fullPath!);
            BaseFileEntry? matchedEntry = null;
            if (layerRelPath != null)
            {
                manifest.Files.TryGetValue(layerRelPath, out matchedEntry);
            }

            if (matchedEntry != null)
            {
                var containingTypeName = symbol.ContainingType?.Name ?? "Class";

                var memberKey = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

                // Check Rule TG001: New member added directly inside locked base file
                if (!matchedEntry.KnownMembers.Contains(symbol.Name) && !matchedEntry.KnownMembers.Contains(memberKey))
                {
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg001, location, symbol.Name, fileName, containingTypeName));
                    return;
                }

                // Check Rule TG002: Base method modified without [BaseModification]
                if (symbol is IMethodSymbol methodSymbol && methodSymbol.DeclaringSyntaxReferences.Length > 0)
                {
                    var syntax = methodSymbol.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken);
                    var currentHash = ComputeSyntaxHash(syntax);

                    string? baseHash = null;
                    if (matchedEntry.MemberSyntaxHashes.TryGetValue(memberKey, out baseHash) ||
                        matchedEntry.MemberSyntaxHashes.TryGetValue(symbol.Name, out baseHash))
                    {
                        if (!string.Equals(currentHash, baseHash, StringComparison.Ordinal))
                        {
                            var hasBaseModAttr = symbol.GetAttributes()
                                .Any(a => a.AttributeClass?.Name.Equals("BaseModificationAttribute", StringComparison.Ordinal) == true);

                            if (!hasBaseModAttr)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(RuleTg002, location, symbol.Name, fileName, currentHash, baseHash));
                            }
                        }
                    }
                }
            }
        }

        private static string ComputeSyntaxHash(SyntaxNode node)
        {
            var normalized = node.NormalizeWhitespace().ToString().Replace("null !", "null!");
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string? NormalizeToLayerRelativePath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var layers = new[] { "UI.Templates", "Core", "Persistence", "Infrastructure", "Jobs", "Web" };
            foreach (var layer in layers)
            {
                var marker = "." + layer + "/";
                var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return layer + "/" + normalized.Substring(idx + marker.Length);
                }

                var directMarker = "/" + layer + "/";
                idx = normalized.IndexOf(directMarker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return layer + "/" + normalized.Substring(idx + directMarker.Length);
                }
            }
            return null;
        }

        private void AnalyzeAdditionalFile(AdditionalFileAnalysisContext context, BaseManifestModel manifest)
        {
            var file = context.AdditionalFile;
            if (!file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) return;

            var layerRelPath = NormalizeToLayerRelativePath(file.Path);
            if (layerRelPath == null) return;

            if (!manifest.Files.TryGetValue(layerRelPath, out var matchedEntry) || matchedEntry == null) return;

            if (!matchedEntry.MemberSyntaxHashes.TryGetValue("$Markup", out var baseMarkupHash) || string.IsNullOrEmpty(baseMarkupHash)) return;

            var text = file.GetText(context.CancellationToken);
            if (text == null) return;

            var rawContent = text.ToString();
            var currentHash = ComputeRazorHash(rawContent);

            if (!string.Equals(currentHash, baseMarkupHash, StringComparison.Ordinal))
            {
                var hasBaseMod = rawContent.IndexOf("[BaseModification", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hasBaseMod)
                {
                    var fileName = Path.GetFileName(file.Path);
                    var location = Location.Create(file.Path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg004, location, fileName));
                }
            }
        }

        private static string ComputeRazorHash(string content)
        {
            var lines = content.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.TrimEnd());
            var normalized = string.Join("\n", lines).Trim();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static readonly System.Collections.Generic.HashSet<string> WhitelistedAnonymousRoutes = new(StringComparer.OrdinalIgnoreCase)
        {
            "/login",
            "/error",
            "/auth-error",
            "/not-found"
        };

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol namedType || namedType.TypeKind != TypeKind.Class) return;

            var routeAttributes = namedType.GetAttributes()
                .Where(a => a.AttributeClass?.Name == "RouteAttribute" || a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Components.RouteAttribute")
                .ToList();

            if (routeAttributes.Count == 0) return;

            foreach (var routeAttr in routeAttributes)
            {
                var routeTemplate = routeAttr.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;
                var normalizedRoute = routeTemplate.Trim().ToLowerInvariant();

                var hasAllowAnonymous = namedType.GetAttributes().Any(a => a.AttributeClass?.Name is "AllowAnonymousAttribute" or "AllowAnonymous");
                var hasAuthorize = namedType.GetAttributes().Any(a => a.AttributeClass?.Name is "AuthorizeAttribute" or "Authorize");
                var hasClearance = namedType.GetAttributes().Any(a => a.AttributeClass?.Name is "RequiredClearanceAttribute" or "RequiredClearance");

                if (hasAllowAnonymous)
                {
                    if (!WhitelistedAnonymousRoutes.Contains(normalizedRoute))
                    {
                        var location = routeAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation() 
                                       ?? namedType.Locations.FirstOrDefault() 
                                       ?? Location.None;

                        context.ReportDiagnostic(Diagnostic.Create(
                            RuleTg005,
                            location,
                            namedType.Name,
                            routeTemplate));
                    }
                }
                else if (!hasAuthorize && !hasClearance)
                {
                    var location = routeAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation() 
                                   ?? namedType.Locations.FirstOrDefault() 
                                   ?? Location.None;

                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleTg005,
                        location,
                        namedType.Name,
                        routeTemplate));
                }
            }
        }
    }
}
