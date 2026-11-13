<#
.SYNOPSIS
    Syncs modular AI skills across .gemini/skills, .cursor/skills, .claude/skills, and .github/skills.

.DESCRIPTION
    Designates .gemini/skills as the canonical master source for AI skills.
    Mirrors all skills to Cursor, Claude, and GitHub Copilot directories so all AI assistants
    share the exact same 11 skills without drift.
#>

[CmdletBinding()]
param(
    [string]$SourceDir = "$PSScriptRoot\..\.gemini\skills",
    [string[]]$TargetDirs = @(
        "$PSScriptRoot\..\.cursor\skills",
        "$PSScriptRoot\..\.claude\skills",
        "$PSScriptRoot\..\.github\skills"
    )
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SourceDir)) {
    Write-Error "Source skills directory not found: $SourceDir"
    exit 1
}

$resolvedSource = Resolve-Path $SourceDir
Write-Host "Syncing AI skills from canonical source: $resolvedSource" -ForegroundColor Cyan

foreach ($target in $TargetDirs) {
    if (-not (Test-Path $target)) {
        New-Item -ItemType Directory -Path $target -Force | Out-Null
    }

    Copy-Item -Path "$SourceDir\*" -Destination $target -Recurse -Force
    $targetCount = (Get-ChildItem -Path $target -Directory).Count
    Write-Host "  -> Synced to: $target ($targetCount skills)" -ForegroundColor Green
}

Write-Host "AI Skills synchronization complete across all AI assistants." -ForegroundColor Cyan
