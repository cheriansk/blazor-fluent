#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Synchronizes and compares the base template project against a renamed downstream project.

.DESCRIPTION
    Always executed from the Base Template project.
    Takes the path to your renamed downstream project folder (or prompts for it),
    stages a temporary copy of this template into .template-staging,
    translates all namespaces, project files, and folders to match the downstream project's name,
    and then launches Beyond Compare (or runs safe auto-sync).

.PARAMETER TemplateFolder
    Local path to the base template folder (e.g. 'C:\Users\cheri\source\repos\BlazorFluent').

.PARAMETER DownstreamFolder
    Path to the renamed downstream project folder. If omitted, prompts interactively.

.PARAMETER TemplateFolder
    Path to the base template repo. Defaults to the current template repository root.

.PARAMETER DownstreamName
    Downstream project name. Auto-detected from *.slnx in DownstreamFolder if omitted.

.PARAMETER TemplateName
    Base template brand name. Auto-detected from *.slnx in TemplateFolder (defaults to 'BlazorEnterprise').

.PARAMETER Mode
    Sync mode: 'Compare' (default - provides comparison paths), 'StageOnly', or 'AutoApply'.

.PARAMETER LaunchBeyondCompare
    If specified, automatically launches Beyond Compare (BCompare.exe) GUI. Default is false (outputs paths only).

.PARAMETER BeyondCompareExe
    Optional path to BCompare.exe. Auto-detected if omitted.

.PARAMETER Force
    Skips interactive confirmation prompts.

.PARAMETER KeepStaging
    If specified, preserves .template-staging.

.PARAMETER DryRun
    Simulates the operations and displays the file classification report without modifying files.

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\Users\cheri\source\repos\AcmePortal"

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\Users\cheri\source\repos\AcmePortal" -LaunchBeyondCompare
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0, Mandatory = $false, HelpMessage = "Path to the renamed downstream project directory")]
    [string]$DownstreamFolder = "",

    [Parameter(Mandatory = $false, HelpMessage = "Path to base template directory (defaults to current repo)")]
    [string]$TemplateFolder = "",

    [Parameter(Mandatory = $false, HelpMessage = "Downstream project brand name (auto-detected if omitted)")]
    [string]$DownstreamName = "",

    [Parameter(Mandatory = $false, HelpMessage = "Template project brand name (auto-detected if omitted)")]
    [string]$TemplateName = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("Compare", "BeyondCompare", "StageOnly", "AutoApply")]
    [string]$Mode = "Compare",

    [Parameter(Mandatory = $false, HelpMessage = "Explicitly launch Beyond Compare GUI")]
    [switch]$LaunchBeyondCompare,

    [Parameter(Mandatory = $false, HelpMessage = "Path to BCompare.exe")]
    [string]$BeyondCompareExe = "",

    [Parameter(Mandatory = $false, HelpMessage = "Skip interactive confirmation prompts")]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [switch]$KeepStaging,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --- 1. Resolve Template Folder (Always running from Template Project) ---
if (-not $TemplateFolder) {
    $TemplateFolder = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
} else {
    $TemplateFolder = (Resolve-Path $TemplateFolder).Path
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Template Synchronization Engine (Local Upstream Mirror)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Template Source   : $TemplateFolder" -ForegroundColor DarkGray
Write-Host "  Downstream Target : $DownstreamFolder" -ForegroundColor DarkGray
Write-Host "  Execution Mode    : $Mode" -ForegroundColor Yellow

# --- 2. Auto-Detect Project Brand Names ---
function Get-ProjectNameFromSlnx([string]$folderPath) {
    $slnx = Get-ChildItem -Path $folderPath -Filter "*.slnx" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($slnx) {
        return [System.IO.Path]::GetFileNameWithoutExtension($slnx.Name)
    }
    $sln = Get-ChildItem -Path $folderPath -Filter "*.sln" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($sln) {
        return [System.IO.Path]::GetFileNameWithoutExtension($sln.Name)
    }
    return $null
}

if (-not $TemplateName) {
    $TemplateName = Get-ProjectNameFromSlnx $TemplateFolder
    if (-not $TemplateName) { $TemplateName = "BlazorEnterprise" }
}

# --- 3. Prominent Header ---
Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "       BASE TEMPLATE SYNCHRONIZATION & COMPARISON ENGINE" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Running from Base Template Project:" -ForegroundColor DarkGray
Write-Host "    $TemplateFolder ($TemplateName)" -ForegroundColor White
Write-Host ""
Write-Host "  [!] NOTICE: This tool will stage a temporary mirror of this template," -ForegroundColor Yellow
Write-Host "      translate all namespaces and folder names to match your downstream" -ForegroundColor Yellow
Write-Host "      renamed project, and open a 1:1 noise-free comparison in Beyond Compare." -ForegroundColor Yellow
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# --- 4. Prompt for Downstream Project Folder if not supplied ---
while (-not $DownstreamFolder -or -not (Test-Path $DownstreamFolder -PathType Container)) {
    if ($DownstreamFolder) {
        Write-Host "  Directory not found: '$DownstreamFolder'" -ForegroundColor Red
    }
    $DownstreamFolder = Read-Host "  Enter the folder path of the renamed downstream project"
    if (-not $DownstreamFolder) {
        Write-Host "  Aborted: Downstream project folder is required." -ForegroundColor Yellow
        exit 0
    }
}

$DownstreamFolder = (Resolve-Path $DownstreamFolder).Path

if ($DownstreamFolder -eq $TemplateFolder) {
    Write-Host "  ERROR: Downstream project cannot be the same as the base template directory!" -ForegroundColor Red
    Write-Host "  Please enter the path to the cloned/renamed project." -ForegroundColor Red
    exit 1
}

if (-not $DownstreamName) {
    $DownstreamName = Get-ProjectNameFromSlnx $DownstreamFolder
    if (-not $DownstreamName) {
        $DownstreamName = [System.IO.Path]::GetFileName($DownstreamFolder)
}
}

# --- 5. Display Configuration Summary & Ask Confirmation ---
Write-Host ""
Write-Host "  Sync & Compare Configuration:" -ForegroundColor Cyan
Write-Host "    Base Template (Source) : $TemplateFolder ($TemplateName)" -ForegroundColor DarkGray
Write-Host "    Renamed Target         : $DownstreamFolder ($DownstreamName)" -ForegroundColor DarkGray
Write-Host "    Execution Mode         : $Mode" -ForegroundColor Yellow
Write-Host ""

if (-not $Force -and -not $DryRun) {
    $confirm = Read-Host "  Do you want to continue? [Y/n] (default: Y)"
    if ($confirm -and $confirm.Trim().ToLowerInvariant() -notin @("y", "yes")) {
        Write-Host "  Operation cancelled by user." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# --- 6. Clean Up Any Stale Downstream Staging Workspace ---
$downstreamStaging = Join-Path $DownstreamFolder ".template-staging"
if (Test-Path $downstreamStaging) {
    Write-Host "==> Cleaning stale .template-staging found in downstream project: $downstreamStaging" -ForegroundColor Yellow
    Remove-Item -Path $downstreamStaging -Recurse -Force -ErrorAction SilentlyContinue
}

# --- 7. Prepare Staging Workspace inside Template Project ---
$stagingDir = Join-Path $TemplateFolder ".template-staging"
if (Test-Path $stagingDir) {
    Write-Host "==> Cleaning prior staging workspace: $stagingDir" -ForegroundColor DarkGray
    Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "==> Creating fresh staging directory..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

$excludeFolders = @(".git", ".vs", ".template-staging", "bin", "obj", "artifacts", "TestResults", "dist", "node_modules")

Write-Host "==> Copying template files to staging workspace..." -ForegroundColor Cyan
$items = Get-ChildItem -Path $TemplateFolder -Force | Where-Object { $excludeFolders -notcontains $_.Name }
foreach ($item in $items) {
    Copy-Item -Path $item.FullName -Destination $stagingDir -Recurse -Force
}

# --- 8. Rename Staging Workspace to Match Downstream Project ---
if ($TemplateName -ne $DownstreamName) {
    Write-Host "==> Translating template brand '$TemplateName' -> '$DownstreamName' in staging..." -ForegroundColor Cyan
    $renameScript = Join-Path $stagingDir "scripts\Rename-Project.ps1"
    if (-not (Test-Path $renameScript)) {
        $renameScript = Join-Path $stagingDir "Rename-Project.ps1"
    }

    if (Test-Path $renameScript) {
        & pwsh -File $renameScript -OldName $TemplateName -NewName $DownstreamName -Force | Out-Null
        Write-Host "  Staged mirror successfully rebranded to '$DownstreamName'!" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Rename-Project.ps1 not found in staging. Performing direct text substitution..." -ForegroundColor Yellow
    }
} else {
    Write-Host "==> Template and Downstream project names match ('$TemplateName'). No rename needed in staging." -ForegroundColor DarkGray
}

Write-Host ""

# --- 9. Scan & Classify Files ---
Write-Host "=== File Classification & Safety Audit ===" -ForegroundColor Cyan

$stagedFiles = Get-ChildItem -Path (Join-Path $stagingDir "src") -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs)[\\/]'
}

$newTemplateFiles = [System.Collections.Generic.List[string]]::new()
$updatedBaseFiles = [System.Collections.Generic.List[string]]::new()
$protectedModifiedFiles = [System.Collections.Generic.List[string]]::new()
$unchangedFiles = [System.Collections.Generic.List[string]]::new()

foreach ($sFile in $stagedFiles) {
    $relPath = $sFile.FullName.Substring($stagingDir.Length + 1)
    $downstreamFile = Join-Path $DownstreamFolder $relPath

    if (-not (Test-Path $downstreamFile)) {
        $newTemplateFiles.Add($relPath)
        continue
    }

    $stagedBytes = [System.IO.File]::ReadAllBytes($sFile.FullName)
    $downstreamBytes = [System.IO.File]::ReadAllBytes($downstreamFile)

    # Compute quick hash comparison
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stagedHash = [System.BitConverter]::ToString($sha.ComputeHash($stagedBytes))
    $downstreamHash = [System.BitConverter]::ToString($sha.ComputeHash($downstreamBytes))

    if ($stagedHash -eq $downstreamHash) {
        $unchangedFiles.Add($relPath)
        continue
    }

    # Content differs. Check if downstream file has [BaseModification]
    $downstreamText = [System.IO.File]::ReadAllText($downstreamFile)
    if ($downstreamText.IndexOf("[BaseModification", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $protectedModifiedFiles.Add($relPath)
    } else {
        $updatedBaseFiles.Add($relPath)
    }
}

Write-Host "  Identical Files                   : $($unchangedFiles.Count)" -ForegroundColor Green
Write-Host "  New Upstream Template Files       : $($newTemplateFiles.Count)" -ForegroundColor Cyan
Write-Host "  Updated Safe Base Files           : $($updatedBaseFiles.Count)" -ForegroundColor Yellow
Write-Host "  Protected [BaseModification] Files : $($protectedModifiedFiles.Count)" -ForegroundColor Magenta
Write-Host ""

if ($protectedModifiedFiles.Count -gt 0) {
    Write-Host "  The following files have downstream [BaseModification] annotations:" -ForegroundColor Magenta
    foreach ($pmf in $protectedModifiedFiles) {
        Write-Host "    - $pmf" -ForegroundColor Magenta
    }
    Write-Host "  These files will NEVER be automatically overwritten to protect custom code." -ForegroundColor DarkGray
    Write-Host ""
}

# --- 10. Execute Selected Mode ---
switch ($Mode) {
    { $_ -in @("Compare", "BeyondCompare") } {
        Write-Host "================================================================================" -ForegroundColor Cyan
        Write-Host "                       DIFF / COMPARISON PATHS READY                             " -ForegroundColor Yellow
        Write-Host "================================================================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  Source (Left  / Upstream Template):" -ForegroundColor Cyan
        Write-Host "    $stagingDir" -ForegroundColor White
        Write-Host ""
        Write-Host "  Target (Right / Downstream Project):" -ForegroundColor Cyan
        Write-Host "    $DownstreamFolder" -ForegroundColor White
        Write-Host ""
        Write-Host "  To compare in Beyond Compare via command line:" -ForegroundColor DarkGray
        Write-Host "    bcompare `"$stagingDir`" `"$DownstreamFolder`"" -ForegroundColor Green
        Write-Host "================================================================================" -ForegroundColor Cyan
        Write-Host ""

        if ($LaunchBeyondCompare) {
            if (-not $BeyondCompareExe) {
                $candidatePaths = @(
                    "C:\Program Files\Beyond Compare 5\BCompare.exe",
                    "C:\Program Files\Beyond Compare 4\BCompare.exe",
                    "$env:LOCALAPPDATA\Programs\Beyond Compare 5\BCompare.exe",
                    "$env:LOCALAPPDATA\Programs\Beyond Compare 4\BCompare.exe"
                )
                foreach ($cand in $candidatePaths) {
                    if (Test-Path $cand) {
                        $BeyondCompareExe = $cand
                        break
                    }
                }
                if (-not $BeyondCompareExe) {
                    $cmd = Get-Command BCompare.exe -ErrorAction SilentlyContinue
                    if ($cmd) { $BeyondCompareExe = $cmd.Source }
                }
            }

            if ($BeyondCompareExe -and (Test-Path $BeyondCompareExe)) {
                Write-Host "==> Launching Beyond Compare GUI..." -ForegroundColor Cyan
                $bcArgs = @(
                    $stagingDir,
                    $DownstreamFolder,
                    "/title1=Base Template ($TemplateName -> $DownstreamName)",
                    "/title2=Renamed Downstream ($DownstreamName)"
                )
                $proc = Start-Process -FilePath $BeyondCompareExe -ArgumentList $bcArgs -PassThru
                Write-Host "Beyond Compare started (Process ID: $($proc.Id))." -ForegroundColor Green
            } else {
                Write-Host "  WARNING: BCompare.exe was not found. Please open Beyond Compare manually using the paths above." -ForegroundColor Yellow
            }
        }

        if (-not $KeepStaging) {
            Write-Host "Staging files preserved at: $stagingDir" -ForegroundColor DarkGray
            Write-Host "Note: When you are finished comparing, you can remove .template-staging." -ForegroundColor DarkGray
        } else {
            Write-Host "Staging workspace preserved at: $stagingDir" -ForegroundColor Green
        }
    }

    "StageOnly" {
        Write-Host "==> StageOnly mode complete." -ForegroundColor Green
        Write-Host "  Renamed staging workspace is ready at:" -ForegroundColor Cyan
        Write-Host "  $stagingDir" -ForegroundColor White
        Write-Host ""
        Write-Host "You can compare this folder against your downstream project in any diff tool." -ForegroundColor DarkGray
    }

    "AutoApply" {
        if ($DryRun) {
            Write-Host "==> [DRY RUN] Would auto-apply $($newTemplateFiles.Count) new files and $($updatedBaseFiles.Count) updated base files." -ForegroundColor Magenta
            Write-Host "==> [DRY RUN] Would update TemplateGuard analyzers and locked manifest." -ForegroundColor Magenta
            Write-Host "==> [DRY RUN] Would preserve all *.Custom.cs files and $($protectedModifiedFiles.Count) [BaseModification] files." -ForegroundColor Magenta
        } else {
            Write-Host "==> Applying safe updates to downstream project..." -ForegroundColor Cyan

            # 1. Copy New Template Files
            foreach ($nf in $newTemplateFiles) {
                $src = Join-Path $stagingDir $nf
                $dest = Join-Path $DownstreamFolder $nf
                $destDir = [System.IO.Path]::GetDirectoryName($dest)
                if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
                Copy-Item -Path $src -Destination $dest -Force
                Write-Host "  [NEW] $nf" -ForegroundColor Green
            }

            # 2. Copy Safe Base Updates
            foreach ($uf in $updatedBaseFiles) {
                $src = Join-Path $stagingDir $uf
                $dest = Join-Path $DownstreamFolder $uf
                Copy-Item -Path $src -Destination $dest -Force
                Write-Host "  [UPDATED] $uf" -ForegroundColor Cyan
            }

            # 3. Synchronize TemplateGuard Artifacts
            $stagedManifest = Join-Path $stagingDir "tools\TemplateGuard\.basefiles.json"
            $destManifest = Join-Path $DownstreamFolder "tools\TemplateGuard\.basefiles.json"
            if (Test-Path $stagedManifest) {
                if (Test-Path $destManifest) { Set-ItemProperty -Path $destManifest -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue }
                Copy-Item -Path $stagedManifest -Destination $destManifest -Force
                Set-ItemProperty -Path $destManifest -Name IsReadOnly -Value $true
                Write-Host "  [GOVERNANCE] Updated and locked tools/TemplateGuard/.basefiles.json" -ForegroundColor Green
            }

            $stagedAnalyzerDll = Join-Path $stagingDir "tools\TemplateGuard\TemplateGuard.Analyzers.dll"
            $destAnalyzerDll = Join-Path $DownstreamFolder "tools\TemplateGuard\TemplateGuard.Analyzers.dll"
            if (Test-Path $stagedAnalyzerDll) {
                Copy-Item -Path $stagedAnalyzerDll -Destination $destAnalyzerDll -Force
                Write-Host "  [GOVERNANCE] Updated tools/TemplateGuard/TemplateGuard.Analyzers.dll" -ForegroundColor Green
            }

            Write-Host ""
            Write-Host "==> AutoApply synchronization completed successfully!" -ForegroundColor Green

            if ($protectedModifiedFiles.Count -gt 0) {
                Write-Host "  REMINDER: Review the $($protectedModifiedFiles.Count) [BaseModification] file(s) in Beyond Compare." -ForegroundColor Yellow
            }
        }

        if (-not $KeepStaging -and -not $DryRun) {
            Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
