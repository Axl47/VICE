param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [Parameter(Mandatory)][string]$PackageDir
)
$ErrorActionPreference = 'Stop'
# Compatibility entry point; use the shared install engine for current exports.
& (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $GameDir -PackageDir $PackageDir -Modules Enforcement
