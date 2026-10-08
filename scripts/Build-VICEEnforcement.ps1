param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [string]$OutputDirectory = '',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') -Modules Enforcement @PSBoundParameters
