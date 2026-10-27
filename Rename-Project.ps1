#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Root entry point to rebrand and rename the project template.
    Forwards all arguments to scripts/Rename-Project.ps1.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$ScriptArgs
)

$targetScript = Join-Path $PSScriptRoot "scripts\Rename-Project.ps1"
if (-not (Test-Path $targetScript)) {
    Write-Error "Could not find target script: $targetScript"
    exit 1
}

& $targetScript @args
exit $LASTEXITCODE
