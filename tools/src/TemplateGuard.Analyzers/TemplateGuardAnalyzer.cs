using System.Text.RegularExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public static readonly DiagnosticDescriptor RuleTg006 = new(
            id: "TG006",
            title: "DataList enum type name must end with 'Enum' suffix",
            messageFormat: "Enum '{0}' in namespace '{1}' does not end with 'Enum' (e.g. '{0}Enum'). For cognitive readability and quick identification, all enums in BlazorEnterprise.Shared.DataListTypes must end with the 'Enum' suffix.",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All enums in BlazorEnterprise.Shared.DataListTypes must end with the 'Enum' suffix.",
            helpLinkUri: HelpBaseUrl + "#tg006");

        public static readonly DiagnosticDescriptor RuleTg007 = new(
            id: "TG007",
            title: "Domain entity class name must end with 'Entity' suffix",
            messageFormat: "Class '{0}' inherits from BaseEntity or is declared in a domain entity namespace but does not end with 'Entity' (e.g. '{0}Entity'). All domain entities must end with the 'Entity' suffix.",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All domain entities must end with the 'Entity' suffix for immediate cognitive recognition.",
            helpLinkUri: HelpBaseUrl + "#tg007");

        public static readonly DiagnosticDescriptor RuleTg008 = new(
            id: "TG008",
            title: "Domain entity must inherit BaseEntity and implement required scoping and lifecycle marker interfaces",
            messageFormat: "Entity '{0}' violates architectural requirements: {1}. ALLOWED APPROACH: Inherit BaseEntity, implement ITenantEntity or IGlobalEntity, and implement exactly one deletion lifecycle marker (ISoftDeletableEntity, IHardDeletableEntity, or IImmutableEntity).",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Domain entities must adhere to base class inheritance and marker interface scoping rules.",
            helpLinkUri: HelpBaseUrl + "#tg008");

        public static readonly DiagnosticDescriptor RuleTg009 = new(
            id: "TG009",
            title: "Concrete domain entity must have a companion FluentValidation validator",
            messageFormat: "Concrete domain entity '{0}' lacks a companion validator class (e.g. '{0}Validator' implementing AbstractValidator<{0}>). Every business entity must have a companion validator.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Every concrete domain entity must have a companion FluentValidation validator for fail-closed runtime safety.",
            helpLinkUri: HelpBaseUrl + "#tg009");

        public static readonly DiagnosticDescriptor RuleTg010 = new(
            id: "TG010",
            title: "Concrete domain entity must have a companion EF Core entity type configuration",
            messageFormat: "Concrete domain entity '{0}' lacks an EF Core configuration class implementing IEntityTypeConfiguration<{0}>. Every persisted domain entity must have a companion configuration.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Every persisted domain entity must have a companion IEntityTypeConfiguration<TEntity>.",
            helpLinkUri: HelpBaseUrl + "#tg010");

        public static readonly DiagnosticDescriptor RuleTg011 = new(
            id: "TG011",
            title: "Strict prohibition of DateOnly in domain entities",
            messageFormat: "Property '{0}.{1}' uses DateOnly. DateOnly is strictly prohibited in domain entities; all dates and timestamps must use UTC DateTime.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "DateOnly is prohibited across domain entities in favor of UTC DateTime.",
            helpLinkUri: HelpBaseUrl + "#tg011");

        public static readonly DiagnosticDescriptor RuleTg012 = new(
            id: "TG012",
            title: "Domain entity must not persist mutable user attributes without [NotMapped]",
            messageFormat: "Property '{0}.{1}' duplicates mutable user attribute '{1}'. Relational 3NF requires persisting foreign keys (e.g. UserEntityId) only; apply [NotMapped] to convenience navigation forwarders.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Domain entities must persist foreign keys only and decorate convenience navigation properties with [NotMapped].",
            helpLinkUri: HelpBaseUrl + "#tg012");

        public static readonly DiagnosticDescriptor RuleTg013 = new(
            id: "TG013",
            title: "Enum operations must use DataListExtensions extension methods",
            messageFormat: "Direct invocation of '{0}' on enum '{1}' violates architectural standards. ALLOWED APPROACH: Use DataListExtensions extension methods (.ToEnum<T>(), .TryToEnum<T>(), .GetEnumDisplayName<T>(), .GetEnumCode<T>(), .GetEnumCodesByCategory<T>(), or .GetDataListItems<T>()).",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Direct Enum parsing and raw reflection on enums must use centralized DataListExtensions.",
            helpLinkUri: HelpBaseUrl + "#tg013");

        public static readonly DiagnosticDescriptor RuleTg014 = new(
            id: "TG014",
            title: "Enum must declare taxonomy attribute [LifecycleEnum] or [ClassificationEnum]",
            messageFormat: "Enum '{0}' in namespace '{1}' must be decorated with either [LifecycleEnum] or [ClassificationEnum] to explicitly specify its domain taxonomy.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "All enums in BlazorEnterprise.Shared.DataListTypes must declare their taxonomy category via [LifecycleEnum] or [ClassificationEnum].",
            helpLinkUri: HelpBaseUrl + "#tg014");

        public static readonly DiagnosticDescriptor RuleTg015 = new(
            id: "TG015",
            title: "Lifecycle enum must declare companion definitions containing 'AllowedTransitions'",
            messageFormat: "Lifecycle enum '{0}' lacks a companion definitions class containing an 'AllowedTransitions' dictionary. State machines must define their legal transition matrix.",
            category: "Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Enums decorated with [LifecycleEnum] must declare a companion definitions class with an AllowedTransitions matrix.",
            helpLinkUri: HelpBaseUrl + "#tg015");

        public static readonly DiagnosticDescriptor RuleTg016 = new(
            id: "TG016",
            title: "PureStrict CSP Violation: Inline style attribute prohibited in Razor markup",
            messageFormat: "Razor component '{0}' contains inline style attribute '{1}' at line {2}. PureStrict CSP prohibits inline styles. ALLOWED APPROACH: Define CSS classes in app.css.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Inline styles (style= or Style=) violate enterprise PureStrict Content Security Policy.",
            helpLinkUri: HelpBaseUrl + "#tg016");

        public static readonly DiagnosticDescriptor RuleTg017 = new(
            id: "TG017",
            title: "PureStrict CSP Violation: Un-nonced <script> or <style> tag prohibited in Razor markup",
            messageFormat: "Razor component '{0}' contains an un-nonced <{1}> tag at line {2}. PureStrict CSP requires a cryptographic nonce. ALLOWED APPROACH: Use static nonced scripts in App.razor or include nonce=\"@nonce\".",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Un-nonced script and style blocks violate enterprise PureStrict Content Security Policy.",
            helpLinkUri: HelpBaseUrl + "#tg017");

        public static readonly DiagnosticDescriptor RuleTg018 = new(
            id: "TG018",
            title: "PureStrict CSP Violation: Prohibited inline JavaScript handler or javascript: URI",
            messageFormat: "Razor component '{0}' contains prohibited inline JavaScript '{1}' at line {2}. PureStrict CSP prohibits inline DOM execution. ALLOWED APPROACH: Use Blazor C# event directives (e.g. @onclick) and standard routing.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Inline DOM event handlers and javascript: URIs violate enterprise PureStrict Content Security Policy.",
            helpLinkUri: HelpBaseUrl + "#tg018");


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                RuleTg001, RuleTg002, RuleTg003, RuleTg004, RuleTg005, RuleTg006,
                RuleTg007, RuleTg008, RuleTg009, RuleTg010, RuleTg011, RuleTg012, RuleTg013,
                RuleTg014, RuleTg015, RuleTg016, RuleTg017, RuleTg018);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterAdditionalFileAction(AnalyzeRazorCsp);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var compilation = compilationContext.Compilation;
                var assemblyName = compilation.AssemblyName ?? string.Empty;

                bool isCoreAssembly = assemblyName.EndsWith(".Core", StringComparison.OrdinalIgnoreCase) ||
                                      assemblyName.Equals("BlazorEnterprise.Core", StringComparison.OrdinalIgnoreCase);

                bool isPersistenceAssembly = assemblyName.EndsWith(".Persistence", StringComparison.OrdinalIgnoreCase) ||
                                            assemblyName.Equals("BlazorEnterprise.Persistence", StringComparison.OrdinalIgnoreCase);

                var concreteEntitiesInSource = new ConcurrentBag<INamedTypeSymbol>();
                var validatorTargetTypes = new ConcurrentBag<string>();
                var configurationTargetTypes = new ConcurrentBag<string>();

                compilationContext.RegisterSymbolAction(symCtx =>
                {
                    if (symCtx.Symbol is not INamedTypeSymbol namedType || namedType.TypeKind != TypeKind.Class) return;
                    if (namedType.IsImplicitlyDeclared || namedType.Name.StartsWith("<")) return;

                    if (!namedType.IsAbstract && InheritsFrom(namedType, "BaseEntity") && namedType.DeclaringSyntaxReferences.Length > 0)
                    {
                        concreteEntitiesInSource.Add(namedType);
                    }

                    if (isCoreAssembly)
                    {
                        var genericBase = namedType.BaseType;
                        while (genericBase != null)
                        {
                            if (genericBase.Name == "AbstractValidator" && genericBase.TypeArguments.Length > 0)
                            {
                                validatorTargetTypes.Add(genericBase.TypeArguments[0].Name);
                            }
                            genericBase = genericBase.BaseType;
                        }

                        foreach (var iface in namedType.AllInterfaces)
                        {
                            if (iface.Name == "IValidator" && iface.TypeArguments.Length > 0)
                            {
                                validatorTargetTypes.Add(iface.TypeArguments[0].Name);
                            }
                        }

                        if (namedType.Name.EndsWith("Validator"))
                        {
                            var targetName = namedType.Name.Substring(0, namedType.Name.Length - "Validator".Length);
                            validatorTargetTypes.Add(targetName);
                        }
                    }

                    if (isPersistenceAssembly)
                    {
                        foreach (var iface in namedType.AllInterfaces)
                        {
                            if (iface.Name == "IEntityTypeConfiguration" && iface.TypeArguments.Length > 0)
                            {
                                configurationTargetTypes.Add(iface.TypeArguments[0].Name);
                            }
                        }

                        if (namedType.Name.EndsWith("Configuration"))
                        {
                            var targetName = namedType.Name.Substring(0, namedType.Name.Length - "Configuration".Length);
                            configurationTargetTypes.Add(targetName);
                            if (!targetName.EndsWith("Entity"))
                            {
                                configurationTargetTypes.Add(targetName + "Entity");
                            }
                        }
                    }
                }, SymbolKind.NamedType);

                compilationContext.RegisterCompilationEndAction(endCtx =>
                {
                    if (isCoreAssembly)
                    {
                        var validators = new HashSet<string>(validatorTargetTypes, StringComparer.Ordinal);
                        foreach (var entity in concreteEntitiesInSource)
                        {
                            if (!validators.Contains(entity.Name))
                            {
                                var location = entity.Locations.FirstOrDefault() ?? Location.None;
                                endCtx.ReportDiagnostic(Diagnostic.Create(RuleTg009, location, entity.Name));
                            }
                        }
                    }

                    if (isPersistenceAssembly)
                    {
                        var configurations = new HashSet<string>(configurationTargetTypes, StringComparer.Ordinal);
                        var allEntities = new List<INamedTypeSymbol>(concreteEntitiesInSource);

                        foreach (var refAssembly in endCtx.Compilation.SourceModule.ReferencedAssemblySymbols)
                        {
                            if (refAssembly.Name.EndsWith(".Core", StringComparison.OrdinalIgnoreCase) ||
                                refAssembly.Name.Equals("BlazorEnterprise.Core", StringComparison.OrdinalIgnoreCase))
                            {
                                CollectEntitiesFromNamespace(refAssembly.GlobalNamespace, allEntities);
                            }
                        }

                        foreach (var entity in allEntities)
                        {
                            if (!entity.IsAbstract && InheritsFrom(entity, "BaseEntity") && !configurations.Contains(entity.Name))
                            {
                                var appDbContextSymbol = endCtx.Compilation.GetTypeByMetadataName("BlazorEnterprise.Persistence.Context.AppDbContext");
                                var location = appDbContextSymbol?.Locations.FirstOrDefault() ?? Location.None;
                                endCtx.ReportDiagnostic(Diagnostic.Create(RuleTg010, location, entity.Name));
                            }
                        }
                    }
                });

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
            var layers = new[] { "Shared.UI", "Core", "Persistence", "Infrastructure", "Jobs", "Web" };
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

        private static readonly HashSet<string> WhitelistedAnonymousRoutes = new(StringComparer.OrdinalIgnoreCase)
        {
            "/login",
            "/error",
            "/auth-error",
            "/not-found"
        };

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol namedType) return;

            if (namedType.TypeKind == TypeKind.Enum)
            {
                AnalyzeEnum(context, namedType);
                return;
            }

            if (namedType.TypeKind != TypeKind.Class) return;

            AnalyzeEntity(context, namedType);

            AnalyzePageRoute(context, namedType);
        }

        private void AnalyzeEntity(SymbolAnalysisContext context, INamedTypeSymbol namedType)
        {
            if (namedType.IsImplicitlyDeclared || namedType.Name.StartsWith("<")) return;

            var ns = namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var isDomainNamespace = ns.Contains(".Core.Domain") || ns.StartsWith("BlazorEnterprise.Core.Domain");
            var isValueObjectNamespace = ns.EndsWith(".ValueObjects") || ns.Contains(".ValueObjects.");

            // Exclude value objects (e.g. Address) and nested definitions/validators
            if (isValueObjectNamespace) return;
            if (namedType.Name.EndsWith("Validator") || namedType.Name.EndsWith("Definitions")) return;

            // Exclude EF Core Migrations and ModelSnapshots (e.g. migration classes named *Entity inheriting Migration)
            if (InheritsFrom(namedType, "Migration") || InheritsFrom(namedType, "ModelSnapshot")) return;
            if (ns.Contains(".Migrations") || ns.EndsWith(".Migrations") || ns.Equals("Migrations", StringComparison.OrdinalIgnoreCase)) return;
            if (namedType.GetAttributes().Any(a => a.AttributeClass?.Name is "MigrationAttribute" or "DbContextAttribute")) return;
            if (namedType.Locations.Any(loc => loc.SourceTree?.FilePath != null && loc.SourceTree.FilePath.Replace('\\', '/').Contains("/Migrations/"))) return;

            var inheritsBaseEntity = InheritsFrom(namedType, "BaseEntity");
            var endsWithEntity = namedType.Name.EndsWith("Entity", StringComparison.Ordinal);

            if (!inheritsBaseEntity && !isDomainNamespace && !endsWithEntity) return;

            // Rule TG007: Entity name must end with 'Entity' suffix
            if ((inheritsBaseEntity || isDomainNamespace) && !endsWithEntity)
            {
                var location = namedType.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(RuleTg007, location, namedType.Name));
            }

            // Rule TG008: Inheritance & marker interfaces
            if (endsWithEntity || isDomainNamespace || inheritsBaseEntity)
            {
                AnalyzeEntityInheritanceAndMarkers(context, namedType, inheritsBaseEntity);
            }

            // Rule TG011 & TG012: Property invariants
            if (inheritsBaseEntity)
            {
                AnalyzeEntityProperties(context, namedType);
            }
        }

        private void AnalyzeEntityInheritanceAndMarkers(SymbolAnalysisContext context, INamedTypeSymbol namedType, bool inheritsBaseEntity)
        {
            if (namedType.Name == "BaseEntity") return;

            var location = namedType.Locations.FirstOrDefault() ?? Location.None;

            if (!inheritsBaseEntity)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg008,
                    location,
                    namedType.Name,
                    "must inherit from 'BaseEntity' (directly or via 'AuditableEntity' / 'TenantAuditableEntity')"));
                return;
            }

            // Rules below apply to concrete entity tables
            if (namedType.IsAbstract) return;

            var isTenant = ImplementsInterface(namedType, "ITenantEntity");
            var isGlobal = ImplementsInterface(namedType, "IGlobalEntity");

            if (!isTenant && !isGlobal)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg008,
                    location,
                    namedType.Name,
                    "must declare its tenancy boundary by implementing either 'ITenantEntity' (for tenant-scoped data) or 'IGlobalEntity' (for platform-wide data)"));
            }

            var isSoft = ImplementsInterface(namedType, "ISoftDeletableEntity");
            var isHard = ImplementsInterface(namedType, "IHardDeletableEntity");
            var isImmutable = ImplementsInterface(namedType, "IImmutableEntity");
            var markerCount = (isSoft ? 1 : 0) + (isHard ? 1 : 0) + (isImmutable ? 1 : 0);

            if (markerCount == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg008,
                    location,
                    namedType.Name,
                    "must implement exactly one deletion lifecycle marker: 'ISoftDeletableEntity', 'IHardDeletableEntity', or 'IImmutableEntity'"));
            }
            else if (markerCount > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg008,
                    location,
                    namedType.Name,
                    "implements multiple conflicting deletion markers. An entity must implement at most ONE of 'ISoftDeletableEntity', 'IHardDeletableEntity', or 'IImmutableEntity'"));
            }

            var isProjectScoped = ImplementsInterface(namedType, "IProjectScopedEntity");
            if (isProjectScoped && !isTenant)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg008,
                    location,
                    namedType.Name,
                    "implements 'IProjectScopedEntity' but violates multi-tenant architecture by not implementing 'ITenantEntity'. All project tables must be tenant-scoped"));
            }
        }

        private void AnalyzeEntityProperties(SymbolAnalysisContext context, INamedTypeSymbol namedType)
        {
            var isImmutable = ImplementsInterface(namedType, "IImmutableEntity");

            foreach (var member in namedType.GetMembers())
            {
                if (member is not IPropertySymbol property || property.IsImplicitlyDeclared) continue;

                // Rule TG011: Strict prohibition of DateOnly
                var propTypeName = property.Type.Name;
                if (property.Type is INamedTypeSymbol namedPropType && namedPropType.IsGenericType && namedPropType.Name == "Nullable")
                {
                    propTypeName = namedPropType.TypeArguments.FirstOrDefault()?.Name ?? propTypeName;
                }

                if (propTypeName == "DateOnly")
                {
                    var location = property.Locations.FirstOrDefault() ?? namedType.Locations.FirstOrDefault() ?? Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg011, location, namedType.Name, property.Name));
                }

                // Rule TG012: Mutable user attributes without [NotMapped]
                if (!isImmutable && (property.Name == "UserEmail" || property.Name == "UserName" || property.Name == "AuthorEmail" || property.Name == "AuthorName"))
                {
                    if (namedType.Name != "UserSessionEntity")
                    {
                        var hasNotMapped = property.GetAttributes().Any(a => a.AttributeClass?.Name is "NotMappedAttribute" or "NotMapped");
                        if (!hasNotMapped)
                        {
                            var location = property.Locations.FirstOrDefault() ?? namedType.Locations.FirstOrDefault() ?? Location.None;
                            context.ReportDiagnostic(Diagnostic.Create(RuleTg012, location, namedType.Name, property.Name));
                        }
                    }
                }
            }
        }

        private void AnalyzePageRoute(SymbolAnalysisContext context, INamedTypeSymbol namedType)
        {
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

        private void AnalyzeEnum(SymbolAnalysisContext context, INamedTypeSymbol enumSymbol)
        {
            var ns = enumSymbol.ContainingNamespace?.ToDisplayString();
            if (ns == null || (!ns.EndsWith(".Shared.DataListTypes") && ns != "Shared.DataListTypes")) return;

            var location = enumSymbol.Locations.FirstOrDefault() ?? Location.None;

            if (!enumSymbol.Name.EndsWith("Enum", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg006,
                    location,
                    enumSymbol.Name,
                    ns));
            }

            // Rule TG014: Taxonomy Attribute Law
            var attributes = enumSymbol.GetAttributes();
            var isLifecycle = attributes.Any(a => a.AttributeClass?.Name is "LifecycleEnumAttribute" or "LifecycleEnum");
            var isClassification = attributes.Any(a => a.AttributeClass?.Name is "ClassificationEnumAttribute" or "ClassificationEnum");

            if (!isLifecycle && !isClassification)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleTg014,
                    location,
                    enumSymbol.Name,
                    ns));
            }

            // Rule TG015: Lifecycle Enum Transition Matrix Law
            if (isLifecycle)
            {
                var containingTypes = enumSymbol.ContainingNamespace.GetTypeMembers();
                var expectedDefNames = new[] { $"{enumSymbol.Name}Definitions", $"{enumSymbol.Name}Meta" };
                var hasAllowedTransitions = containingTypes.Any(t =>
                    expectedDefNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase) &&
                    t.GetMembers().Any(m => m.Name.Equals("AllowedTransitions", StringComparison.OrdinalIgnoreCase)));

                if (!hasAllowedTransitions)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleTg015,
                        location,
                        enumSymbol.Name));
                }
            }
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not InvocationExpressionSyntax invocation) return;

            var filePath = invocation.SyntaxTree.FilePath?.Replace('\\', '/');
            if (filePath != null)
            {
                if (filePath.EndsWith("/DataListExtensions.cs", StringComparison.OrdinalIgnoreCase) ||
                    filePath.Contains("/BlazorEnterprise.Shared/Extensions/") ||
                    filePath.Contains("/Migrations/") ||
                    filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol) return;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null) return;

            // Rule TG013: Prohibit direct Enum.Parse, Enum.TryParse, Enum.GetValues, Enum.GetNames
            if (containingType.SpecialType == SpecialType.System_Enum ||
                (containingType.Name == "Enum" && containingType.ContainingNamespace?.ToDisplayString() == "System"))
            {
                if (methodSymbol.Name is "TryParse" or "Parse" or "GetValues" or "GetNames")
                {
                    var targetEnumName = methodSymbol.TypeArguments.FirstOrDefault()?.Name ?? "Enum";
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg013, invocation.GetLocation(), methodSymbol.Name, targetEnumName));
                }
            }
        }

        private static bool InheritsFrom(INamedTypeSymbol? symbol, string baseTypeName)
        {
            var current = symbol?.BaseType;
            while (current != null)
            {
                if (current.Name == baseTypeName) return true;
                current = current.BaseType;
            }
            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol? symbol, string interfaceName)
        {
            if (symbol == null) return false;
            return symbol.AllInterfaces.Any(i => i.Name == interfaceName);
        }

        private static void CollectEntitiesFromNamespace(INamespaceSymbol nsSymbol, List<INamedTypeSymbol> result)
        {
            foreach (var type in nsSymbol.GetTypeMembers())
            {
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && InheritsFrom(type, "BaseEntity"))
                {
                    result.Add(type);
                }
            }

            foreach (var subNs in nsSymbol.GetNamespaceMembers())
            {
                CollectEntitiesFromNamespace(subNs, result);
            }
        }

        private static readonly Regex InlineStyleRegex = new(
            @"(?<![\w@])(?:style|Style)\s*=\s*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ScriptOrStyleTagRegex = new(
            @"<(?<tag>script|style)\b(?![^>]*\bnonce\s*=)[^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DomJsHandlerRegex = new(
            @"(?<!@)\b(?:onload|onerror|onclick|onsubmit|onfocus|onblur|onkeydown|onkeyup|onkeypress|onmouseover|onmouseout|onmouseenter|onmouseleave)\s*=\s*[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex JavascriptUriRegex = new(
            @"\b(?:href|src|action)\s*=\s*[""']\s*javascript:",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static void AnalyzeRazorCsp(AdditionalFileAnalysisContext context)
        {
            var file = context.AdditionalFile;
            if (!file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) return;

            var text = file.GetText(context.CancellationToken);
            if (text == null) return;

            var fileName = Path.GetFileName(file.Path);
            bool inCodeBlock = false;
            bool inRazorComment = false;

            for (int i = 0; i < text.Lines.Count; i++)
            {
                var line = text.Lines[i];
                var lineStr = line.ToString();

                // Track multiline Razor comments: @* ... *@
                if (!inRazorComment && lineStr.Contains("@*"))
                {
                    if (!lineStr.Contains("*@"))
                    {
                        inRazorComment = true;
                        continue;
                    }
                }
                else if (inRazorComment)
                {
                    if (lineStr.Contains("*@"))
                    {
                        inRazorComment = false;
                    }
                    continue;
                }

                // Check for @code or @functions blocks
                var trimmed = lineStr.Trim();
                if (trimmed.StartsWith("@code", StringComparison.Ordinal) || trimmed.StartsWith("@functions", StringComparison.Ordinal))
                {
                    inCodeBlock = true;
                }

                // Only scan markup outside @code / @functions blocks
                if (inCodeBlock) continue;

                // 1. Inline Style Check (TG016)
                var styleMatches = InlineStyleRegex.Matches(lineStr);
                foreach (Match match in styleMatches)
                {
                    int commentStart = lineStr.IndexOf("@*");
                    int commentEnd = lineStr.IndexOf("*@");
                    if (commentStart >= 0 && commentEnd > commentStart && match.Index >= commentStart && match.Index <= commentEnd)
                    {
                        continue;
                    }

                    var span = new TextSpan(line.Start + match.Index, match.Length);
                    var linePosSpan = new LinePositionSpan(
                        new LinePosition(i, match.Index),
                        new LinePosition(i, match.Index + match.Length));
                    var location = Location.Create(file.Path, span, linePosSpan);
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg016, location, fileName, match.Value.Trim(), i + 1));
                }

                // 2. Un-nonced <script> or <style> tags (TG017)
                var tagMatches = ScriptOrStyleTagRegex.Matches(lineStr);
                foreach (Match match in tagMatches)
                {
                    var span = new TextSpan(line.Start + match.Index, match.Length);
                    var linePosSpan = new LinePositionSpan(
                        new LinePosition(i, match.Index),
                        new LinePosition(i, match.Index + match.Length));
                    var location = Location.Create(file.Path, span, linePosSpan);
                    var tagName = match.Groups["tag"].Value;
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg017, location, fileName, tagName, i + 1));
                }

                // 3. Inline DOM JS handler or javascript: URI (TG018)
                var domMatches = DomJsHandlerRegex.Matches(lineStr);
                foreach (Match match in domMatches)
                {
                    var span = new TextSpan(line.Start + match.Index, match.Length);
                    var linePosSpan = new LinePositionSpan(
                        new LinePosition(i, match.Index),
                        new LinePosition(i, match.Index + match.Length));
                    var location = Location.Create(file.Path, span, linePosSpan);
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg018, location, fileName, match.Value.Trim(), i + 1));
                }

                var jsUriMatches = JavascriptUriRegex.Matches(lineStr);
                foreach (Match match in jsUriMatches)
                {
                    var span = new TextSpan(line.Start + match.Index, match.Length);
                    var linePosSpan = new LinePositionSpan(
                        new LinePosition(i, match.Index),
                        new LinePosition(i, match.Index + match.Length));
                    var location = Location.Create(file.Path, span, linePosSpan);
                    context.ReportDiagnostic(Diagnostic.Create(RuleTg018, location, fileName, match.Value.Trim(), i + 1));
                }
            }
        }
    }
}
