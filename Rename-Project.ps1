#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Root forwarding script for scripts/Rename-Project.ps1
.DESCRIPTION
    Forwards all arguments, parameters, and switches to scripts/Rename-Project.ps1.
    Allows running `./Rename-Project.ps1` directly from the solution root.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$NewName = "",

    [Parameter(Mandatory = $false)]
    [string]$OldName = "BlazorFluent",

    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$scriptPath = Join-Path $PSScriptRoot "scripts\Rename-Project.ps1"
if (-not (Test-Path $scriptPath)) {
    throw "Target rename script not found at '$scriptPath'."
}

& $scriptPath @PSBoundParameters
