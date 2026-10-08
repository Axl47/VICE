# Compatibility entry point; packaging rules live in one implementation.
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Package-VICE.ps1') -Modules Commerce,Arsenal,Lifestyle,Interface -OutputDirectory $OutputDirectory
