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
    The new project name (e.g., "MyApp", "commandcenter", "command_center").
    Must start with a letter or underscore and contain only letters, digits, and underscores.
    Note: Hyphens (-) are disallowed because C# namespaces do not support hyphens.

.PARAMETER OldName
    The old project name to replace (defaults to "BlazorFluent").
    Allows renaming projects that have already been renamed once.

.PARAMETER Force
    Skips interactive confirmation prompts and git uncommitted changes warnings.
    Useful for automated CI/CD pipelines or scripted project scaffolding.

.EXAMPLE
    ./Rename-Project.ps1 -NewName "ContosoPortal"

.EXAMPLE
    ./Rename-Project.ps1 -NewName "commandcenter"

.EXAMPLE
    ./Rename-Project.ps1 -NewName "command_center"

.EXAMPLE
    ./Rename-Project.ps1 -NewName "ContosoPortal" -Force

.EXAMPLE
    ./Rename-Project.ps1 -OldName "ContosoPortal" -NewName "AcmeApp" -Force

.NOTES
    Requires PowerShell 7+ or Windows PowerShell 5.1.
    This script performs irreversible in-place modifications.
#>

param(
    [Parameter(Mandatory = $false, HelpMessage = "New project name (e.g., 'MyApp', 'commandcenter', 'command_center')")]
    [ValidateScript({
        if ($_ -eq "" -or $_ -match '^[a-zA-Z_][a-zA-Z0-9_]+$') { return $true }
        throw "Invalid project name '$_'.`n`nFormat Requirements:`n  - Allowed characters : Letters (A-Z, a-z), digits (0-9), and underscores (_)`n  - Starting character : Must start with a letter or underscore`n  - Allowed symbols    : '_' (underscore) is the ONLY allowed symbol`n  - Disallowed symbols : Hyphens (-), spaces, dots, or special symbols`n                         (C# namespaces cannot contain hyphens or spaces)`n  - Valid examples     : 'CommandCenter', 'commandcenter', 'command_center', 'MyPortal_v2'"
    })]
    [string]$NewName = "",

    [Parameter(Mandatory = $false, HelpMessage = "Old project name to replace (defaults to 'BlazorFluent')")]
    [ValidateScript({
        if ($_ -match '^[a-zA-Z_][a-zA-Z0-9_]+$') { return $true }
        throw "Invalid old project name '$_'. Must start with a letter or underscore and contain only letters, digits, and underscores."
    })]
    [string]$OldName = "BlazorFluent",

    [Parameter(Mandatory = $false, HelpMessage = "Skip interactive confirmation prompts and git uncommitted changes warnings")]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Interactive Prompt for NewName if not provided --------------------------
if ([string]::IsNullOrWhiteSpace($NewName)) {
    Write-Host ""
    Write-Host "+==============================================================+" -ForegroundColor Cyan
    Write-Host "|          BlazorFluent Template -> Project Renamer           |" -ForegroundColor Cyan
    Write-Host "+==============================================================+" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Project Name Format Rules:" -ForegroundColor White
    Write-Host "  - Allowed characters : Letters (A-Z, a-z), digits (0-9), and underscores (_)" -ForegroundColor DarkGray
    Write-Host "  - Starting character : Must start with a letter or underscore" -ForegroundColor DarkGray
    Write-Host "  - Allowed symbols    : '_' (underscore) is the ONLY allowed symbol" -ForegroundColor Green
    Write-Host "  - Disallowed symbols : Hyphens (-), spaces, dots, or special symbols" -ForegroundColor Yellow
    Write-Host "                         (C# namespaces do not support hyphens or spaces)" -ForegroundColor DarkGray
    Write-Host "  - Valid examples     : MyPortalv2, myportalv2, myportal_v2, MyPortal_v2" -ForegroundColor Cyan
    Write-Host ""

    while ($true) {
        $inputName = Read-Host "  Enter new project name"
        if ($inputName -match '^[a-zA-Z_][a-zA-Z0-9_]+$') {
            $NewName = $inputName
            break
        }
        Write-Host ""
        Write-Host "  ERROR: '$inputName' is not a valid project name." -ForegroundColor Red
        Write-Host "  - Must start with a letter or underscore." -ForegroundColor Yellow
        Write-Host "  - Only letters, digits, and underscores (_) are allowed." -ForegroundColor Yellow
        Write-Host "  - Hyphens (-) and spaces are not allowed." -ForegroundColor Yellow
        Write-Host "  - Examples: 'MyPortalv2', 'myportalv2', 'myportal_v2'" -ForegroundColor Cyan
        Write-Host ""
    }
}

# --- Constants ---------------------------------------------------------------
$OldNameLower = $OldName.ToLower()
$NewNameLower = $NewName.ToLower()
$ScriptRoot   = $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = (Get-Location).Path }

# --- Counters -----------------------------------------------------------------
$filesModified    = 0
$filesRenamed     = 0
$dirsRenamed      = 0
$binObjCleaned    = 0

# --- Exclude patterns ---------------------------------------------------------
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

# --- Banner ------------------------------------------------------------------
Write-Host ""
Write-Host "+==============================================================+" -ForegroundColor Cyan
Write-Host "|          BlazorFluent Template -> Project Renamer           |" -ForegroundColor Cyan
Write-Host "+==============================================================+" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Old Name : $OldName ($OldNameLower)" -ForegroundColor Yellow
Write-Host "  New Name : $NewName ($NewNameLower)" -ForegroundColor Green
Write-Host "  Root     : $ScriptRoot" -ForegroundColor DarkGray
Write-Host ""

if ($NewName -ceq $OldName) {
    Write-Host "  ERROR: New name is the same as the old name. Nothing to do." -ForegroundColor Red
    exit 1
}

# --- Pre-flight Git Safety Check ---------------------------------------------
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

# --- Confirmation Prompt ------------------------------------------------------
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

# =============================================================================
# PHASE 1: Replace file contents
# =============================================================================
Write-Host "=== Phase 1: Replacing file contents ===" -ForegroundColor Cyan

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

    # Pass 1: PascalCase replacement (e.g. BlazorFluent -> NewName)
    $content = $content -creplace [regex]::Escape($OldName), $NewName

    # Pass 2: lowercase replacement (e.g. blazorfluent -> newname)
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

# =============================================================================
# PHASE 2: Rename files
# =============================================================================
Write-Host "=== Phase 2: Renaming files ===" -ForegroundColor Cyan

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
        Write-Host "  Renamed: $rel -> $newFileName" -ForegroundColor DarkGreen
    }
}

Write-Host "  Files renamed: $filesRenamed" -ForegroundColor Green
Write-Host ""

# =============================================================================
# PHASE 3: Rename directories
# =============================================================================
Write-Host "=== Phase 3: Renaming directories ===" -ForegroundColor Cyan

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
        Write-Host "  Renamed: $rel -> $newDirName" -ForegroundColor DarkGreen
    }
}

Write-Host "  Directories renamed: $dirsRenamed" -ForegroundColor Green
Write-Host ""

# =============================================================================
# PHASE 4: Clean bin/obj folders
# =============================================================================
Write-Host "=== Phase 4: Cleaning stale bin/obj folders ===" -ForegroundColor Cyan

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

# =============================================================================
# PHASE 5: Post-rename verification
# =============================================================================
Write-Host "=== Phase 5: Verification scan ===" -ForegroundColor Cyan

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

# =============================================================================
# PHASE 6: Git remote configuration (optional)
# =============================================================================
Write-Host "=== Phase 6: Git remote configuration (optional) ===" -ForegroundColor Cyan

$gitInstalled = (Get-Command git -ErrorAction SilentlyContinue) -ne $null
$isGitRepo = $false
if ($gitInstalled) {
    $insideWorkTree = (git rev-parse --is-inside-work-tree 2>$null)
    if ($insideWorkTree -eq "true") {
        $isGitRepo = $true
    }
}

if (-not $gitInstalled -or -not $isGitRepo) {
    Write-Host "  Skipping git remote setup (not a git repository or git not installed)." -ForegroundColor DarkGray
    Write-Host ""
} else {
    $existingRemotes = git remote -v 2>$null
    if ($existingRemotes) {
        Write-Host "  Current Git remotes:" -ForegroundColor DarkGray
        foreach ($line in ($existingRemotes | Select-Object -Unique)) {
            Write-Host "    $line" -ForegroundColor DarkGray
        }
        Write-Host ""
    }

    $setupGit = Read-Host "  Do you want to configure git remotes for your new project repository? (y/N)"
    if ($setupGit -eq "y" -or $setupGit -eq "yes") {
        Write-Host ""

        # Step 1: Handle existing template remote ('origin')
        $remotesList = @(git remote 2>$null)
        $hasOrigin = $remotesList -contains "origin"

        if ($hasOrigin) {
            Write-Host "  What would you like to do with the template remote ('origin' - 'https://github.com/cheriansk/blazor-fluent')?" -ForegroundColor White
            Write-Host "    1. Keep as 'template' (renames 'origin' -> 'template' to pull future template updates)" -ForegroundColor Cyan
            Write-Host "    2. Remove template remote completely (removes reference to this template repository)" -ForegroundColor Yellow
            Write-Host "    3. Keep 'origin' unchanged" -ForegroundColor DarkGray
            Write-Host ""
            $templateChoice = Read-Host "  Choice [1/2/3] (default: 1)"
            if (-not $templateChoice) { $templateChoice = "1" }

            switch ($templateChoice) {
                "1" {
                    if ($remotesList -contains "template") {
                        git remote remove template 2>$null
                    }
                    git remote rename origin template
                    Write-Host "  Renamed remote 'origin' -> 'template'" -ForegroundColor Green
                }
                "2" {
                    git remote remove origin
                    Write-Host "  Removed remote 'origin'" -ForegroundColor Yellow
                }
                default {
                    Write-Host "  Kept 'origin' remote unchanged." -ForegroundColor DarkGray
                }
            }
            Write-Host ""
        }

        # Step 2: Configure new 'origin' remote
        Write-Host "  Enter the Git URL for your new repository (e.g. https://github.com/username/my-repo.git)" -ForegroundColor White
        $newOriginUrl = Read-Host "  New 'origin' URL (leave blank to skip)"
        if (-not [string]::IsNullOrWhiteSpace($newOriginUrl)) {
            $newOriginUrl = $newOriginUrl.Trim()
            $currentRemotes = @(git remote 2>$null)
            if ($currentRemotes -contains "origin") {
                git remote set-url origin $newOriginUrl
                Write-Host "  Updated 'origin' remote -> $newOriginUrl" -ForegroundColor Green
            } else {
                git remote add origin $newOriginUrl
                Write-Host "  Added 'origin' remote -> $newOriginUrl" -ForegroundColor Green
            }
        } else {
            Write-Host "  Skipped setting new 'origin' remote." -ForegroundColor DarkGray
        }
        Write-Host ""
    } else {
        Write-Host "  Skipped git remote configuration." -ForegroundColor DarkGray
        Write-Host ""
    }
}

# =============================================================================
# Summary
# =============================================================================
Write-Host "+==============================================================+" -ForegroundColor Cyan
Write-Host "|                    Rename Complete!                          |" -ForegroundColor Cyan
Write-Host "+==============================================================+" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Summary:" -ForegroundColor White
Write-Host "    Files modified    : $filesModified" -ForegroundColor Green
Write-Host "    Files renamed     : $filesRenamed" -ForegroundColor Green
Write-Host "    Dirs renamed      : $dirsRenamed" -ForegroundColor Green
Write-Host "    bin/obj cleaned   : $binObjCleaned" -ForegroundColor Green

if ($gitInstalled -and $isGitRepo) {
    $finalRemotes = (git remote -v 2>$null | Select-Object -Unique)
    if ($finalRemotes) {
        Write-Host ""
        Write-Host "  Git Remotes:" -ForegroundColor White
        foreach ($r in $finalRemotes) {
            Write-Host "    $r" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "  Next steps:" -ForegroundColor White
Write-Host "    1. Open $NewName.slnx in Visual Studio / Rider" -ForegroundColor DarkGray
Write-Host "    2. Run: dotnet restore" -ForegroundColor DarkGray
Write-Host "    3. Run: dotnet build" -ForegroundColor DarkGray
Write-Host "    4. Review and commit your changes:" -ForegroundColor DarkGray
Write-Host "         git add ." -ForegroundColor Cyan
Write-Host "         git commit -m `"Scaffold project from BlazorFluent template`"" -ForegroundColor Cyan
Write-Host "         git push -u origin main" -ForegroundColor Cyan
Write-Host "    5. Update Security:IntegritySecret in appsettings.json" -ForegroundColor DarkGray
Write-Host "    6. Update ConnectionStrings:DefaultConnection for your database" -ForegroundColor DarkGray
Write-Host ""
