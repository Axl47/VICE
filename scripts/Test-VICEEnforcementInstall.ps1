param([Parameter(Mandatory)][string]$PackageDir)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $repoRoot ('artifacts\install-fixtures\enforcement-'+[Guid]::NewGuid().ToString('N'))
foreach($folder in @('Mods','UserLibs','UserData\VICE\Enforcement')){New-Item -ItemType Directory -Path (Join-Path $fixture $folder) -Force | Out-Null}
Set-Content -LiteralPath (Join-Path $fixture 'Schedule I.exe') -Value 'fixture only'
Set-Content -LiteralPath (Join-Path $fixture 'Mods\Personal.BetterPolice.dll') -Value 'legacy police'
Set-Content -LiteralPath (Join-Path $fixture 'Mods\VICE.Core.dll') -Value 'duplicate core'
Set-Content -LiteralPath (Join-Path $fixture 'Mods\Personal.MoreGuns.dll') -Value 'other module'
Set-Content -LiteralPath (Join-Path $fixture 'UserData\VICE\Enforcement\config.json') -Value 'personal settings'
& (Join-Path $PSScriptRoot 'Install-VICEEnforcement.ps1') -GameDir $fixture -PackageDir $PackageDir
if(Test-Path -LiteralPath (Join-Path $fixture 'Mods\Personal.BetterPolice.dll')){throw 'Legacy police remained active.'}
if(Test-Path -LiteralPath (Join-Path $fixture 'Mods\VICE.Core.dll')){throw 'Duplicate Core remained active in Mods.'}
foreach($relative in @('Mods\VICE.Enforcement.dll','UserLibs\VICE.Core.dll')){
    if((Get-FileHash -LiteralPath (Join-Path $fixture $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PackageDir $relative)).Hash){throw "Fixture hash mismatch: $relative"}
}
if((Get-Content -LiteralPath (Join-Path $fixture 'Mods\Personal.MoreGuns.dll') -Raw).Trim() -ne 'other module'){throw 'Unrelated module changed.'}
if((Get-Content -LiteralPath (Join-Path $fixture 'UserData\VICE\Enforcement\config.json') -Raw).Trim() -ne 'personal settings'){throw 'Existing configuration changed.'}
if(-not(Test-Path -LiteralPath (Join-Path $fixture 'UserData\VICE\Enforcement\Spawn\patrols.json'))){throw 'Missing defaults were not seeded.'}
& (Join-Path $PSScriptRoot 'Install-VICEEnforcement.ps1') -GameDir $fixture -PackageDir $PackageDir
if((Get-Content -LiteralPath (Join-Path $fixture 'UserData\VICE\Enforcement\config.json') -Raw).Trim() -ne 'personal settings'){throw 'Reinstallation overwrote configuration.'}
Write-Output "Installer checks passed in isolated fixture: $fixture"
