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

# Regenerate the manifest first so the analyzer embeds an up-to-date baseline.
$toolProject = "$PSScriptRoot\..\tools\src\TemplateGuard.Tool\TemplateGuard.Tool.csproj"
Write-Host "==> Regenerating base manifest..." -ForegroundColor Cyan
dotnet run --project $toolProject -- "$PSScriptRoot\.."
if ($LASTEXITCODE -ne 0) { throw "Manifest generation failed." }

dotnet build $projectPath -c $Configuration -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "Analyzer build failed." }
Write-Host "==> TemplateGuard analyzer + manifest rebuilt. Commit tools/TemplateGuard/*." -ForegroundColor Green
