#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Renames the BlazorEnterprise template project to a new name.

.DESCRIPTION
    Cross-platform PowerShell 7+ script that performs a complete project rename:
    - Replaces all file contents (namespaces, using directives, string literals, config values)
    - Renames all .csproj, .slnx files
    - Renames all project directories in src/ and root
    - Cleans stale bin/obj folders
    - Performs pre-flight git safety check
    - Performs post-rename verification scan
    - Supports -DryRun preview mode to simulate changes without altering files

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

.PARAMETER DryRun
    Simulates the rename operation without making any modifications on disk.
    Displays what files would be modified, renamed, and cleaned.

.EXAMPLE
    ./scripts/Rename-Project.ps1 -NewName "ContosoPortal"

.EXAMPLE
    ./scripts/Rename-Project.ps1 -NewName "ContosoPortal" -DryRun

.EXAMPLE
    ./scripts/Rename-Project.ps1 -NewName "command_center" -Force

.EXAMPLE
    ./scripts/Rename-Project.ps1 -OldName "ContosoPortal" -NewName "AcmeApp" -Force

.NOTES
    Requires PowerShell 7+ or Windows PowerShell 5.1.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false, Position = 0, HelpMessage = "New project name (e.g., 'MyApp', 'commandcenter', 'command_center')")]
    [ValidateScript({
        if ($_ -eq "" -or $_ -match '^[a-zA-Z_][a-zA-Z0-9_]+$') { return $true }
        throw "Invalid project name '$_'.`n`nFormat Requirements:`n  - Allowed characters : Letters (A-Z, a-z), digits (0-9), and underscores (_)`n  - Starting character : Must start with a letter or underscore`n  - Allowed symbols    : '_' (underscore) is the ONLY allowed symbol`n  - Disallowed symbols : Hyphens (-), spaces, dots, or special symbols`n                         (C# namespaces cannot contain hyphens or spaces)`n  - Valid examples     : 'CommandCenter', 'commandcenter', 'command_center', 'MyPortal_v2'"
    })]
    [string]$NewName = "",

    [Parameter(Mandatory = $false, HelpMessage = "Old project name to replace (defaults to 'BlazorEnterprise')")]
    [ValidateScript({
        if ($_ -match '^[a-zA-Z_][a-zA-Z0-9_]+$') { return $true }
        throw "Invalid old project name '$_'. Must start with a letter or underscore and contain only letters, digits, and underscores."
    })]
    [string]$OldName = "BlazorEnterprise",

    [Parameter(Mandatory = $false, HelpMessage = "Skip interactive confirmation prompts and git uncommitted changes warnings")]
    [switch]$Force,

    [Parameter(Mandatory = $false, HelpMessage = "Preview rename changes without altering files on disk")]
    [switch]$DryRun,

    [Parameter(Mandatory = $false, HelpMessage = "Distribution Mode: 'Full' (all projects + Aspire) or 'ClientDistribution' (strips proprietary RulesEngine and Api)")]
    [ValidateSet("Full", "ClientDistribution")]
    [string]$Mode = "Full"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Determine Repository Root Dynamically -----------------------------------
# If invoked from scripts/ or any subdirectory, locate repo root via .slnx or src/
$CurrentLocation = (Get-Location).Path
$CandidateRoots = @()

if ($PSScriptRoot) {
    $CandidateRoots += $PSScriptRoot
    $CandidateRoots += (Split-Path -Parent $PSScriptRoot)
}
$CandidateRoots += $CurrentLocation
if ($CurrentLocation) {
    $CandidateRoots += (Split-Path -Parent $CurrentLocation)
}

$RepoRoot = $null
foreach ($cand in $CandidateRoots) {
    if (-not $cand) { continue }
    if ((Test-Path (Join-Path $cand "$OldName.slnx")) -or 
        (Test-Path (Join-Path $cand "src")) -or 
        (Get-ChildItem -Path $cand -Filter "*.slnx" -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
        $RepoRoot = (Resolve-Path $cand).Path
        break
    }
}

if (-not $RepoRoot) {
    $RepoRoot = if ($PSScriptRoot) { (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path } else { $CurrentLocation }
}

# --- Interactive Prompt for NewName if not provided --------------------------
if ([string]::IsNullOrWhiteSpace($NewName)) {
    Write-Host ""
    Write-Host "+==============================================================+" -ForegroundColor Cyan
    Write-Host "|          BlazorEnterprise Template -> Project Renamer           |" -ForegroundColor Cyan
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
Write-Host "|          BlazorEnterprise Template -> Project Renamer           |" -ForegroundColor Cyan
Write-Host "+==============================================================+" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Old Name : $OldName ($OldNameLower)" -ForegroundColor Yellow
Write-Host "  New Name : $NewName ($NewNameLower)" -ForegroundColor Green
Write-Host "  Repo Root: $RepoRoot" -ForegroundColor DarkGray
if ($DryRun) {
    Write-Host "  Mode     : DRY RUN (preview only - no files will be changed)" -ForegroundColor Magenta
}
Write-Host ""

if ($NewName -ceq $OldName) {
    Write-Host "  ERROR: New name is the same as the old name. Nothing to do." -ForegroundColor Red
    exit 1
}

# --- Pre-flight Git Safety Check ---------------------------------------------
if (-not $DryRun -and (Get-Command git -ErrorAction SilentlyContinue)) {
    $gitStatus = git -C $RepoRoot status --porcelain 2>$null
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
if (-not $Force -and -not $DryRun) {
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
                "*.yml", "*.yaml", "*.md", "*.js", "*.css", "*.html",
                "*.user", "*.ps1", "*.sql", "Dockerfile*")

# Exclude current script itself from self-modification during run
$currentScriptName = if ($MyInvocation.MyCommand.Name) { $MyInvocation.MyCommand.Name } else { "Rename-Project.ps1" }

$targetFiles = @()
foreach ($ext in $extensions) {
    $found = Get-ChildItem -Path $RepoRoot -Recurse -Filter $ext -File |
        Where-Object {
            $rel = $_.FullName.Substring($RepoRoot.Length)
            -not (Should-Exclude $rel) -and ($_.Name -ne $currentScriptName)
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
        $filesModified++
        $rel = $file.FullName.Substring($RepoRoot.Length + 1)
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would modify: $rel" -ForegroundColor Magenta
        } else {
            Set-Content -Path $file.FullName -Value $content -NoNewline -Encoding utf8
            Write-Host "  Modified: $rel" -ForegroundColor DarkGreen
        }
    }
}

Write-Host "  Files modified: $filesModified $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host ""

# Flip TemplateGuard to DownstreamEnforce mode
$buildProps = Join-Path $RepoRoot "Directory.Build.props"
if (Test-Path $buildProps) {
    $propsContent = Get-Content $buildProps -Raw
    if ($propsContent -match "<TemplateGuardMode>TemplateAuthoring</TemplateGuardMode>") {
        $propsContent = $propsContent.Replace("<TemplateGuardMode>TemplateAuthoring</TemplateGuardMode>", "<TemplateGuardMode>DownstreamEnforce</TemplateGuardMode>")
        if (-not $DryRun) {
            Set-Content -Path $buildProps -Value $propsContent -NoNewline -Encoding utf8
            Write-Host "  TemplateGuard switched to DownstreamEnforce mode." -ForegroundColor Green
        } else {
            Write-Host "  [DRY RUN] Would switch TemplateGuard to DownstreamEnforce mode." -ForegroundColor Magenta
        }
    }
}
Write-Host ""

# =============================================================================
# PHASE 2: Rename files
# =============================================================================
Write-Host "=== Phase 2: Renaming files ===" -ForegroundColor Cyan

$filesToRename = Get-ChildItem -Path $RepoRoot -Recurse -File |
    Where-Object {
        $rel = $_.FullName.Substring($RepoRoot.Length)
        ($_.Name -like "*$OldName*") -and (-not (Should-Exclude $rel)) -and ($_.Name -ne $currentScriptName)
    } |
    Sort-Object { $_.FullName.Length } -Descending  # Deepest first

foreach ($file in $filesToRename) {
    $newFileName = $file.Name -creplace [regex]::Escape($OldName), $NewName
    if ($newFileName -ne $file.Name) {
        $filesRenamed++
        $rel = $file.FullName.Substring($RepoRoot.Length + 1)
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would rename: $rel -> $newFileName" -ForegroundColor Magenta
        } else {
            Rename-Item -Path $file.FullName -NewName $newFileName
            Write-Host "  Renamed: $rel -> $newFileName" -ForegroundColor DarkGreen
        }
    }
}

Write-Host "  Files renamed: $filesRenamed $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host ""

# =============================================================================
# PHASE 3: Rename directories
# =============================================================================
Write-Host "=== Phase 3: Renaming directories ===" -ForegroundColor Cyan

$dirsToRename = Get-ChildItem -Path $RepoRoot -Recurse -Directory |
    Where-Object {
        $rel = $_.FullName.Substring($RepoRoot.Length)
        ($_.Name -like "*$OldName*") -and (-not (Should-Exclude $rel))
    } |
    Sort-Object { $_.FullName.Length } -Descending  # Deepest first to avoid parent conflicts

foreach ($dir in $dirsToRename) {
    $newDirName = $dir.Name -creplace [regex]::Escape($OldName), $NewName
    if ($newDirName -ne $dir.Name) {
        $dirsRenamed++
        $rel = $dir.FullName.Substring($RepoRoot.Length + 1)
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would rename dir: $rel -> $newDirName" -ForegroundColor Magenta
        } else {
            Rename-Item -Path $dir.FullName -NewName $newDirName
            Write-Host "  Renamed dir: $rel -> $newDirName" -ForegroundColor DarkGreen
        }
    }
}

Write-Host "  Directories renamed: $dirsRenamed $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host ""

# =============================================================================
# PHASE 4: Clean bin/obj folders
# =============================================================================
Write-Host "=== Phase 4: Cleaning stale bin/obj folders ===" -ForegroundColor Cyan

$staleDirectories = Get-ChildItem -Path $RepoRoot -Recurse -Directory |
    Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" } |
    Where-Object { $_.FullName -notmatch "([\\/])\.git([\\/])" }

foreach ($staleDir in $staleDirectories) {
    $binObjCleaned++
    $rel = $staleDir.FullName.Substring($RepoRoot.Length + 1)
    if ($DryRun) {
        Write-Host "  [DRY RUN] Would clean: $rel" -ForegroundColor Magenta
    } else {
        Remove-Item -Path $staleDir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Cleaned: $rel" -ForegroundColor DarkYellow
    }
}

Write-Host "  Directories cleaned: $binObjCleaned $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host ""

# =============================================================================
# PHASE 4.5: Strip Proprietary Cloud Projects (ClientDistribution Mode)
# =============================================================================
if ($Mode -eq "ClientDistribution") {
    Write-Host "=== Phase 4.5: Stripping Proprietary Cloud Projects (ClientDistribution Mode) ===" -ForegroundColor Yellow
    $proprietaryFolders = @(
        "src/$OldName.RulesEngine",
        "src/$OldName.Api",
        "src/$NewName.RulesEngine",
        "src/$NewName.Api"
    )
    foreach ($pf in $proprietaryFolders) {
        $fullPath = Join-Path $RepoRoot $pf
        if (Test-Path $fullPath) {
            if ($DryRun) {
                Write-Host "  [DRY RUN] Would remove proprietary project: $pf" -ForegroundColor Magenta
            } else {
                Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host "  Stripped proprietary project: $pf" -ForegroundColor Green
            }
        }
    }

    # Also strip RulesEngine and Api project entries from solution file in ClientDistribution mode
    $slnxFiles = Get-ChildItem -Path $RepoRoot -Filter "*.slnx" -File
    foreach ($slnx in $slnxFiles) {
        $slnxContent = Get-Content -Path $slnx.FullName -Raw
        $newSlnx = $slnxContent -replace '(?m)^\s*<Project Path="[^"]*RulesEngine[^"]*"\s*/>\r?\n', ''
        $newSlnx = $newSlnx -replace '(?m)^\s*<Project Path="[^"]*Api\.csproj[^"]*"\s*/>\r?\n', ''
        if (-not $DryRun) {
            Set-Content -Path $slnx.FullName -Value $newSlnx -NoNewline
            Write-Host "  Updated solution file: $($slnx.Name) (removed proprietary projects)" -ForegroundColor Green
        }
    }
    Write-Host ""
}

# =============================================================================
# PHASE 4.6: Purge Template Authoring Generator Tools (All Distributions)
# =============================================================================
Write-Host "=== Phase 4.6: Purging Template Authoring Generator Tools ===" -ForegroundColor Cyan

# 1. Clean up TemplateGuard.Tool generator and manifest update scripts (locks downstream against base regeneration)
$toolDir = Join-Path $RepoRoot "tools\src\TemplateGuard.Tool"
if (Test-Path $toolDir) {
    if (-not $DryRun) {
        Remove-Item -Path $toolDir -Recurse -Force
        Write-Host "  Removed TemplateGuard.Tool generator (prevent downstream base regeneration)." -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would remove: tools/src/TemplateGuard.Tool" -ForegroundColor Magenta
    }
}

$genScript = Join-Path $RepoRoot "scripts\generate-base-manifest.ps1"
if (Test-Path $genScript) {
    if (-not $DryRun) {
        Remove-Item -Path $genScript -Force
        Write-Host "  Removed scripts/generate-base-manifest.ps1." -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would remove: scripts/generate-base-manifest.ps1" -ForegroundColor Magenta
    }
}

$buildTgScript = Join-Path $RepoRoot "scripts\build-templateguard.ps1"
if (Test-Path $buildTgScript) {
    if (-not $DryRun) {
        Remove-Item -Path $buildTgScript -Force
        Write-Host "  Removed scripts/build-templateguard.ps1." -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would remove: scripts/build-templateguard.ps1" -ForegroundColor Magenta
    }
}

# 2. Strip TemplateGuard.Tool project entry from solution file
$slnxFiles = Get-ChildItem -Path $RepoRoot -Filter "*.slnx" -File
foreach ($slnx in $slnxFiles) {
    $slnxContent = Get-Content -Path $slnx.FullName -Raw
    if ($slnxContent -match 'TemplateGuard\.Tool') {
        $newSlnx = $slnxContent -replace '(?m)^\s*<Project Path="[^"]*TemplateGuard\.Tool[^"]*"\s*/>\r?\n', ''
        if (-not $DryRun) {
            Set-Content -Path $slnx.FullName -Value $newSlnx -NoNewline
            Write-Host "  Updated solution file: $($slnx.Name) (removed TemplateGuard.Tool authoring project)" -ForegroundColor Green
        } else {
            Write-Host "  [DRY RUN] Would remove TemplateGuard.Tool from $($slnx.Name)" -ForegroundColor Magenta
        }
    }
}

# 3. Strip AutoUpdateBaseManifestOnBuild target from Directory.Build.targets
$buildTargetsPath = Join-Path $RepoRoot "Directory.Build.targets"
if (Test-Path $buildTargetsPath) {
    $targetsContent = Get-Content -Path $buildTargetsPath -Raw
    if ($targetsContent -match 'AutoUpdateBaseManifestOnBuild') {
        # Strip AutoUpdate target and UpToDateCheck items for basefiles.json
        $newTargets = $targetsContent -replace '(?s)\s*<!-- Visual Studio FastUpToDateCheck.*?AutoUpdateBaseManifestOnBuild.*?</Target>', ''
        if (-not $DryRun) {
            Set-Content -Path $buildTargetsPath -Value $newTargets -NoNewline
            Write-Host "  Removed AutoUpdateBaseManifestOnBuild target from Directory.Build.targets." -ForegroundColor Green
        } else {
            Write-Host "  [DRY RUN] Would remove AutoUpdateBaseManifestOnBuild target from Directory.Build.targets." -ForegroundColor Magenta
        }
    }
}

# 4. Lock .basefiles.json as read-only on disk
$manifestFile = Join-Path $RepoRoot "tools\TemplateGuard\.basefiles.json"
if (Test-Path $manifestFile) {
    if (-not $DryRun) {
        Set-ItemProperty -Path $manifestFile -Name IsReadOnly -Value $true
        Write-Host "  Locked tools/TemplateGuard/.basefiles.json as ReadOnly." -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would set .basefiles.json to ReadOnly." -ForegroundColor Magenta
    }
}
Write-Host ""

# =============================================================================
# PHASE 5: Post-rename verification
# =============================================================================
Write-Host "=== Phase 5: Verification scan ===" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "  [DRY RUN] Skipping post-rename verification scan (dry-run mode)." -ForegroundColor Magenta
} else {
    $remainingHits = @()
    foreach ($ext in $extensions) {
        $found = Get-ChildItem -Path $RepoRoot -Recurse -Filter $ext -File |
            Where-Object {
                $rel = $_.FullName.Substring($RepoRoot.Length)
                -not (Should-Exclude $rel) -and ($_.Name -ne $currentScriptName)
            }
        foreach ($f in $found) {
            $c = Get-Content -Path $f.FullName -Raw -ErrorAction SilentlyContinue
            if ($c -and ($c -cmatch [regex]::Escape($OldName))) {
                $remainingHits += $f.FullName.Substring($RepoRoot.Length + 1)
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
}

Write-Host ""

# =============================================================================
# PHASE 6: Git remote and branch configuration (optional)
# =============================================================================
Write-Host "=== Phase 6: Git remote and branch configuration (optional) ===" -ForegroundColor Cyan

$gitInstalled = (Get-Command git -ErrorAction SilentlyContinue) -ne $null
$isGitRepo = $false
if ($gitInstalled) {
    $insideWorkTree = (git -C $RepoRoot rev-parse --is-inside-work-tree 2>$null)
    if ($insideWorkTree -eq "true") {
        $isGitRepo = $true
    }
}

if ($DryRun) {
    Write-Host "  [DRY RUN] Skipping git configuration (dry-run mode)." -ForegroundColor Magenta
    Write-Host ""
} elseif (-not $gitInstalled -or -not $isGitRepo) {
    Write-Host "  Skipping git configuration (not a git repository or git not installed)." -ForegroundColor DarkGray
    Write-Host ""
} else {
    $existingRemotes = git -C $RepoRoot remote -v 2>$null
    if ($existingRemotes) {
        Write-Host "  Current Git remotes:" -ForegroundColor DarkGray
        foreach ($line in ($existingRemotes | Select-Object -Unique)) {
            Write-Host "    $line" -ForegroundColor DarkGray
        }
        Write-Host ""
    }

    $setupGit = if ($Force) { "n" } else { Read-Host "  Do you want to configure git remotes and branches for your new project repository? (y/N)" }
    if ($setupGit -eq "y" -or $setupGit -eq "yes") {
        Write-Host ""

        # Step 1: Verify current branch is 'main'
        $currentBranch = (git -C $RepoRoot branch --show-current 2>$null)
        if ($currentBranch) { $currentBranch = $currentBranch.Trim() }
        if ($currentBranch -ne "main") {
            Write-Host "  ERROR: Expected current branch to be 'main', but found '$currentBranch'." -ForegroundColor Red
            Write-Host "  The template repository must be on the 'main' branch to initialize 'main_template' and 'develop'." -ForegroundColor Yellow
            Write-Host "  Please checkout the 'main' branch and re-run, or configure git manually." -ForegroundColor Yellow
            Write-Host ""
            exit 1
        }

        # Step 2: Handle existing template remote ('origin')
        $remotesList = @(git -C $RepoRoot remote 2>$null)
        $hasOrigin = $remotesList -contains "origin"

        if ($hasOrigin) {
            Write-Host "  What would you like to do with the template remote ('origin' - 'https://github.com/cheriansk/blazor-fluent')?" -ForegroundColor White
            Write-Host "    1. Keep as 'template' (read-only: renames 'origin' -> 'template' and disables push)" -ForegroundColor Cyan
            Write-Host "    2. Remove template remote completely (removes reference to this template repository)" -ForegroundColor Yellow
            Write-Host "    3. Keep 'origin' unchanged" -ForegroundColor DarkGray
            Write-Host ""
            $templateChoice = Read-Host "  Choice [1/2/3] (default: 1)"
            if (-not $templateChoice) { $templateChoice = "1" }

            switch ($templateChoice) {
                "1" {
                    if ($remotesList -contains "template") {
                        git -C $RepoRoot remote remove template 2>$null
                    }
                    git -C $RepoRoot remote rename origin template
                    git -C $RepoRoot remote set-url --push template "DISABLED_DO_NOT_PUSH"
                    Write-Host "  Renamed remote 'origin' -> 'template' (push disabled for safety)" -ForegroundColor Green
                }
                "2" {
                    git -C $RepoRoot remote remove origin
                    Write-Host "  Removed remote 'origin'" -ForegroundColor Yellow
                }
                default {
                    Write-Host "  Kept 'origin' remote unchanged." -ForegroundColor DarkGray
                }
            }
            Write-Host ""
        }

        # Step 3: Configure new 'origin' remote
        Write-Host "  Enter the Git URL for your new repository (e.g. https://github.com/username/my-repo.git)" -ForegroundColor White
        $newOriginUrl = Read-Host "  New 'origin' URL (leave blank to skip)"
        if (-not [string]::IsNullOrWhiteSpace($newOriginUrl)) {
            $newOriginUrl = $newOriginUrl.Trim()
            $currentRemotes = @(git -C $RepoRoot remote 2>$null)
            if ($currentRemotes -contains "origin") {
                git -C $RepoRoot remote set-url origin $newOriginUrl
                Write-Host "  Updated 'origin' remote -> $newOriginUrl" -ForegroundColor Green
            } else {
                git -C $RepoRoot remote add origin $newOriginUrl
                Write-Host "  Added 'origin' remote -> $newOriginUrl" -ForegroundColor Green
            }
        } else {
            Write-Host "  Skipped setting new 'origin' remote." -ForegroundColor DarkGray
        }
        Write-Host ""

        # Step 4: Branch restructuring (main -> main_template, create clean develop)
        Write-Host "  Configuring branches:" -ForegroundColor White
        git -C $RepoRoot branch -m main main_template
        $currentRemotes = @(git -C $RepoRoot remote 2>$null)
        if ($currentRemotes -contains "template") {
            git -C $RepoRoot branch -u template/main main_template 2>$null
        }
        Write-Host "  Renamed branch 'main' -> 'main_template' (tracking template repository)" -ForegroundColor Green

        git -C $RepoRoot checkout -q --orphan develop 2>$null
        git -C $RepoRoot add -A
        Write-Host "  Created clean orphan branch 'develop' with zero template commit history" -ForegroundColor Green
        Write-Host "  All renamed files are staged and ready for your initial commit." -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host "  Skipped git configuration." -ForegroundColor DarkGray
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
Write-Host "    Files modified    : $filesModified $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host "    Files renamed     : $filesRenamed $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host "    Dirs renamed      : $dirsRenamed $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green
Write-Host "    bin/obj cleaned   : $binObjCleaned $(if ($DryRun) { '(simulated)' })" -ForegroundColor Green

if ($gitInstalled -and $isGitRepo -and -not $DryRun) {
    $activeBranch = (git -C $RepoRoot branch --show-current 2>$null)
    if ($activeBranch) {
        Write-Host ""
        Write-Host "  Current Branch: $($activeBranch.Trim())" -ForegroundColor Cyan
    }
    $finalRemotes = (git -C $RepoRoot remote -v 2>$null | Select-Object -Unique)
    if ($finalRemotes) {
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
Write-Host "    4. Generate initial database migration (creates DataProtectionKeys and entity tables):" -ForegroundColor DarkGray
Write-Host "         dotnet ef migrations add InitialCreate --project src/$NewName.Persistence --startup-project src/$NewName.Web" -ForegroundColor Cyan
Write-Host "    5. Review staged changes and create your initial commit on 'develop':" -ForegroundColor DarkGray
Write-Host "         git status" -ForegroundColor Cyan
Write-Host "         git commit -m `"Initial commit: scaffold from BlazorEnterprise template`"" -ForegroundColor Cyan
Write-Host "         git push -u origin develop" -ForegroundColor Cyan
Write-Host "    6. Update Security:IntegritySecret in src/$NewName.Web/appsettings.json" -ForegroundColor DarkGray
Write-Host "    7. Update ConnectionStrings:DefaultConnection for your database" -ForegroundColor DarkGray
Write-Host "    (Optional) To pull future template updates:" -ForegroundColor DarkGray
Write-Host "         git checkout main_template" -ForegroundColor Cyan
Write-Host "         git pull template main" -ForegroundColor Cyan
Write-Host ""
