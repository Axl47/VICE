param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [switch]$Install
)
& (Join-Path $PSScriptRoot 'Build-VICEModules.ps1') @PSBoundParameters
