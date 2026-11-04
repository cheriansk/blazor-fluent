<#
.SYNOPSIS
    Authoring tool: Builds TemplateGuard.Analyzers project into tools/TemplateGuard/TemplateGuard.Analyzers.dll
    and produces BlazorEnterprise.TemplateGuard.*.nupkg directly in tools/TemplateGuard.
#>

param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Compiling TemplateGuard.Analyzers..." -ForegroundColor Cyan

$projectPath = "$PSScriptRoot\..\tools\src\TemplateGuard.Analyzers\TemplateGuard.Analyzers.csproj"
$outputDir   = "$PSScriptRoot\..\tools\TemplateGuard"

if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Regenerate the base manifest first so the analyzer embeds an up-to-date baseline
$toolProject = "$PSScriptRoot\..\tools\src\TemplateGuard.Tool\TemplateGuard.Tool.csproj"
Write-Host "==> Regenerating base manifest..." -ForegroundColor Cyan
dotnet run --project $toolProject -- "$PSScriptRoot\.."
if ($LASTEXITCODE -ne 0) { throw "Manifest generation failed." }

# 2. Build the analyzer project.
# Note: TemplateGuard.Analyzers.csproj automatically copies $(TargetPath) to tools/TemplateGuard
# via DeployAnalyzerBinary target, and generates the .nupkg directly into PackageOutputPath (tools/TemplateGuard).
Write-Host "==> Building analyzer and generating NuGet package..." -ForegroundColor Cyan
dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Analyzer build failed." }

Write-Host "==> TemplateGuard analyzer + manifest + nupkg rebuilt successfully. Commit tools/TemplateGuard/*." -ForegroundColor Green
