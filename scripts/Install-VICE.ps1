param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [Parameter(Mandatory)][string]$PackageDir,
    [ValidateSet('Commerce', 'Arsenal', 'Lifestyle', 'Interface', 'Enforcement', 'Syndicate')]
    [string[]]$Modules = @()
)
$ErrorActionPreference = 'Stop'
if (-not $PSBoundParameters.ContainsKey('Modules')) {
    $metadata = Get-Content -LiteralPath (Join-Path $PackageDir 'package.json') -Raw | ConvertFrom-Json
    $Modules = @($metadata.Modules)
}
& (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $GameDir -PackageDir $PackageDir -Modules $Modules
