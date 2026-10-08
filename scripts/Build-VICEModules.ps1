param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [string]$OutputDirectory = '',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') -Modules Commerce,Arsenal,Lifestyle,Interface @PSBoundParameters
