param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [Parameter(Mandatory)][string]$PackageDir,
    [ValidateSet('Commerce', 'Arsenal', 'Lifestyle', 'Interface', 'Enforcement', 'Syndicate', 'Substances')]
    [string[]]$Modules = @(),
    [ValidateSet('YouTube')][string[]]$AddOns = @()
)
$ErrorActionPreference = 'Stop'
$metadata = Get-Content -LiteralPath (Join-Path $PackageDir 'package.json') -Raw | ConvertFrom-Json
if (-not $PSBoundParameters.ContainsKey('Modules')) {
    $Modules = @($metadata.Modules)
}
if (-not $PSBoundParameters.ContainsKey('AddOns')) { $AddOns = @($metadata.AddOns | Where-Object { $_ }) }
& (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $GameDir -PackageDir $PackageDir -Modules $Modules -AddOns $AddOns
