param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$package = Join-Path $repoRoot 'artifacts\PersonalMods-0.3.1\Mods'
$gameRoot = (Resolve-Path -LiteralPath $GameDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'Schedule I.exe'))) { throw 'GameDir is not a Schedule I installation.' }
if (Get-Process -Name 'Schedule I', 'ScheduleI' -ErrorAction SilentlyContinue) { throw 'Close Schedule I before replacing mod DLLs.' }
$destination = Join-Path $gameRoot 'Mods'
$names = @('Personal.Warehouse.dll', 'Personal.BetterPolice.dll', 'Personal.Highballer.dll', 'Personal.Casino.dll', 'Personal.MoreGuns.dll', 'Personal.GasPurchases.dll', 'Personal.UIEnhancements.dll')
foreach ($name in $names) { if (-not (Test-Path -LiteralPath (Join-Path $package $name))) { throw "Build the package first; missing $name" } }
$conflicts = @('WareHouseAlwaysOpen.dll', 'BetterPolice.dll', 'HighBaller_IL2CPP.dll', 'HighBaller.dll', 'MoreGuns.dll') | Where-Object { Test-Path -LiteralPath (Join-Path $destination $_) }
if ($conflicts) { throw "Old conflicting DLLs are installed: $($conflicts -join ', '). Move them to a backup outside Mods first." }
$null = New-Item -ItemType Directory -Path $destination -Force
$backup = Join-Path $repoRoot ('artifacts\install-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($name in $names) {
    $target = Join-Path $destination $name
    if (Test-Path -LiteralPath $target) {
        $null = New-Item -ItemType Directory -Path $backup -Force
        Copy-Item -LiteralPath $target -Destination $backup
    }
    Copy-Item -LiteralPath (Join-Path $package $name) -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath (Join-Path $package $name)).Hash) { throw "Installed hash does not match: $name" }
}
Write-Output "Installed seven verified DLLs into $destination. Existing S1API and TheRegulars were retained."
