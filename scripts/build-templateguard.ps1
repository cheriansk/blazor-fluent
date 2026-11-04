<#
.SYNOPSIS
    Authoring tool: Rebuilds the TemplateGuard Roslyn Governance NuGet package.
    Automatically increments the patch version (e.g. 1.0.0 -> 1.0.1) when base files
    or analyzer source code have changed, and skips if already up to date.
#>

param (
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "==> Checking TemplateGuard governance baseline..." -ForegroundColor Cyan

$repoRoot      = Resolve-Path "$PSScriptRoot\.."
$outputDir     = Join-Path $repoRoot "tools\TemplateGuard"
$projectPath   = Join-Path $repoRoot "tools\src\TemplateGuard.Analyzers\TemplateGuard.Analyzers.csproj"
$toolProject   = Join-Path $repoRoot "tools\src\TemplateGuard.Tool\TemplateGuard.Tool.csproj"
$manifestPath  = Join-Path $outputDir ".basefiles.json"
$propsPath     = Join-Path $repoRoot "Directory.Packages.props"

if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 1. Snapshot previous manifest hash if it exists
$oldManifestHash = ""
if (Test-Path $manifestPath) {
    $oldManifestHash = (Get-FileHash -Path $manifestPath -Algorithm SHA256).Hash
    Set-ItemProperty -Path $manifestPath -Name IsReadOnly -Value $false
}

# 2. Run TemplateGuard.Tool to generate the latest manifest
Write-Host "==> Indexing base files with TemplateGuard.Tool..." -ForegroundColor Cyan
dotnet run --project $toolProject -- $repoRoot
if ($LASTEXITCODE -ne 0) { throw "Base manifest indexing failed." }

$newManifestHash = ""
if (Test-Path $manifestPath) {
    $newManifestHash = (Get-FileHash -Path $manifestPath -Algorithm SHA256).Hash
}

# 3. Determine if changes occurred
$manifestChanged = ($oldManifestHash -ne $newManifestHash)
$existingPackages = Get-ChildItem -Path $outputDir -Filter "BlazorEnterprise.TemplateGuard.*.nupkg" -File

if (-not $manifestChanged -and ($existingPackages.Count -gt 0) -and -not $Force) {
    Write-Host "==> [UP TO DATE] TemplateGuard base manifest and package are unchanged. Skipping rebuild." -ForegroundColor Green
    exit 0
}

Write-Host "==> Base file modifications or analyzer updates detected. Incrementing patch version..." -ForegroundColor Yellow

# 4. Read current version from TemplateGuard.Analyzers.csproj
$csprojContent = Get-Content -Path $projectPath -Raw -Encoding utf8
if ($csprojContent -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3] + 1
    $newVersion = "$major.$minor.$patch"
} else {
    $newVersion = "1.0.1"
}

Write-Host "==> Bumping version to $newVersion..." -ForegroundColor Cyan

# Update csproj version
$csprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
Set-Content -Path $projectPath -Value $csprojContent -NoNewline -Encoding utf8

# Update Directory.Packages.props version
if (Test-Path $propsPath) {
    $propsContent = Get-Content -Path $propsPath -Raw -Encoding utf8
    $propsContent = $propsContent -replace '<PackageVersion Include="BlazorEnterprise\.TemplateGuard" Version="[^"]*" />', "<PackageVersion Include=`"BlazorEnterprise.TemplateGuard`" Version=`"$newVersion`" />"
    Set-Content -Path $propsPath -Value $propsContent -NoNewline -Encoding utf8
    Write-Host "  Updated Directory.Packages.props to $newVersion" -ForegroundColor Green
}

# 5. Clean stale packages in tools/TemplateGuard
foreach ($pkg in $existingPackages) {
    Remove-Item -Path $pkg.FullName -Force -ErrorAction SilentlyContinue
}

# 6. Build analyzer and pack NuGet package
Write-Host "==> Building and packing BlazorEnterprise.TemplateGuard.$newVersion.nupkg..." -ForegroundColor Cyan
dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Analyzer build and packaging failed." }

# 7. Clean up loose dll/pdb from tools/TemplateGuard (now packaged inside .nupkg)
$staleBinaries = @(
    (Join-Path $outputDir "TemplateGuard.Analyzers.dll"),
    (Join-Path $outputDir "TemplateGuard.Analyzers.pdb"),
    (Join-Path $outputDir "TemplateGuard.Analyzers.deps.json")
)
foreach ($bin in $staleBinaries) {
    if (Test-Path $bin) { Remove-Item -Path $bin -Force -ErrorAction SilentlyContinue }
}

Write-Host "==> Successfully created tools/TemplateGuard/BlazorEnterprise.TemplateGuard.$newVersion.nupkg!" -ForegroundColor Green
Write-Host "==> Cleaned up loose binaries. Commit tools/TemplateGuard/*.nupkg and .basefiles.json." -ForegroundColor Green
