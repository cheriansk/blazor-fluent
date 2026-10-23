# ==============================================================================
# verify-no-hardcode.ps1
# Automated Zero-Trust & Fail-First Hardcoding Verification Scanner
# Scans solution source files and configuration for:
#   - Hardcoded GUID literals
#   - Obsolete static identity members (DefaultRootTenantId, RequiredTenantId, etc.)
#   - Hardcoded daemon emails ("daemon@default.internal", etc.)
#   - Committed passwords or credentials in appsettings*.json
# Fails (exit code 1) if any violation is detected.
# ==============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " [ZERO-TRUST AUDIT] Scanning solution for hardcoded secrets and entities" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$violationsFound = 0

# 1. Source code scanner (excluding tests, migrations, bin, obj, git)
$codeRules = @(
    @{
        Pattern = '(?<![\w])(DefaultRootTenantId|DefaultTenantSlug|RequiredTenantId|SystemDaemonTenantId|SystemTenantSlug)\b';
        Description = "Obsolete static identity reference detected. Use IRootAdminService or ISystemIdentity instead."
    },
    @{
        Pattern = 'daemon@default\.internal';
        Description = "Hardcoded fallback daemon email 'daemon@default.internal'. Use ISystemIdentity.DaemonEmailFor(slug)."
    },
    @{
        Pattern = 'daemon@\{slug\}\.internal';
        Description = "Hardcoded fallback daemon email template. Use ISystemIdentity.DaemonEmailFor(slug)."
    },
    @{
        Pattern = 'system@daemon\.local';
        Description = "Hardcoded legacy daemon email 'system@daemon.local'."
    },
    @{
        Pattern = '(?<![\w])Guid\.Parse\(["''][0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}["'']\)';
        Description = "Hardcoded parsed GUID literal. Pass identity via configuration or generate dynamic UUIDv7."
    },
    @{
        Pattern = 'new Guid\(["''][0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}["'']\)';
        Description = "Hardcoded instantiated GUID literal. Pass identity via configuration or generate dynamic UUIDv7."
    }
)

$srcFiles = Get-ChildItem -Path "src" -Recurse -Include *.cs, *.razor | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|artifacts|Migrations)[\\/]'
}

foreach ($rule in $codeRules) {
    $pattern = $rule.Pattern
    $desc = $rule.Description
    
    $matches = $srcFiles | Select-String -Pattern $pattern
    if ($matches) {
        foreach ($m in $matches) {
            $relPath = Resolve-Path -Relative $m.Path
            Write-Host " [FAIL] $($relPath):$($m.LineNumber)" -ForegroundColor Red
            Write-Host "        Rule: $desc" -ForegroundColor Yellow
            Write-Host "        Line: $($m.Line.Trim())" -ForegroundColor DarkGray
            $violationsFound++
        }
    }
}

# 2. Configuration files scanner (appsettings*.json in src)
$configRules = @(
    @{
        Pattern = 'Password=\s*[^;"]+';
        Description = "Plaintext password detected in committed configuration. Use user-secrets or environment variables."
    },
    @{
        Pattern = '"RootTenantId"\s*:\s*"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"';
        Description = "Committed Root Tenant GUID detected in configuration. Keep placeholder empty and populate via user-secrets."
    },
    @{
        Pattern = 'admin@company\.com';
        Description = "Hardcoded sample email 'admin@company.com' detected in configuration."
    }
)

$configFiles = Get-ChildItem -Path "src" -Recurse -Filter "appsettings*.json" | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|artifacts)[\\/]'
}

foreach ($rule in $configRules) {
    $pattern = $rule.Pattern
    $desc = $rule.Description
    
    $matches = $configFiles | Select-String -Pattern $pattern
    if ($matches) {
        foreach ($m in $matches) {
            $relPath = Resolve-Path -Relative $m.Path
            Write-Host " [FAIL] $($relPath):$($m.LineNumber)" -ForegroundColor Red
            Write-Host "        Rule: $desc" -ForegroundColor Yellow
            Write-Host "        Line: $($m.Line.Trim())" -ForegroundColor DarkGray
            $violationsFound++
        }
    }
}

Write-Host "----------------------------------------------------------------------" -ForegroundColor Cyan
if ($violationsFound -eq 0) {
    Write-Host " [PASS] Zero-Trust verification succeeded: Zero hardcoded secrets or entities found!" -ForegroundColor Green
    exit 0
} else {
    Write-Host " [FAIL] $violationsFound violation(s) detected. Fix all hardcodings before committing." -ForegroundColor Red
    exit 1
}
