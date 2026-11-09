<#
.SYNOPSIS
    Generates an enterprise-grade HTML Data Dictionary portal and Markdown specification
    for BlazorEnterprise, modeled after modern enterprise architecture data dictionary standards.

.DESCRIPTION
    Scans the C# domain entity layer (src/BlazorEnterprise.Core/Domain) and AppDbContext.cs.
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
    Path to Domain entities. Defaults to "src/BlazorEnterprise.Core/Domain".

.PARAMETER DbContextPath
    Path to AppDbContext.cs. Defaults to "src/BlazorEnterprise.Persistence/Context/AppDbContext.cs".

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
    [string]$DomainPath = "src/BlazorEnterprise.Core/Domain",
    [string]$DbContextPath = "src/BlazorEnterprise.Persistence/Context/AppDbContext.cs",
    [string]$ModelSnapshotPath = "src/BlazorEnterprise.Persistence/Migrations/AppDbContextModelSnapshot.cs"
)

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

# --- 1. RESOLVE DIRECTORIES ---------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path

$resolvedDomain = if ([System.IO.Path]::IsPathRooted($DomainPath)) { $DomainPath } else { Join-Path $rootDir $DomainPath }
$resolvedDbContext = if ([System.IO.Path]::IsPathRooted($DbContextPath)) { $DbContextPath } else { Join-Path $rootDir $DbContextPath }
$resolvedOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $rootDir $OutputDir }
$resolvedMarkdown = if ([System.IO.Path]::IsPathRooted($MarkdownPath)) { $MarkdownPath } else { Join-Path $rootDir $MarkdownPath }
$resolvedSnapshot = if ([System.IO.Path]::IsPathRooted($ModelSnapshotPath)) { $ModelSnapshotPath } else { Join-Path $rootDir $ModelSnapshotPath }

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

# --- 2. MAP DBSETS AND EF CORE MODELSNAPSHOT RELATIONSHIPS --------------------
$dbSetMap = @{}
if (Test-Path $resolvedDbContext) {
    $dbContextContent = Get-Content $resolvedDbContext -Raw -Encoding utf8
    $matches = [regex]::Matches($dbContextContent, 'public\s+DbSet<(?<entity>\w+)>\s+(?<name>\w+)\s*=>')
    foreach ($m in $matches) {
        $dbSetMap[$m.Groups['entity'].Value] = $m.Groups['name'].Value
    }
}
Write-Host "Mapped $($dbSetMap.Count) DbSet properties from AppDbContext." -ForegroundColor Green

# Parse EF Core ModelSnapshot
$classToTable = @{}
$tableToClass = @{}
$tableMetadata = [System.Collections.Generic.Dictionary[string, hashtable]]::new()
$snapshotRelationships = [System.Collections.Generic.List[hashtable]]::new()

if (Test-Path $resolvedSnapshot) {
    $snapshotContent = Get-Content -Path $resolvedSnapshot -Raw -Encoding utf8
    $entityBlockPattern = 'modelBuilder\.Entity\("([^"]+)",\s*b\s*=>\s*\{([\s\S]*?)\r?\n\s{16}\}\);'
    $entityMatches = [regex]::Matches($snapshotContent, $entityBlockPattern)

    foreach ($match in $entityMatches) {
        $fullTypeName = $match.Groups[1].Value.Trim()
        $body = $match.Groups[2].Value
        $shortName = $fullTypeName.Substring($fullTypeName.LastIndexOf('.') + 1)

        $toTableMatch = [regex]::Match($body, 'b\.ToTable\("([^"]+)"(?:,\s*"([^"]+)")?')
        if ($toTableMatch.Success) {
            $tableName = $toTableMatch.Groups[1].Value
            $schema = "dbo"
            if ($toTableMatch.Groups[2].Success -and -not [string]::IsNullOrWhiteSpace($toTableMatch.Groups[2].Value)) {
                $schema = $toTableMatch.Groups[2].Value
            }

            $classToTable[$fullTypeName] = $tableName
            $classToTable[$shortName] = $tableName
            $tableToClass[$tableName] = $shortName

            if (-not $tableMetadata.ContainsKey($tableName)) {
                $tableMetadata[$tableName] = @{
                    TableName = $tableName
                    ClassName = $shortName
                    Schema    = $schema
                }
            }
        }
    }

    # Extract Foreign Key relationships
    foreach ($match in $entityMatches) {
        $fullSourceTypeName = $match.Groups[1].Value.Trim()
        $body = $match.Groups[2].Value
        $shortSourceName = $fullSourceTypeName.Substring($fullSourceTypeName.LastIndexOf('.') + 1)

        $sourceTable = if ($classToTable.ContainsKey($fullSourceTypeName)) {
            $classToTable[$fullSourceTypeName]
        } elseif ($classToTable.ContainsKey($shortSourceName)) {
            $classToTable[$shortSourceName]
        } else {
            $shortSourceName
        }

        $hasOnePattern = 'b\.HasOne\("([^"]+)",\s*(?:"([^"]+)"|null)\)\s*\r?\n\s*\.(WithMany|WithOne)\((?:"([^"]+)")?\)'
        $relMatches = [regex]::Matches($body, $hasOnePattern)

        foreach ($rel in $relMatches) {
            $targetTypeName = $rel.Groups[1].Value.Trim()
            $targetShortName = $targetTypeName.Substring($targetTypeName.LastIndexOf('.') + 1)
            
            $targetTable = if ($classToTable.ContainsKey($targetTypeName)) {
                $classToTable[$targetTypeName]
            } elseif ($classToTable.ContainsKey($targetShortName)) {
                $classToTable[$targetShortName]
            } else {
                $targetShortName
            }

            $navProp = if ($rel.Groups[2].Success) { $rel.Groups[2].Value } else { "" }
            $withType = $rel.Groups[3].Value
            $inverseNav = if ($rel.Groups[4].Success) { $rel.Groups[4].Value } else { "" }

            $cardinality = if ($withType -eq "WithOne") { "||--||" } else { "||--o{" }

            $label = if (-not [string]::IsNullOrWhiteSpace($inverseNav)) {
                $inverseNav
            } elseif (-not [string]::IsNullOrWhiteSpace($navProp)) {
                $navProp
            } else {
                "references"
            }

            $parentEntityName = if ($tableToClass.ContainsKey($targetTable)) { $tableToClass[$targetTable] } else { $targetShortName }
            $childEntityName = if ($tableToClass.ContainsKey($sourceTable)) { $tableToClass[$sourceTable] } else { $shortSourceName }
            $relKey = "$parentEntityName|$cardinality|$childEntityName|$label"
            $snapshotRelationships.Add(@{
                Key          = $relKey
                ParentEntity = $parentEntityName
                ChildEntity  = $childEntityName
                ParentTable  = $targetTable
                ChildTable   = $sourceTable
                Cardinality  = $cardinality
                Label        = $label
            })
        }
    }
}
$uniqueSnapshotRels = $snapshotRelationships | Group-Object Key | ForEach-Object { $_.Group[0] }
Write-Host "Mapped $($tableMetadata.Count) database tables and $($uniqueSnapshotRels.Count) foreign key relationships from ModelSnapshot." -ForegroundColor Green

# --- 3. SCAN ENTITY FILES -----------------------------------------------------
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
    $content = Get-Content $file.FullName -Raw -Encoding utf8
    $relDir = Split-Path $file.FullName -Parent
    $subsystem = Split-Path $relDir -Leaf
    if ($subsystem -eq "Domain") { $subsystem = "Base" }

    $lines = $content -split '\r?\n'
    $curDocLines = [System.Collections.Generic.List[string]]::new()
    $inDoc = $false
    
    # Track types declared in this file
    $fileTypes = [System.Collections.Generic.List[PSObject]]::new()
    $currentType = $null

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]

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
            # Attribute line preceding class/prop
            continue
        }

        # Check for interface declaration
        if ($line -match '^\s*public\s+interface\s+(?<name>\w+)(?:\s*:\s*(?<bases>[^\r\n{]+))?') {
            $iName = $Matches['name']
            $iBases = @()
            if ($Matches['bases']) {
                $iBases = $Matches['bases'] -split ',' | ForEach-Object { $_.Trim() }
            }
            $iSummary = ($curDocLines -join ' ')
            $curDocLines.Clear()

            $currentType = [PSCustomObject]@{
                Name           = $iName
                File           = $file.FullName
                Subsystem      = $subsystem
                IsInterface    = $true
                IsAbstract     = $false
                Summary        = $iSummary
                Inherits       = $iBases
                BaseClass      = ($iBases | Where-Object { $_ -notmatch '^I[A-Z]' } | Select-Object -First 1)
                DirectMarkers  = ($iBases | Where-Object { $_ -match '^I[A-Z]' })
                AllMarkers     = [System.Collections.Generic.HashSet[string]]::new()
                Properties     = @()
                Navigations    = @()
                Collections    = @()
                ForeignKeys    = @()
                InboundLinks   = @()
                DbSetName      = $null
            }
            $fileTypes.Add($currentType)
            continue
        }

        # Check for class declaration
        if ($line -match '^\s*public\s+(?:(?<abstract>abstract)\s+)?(?:partial\s+)?class\s+(?<name>\w+)(?:\s*:\s*(?<bases>[^\r\n{]+))?') {
            $cName = $Matches['name']
            $isAbs = [bool]$Matches['abstract']
            $cBases = @()
            if ($Matches['bases']) {
                $cBases = $Matches['bases'] -split ',' | ForEach-Object { $_.Trim() }
            }
            $cSummary = ($curDocLines -join ' ')
            $curDocLines.Clear()

            $currentType = [PSCustomObject]@{
                Name           = $cName
                File           = $file.FullName
                Subsystem      = $subsystem
                IsInterface    = $false
                IsAbstract     = $isAbs
                Summary        = $cSummary
                Inherits       = $cBases
                BaseClass      = ($cBases | Where-Object { $_ -notmatch '^I[A-Z]' } | Select-Object -First 1)
                DirectMarkers  = ($cBases | Where-Object { $_ -match '^I[A-Z]' })
                AllMarkers     = [System.Collections.Generic.HashSet[string]]::new()
                Properties     = @()
                Navigations    = @()
                Collections    = @()
                ForeignKeys    = @()
                InboundLinks   = @()
                DbSetName      = $dbSetMap[$cName]
            }
            $fileTypes.Add($currentType)
            continue
        }

        # Check for property declaration (supports required, virtual, override, new, block getters, interface members)
        $isPropMatch = ($line -match '^\s*(?:public\s+)?(?:(?:virtual|override|required|new)\s+)*(?<type>[A-Za-z0-9_<>?]+)\s+(?<prop>\w+)\s*(?:\{\s*get\b|=>)')
        if ($isPropMatch -and $currentType) {
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
                Origin       = $currentType.Name
            }

            $currentType.Properties += $propObj
            if ($isFk) { $currentType.ForeignKeys += $propObj }
            if ($isNav) { $currentType.Navigations += $propObj }
            if ($isCol) { $currentType.Collections += $propObj }
            continue
        }

        if ($line.Trim().Length -gt 0) {
            $curDocLines.Clear()
        }
    }

    foreach ($ft in $fileTypes) {
        $typeDict[$ft.Name] = $ft
    }
}

# --- 4. RESOLVE TRANSITIVE MARKERS & INBOUND LINKS -----------------------------
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

# --- 4.5 SCAN RAZOR PAGES & UI FIELD USAGES (PRESENTATION LAYER USAGE) ----------
Write-Host "Scanning Razor screens and field usages across UI layer..." -ForegroundColor Cyan

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$webPath = Join-Path $repoRoot "src\BlazorEnterprise.Web"
$uiTemplatesPath = Join-Path $repoRoot "src\BlazorEnterprise.Shared.UI"

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
    $rfContent = Get-Content $rf.FullName -Raw -Encoding utf8
    $csCompanion = $rf.FullName + ".cs"
    if (Test-Path $csCompanion) {
        $rfContent += "`n" + (Get-Content $csCompanion -Raw -Encoding utf8)
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
        $rawTitle = $Matches['title'] -replace '&amp;', '&' -replace '\s*-\s*BlazorEnterprise.*$', '' -replace '\s*-\s*SAMPLE PAGE ONLY.*$', ''
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


# --- 5. GENERATE HTML PORTAL --------------------------------------------------
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

/* Entity Hierarchy Tree & Slide-Over Drawer */
.hierarchy-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 20px; flex-wrap: wrap; background: #fff; padding: 12px 16px; border: 1px solid var(--border); border-radius: 4px; }
.btn-action { background: var(--bg-main); border: 1px solid var(--border); padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 12px; font-weight: 600; color: var(--text-primary); transition: all 0.2s; }
.btn-action:hover { background: #e1dfdd; border-color: #c8c6c4; }
.btn-action.primary { background: var(--primary); color: #fff; border-color: var(--primary-dark); }
.btn-action.primary:hover { background: var(--primary-dark); }

.tree-container { background: #fff; border: 1px solid var(--border); border-radius: 4px; padding: 24px; overflow-x: auto; min-height: 600px; }
.tree-root { list-style: none; padding-left: 0; margin: 0; }
.tree-branch { list-style: none; padding-left: 28px; position: relative; margin: 4px 0; }
.tree-branch::before { content: ""; position: absolute; left: 10px; top: 0; bottom: 0; border-left: 2px dashed #edebe9; }
.tree-node-wrap { display: flex; align-items: center; gap: 8px; margin: 6px 0; position: relative; }
.tree-node-wrap::before { content: ""; position: absolute; left: -18px; top: 50%; width: 14px; border-top: 2px dashed #edebe9; }

.toggle-btn { width: 22px; height: 22px; border-radius: 4px; border: 1px solid #c8c6c4; background: #faf9f8; display: inline-flex; align-items: center; justify-content: center; cursor: pointer; user-select: none; transition: all 0.15s; flex-shrink: 0; padding: 0; color: #605e5c; }
.toggle-btn:hover { background: var(--primary); color: #fff; border-color: var(--primary-dark); }
.toggle-btn .chevron-icon { transition: transform 0.2s cubic-bezier(0.4, 0, 0.2, 1); transform: rotate(0deg); }
.toggle-btn.collapsed .chevron-icon { transform: rotate(-90deg); }
.toggle-spacer { width: 22px; height: 22px; flex-shrink: 0; }

.tree-card { display: inline-flex; flex-direction: column; background: #ffffff; border: 1.5px solid #e1dfdd; border-radius: 6px; padding: 8px 14px; cursor: pointer; transition: all 0.2s ease-in-out; box-shadow: 0 1.6px 3.6px 0 rgba(0,0,0,0.06); min-width: 260px; max-width: 480px; }
.tree-card:hover { border-color: var(--primary); box-shadow: 0 3.2px 7.2px 0 rgba(0,120,212,0.18); transform: translateY(-1px); }
.tree-card.root-card { border-left: 4px solid var(--primary); background: #faf9f8; }
.tree-card.base-card { border-left: 4px solid #5c2d91; }
.tree-card.tenant-card { border-left: 4px solid #004578; }
.tree-card.domain-card { border-left: 4px solid #107c10; }

.tree-card-header { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.tree-entity-name { font-size: 14px; font-weight: 700; color: #201f1e; font-family: Consolas, monospace; }
.tree-subsystem-pill { font-size: 10px; font-weight: 600; padding: 2px 6px; border-radius: 3px; background: #edebe9; color: #605e5c; text-transform: uppercase; }

.tree-card-extends { font-size: 11px; color: #605e5c; margin-top: 3px; font-family: Consolas, monospace; }
.tree-card-extends strong { color: #a4262c; }
.tree-card-ancestors { font-size: 10px; color: #8a8886; margin-top: 2px; font-family: Consolas, monospace; }

.tree-children { transition: all 0.2s ease; margin: 0; }
.tree-children.collapsed { display: none; }

/* Slide-Over Drawer */
.drawer-backdrop { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.4); z-index: 1000; opacity: 0; pointer-events: none; transition: opacity 0.3s ease; }
.drawer-backdrop.open { opacity: 1; pointer-events: auto; }
.slide-drawer { position: fixed; top: 0; right: -750px; width: 700px; max-width: 85vw; height: 100vh; background: #fff; box-shadow: -4px 0 16px rgba(0,0,0,0.25); z-index: 1001; transition: right 0.3s cubic-bezier(0.16, 1, 0.3, 1); display: flex; flex-direction: column; }
.slide-drawer.open { right: 0; }
.drawer-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 20px; background: #002050; color: #fff; border-bottom: 2px solid var(--primary); }
.drawer-title { font-size: 15px; font-weight: 600; display: flex; align-items: center; gap: 8px; }
.drawer-close { background: transparent; border: none; color: #fff; font-size: 20px; font-weight: 700; cursor: pointer; padding: 2px 8px; border-radius: 4px; line-height: 1; }
.drawer-close:hover { background: rgba(255,255,255,0.2); }
.drawer-body { flex: 1; overflow: hidden; background: #f3f2f1; }
.drawer-iframe { width: 100%; height: 100%; border: none; background: #fff; }
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
    [void]$ovHtml.AppendLine('<div class="page-header"><div><h1 class="entity-title">Data Model Architecture Overview</h1><div class="breadcrumb">Enterprise Data Dictionary &bull; BlazorEnterprise Monolith</div></div></div>')

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
    [void]$ovHtml.AppendLine('<p>BlazorEnterprise applies strict zero-trust multi-tenancy, soft deletion, and row-level auditing through a standardized three-tier inheritance model:</p>')
    [void]$ovHtml.AppendLine('<ul style="margin: 8px 0 12px 20px;">')
    [void]$ovHtml.AppendLine('<li><b>Tier 1 &mdash; Architectural Markers (<code>IMarkerEntity</code>)</b>: Interface gates driving EF Core query filters and interceptors (<code>ITenantEntity</code>, <code>ISoftDeletableEntity</code>, <code>IAuditableEntity</code>, <code>IProjectScopedEntity</code>).</li>')
    [void]$ovHtml.AppendLine('<li><b>Tier 2 &mdash; Reusable Base Classes</b>: Sequential UUIDv7 primary keys (<code>BaseEntity</code>), audit timestamps (<code>AuditableEntity</code>), tenant write barriers (<code>TenantAuditableEntity</code>), and work item baselines (<code>BaseTaskEntity</code>).</li>')
    [void]$ovHtml.AppendLine('</ul>')
    [void]$ovHtml.AppendLine('<a href="hierarchy.html" class="nav-tab active" style="display: inline-block; background: var(--primary); color: #fff; padding: 6px 14px; border-radius: 4px; text-decoration: none; font-weight: 600;">🌳 Explore Collapsible Entity Hierarchy Tree &rarr;</a>')
    [void]$ovHtml.AppendLine('</div></div>')

    # UI Screen Integration Card
    [void]$ovHtml.AppendLine('<div class="card">')
    [void]$ovHtml.AppendLine('<div class="card-title">🖥️ UI Screen & Field Binding Integration (Presentation Layer Parity)</div>')
    [void]$ovHtml.AppendLine('<div class="desc-box">')
    [void]$ovHtml.AppendLine("<p>BlazorEnterprise indexes all Razor pages, settings tabs, modals, and components (<code>*.razor</code>) to track UI field bindings across domain entities:</p>")
    [void]$ovHtml.AppendLine('<ul style="margin: 8px 0 12px 20px;">')
    [void]$ovHtml.AppendLine("<li><b>$($screens.Count) Screens Cataloged</b>: Across routed pages, modal dialogs, drawers, and multi-tenant settings tabs.</li>")
    [void]$ovHtml.AppendLine("<li><b>$entitiesWithScreens Entities Active in UI</b>: Every entity page documents its screens and exact property bindings (forms, data grids, and audit buttons).</li>")
    [void]$ovHtml.AppendLine("<li><b>$totalBindings Property Usages Tracked</b>: Full traceability between schema properties and presentation components.</li>")
    [void]$ovHtml.AppendLine('</ul>')
    [void]$ovHtml.AppendLine('<a href="razor-pages.html" class="nav-tab active" style="display: inline-block; background: var(--primary); color: #fff; padding: 6px 14px; border-radius: 4px; text-decoration: none; font-weight: 600;">Browse All Razor Screens &rarr;</a>')
    [void]$ovHtml.AppendLine('</div></div>')

    # Visual ER Diagram Banner Card
    [void]$ovHtml.AppendLine('<div class="card" style="background: linear-gradient(135deg, #ffffff 0%, #f0f7ff 100%); border-left: 4px solid var(--primary);">')
    [void]$ovHtml.AppendLine('<div class="card-title">🗺️ Interactive Visual Entity-Relationship Diagram</div>')
    [void]$ovHtml.AppendLine('<div class="desc-box">')
    [void]$ovHtml.AppendLine('<p>Explore the full platform database architecture visually with real-time relational foreign key navigations and interactive clickable entity nodes:</p>')
    [void]$ovHtml.AppendLine('<ul style="margin: 8px 0 12px 20px;">')
    [void]$ovHtml.AppendLine("<li><b>$($tableMetadata.Count) Database Tables Mapped</b>: Complete schema coverage across all tenancy, identity, tasks, and audit bounded contexts.</li>")
    [void]$ovHtml.AppendLine("<li><b>$($uniqueSnapshotRels.Count) Relational Connections</b>: Visual cardinalities (1:N, 1:1) and navigation property labels.</li>")
    [void]$ovHtml.AppendLine('<li><b>Interactive Click-Through</b>: Click any entity box in the diagram to inspect its properties, marker gates, and presentation usages.</li>')
    [void]$ovHtml.AppendLine('</ul>')
    [void]$ovHtml.AppendLine('<a href="diagram.html" class="nav-tab active" style="display: inline-block; background: var(--primary); color: #fff; padding: 6px 14px; border-radius: 4px; text-decoration: none; font-weight: 600;">Open Interactive Visual ER Diagram &rarr;</a>')
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
    [void]$rpHtml.AppendLine('<div class="page-header"><div><h1 class="entity-title">Razor Screens & Component Catalog</h1><div class="breadcrumb">Presentation Layer Usage Matrix &bull; BlazorEnterprise</div></div></div>')
    
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

    # 5.3B DIAGRAM.HTML (INTERACTIVE CLICKABLE MERMAID ER DIAGRAM WITH PAN & ZOOM)
    Write-Host "Building Interactive Visual ER Diagram with Pan & Zoom in '$(Join-Path $resolvedOutputDir 'diagram.html')'..." -ForegroundColor Cyan
    $diagHtml = [System.Text.StringBuilder]::new()
    [void]$diagHtml.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Visual ER Diagram</title><link rel="stylesheet" href="styles.css">')
    [void]$diagHtml.AppendLine('<script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>')
    [void]$diagHtml.AppendLine(@'
<style>
.diag-container { max-width: 100%; padding: 16px 24px; box-sizing: border-box; }
.diag-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; background: #fff; padding: 10px 18px; border-radius: 6px; border: 1px solid var(--border); margin-bottom: 14px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); flex-wrap: wrap; }
.hint-box { font-size: 13px; color: var(--text-secondary); display: flex; align-items: center; gap: 8px; }
.hint-badge { background: #e1dfdd; color: var(--text-primary); font-size: 11px; padding: 2px 6px; border-radius: 4px; font-weight: 600; }
.toolbar-controls { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.zoom-indicator { font-family: Consolas, monospace; font-size: 12px; font-weight: 700; color: var(--primary); min-width: 48px; text-align: center; }

/* Viewport with Drag-to-Pan and Smooth Zoom */
.diag-viewport {
    position: relative;
    width: 100%;
    height: 78vh;
    min-height: 650px;
    background: #ffffff;
    border: 1px solid var(--border);
    border-radius: 6px;
    box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    overflow: hidden;
    cursor: grab;
    user-select: none;
    box-sizing: border-box;
}
.diag-viewport:active {
    cursor: grabbing;
}
.diag-viewport:fullscreen {
    height: 100vh;
    border-radius: 0;
    border: none;
    padding: 20px;
    background: #ffffff;
}

/* Transform Canvas */
.diag-canvas {
    position: absolute;
    top: 0;
    left: 0;
    transform-origin: 0 0;
    transition: transform 0.05s ease-out;
    display: inline-block;
    padding: 40px;
}
.diag-canvas pre.mermaid {
    margin: 0;
    padding: 0;
    text-align: center;
}
.diag-canvas svg {
    max-width: none !important;
    height: auto !important;
    overflow: visible !important;
}

g.entityBox, g[id*="entity-"], .er.entityBox { cursor: pointer; transition: all 0.2s; }
g.entityBox:hover, g[id*="entity-"]:hover { filter: drop-shadow(0 0 6px var(--primary)); opacity: 0.95; }

/* Interactive Hover Highlighting Classes */
.er-dimmed {
    opacity: 0.15 !important;
    transition: opacity 0.2s ease-in-out;
}
.er-highlighted-box {
    filter: drop-shadow(0 0 10px rgba(0, 120, 212, 0.95)) !important;
    opacity: 1 !important;
}
.er-connected-box {
    filter: drop-shadow(0 0 8px rgba(16, 124, 16, 0.9)) !important;
    opacity: 1 !important;
}
.er-highlighted-line {
    stroke: #0078d4 !important;
    stroke-width: 3.5px !important;
    stroke-opacity: 1 !important;
    filter: drop-shadow(0 0 5px rgba(0, 120, 212, 0.8)) !important;
    opacity: 1 !important;
}
.er-highlighted-label {
    fill: #004578 !important;
    font-weight: 700 !important;
    font-size: 13px !important;
    opacity: 1 !important;
}
</style>
</head><body>
<div class="diag-container">
    <div class="page-header" style="margin-bottom: 12px;">
        <div>
            <h1 class="entity-title" style="font-size:22px;">🗺️ Visual Entity-Relationship Diagram</h1>
            <div class="breadcrumb">Comprehensive Database Relational Architecture &bull; Click any entity box to view its full specification</div>
        </div>
    </div>
    
    <div class="diag-toolbar">
        <div class="hint-box">
            <span class="hint-badge">💡 Controls</span>
            <span>Drag canvas to pan &bull; Mouse wheel to zoom &bull; Click entity box to inspect</span>
        </div>
        <div class="toolbar-controls">
            <span class="zoom-indicator" id="zoomLevel">100%</span>
            <button class="btn-action" onclick="zoomIn()" title="Zoom In (Ctrl + Scroll Up)">➕ Zoom In</button>
            <button class="btn-action" onclick="zoomOut()" title="Zoom Out (Ctrl + Scroll Down)">➖ Zoom Out</button>
            <button class="btn-action" onclick="fitView()" title="Fit Diagram to Viewport">📐 Fit View</button>
            <button class="btn-action" onclick="resetZoom()" title="Reset to 100%">🔄 100%</button>
            <button class="btn-action primary" onclick="toggleFullscreen()" title="Toggle Fullscreen Canvas">⛶ Fullscreen</button>
        </div>
    </div>

    <div class="diag-viewport" id="viewport">
        <div class="diag-canvas" id="canvas">
            <pre class="mermaid" id="erDiagramPre">
erDiagram
'@)

    $sortedTables = $tableMetadata.Values | Sort-Object Schema, TableName

    foreach ($t in $sortedTables) {
        $cName = $t.ClassName
        $extendsStr = ""
        if ($typeDict.ContainsKey($cName)) {
            $eObj = $typeDict[$cName]
            $extendsList = @()
            if ($eObj.BaseClass) { $extendsList += $eObj.BaseClass }
            $extendsList += $eObj.DirectMarkers
            if ($extendsList.Count -gt 0) { $extendsStr = $extendsList -join ', ' }
        }
        if (-not [string]::IsNullOrWhiteSpace($extendsStr)) {
            [void]$diagHtml.AppendLine("    %% Extends: $extendsStr")
        }
        [void]$diagHtml.AppendLine("    $cName {")
        [void]$diagHtml.AppendLine("    }")
    }

    foreach ($r in ($uniqueSnapshotRels | Sort-Object ParentEntity, ChildEntity)) {
        $cleanLabel = $r.Label -replace '"', ''
        [void]$diagHtml.AppendLine("    $($r.ParentEntity) $($r.Cardinality) $($r.ChildEntity) : `"$cleanLabel`"")
    }

    [void]$diagHtml.AppendLine('            </pre>')
    [void]$diagHtml.AppendLine('        </div>')
    [void]$diagHtml.AppendLine('    </div>')

    # Script to initialize Mermaid, bind Pan & Zoom, Hover Highlighting and Click Handlers
    [void]$diagHtml.AppendLine(@'
<script>
    mermaid.initialize({
        startOnLoad: true,
        theme: 'neutral',
        securityLevel: 'loose',
        er: { useMaxWidth: false }
    });

    const activeRelationships = [
'@)

    foreach ($r in ($uniqueSnapshotRels | Sort-Object ParentEntity, ChildEntity)) {
        $pEnt = $r.ParentEntity
        $cEnt = $r.ChildEntity
        $lbl = ($r.Label -replace '"', '').Trim()
        [void]$diagHtml.AppendLine("        { parent: '$pEnt', child: '$cEnt', label: '$lbl' },")
    }

    [void]$diagHtml.AppendLine(@'
    ];

    const tableToEntityMap = {
'@)

    foreach ($t in $sortedTables) {
        $tName = $t.TableName
        $cName = $t.ClassName
        [void]$diagHtml.AppendLine("        '$tName': '$cName.html',")
        [void]$diagHtml.AppendLine("        '$cName': '$cName.html',")
    }

    [void]$diagHtml.AppendLine(@'
    };

    // Pan & Zoom Engine State
    let scale = 1.0;
    let pointX = 0;
    let pointY = 0;
    let isPanning = false;
    let startX = 0;
    let startY = 0;
    let hasMoved = false;

    const viewport = document.getElementById('viewport');
    const canvas = document.getElementById('canvas');
    const zoomLevelEl = document.getElementById('zoomLevel');

    function updateTransform() {
        canvas.style.transform = `translate(${pointX}px, ${pointY}px) scale(${scale})`;
        zoomLevelEl.textContent = Math.round(scale * 100) + '%';
    }

    function zoomIn() {
        scale = Math.min(scale * 1.25, 4.0);
        updateTransform();
    }

    function zoomOut() {
        scale = Math.max(scale / 1.25, 0.15);
        updateTransform();
    }

    function resetZoom() {
        scale = 1.0;
        pointX = 40;
        pointY = 40;
        updateTransform();
    }

    function fitView() {
        const svg = canvas.querySelector('svg');
        if (!svg) return;
        const vRect = viewport.getBoundingClientRect();
        const sRect = svg.getBoundingClientRect();
        const rawW = svg.viewBox ? svg.viewBox.baseVal.width : (svg.clientWidth || sRect.width / scale);
        const rawH = svg.viewBox ? svg.viewBox.baseVal.height : (svg.clientHeight || sRect.height / scale);

        if (rawW > 0 && rawH > 0) {
            const scaleX = (vRect.width - 60) / rawW;
            const scaleY = (vRect.height - 60) / rawH;
            scale = Math.min(scaleX, scaleY, 1.2);
            pointX = Math.max((vRect.width - rawW * scale) / 2, 20);
            pointY = Math.max((vRect.height - rawH * scale) / 2, 20);
            updateTransform();
        }
    }

    function toggleFullscreen() {
        if (!document.fullscreenElement) {
            viewport.requestFullscreen().catch(err => alert(err.message));
        } else {
            document.exitFullscreen();
        }
    }

    // Mouse Pan & Drag
    viewport.addEventListener('mousedown', (e) => {
        // Only primary mouse button
        if (e.button !== 0) return;
        isPanning = true;
        startX = e.clientX - pointX;
        startY = e.clientY - pointY;
        hasMoved = false;
    });

    window.addEventListener('mousemove', (e) => {
        if (!isPanning) return;
        const dx = Math.abs(e.clientX - (startX + pointX));
        const dy = Math.abs(e.clientY - (startY + pointY));
        if (dx > 4 || dy > 4) hasMoved = true;
        pointX = e.clientX - startX;
        pointY = e.clientY - startY;
        updateTransform();
    });

    window.addEventListener('mouseup', () => {
        isPanning = false;
    });

    // Mouse Wheel Zoom centered at cursor
    viewport.addEventListener('wheel', (e) => {
        e.preventDefault();
        const rect = viewport.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;

        const zoomFactor = e.deltaY < 0 ? 1.15 : (1 / 1.15);
        const newScale = Math.min(Math.max(scale * zoomFactor, 0.15), 4.0);

        // Zoom toward cursor location
        pointX = mouseX - (mouseX - pointX) * (newScale / scale);
        pointY = mouseY - (mouseY - pointY) * (newScale / scale);
        scale = newScale;
        updateTransform();
    }, { passive: false });

    // Interactive Entity Click Handler, Hover Highlighting & Pan-Suppression
    function attachClickHandlers() {
        const svg = canvas.querySelector('svg');
        if (!svg) {
            setTimeout(attachClickHandlers, 300);
            return;
        }

        // Map entity names to SVG groups
        const entityGroups = {};
        const allEntityGs = [];
        svg.querySelectorAll('g').forEach(g => {
            const textEl = g.querySelector('text');
            if (textEl) {
                const raw = textEl.textContent.trim();
                if (tableToEntityMap[raw] && !entityGroups[raw]) {
                    entityGroups[raw] = g;
                    allEntityGs.push(g);
                }
            }
        });

        // Collect all relationship paths and labels
        const allPaths = Array.from(svg.querySelectorAll('path')).filter(p => {
            const cls = (p.getAttribute('class') || '') + ' ' + (p.getAttribute('id') || '');
            return cls.includes('relationship') || p.getAttribute('marker-start') || p.getAttribute('marker-end') || p.getAttribute('d');
        });
        const allLabels = Array.from(svg.querySelectorAll('text')).filter(t => {
            const cls = t.getAttribute('class') || '';
            return cls.includes('relationship') || cls.includes('label');
        });

        // Map relationships in order to SVG paths
        // In Mermaid ER diagrams, relationship paths are rendered sequentially matching the input statements
        const relPathMap = [];
        for (let i = 0; i < activeRelationships.length; i++) {
            const rel = activeRelationships[i];
            const p = allPaths[i] || null;
            const l = allLabels[i] || null;
            relPathMap.push({
                parent: rel.parent,
                child: rel.child,
                label: rel.label,
                pathEl: p,
                labelEl: l
            });
        }

        // Hover Highlighting Logic
        function highlightEntityRelations(entityName) {
            // Collect connected lines, labels, and partners
            const connectedPaths = [];
            const connectedLabels = [];
            const connectedPartners = new Set();

            relPathMap.forEach(item => {
                if (item.parent === entityName || item.child === entityName) {
                    if (item.pathEl) connectedPaths.push(item.pathEl);
                    if (item.labelEl) connectedLabels.push(item.labelEl);
                    if (item.parent === entityName) connectedPartners.add(item.child);
                    if (item.child === entityName) connectedPartners.add(item.parent);
                }
            });

            // Dim everything
            allEntityGs.forEach(g => g.classList.add('er-dimmed'));
            allPaths.forEach(p => p.classList.add('er-dimmed'));
            allLabels.forEach(l => l.classList.add('er-dimmed'));

            // Highlight hovered entity
            if (entityGroups[entityName]) {
                entityGroups[entityName].classList.remove('er-dimmed');
                entityGroups[entityName].classList.add('er-highlighted-box');
            }

            // Highlight connected partner entities
            connectedPartners.forEach(partnerName => {
                if (entityGroups[partnerName]) {
                    entityGroups[partnerName].classList.remove('er-dimmed');
                    entityGroups[partnerName].classList.add('er-connected-box');
                }
            });

            // Highlight connected lines & labels
            connectedPaths.forEach(p => {
                p.classList.remove('er-dimmed');
                p.classList.add('er-highlighted-line');
                // Move path to top of SVG render stack so it sits above dimmed lines
                p.parentNode.appendChild(p);
            });
            connectedLabels.forEach(l => {
                l.classList.remove('er-dimmed');
                l.classList.add('er-highlighted-label');
                l.parentNode.appendChild(l);
            });
        }

        function clearHighlights() {
            allEntityGs.forEach(g => {
                g.classList.remove('er-dimmed', 'er-highlighted-box', 'er-connected-box');
            });
            allPaths.forEach(p => {
                p.classList.remove('er-dimmed', 'er-highlighted-line');
            });
            allLabels.forEach(l => {
                l.classList.remove('er-dimmed', 'er-highlighted-label');
            });
        }

        // Attach clicks and hover events to text labels and rect containers
        svg.querySelectorAll('text, rect').forEach(el => {
            const parentG = el.closest('g');
            if (parentG) {
                const textEl = parentG.querySelector('text');
                if (textEl) {
                    const raw = textEl.textContent.trim();
                    if (tableToEntityMap[raw]) {
                        parentG.style.cursor = 'pointer';
                        parentG.setAttribute('title', 'Click to inspect ' + raw);

                        parentG.addEventListener('mouseenter', () => {
                            highlightEntityRelations(raw);
                        });
                        parentG.addEventListener('mouseleave', () => {
                            clearHighlights();
                        });

                        parentG.addEventListener('click', (e) => {
                            if (hasMoved) return; // Prevent navigation when user was dragging/panning
                            e.stopPropagation();
                            window.location.href = 'entities/' + tableToEntityMap[raw];
                        });
                    }
                }
            }
        });

        // Auto-fit on initial load after SVG renders
        setTimeout(fitView, 350);
    }

    window.addEventListener('load', () => {
        setTimeout(attachClickHandlers, 500);
    });
</script>
</div></body></html>
'@)

    Set-Content -Path (Join-Path $resolvedOutputDir "diagram.html") -Value $diagHtml.ToString() -Encoding UTF8
    # 5.4 HIERARCHY.HTML (Collapsible Entity Inheritance Tree & Slide-Over Drawer)
    Write-Host "Building Collapsible Entity Hierarchy diagram in '$(Join-Path $resolvedOutputDir 'hierarchy.html')'..." -ForegroundColor Cyan
    
    $nonInterfaces = $typeDict.Values | Where-Object { -not $_.IsInterface }
    
    $childMap = @{}
    foreach ($e in $nonInterfaces) {
        $parent = $e.BaseClass
        if (-not $parent) { $parent = "ROOT" }
        if (-not $childMap.ContainsKey($parent)) { $childMap[$parent] = [System.Collections.Generic.List[string]]::new() }
        $childMap[$parent].Add($e.Name)
    }

    function Render-HierarchyBranch($name, $childMap, $typeDict, $depth) {
        $e = $typeDict[$name]
        $children = if ($childMap.ContainsKey($name)) { $childMap[$name] | Sort-Object } else { @() }
        $hasChildren = ($children.Count -gt 0)

        $ancestors = [System.Collections.Generic.List[string]]::new()
        $curr = $e.BaseClass
        while ($curr -and $typeDict.ContainsKey($curr)) {
            $ancestors.Add($curr)
            $curr = $typeDict[$curr].BaseClass
        }

        $cardClass = "tree-card"
        if ($name -eq "BaseEntity") { $cardClass += " root-card" }
        elseif ($name -in @("AuditableEntity", "TenantAuditableEntity", "BaseTaskEntity", "BaseCalendarEntity")) { $cardClass += " base-card" }
        elseif ($e.AllMarkers.Contains("ITenantEntity")) { $cardClass += " tenant-card" }
        else { $cardClass += " domain-card" }

        # Default expansion: Top tiers open (BaseEntity, AuditableEntity, TenantAuditableEntity)
        $isCollapsed = ($depth -gt 2) -or ($depth -eq 2 -and $name -ne "TenantAuditableEntity" -and $hasChildren)
        $childClass = if ($isCollapsed) { "tree-children collapsed" } else { "tree-children" }
        $btnClass = if ($isCollapsed) { "toggle-btn collapsed" } else { "toggle-btn" }

        $sb = [System.Text.StringBuilder]::new()
        [void]$sb.AppendLine("<li class=`"tree-branch`" data-entity=`"$name`">")
        [void]$sb.AppendLine("<div class=`"tree-node-wrap`">")

        if ($hasChildren) {
            $chevronSvg = '<svg class="chevron-icon" viewBox="0 0 16 16" width="12" height="12"><path fill="currentColor" d="M3.2 5.2a.75.75 0 0 1 1.06 0L8 8.94l3.74-3.74a.75.75 0 1 1 1.06 1.06l-4.27 4.27a.75.75 0 0 1-1.06 0L3.2 6.26a.75.75 0 0 1 0-1.06z"/></svg>'
            [void]$sb.AppendLine("<button class=`"$btnClass`" onclick=`"toggleNode(this, event)`" title=`"Expand/Collapse $($children.Count) child classes`" aria-label=`"Expand or collapse $name child classes`">$chevronSvg</button>")
        } else {
            [void]$sb.AppendLine("<div class=`"toggle-spacer`"></div>")
        }

        $searchMeta = "$name $($e.Subsystem) $($e.BaseClass) $($ancestors -join ' ')".ToLower()
        [void]$sb.AppendLine("<div class=`"$cardClass`" onclick=`"openDetailDrawer('$name')`" data-search=`"$searchMeta`" title=`"Click to view $name details in slide-over drawer`">")
        [void]$sb.AppendLine("<div class=`"tree-card-header`">")
        [void]$sb.AppendLine("<span class=`"tree-entity-name`">$name</span>")
        if ($e.Subsystem) {
            [void]$sb.AppendLine("<span class=`"tree-subsystem-pill`">$($e.Subsystem)</span>")
        }
        [void]$sb.AppendLine("</div>")

        if ($e.BaseClass) {
            [void]$sb.AppendLine("<div class=`"tree-card-extends`">extends <strong>$($e.BaseClass)</strong></div>")
            if ($ancestors.Count -gt 1) {
                $ancestorLine = ($ancestors | Select-Object -Skip 1) -join " &rarr; "
                [void]$sb.AppendLine("<div class=`"tree-card-ancestors`">&rarr; $ancestorLine</div>")
            }
        }

        [void]$sb.AppendLine("</div>")
        [void]$sb.AppendLine("</div>")

        if ($hasChildren) {
            [void]$sb.AppendLine("<ul class=`"$childClass`">")
            foreach ($child in $children) {
                [void]$sb.Append((Render-HierarchyBranch $child $childMap $typeDict ($depth + 1)))
            }
            [void]$sb.AppendLine("</ul>")
        }

        [void]$sb.AppendLine("</li>")
        return $sb.ToString()
    }

    $hierHtml = [System.Text.StringBuilder]::new()
    [void]$hierHtml.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Entity Hierarchy Diagram</title><link rel="stylesheet" href="styles.css"></head><body>')
    [void]$hierHtml.AppendLine('<div class="page-container" style="max-width: 100%; padding: 20px 30px;">')
    [void]$hierHtml.AppendLine('<div class="page-header"><div><h1 class="entity-title">🌳 Entity Hierarchy &amp; Inheritance Architecture</h1><div class="breadcrumb">Clean Architecture Inheritance Tree &bull; Click any entity card to inspect details</div></div></div>')

    # Toolbar
    [void]$hierHtml.AppendLine('<div class="hierarchy-toolbar">')
    [void]$hierHtml.AppendLine('<div style="display: flex; align-items: center; gap: 10px; flex: 1; max-width: 450px;">')
    [void]$hierHtml.AppendLine('<input type="text" id="treeSearch" class="search-input" placeholder="Search entity, base class, or subsystem..." onkeyup="filterTree()" style="width: 100%;">')
    [void]$hierHtml.AppendLine('</div>')
    [void]$hierHtml.AppendLine('<div style="display: flex; align-items: center; gap: 8px;">')
    [void]$hierHtml.AppendLine('<button class="btn-action primary" onclick="expandAll()">Expand All</button>')
    [void]$hierHtml.AppendLine('<button class="btn-action" onclick="collapseAll()">Collapse All</button>')
    [void]$hierHtml.AppendLine('<button class="btn-action" onclick="resetView()">Reset View</button>')
    [void]$hierHtml.AppendLine('</div>')
    [void]$hierHtml.AppendLine('<div style="display: flex; align-items: center; gap: 12px; font-size: 11px;">')
    [void]$hierHtml.AppendLine('<span style="display: inline-flex; align-items: center; gap: 4px;"><span style="width: 10px; height: 10px; background: #faf9f8; border: 1.5px solid var(--primary); border-left: 3px solid var(--primary); border-radius: 2px;"></span> Root Base</span>')
    [void]$hierHtml.AppendLine('<span style="display: inline-flex; align-items: center; gap: 4px;"><span style="width: 10px; height: 10px; background: #fff; border: 1.5px solid #e1dfdd; border-left: 3px solid #5c2d91; border-radius: 2px;"></span> Framework Base</span>')
    [void]$hierHtml.AppendLine('<span style="display: inline-flex; align-items: center; gap: 4px;"><span style="width: 10px; height: 10px; background: #fff; border: 1.5px solid #e1dfdd; border-left: 3px solid #004578; border-radius: 2px;"></span> Tenant Isolated</span>')
    [void]$hierHtml.AppendLine('<span style="display: inline-flex; align-items: center; gap: 4px;"><span style="width: 10px; height: 10px; background: #fff; border: 1.5px solid #e1dfdd; border-left: 3px solid #107c10; border-radius: 2px;"></span> Domain Entity</span>')
    [void]$hierHtml.AppendLine('</div>')
    [void]$hierHtml.AppendLine('</div>')

    # Tree container
    [void]$hierHtml.AppendLine('<div class="tree-container">')
    [void]$hierHtml.AppendLine('<ul class="tree-root">')
    [void]$hierHtml.Append((Render-HierarchyBranch "BaseEntity" $childMap $typeDict 0))
    [void]$hierHtml.AppendLine('</ul>')
    [void]$hierHtml.AppendLine('</div>')

    # Slide-over drawer & backdrop
    [void]$hierHtml.AppendLine('<div class="drawer-backdrop" id="drawerBackdrop" onclick="closeDetailDrawer()"></div>')
    [void]$hierHtml.AppendLine('<aside class="slide-drawer" id="slideDrawer">')
    [void]$hierHtml.AppendLine('<div class="drawer-header">')
    [void]$hierHtml.AppendLine('<div class="drawer-title"><span>📋</span> <span id="drawerTitle">Entity Details</span></div>')
    [void]$hierHtml.AppendLine('<button class="drawer-close" onclick="closeDetailDrawer()" title="Close details panel">&times;</button>')
    [void]$hierHtml.AppendLine('</div>')
    [void]$hierHtml.AppendLine('<div class="drawer-body">')
    [void]$hierHtml.AppendLine('<iframe id="drawerIframe" class="drawer-iframe" src="about:blank"></iframe>')
    [void]$hierHtml.AppendLine('</div>')
    [void]$hierHtml.AppendLine('</aside>')

    # JavaScript logic
    [void]$hierHtml.AppendLine(@'
<script>
function toggleNode(btn, e) {
    if (e) { e.stopPropagation(); }
    var branch = btn.closest('.tree-branch');
    if (!branch) return;
    var childList = branch.querySelector(':scope > ul.tree-children');
    if (childList) {
        var isCollapsed = childList.classList.toggle('collapsed');
        btn.classList.toggle('collapsed', isCollapsed);
    }
}

function openDetailDrawer(entityName) {
    document.getElementById('drawerTitle').textContent = entityName + ' Details';
    document.getElementById('drawerIframe').src = 'entities/' + entityName + '.html';
    document.getElementById('slideDrawer').classList.add('open');
    document.getElementById('drawerBackdrop').classList.add('open');
}

function closeDetailDrawer() {
    document.getElementById('slideDrawer').classList.remove('open');
    document.getElementById('drawerBackdrop').classList.remove('open');
    document.getElementById('drawerIframe').src = 'about:blank';
}

function expandAll() {
    document.querySelectorAll('.tree-children').forEach(function(el) {
        el.classList.remove('collapsed');
    });
    document.querySelectorAll('.toggle-btn').forEach(function(btn) {
        btn.classList.remove('collapsed');
    });
}

function collapseAll() {
    document.querySelectorAll('.tree-children').forEach(function(el) {
        el.classList.add('collapsed');
    });
    document.querySelectorAll('.toggle-btn').forEach(function(btn) {
        btn.classList.add('collapsed');
    });
}

function resetView() {
    collapseAll();
    ['BaseEntity', 'AuditableEntity', 'TenantAuditableEntity'].forEach(function(name) {
        var branch = document.querySelector('[data-entity="' + name + '"]');
        if (branch) {
            var ul = branch.querySelector(':scope > ul.tree-children');
            if (ul) ul.classList.remove('collapsed');
            var btn = branch.querySelector(':scope > .tree-node-wrap > .toggle-btn');
            if (btn) btn.classList.remove('collapsed');
        }
    });
}

function filterTree() {
    var q = document.getElementById('treeSearch').value.toLowerCase().trim();
    var allCards = document.querySelectorAll('.tree-card');
    if (!q) {
        allCards.forEach(function(c) {
            c.style.opacity = '1';
            c.style.borderWidth = '1.5px';
        });
        return;
    }
    allCards.forEach(function(c) {
        var meta = c.getAttribute('data-search') || '';
        var match = meta.indexOf(q) !== -1;
        if (match) {
            c.style.opacity = '1';
            c.style.borderWidth = '2.5px';
            c.style.borderColor = 'var(--primary)';
            var curr = c.closest('.tree-branch');
            while (curr) {
                var parentUl = curr.closest('ul.tree-children');
                if (parentUl) {
                    parentUl.classList.remove('collapsed');
                    var parentBranch = parentUl.closest('.tree-branch');
                    if (parentBranch) {
                        var toggleBtn = parentBranch.querySelector(':scope > .tree-node-wrap > .toggle-btn');
                        if (toggleBtn) toggleBtn.classList.remove('collapsed');
                    }
                    curr = parentBranch;
                } else {
                    curr = null;
                }
            }
        } else {
            c.style.opacity = '0.35';
            c.style.borderWidth = '1px';
            c.style.borderColor = '#edebe9';
        }
    });
}

window.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeDetailDrawer();
    }
});
</script>
</div></body></html>
'@)

    Set-Content -Path (Join-Path $resolvedOutputDir "hierarchy.html") -Value $hierHtml.ToString() -Encoding UTF8

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

    # 5.5 ENTITIES/{Name}.HTML (Generates pages for all domain entities, base classes, and architectural interfaces)
    foreach ($e in $typeDict.Values) {
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
        $mappedTable = if ($classToTable.ContainsKey($e.Name)) { $classToTable[$e.Name] } else { $e.Name }
        [void]$eHtml.AppendLine("<span class=`"badge badge-tenant`" title=`"Database Table Name`">Table: $mappedTable</span>")
        if ($e.BaseClass) {
            $baseLink = if ($typeDict.ContainsKey($e.BaseClass)) { "<a class=`"badge badge-base`" href=`"$($e.BaseClass).html`">Base: $($e.BaseClass)</a>" } else { "<span class=`"badge badge-base`">Base: $($e.BaseClass)</span>" }
            [void]$eHtml.AppendLine($baseLink)
        }
        foreach ($dm in $e.DirectMarkers) {
            $ifaceLink = "<a class=`"badge badge-base`" style=`"background:#004b50;`" href=`"$dm.html`">Implements: $dm</a>"
            [void]$eHtml.AppendLine($ifaceLink)
        }
        [void]$eHtml.AppendLine('</div></div>')
        [void]$eHtml.AppendLine('<div style="display:flex; gap:8px;">')
        [void]$eHtml.AppendLine('<a href="../diagram.html" class="nav-tab active" style="display:inline-block; background:var(--primary); color:#fff; padding:6px 12px; border-radius:4px; text-decoration:none; font-weight:600; font-size:12px;">🗺️ Full ER Diagram &rarr;</a>')
        [void]$eHtml.AppendLine('</div></div>')

        # 1. Connected Database Relationships (PRIORITY FULL-WIDTH FIRST ITEM WITH PAN & ZOOM)
        [void]$eHtml.AppendLine('<div class="card" style="padding: 20px 24px;">')
        [void]$eHtml.AppendLine('<div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; flex-wrap: wrap; gap: 10px;">')
        [void]$eHtml.AppendLine('<div><div class="card-title" style="margin-bottom: 2px;">🗺️ Connected Database Relationships</div><div style="font-size: 12px; color: var(--text-secondary);">Direct relational model, extends & implements, and foreign keys &bull; Click any entity node to inspect its specification</div></div>')
        [void]$eHtml.AppendLine('<div style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap;">')
        [void]$eHtml.AppendLine('<button class="btn-action" onclick="zoomEntityIn()" title="Zoom In">➕</button>')
        [void]$eHtml.AppendLine('<button class="btn-action" onclick="zoomEntityOut()" title="Zoom Out">➖</button>')
        [void]$eHtml.AppendLine('<button class="btn-action" onclick="resetEntityZoom()" title="Reset Zoom">🔄</button>')
        [void]$eHtml.AppendLine('<a href="#sectionClassHierarchy" class="nav-tab active" style="display:inline-block; background:#edebe9; color:var(--text-primary); padding:6px 12px; border-radius:4px; text-decoration:none; font-weight:600; font-size:12px;">⬇️ View Class Hierarchy Below</a>')
        [void]$eHtml.AppendLine('</div></div>')

        [void]$eHtml.AppendLine('<script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>')
        [void]$eHtml.AppendLine('<style>')
        [void]$eHtml.AppendLine('.diag-fullview { position: relative; width: 100%; background: #ffffff; border: 1px solid var(--border); border-radius: 6px; padding: 24px 20px 48px 20px; box-sizing: border-box; overflow: auto; min-height: 520px; display: flex; justify-content: center; align-items: flex-start; }')
        [void]$eHtml.AppendLine('.diag-fullview pre.mermaid { width: 100%; text-align: center; }')
        [void]$eHtml.AppendLine('.diag-fullview svg { max-width: none !important; height: auto !important; min-width: 400px; overflow: visible; display: inline-block; transform-origin: top center; transition: transform 0.15s ease-out; }')
        [void]$eHtml.AppendLine('g.entityBox, g[id*="entity-"], .er.entityBox, g.classGroup { cursor: pointer !important; transition: all 0.2s; }')
        [void]$eHtml.AppendLine('g.entityBox:hover, g[id*="entity-"]:hover, g.classGroup:hover { filter: drop-shadow(0 0 6px var(--primary)); opacity: 0.95; }')
        [void]$eHtml.AppendLine('.er-dimmed { opacity: 0.15 !important; transition: opacity 0.2s ease-in-out; }')
        [void]$eHtml.AppendLine('.er-highlighted-box { filter: drop-shadow(0 0 10px rgba(0, 120, 212, 0.95)) !important; opacity: 1 !important; }')
        [void]$eHtml.AppendLine('.er-connected-box { filter: drop-shadow(0 0 8px rgba(16, 124, 16, 0.9)) !important; opacity: 1 !important; }')
        [void]$eHtml.AppendLine('.er-highlighted-line { stroke: #0078d4 !important; stroke-width: 3.5px !important; stroke-opacity: 1 !important; filter: drop-shadow(0 0 5px rgba(0, 120, 212, 0.8)) !important; opacity: 1 !important; }')
        [void]$eHtml.AppendLine('.er-highlighted-label { fill: #004578 !important; font-weight: 700 !important; font-size: 13px !important; opacity: 1 !important; }')
        [void]$eHtml.AppendLine('</style>')

        # Database Relational Model Diagram Canvas
        [void]$eHtml.AppendLine('<div class="diag-fullview" id="entityDiagViewport">')
        [void]$eHtml.AppendLine('<pre class="mermaid" id="erDiagramPre">')
        [void]$eHtml.AppendLine('erDiagram')

        $thisEntity = $e.Name
        $relevantRels = $uniqueSnapshotRels | Where-Object {
            $_.ParentEntity -eq $thisEntity -or $_.ChildEntity -eq $thisEntity
        }

        $relEntities = [System.Collections.Generic.HashSet[string]]::new()
        [void]$relEntities.Add($thisEntity)
        foreach ($r in $relevantRels) {
            [void]$relEntities.Add($r.ParentEntity)
            [void]$relEntities.Add($r.ChildEntity)
        }

        # Add direct inheritance nodes (BaseClass and DirectMarkers)
        if ($e.BaseClass) {
            [void]$relEntities.Add($e.BaseClass)
        }
        foreach ($dm in $e.DirectMarkers) {
            [void]$relEntities.Add($dm)
        }

        foreach ($entName in ($relEntities | Sort-Object)) {
            [void]$eHtml.AppendLine("    $entName {")
            [void]$eHtml.AppendLine("    }")
        }

        # Render inheritance relationships
        if ($e.BaseClass) {
            [void]$eHtml.AppendLine("    $($e.BaseClass) ||--|| $thisEntity : `"extends`"")
        }
        foreach ($dm in $e.DirectMarkers) {
            [void]$eHtml.AppendLine("    $dm ||--o{ $thisEntity : `"implements`"")
        }

        if ($relevantRels.Count -eq 0 -and -not $e.BaseClass -and $e.DirectMarkers.Count -eq 0) {
            [void]$eHtml.AppendLine("    %% Standalone table or indirect relationships")
        } else {
            foreach ($r in ($relevantRels | Sort-Object ParentEntity, ChildEntity)) {
                $cleanLabel = $r.Label -replace '"', ''
                [void]$eHtml.AppendLine("    $($r.ParentEntity) $($r.Cardinality) $($r.ChildEntity) : `"$cleanLabel`"")
            }
        }

        [void]$eHtml.AppendLine('</pre></div>')
        [void]$eHtml.AppendLine('</div>') # end card
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

        # Collect declared + inherited properties
        $allEntityProps = [System.Collections.Generic.List[PSObject]]::new()
        $seenPropNames = [System.Collections.Generic.HashSet[string]]::new()

        # 1. Declared properties
        foreach ($p in $e.Properties) {
            if (-not $seenPropNames.Contains($p.Name)) {
                [void]$seenPropNames.Add($p.Name)
                $allEntityProps.Add($p)
            }
        }

        # 2. Inherited properties from Base Class hierarchy
        $currBaseName = $e.BaseClass
        while ($currBaseName -and $typeDict.ContainsKey($currBaseName)) {
            $baseObj = $typeDict[$currBaseName]
            foreach ($bp in $baseObj.Properties) {
                if (-not $seenPropNames.Contains($bp.Name)) {
                    [void]$seenPropNames.Add($bp.Name)
                    $inheritedProp = [PSCustomObject]@{
                        Name         = $bp.Name
                        Type         = $bp.Type
                        CleanType    = $bp.CleanType
                        IsNullable   = $bp.IsNullable
                        Description  = $bp.Description
                        IsForeignKey = $bp.IsForeignKey
                        IsNavigation = $bp.IsNavigation
                        IsCollection = $bp.IsCollection
                        TargetEntity = $bp.TargetEntity
                        Origin       = $baseObj.Name
                    }
                    $allEntityProps.Add($inheritedProp)
                }
            }
            $currBaseName = $baseObj.BaseClass
        }

        # 3. Inherited properties from Direct Interface contracts
        foreach ($dmName in $e.DirectMarkers) {
            if ($typeDict.ContainsKey($dmName)) {
                $ifaceObj = $typeDict[$dmName]
                foreach ($ip in $ifaceObj.Properties) {
                    if (-not $seenPropNames.Contains($ip.Name)) {
                        [void]$seenPropNames.Add($ip.Name)
                        $inheritedProp = [PSCustomObject]@{
                            Name         = $ip.Name
                            Type         = $ip.Type
                            CleanType    = $ip.CleanType
                            IsNullable   = $ip.IsNullable
                            Description  = $ip.Description
                            IsForeignKey = $ip.IsForeignKey
                            IsNavigation = $ip.IsNavigation
                            IsCollection = $ip.IsCollection
                            TargetEntity = $ip.TargetEntity
                            Origin       = $ifaceObj.Name
                        }
                        $allEntityProps.Add($inheritedProp)
                    }
                }
            }
        }

        # Properties Table
        [void]$eHtml.AppendLine('<div class="card">')
        [void]$eHtml.AppendLine("<div class=`"card-title`">Properties & Fields ($($allEntityProps.Count))</div>")
        if ($allEntityProps.Count -gt 0) {
            [void]$eHtml.AppendLine('<table><thead><tr><th>Property</th><th>Type</th><th>Nullable</th><th>Origin / Inheritance</th><th>Description</th></tr></thead><tbody>')
            foreach ($p in $allEntityProps) {
                $pBadge = ""
                if ($p.IsForeignKey) { $pBadge = "<span class=`"fk-pill`">FK</span>" }
                elseif ($p.IsNavigation) { $pBadge = "<span class=`"fk-pill`">NAV</span>" }
                elseif ($p.IsCollection) { $pBadge = "<span class=`"fk-pill`">1:N</span>" }

                $typeDisplay = $p.Type
                if ($typeDict.ContainsKey($p.CleanType)) {
                    $typeDisplay = "<a href=`"$($p.CleanType).html`" class=`"code-type`">$($p.Type)</a>"
                } else {
                    $typeDisplay = "<span class=`"code-type`">$($p.Type)</span>"
                }

                $originDisplay = if ($p.Origin -eq $e.Name) {
                    "<span class=`"badge badge-tenant`" style=`"font-size:10px; background:#107c10;`">Declared</span>"
                } else {
                    "<a href=`"$($p.Origin).html`" class=`"badge badge-base`" style=`"font-size:10px;`">from $($p.Origin)</a>"
                }

                $nullStr = if ($p.IsNullable) { "Yes" } else { "No" }
                $descStr = if ($p.Description) { $p.Description } else { "-" }

                [void]$eHtml.AppendLine("<tr><td class=`"prop-name`">$($p.Name) $pBadge</td><td>$typeDisplay</td><td>$nullStr</td><td>$originDisplay</td><td>$descStr</td></tr>")
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

                # 2. Class & Interface Hierarchy Card (BOTTOM OF PAGE)
        [void]$eHtml.AppendLine('<div class="card" id="sectionClassHierarchy" style="padding: 20px 24px;">')
        [void]$eHtml.AppendLine('<div class="card-title">📐 Class &amp; Interface Hierarchy (Object-Oriented Architecture)</div>')
        [void]$eHtml.AppendLine('<div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 14px;">Inheritance tree, abstract base classes, and implemented interface contracts &bull; Click any node to inspect its specification</div>')
        [void]$eHtml.AppendLine('<div class="diag-fullview">')
        [void]$eHtml.AppendLine('<pre class="mermaid" id="classDiagramPre">')
        [void]$eHtml.AppendLine('classDiagram')

        # Ancestry resolution
        $nodesToRender = [System.Collections.Generic.HashSet[string]]::new()
        $inheritanceLinks = [System.Collections.Generic.List[string]]::new()
        $queue = [System.Collections.Generic.Queue[string]]::new()
        $queue.Enqueue($e.Name)
        [void]$nodesToRender.Add($e.Name)

        while ($queue.Count -gt 0) {
            $curr = $queue.Dequeue()
            if ($typeDict.ContainsKey($curr)) {
                $currObj = $typeDict[$curr]
                $parents = @()
                if ($currObj.BaseClass) { $parents += $currObj.BaseClass }
                $parents += $currObj.DirectMarkers

                foreach ($parent in $parents) {
                    $isParentInterface = ($parent -like "I[A-Z]*" -or ($typeDict.ContainsKey($parent) -and $typeDict[$parent].IsInterface))
                    $isCurrInterface = ($curr -like "I[A-Z]*" -or ($typeDict.ContainsKey($curr) -and $typeDict[$curr].IsInterface))

                    if ($isParentInterface -and -not $isCurrInterface) {
                        $inheritanceLinks.Add("    $parent <|.. $curr : implements")
                    } else {
                        $inheritanceLinks.Add("    $parent <|-- $curr : extends")
                    }

                    if (-not $nodesToRender.Contains($parent)) {
                        [void]$nodesToRender.Add($parent)
                        $queue.Enqueue($parent)
                    }
                }
            }
        }

        foreach ($node in ($nodesToRender | Sort-Object)) {
            $isIface = ($node -like "I[A-Z]*" -or ($typeDict.ContainsKey($node) -and $typeDict[$node].IsInterface))
            $isAbs = ($typeDict.ContainsKey($node) -and $typeDict[$node].IsAbstract)
            [void]$eHtml.AppendLine("    class $node {")
            if ($isIface) { [void]$eHtml.AppendLine("        <<interface>>") }
            elseif ($isAbs) { [void]$eHtml.AppendLine("        <<abstract>>") }
            [void]$eHtml.AppendLine("    }")
        }

        foreach ($link in ($inheritanceLinks | Sort-Object -Unique)) {
            [void]$eHtml.AppendLine($link)
        }
        [void]$eHtml.AppendLine('</pre></div>')

        # Interactive Safe Click and Hover Script
        [void]$eHtml.AppendLine('<script>')
        [void]$eHtml.AppendLine('mermaid.initialize({ startOnLoad: true, theme: "neutral", securityLevel: "loose", er: { useMaxWidth: false } });')
        
        # Serialize active relationships for this entity diagram
        [void]$eHtml.AppendLine('const activeEntityRels = [')
        if ($e.BaseClass) {
            [void]$eHtml.AppendLine("    { parent: '$($e.BaseClass)', child: '$thisEntity', label: 'extends' },")
        }
        foreach ($dm in $e.DirectMarkers) {
            [void]$eHtml.AppendLine("    { parent: '$dm', child: '$thisEntity', label: 'implements' },")
        }
        foreach ($r in ($relevantRels | Sort-Object ParentEntity, ChildEntity)) {
            $pEnt = $r.ParentEntity
            $cEnt = $r.ChildEntity
            $lbl = ($r.Label -replace '"', '').Trim()
            [void]$eHtml.AppendLine("    { parent: '$pEnt', child: '$cEnt', label: '$lbl' },")
        }
        [void]$eHtml.AppendLine('];')

        [void]$eHtml.AppendLine('const entityLookup = {')
        
        foreach ($t in $tableMetadata.Values) {
            $tName = $t.TableName
            $cName = $t.ClassName
            [void]$eHtml.AppendLine("    '$tName': '$cName.html',")
            [void]$eHtml.AppendLine("    '$cName': '$cName.html',")
        }
        foreach ($td in $typeDict.Values) {
            $n = $td.Name
            [void]$eHtml.AppendLine("    '$n': '$n.html',")
        }

        $jsSnippet = @'
};

let entityDiagScale = 1.0;
function zoomEntityIn() {
    const svg = document.querySelector('#erDiagramPre svg');
    if (svg) {
        entityDiagScale = Math.min(entityDiagScale * 1.2, 3.0);
        svg.style.transform = `scale(${entityDiagScale})`;
    }
}
function zoomEntityOut() {
    const svg = document.querySelector('#erDiagramPre svg');
    if (svg) {
        entityDiagScale = Math.max(entityDiagScale / 1.2, 0.3);
        svg.style.transform = `scale(${entityDiagScale})`;
    }
}
function resetEntityZoom() {
    const svg = document.querySelector('#erDiagramPre svg');
    if (svg) {
        entityDiagScale = 1.0;
        svg.style.transform = 'none';
    }
}

function bindDetailClicks() {
    const svg = document.querySelector('#erDiagramPre svg');
    if (!svg) {
        setTimeout(bindDetailClicks, 300);
        return;
    }

    const entityGs = {};
    const allGs = [];
    svg.querySelectorAll('g').forEach(g => {
        const t = g.querySelector('text');
        if (t) {
            const raw = t.textContent.trim();
            if (entityLookup[raw] && !entityGs[raw]) {
                entityGs[raw] = g;
                allGs.push(g);
            }
        }
    });

    const allPaths = Array.from(svg.querySelectorAll('path')).filter(p => {
        const cls = (p.getAttribute('class') || '') + ' ' + (p.getAttribute('id') || '');
        return cls.includes('relationship') || p.getAttribute('marker-start') || p.getAttribute('marker-end') || p.getAttribute('d');
    });
    const allLabels = Array.from(svg.querySelectorAll('text')).filter(t => {
        const cls = t.getAttribute('class') || '';
        return cls.includes('relationship') || cls.includes('label');
    });

    const entityRels = (typeof activeEntityRels !== 'undefined') ? activeEntityRels : [];
    const relMap = [];
    for (let i = 0; i < entityRels.length; i++) {
        relMap.push({
            parent: entityRels[i].parent,
            child: entityRels[i].child,
            pathEl: allPaths[i] || null,
            labelEl: allLabels[i] || null
        });
    }

    function highlightRelations(entName) {
        const connPaths = [];
        const connLabels = [];
        const connPartners = new Set();

        relMap.forEach(r => {
            if (r.parent === entName || r.child === entName) {
                if (r.pathEl) connPaths.push(r.pathEl);
                if (r.labelEl) connLabels.push(r.labelEl);
                if (r.parent === entName) connPartners.add(r.child);
                if (r.child === entName) connPartners.add(r.parent);
            }
        });

        allGs.forEach(g => g.classList.add('er-dimmed'));
        allPaths.forEach(p => p.classList.add('er-dimmed'));
        allLabels.forEach(l => l.classList.add('er-dimmed'));

        if (entityGs[entName]) {
            entityGs[entName].classList.remove('er-dimmed');
            entityGs[entName].classList.add('er-highlighted-box');
        }
        connPartners.forEach(pName => {
            if (entityGs[pName]) {
                entityGs[pName].classList.remove('er-dimmed');
                entityGs[pName].classList.add('er-connected-box');
            }
        });
        connPaths.forEach(p => {
            p.classList.remove('er-dimmed');
            p.classList.add('er-highlighted-line');
            p.parentNode.appendChild(p);
        });
        connLabels.forEach(l => {
            l.classList.remove('er-dimmed');
            l.classList.add('er-highlighted-label');
            l.parentNode.appendChild(l);
        });
    }

    function clearRelHighlights() {
        allGs.forEach(g => g.classList.remove('er-dimmed', 'er-highlighted-box', 'er-connected-box'));
        allPaths.forEach(p => p.classList.remove('er-dimmed', 'er-highlighted-line'));
        allLabels.forEach(l => l.classList.remove('er-dimmed', 'er-highlighted-label'));
    }

    document.querySelectorAll('.mermaid svg text, .mermaid svg rect').forEach(el => {
        let parentG = el.closest('g');
        if (parentG) {
            let text = parentG.querySelector('text');
            if (text) {
                const raw = text.textContent.trim();
                if (entityLookup[raw]) {
                    parentG.style.cursor = 'pointer';
                    parentG.setAttribute('title', 'Click to inspect ' + raw);

                    parentG.onmouseenter = () => highlightRelations(raw);
                    parentG.onmouseleave = () => clearRelHighlights();

                    parentG.onclick = (e) => {
                        e.stopPropagation();
                        window.location.href = entityLookup[raw];
                    };
                }
            }
        }
    });
}

window.addEventListener('load', () => {
    setTimeout(bindDetailClicks, 600);
});
</script>
</div>
'@
        [void]$eHtml.AppendLine($jsSnippet)

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
    <title>BlazorEnterprise Data Dictionary</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body style="overflow: hidden;">
    <header class="top-header">
        <div class="brand">
            <span class="brand-badge">Enterprise Edition</span>
            <span>BlazorEnterprise Data Dictionary</span>
        </div>
        <nav class="nav-tabs">
            <a href="overview.html" target="contentFrame" class="nav-tab active" onclick="switchNav(this)">📊 Data Model</a>
            <a href="diagram.html" target="contentFrame" class="nav-tab" onclick="switchNav(this)">🗺️ Visual ER Diagram</a>
            <a href="hierarchy.html" target="contentFrame" class="nav-tab" onclick="switchNav(this)">🌳 Entity Hierarchy</a>
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

# --- 6. GENERATE MARKDOWN SPECIFICATION ---------------------------------------
if ($Format -eq "Markdown" -or $Format -eq "All") {
    Write-Host "Updating Markdown specification in '$resolvedMarkdown'..." -ForegroundColor Cyan

    $doc = [System.Text.StringBuilder]::new()
    [void]$doc.AppendLine("# Data Model & Entity Hierarchy Reference (Data Dictionary)")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("> [!NOTE]")
    [void]$doc.AppendLine("> **Automated Specification**: This document is generated from source code by `scripts/Generate-DataModelDocs.ps1`.")
    [void]$doc.AppendLine("> An interactive HTML portal version is available at [`docs/data-dictionary/index.html`](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/data-dictionary/index.html).")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("---")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("## 1. Architectural Foundation: The 3-Tier Entity Model")
    [void]$doc.AppendLine("")
    [void]$doc.AppendLine("BlazorEnterprise enforces strict zero-trust multi-tenancy and audit compliance via a 3-tier entity taxonomy:")
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
