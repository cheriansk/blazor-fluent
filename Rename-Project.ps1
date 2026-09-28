#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Renames the BlazorFluent template project to a new name.

.DESCRIPTION
    Cross-platform PowerShell 7+ script that performs a complete project rename:
    - Replaces all file contents (namespaces, using directives, string literals, config values)
    - Renames all .csproj, .slnx files
    - Renames all project directories
    - Cleans stale bin/obj folders
    - Performs pre-flight git safety check
    - Performs post-rename verification scan

    Run this script immediately after cloning the template, before writing any custom code.

.PARAMETER NewName
    The new PascalCase project name (e.g., "MyApp", "ContosoPortal").
    Must start with an uppercase letter and contain only letters and digits.

.PARAMETER OldName
    The old PascalCase project name to replace (defaults to "BlazorFluent").
    Allows renaming projects that have already been renamed once.

.PARAMETER Force
    Skips interactive confirmation prompts and git uncommitted changes warnings.
    Useful for automated CI/CD pipelines or scripted project scaffolding.

.EXAMPLE
    ./Rename-Project.ps1 -NewName "ContosoPortal"

.EXAMPLE
    ./Rename-Project.ps1 -NewName "ContosoPortal" -Force

.EXAMPLE
    ./Rename-Project.ps1 -OldName "ContosoPortal" -NewName "AcmeApp" -Force

.NOTES
    Requires PowerShell 7+ (cross-platform).
    This script performs irreversible in-place modifications.
#>

param(
    [Parameter(Mandatory, HelpMessage = "New PascalCase project name (e.g., 'MyApp')")]
    [ValidatePattern('^[A-Z][a-zA-Z0-9]+$', ErrorMessage = "New name must be PascalCase: start with uppercase letter, letters and digits only.")]
    [string]$NewName,

    [Parameter(Mandatory = $false, HelpMessage = "Old project name to replace (defaults to 'BlazorFluent')")]
    [ValidatePattern('^[A-Z][a-zA-Z0-9]+$', ErrorMessage = "Old name must be PascalCase: start with uppercase letter, letters and digits only.")]
    [string]$OldName = "BlazorFluent",

    [Parameter(Mandatory = $false, HelpMessage = "Skip interactive confirmation prompts and git uncommitted changes warnings")]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Constants ───────────────────────────────────────────────────────────────
$OldNameLower = $OldName.ToLower()
$NewNameLower = $NewName.ToLower()
$ScriptRoot   = $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

# ─── Counters ────────────────────────────────────────────────────────────────
$filesModified    = 0
$filesRenamed     = 0
$dirsRenamed      = 0
$binObjCleaned    = 0

# ─── Exclude patterns ───────────────────────────────────────────────────────
$excludeDirs = @("bin", "obj", ".git", ".vs", "node_modules", "logs")

function Should-Exclude {
    param([string]$Path)
    foreach ($dir in $excludeDirs) {
        if ($Path -match "(^|[\\/])$([regex]::Escape($dir))([\\/]|$)") {
            return $true
        }
    }
    return $false
}

# ─── Banner ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          BlazorFluent Template → Project Renamer           ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Old Name : $OldName ($OldNameLower)" -ForegroundColor Yellow
Write-Host "  New Name : $NewName ($NewNameLower)" -ForegroundColor Green
Write-Host "  Root     : $ScriptRoot" -ForegroundColor DarkGray
Write-Host ""

if ($NewName -eq $OldName) {
    Write-Host "  ERROR: New name is the same as the old name. Nothing to do." -ForegroundColor Red
    exit 1
}

# ─── Pre-flight Git Safety Check ─────────────────────────────────────────────
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitStatus = git status --porcelain 2>$null
    if ($gitStatus -and -not $Force) {
        Write-Host "  WARNING: Uncommitted git changes detected in this repository." -ForegroundColor Yellow
        Write-Host "  It is strongly recommended to commit or stash your changes before running the rename script." -ForegroundColor Yellow
        Write-Host ""
        $continueAnyway = Read-Host "  Do you want to continue anyway? (type 'yes')"
        if ($continueAnyway -ne "yes") {
            Write-Host "  Aborted. Please commit or stash your changes first." -ForegroundColor Yellow
            exit 0
        }
        Write-Host ""
    }
}

# ─── Confirmation Prompt ────────────────────────────────────────────────────
if (-not $Force) {
    Write-Host "  This will perform IRREVERSIBLE in-place modifications." -ForegroundColor Red
    Write-Host ""
    $confirm = Read-Host "  Type 'yes' to proceed"
    if ($confirm -ne "yes") {
        Write-Host "  Aborted." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 1: Replace file contents
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "━━━ Phase 1: Replacing file contents ━━━" -ForegroundColor Cyan

$extensions = @("*.cs", "*.csproj", "*.slnx", "*.razor", "*.json", "*.props",
                "*.yml", "*.yaml", "*.md", "*.js", "*.css", "*.html")

$targetFiles = @()
foreach ($ext in $extensions) {
    $found = Get-ChildItem -Path $ScriptRoot -Recurse -Filter $ext -File |
        Where-Object {
            $rel = $_.FullName.Substring($ScriptRoot.Length)
            -not (Should-Exclude $rel)
        }
    $targetFiles += $found
}

# Deduplicate
$targetFiles = $targetFiles | Sort-Object FullName -Unique

foreach ($file in $targetFiles) {
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    $original = $content

    # Pass 1: PascalCase replacement (e.g. BlazorFluent → NewName)
    $content = $content -creplace [regex]::Escape($OldName), $NewName

    # Pass 2: lowercase replacement (e.g. blazorfluent → newname)
    $content = $content -creplace [regex]::Escape($OldNameLower), $NewNameLower

    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -NoNewline -Encoding utf8
        $filesModified++
        $rel = $file.FullName.Substring($ScriptRoot.Length + 1)
        Write-Host "  Modified: $rel" -ForegroundColor DarkGreen
    }
}

Write-Host "  Files modified: $filesModified" -ForegroundColor Green
Write-Host ""

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 2: Rename files
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "━━━ Phase 2: Renaming files ━━━" -ForegroundColor Cyan

$filesToRename = Get-ChildItem -Path $ScriptRoot -Recurse -File |
    Where-Object {
        $rel = $_.FullName.Substring($ScriptRoot.Length)
        ($_.Name -like "*$OldName*") -and (-not (Should-Exclude $rel))
    } |
    Sort-Object { $_.FullName.Length } -Descending  # Deepest first

foreach ($file in $filesToRename) {
    $newFileName = $file.Name -creplace [regex]::Escape($OldName), $NewName
    if ($newFileName -ne $file.Name) {
        $newPath = Join-Path $file.DirectoryName $newFileName
        Rename-Item -Path $file.FullName -NewName $newFileName
        $filesRenamed++
        $rel = $file.FullName.Substring($ScriptRoot.Length + 1)
        Write-Host "  Renamed: $rel → $newFileName" -ForegroundColor DarkGreen
    }
}

Write-Host "  Files renamed: $filesRenamed" -ForegroundColor Green
Write-Host ""

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 3: Rename directories
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "━━━ Phase 3: Renaming directories ━━━" -ForegroundColor Cyan

$dirsToRename = Get-ChildItem -Path $ScriptRoot -Recurse -Directory |
    Where-Object {
        $rel = $_.FullName.Substring($ScriptRoot.Length)
        ($_.Name -like "*$OldName*") -and (-not (Should-Exclude $rel))
    } |
    Sort-Object { $_.FullName.Length } -Descending  # Deepest first to avoid parent conflicts

foreach ($dir in $dirsToRename) {
    $newDirName = $dir.Name -creplace [regex]::Escape($OldName), $NewName
    if ($newDirName -ne $dir.Name) {
        $newPath = Join-Path $dir.Parent.FullName $newDirName
        Rename-Item -Path $dir.FullName -NewName $newDirName
        $dirsRenamed++
        $rel = $dir.FullName.Substring($ScriptRoot.Length + 1)
        Write-Host "  Renamed: $rel → $newDirName" -ForegroundColor DarkGreen
    }
}

Write-Host "  Directories renamed: $dirsRenamed" -ForegroundColor Green
Write-Host ""

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 4: Clean bin/obj folders
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "━━━ Phase 4: Cleaning stale bin/obj folders ━━━" -ForegroundColor Cyan

$staleDirectories = Get-ChildItem -Path $ScriptRoot -Recurse -Directory |
    Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" } |
    Where-Object { $_.FullName -notmatch "([\\/])\.git([\\/])" }

foreach ($staleDir in $staleDirectories) {
    Remove-Item -Path $staleDir.FullName -Recurse -Force -ErrorAction SilentlyContinue
    $binObjCleaned++
    $rel = $staleDir.FullName.Substring($ScriptRoot.Length + 1)
    Write-Host "  Cleaned: $rel" -ForegroundColor DarkYellow
}

Write-Host "  Directories cleaned: $binObjCleaned" -ForegroundColor Green
Write-Host ""

# ═════════════════════════════════════════════════════════════════════════════
# PHASE 5: Post-rename verification
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "━━━ Phase 5: Verification scan ━━━" -ForegroundColor Cyan

$remainingHits = @()
foreach ($ext in $extensions) {
    $found = Get-ChildItem -Path $ScriptRoot -Recurse -Filter $ext -File |
        Where-Object {
            $rel = $_.FullName.Substring($ScriptRoot.Length)
            -not (Should-Exclude $rel)
        }
    foreach ($f in $found) {
        $c = Get-Content -Path $f.FullName -Raw -ErrorAction SilentlyContinue
        if ($c -and ($c -cmatch [regex]::Escape($OldName))) {
            $remainingHits += $f.FullName.Substring($ScriptRoot.Length + 1)
        }
    }
}

if ($remainingHits.Count -gt 0) {
    Write-Host "  WARNING: Found $($remainingHits.Count) file(s) still containing '$OldName':" -ForegroundColor Yellow
    foreach ($hit in $remainingHits) {
        Write-Host "    - $hit" -ForegroundColor Yellow
    }
} else {
    Write-Host "  All clear! No remaining '$OldName' references found." -ForegroundColor Green
}

Write-Host ""

# ═════════════════════════════════════════════════════════════════════════════
# Summary
# ═════════════════════════════════════════════════════════════════════════════
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    Rename Complete!                        ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Summary:" -ForegroundColor White
Write-Host "    Files modified    : $filesModified" -ForegroundColor Green
Write-Host "    Files renamed     : $filesRenamed" -ForegroundColor Green
Write-Host "    Dirs renamed      : $dirsRenamed" -ForegroundColor Green
Write-Host "    bin/obj cleaned   : $binObjCleaned" -ForegroundColor Green
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor White
Write-Host "    1. Open $NewName.slnx in Visual Studio / Rider" -ForegroundColor DarkGray
Write-Host "    2. Run: dotnet restore" -ForegroundColor DarkGray
Write-Host "    3. Run: dotnet build" -ForegroundColor DarkGray
Write-Host "    4. Update Security:IntegritySecret in appsettings.json" -ForegroundColor DarkGray
Write-Host "    5. Update ConnectionStrings:DefaultConnection for your database" -ForegroundColor DarkGray
Write-Host ""
