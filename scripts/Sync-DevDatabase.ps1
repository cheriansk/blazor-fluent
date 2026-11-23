# [NEED-HUMAN-REVIEW]
<#
.SYNOPSIS
    Exports or imports a full PostgreSQL development database snapshot.
.EXAMPLE
    pwsh scripts/Sync-DevDatabase.ps1 -Export -FileName dev-snapshot.dump
    pwsh scripts/Sync-DevDatabase.ps1 -Import -FileName dev-snapshot.dump
#>
param(
    [switch]$Export,
    [switch]$Import,
    [Parameter(Mandatory = $true)]
    [string]$FileName,
    [string]$DbHost = "localhost",
    [int]$Port = 5432,
    [string]$Username = "postgres",
    [string]$Database = "blazorenterprise",
    [string]$DockerContainer = ""
)

$ErrorActionPreference = 'Stop'

if ($Export -and $Import) {
    throw "Specify either -Export or -Import, not both."
}
if (-not $Export -and -not $Import) {
    throw "Specify either -Export or -Import."
}

$resolvedPath = [System.IO.Path]::GetFullPath($FileName)

if ($Export) {
    Write-Host "Exporting database '$Database' to '$resolvedPath'..." -ForegroundColor Cyan
    if (-not [string]::IsNullOrWhiteSpace($DockerContainer)) {
        docker exec $DockerContainer pg_dump -U $Username -Fc --no-owner --no-acl $Database | Set-Content -Path $resolvedPath -AsByteStream
    } else {
        pg_dump --format=custom --no-owner --no-acl -h $DbHost -p $Port -U $Username -d $Database -f $resolvedPath
    }
    Write-Host "Export completed: $resolvedPath" -ForegroundColor Green
}
elseif ($Import) {
    if (-not (Test-Path $resolvedPath)) {
        throw "Dump file not found: $resolvedPath"
    }
    Write-Host "Importing '$resolvedPath' into database '$Database'..." -ForegroundColor Cyan
    if (-not [string]::IsNullOrWhiteSpace($DockerContainer)) {
        Get-Content -Path $resolvedPath -AsByteStream -Raw | docker exec -i $DockerContainer pg_restore --clean --if-exists --no-owner --no-acl -U $Username -d $Database
    } else {
        pg_restore --clean --if-exists --no-owner --no-acl -h $DbHost -p $Port -U $Username -d $Database $resolvedPath
    }
    Write-Host "Import completed from: $resolvedPath" -ForegroundColor Green
}
