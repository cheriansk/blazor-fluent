using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TemplateGuard.Analyzers.Models;

namespace TemplateGuard.Tool;

public static class Program
{
    public static void Main(string[] args)
    {
        var repoRoot = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var srcDir = Path.Combine(repoRoot, "src");
        var outputFile = Path.Combine(repoRoot, "tools", "TemplateGuard", ".basefiles.json");

        Console.WriteLine($"==> Generating Base Manifest using Roslyn AST parser...");
        Console.WriteLine($"==> Source: {srcDir}");
        Console.WriteLine($"==> Output: {outputFile}");

        var manifest = new BaseManifestModel();
        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("/bin/") &&
                        !f.Contains("\\obj\\") && !f.Contains("/obj/") &&
                        !f.EndsWith(".Custom.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        var layers = new[] { "Shared", "Core", "Persistence", "Infrastructure", "Jobs", "Web", "Shared.UI", "RulesEngine", "Api", "ServiceDefaults", "AppHost" };

        manifest.Layers = layers.ToList();

        foreach (var file in csFiles)
        {
            var layerRelPath = GetLayerRelativePath(file, layers);
            if (layerRelPath == null) continue;

            var code = File.ReadAllText(file);
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse);
            var tree = CSharpSyntaxTree.ParseText(code, parseOptions);
            var root = tree.GetRoot();

            var compilation = CSharpCompilation.Create("Gen", new[] { tree }, new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location)
            }, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            var model = compilation.GetSemanticModel(tree);

            var entry = new BaseFileEntry { RelativePath = layerRelPath };

            var typeDecls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
            foreach (var typeDecl in typeDecls)
            {
                var typeSymbol = model.GetDeclaredSymbol(typeDecl);
                if (typeSymbol != null)
                {
                    foreach (var m in typeSymbol.GetMembers())
                    {
                        entry.KnownMembers.Add(m.Name);
                        entry.KnownMembers.Add(m.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat));
                    }
                }
            }

            var records = root.DescendantNodes().OfType<RecordDeclarationSyntax>();
            foreach (var rec in records)
            {
                if (rec.ParameterList != null)
                {
                    foreach (var p in rec.ParameterList.Parameters)
                    {
                        entry.KnownMembers.Add(p.Identifier.Text);
                    }
                }
            }

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodSyntax in methods)
            {
                var declaredSymbol = model.GetDeclaredSymbol(methodSyntax);
                var memberKey = declaredSymbol != null
                    ? declaredSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)
                    : methodSyntax.Identifier.Text;

                entry.KnownMembers.Add(methodSyntax.Identifier.Text);
                entry.KnownMembers.Add(memberKey);

                var normalizedSyntax = methodSyntax.NormalizeWhitespace().ToString().Replace("null !", "null!");
                using var sha = SHA256.Create();
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedSyntax));
                var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                entry.MemberSyntaxHashes[memberKey] = hash;
            }

            manifest.Files[layerRelPath] = entry;
        }

        var razorFiles = Directory.GetFiles(srcDir, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("/bin/") &&
                        !f.Contains("\\obj\\") && !f.Contains("/obj/") &&
                        !f.EndsWith(".Custom.razor", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        foreach (var file in razorFiles)
        {
            var layerRelPath = GetLayerRelativePath(file, layers);
            if (layerRelPath == null) continue;

            var content = File.ReadAllText(file);
            var normalizedText = NormalizeRazorContent(content);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedText));
            var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            var entry = new BaseFileEntry { RelativePath = layerRelPath };
            entry.MemberSyntaxHashes["$Markup"] = hash;
            manifest.Files[layerRelPath] = entry;
        }

        var staticAssetFiles = Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".css", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) &&
                        !f.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".Custom.css", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".Custom.js", StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var n = f.Replace('\\', '/');
                return !n.Contains("/bin/") && !n.Contains("/obj/") && !n.Contains("/node_modules/") &&
                       !n.Contains("/wwwroot/lib/") && !n.Contains("/Migrations/");
            })
            .OrderBy(f => f)
            .ToList();

        foreach (var file in staticAssetFiles)
        {
            var layerRelPath = GetLayerRelativePath(file, layers);
            if (layerRelPath == null) continue;

            var normalizedText = NormalizeRazorContent(File.ReadAllText(file));
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedText))).Replace("-", "").ToLowerInvariant();

            var entry = new BaseFileEntry { RelativePath = layerRelPath };
            entry.MemberSyntaxHashes["$Content"] = hash;
            manifest.Files[layerRelPath] = entry;
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        
        if (File.Exists(outputFile) && File.ReadAllText(outputFile) == json)
        {
            Console.WriteLine($"==> Base manifest is up to date ({manifest.Files.Count} files). Timestamp preserved for fast builds.");
        }
        else
        {
            File.WriteAllText(outputFile, json);
            Console.WriteLine($"==> Successfully indexed {manifest.Files.Count} files ({csFiles.Count()} C#, {razorFiles.Count()} Razor, {staticAssetFiles.Count} CSS/JS) in {outputFile}");
        }
    }

    public static string? GetLayerRelativePath(string fullPath, string[] layers)
    {
        var normalized = fullPath.Replace('\\', '/');
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

    public static string NormalizeRazorContent(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd());
        return string.Join("\n", lines).Trim();
    }
}
