<#
.SYNOPSIS
    Generates an enterprise-grade HTML Data Dictionary portal and Markdown specification
    for BlazorFluent, modeled after modern enterprise architecture data dictionary standards.

.DESCRIPTION
    Scans the C# domain entity layer (src/BlazorFluent.Core/Domain) and AppDbContext.cs.
    Extracts entities, base classes, marker interfaces, properties, foreign keys, navigation
    properties, and C# XML doc comments. Generates a standalone, multi-page HTML portal
    in docs/data-dictionary/ with:
    - Searchable left sidebar grouped by subsystem with instant live filtering
    - Top navigation tabs for Data Model, Razor Pages (Phase 2), and Services (Phase 3)
    - Overview architecture dashboard with metrics and subsystem cards
    - Dedicated detail pages for each entity with property schemas, marker badges,
      and bidirectional inbound/outbound relational links.
    - Also updates docs/DATA_MODEL.md for repository markdown viewing.

.PARAMETER Format
    Output format: "Html", "Markdown", or "All". Defaults to "All".

.PARAMETER OutputDir
    Output directory for the HTML portal. Defaults to "docs/data-dictionary".

.PARAMETER MarkdownPath
    Output path for the Markdown data model. Defaults to "docs/DATA_MODEL.md".

.PARAMETER DomainPath
    Path to Domain entities. Defaults to "src/BlazorFluent.Core/Domain".

.PARAMETER DbContextPath
    Path to AppDbContext.cs. Defaults to "src/BlazorFluent.Persistence/Context/AppDbContext.cs".

.EXAMPLE
    pwsh scripts/Generate-DataModelDocs.ps1
    pwsh scripts/Generate-DataModelDocs.ps1 -Format Html
#>

[CmdletBinding()]
param(
    [ValidateSet("Html", "Markdown", "All")]
    [string]$Format = "All",
    [string]$OutputDir = "docs/data-dictionary",
    [string]$MarkdownPath = "docs/DATA_MODEL.md",
    [string]$DomainPath = "src/BlazorFluent.Core/Domain",
    [string]$DbContextPath = "src/BlazorFluent.Persistence/Context/AppDbContext.cs"
)

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

# ─── 1. RESOLVE DIRECTORIES ───────────────────────────────────────────────────
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path

$resolvedDomain = if ([System.IO.Path]::IsPathRooted($DomainPath)) { $DomainPath } else { Join-Path $rootDir $DomainPath }
$resolvedDbContext = if ([System.IO.Path]::IsPathRooted($DbContextPath)) { $DbContextPath } else { Join-Path $rootDir $DbContextPath }
$resolvedOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $rootDir $OutputDir }
$resolvedMarkdown = if ([System.IO.Path]::IsPathRooted($MarkdownPath)) { $MarkdownPath } else { Join-Path $rootDir $MarkdownPath }

if (-not (Test-Path $resolvedDomain)) {
    throw "Domain directory not found at '$resolvedDomain'."
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " [DATA DICTIONARY GENERATOR] Enterprise Data Model Builder" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Domain path:    $resolvedDomain"
Write-Host "HTML Output:    $resolvedOutputDir"
Write-Host "Markdown:       $resolvedMarkdown"
Write-Host "Format:         $Format"
Write-Host ""

# ─── 2. MAP DBSETS FROM APPDBNAMES ─────────────────────────────────────────────
$dbSetMap = @{}
if (Test-Path $resolvedDbContext) {
    $dbContextContent = Get-Content $resolvedDbContext -Raw
    $matches = [regex]::Matches($dbContextContent, 'public\s+DbSet<(?<entity>\w+)>\s+(?<name>\w+)\s*=>')
    foreach ($m in $matches) {
        $dbSetMap[$m.Groups['entity'].Value] = $m.Groups['name'].Value
    }
}
Write-Host "Mapped $($dbSetMap.Count) DbSet properties from AppDbContext." -ForegroundColor Green

# ─── 3. SCAN ENTITY FILES ─────────────────────────────────────────────────────
$files = Get-ChildItem -Path $resolvedDomain -Recurse -Filter *.cs |
    Where-Object { 
        $_.FullName -notmatch '\\obj\\' -and 
        $_.FullName -notmatch '\\bin\\' -and 
        $_.FullName -notmatch '\\Validators\\' -and
        $_.Name -notmatch 'Validator\.cs$'
    }

Write-Host "Scanning $($files.Count) C# domain definitions..." -ForegroundColor Green

$typeDict = [System.Collections.Generic.Dictionary[string, PSObject]]::new()

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $relDir = Split-Path $file.FullName -Parent
    $subsystem = Split-Path $relDir -Leaf
    if ($subsystem -eq "Domain") { $subsystem = "Base" }

    # Extract class/interface XML summary
    $classSummary = ""
    if ($content -match '(?ms)(?:/\*\*\s*(?<jdoc>.*?)\*/|///\s*<summary>\s*(?<doc>.*?)</summary>)\s*public\s+(?:abstract\s+|sealed\s+)?(?:partial\s+)?(?:class|interface)') {
        $rawDoc = if ($Matches['doc']) { $Matches['doc'] } else { $Matches['jdoc'] }
        $classSummary = ($rawDoc -split '\r?\n' | ForEach-Object { 
            $_ -replace '^\s*///\s*', '' -replace '<.*?>', '' -replace '\s+', ' ' 
        } | Where-Object { $_.Trim().Length -gt 0 }) -join ' '
    }

    $isInterface = $false
    $isAbstract = $false
    $typeName = ""
    $inherits = @()

    if ($content -match '(?m)^\s*public\s+interface\s+(?<name>\w+)(?:\s*:\s*(?<bases>[^\r\n{]+))?') {
        $isInterface = $true
        $typeName = $Matches['name']
        if ($Matches['bases']) {
            $inherits = $Matches['bases'] -split ',' | ForEach-Object { $_.Trim() }
        }
    }
    elseif ($content -match '(?m)^\s*public\s+(?:(?<abstract>abstract)\s+)?(?:partial\s+)?class\s+(?<name>\w+)(?:\s*:\s*(?<bases>[^\r\n{]+))?') {
        $typeName = $Matches['name']
        $isAbstract = [bool]$Matches['abstract']
        if ($Matches['bases']) {
            $inherits = $Matches['bases'] -split ',' | ForEach-Object { $_.Trim() }
        }
    }

    if (-not $typeName) { continue }

    # Extract properties with XML doc comments
    $properties = @()
    $navProps = @()
    $collections = @()
    $foreignKeys = @()

    $lines = $content -split '\r?\n'
    $curDocLines = [System.Collections.Generic.List[string]]::new()
    $inDoc = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*///\s*<summary>') {
            $curDocLines.Clear()
            $inDoc = $true
            continue
        }
        if ($inDoc) {
            if ($line -match '^\s*///\s*</summary>') {
                $inDoc = $false
                continue
            }
            $cleanLine = $line -replace '^\s*///\s*', '' -replace '<.*?>', '' -replace '\s+', ' '
            if ($cleanLine.Trim().Length -gt 0) {
                $curDocLines.Add($cleanLine.Trim())
            }
            continue
        }
        if ($line -match '^\s*\[') {
            # Attribute line preceding property, preserve doc comment
            continue
        }
        if ($line -match '^\s*public\s+(?:virtual\s+)?(?<type>[A-Za-z0-9_<>?]+)\s+(?<prop>\w+)\s*\{\s*get;\s*(?:private\s+|protected\s+)?(?:set|init);\s*\}') {
            $pType = $Matches['type']
            $pName = $Matches['prop']
            $pDoc = ($curDocLines -join ' ')
            $curDocLines.Clear()

            $isNullable = $pType.EndsWith('?')
            $cleanType = $pType.TrimEnd('?')

            $isFk = $pName -match 'Id$' -and $pName -ne 'Id' -and $pName -ne 'TenantId'
            $isNav = $cleanType -match 'Entity$'
            $isCol = $pType -match '^(?:ICollection|List|IList|IEnumerable|HashSet)<(?<inner>\w+)>$'

            $targetEntity = ""
            if ($isNav) { $targetEntity = $cleanType }
            elseif ($isCol) { $targetEntity = $Matches['inner'] }
            elseif ($isFk) {
                $guessedPlain = $pName -replace 'Id$', ''
                $targetEntity = $guessedPlain
            }

            $propObj = [PSCustomObject]@{
                Name         = $pName
                Type         = $pType
                CleanType    = $cleanType
                IsNullable   = $isNullable
                Description  = $pDoc
                IsForeignKey = $isFk
                IsNavigation = $isNav
                IsCollection = $isCol
                TargetEntity = $targetEntity
            }

            $properties += $propObj

            if ($isFk) { $foreignKeys += $propObj }
            if ($isNav) { $navProps += $propObj }
            if ($isCol) { $collections += $propObj }
            continue
        }

        # Any other non-empty line (class, method, or blank line after code) resets doc buffer
        if ($line.Trim().Length -gt 0) {
            $curDocLines.Clear()
        }
    }

    $typeObj = [PSCustomObject]@{
        Name           = $typeName
        File           = $file.FullName
        Subsystem      = $subsystem
        IsInterface    = $isInterface
        IsAbstract     = $isAbstract
        Summary        = $classSummary
        Inherits       = $inherits
        BaseClass      = ($inherits | Where-Object { $_ -notmatch '^I[A-Z]' } | Select-Object -First 1)
        DirectMarkers  = ($inherits | Where-Object { $_ -match '^I[A-Z]' })
        AllMarkers     = [System.Collections.Generic.HashSet[string]]::new()
        Properties     = $properties
        Navigations    = $navProps
        Collections    = $collections
        ForeignKeys    = $foreignKeys
        InboundLinks   = @()
        DbSetName      = $dbSetMap[$typeName]
    }

    $typeDict[$typeName] = $typeObj
}

# ─── 4. RESOLVE TRANSITIVE MARKERS & INBOUND LINKS ─────────────────────────────
foreach ($typeName in $typeDict.Keys) {
    $t = $typeDict[$typeName]
    foreach ($m in $t.DirectMarkers) {
        [void]$t.AllMarkers.Add($m)
    }

    $currBase = $t.BaseClass
    while ($currBase -and $typeDict.ContainsKey($currBase)) {
        $parent = $typeDict[$currBase]
        foreach ($pm in $parent.DirectMarkers) {
            [void]$t.AllMarkers.Add($pm)
        }
        $currBase = $parent.BaseClass
    }
}

# Compute reverse inbound links
foreach ($source in $typeDict.Values) {
    foreach ($nav in $source.Navigations) {
        if ($typeDict.ContainsKey($nav.TargetEntity)) {
            $target = $typeDict[$nav.TargetEntity]
            $target.InboundLinks += [PSCustomObject]@{
                SourceEntity   = $source.Name
                SourceProperty = $nav.Name
                SourceSubsystem = $source.Subsystem
                Type           = "Navigation (1:1)"
            }
        }
    }
    foreach ($col in $source.Collections) {
        if ($typeDict.ContainsKey($col.TargetEntity)) {
            $target = $typeDict[$col.TargetEntity]
            $target.InboundLinks += [PSCustomObject]@{
                SourceEntity   = $source.Name
                SourceProperty = $col.Name
                SourceSubsystem = $source.Subsystem
                Type           = "Collection (1:N)"
            }
        }
    }
    foreach ($fk in $source.ForeignKeys) {
        # Check target candidates
        $cands = @(($fk.Name -replace 'Id$', 'Entity'), ($fk.Name -replace 'Id$', ''))
        foreach ($cand in $cands) {
            if ($typeDict.ContainsKey($cand) -and $cand -ne $source.Name) {
                $target = $typeDict[$cand]
                $target.InboundLinks += [PSCustomObject]@{
                    SourceEntity   = $source.Name
                    SourceProperty = $fk.Name
                    SourceSubsystem = $source.Subsystem
                    Type           = "Foreign Key FK"
                }
                break
            }
        }
    }
}

Write-Host "Resolved $($typeDict.Count) total types across domain." -ForegroundColor Green

# ─── 4.5 SCAN RAZOR PAGES & UI FIELD USAGES (PRESENTATION LAYER USAGE) ──────────
Write-Host "Scanning Razor screens and field usages across UI layer..." -ForegroundColor Cyan

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$webPath = Join-Path $repoRoot "src\BlazorFluent.Web"
$uiTemplatesPath = Join-Path $repoRoot "src\BlazorFluent.UI.Templates"

$razorFiles = @()
if (Test-Path $webPath) {
    $razorFiles += Get-ChildItem -Path $webPath -Include *.razor -Recurse | Where-Object { 
        $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' -and $_.Name -ne "_Imports.razor" -and $_.Name -ne "App.razor" -and $_.Name -ne "Routes.razor" 
    }
}
if (Test-Path $uiTemplatesPath) {
    $razorFiles += Get-ChildItem -Path $uiTemplatesPath -Include *.razor -Recurse | Where-Object { 
        $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' -and $_.Name -ne "_Imports.razor" -and $_.Name -ne "App.razor" -and $_.Name -ne "Routes.razor" 
    }
}

foreach ($e in $typeDict.Values) {
    $e | Add-Member -MemberType NoteProperty -Name "ScreenUsages" -Value @() -Force
}

$screens = @()

foreach ($rf in $razorFiles) {
    $rfContent = Get-Content $rf.FullName -Raw
    $csCompanion = $rf.FullName + ".cs"
    if (Test-Path $csCompanion) {
        $rfContent += "`n" + (Get-Content $csCompanion -Raw)
    }

    # Extract routes
    $routes = @()
    $routeMatches = [regex]::Matches($rfContent, '@page\s+"(?<route>[^"]+)"')
    foreach ($rm in $routeMatches) {
        $routes += $rm.Groups['route'].Value
    }
    $primaryRoute = if ($routes.Count -gt 0) { $routes[0] } else { "" }

    # Component classification
    $compType = "Component"
    if ($routes.Count -gt 0) { $compType = "Page" }
    elseif ($rf.Name -match 'Modal\.razor$') { $compType = "Modal" }
    elseif ($rf.Name -match 'Drawer\.razor$') { $compType = "Drawer" }
    elseif ($rf.Name -match 'Tab\.razor$') { $compType = "Settings Tab" }
    elseif ($rf.Name -match 'Popup\.razor$') { $compType = "Popup" }
    elseif ($rf.Name -match 'Comp\.razor$') { $compType = "Component" }

    # Subsystem / module classification
    $relPath = $rf.FullName.Replace($repoRoot, "").TrimStart('\/')
    $screenSubsystem = "Common"
    if ($relPath -match 'Modules[\\/](?<mod>[^\\/]+)') {
        $screenSubsystem = $Matches['mod']
    } elseif ($relPath -match 'Components[\\/]Pages[\\/]Settings') {
        $screenSubsystem = "Settings"
    } elseif ($relPath -match 'Components[\\/]Pages') {
        $screenSubsystem = "Pages"
    } elseif ($relPath -match 'Components[\\/]Layout') {
        $screenSubsystem = "Layout"
    } elseif ($relPath -match 'UI\.Templates') {
        $screenSubsystem = "Templates"
    }

    # Title extraction
    $screenTitle = $rf.Name -replace '\.razor$', ''
    if ($rfContent -match '<PageTitle>(?<title>.*?)</PageTitle>') {
        $rawTitle = $Matches['title'] -replace '&amp;', '&' -replace '\s*-\s*BlazorFluent.*$', '' -replace '\s*-\s*SAMPLE PAGE ONLY.*$', ''
        if ($rawTitle.Trim().Length -gt 0) { $screenTitle = $rawTitle.Trim() }
    }

    $screenEntityUsages = @()

    foreach ($e in ($typeDict.Values | Where-Object { -not $_.IsInterface })) {
        $eName = $e.Name
        $stem = $eName -replace 'Entity$', ''
        $dbSet = $e.DbSetName

        $isReferenced = ($rfContent -match "\b$eName\b") -or 
                        ($rfContent -match "EntityName\s*=\s*`"$eName`"") -or
                        ($dbSet -and ($rfContent -match "DbContext\.$dbSet\b" -or $rfContent -match "\b$dbSet\b")) -or
                        ($stem.Length -gt 4 -and ($rf.Name -match "\b$stem\w*\.razor" -or $rfContent -match "\b$stem(?:List|Detail|Create|Edit|Response)?Dto\b"))

        if ($isReferenced) {
            # Collect all transitive properties for entity $e
            $allProps = [System.Collections.Generic.HashSet[string]]::new()
            $curr = $e
            while ($curr) {
                foreach ($p in $curr.Properties) { [void]$allProps.Add($p.Name) }
                if ($curr.BaseClass -and $typeDict.ContainsKey($curr.BaseClass)) {
                    $curr = $typeDict[$curr.BaseClass]
                } else {
                    $curr = $null
                }
            }
            [void]$allProps.Add("Id")
            [void]$allProps.Add("Created")
            [void]$allProps.Add("CreatedBy")
            [void]$allProps.Add("Updated")
            [void]$allProps.Add("UpdatedBy")
            [void]$allProps.Add("TenantId")

            $matchedProps = @()
            foreach ($p in $allProps) {
                if ($rfContent -match "(\.$p\b|PropertyName\s*=\s*`"$p`"|@\(\s*\w+\s*=>\s*\w+\.$p\b|`"$p`"|@bind-Value=`"[^`"]*\b$p\b`"|nameof\([^\)]*\b$p\b\))") {
                    $matchedProps += $p
                }
            }

            $usageObj = [PSCustomObject]@{
                EntityName  = $eName
                Fields      = $matchedProps
            }
            $screenEntityUsages += $usageObj

            $e.ScreenUsages += [PSCustomObject]@{
                ScreenName    = $rf.Name
                ScreenTitle   = $screenTitle
                Subsystem     = $screenSubsystem
                Route         = $primaryRoute
                ComponentType = $compType
                Fields        = $matchedProps
                RelativePath  = $relPath
            }
        }
    }

    $screens += [PSCustomObject]@{
        Name          = $rf.Name
        Title         = $screenTitle
        Route         = $primaryRoute
        Routes        = $routes
        ComponentType = $compType
        Subsystem     = $screenSubsystem
        EntityUsages  = $screenEntityUsages
        RelativePath  = $relPath
    }
}

$entitiesWithScreens = ($typeDict.Values | Where-Object { $_.ScreenUsages.Count -gt 0 }).Count
Write-Host "Scanned $($screens.Count) screens. Found $entitiesWithScreens entities active in UI." -ForegroundColor Green


# ─── 5. GENERATE HTML PORTAL ──────────────────────────────────────────────────
if ($Format -eq "Html" -or $Format -eq "All") {
    Write-Host "Building HTML Data Dictionary portal in '$resolvedOutputDir'..." -ForegroundColor Cyan

    if (-not (Test-Path $resolvedOutputDir)) {
        New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null
    }
    $entitiesDir = Join-Path $resolvedOutputDir "entities"
    $placeholdersDir = Join-Path $resolvedOutputDir "placeholders"
    if (-not (Test-Path $entitiesDir)) { New-Item -ItemType Directory -Path $entitiesDir -Force | Out-Null }
    if (-not (Test-Path $placeholdersDir)) { New-Item -ItemType Directory -Path $placeholdersDir -Force | Out-Null }

    # 5.1 STYLES.CSS
    $css = @'
:root {
    --primary: #0078d4;
    --primary-dark: #106ebe;
    --primary-light: #e1dfdd;
    --bg-main: #f3f2f1;
    --bg-card: #ffffff;
    --text-primary: #201f1e;
    --text-secondary: #605e5c;
    --border: #edebe9;
    --badge-tenant: #004578;
    --badge-softdelete: #a80000;
    --badge-audit: #107c10;
    --badge-global: #5c2d91;
    --badge-project: #d83b01;
    --badge-date: #008272;
    --font-stack: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
}

* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: var(--font-stack); background: var(--bg-main); color: var(--text-primary); line-height: 1.5; font-size: 14px; }

/* Header */
.top-header { background: #002050; color: #ffffff; display: flex; align-items: center; justify-content: space-between; padding: 0 20px; height: 50px; border-bottom: 2px solid var(--primary); }
.brand { display: flex; align-items: center; gap: 12px; font-weight: 600; font-size: 16px; }
.brand-badge { background: var(--primary); padding: 2px 8px; border-radius: 4px; font-size: 11px; text-transform: uppercase; font-weight: 700; }
.nav-tabs { display: flex; gap: 4px; height: 100%; align-items: flex-end; }
.nav-tab { padding: 10px 16px; color: #c8c6c4; text-decoration: none; font-weight: 500; border-top-left-radius: 4px; border-top-right-radius: 4px; transition: all 0.2s; }
.nav-tab:hover { background: rgba(255, 255, 255, 0.1); color: #ffffff; }
.nav-tab.active { background: #ffffff; color: var(--text-primary); font-weight: 600; }

/* Main Split View */
.portal-container { display: flex; height: calc(100vh - 50px); overflow: hidden; }
.sidebar-pane { width: 320px; min-width: 280px; background: #ffffff; border-right: 1px solid var(--border); display: flex; flex-direction: column; }
.content-pane { flex: 1; height: 100%; border: none; background: var(--bg-main); }

/* Sidebar */
.search-box-wrap { padding: 12px; border-bottom: 1px solid var(--border); }
.search-input { width: 100%; padding: 8px 12px; border: 1px solid #c8c6c4; border-radius: 4px; font-size: 13px; outline: none; }
.search-input:focus { border-color: var(--primary); box-shadow: 0 0 0 1px var(--primary); }
.sidebar-nav { flex: 1; overflow-y: auto; padding: 8px 0; }
.subsystem-header { display: flex; justify-content: space-between; align-items: center; padding: 8px 16px; font-size: 12px; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); cursor: pointer; user-select: none; }
.subsystem-header:hover { background: #f8f8f8; }
.badge-count { background: #edebe9; color: var(--text-secondary); font-size: 11px; padding: 1px 6px; border-radius: 10px; font-weight: 600; }
.entity-link { display: block; padding: 6px 16px 6px 28px; text-decoration: none; color: var(--text-primary); font-size: 13px; border-left: 3px solid transparent; }
.entity-link:hover { background: #f3f2f1; color: var(--primary); border-left-color: var(--primary); }
.entity-link.active { background: #e1dfdd; font-weight: 600; border-left-color: var(--primary); }

/* Page Content */
.page-container { padding: 24px 32px; max-width: 1400px; margin: 0 auto; }
.breadcrumb { font-size: 12px; color: var(--text-secondary); margin-bottom: 8px; }
.breadcrumb a { color: var(--primary); text-decoration: none; }
.page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 20px; padding-bottom: 12px; border-bottom: 1px solid var(--border); }
.entity-title { font-size: 26px; font-weight: 600; color: var(--text-primary); }
.entity-meta { display: flex; gap: 8px; margin-top: 6px; flex-wrap: wrap; }

/* Badges */
.badge { display: inline-block; padding: 2px 8px; border-radius: 3px; font-size: 11px; font-weight: 600; color: #fff; text-decoration: none; }
.badge-tenant { background: var(--badge-tenant); }
.badge-softdelete { background: var(--badge-softdelete); }
.badge-audit { background: var(--badge-audit); }
.badge-global { background: var(--badge-global); }
.badge-project { background: var(--badge-project); }
.badge-date { background: var(--badge-date); }
.badge-base { background: #605e5c; }
.badge-subsystem { background: #323130; }

/* Cards & Tables */
.card { background: var(--bg-card); border-radius: 4px; border: 1px solid var(--border); padding: 20px; margin-bottom: 20px; box-shadow: 0 1.6px 3.6px 0 rgba(0,0,0,0.05); }
.card-title { font-size: 16px; font-weight: 600; margin-bottom: 12px; color: var(--text-primary); display: flex; align-items: center; justify-content: space-between; }
.desc-box { font-size: 14px; line-height: 1.6; color: #323130; }

table { width: 100%; border-collapse: collapse; margin-top: 8px; font-size: 13px; }
th { text-align: left; padding: 8px 12px; background: #faf9f8; border-bottom: 2px solid var(--border); color: var(--text-secondary); font-weight: 600; }
td { padding: 8px 12px; border-bottom: 1px solid var(--border); vertical-align: top; }
tr:hover td { background: #fdfdfd; }

.code-type { font-family: Consolas, monospace; color: #a4262c; font-size: 12px; }
.prop-name { font-weight: 600; font-family: Consolas, monospace; }
.fk-pill { background: #edebe9; padding: 1px 6px; border-radius: 3px; font-size: 10px; color: #323130; margin-left: 4px; font-family: var(--font-stack); }

/* Dashboard Grid */
.stat-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 24px; }
.stat-card { background: #fff; padding: 16px; border-radius: 4px; border: 1px solid var(--border); text-align: center; }
.stat-val { font-size: 32px; font-weight: 700; color: var(--primary); }
.stat-lbl { font-size: 12px; text-transform: uppercase; color: var(--text-secondary); font-weight: 600; }

.subsystem-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; }
.subsystem-card { background: #fff; border: 1px solid var(--border); border-radius: 4px; padding: 16px; }
.subsystem-card h3 { font-size: 16px; font-weight: 600; margin-bottom: 8px; display: flex; justify-content: space-between; }
.entity-tag-list { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 10px; }
.entity-tag { background: #f3f2f1; padding: 3px 8px; border-radius: 3px; font-size: 12px; text-decoration: none; color: var(--primary); }
.entity-tag:hover { background: #e1dfdd; }

/* Screen Badges & Field Pills */
.badge-field { background: #edebe9; color: #201f1e; font-family: Consolas, monospace; font-size: 11px; padding: 2px 6px; border-radius: 3px; display: inline-block; margin: 2px 2px; font-weight: 500; border: 1px solid #e1dfdd; }
.badge-field:hover { background: #e1dfdd; }
.badge-page { background: #0078d4; }
.badge-modal { background: #5c2d91; }
.badge-settingstab { background: #008272; }
.badge-component { background: #605e5c; }
.badge-drawer { background: #d83b01; }
.badge-popup { background: #498205; }
'@

    Set-Content -Path (Join-Path $resolvedOutputDir "styles.css") -Value $css -Encoding UTF8

    # 5.2 SIDEBAR.HTML
    $sbHtml = [System.Text.StringBuilder]::new()
    [void]$sbHtml.AppendLine('<!DOCTYPE html>')
    [void]$sbHtml.AppendLine('<html><head><meta charset="utf-8"><link rel="stylesheet" href="styles.css"></head><body style="background:#fff;">')
    [void]$sbHtml.AppendLine('<div class="search-box-wrap"><input type="text" id="entitySearch" class="search-input" placeholder="Filter entities..." onkeyup="filterEntities()"></div>')
    [void]$sbHtml.AppendLine('<div class="sidebar-nav" id="sidebarNav">')

    $subgroups = $typeDict.Values | 
        Where-Object { -not $_.IsInterface } | 
        Group-Object Subsystem | 
        Sort-Object Name

    foreach ($group in $subgroups) {
        $icon = switch ($group.Name) {
            "Tenancy" { "🏛️" }
            "Tasks" { "📋" }
            "Identity" { "🔐" }
            "Calendar" { "📅" }
            "Auditing" { "🔍" }
            "Events" { "⚡" }
            "Knowledge" { "📚" }
            "Imports" { "📦" }
            "Catalog" { "🏷️" }
            "Notifications" { "🔔" }
            "Jobs" { "⏱️" }
            default { "📦" }
        }

        [void]$sbHtml.AppendLine("<div class=`"subsystem-group`" data-group=`"$($group.Name)`">")
        [void]$sbHtml.AppendLine("<div class=`"subsystem-header`" onclick=`"toggleGroup(this)`"><span>$icon $($group.Name)</span><span class=`"badge-count`">$($group.Count)</span></div>")
        [void]$sbHtml.AppendLine("<div class=`"subsystem-items`">")

        foreach ($e in ($group.Group | Sort-Object Name)) {
            [void]$sbHtml.AppendLine("<a class=`"entity-link`" href=`"entities/$($e.Name).html`" target=`"contentFrame`" onclick=`"setActive(this)`" data-name=`"$($e.Name.ToLower())`">$($e.Name)</a>")
        }

        [void]$sbHtml.AppendLine('</div></div>')
    }

    [void]$sbHtml.AppendLine('</div>')
    [void]$sbHtml.AppendLine(@'
<script>
function filterEntities() {
    var q = document.getElementById('entitySearch').value.toLowerCase();
    var links = document.querySelectorAll('.entity-link');
    links.forEach(function(l) {
        var name = l.getAttribute('data-name');
        if (!q || name.indexOf(q) !== -1) {
            l.style.display = 'block';
        } else {
            l.style.display = 'none';
        }
    });
    // Hide empty groups
    document.querySelectorAll('.subsystem-group').forEach(function(g) {
        var visible = g.querySelectorAll('.entity-link[style="display: block;"], .entity-link:not([style*="display"])');
        if (!q) {
            g.style.display = 'block';
        } else {
            var any = false;
            g.querySelectorAll('.entity-link').forEach(function(l) { if (l.style.display !== 'none') any = true; });
            g.style.display = any ? 'block' : 'none';
        }
    });
}
function toggleGroup(el) {
    var items = el.nextElementSibling;
    items.style.display = (items.style.display === 'none') ? 'block' : 'none';
}
function setActive(el) {
    document.querySelectorAll('.entity-link').forEach(function(l) { l.classList.remove('active'); });
    el.classList.add('active');
}
</script>
</body></html>
'@)

    Set-Content -Path (Join-Path $resolvedOutputDir "sidebar.html") -Value $sbHtml.ToString() -Encoding UTF8

    # 5.3 OVERVIEW.HTML
    $totalEntities = ($typeDict.Values | Where-Object { -not $_.IsInterface }).Count
    $totalMarkers = ($typeDict.Values | Where-Object { $_.IsInterface }).Count
    $totalDbSets = $dbSetMap.Count
    $totalSubsystems = $subgroups.Count

    $ovHtml = [System.Text.StringBuilder]::new()
    [void]$ovHtml.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><link rel="stylesheet" href="styles.css"></head><body>')
    [void]$ovHtml.AppendLine('<div class="page-container">')
    [void]$ovHtml.AppendLine('<div class="page-header"><div><h1 class="entity-title">Data Model Architecture Overview</h1><div class="breadcrumb">Enterprise Data Dictionary &bull; BlazorFluent Monolith</div></div></div>')

    # Stat Cards
    $screensWithEntitiesCount = ($screens | Where-Object { $_.EntityUsages.Count -gt 0 }).Count
    $totalBindings = 0
    foreach ($sc in $screens) {
        foreach ($eu in $sc.EntityUsages) { $totalBindings += $eu.Fields.Count }
    }

    [void]$ovHtml.AppendLine('<div class="stat-grid">')
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$totalEntities</div><div class=`"stat-lbl`">Domain Entities</div></div>")
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$totalSubsystems</div><div class=`"stat-lbl`">Subsystems</div></div>")
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$totalDbSets</div><div class=`"stat-lbl`">AppDbContext DbSets</div></div>")
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$($screens.Count)</div><div class=`"stat-lbl`">Razor Screens</div></div>")
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$entitiesWithScreens</div><div class=`"stat-lbl`">Entities in UI</div></div>")
    [void]$ovHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$totalBindings</div><div class=`"stat-lbl`">Field Bindings</div></div>")
    [void]$ovHtml.AppendLine('</div>')

    # Architecture Card
    [void]$ovHtml.AppendLine('<div class="card">')
    [void]$ovHtml.AppendLine('<div class="card-title">The 3-Tier Enterprise Entity Framework</div>')
    [void]$ovHtml.AppendLine('<div class="desc-box">')
    [void]$ovHtml.AppendLine('<p>BlazorFluent applies strict zero-trust multi-tenancy, soft deletion, and row-level auditing through a standardized three-tier inheritance model:</p>')
    [void]$ovHtml.AppendLine('<ul style="margin: 8px 0 12px 20px;">')
    [void]$ovHtml.AppendLine('<li><b>Tier 1 &mdash; Architectural Markers (<code>IMarkerEntity</code>)</b>: Interface gates driving EF Core query filters and interceptors (<code>ITenantEntity</code>, <code>ISoftDeletableEntity</code>, <code>IAuditableEntity</code>, <code>IProjectScopedEntity</code>).</li>')
    [void]$ovHtml.AppendLine('<li><b>Tier 2 &mdash; Reusable Base Classes</b>: Sequential UUIDv7 primary keys (<code>BaseEntity</code>), audit timestamps (<code>AuditableEntity</code>), tenant write barriers (<code>TenantAuditableEntity</code>), and work item baselines (<code>BaseTaskEntity</code>).</li>')
    [void]$ovHtml.AppendLine('<li><b>Tier 3 &mdash; Domain Entities</b>: Strongly-typed entity records partitioned across isolated bounded contexts and PostgreSQL schemas.</li>')
    [void]$ovHtml.AppendLine('</ul></div></div>')

    # UI Screen Integration Card
    [void]$ovHtml.AppendLine('<div class="card">')
    [void]$ovHtml.AppendLine('<div class="card-title">🖥️ UI Screen & Field Binding Integration (Presentation Layer Parity)</div>')
    [void]$ovHtml.AppendLine('<div class="desc-box">')
    [void]$ovHtml.AppendLine("<p>BlazorFluent indexes all Razor pages, settings tabs, modals, and components (<code>*.razor</code>) to track UI field bindings across domain entities:</p>")
    [void]$ovHtml.AppendLine('<ul style="margin: 8px 0 12px 20px;">')
    [void]$ovHtml.AppendLine("<li><b>$($screens.Count) Screens Cataloged</b>: Across routed pages, modal dialogs, drawers, and multi-tenant settings tabs.</li>")
    [void]$ovHtml.AppendLine("<li><b>$entitiesWithScreens Entities Active in UI</b>: Every entity page documents its screens and exact property bindings (forms, data grids, and audit buttons).</li>")
    [void]$ovHtml.AppendLine("<li><b>$totalBindings Property Usages Tracked</b>: Full traceability between schema properties and presentation components.</li>")
    [void]$ovHtml.AppendLine('</ul>')
    [void]$ovHtml.AppendLine('<a href="razor-pages.html" class="nav-tab active" style="display: inline-block; background: var(--primary); color: #fff; padding: 6px 14px; border-radius: 4px; text-decoration: none; font-weight: 600;">Browse All Razor Screens &rarr;</a>')
    [void]$ovHtml.AppendLine('</div></div>')

    # Subsystem Grid
    [void]$ovHtml.AppendLine('<div class="card"><div class="card-title">Domain Subsystems & Bounded Contexts</div>')
    [void]$ovHtml.AppendLine('<div class="subsystem-grid">')
    foreach ($sg in $subgroups) {
        [void]$ovHtml.AppendLine('<div class="subsystem-card">')
        [void]$ovHtml.AppendLine("<h3><span>$($sg.Name)</span><span class=`"badge-count`">$($sg.Count)</span></h3>")
        [void]$ovHtml.AppendLine('<div class="entity-tag-list">')
        foreach ($e in ($sg.Group | Sort-Object Name)) {
            [void]$ovHtml.AppendLine("<a class=`"entity-tag`" href=`"entities/$($e.Name).html`">$($e.Name)</a>")
        }
        [void]$ovHtml.AppendLine('</div></div>')
    }
    [void]$ovHtml.AppendLine('</div></div>')

    [void]$ovHtml.AppendLine('</div></body></html>')
    Set-Content -Path (Join-Path $resolvedOutputDir "overview.html") -Value $ovHtml.ToString() -Encoding UTF8

    # 5.4 RAZOR-PAGES.HTML (RAZOR SCREENS DICTIONARY)
    $rpTotalPages = ($screens | Where-Object { $_.Route }).Count
    $rpTotalModals = ($screens | Where-Object { $_.ComponentType -in @('Modal', 'Settings Tab', 'Drawer') }).Count

    $rpHtml = [System.Text.StringBuilder]::new()
    [void]$rpHtml.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Razor Screens Dictionary</title><link rel="stylesheet" href="styles.css"></head><body>')
    [void]$rpHtml.AppendLine('<div class="page-container">')
    [void]$rpHtml.AppendLine('<div class="page-header"><div><h1 class="entity-title">Razor Screens & Component Catalog</h1><div class="breadcrumb">Presentation Layer Usage Matrix &bull; BlazorFluent</div></div></div>')
    
    # KPI Grid
    [void]$rpHtml.AppendLine('<div class="stat-grid">')
    [void]$rpHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$($screens.Count)</div><div class=`"stat-lbl`">Screens & Components</div></div>")
    [void]$rpHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$rpTotalPages</div><div class=`"stat-lbl`">Routed Pages (@page)</div></div>")
    [void]$rpHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$rpTotalModals</div><div class=`"stat-lbl`">Modals, Tabs & Drawers</div></div>")
    [void]$rpHtml.AppendLine("<div class=`"stat-card`"><div class=`"stat-val`">$entitiesWithScreens</div><div class=`"stat-lbl`">Entities Active in UI</div></div>")
    [void]$rpHtml.AppendLine('</div>')

    # Search Card
    [void]$rpHtml.AppendLine('<div class="card">')
    [void]$rpHtml.AppendLine('<div class="card-title">Filter Screens & Field Usages</div>')
    [void]$rpHtml.AppendLine('<div style="margin-bottom: 16px;"><input type="text" id="screenSearch" class="search-input" placeholder="Filter screens by name, route, subsystem, entity, or field..." onkeyup="filterScreens()"></div>')
    
    # Table of screens
    [void]$rpHtml.AppendLine('<table><thead><tr><th>Screen / Component</th><th>Type</th><th>Subsystem</th><th>Route Directive</th><th>Referenced Entities & Bound Fields</th><th>Source Path</th></tr></thead><tbody>')

    foreach ($sc in ($screens | Sort-Object Subsystem, Name)) {
        $typeClass = $sc.ComponentType.ToLower() -replace '\s+', ''
        $typeBadge = "<span class=`"badge badge-$typeClass`">$($sc.ComponentType)</span>"
        $routeDisplay = if ($sc.Route) { "<code>$($sc.Route)</code>" } else { "<span style=`"color:#8a8886;font-style:italic;`">$($sc.ComponentType)</span>" }
        
        $entityLines = @()
        $searchTerms = @($sc.Name, $sc.Subsystem, $sc.ComponentType, $sc.Route)

        foreach ($eu in $sc.EntityUsages) {
            $searchTerms += $eu.EntityName
            $fieldPills = @()
            foreach ($f in $eu.Fields) {
                $searchTerms += $f
                $fieldPills += "<span class=`"badge-field`">$f</span>"
            }
            $pillsStr = if ($fieldPills.Count -gt 0) { $fieldPills -join ' ' } else { "<span style=`"color:#8a8886;font-size:11px;`">(instance/ref)</span>" }
            $entityLines += "<div style=`"margin: 4px 0;`"><a class=`"badge badge-base`" href=`"entities/$($eu.EntityName).html`">$($eu.EntityName)</a> $pillsStr</div>"
        }

        $entityDisplay = if ($entityLines.Count -gt 0) { $entityLines -join '' } else { "<span style=`"color:#a19f9d;font-size:12px;`">UI presentational / layout</span>" }
        $searchAttr = ($searchTerms -join ' ').ToLower()

        [void]$rpHtml.AppendLine("<tr class=`"screen-row`" id=`"screen-$($sc.Name)`" data-search=`"$searchAttr`">")
        [void]$rpHtml.AppendLine("<td><b>$($sc.Name)</b></td>")
        [void]$rpHtml.AppendLine("<td>$typeBadge</td>")
        [void]$rpHtml.AppendLine("<td>$($sc.Subsystem)</td>")
        [void]$rpHtml.AppendLine("<td>$routeDisplay</td>")
        [void]$rpHtml.AppendLine("<td>$entityDisplay</td>")
        [void]$rpHtml.AppendLine("<td><span style=`"font-family:Consolas,monospace;font-size:11px;color:#605e5c;`">$($sc.RelativePath)</span></td>")
        [void]$rpHtml.AppendLine('</tr>')
    }

    [void]$rpHtml.AppendLine('</tbody></table></div>')

    # Script for filtering
    [void]$rpHtml.AppendLine(@'
<script>
function filterScreens() {
    var q = document.getElementById('screenSearch').value.toLowerCase();
    var rows = document.querySelectorAll('.screen-row');
    rows.forEach(function(r) {
        var text = r.getAttribute('data-search') || '';
        r.style.display = text.indexOf(q) !== -1 ? '' : 'none';
    });
}
</script>
</div></body></html>
'@)

    Set-Content -Path (Join-Path $resolvedOutputDir "razor-pages.html") -Value $rpHtml.ToString() -Encoding UTF8

    $servicesPlaceholder = @'
<!DOCTYPE html><html><head><meta charset="utf-8"><link rel="stylesheet" href="../styles.css"></head><body>
<div class="page-container">
    <div class="card">
        <div class="card-title">⚙️ Classes & Service Dependencies (Phase 3 Roadmap)</div>
        <div class="desc-box">
            <p>Similar to enterprise metadata dictionaries, Phase 3 will scan domain services and repositories to map:</p>
            <ul style="margin: 12px 0 12px 20px;">
                <li>Which service classes inject <code>AppDbContext</code> and query each entity</li>
                <li>Background jobs and schedulers executing batch operations per entity</li>
                <li>Interceptors and event listeners enforcing cross-cutting rules</li>
            </ul>
            <p style="color: #605e5c; font-style: italic;">Planned implementation: Phase 3.</p>
        </div>
    </div>
</div></body></html>
'@
    Set-Content -Path (Join-Path $placeholdersDir "services.html") -Value $servicesPlaceholder -Encoding UTF8

    # 5.5 ENTITIES/{Name}.HTML
    foreach ($e in ($typeDict.Values | Where-Object { -not $_.IsInterface })) {
        $eHtml = [System.Text.StringBuilder]::new()
        [void]$eHtml.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><link rel="stylesheet" href="../styles.css"></head><body>')
        [void]$eHtml.AppendLine('<div class="page-container">')

        # Breadcrumbs
        [void]$eHtml.AppendLine("<div class=`"breadcrumb`"><a href=`"../overview.html`">Data Model</a> &bull; <span>$($e.Subsystem)</span> &bull; <b>$($e.Name)</b></div>")

        # Page Header
        [void]$eHtml.AppendLine('<div class="page-header">')
        [void]$eHtml.AppendLine("<div><h1 class=`"entity-title`">$($e.Name)</h1>")
        [void]$eHtml.AppendLine('<div class="entity-meta">')
        [void]$eHtml.AppendLine("<span class=`"badge badge-subsystem`">Subsystem: $($e.Subsystem)</span>")
        if ($e.DbSetName) {
            [void]$eHtml.AppendLine("<span class=`"badge badge-tenant`">DbSet: $($e.DbSetName)</span>")
        }
        if ($e.BaseClass) {
            $baseLink = if ($typeDict.ContainsKey($e.BaseClass) -and (-not $typeDict[$e.BaseClass].IsInterface)) { "<a class=`"badge badge-base`" href=`"$($e.BaseClass).html`">Base: $($e.BaseClass)</a>" } else { "<span class=`"badge badge-base`">Base: $($e.BaseClass)</span>" }
            [void]$eHtml.AppendLine($baseLink)
        }
        [void]$eHtml.AppendLine('</div></div></div>')

        # Architectural Markers
        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine('<div class="card-title">Architectural Markers & Security Gates</div>')
        [void]$eHtml.AppendLine('<div class="entity-meta">')
        if ($e.AllMarkers.Contains("ITenantEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-tenant" title="Multi-tenant isolation active via QueryFilters.Tenant">[Tenant Isolated]</span>') }
        if ($e.AllMarkers.Contains("IGlobalEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-global" title="Host-wide entity; bypasses tenant query filter">[Global Scope]</span>') }
        if ($e.AllMarkers.Contains("ISoftDeletableEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-softdelete" title="Soft-deletable record via QueryFilters.SoftDelete">[Soft Deletable]</span>') }
        if ($e.AllMarkers.Contains("IAuditableEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-audit" title="Stamped with Created/Updated audit timestamps">[Auditable]</span>') }
        if ($e.AllMarkers.Contains("IProjectScopedEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-project" title="Guarded by ProjectSecurityInterceptor write barriers">[Project Scoped]</span>') }
        if ($e.AllMarkers.Contains("IEffectiveDatedEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-date" title="Temporal validity via StartDate / EndDate">[Effective Dated]</span>') }
        if ($e.AllMarkers.Contains("IVersionedEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-base" title="Concurrency version tracking">[Versioned]</span>') }
        if ($e.AllMarkers.Contains("IAuditExemptEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-base" title="Exempt from property diff logging">[Audit Exempt]</span>') }
        if ($e.AllMarkers.Contains("IValidationExemptEntity")) { [void]$eHtml.AppendLine('<span class="badge badge-base" title="Exempt from startup validator gate">[Validation Exempt]</span>') }
        [void]$eHtml.AppendLine('</div>')

        if ($e.Summary) {
            [void]$eHtml.AppendLine("<div class=`"desc-box`" style=`"margin-top: 14px;`">$($e.Summary)</div>")
        }
        [void]$eHtml.AppendLine('</div>')

        # Properties Table
        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine("<div class=`"card-title`">Properties & Fields ($($e.Properties.Count))</div>")
        if ($e.Properties.Count -gt 0) {
            [void]$eHtml.AppendLine('<table><thead><tr><th>Property</th><th>Type</th><th>Nullable</th><th>Description</th></tr></thead><tbody>')
            foreach ($p in $e.Properties) {
                $pBadge = ""
                if ($p.IsForeignKey) { $pBadge = "<span class=`"fk-pill`">FK</span>" }
                elseif ($p.IsNavigation) { $pBadge = "<span class=`"fk-pill`">NAV</span>" }
                elseif ($p.IsCollection) { $pBadge = "<span class=`"fk-pill`">1:N</span>" }

                $typeDisplay = $p.Type
                if ($typeDict.ContainsKey($p.CleanType) -and (-not $typeDict[$p.CleanType].IsInterface)) {
                    $typeDisplay = "<a href=`"$($p.CleanType).html`" class=`"code-type`">$($p.Type)</a>"
                } else {
                    $typeDisplay = "<span class=`"code-type`">$($p.Type)</span>"
                }

                $nullStr = if ($p.IsNullable) { "Yes" } else { "No" }
                $descStr = if ($p.Description) { $p.Description } else { "-" }

                [void]$eHtml.AppendLine("<tr><td class=`"prop-name`">$($p.Name) $pBadge</td><td>$typeDisplay</td><td>$nullStr</td><td>$descStr</td></tr>")
            }
            [void]$eHtml.AppendLine('</tbody></table>')
        } else {
            [void]$eHtml.AppendLine('<p style="color:#605e5c;">No declared public properties (inherits all members from base class).</p>')
        }
        [void]$eHtml.AppendLine('</div>')

        # Inbound Relationships (Referenced By)
        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine("<div class=`"card-title`">Inbound References (Where this entity is referenced) ($($e.InboundLinks.Count))</div>")
        if ($e.InboundLinks.Count -gt 0) {
            [void]$eHtml.AppendLine('<table><thead><tr><th>Source Entity</th><th>Subsystem</th><th>Referencing Property</th><th>Relation Type</th></tr></thead><tbody>')
            foreach ($inLink in $e.InboundLinks) {
                [void]$eHtml.AppendLine("<tr><td><a href=`"$($inLink.SourceEntity).html`"><b>$($inLink.SourceEntity)</b></a></td><td>$($inLink.SourceSubsystem)</td><td><code>$($inLink.SourceProperty)</code></td><td>$($inLink.Type)</td></tr>")
            }
            [void]$eHtml.AppendLine('</tbody></table>')
        } else {
            [void]$eHtml.AppendLine('<p style="color:#605e5c;">No inbound foreign keys or navigations detected.</p>')
        }
        [void]$eHtml.AppendLine('</div>')

        # Screen / Razor Page Usages (Presentation Layer Parity)
        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine("<div class=`"card-title`">🖥️ Screen / Razor Page Usages ($($e.ScreenUsages.Count))</div>")
        if ($e.ScreenUsages.Count -gt 0) {
            [void]$eHtml.AppendLine('<table><thead><tr><th>Screen / Component</th><th>Type</th><th>Subsystem</th><th>Route Directive</th><th>Fields & Properties Referenced</th></tr></thead><tbody>')
            foreach ($su in ($e.ScreenUsages | Sort-Object Subsystem, ScreenName)) {
                $typeClass = $su.ComponentType.ToLower() -replace '\s+', ''
                $typeBadge = "<span class=`"badge badge-$typeClass`">$($su.ComponentType)</span>"
                $routeDisplay = if ($su.Route) { "<code>$($su.Route)</code>" } else { "<span style=`"color:#8a8886;font-style:italic;`">$($su.ComponentType)</span>" }
                
                $fieldPills = @()
                foreach ($f in $su.Fields) {
                    $fieldPills += "<span class=`"badge-field`">$f</span>"
                }
                $fieldsDisplay = if ($fieldPills.Count -gt 0) { $fieldPills -join ' ' } else { "<span style=`"color:#8a8886;font-style:italic;`">Entity instance / collection reference</span>" }

                [void]$eHtml.AppendLine("<tr><td><a href=`"../razor-pages.html#screen-$($su.ScreenName)`"><b>$($su.ScreenName)</b></a></td><td>$typeBadge</td><td>$($su.Subsystem)</td><td>$routeDisplay</td><td>$fieldsDisplay</td></tr>")
            }
            [void]$eHtml.AppendLine('</tbody></table>')
        } else {
            [void]$eHtml.AppendLine('<div class="desc-box" style="color: #605e5c; font-style: italic;">No direct Razor component references detected. (Backend domain entity, background batch worker, or intermediate staging ledger).</div>')
        }
        [void]$eHtml.AppendLine('</div>')

        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine('<div class="card-title">⚙️ Service Dependencies (Phase 3)</div>')
        [void]$eHtml.AppendLine('<div class="desc-box" style="color: #605e5c; font-style: italic;">Domain services, repositories, and scheduled jobs referencing this entity will be automatically indexed here in Phase 3.</div>')
        [void]$eHtml.AppendLine('</div>')

        [void]$eHtml.AppendLine('</div></body></html>')

        $entPath = Join-Path $entitiesDir "$($e.Name).html"
        Set-Content -Path $entPath -Value $eHtml.ToString() -Encoding UTF8
    }

    # 5.6 INDEX.HTML (PORTAL SHELL)
    $indexHtml = @'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <title>BlazorFluent Data Dictionary</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body style="overflow: hidden;">
    <header class="top-header">
        <div class="brand">
            <span class="brand-badge">Enterprise Edition</span>
            <span>BlazorFluent Data Dictionary</span>
        </div>
        <nav class="nav-tabs">
            <a href="overview.html" target="contentFrame" class="nav-tab active" onclick="switchNav(this)">📊 Data Model</a>
            <a href="razor-pages.html" target="contentFrame" class="nav-tab" onclick="switchNav(this)">🖥️ Razor Screens</a>
            <a href="placeholders/services.html" target="contentFrame" class="nav-tab" onclick="switchNav(this)">⚙️ Classes & Services (Phase 3)</a>
        </nav>
    </header>

    <div class="portal-container">
        <iframe src="sidebar.html" class="sidebar-pane" name="sidebarFrame" id="sidebarFrame"></iframe>
        <iframe src="overview.html" class="content-pane" name="contentFrame" id="contentFrame"></iframe>
    </div>

    <script>
    function switchNav(el) {
        document.querySelectorAll('.nav-tab').forEach(function(t) { t.classList.remove('active'); });
        el.classList.add('active');
    }
    </script>
</body>
</html>
'@
    Set-Content -Path (Join-Path $resolvedOutputDir "index.html") -Value $indexHtml -Encoding UTF8

    Write-Host "HTML Data Dictionary Portal successfully generated at:" -ForegroundColor Green
    Write-Host " $(Join-Path $resolvedOutputDir 'index.html')" -ForegroundColor Cyan
}

# ─── 6. GENERATE MARKDOWN SPECIFICATION ───────────────────────────────────────
if ($Format -eq "Markdown" -or $Format -eq "All") {
    Write-Host "Updating Markdown specification in '$resolvedMarkdown'..." -ForegroundColor Cyan

    $doc = [System.Text.StringBuilder]::new()
    [void]$doc.AppendLine("# Data Model & Entity Hierarchy Reference (Data Dictionary)")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("> [!NOTE]")
    [void]$doc.AppendLine("> **Automated Specification**: This document is generated from source code by `scripts/Generate-DataModelDocs.ps1`.")
    [void]$doc.AppendLine("> An interactive HTML portal version is available at [`docs/data-dictionary/index.html`](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/data-dictionary/index.html).")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("---")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("## 1. Architectural Foundation: The 3-Tier Entity Model")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("BlazorFluent enforces strict zero-trust multi-tenancy and audit compliance via a 3-tier entity taxonomy:")
    [void]$doc.AppendLine("1. **Tier 1: Architectural Marker Interfaces (`IMarkerEntity`)**: Compile-time gates and EF Core interceptor drivers (`ITenantEntity`, `IGlobalEntity`, `ISoftDeletableEntity`, `IProjectScopedEntity`, `IAuditableEntity`, `IEffectiveDatedEntity`, `IVersionedEntity`).")
    [void]$doc.AppendLine("2. **Tier 2: Framework Base Classes**: Reusable abstractions encapsulating sequential UUIDv7 primary keys (`BaseEntity`), Level 1 audit properties (`AuditableEntity`), tenant isolation barriers (`TenantAuditableEntity`), work items (`BaseTaskEntity`), and temporal schedules (`BaseCalendarEntity`).")
    [void]$doc.AppendLine("3. **Tier 3: Domain Entities**: Strongly-typed business objects executing inside isolated database schemas and bounded contexts.")
    [void]$doc.AppendLine("")

    # Table of entities
    [void]$doc.AppendLine("## 2. Data Dictionary Reference Matrix")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("| Entity Name | Subsystem | DbSet | Base Class | Architectural Markers | Foreign Keys & Navigations | UI Screen Usages | Description |")
    [void]$doc.AppendLine("|:---|:---|:---|:---|:---|:---|:---|:---|")

    $sortedEntities = $typeDict.Values | 
        Where-Object { -not $_.IsInterface } | 
        Sort-Object Subsystem, Name

    foreach ($e in $sortedEntities) {
        $dbSet = if ($e.DbSetName) { '`' + $e.DbSetName + '`' } else { "-" }
        $baseClass = if ($e.BaseClass) { '`' + $e.BaseClass + '`' } else { "-" }
        
        $markerList = @()
        if ($e.AllMarkers.Contains("ITenantEntity")) { $markerList += '`[Tenant]`' }
        if ($e.AllMarkers.Contains("IGlobalEntity")) { $markerList += '`[Global]`' }
        if ($e.AllMarkers.Contains("ISoftDeletableEntity")) { $markerList += '`[SoftDelete]`' }
        if ($e.AllMarkers.Contains("IAuditableEntity")) { $markerList += '`[Audit]`' }
        if ($e.AllMarkers.Contains("IProjectScopedEntity")) { $markerList += '`[ProjectScoped]`' }
        if ($e.AllMarkers.Contains("IEffectiveDatedEntity")) { $markerList += '`[EffectiveDated]`' }
        if ($e.AllMarkers.Contains("IVersionedEntity")) { $markerList += '`[Versioned]`' }
        if ($e.AllMarkers.Contains("IAuditExemptEntity")) { $markerList += '`[AuditExempt]`' }
        if ($e.AllMarkers.Contains("IValidationExemptEntity")) { $markerList += '`[ValidationExempt]`' }

        $markersStr = if ($markerList.Count -gt 0) { $markerList -join " " } else { "-" }

        $linkList = @()
        foreach ($fk in $e.ForeignKeys) { $linkList += "$($fk.Name)" }
        foreach ($nav in $e.Navigations) { $linkList += "&rarr; $($nav.TargetEntity)" }
        $linksStr = if ($linkList.Count -gt 0) { $linkList -join "<br/>" } else { "-" }

        $cleanDesc = if ($e.Summary) { 
            $e.Summary -replace '\|', '&#124;' -replace '\r?\n', ' ' 
        } else { 
            "-" 
        }

        $screenUsageStr = if ($e.ScreenUsages.Count -gt 0) {
            $topScreens = ($e.ScreenUsages | Select-Object -First 2 | ForEach-Object { $_.ScreenName }) -join ", "
            if ($e.ScreenUsages.Count -gt 2) {
                "$($e.ScreenUsages.Count) screens ($topScreens, ...)"
            } else {
                "$($e.ScreenUsages.Count) screens ($topScreens)"
            }
        } else {
            "-"
        }

        [void]$doc.AppendLine("| **[$($e.Name)](data-dictionary/entities/$($e.Name).html)** | $($e.Subsystem) | $dbSet | $baseClass | $markersStr | $linksStr | $screenUsageStr | $cleanDesc |")
    }

    $mdDir = Split-Path -Parent $resolvedMarkdown
    if (-not (Test-Path $mdDir)) { New-Item -ItemType Directory -Path $mdDir -Force | Out-Null }
    Set-Content -Path $resolvedMarkdown -Value $doc.ToString() -Encoding UTF8
    Write-Host "Markdown specification updated at: $resolvedMarkdown" -ForegroundColor Green
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host " [COMPLETE] Data Dictionary generation finished successfully!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
