param([Parameter(Mandatory)][string]$PackageDir)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$metadata = Get-Content -LiteralPath (Join-Path $PackageDir 'package.json') -Raw | ConvertFrom-Json
$modules = @($metadata.Modules)
$addOns = @($metadata.AddOns | Where-Object { $_ })
$checks = 0
function Assert-ExportInstall([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
$fixture = Join-Path $repoRoot ('artifacts\install-tests\VICE-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $fixture 'game'
New-Item -ItemType Directory -Path (Join-Path $game 'Mods'), (Join-Path $game 'UserLibs') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'Schedule I.exe') -Value 'fixture'
Set-Content -LiteralPath (Join-Path $game 'Mods\S1API.Il2Cpp.MelonLoader.dll') -Value 'existing dependency'
Set-Content -LiteralPath (Join-Path $game 'Mods\VICE.Core.dll') -Value 'duplicate Core'
Set-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDPreview.dll') -Value 'unrelated module'
Set-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDGameplay.dll') -Value 'legacy workshop'
New-Item -ItemType Directory -Path (Join-Path $game 'UserData'), (Join-Path $game 'Saves\SaveGame_5\Modded\OffRegister') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'UserData\MelonPreferences.cfg') -Value '[PersonalLSDPreview] Intensity = 0.27'
Set-Content -LiteralPath (Join-Path $game 'Saves\SaveGame_5\Modded\OffRegister\Workshop.json') -Value 'existing workshop state'
Set-Content -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll') -Value 'previous Core'
foreach ($module in @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate','Substances')) {
    Set-Content -LiteralPath (Join-Path $game "Mods\VICE.$module.dll") -Value "previous $module"
}
if ($modules -contains 'Enforcement') {
    New-Item -ItemType Directory -Path (Join-Path $game 'UserData\VICE\Enforcement') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $game 'UserData\VICE\Enforcement\config.json') -Value 'personal settings'
    Set-Content -LiteralPath (Join-Path $game 'Mods\Personal.BetterPolice.dll') -Value 'legacy police'
}
Set-Content -LiteralPath (Join-Path $game 'Mods\VICE.YouTube.dll') -Value 'previous YouTube'
if ($addOns -contains 'YouTube' -and $modules -notcontains 'Lifestyle') {
    $missing = Join-Path $fixture 'missing-base'
    New-Item -ItemType Directory -Path (Join-Path $missing 'Mods'),(Join-Path $missing 'UserLibs') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $missing 'Schedule I.exe') -Value 'fixture'
    Set-Content -LiteralPath (Join-Path $missing 'Mods\S1API.Il2Cpp.MelonLoader.dll') -Value 'existing dependency'
    Set-Content -LiteralPath (Join-Path $missing 'UserLibs\VICE.Core.dll') -Value 'preserve this Core'
    $rejected = $false
    try { & (Join-Path $PackageDir 'Install-VICE.ps1') -GameDir $missing -PackageDir $PackageDir } catch { $rejected = $_.Exception.Message -like '*requires VICE Lifestyle*' }
    Assert-ExportInstall $rejected 'Add-on-only install accepted a missing base.'
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $missing 'UserLibs\VICE.Core.dll') -Raw).Trim() -eq 'preserve this Core') 'Missing-base preflight wrote Core.'
    $coreBefore = (Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash
    $rejected = $false
    try { & (Join-Path $PackageDir 'Install-VICE.ps1') -GameDir $game -PackageDir $PackageDir } catch { $rejected = $_.Exception.Message -like '*requires VICE Lifestyle*' }
    Assert-ExportInstall $rejected 'Add-on-only install accepted an incompatible base.'
    Assert-ExportInstall ((Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash -eq $coreBefore) 'Failed add-on dependency preflight wrote Core.'
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Lifestyle\bin\Release\net6.0\VICE.Lifestyle.dll') -Destination (Join-Path $game 'Mods\VICE.Lifestyle.dll') -Force
    $baseBefore = (Get-FileHash -LiteralPath (Join-Path $game 'Mods\VICE.Lifestyle.dll')).Hash
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
if ($modules -contains 'Substances') {
    $missing = Join-Path $fixture 'missing-substances-dependency'
    New-Item -ItemType Directory -Path (Join-Path $missing 'Mods'),(Join-Path $missing 'UserLibs') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $missing 'Schedule I.exe') -Value 'fixture'
    Set-Content -LiteralPath (Join-Path $missing 'UserLibs\VICE.Core.dll') -Value 'preserve this Core'
    foreach ($name in @('Personal.LSDGameplay.dll','Personal.LSDPreview.dll')) { Set-Content -LiteralPath (Join-Path $missing "Mods\$name") -Value $name }
    $rejected = $false
    try { & (Join-Path $PackageDir 'Install-VICE.ps1') -GameDir $missing -PackageDir $PackageDir -Modules Substances -AddOns @() } catch { $rejected = $_.Exception.Message -like '*S1API dependency*' }
    Assert-ExportInstall $rejected 'Substances accepted a missing S1API dependency.'
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $missing 'UserLibs\VICE.Core.dll') -Raw).Trim() -eq 'preserve this Core') 'Failed Substances preflight replaced Core.'
    foreach ($name in @('Personal.LSDGameplay.dll','Personal.LSDPreview.dll')) { Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $missing "Mods\$name") -Raw).Trim() -eq $name) 'Failed Substances preflight removed a predecessor.' }
}
$installOutput = @(& (Join-Path $PackageDir 'Install-VICE.ps1') -GameDir $game -PackageDir $PackageDir)
$backupRoot = ($installOutput[-1] -split 'Previous DLLs backed up outside Mods: ', 2)[1]
Assert-ExportInstall ((Get-FileHash -LiteralPath (Join-Path $game 'UserLibs\VICE.Core.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PackageDir 'UserLibs\VICE.Core.dll')).Hash) 'Installer did not update Core.'
Assert-ExportInstall (-not (Test-Path -LiteralPath (Join-Path $game 'Mods\VICE.Core.dll'))) 'Duplicate Core remained active.'
foreach ($module in @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate','Substances')) {
    $target = Join-Path $game "Mods\VICE.$module.dll"
    if ($module -in $modules) {
        Assert-ExportInstall ((Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PackageDir "Mods\VICE.$module.dll")).Hash) "Wrong installed DLL: $module"
    } elseif ($module -eq 'Lifestyle' -and $addOns -contains 'YouTube') {
        Assert-ExportInstall ((Get-FileHash -LiteralPath $target).Hash -eq $baseBefore) 'Add-on-only install replaced the existing base TV.'
    } else { Assert-ExportInstall ((Get-Content -LiteralPath $target -Raw).Trim() -eq "previous $module") "Unselected module changed: $module" }
}
if ($addOns -contains 'YouTube') {
    Assert-ExportInstall ((Get-FileHash -LiteralPath (Join-Path $game 'Mods\VICE.YouTube.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PackageDir 'Mods\VICE.YouTube.dll')).Hash) 'YouTube add-on DLL was not installed.'
} else { Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'Mods\VICE.YouTube.dll') -Raw).Trim() -eq 'previous YouTube') 'Unselected YouTube add-on changed.' }
if ($modules -contains 'Substances') {
    foreach ($name in @('Personal.LSDGameplay.dll', 'Personal.LSDPreview.dll')) {
        Assert-ExportInstall (-not (Test-Path -LiteralPath (Join-Path $game "Mods\$name"))) "Substances predecessor remained active: $name"
        Assert-ExportInstall (Test-Path -LiteralPath (Join-Path $backupRoot "Mods\$name") -PathType Leaf) "Substances predecessor backup missing: $name"
    }
} else {
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDPreview.dll') -Raw).Trim() -eq 'unrelated module') 'Unselected LSD preview changed.'
    Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'Mods\Personal.LSDGameplay.dll') -Raw).Trim() -eq 'legacy workshop') 'Unselected LSD gameplay changed.'
}
Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'UserData\MelonPreferences.cfg') -Raw).Trim() -eq '[PersonalLSDPreview] Intensity = 0.27') 'Existing effect preferences changed.'
Assert-ExportInstall ((Get-Content -LiteralPath (Join-Path $game 'Saves\SaveGame_5\Modded\OffRegister\Workshop.json') -Raw).Trim() -eq 'existing workshop state') 'Existing workshop save changed.'
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
try { & (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $extra -ExpectedModules $modules -ExpectedAddOns $addOns } catch { $rejected = $_.Exception.Message -like '*exactly Core*' }
Assert-ExportInstall $rejected 'An export with extra DLLs passed validation.'
if ($modules -contains 'Substances') {
    # A predecessor cannot leak back into either a selective or full release export.
    foreach ($name in @('Personal.LSDGameplay.dll', 'Personal.LSDPreview.dll')) {
        $legacyPackage = Join-Path $fixture ([IO.Path]::GetFileNameWithoutExtension($name))
        Copy-Item -LiteralPath $PackageDir -Destination $legacyPackage -Recurse
        Set-Content -LiteralPath (Join-Path $legacyPackage "Mods\$name") -Value 'retired predecessor'
        $rejected = $false
        try { & (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $legacyPackage -ExpectedModules $modules -ExpectedAddOns $addOns } catch { $rejected = $_.Exception.Message -like '*exactly Core*' }
        Assert-ExportInstall $rejected "An export containing $name passed exact packaging validation."
    }
}
if (Test-Path -LiteralPath "$PackageDir.zip") {
    $badZip = Join-Path $fixture 'extra-predecessor.zip'
    Copy-Item -LiteralPath "$PackageDir.zip" -Destination $badZip
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($badZip, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.CreateEntry('Mods/Personal.LSDPreview.dll')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write('retired predecessor') } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $PackageDir -ExpectedModules $modules -ExpectedAddOns $addOns -ZipPath $badZip } catch { $rejected = $_.Exception.Message -like '*ZIP contents differ*' }
    Assert-ExportInstall $rejected 'A ZIP containing an extra predecessor DLL passed validation.'
}
Write-Host "Verified installer in isolated fixture ($checks checks): Core updated, selected modules only, corrupt export rejected."
