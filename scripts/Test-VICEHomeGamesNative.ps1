param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$Save5,
    [string]$CandidateDll,
    [switch]$PrimitiveArtwork,
    [switch]$RejectInitialization
)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path $PSScriptRoot -Parent
if (-not $CandidateDll) { $CandidateDll = Join-Path $taskRepo 'src/VICE.Lifestyle/bin/Release/net6.0/VICE.Lifestyle.dll' }
if (Get-Process -Name 'Schedule I','ScheduleI' -ErrorAction SilentlyContinue) { throw 'Close the game before running the isolated native check.' }
if (-not $Save5) {
    $taskSaves = @(Get-ChildItem -LiteralPath (Join-Path $env:USERPROFILE 'AppData/LocalLow/TVGS/Schedule I/Saves') -Directory | ForEach-Object { Join-Path $_.FullName 'SaveGame_5' } | Where-Object { Test-Path -LiteralPath $_ })
    if ($taskSaves.Count -ne 1) { throw 'Specify the exact SaveGame_5 folder.' }
    $Save5 = $taskSaves[0]
}
$Save5 = (Resolve-Path -LiteralPath $Save5).Path
if ((Split-Path $Save5 -Leaf) -ne 'SaveGame_5') { throw 'The source must be SaveGame_5.' }
function Get-TreeHashes([string]$taskRoot) {
    @(Get-ChildItem -LiteralPath $taskRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($taskRoot.Length); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
$taskSaveBefore = Get-TreeHashes $Save5
$taskProductionFiles = @(Join-Path $GameDir 'Mods/VICE.Lifestyle.dll'; Join-Path $GameDir 'UserLibs/VICE.Core.dll'; Join-Path $GameDir 'UserData/MelonPreferences.cfg')
$taskProductionBefore = @($taskProductionFiles | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
$taskHash = (Get-FileHash -LiteralPath $CandidateDll -Algorithm SHA256).Hash.Substring(0,12).ToLowerInvariant()
$taskRoot = Join-Path $taskRepo ('artifacts/home-games-native-' + $taskHash + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + $(if ($PrimitiveArtwork) { '-primitive' } else { '-artwork' }))
$taskProfile = Join-Path $taskRoot 'Profile'
$taskEvidence = Join-Path $taskRoot 'Evidence'
$taskCopyParent = Join-Path $taskRoot 'CopiedSave'
New-Item -ItemType Directory -Path $taskProfile,$taskEvidence,$taskCopyParent,(Join-Path $taskProfile 'Mods'),(Join-Path $taskProfile 'UserLibs'),(Join-Path $taskProfile 'UserData'),(Join-Path $taskProfile 'Plugins') | Out-Null
Copy-Item -LiteralPath $Save5 -Destination $taskCopyParent -Recurse
Copy-Item -LiteralPath (Join-Path $GameDir 'MelonLoader') -Destination $taskProfile -Recurse
Copy-Item -LiteralPath (Join-Path $GameDir 'Mods/S1API.Il2Cpp.MelonLoader.dll') -Destination (Join-Path $taskProfile 'Mods')
Copy-Item -LiteralPath $CandidateDll -Destination (Join-Path $taskProfile 'Mods/VICE.Lifestyle.dll')
Copy-Item -LiteralPath (Join-Path $taskRepo 'src/VICE.Core/bin/Release/net6.0/VICE.Core.dll') -Destination (Join-Path $taskProfile 'UserLibs')
$taskArtwork = if ($PrimitiveArtwork) { 'false' } else { 'true' }
@"
["VICE.Lifestyle.Amenities"]
EnablePrototypes = true
UseModelArtwork = $taskArtwork
"@ | Set-Content -LiteralPath (Join-Path $taskProfile 'UserData/MelonPreferences.cfg') -Encoding utf8
$taskSaveBefore | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'original-save-before.json')
$taskCheckRoot = if ($RejectInitialization) { Join-Path $taskRoot 'RefusedInitialization' } else { $taskRoot }
$taskArgs = '--melonloader.basedir "' + $taskProfile + '" --melonloader.hideconsole --home-games-check="' + $taskCheckRoot + '" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "' + (Join-Path $taskEvidence 'unity.log') + '"'
Write-Output "Isolated native check: $taskRoot"
$taskProcess = Start-Process -FilePath (Join-Path $GameDir 'Schedule I.exe') -WorkingDirectory $GameDir -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
try {
    $taskDeadline = (Get-Date).AddMinutes(6)
    while (-not $taskProcess.HasExited) {
        if ((Get-Date) -gt $taskDeadline) { Stop-Process -Id $taskProcess.Id; throw 'Isolated game process timed out.' }
        Start-Sleep -Seconds 2
        $taskProcess.Refresh()
    }
} finally {
    $taskSaveAfter = Get-TreeHashes $Save5
    $taskProductionAfter = @($taskProductionFiles | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
    $taskSaveDiff = @(Compare-Object ($taskSaveBefore | ConvertTo-Json -Compress) ($taskSaveAfter | ConvertTo-Json -Compress))
    $taskProductionDiff = @(Compare-Object $taskProductionBefore $taskProductionAfter)
    $taskSaveAfter | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'original-save-after.json')
    [pscustomobject]@{ OriginalSaveFiles = $taskSaveBefore.Count; OriginalSaveUnchanged = $taskSaveDiff.Count -eq 0; ProductionDllsAndPreferencesUnchanged = $taskProductionDiff.Count -eq 0; CandidateHash = (Get-FileHash -LiteralPath $CandidateDll).Hash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskEvidence 'isolation-proof.json')
    if ($taskSaveDiff.Count -or $taskProductionDiff.Count) { throw 'Original-save or production hash verification failed.' }
}
if ($RejectInitialization) {
    $taskLoaderLog = Get-Content -LiteralPath (Join-Path $taskProfile 'MelonLoader/Latest.log') -Raw
    if ($taskLoaderLog -notmatch 'Native diagnostic initialization failed; refusing to run' -or $taskLoaderLog -match 'HOME_GAMES_NATIVE_PASS Copied save is readable') { throw 'Fail-closed initialization was not confirmed.' }
    Set-Content -LiteralPath (Join-Path $taskEvidence 'passed.txt') -Value 'PASS: Invalid diagnostic root quits before loading a save; original save and production hashes unchanged.'
}
if (Test-Path -LiteralPath (Join-Path $taskEvidence 'failure.txt')) { throw (Get-Content -LiteralPath (Join-Path $taskEvidence 'failure.txt') -Raw) }
if (-not (Test-Path -LiteralPath (Join-Path $taskEvidence 'passed.txt'))) { throw "Native check did not finish; inspect $taskEvidence and the profile loader log." }
Get-Content -LiteralPath (Join-Path $taskEvidence 'passed.txt')
Write-Output "PASS: Original save and production DLL/preferences hashes unchanged. Evidence: $taskEvidence"
