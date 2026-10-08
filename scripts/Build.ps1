param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [ValidateSet('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')]
    [string[]]$Modules = @(),
    [switch]$All,
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [string]$OutputDirectory = '',
    [switch]$Install
)
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') @PSBoundParameters
