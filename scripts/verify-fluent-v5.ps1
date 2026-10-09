# ==============================================================================
# verify-fluent-v5.ps1
# Automated verification scanner ensuring 100% compliance with Microsoft Fluent UI Blazor V5.
# Fails (exit code 1) if any legacy V4 components, deprecated tags, or obsolete patterns are detected.
# ==============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " [V5 AUDIT] Scanning solution for deprecated Fluent UI Blazor V4 tokens" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$forbiddenPatterns = @(
    @{ Pattern = "<FluentProgressRing\b"; Description = "Legacy 'FluentProgressRing' (use '<FluentSpinner>' instead)" },
    @{ Pattern = "<FluentSearch\b"; Description = "Removed 'FluentSearch' (use '<FluentTextInput>' with StartTemplate search icon)" },
    @{ Pattern = "<FluentTextField\b"; Description = "Removed 'FluentTextField' (use '<FluentTextInput>' instead)" },
    @{ Pattern = "<FluentNumberField\b"; Description = "Removed 'FluentNumberField' (use '<FluentNumberInput>' instead)" },
    @{ Pattern = "<FluentAnchor\b"; Description = "Removed 'FluentAnchor' (use '<FluentLink>' instead)" },
    @{ Pattern = "<FluentBreadcrumb\b"; Description = "Removed 'FluentBreadcrumb' (use '<FluentLink>' navigation hierarchy)" },
    @{ Pattern = "<FluentBreadcrumbItem\b"; Description = "Removed 'FluentBreadcrumbItem'" },
    @{ Pattern = "<FluentDesignTheme\b"; Description = "Removed 'FluentDesignTheme'" },
    @{ Pattern = "<FluentDesignSystemProvider\b"; Description = "Removed 'FluentDesignSystemProvider'" },
    @{ Pattern = "<FluentNavMenu\b"; Description = "Removed 'FluentNavMenu' (use '<FluentNav>' instead)" },
    @{ Pattern = "<FluentNavGroup\b"; Description = "Removed 'FluentNavGroup' (use '<FluentNavCategory>' instead)" },
    @{ Pattern = "<FluentNavLink\b"; Description = "Removed 'FluentNavLink' (use '<FluentNavItem>' instead)" },
    @{ Pattern = "<FluentSplitter\b(?![A-Za-z])"; Description = "Removed 'FluentSplitter' (use '<FluentMultiSplitter>' instead)" },
    @{ Pattern = "<FluentToolbar\b"; Description = "Removed 'FluentToolbar' (use '<FluentStack Orientation=""Orientation.Horizontal"">' instead)" },
    @{ Pattern = "<FluentValidationMessage\b"; Description = "Removed 'FluentValidationMessage' (use '<FluentField>' instead)" },
    @{ Pattern = "<FluentFlipper\b"; Description = "Removed 'FluentFlipper' (use '<FluentButton>' with chevron icon)" },
    @{ Pattern = "<FluentHorizontalScroll\b"; Description = "Removed 'FluentHorizontalScroll' (use CSS overflow-x: auto)" },
    @{ Pattern = "<FluentCollapsibleRegion\b"; Description = "Removed 'FluentCollapsibleRegion' (use Blazor conditional rendering)" },
    @{ Pattern = "<FluentProfileMenu\b"; Description = "Removed 'FluentProfileMenu' (use FluentPopover + FluentAvatar + FluentButton)" },
    @{ Pattern = "<FluentEditForm\b"; Description = "Removed 'FluentEditForm' (use standard Blazor '<EditForm>' with FluentField)" },
    @{ Pattern = "FluentInputAppearance"; Description = "Removed enum 'FluentInputAppearance' (use 'TextInputAppearance' or 'ListAppearance')" }
)

$targetFiles = Get-ChildItem -Path . -Recurse -Include *.razor, *.cs | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|artifacts)[\\/]'
}

$violationsFound = 0

foreach ($rule in $forbiddenPatterns) {
    $pattern = $rule.Pattern
    $desc = $rule.Description

    foreach ($file in $targetFiles) {
        $matches = Select-String -Path $file.FullName -Pattern $pattern
        if ($matches) {
            foreach ($match in $matches) {
                Write-Host " VIOLATION: $($file.FullName):$($match.LineNumber)" -ForegroundColor Red
                Write-Host "   Pattern: $pattern ($desc)" -ForegroundColor Yellow
                Write-Host "   Snippet: $($match.Line.Trim())" -ForegroundColor Gray
                $violationsFound++
            }
        }
    }
}

Write-Host "----------------------------------------------------------------------" -ForegroundColor Cyan

if ($violationsFound -eq 0) {
    Write-Host "SUCCESS: All Razor and C# files strictly comply with Fluent UI Blazor V5!" -ForegroundColor Green
    Write-Host "Scanned $($targetFiles.Count) files. Zero legacy V4 patterns detected." -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILURE: Detected $violationsFound legacy Fluent UI V4 violation(s)." -ForegroundColor Red
    Write-Host "Please migrate the above components to their V5 replacements." -ForegroundColor Red
    exit 1
}
