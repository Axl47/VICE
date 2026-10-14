param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [string]$OutputDirectory = '',
    [switch]$PrimitiveHomeGames,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') -Modules Lifestyle @PSBoundParameters
