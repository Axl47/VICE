param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [ValidateSet('Menu', 'Migration')][string]$Mode = 'Menu',
    [string]$Save5 = '',
    [string]$CandidateDll = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $CandidateDll) { $CandidateDll = Join-Path $repoRoot 'src\VICE.Substances\bin\Release\net6.0\VICE.Substances.dll' }
if (Get-Process -Name 'Schedule I','ScheduleI' -ErrorAction SilentlyContinue) { throw 'Close the game before running isolated Substances checks.' }
function Tree-Hashes([string]$root) {
    @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($root.Length); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }) | ConvertTo-Json -Compress
}
$root = Join-Path $repoRoot ('artifacts\substances-native-' + $Mode.ToLowerInvariant() + '-' + [Guid]::NewGuid().ToString('N'))
$profile = Join-Path $root 'Profile'
$evidence = Join-Path $root 'Evidence'
New-Item -ItemType Directory -Path $profile,$evidence,(Join-Path $profile 'Mods'),(Join-Path $profile 'UserLibs'),(Join-Path $profile 'UserData'),(Join-Path $profile 'Plugins') -Force | Out-Null
if ($Mode -eq 'Migration') {
    if (-not $Save5) { throw 'Migration mode requires an existing fifth-slot save with workshop data via -Save5.' }
    $Save5 = (Resolve-Path -LiteralPath $Save5).Path
    if (-not (Test-Path -LiteralPath (Join-Path $Save5 'Modded\OffRegister\Workshop.json'))) { throw 'The migration source has no predecessor workshop data.' }
    $saveBefore = Tree-Hashes $Save5
    # Always rename the COPY to the guarded fifth-slot path; the source is read-only.
    Copy-Item -LiteralPath $Save5 -Destination (Join-Path $evidence 'SaveGame_5') -Recurse
}
$productionRoots = @('Mods','UserLibs','UserData') | ForEach-Object { Join-Path $GameDir $_ } | Where-Object { Test-Path -LiteralPath $_ }
$productionBefore = @($productionRoots | ForEach-Object { Tree-Hashes $_ })
Copy-Item -LiteralPath (Join-Path $GameDir 'MelonLoader') -Destination $profile -Recurse
Remove-Item -LiteralPath (Join-Path $profile 'MelonLoader\Latest.log') -ErrorAction SilentlyContinue
Copy-Item -LiteralPath (Join-Path $GameDir 'Mods\S1API.Il2Cpp.MelonLoader.dll') -Destination (Join-Path $profile 'Mods')
Copy-Item -LiteralPath $CandidateDll -Destination (Join-Path $profile 'Mods\VICE.Substances.dll')
$candidateHash = (Get-FileHash -LiteralPath (Join-Path $profile 'Mods\VICE.Substances.dll') -Algorithm SHA256).Hash
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination (Join-Path $profile 'UserLibs')
@'
[PersonalLSDPreview]
Intensity = 0.27
AudioStrength = 0.23
DefaultsRevision = 6
'@ | Set-Content -LiteralPath (Join-Path $profile 'UserData\MelonPreferences.cfg') -Encoding utf8
$checks = if ($Mode -eq 'Menu') { '--lsd-render-check --lsd-audio-check' } else {
    '--lsd-slice-check --lsd-migration-check --lsd-isolated-save="' + (Join-Path $evidence 'SaveGame_5') + '" --lsd-check-output="' + $evidence + '"'
}
$arguments = '--melonloader.basedir "' + $profile + '" --melonloader.hideconsole ' + $checks + ' -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + (Join-Path $evidence 'unity.log') + '"'
Write-Output "Isolated Substances check: $root"
$process = Start-Process -FilePath (Join-Path $GameDir 'Schedule I.exe') -WorkingDirectory $GameDir -ArgumentList $arguments -WindowStyle Hidden -PassThru
Set-Content -LiteralPath (Join-Path $evidence 'process-id.txt') -Value $process.Id
try {
    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 2
        $process.Refresh()
        $logPath = Join-Path $profile 'MelonLoader\Latest.log'
        $log = if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Raw } else { '' }
        if (Test-Path -LiteralPath (Join-Path $evidence 'failure.txt')) { Stop-Process -Id $process.Id; break }
        if ($Mode -eq 'Menu' -and $log -match 'LSD FINAL-FRAME flow check PASSED' -and $log -match 'LSD closed-eye shader check PASSED' -and $log -match 'LSD final-listener echo check PASSED' -and $log -match 'original native bus routes restored True') {
            Stop-Process -Id $process.Id
            break
        }
        if ((Get-Date) -gt $deadline) { Stop-Process -Id $process.Id; throw "Substances $Mode check timed out. Inspect $logPath" }
    } while (-not $process.HasExited)
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    $productionAfter = @($productionRoots | ForEach-Object { Tree-Hashes $_ })
    $productionUnchanged = @(Compare-Object $productionBefore $productionAfter).Count -eq 0
    $saveUnchanged = $Mode -ne 'Migration' -or $saveBefore -eq (Tree-Hashes $Save5)
    [ordered]@{ ProductionUnchanged = $productionUnchanged; SourceSaveUnchanged = $saveUnchanged; CandidateSHA256 = $candidateHash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'isolation-proof.json')
    if (-not $productionUnchanged -or -not $saveUnchanged) { throw 'Substances isolation hash check failed.' }
}
$log = Get-Content -LiteralPath (Join-Path $profile 'MelonLoader\Latest.log') -Raw
if ($Mode -eq 'Migration') {
    if (-not (Test-Path -LiteralPath (Join-Path $evidence 'migration-passed.txt')) -or $log -notmatch 'VICE_SUBSTANCES_MIGRATION_PASSED') { throw "Migration check did not pass. Inspect $evidence" }
} elseif ($log -notmatch 'LSD FINAL-FRAME flow check PASSED' -or $log -notmatch 'LSD closed-eye shader check PASSED' -or $log -notmatch 'LSD final-listener echo check PASSED') { throw "Menu checks did not pass. Inspect $evidence" }
if ($log -match 'LSD_SLICE_CHECK_FAILED|LSD_SLICE_CHECK failure|LSD final-listener echo check FAILED|cannot initialize alongside|Failed to (load|initialize)|Exception:|\[ERROR\]') { throw 'Substances loader log contains a validation failure.' }
Set-Content -LiteralPath (Join-Path $evidence 'passed.txt') -Value "PASS: Substances $Mode checks; original save and production files unchanged."
Write-Output "PASS: Substances $Mode checks. Evidence: $evidence"
