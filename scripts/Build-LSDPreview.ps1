# Compatibility entry point: preview-only predecessor packages are retired.
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = '',
    [string]$NugetConfig = ''
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') -Modules Substances @PSBoundParameters