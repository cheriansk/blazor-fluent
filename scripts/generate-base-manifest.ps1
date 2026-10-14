<#
.SYNOPSIS
    Generates the locked base manifest (tools/TemplateGuard/.basefiles.json)
    using the Roslyn-based TemplateGuard.Tool for 100% hash parity with the analyzer.
#>

param (
    [string]$RepoRoot = "$PSScriptRoot\.."
)

Write-Host "==> Generating Base Product Manifest with Roslyn Parser..." -ForegroundColor Cyan

$toolProject = "$PSScriptRoot\..\tools\src\TemplateGuard.Tool\TemplateGuard.Tool.csproj"
dotnet run --project $toolProject -- $RepoRoot

Write-Host "==> Base Manifest successfully updated!" -ForegroundColor Green
