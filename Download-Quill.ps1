# Download-Quill.ps1
# Downloads Quill 2.0.2 JS and Snow CSS assets locally into wwwroot/lib/quill/
# for self-hosted, air-gapped, zero-CDN usage.

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

$targetDir = Join-Path $ScriptRoot "wwwroot\lib\quill"

if (-not (Test-Path $targetDir)) {
    Write-Host "Creating directory: $targetDir" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$jsUrl  = "https://cdnjs.cloudflare.com/ajax/libs/quill/2.0.2/quill.js"
$cssUrl = "https://cdnjs.cloudflare.com/ajax/libs/quill/2.0.2/quill.snow.css"

Write-Host "Downloading Quill 2.0.2 assets..." -ForegroundColor Cyan

Invoke-WebRequest -Uri $jsUrl -OutFile (Join-Path $targetDir "quill.js")
Write-Host "  Downloaded: wwwroot/lib/quill/quill.js" -ForegroundColor Green

Invoke-WebRequest -Uri $cssUrl -OutFile (Join-Path $targetDir "quill.snow.css")
Write-Host "  Downloaded: wwwroot/lib/quill/quill.snow.css" -ForegroundColor Green

Write-Host "Done! Quill is now hosted locally in wwwroot/lib/quill/ with zero external dependencies." -ForegroundColor Green
