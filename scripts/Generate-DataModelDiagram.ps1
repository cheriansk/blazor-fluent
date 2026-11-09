<#
.SYNOPSIS
    Data model diagram generator forwarder.
    Invokes the unified documentation generator in Generate-DataModelDocs.ps1.
#>
[CmdletBinding()]
param(
    [string]$EntityName = "",
    [string]$DomainPath = "src/BlazorEnterprise.Core/Domain",
    [string]$ModelSnapshotPath = "src/BlazorEnterprise.Persistence/Migrations/AppDbContextModelSnapshot.cs",
    [string]$OutputPath = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$docScript = Join-Path $scriptDir "Generate-DataModelDocs.ps1"

Write-Host "==> Invoking unified Data Model generator: $docScript" -ForegroundColor Cyan
& pwsh -NoProfile -File $docScript -DomainPath $DomainPath -ModelSnapshotPath $ModelSnapshotPath
