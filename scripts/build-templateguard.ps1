<#
.SYNOPSIS
    Builds the TemplateGuard.Analyzers project into tools/TemplateGuard/TemplateGuard.Analyzers.dll
#>

param (
    [string]$Configuration = "Release"
)

Write-Host "==> Compiling TemplateGuard.Analyzers..." -ForegroundColor Cyan

$projectPath = "$PSScriptRoot\..\tools\src\TemplateGuard.Analyzers\TemplateGuard.Analyzers.csproj"
$outputDir   = "$PSScriptRoot\..\tools\TemplateGuard"

if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "To compile the analyzer, run:" -ForegroundColor Yellow
Write-Host "dotnet build `"$projectPath`" -c $Configuration -o `"$outputDir`"" -ForegroundColor White
