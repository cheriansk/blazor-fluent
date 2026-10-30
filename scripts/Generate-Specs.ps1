#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates Antigravity / Gemini-optimized ultra-lean .requirements.md specifications
    for target Razor components and C# classes defined in docs/specs/specs-manifest.json,
    and optionally generates an interactive HTML Mermaid flow explorer (-GenerateHtml).

.DESCRIPTION
    Reads docs/specs/specs-manifest.json (or a single -Target file).
    Performs 4 automated passes:
    1. Indexes service interfaces, concrete implementations, and Razor components across src/ (mapping methods, line ranges, DbSets, Audit calls, and Result.Failure branches).
    2. Parses target .razor + .razor.cs pairs or standalone .cs classes (extracting UI component hierarchies, child Razor components, data grids, event bindings, lifecycle methods, and action methods).
    3. Preserves any user-authored [PENDING CHANGE REQUESTS] and [CUSTOM BUSINESS RULES] blocks (plus rescues inline [CHANGE: ...] tags) while generating an ultra-compact, token-optimized .requirements.md file containing only what the coding agent needs.
    4. Generates docs/specs/README.md (and optionally docs/specs/index.html with full visual Mermaid sequenceDiagrams and flowcharts when -GenerateHtml is passed).

.PARAMETER ManifestPath
    Path to the JSON manifest file. Defaults to "docs/specs/specs-manifest.json".

.PARAMETER Target
    Optional single file path (.razor or .cs) to generate on-demand.

.PARAMETER TargetFilter
    Optional substring filter to only regenerate matching target(s) from the manifest.

.PARAMETER AddToManifest
    When used with -Target, adds the target file to specs-manifest.json if not already present.

.PARAMETER GenerateHtml
    Optional switch to generate docs/specs/index.html containing full visual Mermaid sequenceDiagrams and composition flowcharts. Suppressed by default.

.PARAMETER ArchiveCompleted
    Moves completed requirements from the [BEGIN PENDING CHANGES] block into the date-stamped [BEGIN CHANGE HISTORY] block (under the current date/time), resets [BEGIN PENDING CHANGES] to the clean placeholder, and refreshes the spec's Page Overview and Agent Code Index.

.PARAMETER EnsureFresh
    Fast pre-flight SHA-256 hash check. Compares the stored source_hash_sha256 in the .requirements.md file against the live source file(s) on disk. If unchanged, exits immediately in <50ms without rescanning the workspace. If external code edits occurred, regenerates only the stale spec(s) (preserving Section 1 Pending Changes, Permanent Rules, and Change History).

.EXAMPLE
    pwsh scripts/Generate-Specs.ps1
    pwsh scripts/Generate-Specs.ps1 -TargetFilter "SettingsTenantsTab" -EnsureFresh
    pwsh scripts/Generate-Specs.ps1 -TargetFilter "SettingsTenantsTab" -ArchiveCompleted
#>

[CmdletBinding()]
param(
    [string]$ManifestPath = "docs/specs/specs-manifest.json",
    [string]$Target = "",
    [string]$TargetFilter = "",
    [switch]$AddToManifest,
    [switch]$GenerateHtml = $false,
    [switch]$ArchiveCompleted = $false,
    [switch]$EnsureFresh = $false
)

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

# Always use UTF-8 with BOM so Windows PowerShell 5.1, pwsh 7+, and Visual Studio never misread encoding
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path
$srcDir = Join-Path $rootDir "src"
$resolvedManifest = if ([System.IO.Path]::IsPathRooted($ManifestPath)) { $ManifestPath } else { Join-Path $rootDir $ManifestPath }
$specsDir = Split-Path -Parent $resolvedManifest
$indexDir = Join-Path $specsDir "index"
$historyDir = Join-Path $specsDir "history"
$validationDir = Join-Path $specsDir "validation"

foreach ($d in @($specsDir, $indexDir, $historyDir, $validationDir)) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
    }
}

# Remove stale index.html when HTML generation is suppressed
$htmlPath = Join-Path $specsDir "index.html"
if (-not $GenerateHtml -and (Test-Path $htmlPath)) {
    Remove-Item -Path $htmlPath -Force -ErrorAction SilentlyContinue
}

# --- Helper: Normalize & Clean Paths (Strip C:/.../src/ and leading src/) ---
function Format-CleanPath {
    param([string]$InputPath)
    if ([string]::IsNullOrWhiteSpace($InputPath)) { return "" }
    $p = $InputPath.Replace('\', '/').Trim()
    $rootNorm = $rootDir.Replace('\', '/').TrimEnd('/')
    if ($p.StartsWith($rootNorm, [System.StringComparison]::OrdinalIgnoreCase)) {
        $p = $p.Substring($rootNorm.Length).TrimStart('/')
    }
    if ($p -match '^src/(.+)$') {
        $p = $Matches[1]
    }
    return $p
}

function Resolve-FullSourcePath {
    param([string]$CleanOrRelPath)
    $clean = Format-CleanPath $CleanOrRelPath
    $candidateInSrc = Join-Path $srcDir ($clean.Replace('/', '\'))
    if (Test-Path $candidateInSrc) { return (Resolve-Path $candidateInSrc).Path }
    $candidateInRoot = Join-Path $rootDir ($CleanOrRelPath.Replace('/', '\'))
    if (Test-Path $candidateInRoot) { return (Resolve-Path $candidateInRoot).Path }
    return $candidateInSrc
}

function Get-RelativeDocLink {
    param(
        [string]$CleanSourcePath,
        [string]$LineAnchor = ""
    )
    $clean = Format-CleanPath $CleanSourcePath
    $link = "../../src/$clean"
    if (-not [string]::IsNullOrWhiteSpace($LineAnchor)) {
        $link = "${link}#${LineAnchor}"
    }
    return $link
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  [SPEC & FLOW GENERATOR] Antigravity-Optimized Lean Spec & Flow Builder        " -ForegroundColor Yellow
Write-Host "================================================================================" -ForegroundColor Cyan

# --- 1. Load Manifest & Flatten Hierarchy (Parents + Registered Children) ---
if (-not (Test-Path $resolvedManifest)) {
    throw "Specs manifest not found at: $resolvedManifest"
}

$manifestRaw = [System.IO.File]::ReadAllText($resolvedManifest, [System.Text.Encoding]::UTF8)
$manifest = $manifestRaw | ConvertFrom-Json

$defaultThinking = if ($manifest.agentRuntime -and $manifest.agentRuntime.thinking_level) { $manifest.agentRuntime.thinking_level } else { "low" }
$defaultAgentMode = if ($manifest.agentRuntime -and $manifest.agentRuntime.agent_mode) { $manifest.agentRuntime.agent_mode } else { "targeted_surgical_edit" }
$defaultTokenPolicy = if ($manifest.agentRuntime -and $manifest.agentRuntime.token_budget_policy) { $manifest.agentRuntime.token_budget_policy } else { "minimal_context_reads" }
$forbiddenCmds = if ($manifest.agentRuntime -and $manifest.agentRuntime.do_not_run_commands) { ($manifest.agentRuntime.do_not_run_commands -join '", "') } else { "dotnet build`", `"dotnet test" }

$ignorePrefixes = @("Fluent", "Csk", "Mud", "EditForm", "DataAnnotations", "Validation", "Input", "Authorize", "Cascading", "Router", "Route", "FocusOnNavigate", "PageTitle", "HeadContent", "SectionOutlet", "SectionContent", "Virtualize", "ErrorBoundary")
$ignoreComponents = @("PageHeaderComp", "RedirectToLogin")

if ($manifest.component_discovery) {
    if ($manifest.component_discovery.ignore_prefixes) {
        $ignorePrefixes = @($manifest.component_discovery.ignore_prefixes)
    }
    if ($manifest.component_discovery.ignore_components) {
        $ignoreComponents = @($manifest.component_discovery.ignore_components)
    }
}

$targetEntries = [System.Collections.Generic.List[PSObject]]::new()
$seenTargetPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Add-ManifestNode {
    param(
        [PSObject]$Node,
        [string]$ParentCleanPath = "",
        [string]$ParentSpecFile = ""
    )
    if ($null -eq $Node) { return }
    if ($Node.PSObject.Properties['enabled'] -and $Node.enabled -eq $false) { return }

    $rawPath = if ($Node -is [string]) { $Node } else { $Node.path }
    $cleanPath = Format-CleanPath $rawPath
    if ([string]::IsNullOrWhiteSpace($cleanPath)) { return }

    $registeredChildList = [System.Collections.Generic.List[PSObject]]::new()
    if ($Node -isnot [string] -and $Node.PSObject.Properties['children'] -and $Node.children) {
        foreach ($ch in $Node.children) {
            if ($ch.PSObject.Properties['enabled'] -and $ch.enabled -eq $false) { continue }
            $chClean = Format-CleanPath $ch.path
            $registeredChildList.Add([PSCustomObject]@{
                path           = $chClean
                name           = [System.IO.Path]::GetFileNameWithoutExtension($chClean)
                category       = if ($ch.category) { $ch.category } else { $Node.category }
                thinking_level = if ($ch.thinking_level) { $ch.thinking_level } else { $defaultThinking }
                description    = if ($ch.description) { $ch.description } else { "" }
                rawNode        = $ch
            })
        }
    }

    if ($seenTargetPaths.Add($cleanPath)) {
        $targetEntries.Add([PSCustomObject]@{
            path               = $cleanPath
            category           = if ($Node -isnot [string] -and $Node.category) { $Node.category } else { "General" }
            thinking_level     = if ($Node -isnot [string] -and $Node.thinking_level) { $Node.thinking_level } else { $defaultThinking }
            description        = if ($Node -isnot [string] -and $Node.description) { $Node.description } else { "" }
            parentPath         = $ParentCleanPath
            parentSpecFile     = $ParentSpecFile
            registeredChildren = @($registeredChildList)
        })
    }

    $thisBaseName = [System.IO.Path]::GetFileNameWithoutExtension($cleanPath)
    $thisSpecFile = "$thisBaseName.requirements.md"
    foreach ($rc in $registeredChildList) {
        Add-ManifestNode -Node $rc.rawNode -ParentCleanPath $cleanPath -ParentSpecFile $thisSpecFile
    }
}

if (-not [string]::IsNullOrWhiteSpace($Target)) {
    $cleanTarget = Format-CleanPath $Target
    $entryObj = [PSCustomObject]@{
        path           = $cleanTarget
        category       = if ($cleanTarget -match '\.razor$') { "UI Component / Page" } else { "C# Service / Batch" }
        thinking_level = $defaultThinking
        description    = "On-demand generated specification for $([System.IO.Path]::GetFileName($cleanTarget))."
        enabled        = $true
    }
    Add-ManifestNode -Node $entryObj
} else {
    foreach ($t in $manifest.targets) {
        Add-ManifestNode -Node $t
    }
}

if (-not [string]::IsNullOrWhiteSpace($TargetFilter)) {
    $filtered = @($targetEntries | Where-Object { $_.path -match [regex]::Escape($TargetFilter) })
    $targetEntries.Clear()
    foreach ($f in $filtered) { $targetEntries.Add($f) }
}

# --- Fast Pre-Flight Freshness Check (-EnsureFresh) ---
function Get-TargetSourceHash {
    param([string]$FullSourcePath)
    if (-not (Test-Path $FullSourcePath)) { return "" }
    $shaLocal = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.IO.File]::ReadAllBytes($FullSourcePath)
    $isRazorLocal = ($FullSourcePath -match '\.razor$')
    $cbFullPath = "$FullSourcePath.cs"
    if ($isRazorLocal -and (Test-Path $cbFullPath)) {
        $cbBytes = [System.IO.File]::ReadAllBytes($cbFullPath)
        $combined = New-Object byte[] ($bytes.Length + $cbBytes.Length)
        [System.Array]::Copy($bytes, 0, $combined, 0, $bytes.Length)
        [System.Array]::Copy($cbBytes, 0, $combined, $bytes.Length, $cbBytes.Length)
        $bytes = $combined
    }
    return ([System.BitConverter]::ToString($shaLocal.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant().Substring(0, 16)
}

if ($EnsureFresh -and -not $ArchiveCompleted -and -not $GenerateHtml) {
    $staleEntries = [System.Collections.Generic.List[PSObject]]::new()
    foreach ($te in $targetEntries) {
        $fPath = Resolve-FullSourcePath $te.path
        $bName = [System.IO.Path]::GetFileNameWithoutExtension($fPath)
        $sFile = "$bName.requirements.md"
        $iFile = "$bName.index.md"
        $sFullPath = Join-Path $specsDir $sFile
        $iFullPath = Join-Path $indexDir $iFile
        $liveHash = Get-TargetSourceHash -FullSourcePath $fPath
        $storedHash = ""
        if (Test-Path $iFullPath) {
            $headerSample = [System.IO.File]::ReadAllText($iFullPath, [System.Text.Encoding]::UTF8)
            if ($headerSample -match 'source_hash_sha256:\s*(?<h>[0-9a-fA-F]{16})') {
                $storedHash = $Matches['h'].ToLowerInvariant()
            }
        } elseif (Test-Path $sFullPath) {
            $headerSample = [System.IO.File]::ReadAllText($sFullPath, [System.Text.Encoding]::UTF8)
            if ($headerSample -match 'source_hash_sha256:\s*(?<h>[0-9a-fA-F]{16})') {
                $storedHash = $Matches['h'].ToLowerInvariant()
            }
        }
        if ([string]::IsNullOrWhiteSpace($storedHash) -or ($storedHash -ne $liveHash) -or -not (Test-Path $iFullPath)) {
            Write-Host "  [SYNC] External source changes detected for '$sFile' (stored: '$storedHash' -> live: '$liveHash'). Regenerating index & line numbers while preserving Section 1 Pending Changes..." -ForegroundColor Yellow
            $staleEntries.Add($te)
        } else {
            Write-Host "  [OK] Spec '$sFile' and 'index/$iFile' are already in sync with live source files (hash: $liveHash). Skipping regeneration." -ForegroundColor Green
        }
    }
    if ($staleEntries.Count -eq 0) {
        exit 0
    }
    $targetEntries.Clear()
    foreach ($se in $staleEntries) { $targetEntries.Add($se) }
}

Write-Host "  Manifest       : $(Format-CleanPath $resolvedManifest)" -ForegroundColor Gray
Write-Host "  Target Entries : $($targetEntries.Count) (including registered child components)" -ForegroundColor Cyan

# --- Helper: Parse C# Methods with Line Ranges & Body ---
function Get-CSharpMethods {
    param(
        [string]$FilePath,
        [string[]]$Lines
    )
    $methods = [System.Collections.Generic.List[PSObject]]::new()
    if (-not $Lines -or $Lines.Count -eq 0) { return $methods }

    $sigPattern = '^\s*(?<vis>public|private|protected|internal)\s+(?<mods>(?:static|async|override|virtual|new|sealed)\s+)*(?<ret>[\w<>?,\[\]\.\s]+?)\s+(?<name>[A-Za-z0-9_]+)\s*\('
    $excludeNames = @("if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "new", "nameof", "typeof")

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line -match '^\s*//' -or $line -match '^\s*\*') { continue }
        $m = [regex]::Match($line, $sigPattern)
        if ($m.Success) {
            $mName = $m.Groups['name'].Value
            $retType = $m.Groups['ret'].Value.Trim()
            if ($excludeNames -contains $mName) { continue }
            if ([string]::IsNullOrWhiteSpace($retType)) { continue }

            $startLine = $i + 1
            $braceDepth = 0
            $foundOpenBrace = $false
            $isExpressionBodied = $false
            $endLine = $startLine

            for ($j = $i; $j -lt $Lines.Count; $j++) {
                $cur = $Lines[$j]
                $clean = [regex]::Replace($cur, '"(?:\\.|[^"\\])*"', '""')
                $clean = [regex]::Replace($clean, '//.*$', '')

                if (-not $foundOpenBrace -and $clean -match '=>') {
                    $isExpressionBodied = $true
                }
                if ($isExpressionBodied) {
                    if ($clean -match ';') {
                        $endLine = $j + 1
                        break
                    }
                    continue
                }

                $opens = ([regex]::Matches($clean, '\{')).Count
                $closes = ([regex]::Matches($clean, '\}')).Count
                if ($opens -gt 0) { $foundOpenBrace = $true }
                $braceDepth += ($opens - $closes)

                if ($foundOpenBrace -and $braceDepth -le 0) {
                    $endLine = $j + 1
                    break
                }
                if (-not $foundOpenBrace -and $clean -match ';\s*$') {
                    $endLine = $j + 1
                    break
                }
            }

            $bodyLines = $Lines[($startLine - 1)..($endLine - 1)]
            $bodyText = $bodyLines -join "`n"

            $dbSets = [System.Collections.Generic.HashSet[string]]::new()
            foreach ($dbm in [regex]::Matches($bodyText, '(?:_dbContext|_context|dbContext|db)\.(?<dbset>[A-Z][A-Za-z0-9_]+)\b')) {
                $ds = $dbm.Groups['dbset'].Value
                if ($ds -notin @("SaveChangesAsync", "SaveChanges", "Database", "ChangeTracker", "Set", "Entry")) {
                    [void]$dbSets.Add($ds)
                }
            }

            $savesDb = ($bodyText -match 'SaveChangesAsync|ExecuteUpdate|ExecuteDelete')

            $failures = [System.Collections.Generic.List[string]]::new()
            foreach ($fm in [regex]::Matches($bodyText, 'Result(?:<[^>]+>)?\.(?:Failure|Fail)\(\s*(?<msg>\$?"[^"]+"|[A-Za-z0-9_\.]+)')) {
                $msgVal = $fm.Groups['msg'].Value.Trim()
                if ($msgVal.Length -gt 80) { $msgVal = $msgVal.Substring(0, 77) + '..."' }
                if ($failures -notcontains $msgVal) { $failures.Add($msgVal) }
            }

            $secGates = [System.Collections.Generic.List[string]]::new()
            if ($bodyText -match 'IsRootAdmin') { $secGates.Add("IsRootAdmin") }
            if ($bodyText -match 'IsAuthorizedForTenant|HasActiveTenant') { $secGates.Add("TenantBoundaryGate") }
            if ($bodyText -match 'CanEditTenantSettingsAsync|IsTenantAdminAsync') { $secGates.Add("TenantAdminGate") }
            if ($bodyText -match 'IsProgramAdminAsync|CanAccessProgramAsync') { $secGates.Add("ProgramAccessGate") }
            if ($bodyText -match 'HasProjectClearanceAsync|GetUserRoleAsync') { $secGates.Add("ProjectClearanceGate") }
            if ($bodyText -match 'VerifyIntegritySignature|ComputeIntegritySignature') { $secGates.Add("HmacRowSignature") }

            $audits = [System.Collections.Generic.List[string]]::new()
            foreach ($am in [regex]::Matches($bodyText, '(?:_auditService|auditService)\.(?<amethod>Log[A-Za-z0-9_]+)\(')) {
                $aName = $am.Groups['amethod'].Value
                if ($audits -notcontains $aName) { $audits.Add($aName) }
            }

            $methods.Add([PSCustomObject]@{
                Name          = $mName
                Visibility    = $m.Groups['vis'].Value
                ReturnType    = $retType
                StartLine     = $startLine
                EndLine       = $endLine
                FilePath      = (Format-CleanPath $FilePath)
                BodyText      = $bodyText
                DbSets        = @($dbSets)
                SavesDb       = $savesDb
                Failures      = @($failures)
                SecurityGates = @($secGates)
                Audits        = @($audits)
            })

            if ($endLine -gt ($i + 1)) {
                $i = $endLine - 1
            }
        }
    }
    return $methods
}

# --- Helper: Build Human-Readable Overview + Compact UI Map Table from Razor Lines ---
function Get-RazorLayoutSummaryAndTree {
    param([string[]]$RazorLines)
    $humanLines = [System.Collections.Generic.List[string]]::new()
    $treeLines = [System.Collections.Generic.List[string]]::new()
    $uiMapRows = [System.Collections.Generic.List[string]]::new()
    if (-not $RazorLines -or $RazorLines.Count -eq 0) {
        return [PSCustomObject]@{ HumanOverview = $humanLines; AgentTree = $treeLines; UiMapRows = $uiMapRows }
    }

    function Get-FullTagBlock {
        param([int]$StartIdx)
        $acc = $RazorLines[$StartIdx].Trim()
        $endIdx = $StartIdx
        while ($acc -notmatch '>' -and ($endIdx + 1) -lt $RazorLines.Count -and ($endIdx - $StartIdx) -lt 12) {
            $endIdx++
            $acc += " " + $RazorLines[$endIdx].Trim()
        }
        return [PSCustomObject]@{ Text = $acc; EndIdx = $endIdx }
    }

    $sections = [System.Collections.Generic.List[PSObject]]::new()
    $currentSec = [PSCustomObject]@{
        Title     = "Header & Top-Level Controls"
        StartLine = 1
        EndLine   = $RazorLines.Count
        Gate      = ""
    }
    $sections.Add($currentSec)

    for ($i = 0; $i -lt $RazorLines.Count; $i++) {
        $ln = $RazorLines[$i]
        $lineNo = $i + 1
        $secTitle = ""
        if ($ln -match '^\s*@\*\s*(?<cmt>[^\*]+?)\s*\*@') {
            $secTitle = $Matches['cmt'].Trim()
        } elseif ($ln -match '^\s*<!--\s*(?<hcmt>[^-]+?)\s*-->') {
            $secTitle = $Matches['hcmt'].Trim()
        }

        if (-not [string]::IsNullOrWhiteSpace($secTitle) -and $secTitle.Length -gt 3 -and $secTitle -notmatch 'slide') {
            $currentSec.EndLine = [Math]::Max($currentSec.StartLine, $lineNo - 1)
            $nextGate = ""
            if (($i + 1) -lt $RazorLines.Count -and $RazorLines[$i + 1] -match '^\s*@if\s*\((?<g>.+)\)\s*$') {
                $nextGate = $Matches['g'].Trim()
            }
            $currentSec = [PSCustomObject]@{
                Title     = $secTitle
                StartLine = $lineNo
                EndLine   = $RazorLines.Count
                Gate      = $nextGate
            }
            $sections.Add($currentSec)
        }
    }

    foreach ($sec in $sections) {
        $inputs = [System.Collections.Generic.List[string]]::new()
        $buttons = [System.Collections.Generic.List[string]]::new()
        $grids = [System.Collections.Generic.List[string]]::new()
        $tabs = [System.Collections.Generic.List[string]]::new()
        $gates = [System.Collections.Generic.List[string]]::new()

        $humanFields = [System.Collections.Generic.List[string]]::new()
        $humanButtons = [System.Collections.Generic.List[string]]::new()
        $humanGrids = [System.Collections.Generic.List[string]]::new()

        for ($idx = ($sec.StartLine - 1); $idx -lt $sec.EndLine; $idx++) {
            $line = $RazorLines[$idx]
            $lNum = $idx + 1

            if ($line -match '^\s*(?:@if|else\s+if)\s*\((?<cond>.+)\)\s*$') {
                $c = $Matches['cond'].Trim()
                if ($c -ne $sec.Gate) {
                    $gates.Add("``L${lNum}: @if ($c)``")
                }
            }

            if ($line -match '<CskTab\s+[^>]*Id="(?<tid>[^"]+)"') {
                $tId = $Matches['tid']
                $tabs.Add("``Tab[$tId] @L${lNum}``")
            }

            if ($line -match '<(?<itag>CskTextInput|CskDatePicker|CskSwitch|CskTextArea|DataListSelect|CskSelect|InputText)\b') {
                $iTag = $Matches['itag']
                $blk = Get-FullTagBlock -StartIdx $idx
                $bText = $blk.Text

                $lbl = if ($bText -match 'Label="(?<l>[^"]+)"') { $Matches['l'] }
                       elseif ($bText -match 'Placeholder="(?<p>[^"]+)"') { $Matches['p'] }
                       else { "" }
                $cleanLbl = $lbl
                if ($lbl.Length -gt 28) { $lbl = $lbl.Substring(0, 25) + "..." }

                $bind = if ($bText -match '@bind-Value="(?<b>[^"]+)"') { $Matches['b'] }
                        elseif ($bText -match 'Value="@(?<v>[^"]+)"') { $Matches['v'] }
                        else { "" }
                $vChg = if ($bText -match 'ValueChanged="@(?<vc>[^"]+)"') {
                    $rawVc = $Matches['vc']
                    if ($rawVc -match '=>\s*(?<fn>[A-Za-z0-9_]+)\s*\(') { " -> $($Matches['fn'])" } else { " -> $rawVc" }
                } else { "" }

                $lblPart = if (-not [string]::IsNullOrWhiteSpace($lbl)) { "`"$lbl`": " } else { "" }
                $inputs.Add("``${iTag}(${lblPart}${bind}${vChg})@L${lNum}``")

                if (-not [string]::IsNullOrWhiteSpace($cleanLbl) -and $cleanLbl -notmatch '^@') {
                    $humanFields.Add("*$cleanLbl*")
                } elseif (-not [string]::IsNullOrWhiteSpace($bind)) {
                    $humanFields.Add("``$bind``")
                }
            }

            if ($line -match '<CskButton\b') {
                $blk = Get-FullTagBlock -StartIdx $idx
                $bText = $blk.Text
                $onClick = if ($bText -match 'OnClick="@(?<oc>[^"]+)"') { $Matches['oc'].Trim() } else { "submit" }
                if ($onClick -match '=>\s*(?<fn>[A-Za-z0-9_]+)\s*\(') { $onClick = $Matches['fn'] }
                elseif ($onClick -match '=>\s*(?<asgn>[A-Za-z0-9_]+\s*=[^\)"]*)') { $onClick = $Matches['asgn'].Trim() }

                $btnTitle = if ($bText -match 'Title="(?<t>[^"]+)"') { $Matches['t'] } else { "" }
                if ([string]::IsNullOrWhiteSpace($btnTitle)) {
                    $combinedBtn = $RazorLines[$idx..([Math]::Min($idx + 4, $RazorLines.Count - 1))] -join " "
                    if ($combinedBtn -match '>\s*(?<txt>[^<@]{1,35}?)\s*</CskButton>') {
                        $btnTitle = $Matches['txt'].Trim() -replace '&amp;', '&'
                    }
                }
                $titleStr = if (-not [string]::IsNullOrWhiteSpace($btnTitle)) { "`"$btnTitle`" -> " } else { "" }
                $buttons.Add("``Btn(${titleStr}${onClick})@L${lNum}``")

                if (-not [string]::IsNullOrWhiteSpace($btnTitle)) {
                    $humanButtons.Add("**$btnTitle**")
                } else {
                    $humanButtons.Add("``$onClick``")
                }
            }

            if ($line -match '<CskDataGrid\s+[^>]*Items="@(?<items>[^"]+)"') {
                $gItems = $Matches['items']
                $cols = [System.Collections.Generic.List[string]]::new()
                for ($gi = $idx + 1; $gi -lt [Math]::Min($idx + 80, $RazorLines.Count); $gi++) {
                    if ($RazorLines[$gi] -match '</CskDataGrid>') { break }
                    if ($RazorLines[$gi] -match 'Title="(?<ctitle>[^"]+)"') {
                        $cols.Add($Matches['ctitle'])
                    }
                }
                $grids.Add("``CskDataGrid(Items=$gItems, Cols=[$($cols -join ', ')])@L${lNum}``")
                $humanGrids.Add("Table ($($cols -join ', '))")
            }
        }

        if ($inputs.Count -eq 0 -and $buttons.Count -eq 0 -and $grids.Count -eq 0 -and $gates.Count -eq 0 -and $tabs.Count -eq 0) {
            continue
        }

        # Build Human Overview Line
        $hParts = [System.Collections.Generic.List[string]]::new()
        if ($humanFields.Count -gt 0) { $hParts.Add("Fields: $($humanFields -join ', ')") }
        if ($humanButtons.Count -gt 0) { $hParts.Add("Buttons: $($humanButtons -join ', ')") }
        if ($humanGrids.Count -gt 0) { $hParts.Add("$($humanGrids -join '; ')") }
        if ($hParts.Count -gt 0) {
            $humanLines.Add("- **$($sec.Title)**: $($hParts -join ' | ')")
        }

        # Build Compact UI Map Table Row (for .index.md)
        $gateCell = if (-not [string]::IsNullOrWhiteSpace($sec.Gate)) { "``@if ($($sec.Gate))``" } elseif ($gates.Count -gt 0) { ($gates -join ', ') } else { "Unconditional" }
        $ctrlParts = [System.Collections.Generic.List[string]]::new()
        if ($tabs.Count -gt 0) { $ctrlParts.Add("Tabs: $($tabs -join ', ')") }
        if ($inputs.Count -gt 0) { $ctrlParts.Add("Inputs: $($inputs -join ', ')") }
        if ($buttons.Count -gt 0) { $ctrlParts.Add("Buttons: $($buttons -join ', ')") }
        if ($grids.Count -gt 0) { $ctrlParts.Add("Grids: $($grids -join ', ')") }
        $ctrlCell = if ($ctrlParts.Count -gt 0) { ($ctrlParts -join " <br> ") } else { "-" }
        $uiMapRows.Add("| **$($sec.Title)** | ``L$($sec.StartLine)-L$($sec.EndLine)`` | $gateCell | $ctrlCell |")
    }

    return [PSCustomObject]@{
        HumanOverview = $humanLines
        AgentTree     = $treeLines
        UiMapRows     = $uiMapRows
    }
}

# --- 2. Pass 1: Index Solution Services & Workspace Razor Components ---
Write-Host "==> Pass 1: Indexing backend services & workspace Razor components..." -ForegroundColor Cyan
$serviceIndex = @{} # Key: "ServiceClassName.MethodName" or "IServiceName.MethodName" -> MethodInfo

$serviceFiles = Get-ChildItem -Path $srcDir -Recurse -Filter "*Service*.cs" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and
        $_.FullName -notmatch 'BlazorFluent\.' -and
        $_.Name -notmatch '^I[A-Z]'
    }

foreach ($sf in $serviceFiles) {
    $sfLines = [System.IO.File]::ReadAllLines($sf.FullName, [System.Text.Encoding]::UTF8)
    $sfText = $sfLines -join "`n"
    $classMatch = [regex]::Match($sfText, 'class\s+(?<cls>[A-Za-z0-9_]+)\s*(?::\s*(?<ifaces>[A-Za-z0-9_,\s<>]+))?')
    if (-not $classMatch.Success) { continue }

    $clsName = $classMatch.Groups['cls'].Value
    $ifaces = @()
    if ($classMatch.Groups['ifaces'].Success) {
        $ifaces = $classMatch.Groups['ifaces'].Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^I[A-Z]' }
    }

    $cleanSfPath = Format-CleanPath $sf.FullName
    $parsedMethods = Get-CSharpMethods -FilePath $cleanSfPath -Lines $sfLines

    foreach ($pm in $parsedMethods) {
        $info = [PSCustomObject]@{
            ClassName     = $clsName
            Interfaces    = $ifaces
            MethodName    = $pm.Name
            ReturnType    = $pm.ReturnType
            FilePath      = $cleanSfPath
            StartLine     = $pm.StartLine
            EndLine       = $pm.EndLine
            DbSets        = $pm.DbSets
            SavesDb       = $pm.SavesDb
            Failures      = $pm.Failures
            SecurityGates = $pm.SecurityGates
            Audits        = $pm.Audits
        }
        $serviceIndex["$clsName.$($pm.Name)"] = $info
        foreach ($ifc in $ifaces) {
            $serviceIndex["$ifc.$($pm.Name)"] = $info
        }
    }
}

# Index all .razor components in src/ by component name
$razorComponentIndex = @{} # ComponentName -> CleanPath
$allRazorFiles = Get-ChildItem -Path $srcDir -Recurse -Filter "*.razor" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and
        $_.FullName -notmatch 'BlazorFluent\.'
    }
foreach ($rf in $allRazorFiles) {
    $compName = [System.IO.Path]::GetFileNameWithoutExtension($rf.Name)
    $razorComponentIndex[$compName] = Format-CleanPath $rf.FullName
}

Write-Host "  Indexed $($serviceIndex.Count) service method endpoints across $($serviceFiles.Count) service classes, and $($razorComponentIndex.Count) Razor components." -ForegroundColor Green

# --- 3. Pass 2 & 3: Analyze Each Target & Generate Ultra-Lean .requirements.md ---
$generatedSpecsSummary = [System.Collections.Generic.List[PSObject]]::new()
$globalUnregisteredAlerts = [System.Collections.Generic.List[PSObject]]::new()
$htmlSpecPayloads = [System.Collections.Generic.List[PSObject]]::new()

foreach ($targetEntry in $targetEntries) {
    $cleanRelPath = Format-CleanPath $targetEntry.path
    $fullPath = Resolve-FullSourcePath $cleanRelPath
    if (-not (Test-Path $fullPath)) {
        Write-Host "  [WARNING] Target file not found, skipping: $cleanRelPath" -ForegroundColor Yellow
        continue
    }

    $fileName = [System.IO.Path]::GetFileName($fullPath)
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($fullPath)
    $isRazor = ($fileName -match '\.razor$')
    $specFileName = "$baseName.requirements.md"
    $indexFileName = "$baseName.index.md"
    $historyFileName = "$baseName.history.md"
    $specFullPath = Join-Path $specsDir $specFileName
    $indexFullPath = Join-Path $indexDir $indexFileName
    $historyFullPath = Join-Path $historyDir $historyFileName

    Write-Host "==> Generating lean spec + index for: $cleanRelPath -> docs/specs/$specFileName + docs/specs/index/$indexFileName" -ForegroundColor Cyan

    # Compute SHA-256 hash of source file(s)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $sourceBytes = [System.IO.File]::ReadAllBytes($fullPath)
    $codeBehindCleanPath = ""
    $codeBehindFullPath = "$fullPath.cs"
    $hasCodeBehind = $false
    if ($isRazor -and (Test-Path $codeBehindFullPath)) {
        $hasCodeBehind = $true
        $codeBehindCleanPath = "$cleanRelPath.cs"
        $cbBytes = [System.IO.File]::ReadAllBytes($codeBehindFullPath)
        $combined = New-Object byte[] ($sourceBytes.Length + $cbBytes.Length)
        [System.Array]::Copy($sourceBytes, 0, $combined, 0, $sourceBytes.Length)
        [System.Array]::Copy($cbBytes, 0, $combined, $sourceBytes.Length, $cbBytes.Length)
        $sourceBytes = $combined
    }
    $sourceHash = ([System.BitConverter]::ToString($sha.ComputeHash($sourceBytes))).Replace("-", "").ToLowerInvariant().Substring(0, 16)

    # --- Preserve Existing Protected User Blocks & Externalize Change History ---
    $defaultPendingPlaceholder = "- [ ] *(Add new/modified requirements here, then prompt: ``Implement @[docs/specs/$specFileName]``)*"
    $defaultChangeHistoryPlaceholder = "*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when ``-ArchiveCompleted`` is run.)*"
    $preservedPendingChanges = $defaultPendingPlaceholder
    $preservedBusinessRules = "- *(Permanent domain invariants preserved across regenerations)*"
    $preservedChangeHistory = $defaultChangeHistoryPlaceholder

    if (Test-Path $historyFullPath) {
        $existingHist = [System.IO.File]::ReadAllText($historyFullPath, [System.Text.Encoding]::UTF8)
        $hMatch = [regex]::Match($existingHist, '(?s)<!-- BEGIN CHANGE HISTORY -->\s*(?<content>.*?)\s*<!-- END CHANGE HISTORY -->')
        if ($hMatch.Success -and -not [string]::IsNullOrWhiteSpace($hMatch.Groups['content'].Value)) {
            $preservedChangeHistory = $hMatch.Groups['content'].Value.Trim()
        }
    }

    if (Test-Path $specFullPath) {
        $existingSpec = [System.IO.File]::ReadAllText($specFullPath, [System.Text.Encoding]::UTF8)
        $pcMatch = [regex]::Match($existingSpec, '(?s)<!-- BEGIN PENDING CHANGES -->\s*(?<content>.*?)\s*<!-- END PENDING CHANGES -->')
        if ($pcMatch.Success -and -not [string]::IsNullOrWhiteSpace($pcMatch.Groups['content'].Value)) {
            $preservedPendingChanges = $pcMatch.Groups['content'].Value.Trim()
        }

        $brMatch = [regex]::Match($existingSpec, '(?s)<!-- BEGIN CUSTOM BUSINESS RULES -->\s*(?<content>.*?)\s*<!-- END CUSTOM BUSINESS RULES -->')
        if ($brMatch.Success -and -not [string]::IsNullOrWhiteSpace($brMatch.Groups['content'].Value)) {
            $preservedBusinessRules = $brMatch.Groups['content'].Value.Trim()
        }

        # Migrate any inline <!-- BEGIN CHANGE HISTORY --> from legacy .requirements.md into history/<BaseName>.history.md
        $chMatch = [regex]::Match($existingSpec, '(?s)<!-- BEGIN CHANGE HISTORY -->\s*(?<content>.*?)\s*<!-- END CHANGE HISTORY -->')
        if ($chMatch.Success -and -not [string]::IsNullOrWhiteSpace($chMatch.Groups['content'].Value)) {
            $legacyHist = $chMatch.Groups['content'].Value.Trim()
            if ($legacyHist -notmatch '^\*\(No archived changes yet' -and $preservedChangeHistory -match '^\*\(No archived changes yet') {
                $preservedChangeHistory = $legacyHist
            }
        }

        # Rescue any inline [CHANGE: ...] or [TODO: ...] written outside the protected blocks
        $outsideText = [regex]::Replace($existingSpec, '(?s)<!-- BEGIN PENDING CHANGES -->.*?<!-- END PENDING CHANGES -->', '')
        $outsideText = [regex]::Replace($existingSpec, '(?s)<!-- BEGIN CUSTOM BUSINESS RULES -->.*?<!-- END CUSTOM BUSINESS RULES -->', '')
        $outsideText = [regex]::Replace($outsideText, '(?s)<!-- BEGIN CHANGE HISTORY -->.*?<!-- END CHANGE HISTORY -->', '')
        $inlineChanges = [regex]::Matches($outsideText, '(?m)^.*\[(?:CHANGE|TODO)\s*:\s*(?<note>[^\]]+)\].*$')
        if ($inlineChanges.Count -gt 0) {
            $rescuedList = [System.Collections.Generic.List[string]]::new()
            foreach ($ic in $inlineChanges) {
                $note = $ic.Groups['note'].Value.Trim()
                if ($note -eq 'your note' -or $note -eq '...') { continue }
                if ($preservedPendingChanges -notmatch [regex]::Escape($note)) {
                    $rescuedList.Add("- [ ] **[RESCUED INLINE CHANGE]**: $note")
                }
            }
            if ($rescuedList.Count -gt 0) {
                $preservedPendingChanges = $preservedPendingChanges + "`n" + ($rescuedList -join "`n")
                Write-Host "    [!] Rescued $($rescuedList.Count) inline [CHANGE:] annotation(s) into PENDING CHANGES block." -ForegroundColor Yellow
            }
        }

        # If -ArchiveCompleted is passed, move active Pending Changes into docs/specs/history/<BaseName>.history.md with today's date/time stamp
        if ($ArchiveCompleted) {
            $activeLines = [System.Collections.Generic.List[string]]::new()
            foreach ($pLine in ($preservedPendingChanges -split "`r?`n")) {
                $trimmedLine = $pLine.Trim()
                if ([string]::IsNullOrWhiteSpace($trimmedLine)) { continue }
                if ($trimmedLine -match '\*\(Add (?:new/modified|pending) ') { continue }
                # Preserve Acceptance Criteria sub-indents or normalize top-level items into completed [x] items
                if ($pLine -match '^\s{2,}(.+)$' -or $trimmedLine -match '^Acceptance Criteria:') {
                    $activeLines.Add("  $trimmedLine")
                } elseif ($trimmedLine -match '^-\s*\[\s*\]\s*(.+)$') {
                    $activeLines.Add("- [x] $($Matches[1])")
                } elseif ($trimmedLine -match '^-\s*\[x\]\s*(.+)$') {
                    $activeLines.Add("- [x] $($Matches[1])")
                } elseif ($trimmedLine -match '^(?:\d+\.|-|\*)\s+(.+)$') {
                    $activeLines.Add("- [x] $($Matches[1])")
                } else {
                    $activeLines.Add("- [x] $trimmedLine")
                }
            }

            if ($activeLines.Count -gt 0) {
                $stampHeader = "### $((Get-Date).ToString('yyyy-MM-dd HH:mm'))"
                $newHistoryEntry = $stampHeader + "`n" + ($activeLines -join "`n")
                if ([string]::IsNullOrWhiteSpace($preservedChangeHistory) -or $preservedChangeHistory -match '^\*\(No archived changes yet') {
                    $preservedChangeHistory = $newHistoryEntry
                } else {
                    $preservedChangeHistory = $newHistoryEntry + "`n`n" + $preservedChangeHistory
                }
                $preservedPendingChanges = $defaultPendingPlaceholder
                Write-Host "    [+] Archived $($activeLines.Count) completed line(s) into docs/specs/history/$historyFileName ($stampHeader)." -ForegroundColor Green
            }
        }
    }

    # Write / Update docs/specs/history/<BaseName>.history.md (Zero token overhead during normal spec implementation)
    $histSb = [System.Text.StringBuilder]::new()
    [void]$histSb.AppendLine("# Completed Change History: ``$baseName``")
    [void]$histSb.AppendLine("")
    [void]$histSb.AppendLine("> **Active Spec**: [$specFileName](../$specFileName) | **Navigation Index**: [$indexFileName](../index/$indexFileName)")
    [void]$histSb.AppendLine("")
    [void]$histSb.AppendLine("<!-- BEGIN CHANGE HISTORY -->")
    [void]$histSb.AppendLine($preservedChangeHistory)
    [void]$histSb.AppendLine("<!-- END CHANGE HISTORY -->")
    [System.IO.File]::WriteAllText($historyFullPath, $histSb.ToString(), $utf8Bom)

    # --- Parse Source File(s) ---
    $routes = [System.Collections.Generic.List[string]]::new()
    $authAttributes = [System.Collections.Generic.List[string]]::new()
    $injectedServices = [System.Collections.Generic.Dictionary[string, string]]::new()
    $uiGates = [System.Collections.Generic.List[PSObject]]::new()
    $dataGrids = [System.Collections.Generic.List[PSObject]]::new()
    $uiEvents = [System.Collections.Generic.List[PSObject]]::new()
    $csMethods = [System.Collections.Generic.List[PSObject]]::new()
    $discoveredChildUsages = [System.Collections.Generic.List[PSObject]]::new()

    if ($isRazor) {
        $razorLines = [System.IO.File]::ReadAllLines($fullPath, [System.Text.Encoding]::UTF8)
        $currentGateCondition = "Unconditional"

        for ($li = 0; $li -lt $razorLines.Count; $li++) {
            $rLine = $razorLines[$li]
            $lineNo = $li + 1

            if ($rLine -match '^\s*@page\s+"(?<route>[^"]+)"') {
                $routes.Add($Matches['route'])
            }
            if ($rLine -match '^\s*@attribute\s+\[(?<attr>[^\]]+)\]') {
                $authAttributes.Add($Matches['attr'])
            }
            if ($rLine -match '^\s*@inject\s+(?<type>[A-Za-z0-9_<>]+)\s+(?<prop>[A-Za-z0-9_]+)') {
                $injectedServices[$Matches['prop']] = $Matches['type']
            }
            if ($rLine -match '^\s*(?:@if|else\s+if)\s*\((?<cond>.+)\)\s*$') {
                $currentGateCondition = $Matches['cond'].Trim()
                if ($currentGateCondition -match 'IsRootAdmin|_canEdit|_show|_managing|_editing|_isLoading|_activeTabId|CanView') {
                    $uiGates.Add([PSCustomObject]@{ Line = $lineNo; Condition = $currentGateCondition })
                }
            }

            # Detect PascalCase Child Razor Components
            foreach ($tagMatch in [regex]::Matches($rLine, '<(?<tag>[A-Z][A-Za-z0-9_]*)\b(?<attrs>[^>]*?)(?:/>|>)')) {
                $tagName = $tagMatch.Groups['tag'].Value
                if ($tagName -eq $baseName) { continue }

                $isIgnored = $false
                foreach ($pref in $ignorePrefixes) {
                    if ($tagName.StartsWith($pref, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $isIgnored = $true
                        break
                    }
                }
                if (-not $isIgnored -and ($ignoreComponents -contains $tagName)) {
                    $isIgnored = $true
                }
                if ($isIgnored) { continue }

                if ($razorComponentIndex.ContainsKey($tagName)) {
                    $childCleanPath = $razorComponentIndex[$tagName]
                    $rawAttrs = $tagMatch.Groups['attrs'].Value.Trim()
                    $alreadyCaptured = $discoveredChildUsages | Where-Object { $_.ComponentName -eq $tagName }
                    if (-not $alreadyCaptured) {
                        $discoveredChildUsages.Add([PSCustomObject]@{
                            ComponentName = $tagName
                            CleanPath     = $childCleanPath
                            Line          = $lineNo
                            GateCondition = $currentGateCondition
                            Parameters    = if ([string]::IsNullOrWhiteSpace($rawAttrs)) { "None" } else { $rawAttrs }
                        })
                    }
                }
            }

            # DataGrids
            if ($rLine -match '<CskDataGrid\s+[^>]*Items="@(?<items>[^"]+)"') {
                $gridItems = $Matches['items']
                $cols = [System.Collections.Generic.List[string]]::new()
                for ($gi = $li + 1; $gi -lt [Math]::Min($li + 80, $razorLines.Count); $gi++) {
                    if ($razorLines[$gi] -match '</CskDataGrid>') { break }
                    if ($razorLines[$gi] -match 'Title="(?<title>[^"]+)"') {
                        $cols.Add($Matches['title'])
                    }
                }
                $dataGrids.Add([PSCustomObject]@{
                    Line         = $lineNo
                    ItemsBinding = $gridItems
                    Columns      = ($cols -join ", ")
                })
            }

            # UI Event Bindings
            foreach ($evMatch in [regex]::Matches($rLine, '(?<ev>OnClick|ValueChanged|OnValidSubmit|OnDismiss)="@(?<handler>[^"]+)"')) {
                $evType = $evMatch.Groups['ev'].Value
                $rawHandler = $evMatch.Groups['handler'].Value.Trim()
                $cleanHandler = $rawHandler
                if ($cleanHandler -match '=>\s*(?<fn>[A-Za-z0-9_]+)\s*\(') {
                    $cleanHandler = $Matches['fn']
                } elseif ($cleanHandler -match '=>\s*(?<assign>[A-Za-z0-9_]+\s*=[^\)"]*)') {
                    $cleanHandler = $Matches['assign'].Trim()
                }
                $uiEvents.Add([PSCustomObject]@{
                    Line        = $lineNo
                    Event       = $evType
                    RawBinding  = $rawHandler
                    HandlerName = $cleanHandler
                })
            }
        }

        $razorRaw = $razorLines -join "`n"
        if ($razorRaw -match '@code\s*\{') {
            $inlineMethods = Get-CSharpMethods -FilePath $cleanRelPath -Lines $razorLines
            foreach ($im in $inlineMethods) { $csMethods.Add($im) }
        }

        if ($hasCodeBehind) {
            $cbLines = [System.IO.File]::ReadAllLines($codeBehindFullPath, [System.Text.Encoding]::UTF8)
            for ($ci = 0; $ci -lt $cbLines.Count; $ci++) {
                $cLine = $cbLines[$ci]
                if ($cLine -match '\[Inject\]\s*(?:private|protected|public)?\s*(?<type>[A-Za-z0-9_<>]+)\s+(?<prop>[A-Za-z0-9_]+)') {
                    $injectedServices[$Matches['prop']] = $Matches['type']
                }
            }
            $cbMethods = Get-CSharpMethods -FilePath $codeBehindCleanPath -Lines $cbLines
            foreach ($cbm in $cbMethods) { $csMethods.Add($cbm) }
        }
    } else {
        # Standalone C# File
        $csLines = [System.IO.File]::ReadAllLines($fullPath, [System.Text.Encoding]::UTF8)
        for ($ci = 0; $ci -lt $csLines.Count; $ci++) {
            $cLine = $csLines[$ci]
            if ($cLine -match '^\s*private\s+readonly\s+(?<type>[A-Za-z0-9_<>]+)\s+(?<field>_[A-Za-z0-9_]+)\s*;') {
                $injectedServices[$Matches['field']] = $Matches['type']
            }
            if ($cLine -match '(?:var|[A-Za-z0-9_<>]+)\s+(?<vname>[A-Za-z0-9_]+)\s*=\s*[^;]*?(?:GetRequiredService|GetService)<(?<stype>[A-Za-z0-9_<>]+)>') {
                $injectedServices[$Matches['vname']] = $Matches['stype']
            } elseif ($cLine -match 'GetRequiredService<(?<stype>[A-Za-z0-9_<>]+)>|GetService<(?<stype>[A-Za-z0-9_<>]+)>') {
                $sType = $Matches['stype']
                $injectedServices["scoped:$sType"] = $sType
            }
        }
        $parsedCs = Get-CSharpMethods -FilePath $cleanRelPath -Lines $csLines
        foreach ($pcm in $parsedCs) { $csMethods.Add($pcm) }
    }

    # --- Partition Discovered Child Components into Registered vs Unregistered ---
    $registeredChildUsages = [System.Collections.Generic.List[PSObject]]::new()
    $unregisteredChildUsages = [System.Collections.Generic.List[PSObject]]::new()

    $manifestChildPaths = @{}
    if ($targetEntry.registeredChildren) {
        foreach ($rc in $targetEntry.registeredChildren) {
            $manifestChildPaths[$rc.path.ToLowerInvariant()] = $rc
        }
    }

    foreach ($dcu in $discoveredChildUsages) {
        $key = $dcu.CleanPath.ToLowerInvariant()
        if ($manifestChildPaths.ContainsKey($key)) {
            $rcMeta = $manifestChildPaths[$key]
            $registeredChildUsages.Add([PSCustomObject]@{
                ComponentName = $dcu.ComponentName
                CleanPath     = $dcu.CleanPath
                SpecFile      = "$($dcu.ComponentName).requirements.md"
                Line          = $dcu.Line
                GateCondition = $dcu.GateCondition
                Parameters    = $dcu.Parameters
                Category      = $rcMeta.category
                ThinkingLevel = $rcMeta.thinking_level
                Description   = $rcMeta.description
            })
        } else {
            $unregisteredChildUsages.Add($dcu)
        }
    }

    if ($unregisteredChildUsages.Count -gt 0) {
        Write-Host "    [!] WARNING: $($unregisteredChildUsages.Count) UNREGISTERED child component(s) found in $cleanRelPath : $(($unregisteredChildUsages.ComponentName) -join ', ')" -ForegroundColor Red
        $globalUnregisteredAlerts.Add([PSCustomObject]@{
            ParentPath     = $cleanRelPath
            ParentFileName = $fileName
            Category       = $targetEntry.category
            ThinkingLevel  = $targetEntry.thinking_level
            SpecFile       = "docs/specs/$specFileName"
            Children       = @($unregisteredChildUsages)
        })
    }

    # --- Map Method Call Chains to Backend Services ---
    $methodTraces = [System.Collections.Generic.List[PSObject]]::new()
    $methodDict = @{}
    foreach ($m in $csMethods) { $methodDict[$m.Name] = $m }

    foreach ($m in $csMethods) {
        $localCalls = [System.Collections.Generic.List[string]]::new()
        foreach ($otherM in $csMethods) {
            if ($otherM.Name -ne $m.Name) {
                if ($m.BodyText -match "\b$($otherM.Name)\s*\(") {
                    $localCalls.Add($otherM.Name)
                }
            }
        }

        $combinedBody = $m.BodyText
        foreach ($lc in $localCalls) {
            if ($methodDict.ContainsKey($lc)) {
                $combinedBody += "`n" + $methodDict[$lc].BodyText
            }
        }

        $serviceCalls = [System.Collections.Generic.List[PSObject]]::new()
        foreach ($kvp in $injectedServices.GetEnumerator()) {
            $propName = $kvp.Key
            $svcType = $kvp.Value
            if ($propName.StartsWith("scoped:")) { continue }
            if ($svcType -match '^ILogger\b' -or $svcType -eq 'AppDbContext') { continue }

            $callMatches = [regex]::Matches($combinedBody, "\b$([regex]::Escape($propName))\.(?<smethod>[A-Za-z0-9_]+)\s*\(")
            foreach ($cm in $callMatches) {
                $sMethod = $cm.Groups['smethod'].Value
                $lookupKey = "$svcType.$sMethod"
                $resolvedSvc = if ($serviceIndex.ContainsKey($lookupKey)) { $serviceIndex[$lookupKey] } else { $null }

                $alreadyAdded = $serviceCalls | Where-Object { $_.ServiceProp -eq $propName -and $_.MethodName -eq $sMethod }
                if (-not $alreadyAdded) {
                    $serviceCalls.Add([PSCustomObject]@{
                        ServiceProp = $propName
                        ServiceType = $svcType
                        MethodName  = $sMethod
                        Resolved    = $resolvedSvc
                    })
                }
            }
        }

        $isLifecycle = ($m.Name -in @("OnInitialized", "OnInitializedAsync", "OnParametersSet", "OnParametersSetAsync", "OnAfterRender", "OnAfterRenderAsync", "ExecuteAsync"))
        $methodTraces.Add([PSCustomObject]@{
            Method       = $m
            IsLifecycle  = $isLifecycle
            LocalCalls   = @($localCalls)
            ServiceCalls = @($serviceCalls)
        })
    }

    # --- Build Ultra-Lean Token-Optimized Pure-ASCII .requirements.md + index/<BaseName>.index.md ---
    $nowUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    $nowLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss zzz")
    $sb = [System.Text.StringBuilder]::new()
    $idxSb = [System.Text.StringBuilder]::new()

    # 1. [SYSTEM / AGENT RUNTIME] Header at Line 1 of .requirements.md (with 100% of Copilot docs/17 + Remaining Delta rules)
    [void]$sb.AppendLine("<!-- ===========================================================================")
    [void]$sb.AppendLine("[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]")
    [void]$sb.AppendLine("thinking_strategy: zero_waste_discovery_focused_planning")
    [void]$sb.AppendLine("thinking_level_hint: $($targetEntry.thinking_level) (0% wasted on file/line search; 100% focused on planning UI/logic changes)")
    [void]$sb.AppendLine("agent_mode: $defaultAgentMode")
    [void]$sb.AppendLine("token_budget_policy: $defaultTokenPolicy")
    [void]$sb.AppendLine("navigation_index: docs/specs/index/$indexFileName")
    [void]$sb.AppendLine("change_history: docs/specs/history/$historyFileName")
    [void]$sb.AppendLine("source_file: src/$cleanRelPath")
    if ($hasCodeBehind) {
        [void]$sb.AppendLine("code_behind: src/$codeBehindCleanPath")
    }
    if (-not [string]::IsNullOrWhiteSpace($targetEntry.parentSpecFile)) {
        [void]$sb.AppendLine("parent_spec: docs/specs/$($targetEntry.parentSpecFile) (src/$($targetEntry.parentPath))")
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):")
    [void]$sb.AppendLine("  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the navigation index line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter '$baseName' -EnsureFresh (exits in <50ms if unchanged, or refreshes docs/specs/index/$indexFileName while preserving Section 1 Pending Changes if external edits occurred).")
    [void]$sb.AppendLine("  RULE 1 [READ THE NAVIGATION INDEX]: Read 'docs/specs/index/$indexFileName' to get the UI Map [Lstart-Lend], Method Map, Service Map, and Validation Rules. Completely IGNORE 'docs/specs/history/$historyFileName' and 'docs/specs/validation/' unless explicitly asked.")
    [void]$sb.AppendLine("  RULE 2 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in 'docs/specs/index/$indexFileName' and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.")
    [void]$sb.AppendLine("  RULE 3 [REUSE-FIRST, NO-NEW-ARCHITECTURE & CHANGE BOUNDARY]: Reuse existing components (Csk*, DataListSelect, EntityHistoryButton), dialogs/modals, DTOs, validators, and services first. Do NOT introduce new services, repositories, or abstractions unless required. Modify ONLY referenced files and methods — do NOT refactor unrelated code, rename symbols, or perform opportunistic cleanup.")
    [void]$sb.AppendLine("  RULE 4 [CONDITIONAL PLANNING & COMPLEXITY CLASSIFICATION]:")
    [void]$sb.AppendLine("    - Small / Medium Change (<= 3 files modified, UI/validation updates, existing method/service updates): Implement immediately without waiting for plan approval.")
    [void]$sb.AppendLine("    - Large Change (> 3 files modified, DB schema changes, security model changes, new architecture, or new services/repositories): Output a concise 3-6 bullet 'Execution Plan' first and STOP for user approval ('yes' / Proceed).")
    [void]$sb.AppendLine("  RULE 5 [SURGICAL EDIT, AUTO-ARCHIVE & OUTPUT CONTRACT]: After applying edit_file calls, immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter '$baseName' -ArchiveCompleted (moves completed items to docs/specs/history/$historyFileName with date/time and refreshes line numbers). NEVER run: [`"$forbiddenCmds`"]. Return the standardized Output Contract: (1) Files Modified, (2) Change Summary, (3) Validation Performed, (4) Risks.")
    [void]$sb.AppendLine("============================================================================ -->")
    [void]$sb.AppendLine("# Spec: ``$baseName``")
    [void]$sb.AppendLine("")

    # 2. [WHAT I WANT CHANGED (PLAIN ENGLISH + OPTIONAL TAGS/ACCEPTANCE CRITERIA)] Block
    [void]$sb.AppendLine("## 1. Pending Changes (Write in Plain English)")
    [void]$sb.AppendLine("<!-- Write plain English below. For complex items, optionally prefix with [UI], [DATA], [SECURITY], [VALIDATION], [BUGFIX], or [PERFORMANCE] and add 'Acceptance Criteria:' bullets. Then prompt: Implement @[docs/specs/$specFileName] -->")
    [void]$sb.AppendLine("<!-- BEGIN PENDING CHANGES -->")
    [void]$sb.AppendLine($preservedPendingChanges)
    [void]$sb.AppendLine("<!-- END PENDING CHANGES -->")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## 2. Permanent & Implementation Rules")
    [void]$sb.AppendLine("<!-- BEGIN CUSTOM BUSINESS RULES -->")
    [void]$sb.AppendLine($preservedBusinessRules)
    [void]$sb.AppendLine("<!-- END CUSTOM BUSINESS RULES -->")
    [void]$sb.AppendLine("- **Reuse-First**: Reuse existing UI components, modals, DTOs, validators, and services before creating new code.")
    [void]$sb.AppendLine("- **No New Architecture**: Prefer extending existing methods; do not introduce new services, repositories, or abstractions unless explicitly requested.")
    [void]$sb.AppendLine("- **Change Boundary**: Modify only referenced files and methods. Do not refactor unrelated code, rename symbols, or perform opportunistic cleanup.")
    [void]$sb.AppendLine("- **Conditional Planning**: Implement Small/Medium changes (`<= 3` files, UI/validation/existing method updates) immediately; generate a plan and wait for approval only on Large changes (`> 3` files, DB schema, security model, or new services/architecture).")
    [void]$sb.AppendLine("")

    # 3. Ultra-Concise Business Context (Single Source of Truth for UI Map lives in index/<BaseName>.index.md)
    $razorSummaryObj = if ($isRazor) { Get-RazorLayoutSummaryAndTree -RazorLines $razorLines } else { $null }
    [void]$sb.AppendLine("## 3. Business Context")
    if (-not [string]::IsNullOrWhiteSpace($targetEntry.description)) {
        [void]$sb.AppendLine("- **Purpose**: $($targetEntry.description)")
    }
    [void]$sb.AppendLine("")

    # Write the ultra-lean human .requirements.md file with UTF-8 BOM
    [System.IO.File]::WriteAllText($specFullPath, $sb.ToString(), $utf8Bom)

    # --- Build Auto-Generated AI Navigation Index: docs/specs/index/<BaseName>.index.md ---
    [void]$idxSb.AppendLine("<!-- ===========================================================================")
    [void]$idxSb.AppendLine("[AI NAVIGATION INDEX - AUTO-GENERATED BY scripts/Generate-Specs.ps1]")
    [void]$idxSb.AppendLine("target_spec: docs/specs/$specFileName")
    [void]$idxSb.AppendLine("source_file: src/$cleanRelPath")
    if ($hasCodeBehind) {
        [void]$idxSb.AppendLine("code_behind: src/$codeBehindCleanPath")
    }
    [void]$idxSb.AppendLine("generated_at_utc: $nowUtc")
    [void]$idxSb.AppendLine("source_hash_sha256: $sourceHash")
    [void]$idxSb.AppendLine("============================================================================ -->")
    [void]$idxSb.AppendLine("# AI Navigation Index: ``$baseName``")
    [void]$idxSb.AppendLine("")

    # A. Compact UI Map Table
    [void]$idxSb.AppendLine("## 1. UI Map (``$fileName``)")
    if ($authAttributes.Count -gt 0) {
        [void]$idxSb.AppendLine("- **Auth**: ``$($authAttributes -join '``, ``')``")
    }
    if ($injectedServices.Count -gt 0) {
        $injPairs = [System.Collections.Generic.List[string]]::new()
        foreach ($kvp in $injectedServices.GetEnumerator()) {
            $injPairs.Add("``$($kvp.Key):$($kvp.Value)``")
        }
        [void]$idxSb.AppendLine("- **Injected**: $($injPairs -join ', ')")
    }
    if ($registeredChildUsages.Count -gt 0) {
        [void]$idxSb.AppendLine("- **Registered Child Components**:")
        foreach ($rcu in $registeredChildUsages) {
            [void]$idxSb.AppendLine("  - ``<$($rcu.ComponentName) />`` @ ``L$($rcu.Line)`` | Gate: ``$($rcu.GateCondition)`` | Params: ``$($rcu.Parameters)`` | Spec: [$($rcu.SpecFile)](../$($rcu.SpecFile))")
        }
    }
    if ($razorSummaryObj -and $razorSummaryObj.UiMapRows.Count -gt 0) {
        [void]$idxSb.AppendLine("")
        [void]$idxSb.AppendLine("| UI Section | Lines | Visibility Gate | Fields (`@bind`), Buttons (`OnClick`) & Grids |")
        [void]$idxSb.AppendLine("| :--- | :--- | :--- | :--- |")
        foreach ($row in $razorSummaryObj.UiMapRows) {
            [void]$idxSb.AppendLine($row)
        }
    }
    [void]$idxSb.AppendLine("")

    # B. Compact Method Map & End-to-End Call Chains
    $methodTargetFile = if ($hasCodeBehind) { $codeBehindCleanPath } else { $cleanRelPath }
    [void]$idxSb.AppendLine("## 2. Method Map (``$methodTargetFile``)")
    [void]$idxSb.AppendLine("| Method | Lines | Role / Trigger | End-to-End Call Chain (`[PreGates] -> Service.Method[Gates](DB/Audit)`) |")
    [void]$idxSb.AppendLine("| :--- | :--- | :--- | :--- |")

    foreach ($mt in $methodTraces) {
        $m = $mt.Method
        $matchingEvents = @($uiEvents | Where-Object { $_.HandlerName -eq $m.Name })
        $roleTrigger = if ($mt.IsLifecycle) {
            "[INIT]"
        } elseif ($matchingEvents.Count -gt 0) {
            ($matchingEvents | ForEach-Object { "$($_.Event)@L$($_.Line)" }) -join ", "
        } else {
            "[HELPER]"
        }

        $chainParts = [System.Collections.Generic.List[string]]::new()
        if ($m.SecurityGates.Count -gt 0) {
            $chainParts.Add("``[$($m.SecurityGates -join ',')]``")
        }
        foreach ($lc in $mt.LocalCalls) {
            $chainParts.Add("``local:$lc()``")
        }
        foreach ($sc in $mt.ServiceCalls) {
            if ($sc.Resolved) {
                $r = $sc.Resolved
                $gateTag = if ($r.SecurityGates.Count -gt 0) { "[$($r.SecurityGates -join ',')]" } else { "" }
                $metaItems = [System.Collections.Generic.List[string]]::new()
                if ($r.DbSets.Count -gt 0) {
                    $rw = if ($r.SavesDb) { "DB[W]" } else { "DB[R]" }
                    $metaItems.Add("${rw}:$($r.DbSets -join ',')")
                }
                if ($r.Audits.Count -gt 0) {
                    $metaItems.Add("Audit:$($r.Audits -join ',')")
                }
                $metaStr = if ($metaItems.Count -gt 0) { "($($metaItems -join '; '))" } else { "" }
                $chainParts.Add("``$($sc.ServiceProp).$($sc.MethodName)${gateTag}${metaStr}``")
            } else {
                $chainParts.Add("``$($sc.ServiceProp).$($sc.MethodName)()``")
            }
        }
        if ($m.DbSets.Count -gt 0) {
            $mrw = if ($m.SavesDb) { "DB[W]" } else { "DB[R]" }
            $chainParts.Add("``${mrw}:$($m.DbSets -join ',')``")
        }
        if ($m.Audits.Count -gt 0) {
            $chainParts.Add("``Audit:$($m.Audits -join ',')``")
        }

        $chainStr = if ($chainParts.Count -gt 0) { $chainParts -join " -> " } else { "Local UI State" }
        if ($m.Failures.Count -gt 0) {
            $chainStr += " | Fails: ``$($m.Failures -join '; ')``"
        }

        [void]$idxSb.AppendLine("| ``$($m.Name)`` | ``L$($m.StartLine)-L$($m.EndLine)`` | $roleTrigger | $chainStr |")
    }
    [void]$idxSb.AppendLine("")

    # C. Compact Downstream Service Map & Validation Rules
    $uniqueResolvedServices = [System.Collections.Generic.Dictionary[string, PSObject]]::new()
    foreach ($mt in $methodTraces) {
        foreach ($sc in $mt.ServiceCalls) {
            if ($sc.Resolved) {
                $rKey = "$($sc.Resolved.ClassName).$($sc.Resolved.MethodName)"
                if (-not $uniqueResolvedServices.ContainsKey($rKey)) {
                    $uniqueResolvedServices[$rKey] = $sc.Resolved
                }
            }
        }
    }

    if ($uniqueResolvedServices.Count -gt 0) {
        [void]$idxSb.AppendLine("## 3. Service Map")
        [void]$idxSb.AppendLine("| Service Method | Source Location | Security Gates | DB & Audit |")
        [void]$idxSb.AppendLine("| :--- | :--- | :--- | :--- |")
        foreach ($rk in $uniqueResolvedServices.Keys) {
            $rs = $uniqueResolvedServices[$rk]
            $gatesCell = if ($rs.SecurityGates.Count -gt 0) { "``$($rs.SecurityGates -join ', ')``" } else { "-" }
            $dbAuditParts = [System.Collections.Generic.List[string]]::new()
            if ($rs.DbSets.Count -gt 0) {
                $rw = if ($rs.SavesDb) { "DB[W]" } else { "DB[R]" }
                $dbAuditParts.Add("``${rw}: $($rs.DbSets -join ',')``")
            }
            if ($rs.Audits.Count -gt 0) {
                $dbAuditParts.Add("``Audit: $($rs.Audits -join ',')``")
            }
            $dbAuditCell = if ($dbAuditParts.Count -gt 0) { $dbAuditParts -join " " } else { "In-Memory" }
            [void]$idxSb.AppendLine("| ``$($rs.ClassName).$($rs.MethodName)`` | ``$($rs.FilePath)#L$($rs.StartLine)-L$($rs.EndLine)`` | $gatesCell | $dbAuditCell |")
        }
        [void]$idxSb.AppendLine("")

        # D. Grouped Validation Rules (Only emit methods that contain validation failures)
        $methodsWithFailures = @($uniqueResolvedServices.Values | Where-Object { $_.Failures -and $_.Failures.Count -gt 0 })
        if ($methodsWithFailures.Count -gt 0) {
            [void]$idxSb.AppendLine("## 4. Validation Rules")
            [void]$idxSb.AppendLine("")
            foreach ($mwf in $methodsWithFailures) {
                [void]$idxSb.AppendLine("### $($mwf.ClassName).$($mwf.MethodName)")
                [void]$idxSb.AppendLine("")
                foreach ($fRule in $mwf.Failures) {
                    $cleanRule = $fRule.Trim()
                    if ($cleanRule.StartsWith('$"')) { $cleanRule = $cleanRule.Substring(2) }
                    elseif ($cleanRule.StartsWith('"')) { $cleanRule = $cleanRule.Substring(1) }
                    if ($cleanRule.EndsWith('"')) { $cleanRule = $cleanRule.Substring(0, $cleanRule.Length - 1) }
                    [void]$idxSb.AppendLine("- $cleanRule")
                }
                [void]$idxSb.AppendLine("")
            }
        }
    }

    # Write docs/specs/index/<BaseName>.index.md with UTF-8 BOM
    [System.IO.File]::WriteAllText($indexFullPath, $idxSb.ToString(), $utf8Bom)

    # --- If -GenerateHtml is requested, build rich Mermaid diagrams for the HTML viewer only ---
    if ($GenerateHtml) {
        $htmlMdSb = [System.Text.StringBuilder]::new()
        [void]$htmlMdSb.Append($sb.ToString())
        [void]$htmlMdSb.AppendLine("---")
        [void]$htmlMdSb.AppendLine("## Visual Mermaid Flows (HTML Explorer Only)")
        [void]$htmlMdSb.AppendLine("")

        if ($registeredChildUsages.Count -gt 0) {
            [void]$htmlMdSb.AppendLine("### Parent-to-Child Composition")
            [void]$htmlMdSb.AppendLine('```mermaid')
            [void]$htmlMdSb.AppendLine("flowchart LR")
            [void]$htmlMdSb.AppendLine("    Parent[""$baseName ($fileName)""]")
            foreach ($rcu in $registeredChildUsages) {
                $cId = $rcu.ComponentName -replace '[^A-Za-z0-9_]', ''
                $gateShort = $rcu.GateCondition -replace '"', "'"
                [void]$htmlMdSb.AppendLine("    $cId[""<$($rcu.ComponentName) />""]")
                [void]$htmlMdSb.AppendLine("    Parent -->|""L$($rcu.Line): $gateShort""| $cId")
            }
            [void]$htmlMdSb.AppendLine('```')
            [void]$htmlMdSb.AppendLine("")
        }

        $lifecycleTraces = @($methodTraces | Where-Object { $_.IsLifecycle })
        if ($lifecycleTraces.Count -gt 0) {
            [void]$htmlMdSb.AppendLine("### Initialization & Load Sequence")
            [void]$htmlMdSb.AppendLine('```mermaid')
            [void]$htmlMdSb.AppendLine("sequenceDiagram")
            [void]$htmlMdSb.AppendLine("    autonumber")
            [void]$htmlMdSb.AppendLine("    actor Caller as Caller / Router")
            [void]$htmlMdSb.AppendLine("    participant Comp as $baseName")
            $declaredParticipants = [System.Collections.Generic.HashSet[string]]::new()
            $hasDbParticipant = $false
            foreach ($lt in $lifecycleTraces) {
                foreach ($sc in $lt.ServiceCalls) {
                    $partId = $sc.ServiceType -replace '[^A-Za-z0-9_]', ''
                    $pLabel = if ($sc.Resolved) { $sc.Resolved.ClassName } else { $sc.ServiceType }
                    if ($declaredParticipants.Add($partId)) {
                        [void]$htmlMdSb.AppendLine("    participant $partId as $pLabel")
                    }
                    if ($sc.Resolved -and $sc.Resolved.DbSets.Count -gt 0) { $hasDbParticipant = $true }
                }
                if ($lt.Method.DbSets.Count -gt 0) { $hasDbParticipant = $true }
            }
            if ($hasDbParticipant) {
                [void]$htmlMdSb.AppendLine("    participant DB as AppDbContext")
            }
            foreach ($lt in $lifecycleTraces) {
                $m = $lt.Method
                [void]$htmlMdSb.AppendLine("    Caller->>Comp: $($m.Name)() [L$($m.StartLine)-L$($m.EndLine)]")
                foreach ($sc in $lt.ServiceCalls) {
                    $partId = $sc.ServiceType -replace '[^A-Za-z0-9_]', ''
                    if ($sc.Resolved) {
                        $r = $sc.Resolved
                        [void]$htmlMdSb.AppendLine("    Comp->>${partId}: $($sc.MethodName)() [L$($r.StartLine)-L$($r.EndLine)]")
                        if ($r.DbSets.Count -gt 0) {
                            [void]$htmlMdSb.AppendLine("    $partId->>DB: $($r.DbSets -join ', ')")
                        }
                        [void]$htmlMdSb.AppendLine("    $partId-->>Comp: $($r.ReturnType)")
                    } else {
                        [void]$htmlMdSb.AppendLine("    Comp->>${partId}: $($sc.MethodName)()")
                    }
                }
            }
            [void]$htmlMdSb.AppendLine('```')
            [void]$htmlMdSb.AppendLine("")
        }

        $actionTraces = @($methodTraces | Where-Object { -not $_.IsLifecycle -and ($_.ServiceCalls.Count -gt 0 -or $_.Method.DbSets.Count -gt 0) })
        foreach ($at in $actionTraces) {
            $m = $at.Method
            [void]$htmlMdSb.AppendLine("### Action Flow: ``$($m.Name)`` (L$($m.StartLine)-L$($m.EndLine))")
            [void]$htmlMdSb.AppendLine('```mermaid')
            [void]$htmlMdSb.AppendLine("sequenceDiagram")
            [void]$htmlMdSb.AppendLine("    autonumber")
            [void]$htmlMdSb.AppendLine("    actor Trigger as User / Trigger")
            [void]$htmlMdSb.AppendLine("    participant Comp as $baseName")
            $declaredActP = [System.Collections.Generic.HashSet[string]]::new()
            $actHasDb = ($m.DbSets.Count -gt 0)
            foreach ($sc in $at.ServiceCalls) {
                $partId = $sc.ServiceType -replace '[^A-Za-z0-9_]', ''
                $pLabel = if ($sc.Resolved) { $sc.Resolved.ClassName } else { $sc.ServiceType }
                if ($declaredActP.Add($partId)) {
                    [void]$htmlMdSb.AppendLine("    participant $partId as $pLabel")
                }
                if ($sc.Resolved -and $sc.Resolved.DbSets.Count -gt 0) { $actHasDb = $true }
            }
            if ($actHasDb) { [void]$htmlMdSb.AppendLine("    participant DB as AppDbContext") }
            [void]$htmlMdSb.AppendLine("    Trigger->>Comp: $($m.Name)()")
            foreach ($sc in $at.ServiceCalls) {
                $partId = $sc.ServiceType -replace '[^A-Za-z0-9_]', ''
                if ($sc.Resolved) {
                    $r = $sc.Resolved
                    [void]$htmlMdSb.AppendLine("    Comp->>${partId}: $($sc.MethodName)() [$($r.ClassName)#L$($r.StartLine)]")
                    if ($r.DbSets.Count -gt 0) {
                        [void]$htmlMdSb.AppendLine("    $partId->>DB: $($r.DbSets -join ', ')")
                    }
                    [void]$htmlMdSb.AppendLine("    $partId-->>Comp: $($r.ReturnType)")
                } else {
                    [void]$htmlMdSb.AppendLine("    Comp->>${partId}: $($sc.MethodName)()")
                }
            }
            [void]$htmlMdSb.AppendLine('```')
            [void]$htmlMdSb.AppendLine("")
        }

        $htmlSpecPayloads.Add([PSCustomObject]@{
            name          = $baseName
            specFile      = $specFileName
            sourceRelPath = $cleanRelPath
            category      = $targetEntry.category
            thinkingLevel = $targetEntry.thinking_level
            description   = $targetEntry.description
            generatedAt   = $nowLocal
            markdown      = $htmlMdSb.ToString()
        })
    }

    $generatedSpecsSummary.Add([PSCustomObject]@{
        Name              = $baseName
        SpecFile          = $specFileName
        SourceCleanPath   = $cleanRelPath
        ParentSpecFile    = $targetEntry.parentSpecFile
        Category          = $targetEntry.category
        ThinkingLevel     = $targetEntry.thinking_level
        Description       = $targetEntry.description
        GeneratedAt       = $nowLocal
        MethodsCount      = $csMethods.Count
        ServicesCount     = $uniqueResolvedServices.Count
        UnregisteredCount = $unregisteredChildUsages.Count
    })
}

# --- 4. Pass 4: Generate docs/specs/README.md (Only when running full manifest without -TargetFilter / -Target) ---
if ([string]::IsNullOrWhiteSpace($TargetFilter) -and [string]::IsNullOrWhiteSpace($Target)) {
$readmePath = Join-Path $specsDir "README.md"
$readmeSb = [System.Text.StringBuilder]::new()
[void]$readmeSb.AppendLine("# Antigravity Ultra-Lean Specifications Catalog")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("> **Last Generated**: ``$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))``  ")
[void]$readmeSb.AppendLine("> **Manifest Source**: [specs-manifest.json](./specs-manifest.json)  ")
[void]$readmeSb.AppendLine("> **Regenerate Command**: ``pwsh scripts/Generate-Specs.ps1`` (or ``pwsh scripts/Generate-Specs.ps1 -GenerateHtml`` for visual Mermaid HTML explorer)")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("## Generated Target Specifications")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("| Spec File (Human) | AI Navigation Index | History by Date | Originating Source | Parent Spec | Category | Unregistered Children | Summary |")
[void]$readmeSb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- |")

foreach ($item in $generatedSpecsSummary) {
    $srcDocLink = Get-RelativeDocLink -CleanSourcePath $item.SourceCleanPath
    $parentCell = if (-not [string]::IsNullOrWhiteSpace($item.ParentSpecFile)) { "[$($item.ParentSpecFile)](./$($item.ParentSpecFile))" } else { "*(Root)*" }
    $unregCell = if ($item.UnregisteredCount -gt 0) { "[**[ALERT: $($item.UnregisteredCount) Review]**](./validation/ComponentRegistrationWarnings.md)" } else { "OK (0)" }
    $idxLink = "[``$($item.Name).index.md``](./index/$($item.Name).index.md)"
    $histLink = "[``$($item.Name).history.md``](./history/$($item.Name).history.md)"
    [void]$readmeSb.AppendLine("| [**$($item.SpecFile)**](./$($item.SpecFile)) | $idxLink | $histLink | [``$($item.SourceCleanPath)``]($srcDocLink) | $parentCell | $($item.Category) | $unregCell | $($item.Description) |")
}

[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("---")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("## Clean 4-Tier Architecture (`docs/specs/`)")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("1. **`docs/specs/<Page>.requirements.md` (For YOU — Pure Requirements & Business Context)**:")
[void]$readmeSb.AppendLine("   - Contains **only** `## 1. Pending Changes (Write in Plain English)`, `## 2. Permanent & Implementation Rules`, and `## 3. Business Context` (~40 lines total).")
[void]$readmeSb.AppendLine("   - **Zero clutter**: No UI inventory duplication, no technical line-number tables, no validation warnings, and no old history logs inside `.requirements.md`!")
[void]$readmeSb.AppendLine("2. **`docs/specs/index/<Page>.index.md` (Single Source of Truth for AI Navigation Coordinates)**:")
[void]$readmeSb.AppendLine("   - Auto-generated compact **1. UI Map**, **2. Method Map**, **3. Service Map**, and **4. Validation Rules** (grouped by `### Service.Method`) with exact `[Lstart-Lend]` line coordinates.")
[void]$readmeSb.AppendLine("3. **`docs/specs/history/<Page>.history.md` (Completed History Archive by Date)**:")
[void]$readmeSb.AppendLine("   - Stores all completed requirements under `### YYYY-MM-DD HH:mm` date stamps so historical logs consume **0 tokens** during active code generation.")
[void]$readmeSb.AppendLine("4. **`docs/specs/validation/ComponentRegistrationWarnings.md` (Unregistered Child Component Validation)**:")
[void]$readmeSb.AppendLine("   - Holds any unregistered child Razor component alerts and ready-to-paste `"children`" JSON outside of `.requirements.md`.")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("### Example: 4-Step Smart Plan-Then-Execute Lifecycle (Optimal Token & Thinking Usage)")
[void]$readmeSb.AppendLine("1. **Write Changes in Plain English (with Optional Tags & Acceptance Criteria for Complex Tasks)**: Open ``docs/specs/SettingsTenantsTab.requirements.md`` and write inside ``<!-- BEGIN PENDING CHANGES -->``:")
[void]$readmeSb.AppendLine('```markdown')
[void]$readmeSb.AppendLine("<!-- BEGIN PENDING CHANGES -->")
[void]$readmeSb.AppendLine("1. [UI] Remove Programs, Users, Tasks & Risks, and Dates buttons from the tenant row in the Tenant Directory table.")
[void]$readmeSb.AppendLine("2. [UI] When clicking a tenant, open a popup and show all the details in edit mode.")
[void]$readmeSb.AppendLine("   Acceptance Criteria:")
[void]$readmeSb.AppendLine("   - Slug, Name, and Domains remain read-only; Start Date and End Date are editable.")
[void]$readmeSb.AppendLine("   - Inside the popup, show tabs for Dates, Users, Programs, and Tasks & Risks.")
[void]$readmeSb.AppendLine("<!-- END PENDING CHANGES -->")
[void]$readmeSb.AppendLine('```')
[void]$readmeSb.AppendLine("2. **Phase 1 — Automatic Pre-Flight Sync (`-EnsureFresh`) & Focused Planning**: Prompt Antigravity:")
[void]$readmeSb.AppendLine("   > ``Implement @[docs/specs/SettingsTenantsTab.requirements.md]``")
[void]$readmeSb.AppendLine("   - **RULE 0 (`-EnsureFresh`)**: The agent first runs `pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -EnsureFresh` (<50ms SHA-256 check). If external code changes occurred, it refreshes `index/SettingsTenantsTab.index.md` while keeping your Section 1 text intact.")
[void]$readmeSb.AppendLine("   - **RULE 1–4**: The agent reads `docs/specs/index/SettingsTenantsTab.index.md`, reads only the exact `[Lstart-Lend]` slices needed, applies **Reuse-First** & **No-New-Architecture** rules, outputs a concise **Execution Plan**, and **pauses for your approval**.")
[void]$readmeSb.AppendLine("3. **Phase 2 — Approve & Surgical Execution**: Reply ``yes`` (or click **Proceed**) to apply the exact surgical code edits.")
[void]$readmeSb.AppendLine("4. **Automatic Date-Stamped Archival & Output Contract**: Once edits are applied, the agent automatically runs:")
[void]$readmeSb.AppendLine('```powershell')
[void]$readmeSb.AppendLine("pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted")
[void]$readmeSb.AppendLine('```')
[void]$readmeSb.AppendLine("   This moves your completed items from **Section 1** into [`docs/specs/history/SettingsTenantsTab.history.md`](./history/SettingsTenantsTab.history.md) stamped with ``### YYYY-MM-DD HH:mm``, resets Section 1, refreshes the Navigation Index line numbers, and outputs the standardized **Output Contract** (`Files Modified`, `Change Summary`, `Validation Performed`, `Risks`).")
[void]$readmeSb.AppendLine("")
[void]$readmeSb.AppendLine("### Viewing Full Visual Mermaid Sequence Diagrams (Optional)")
[void]$readmeSb.AppendLine('```powershell')
[void]$readmeSb.AppendLine("pwsh scripts/Generate-Specs.ps1 -GenerateHtml")
[void]$readmeSb.AppendLine('```')

[System.IO.File]::WriteAllText($readmePath, $readmeSb.ToString(), $utf8Bom)
}

# --- 5. Optional Interactive HTML Viewer (Only when -GenerateHtml is passed) ---
if ($GenerateHtml) {
    $specsJson = $htmlSpecPayloads | ConvertTo-Json -Depth 5 -Compress
    $htmlContent = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>BlazorEnterprise - Antigravity Specs & Visual Mermaid Explorer</title>
    <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
    <style>
        :root { --bg: #0f172a; --panel: #1e293b; --border: #334155; --text: #f8fafc; --muted: #94a3b8; --accent: #38bdf8; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: var(--bg); color: var(--text); display: flex; height: 100vh; overflow: hidden; }
        aside { width: 310px; background: var(--panel); border-right: 1px solid var(--border); display: flex; flex-direction: column; }
        .brand { padding: 16px 18px; border-bottom: 1px solid var(--border); font-weight: 700; font-size: 15px; color: var(--accent); }
        .nav-list { flex: 1; overflow-y: auto; padding: 10px; }
        .nav-item { padding: 10px 12px; border-radius: 6px; cursor: pointer; margin-bottom: 6px; border: 1px solid transparent; }
        .nav-item:hover { background: rgba(56, 189, 248, 0.08); }
        .nav-item.active { background: rgba(56, 189, 248, 0.15); border-color: var(--accent); }
        .nav-item .title { font-weight: 600; font-size: 14px; }
        .nav-item .meta { font-size: 11px; color: var(--muted); margin-top: 4px; }
        main { flex: 1; overflow-y: auto; padding: 28px 40px; }
        .spec-container { max-width: 1150px; margin: 0 auto; }
        table { width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 13px; }
        th, td { border: 1px solid var(--border); padding: 8px 12px; text-align: left; }
        th { background: rgba(255,255,255,0.05); color: var(--accent); }
        code { background: rgba(255,255,255,0.08); padding: 2px 5px; border-radius: 4px; font-family: Consolas, monospace; font-size: 12.5px; }
        pre { background: #0b1120; padding: 14px; border-radius: 8px; border: 1px solid var(--border); overflow-x: auto; }
        a { color: var(--accent); text-decoration: none; }
        .mermaid { background: #ffffff; padding: 16px; border-radius: 8px; margin: 16px 0; }
    </style>
</head>
<body>
    <aside>
        <div class="brand">Antigravity Visual Spec Explorer</div>
        <div class="nav-list" id="navList"></div>
    </aside>
    <main>
        <div class="spec-container" id="specContent"></div>
    </main>
    <script>
        mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' });
        const specs = $specsJson;
        const navList = document.getElementById('navList');
        const specContent = document.getElementById('specContent');
        async function selectSpec(idx) {
            document.querySelectorAll('.nav-item').forEach((el, i) => el.classList.toggle('active', i === idx));
            const item = specs[idx];
            specContent.innerHTML = marked.parse(item.markdown);
            const mermaidBlocks = specContent.querySelectorAll('pre code.language-mermaid');
            for (let i = 0; i < mermaidBlocks.length; i++) {
                const codeBlock = mermaidBlocks[i];
                const div = document.createElement('div');
                div.className = 'mermaid';
                div.textContent = codeBlock.textContent;
                codeBlock.parentElement.replaceWith(div);
            }
            await mermaid.run({ nodes: specContent.querySelectorAll('.mermaid') });
        }
        specs.forEach((s, idx) => {
            const div = document.createElement('div');
            div.className = 'nav-item';
            div.innerHTML = '<div class="title">' + s.name + '</div><div class="meta">' + s.category + ' | thinking: ' + s.thinkingLevel + '</div>';
            div.onclick = () => selectSpec(idx);
            navList.appendChild(div);
        });
        if (specs.length > 0) selectSpec(0);
    </script>
</body>
</html>
"@
    [System.IO.File]::WriteAllText($htmlPath, $htmlContent, $utf8Bom)
}

# --- 6. Write docs/specs/validation/ComponentRegistrationWarnings.md & Emit Prominent Console Warning/Error ---
$validationWarningsPath = Join-Path $validationDir "ComponentRegistrationWarnings.md"
$valSb = [System.Text.StringBuilder]::new()
[void]$valSb.AppendLine("# Component Registration Warnings (Validation Report)")
[void]$valSb.AppendLine("")
[void]$valSb.AppendLine("> **Last Generated**: ``$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))``  ")
[void]$valSb.AppendLine("> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)")
[void]$valSb.AppendLine("")

if ($globalUnregisteredAlerts.Count -gt 0) {
    $totalUnreg = ($globalUnregisteredAlerts | ForEach-Object { $_.Children.Count } | Measure-Object -Sum).Sum
    [void]$valSb.AppendLine("> [!CAUTION]")
    [void]$valSb.AppendLine("> **$totalUnreg Unregistered Child Component(s) detected across $($globalUnregisteredAlerts.Count) parent component(s)!** Copy desired entries below into ``""children""`` of the respective parent in ``docs/specs/specs-manifest.json`` (or add shared UI widgets to ``component_discovery.ignore_components``).")
    [void]$valSb.AppendLine("")

    foreach ($alert in $globalUnregisteredAlerts) {
        [void]$valSb.AppendLine("## Parent: ``$($alert.ParentPath)`` ([$($alert.SpecFile)](../../$($alert.SpecFile)))")
        [void]$valSb.AppendLine("")
        [void]$valSb.AppendLine("| Unregistered Component | Source Path | Line | Gate Condition |")
        [void]$valSb.AppendLine("| :--- | :--- | :--- | :--- |")
        $jsonReviewItems = [System.Collections.Generic.List[string]]::new()
        foreach ($ch in $alert.Children) {
            [void]$valSb.AppendLine("| ``<$($ch.ComponentName) />`` | ``$($ch.CleanPath)`` | ``L$($ch.Line)`` | ``$($ch.GateCondition)`` |")
            $safeGate = $ch.GateCondition -replace '"', "'"
            $itemJson = @"
    {
      "path": "$($ch.CleanPath)",
      "category": "$($alert.Category)",
      "thinking_level": "$($alert.ThinkingLevel)",
      "description": "Child component <$($ch.ComponentName) /> rendered by $($alert.ParentFileName) at L$($ch.Line) (Gate: $safeGate).",
      "enabled": true
    }
"@
            $jsonReviewItems.Add($itemJson)
        }
        [void]$valSb.AppendLine("")
        [void]$valSb.AppendLine("### Ready-to-Paste JSON for ``$($alert.ParentFileName)``")
        [void]$valSb.AppendLine('```json')
        [void]$valSb.AppendLine('"children": [')
        [void]$valSb.AppendLine(($jsonReviewItems -join ",`n"))
        [void]$valSb.AppendLine(']')
        [void]$valSb.AppendLine('```')
        [void]$valSb.AppendLine("")
    }

    [System.IO.File]::WriteAllText($validationWarningsPath, $valSb.ToString(), $utf8Bom)

    Write-Host ""
    Write-Warning "ComponentRegistrationWarnings.md was updated with $totalUnreg unregistered child component(s)! Review: docs/specs/validation/ComponentRegistrationWarnings.md"
    Write-Host "================================================================================" -ForegroundColor Red
    Write-Host "  [!] WARNING / ACTION REQUIRED: ComponentRegistrationWarnings.md UPDATED!      " -ForegroundColor Red
    Write-Host "  Validation File : docs/specs/validation/ComponentRegistrationWarnings.md      " -ForegroundColor Yellow
    Write-Host "================================================================================" -ForegroundColor Red
    foreach ($alert in $globalUnregisteredAlerts) {
        Write-Host "  Parent Component : $($alert.ParentPath)" -ForegroundColor Red
        foreach ($ch in $alert.Children) {
            Write-Host "    -> <$($ch.ComponentName) /> ($($ch.CleanPath)) at Line $($ch.Line)" -ForegroundColor Red
        }
    }
    Write-Host "  Open docs/specs/validation/ComponentRegistrationWarnings.md and copy the" -ForegroundColor Yellow
    Write-Host "  ready-to-paste `"children`" JSON into docs/specs/specs-manifest.json" -ForegroundColor Yellow
    Write-Host "  (or add shared widgets to component_discovery.ignore_components)." -ForegroundColor Yellow
    Write-Host "================================================================================" -ForegroundColor Red
} else {
    [void]$valSb.AppendLine("All discovered child Razor components are registered in ``specs-manifest.json`` or listed in ``component_discovery.ignore_components``.")
    [System.IO.File]::WriteAllText($validationWarningsPath, $valSb.ToString(), $utf8Bom)
}

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  Successfully generated $($generatedSpecsSummary.Count) ultra-lean specification(s) in docs/specs/" -ForegroundColor Green
Write-Host "  Index Catalog : docs/specs/README.md" -ForegroundColor Gray
if ($GenerateHtml) {
    Write-Host "  HTML Explorer : docs/specs/index.html" -ForegroundColor Gray
}
Write-Host "================================================================================" -ForegroundColor Cyan
