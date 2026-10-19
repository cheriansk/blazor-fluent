using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BlazorFluent.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

/// <summary>
/// Architecture and security regression tests enforcing deny-by-default page authorization,
/// zero raw IgnoreQueryFilters bypasses, and zero hardcoded identities.
/// </summary>
public class DenyByDefaultAndDevLoginTests
{
    private static string? GetSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlazorFluent.slnx")) ||
                File.Exists(Path.Combine(current.FullName, "BlazorFluent.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }

    [Fact]
    public void DevLoginOptions_DefaultValues_AreDisabledAndEmpty()
    {
        var options = new DevLoginOptions();

        options.Enabled.Should().BeFalse();
        options.Users.Should().BeEmpty();
        DevLoginOptions.SectionName.Should().Be("Auth:DevLogin");
    }

    [Fact]
    public void DevelopmentSeedOptions_DefaultValues_AreDisabledAndEmpty()
    {
        var options = new DevelopmentSeedOptions();

        options.Enabled.Should().BeFalse();
        options.TenantSlug.Should().BeEmpty();
        options.AdminEmail.Should().BeEmpty();
        DevelopmentSeedOptions.SectionName.Should().Be("Seed");
    }

    [Fact]
    public void ArchitectureGuard_ZeroRawIgnoreQueryFiltersInBusinessCode()
    {
        var root = GetSolutionRoot();
        if (root == null) return; // Skip if run outside repository context

        var srcDir = Path.Combine(root, "src");
        if (!Directory.Exists(srcDir)) return;

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("AuditedQueryableExtensions.cs") && !f.EndsWith("QueryFilters.cs"))
            .ToList();

        var violations = (from file in csFiles
                          let lines = File.ReadAllLines(file)
                          from indexedLine in lines.Select((text, index) => new { LineNumber = index + 1, Text = text })
                          where indexedLine.Text.Contains(".IgnoreQueryFilters(") && !indexedLine.Text.TrimStart().StartsWith("//") && !indexedLine.Text.TrimStart().StartsWith("///")
                          select $"{Path.GetFileName(file)}:L{indexedLine.LineNumber} -> {indexedLine.Text.Trim()}").ToList();

        violations.Should().BeEmpty(
            "direct .IgnoreQueryFilters() calls undermine multi-tenant isolation and forensic audit logging. Use AuditedQueryableExtensions instead.");
    }

    [Fact]
    public void ArchitectureGuard_AllRazorPagesRequireAuthorizationOrExplicitAllowAnonymous()
    {
        var root = GetSolutionRoot();
        if (root == null) return;

        var webPagesDir = Path.Combine(root, "src", "BlazorFluent.Web");
        if (!Directory.Exists(webPagesDir)) return;

        var allowedAnonymousPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LoginPage.razor",
            "AuthErrorPage.razor",
            "ErrorPage.razor",
            "NotFoundPage.razor"
        };

        var razorFiles = Directory.GetFiles(webPagesDir, "*.razor", SearchOption.AllDirectories);
        var unprotectedPages = new List<string>();

        foreach (var file in razorFiles)
        {
            var content = File.ReadAllText(file);
            var isPage = Regex.IsMatch(content, @"@page\s+""[^""]+""");
            if (!isPage) continue;

            var fileName = Path.GetFileName(file);
            var hasAuthorize = content.Contains("[Authorize") || content.Contains("@attribute [Authorize") ||
                               content.Contains("[RequiredClearance") || content.Contains("@attribute [RequiredClearance");
            var hasAllowAnonymous = content.Contains("[AllowAnonymous]") || content.Contains("@attribute [AllowAnonymous]");

            if (allowedAnonymousPages.Contains(fileName))
            {
                hasAllowAnonymous.Should().BeTrue($"page '{fileName}' is in the anonymous allowlist and must declare [AllowAnonymous].");
                continue;
            }

            if (!hasAuthorize)
            {
                unprotectedPages.Add(fileName);
            }
        }

        unprotectedPages.Should().BeEmpty(
            "every @page component in BlazorFluent.Web must be explicitly protected by [Authorize] under the deny-by-default security architecture.");
    }

    [Fact]
    public void ZeroTrustGuard_ZeroHardcodedIdentitiesInSourceCode()
    {
        var root = GetSolutionRoot();
        if (root == null) return;

        var srcDir = Path.Combine(root, "src");
        if (!Directory.Exists(srcDir)) return;

        var sourceFiles = Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".razor"))
            .ToList();

        var violations = (from file in sourceFiles
                          let lines = File.ReadAllLines(file)
                          from indexedLine in lines.Select((text, index) => new { LineNumber = index + 1, Text = text })
                          where indexedLine.Text.Contains("@blazorfluent.local", StringComparison.OrdinalIgnoreCase)
                          select $"{Path.GetFileName(file)}:L{indexedLine.LineNumber} -> {indexedLine.Text.Trim()}").ToList();

        violations.Should().BeEmpty(
            "hardcoded identity literals (@blazorfluent.local) violate zero-trust architecture. Identities must be configuration-driven.");
    }
}
