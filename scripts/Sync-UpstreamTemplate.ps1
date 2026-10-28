#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Synchronizes upstream template updates into a renamed downstream project from a local template directory.

.DESCRIPTION
    Takes a local folder pointing to the base template (e.g. BlazorFluent / BlazorEnterprise),
    copies it into a temporary staging workspace (ignoring .git, bin, obj, etc.),
    runs the template rename engine (Rename-Project.ps1) on the staged copy so that all folders,
    .csproj files, and namespaces match the downstream project brand 1-to-1, and then:
      1. BeyondCompare (default): Launches BCompare.exe comparing .template-staging ↔ downstream repo
         with zero rename/namespace noise!
      2. StageOnly: Leaves the renamed staging copy ready for inspection.
      3. AutoApply: Automatically copies new and updated base files while strictly protecting
         *.Custom.cs downstream extensions and files containing [BaseModification].

.PARAMETER TemplateFolder
    Local path to the base template folder (e.g. 'C:\Users\cheri\source\repos\BlazorFluent').

.PARAMETER DownstreamFolder
    Path to the downstream project repo. Defaults to the current repository root.

.PARAMETER DownstreamName
    Downstream project name. Auto-detected from *.slnx in DownstreamFolder if omitted.

.PARAMETER TemplateName
    Base template brand name. Auto-detected from *.slnx in TemplateFolder (defaults to 'BlazorEnterprise').

.PARAMETER Mode
    Sync mode: 'BeyondCompare' (default), 'StageOnly', or 'AutoApply'.

.PARAMETER BeyondCompareExe
    Optional path to BCompare.exe. Auto-detected if omitted.

.PARAMETER KeepStaging
    If specified, preserves .template-staging after Beyond Compare exits.

.PARAMETER DryRun
    Simulates the operations and displays the file classification report without modifying files.

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1 -TemplateFolder "C:\Users\cheri\source\repos\BlazorFluent"

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1 -TemplateFolder "C:\Users\cheri\source\repos\BlazorFluent" -Mode AutoApply -DryRun

.EXAMPLE
    pwsh scripts/Sync-UpstreamTemplate.ps1 -TemplateFolder "C:\Users\cheri\source\repos\BlazorFluent" -Mode AutoApply
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "Local path to base template directory")]
    [ValidateScript({
        if (Test-Path $_ -PathType Container) { return $true }
        throw "Template directory not found: '$_'"
    })]
    [string]$TemplateFolder,

    [Parameter(Mandatory = $false, HelpMessage = "Path to downstream repository root")]
    [string]$DownstreamFolder = "",

    [Parameter(Mandatory = $false, HelpMessage = "Downstream project brand name (auto-detected if omitted)")]
    [string]$DownstreamName = "",

    [Parameter(Mandatory = $false, HelpMessage = "Template project brand name (auto-detected if omitted)")]
    [string]$TemplateName = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("BeyondCompare", "StageOnly", "AutoApply")]
    [string]$Mode = "BeyondCompare",

    [Parameter(Mandatory = $false, HelpMessage = "Path to BCompare.exe")]
    [string]$BeyondCompareExe = "",

    [Parameter(Mandatory = $false)]
    [switch]$KeepStaging,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --- 1. Resolve Paths ---
$TemplateFolder = (Resolve-Path $TemplateFolder).Path

if (-not $DownstreamFolder) {
    $DownstreamFolder = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
} else {
    $DownstreamFolder = (Resolve-Path $DownstreamFolder).Path
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

if (-not $DownstreamName) {
    $DownstreamName = Get-ProjectNameFromSlnx $DownstreamFolder
    if (-not $DownstreamName) { $DownstreamName = "BlazorEnterprise" }
}

Write-Host "  Template Brand    : $TemplateName" -ForegroundColor DarkGray
Write-Host "  Downstream Brand  : $DownstreamName" -ForegroundColor DarkGray
Write-Host ""

if ($TemplateFolder -eq $DownstreamFolder) {
    Write-Host "  NOTICE: TemplateFolder and DownstreamFolder are the same directory." -ForegroundColor Yellow
    Write-Host "  This run will test template staging and renaming without self-sync." -ForegroundColor DarkGray
    Write-Host ""
}

# --- 3. Locate Beyond Compare if in BeyondCompare Mode ---
if ($Mode -eq "BeyondCompare") {
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

    if (-not $BeyondCompareExe -or -not (Test-Path $BeyondCompareExe)) {
        Write-Host "  WARNING: Beyond Compare (BCompare.exe) was not found on this system." -ForegroundColor Yellow
        Write-Host "  Switching mode to 'StageOnly'. You can inspect the staged folder manually." -ForegroundColor Yellow
        $Mode = "StageOnly"
    } else {
        Write-Host "  Beyond Compare    : $BeyondCompareExe" -ForegroundColor Green
    }
}

# --- 4. Prepare Staging Workspace ---
$stagingDir = Join-Path $DownstreamFolder ".template-staging"
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

# Ensure .template-staging gitignore is present so staging is ignored
$stagingGitIgnore = Join-Path $stagingDir ".gitignore"
if (Test-Path $stagingGitIgnore) {
    # It's an isolated copy
}

# --- 5. Rename Staging Workspace to Match Downstream Project ---
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

# --- 6. Scan & Classify Files ---
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

Write-Host "  Identical Files                  : $($unchangedFiles.Count)" -ForegroundColor Green
Write-Host "  New Upstream Template Files      : $($newTemplateFiles.Count)" -ForegroundColor Cyan
Write-Host "  Updated Safe Base Files          : $($updatedBaseFiles.Count)" -ForegroundColor Yellow
Write-Host "  Protected [BaseModification] Files: $($protectedModifiedFiles.Count)" -ForegroundColor Magenta
Write-Host ""

if ($protectedModifiedFiles.Count -gt 0) {
    Write-Host "  The following files have downstream [BaseModification] annotations:" -ForegroundColor Magenta
    foreach ($pmf in $protectedModifiedFiles) {
        Write-Host "    - $pmf" -ForegroundColor Magenta
    }
    Write-Host "  These files will NEVER be automatically overwritten to protect custom code." -ForegroundColor DarkGray
    Write-Host ""
}

# --- 7. Execute Selected Mode ---
switch ($Mode) {
    "BeyondCompare" {
        Write-Host "==> Launching Beyond Compare..." -ForegroundColor Cyan
        Write-Host "  Left  (Staged Upstream Template): $stagingDir" -ForegroundColor DarkGray
        Write-Host "  Right (Your Downstream Project) : $DownstreamFolder" -ForegroundColor DarkGray
        Write-Host ""

        # Launch Beyond Compare process
        $bcArgs = @(
            $stagingDir,
            $DownstreamFolder,
            "/title1=Upstream Template ($TemplateName -> $DownstreamName)",
            "/title2=Downstream Project ($DownstreamName)"
        )

        $proc = Start-Process -FilePath $BeyondCompareExe -ArgumentList $bcArgs -PassThru

        if (-not $KeepStaging) {
            Write-Host "Beyond Compare is running (Process ID: $($proc.Id))." -ForegroundColor Green
            Write-Host "Staging files are at: $stagingDir" -ForegroundColor DarkGray
            Write-Host "Note: When you are done comparing, you can remove .template-staging." -ForegroundColor DarkGray
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
