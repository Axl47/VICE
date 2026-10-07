$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$package = Join-Path $repoRoot 'artifacts\VICE.Modules-0.1.0'
$fixture = Join-Path $repoRoot ('artifacts\install-tests\VICE.Modules-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $fixture 'game'
$mods = Join-Path $game 'Mods'
New-Item -ItemType Directory -Path $mods, (Join-Path $game 'UserData\PersonalGasPurchases'), (Join-Path $game 'UserData\PersonalTV') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'Schedule I.exe'), 'fixture')
$old = @('Personal.Highballer.dll', 'Personal.GasPurchases.dll', 'Personal.Warehouse.dll', 'Personal.MoreGuns.dll', 'Personal.Casino.dll', 'Personal.TV.dll', 'Personal.TVProof.dll', 'Personal.UIEnhancements.dll', 'VICE.Core.dll')
$keep = @('Personal.BetterPolice.dll', 'VICE.Enforcement.dll', 'VICE.Syndicate.dll', 'Personal.LSDGameplay.dll', 'Personal.LSDPreview.dll', 'S1API.Il2Cpp.MelonLoader.dll')
foreach ($name in $old + $keep) { [IO.File]::WriteAllText((Join-Path $mods $name), $name) }
[IO.File]::WriteAllText((Join-Path $game 'UserData\PersonalGasPurchases\orders.json'), 'saved orders')
[IO.File]::WriteAllText((Join-Path $game 'UserData\PersonalTV\keep.txt'), 'local TV media')
$checks = 0
function Assert-Install([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
& (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $game -PackageDir $package -Modules Commerce
foreach ($name in @('Personal.Highballer.dll', 'Personal.GasPurchases.dll', 'Personal.Warehouse.dll', 'VICE.Core.dll')) {
    Assert-Install (-not (Test-Path -LiteralPath (Join-Path $mods $name))) "Commerce migration left duplicate $name"
}
foreach ($name in @('Personal.MoreGuns.dll', 'Personal.Casino.dll', 'Personal.TV.dll', 'Personal.UIEnhancements.dll') + $keep) {
    Assert-Install ((Get-Content -LiteralPath (Join-Path $mods $name) -Raw) -eq $name) "Selective installation changed $name"
}
& (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $game -PackageDir $package
foreach ($name in $old) { Assert-Install (-not (Test-Path -LiteralPath (Join-Path $mods $name))) "Full migration left duplicate $name" }
foreach ($module in @('Commerce', 'Arsenal', 'Lifestyle', 'Interface')) {
    Assert-Install ((Get-FileHash -LiteralPath (Join-Path $mods "VICE.$module.dll")).Hash -eq (Get-FileHash -LiteralPath (Join-Path $package "Mods\VICE.$module.dll")).Hash) "Wrong installed module: $module"
}
foreach ($name in $keep) { Assert-Install ((Get-Content -LiteralPath (Join-Path $mods $name) -Raw) -eq $name) "Migration changed unrelated module $name" }
Assert-Install ((Get-Content -LiteralPath (Join-Path $game 'UserData\PersonalGasPurchases\orders.json') -Raw) -eq 'saved orders') 'Shopping list data changed.'
Assert-Install ((Get-Content -LiteralPath (Join-Path $game 'UserData\PersonalTV\keep.txt') -Raw) -eq 'local TV media') 'TV media changed.'
Assert-Install ((Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $package 'UserLibs\VICE.Core.dll')).Hash) 'Shared Core hash differs.'
# A damaged package must fail preflight before it removes or overwrites any installed DLL.
$bad = Join-Path $fixture 'bad-package'
Copy-Item -LiteralPath $package -Destination $bad -Recurse
[IO.File]::AppendAllText((Join-Path $bad 'Mods\VICE.Commerce.dll'), 'damaged')
$before = (Get-FileHash -LiteralPath (Join-Path $mods 'VICE.Commerce.dll')).Hash
$rejected = $false
try { & (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $game -PackageDir $bad -Modules Commerce }
catch { $rejected = $_.Exception.Message -like '*hash mismatch*' }
Assert-Install $rejected 'Damaged package was not rejected.'
Assert-Install ((Get-FileHash -LiteralPath (Join-Path $mods 'VICE.Commerce.dll')).Hash -eq $before) 'Failed preflight modified the installation.'
Write-Output "VICE installer checks passed ($checks assertions). Isolated fixture: $fixture"
