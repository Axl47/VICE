param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$PackageDir = '',
    [ValidateSet('Commerce', 'Arsenal', 'Lifestyle', 'Interface')]
    [string[]]$Modules = @('Commerce', 'Arsenal', 'Lifestyle', 'Interface')
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $PackageDir) {
    $PackageDir = Join-Path $repoRoot 'artifacts\VICE.Modules-0.1.0'
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'hashes.json')) { $PackageDir = $PSScriptRoot }
}
$gameRoot = (Resolve-Path -LiteralPath $GameDir).Path
$packageRoot = (Resolve-Path -LiteralPath $PackageDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'Schedule I.exe'))) { throw 'GameDir is not a Schedule I installation.' }
$gameExecutable = Join-Path $gameRoot 'Schedule I.exe'
foreach ($process in Get-Process -Name 'Schedule I', 'ScheduleI' -ErrorAction SilentlyContinue) {
    try { $runningPath = $process.Path } catch { $runningPath = $null }
    if (-not $runningPath -or $runningPath -ieq $gameExecutable) { throw 'Close this Schedule I installation before installing VICE modules.' }
}
$Modules = @($Modules | Select-Object -Unique)
if ($Modules.Count -eq 0) { throw 'Select at least one VICE module.' }
if ($Modules -contains 'Lifestyle' -and -not (Test-Path -LiteralPath (Join-Path $gameRoot 'Mods\S1API.Il2Cpp.MelonLoader.dll'))) {
    throw 'Install the IL2CPP S1API dependency before installing VICE Lifestyle.'
}
$legacy = @{
    Commerce = @('Personal.Highballer.dll', 'Personal.GasPurchases.dll', 'Personal.Warehouse.dll', 'HighBaller.dll', 'HighBaller_IL2CPP.dll', 'WareHouseAlwaysOpen.dll')
    Arsenal = @('Personal.MoreGuns.dll', 'MoreGuns.dll')
    Lifestyle = @('Personal.Casino.dll', 'Personal.TV.dll', 'Personal.TVProof.dll')
    Interface = @('Personal.UIEnhancements.dll')
}
$files = @('UserLibs\VICE.Core.dll') + @($Modules | ForEach-Object { "Mods\VICE.$_.dll" })
$manifest = @(Get-Content -LiteralPath (Join-Path $packageRoot 'hashes.json') -Raw | ConvertFrom-Json)
foreach ($relative in $files) {
    $source = Join-Path $packageRoot $relative
    $record = @($manifest | Where-Object { $_.File -eq $relative })
    if ($record.Count -ne 1 -or -not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing packaged file or unique hash: $relative" }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $record[0].SHA256) { throw "Package hash mismatch: $relative" }
}
# Only explicitly selected modules, their predecessors and the shared library are touched.
$previous = @($files) + @($Modules | ForEach-Object { $legacy[$_] } | ForEach-Object { 'Mods\' + $_ }) + @('Mods\VICE.Core.dll')
$previous = @($previous | Select-Object -Unique)
$backup = Join-Path $repoRoot ('artifacts\install-backups\VICE.Modules-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N'))
$present = @{}
foreach ($relative in $previous) {
    $target = Join-Path $gameRoot $relative
    $present[$relative] = Test-Path -LiteralPath $target -PathType Leaf
    if ($present[$relative]) {
        $saved = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
    }
}
try {
    foreach ($relative in $files) {
        $target = Join-Path $gameRoot $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $packageRoot $relative) -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $packageRoot $relative) -Algorithm SHA256).Hash) {
            throw "Installed hash mismatch: $relative"
        }
    }
    foreach ($relative in $previous | Where-Object { $_ -notin $files }) {
        $target = Join-Path $gameRoot $relative
        if ($present[$relative]) { Remove-Item -LiteralPath $target }
    }
}
catch {
    $failure = $_
    foreach ($relative in $previous) {
        $target = Join-Path $gameRoot $relative
        if ($present[$relative]) { Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination $target -Force }
        elseif ($relative -in $files -and (Test-Path -LiteralPath $target -PathType Leaf)) { Remove-Item -LiteralPath $target }
    }
    throw $failure
}
Write-Output "Installed VICE $($Modules -join ', ') and shared UserLibs\VICE.Core.dll. Previous DLLs backed up outside Mods: $backup"
