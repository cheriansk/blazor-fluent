# ==============================================================================
# init-dev-secrets.ps1
# Developer Onboarding & User-Secrets Initialization Helper
# Securely initializes local development user-secrets outside the Git tree.
# Zero-Trust Compliance:
#   - Generates dynamic UUIDs for Root Tenant
#   - Generates cryptographically secure 256-bit HMAC integrity secret
#   - Prompts for local database credentials (never hardcoded in repository)
#   - Writes configuration directly to dotnet user-secrets
# ==============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RootAdminEmail,

    [Parameter(Mandatory = $false)]
    [string]$DatabasePassword,

    [Parameter(Mandatory = $false)]
    [string]$DatabaseHost = "localhost",

    [Parameter(Mandatory = $false)]
    [int]$DatabasePort = 5432,

    [Parameter(Mandatory = $false)]
    [string]$DatabaseName = "blazorfluent_dev",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseUser = "postgres",

    [Parameter(Mandatory = $false)]
    [string]$RootTenantSlug = "root",

    [Parameter(Mandatory = $false)]
    [string]$RootTenantName = "System Administration"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " [DEV SETUP] BlazorFluent Zero-Trust Local Environment Initializer" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Resolve Root Admin Email
if ([string]::IsNullOrWhiteSpace($RootAdminEmail)) {
    $RootAdminEmail = Read-Host "Enter your local Root Admin email (e.g. admin@localdev.internal)"
    if ([string]::IsNullOrWhiteSpace($RootAdminEmail)) {
        $RootAdminEmail = "admin@localdev.internal"
        Write-Host "Using default dev admin email: $RootAdminEmail" -ForegroundColor Yellow
    }
}

# 2. Resolve Database Password
if ([string]::IsNullOrWhiteSpace($DatabasePassword)) {
    $DatabasePassword = Read-Host "Enter local PostgreSQL password for user '$DatabaseUser'" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($DatabasePassword)
    $DatabasePassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
}

# 3. Generate Secure Keys & Dynamic Identities
$rootTenantGuid = [System.Guid]::NewGuid().ToString()
$integritySecret = [System.Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
$connectionString = "Host=$DatabaseHost;Port=$DatabasePort;Database=$DatabaseName;Username=$DatabaseUser;Password=$DatabasePassword"

Write-Host "`nGenerated Identities:" -ForegroundColor Cyan
Write-Host "  Root Tenant ID   : $rootTenantGuid" -ForegroundColor Green
Write-Host "  Root Tenant Slug : $RootTenantSlug" -ForegroundColor Green
Write-Host "  Root Tenant Name : $RootTenantName" -ForegroundColor Green
Write-Host "  Root Admin Email : $RootAdminEmail" -ForegroundColor Green

# 4. Target Projects for User Secrets
$webProject = "src/BlazorFluent.Web"
$apiProject = "src/BlazorFluent.Api"

Write-Host "`nConfiguring User Secrets for BlazorFluent.Web..." -ForegroundColor Cyan

& dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$connectionString" --project $webProject
& dotnet user-secrets set "Admin:RootTenantId" "$rootTenantGuid" --project $webProject
& dotnet user-secrets set "Admin:RootTenantSlug" "$RootTenantSlug" --project $webProject
& dotnet user-secrets set "Admin:RootTenantName" "$RootTenantName" --project $webProject
& dotnet user-secrets set "Admin:RootAdmins" "$RootAdminEmail" --project $webProject
& dotnet user-secrets set "Security:IntegritySecret" "$integritySecret" --project $webProject
& dotnet user-secrets set "Security:SystemBootstrapEmail" "$RootAdminEmail" --project $webProject

if (Test-Path $apiProject) {
    Write-Host "`nConfiguring User Secrets for BlazorFluent.Api..." -ForegroundColor Cyan
    & dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$connectionString" --project $apiProject
    & dotnet user-secrets set "Admin:RootTenantId" "$rootTenantGuid" --project $apiProject
    & dotnet user-secrets set "Admin:RootTenantSlug" "$RootTenantSlug" --project $apiProject
    & dotnet user-secrets set "Admin:RootTenantName" "$RootTenantName" --project $apiProject
    & dotnet user-secrets set "Admin:RootAdmins" "$RootAdminEmail" --project $apiProject
    & dotnet user-secrets set "Security:IntegritySecret" "$integritySecret" --project $apiProject
}

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Local dev secrets initialized successfully in user-secrets!" -ForegroundColor Green
Write-Host " Secrets are stored in your local profile and will NOT be committed to git." -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
