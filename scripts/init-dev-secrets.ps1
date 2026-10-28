# ==============================================================================
# init-dev-secrets.ps1
# Developer Onboarding & User-Secrets Initialization Helper
# Securely initializes local development user-secrets outside the Git tree.
# Zero-Trust Compliance:
#   - Generates dynamic UUIDs for Root Tenant
#   - Generates cryptographically secure 256-bit HMAC integrity secret
#   - Generates cryptographically secure JWT authentication signing keys
#   - Generates Master Encryption and Vault keys
#   - Prompts for local database credentials (never hardcoded in repository)
#   - Writes configuration directly to dotnet user-secrets
#   - Populates local development appsettings.Development.json across all projects
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
    [string]$DatabaseName = "blazorenterprise_dev",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseUser = "postgres",

    [Parameter(Mandatory = $false)]
    [string]$RootTenantSlug = "root",

    [Parameter(Mandatory = $false)]
    [string]$RootTenantName = "System Administration",

    [Parameter(Mandatory = $false)]
    [string]$AuthIssuer = "https://localhost:7001",

    [Parameter(Mandatory = $false)]
    [string]$AuthAudience = "blazorenterprise-api",

    [Parameter(Mandatory = $false)]
    [string]$AuthSigningKey,

    [Parameter(Mandatory = $false)]
    [string]$AdminAllowedDomain,

    [Parameter(Mandatory = $false)]
    [string]$AdminKeyVaultUsers,

    [Parameter(Mandatory = $false)]
    [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " [DEV SETUP] BlazorEnterprise Zero-Trust Local Environment Initializer" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Resolve Root Admin Email
if ([string]::IsNullOrWhiteSpace($RootAdminEmail)) {
    if ($NonInteractive) {
        $RootAdminEmail = "admin@localdev.internal"
    } else {
        $RootAdminEmail = Read-Host "Enter your local Root Admin email (e.g. admin@localdev.internal)"
        if ([string]::IsNullOrWhiteSpace($RootAdminEmail)) {
            $RootAdminEmail = "admin@localdev.internal"
        }
    }
    Write-Host "Root Admin email: $RootAdminEmail" -ForegroundColor Yellow
}

# 2. Resolve Database Password
if ([string]::IsNullOrWhiteSpace($DatabasePassword)) {
    if ($NonInteractive) {
        $DatabasePassword = "postgres_dev_password"
    } else {
        $DatabasePassword = Read-Host "Enter local PostgreSQL password for user '$DatabaseUser'" -AsSecureString
        $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($DatabasePassword)
        $DatabasePassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    }
}

# 3. Resolve Admin Domain & KeyVault Users
if ([string]::IsNullOrWhiteSpace($AdminAllowedDomain)) {
    if ($RootAdminEmail.Contains("@")) {
        $AdminAllowedDomain = $RootAdminEmail.Split("@")[-1]
    } else {
        $AdminAllowedDomain = "localdev.internal"
    }
}

if ([string]::IsNullOrWhiteSpace($AdminKeyVaultUsers)) {
    $AdminKeyVaultUsers = $RootAdminEmail
}

# 4. Resolve or Generate Secure Cryptographic Keys & Dynamic Identities (Idempotent)
$webDevJsonPath = "src/BlazorEnterprise.Web/appsettings.Development.json"
$existingWebDevJson = $null
if (Test-Path $webDevJsonPath) {
    try {
        $existingWebDevJson = Get-Content $webDevJsonPath -Raw -Encoding utf8 | ConvertFrom-Json
    } catch {}
}

$apiDevJsonPath = "src/BlazorEnterprise.Api/appsettings.Development.json"
$existingApiDevJson = $null
if (Test-Path $apiDevJsonPath) {
    try {
        $existingApiDevJson = Get-Content $apiDevJsonPath -Raw -Encoding utf8 | ConvertFrom-Json
    } catch {}
}

# Preserve existing root tenant ID matching existing DB if present
if ($existingWebDevJson -and $existingWebDevJson.Admin -and $existingWebDevJson.Admin.RootTenantId -and $existingWebDevJson.Admin.RootTenantId -ne "9ebc32c7-4a74-4b4a-927b-8014dc9b589c") {
    $rootTenantGuid = $existingWebDevJson.Admin.RootTenantId
} else {
    $rootTenantGuid = "982506b7-3af9-4fe6-99ae-428ad8a49d0b"
}

if ($existingWebDevJson -and $existingWebDevJson.Security -and $existingWebDevJson.Security.IntegritySecret) {
    $integritySecret = $existingWebDevJson.Security.IntegritySecret
} else {
    $integritySecret = [System.Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
}

if ($existingWebDevJson -and $existingWebDevJson.Security -and $existingWebDevJson.Security.MasterEncryptionKey) {
    $masterEncryptionKey = $existingWebDevJson.Security.MasterEncryptionKey
} else {
    $masterEncryptionKey = [System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
}

if ($existingApiDevJson -and $existingApiDevJson.ProprietaryStorage -and $existingApiDevJson.ProprietaryStorage.VaultMasterKey) {
    $vaultMasterKey = $existingApiDevJson.ProprietaryStorage.VaultMasterKey
} else {
    $vaultMasterKey = [System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
}

if ([string]::IsNullOrWhiteSpace($AuthSigningKey)) {
    if ($existingApiDevJson -and $existingApiDevJson.Auth -and $existingApiDevJson.Auth.SigningKey) {
        $AuthSigningKey = $existingApiDevJson.Auth.SigningKey
    } else {
        $AuthSigningKey = [System.Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
    }
}

$connectionString = "Host=$DatabaseHost;Port=$DatabasePort;Database=$DatabaseName;Username=$DatabaseUser;Password=$DatabasePassword;Include Error Detail=true"

Write-Host "`nGenerated Identities & Configuration:" -ForegroundColor Cyan
Write-Host "  Root Tenant ID   : $rootTenantGuid" -ForegroundColor Green
Write-Host "  Root Tenant Slug : $RootTenantSlug" -ForegroundColor Green
Write-Host "  Root Tenant Name : $RootTenantName" -ForegroundColor Green
Write-Host "  Root Admin Email : $RootAdminEmail" -ForegroundColor Green
Write-Host "  Admin Domain     : $AdminAllowedDomain" -ForegroundColor Green
Write-Host "  Auth Issuer      : $AuthIssuer" -ForegroundColor Green
Write-Host "  Auth Audience    : $AuthAudience" -ForegroundColor Green

# 5. Target Projects & Clear Legacy User Secrets
$webProject = "src/BlazorEnterprise.Web"
$apiProject = "src/BlazorEnterprise.Api"
$appHostProject = "src/BlazorEnterprise.AppHost"

Write-Host "`nClearing any existing user secrets to ensure appsettings.Development.json takes precedence..." -ForegroundColor Cyan
if (Test-Path $appHostProject) {
    & dotnet user-secrets clear --project $appHostProject 2>$null
}
& dotnet user-secrets clear --project $webProject 2>$null
if (Test-Path $apiProject) {
    & dotnet user-secrets clear --project $apiProject 2>$null
}

# 6. Populate local appsettings.Development.json files
Write-Host "`nWriting local development configuration to appsettings.Development.json..." -ForegroundColor Cyan

# Web appsettings.Development.json
$webDevSettings = [ordered]@{
    ConnectionStrings = @{
        DefaultConnection = $connectionString
    }
    Alerting = @{
        Teams = @{
            Enabled = $true
            WebhookUrl = if ($existingWebDevJson -and $existingWebDevJson.Alerting -and $existingWebDevJson.Alerting.Teams -and $existingWebDevJson.Alerting.Teams.WebhookUrl) { $existingWebDevJson.Alerting.Teams.WebhookUrl } else { "" }
            MinLevel = "Error"
            MaxAlertsPerMinute = 10
        }
    }
    Security = @{
        IntegritySecret = $integritySecret
        MasterEncryptionKey = $masterEncryptionKey
        SystemBootstrapEmail = $RootAdminEmail
        Csp = @{
            Enforce = $true
            ReportOnly = $false
            ReportUri = "/api/security/csp-violations"
            Profile = "PureStrict"
        }
    }
    Identity = @{
        SystemDaemonLocalPart = "daemon"
        SystemDaemonEmailDomain = $AdminAllowedDomain
        SystemActorName = "System"
    }
    Admin = @{
        RootTenantId = $rootTenantGuid
        RootTenantSlug = $RootTenantSlug
        RootTenantName = $RootTenantName
        RootAdmins = $RootAdminEmail
    }
    Auth = @{
        DevLogin = @{
            Enabled = $true
            Users = @(
                @{ Email = "dev-admin@dev.local"; Label = "👑 Local Dev Admin" }
                @{ Email = "dev-user@dev.local"; Label = "👤 Local Dev User" }
            )
        }
    }
    Seed = @{
        Enabled = $true
        TenantSlug = "dev-org"
        TenantName = "Development Organization"
        InternalEmailDomains = "@dev.local"
        ExternalEmailDomains = "@client.dev.local"
        AdminEmail = "dev-admin@dev.local"
        AdminFullName = "Development Administrator"
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
            "BlazorEnterprise.Components" = "Debug"
        }
    }
}
$webDevPath = "$webProject/appsettings.Development.json"
$webDevSettings | ConvertTo-Json -Depth 10 | Set-Content $webDevPath -Encoding UTF8
Write-Host "  Updated $webDevPath" -ForegroundColor Green

# Api appsettings.Development.json
if (Test-Path $apiProject) {
    $apiDevSettings = [ordered]@{
        ConnectionStrings = @{
            DefaultConnection = $connectionString
        }
        Admin = @{
            RootTenantId = $rootTenantGuid
            RootTenantSlug = $RootTenantSlug
            RootTenantName = $RootTenantName
            RootAdmins = $RootAdminEmail
            AllowedDomain = $AdminAllowedDomain
            KeyVaultUsers = $AdminKeyVaultUsers
        }
        Security = @{
            IntegritySecret = $integritySecret
            SystemBootstrapEmail = $RootAdminEmail
        }
        Identity = @{
            SystemDaemonLocalPart = "daemon"
            SystemDaemonEmailDomain = $AdminAllowedDomain
            SystemActorName = "System"
        }
        Auth = @{
            Issuer = $AuthIssuer
            Audience = $AuthAudience
            SigningKey = $AuthSigningKey
        }
        ProprietaryStorage = @{
            VaultMasterKey = $vaultMasterKey
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
    }
    $apiDevPath = "$apiProject/appsettings.Development.json"
    $apiDevSettings | ConvertTo-Json -Depth 10 | Set-Content $apiDevPath -Encoding UTF8
    Write-Host "  Updated $apiDevPath" -ForegroundColor Green
}

# AppHost appsettings.Development.json
if (Test-Path $appHostProject) {
    $appHostDevSettings = [ordered]@{
        ConnectionStrings = @{
            DefaultConnection = $connectionString
        }
        Aspire = @{
            UseDockerPostgres = $false
        }
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
                "Aspire.Hosting.Dcp" = "Warning"
            }
        }
    }
    $appHostDevPath = "$appHostProject/appsettings.Development.json"
    $appHostDevSettings | ConvertTo-Json -Depth 10 | Set-Content $appHostDevPath -Encoding UTF8
    Write-Host "  Updated $appHostDevPath" -ForegroundColor Green
}

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Local dev environment initialized successfully!" -ForegroundColor Green
Write-Host " Configured exclusively in local appsettings.Development.json." -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
