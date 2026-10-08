param([Parameter(Mandatory)][string]$PackageDir)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$metadata = Get-Content -LiteralPath (Join-Path $PackageDir 'package.json') -Raw | ConvertFrom-Json
$modules = @($metadata.Modules)
$fixture = Join-Path $repoRoot ('artifacts\install-tests\VICE-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $fixture 'game'
New-Item -ItemType Directory -Path (Join-Path $game 'Mods'), (Join-Path $game 'UserLibs') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'Schedule I.exe') -Value 'fixture'
Set-Content -LiteralPath (Join-Path $game 'Mods\S1API.Il2Cpp.MelonLoader.dll') -Value 'existing dependency'
Set-Content -LiteralPath (Join-Path $game 'Mods\VICE.Core.dll') -Value 'duplicate Core'
Set-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDPreview.dll') -Value 'unrelated module'
Set-Content -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll') -Value 'previous Core'
foreach ($module in @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')) {
    Set-Content -LiteralPath (Join-Path $game "Mods\VICE.$module.dll") -Value "previous $module"
}
if ($modules -contains 'Enforcement') {
    New-Item -ItemType Directory -Path (Join-Path $game 'UserData\VICE\Enforcement') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $game 'UserData\VICE\Enforcement\config.json') -Value 'personal settings'
    Set-Content -LiteralPath (Join-Path $game 'Mods\Personal.BetterPolice.dll') -Value 'legacy police'
}
foreach ($module in $modules | Where-Object { $_ -in @('Enforcement', 'Syndicate') }) {
    $legacySetting = if ($module -eq 'Enforcement') { 'officer.json' } else { 'Ambush\settings.json' }
    $legacyTarget = Join-Path $game "UserData\VICE.$module\$legacySetting"
    New-Item -ItemType Directory -Path (Split-Path $legacyTarget -Parent) -Force | Out-Null
    Set-Content -LiteralPath $legacyTarget -Value 'legacy personal settings'
    $save = Join-Path $game "UserData\VICE.$module\SavedRecords\slot.json"
    New-Item -ItemType Directory -Path (Split-Path $save -Parent) -Force | Out-Null
    Set-Content -LiteralPath $save -Value 'legacy saved record'
}
& (Join-Path $PackageDir 'Install-VICE.ps1') -GameDir $game -PackageDir $PackageDir
$checks = 0
function Assert-ExportInstall([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
Assert-ExportInstall ((Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PackageDir 'UserLibs\VICE.Core.dll')).Hash) 'Installer did not update Core.'
Assert-ExportInstall (-not (Test-Path -LiteralPath (Join-Path $game 'Mods\VICE.Core.dll'))) 'Duplicate Core remained active.'
foreach ($module in @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')) {
    $target = Join-Path $game "Mods\VICE.$module.dll"
    if ($module -in $modules) {
        Assert-ExportInstall ((Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PackageDir "Mods\VICE.$module.dll")).Hash) "Wrong installed DLL: $module"
    } else { Assert-ExportInstall ((Get-Content -LiteralPath $target -Raw).Trim() -eq "previous $module") "Unselected module changed: $module" }
}
Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDPreview.dll') -Raw).Trim() -eq 'unrelated module') 'Unrelated personal module changed.'
if ($modules -contains 'Enforcement') {
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'UserData\VICE\Enforcement\config.json') -Raw).Trim() -eq 'personal settings') 'Existing Enforcement settings were overwritten.'
    Assert-ExportInstall (Test-Path -LiteralPath (Join-Path $game 'UserData\VICE\Enforcement\Spawn\patrols.json')) 'Missing Enforcement defaults were not seeded.'
    Assert-ExportInstall (-not (Test-Path -LiteralPath (Join-Path $game 'Mods\Personal.BetterPolice.dll'))) 'Legacy police remained active.'
}
if ($modules -contains 'Syndicate') { Assert-ExportInstall (Test-Path -LiteralPath (Join-Path $game 'UserData\VICE\Syndicate\config.json')) 'Syndicate defaults were not seeded.' }
foreach ($module in $modules | Where-Object { $_ -in @('Enforcement', 'Syndicate') }) {
    $legacySetting = if ($module -eq 'Enforcement') { 'officer.json' } else { 'Ambush\settings.json' }
    Assert-ExportInstall (-not (Test-Path -LiteralPath (Join-Path $game "UserData\VICE\$module\$legacySetting"))) "Defaults masked legacy $module settings before runtime migration."
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game "UserData\VICE.$module\$legacySetting") -Raw).Trim() -eq 'legacy personal settings') "Legacy $module settings changed."
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game "UserData\VICE.$module\SavedRecords\slot.json") -Raw).Trim() -eq 'legacy saved record') "Legacy $module saved data changed."
}
$bad = Join-Path $fixture 'bad-package'
Copy-Item -LiteralPath $PackageDir -Destination $bad -Recurse
[IO.File]::AppendAllText((Join-Path $bad 'UserLibs\VICE.Core.dll'), 'corrupt')
$before = (Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash
$rejected = $false
try { & (Join-Path $bad 'Install-VICE.ps1') -GameDir $game -PackageDir $bad } catch { $rejected = $_.Exception.Message -like '*hash mismatch*' }
Assert-ExportInstall $rejected 'Corrupt Core was not rejected during preflight.'
Assert-ExportInstall ((Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash -eq $before) 'Failed preflight modified installed Core.'
$extra = Join-Path $fixture 'extra-package'
Copy-Item -LiteralPath $PackageDir -Destination $extra -Recurse
New-Item -ItemType Directory -Path (Join-Path $extra 'Mods') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $extra 'Mods\VICE.Core.dll') -Value 'duplicate'
$rejected = $false
try { & (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $extra -ExpectedModules $modules } catch { $rejected = $_.Exception.Message -like '*exactly Core*' }
Assert-ExportInstall $rejected 'An export with extra DLLs passed validation.'
Write-Host "Verified installer in isolated fixture ($checks checks): Core updated, selected modules only, corrupt export rejected."
