param(
    [Parameter(Mandatory)][string]$Archive,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I'
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Schedule I', 'ScheduleI', 'BuildABase.TvBrowser' -ErrorAction SilentlyContinue) {
    throw 'Close the game and TV companion before installing.'
}
$gameRoot = (Resolve-Path -LiteralPath $GameDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'Schedule I.exe') -PathType Leaf)) { throw 'GameDir is not a Schedule I installation.' }
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
# Pin the inspected private dependency rather than silently accepting an unrelated latest ZIP.
$expectedArchive = 'FD0F421D396BEC8E8425486D4B8B6552E8EA0D0AD9443875582118C246B87712'
$expectedHelper = '4D39A2EC63F1090E9C65C02F8A9DB457E518975113AE7C4CAFFD87594F3235AB'
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchive) {
    throw 'Use the inspected Build-A-Base 0.4.26 IL2CPP archive. Its SHA-256 does not match.'
}
$baseRoot = [IO.Path]::GetFullPath((Join-Path $gameRoot 'UserData\BaseBuilder'))
$target = [IO.Path]::GetFullPath((Join-Path $baseRoot 'TvBrowser'))
if (-not $target.StartsWith($gameRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Companion target escaped the game.' }
if (Test-Path -LiteralPath $target) {
    $helper = Join-Path $target 'BuildABase.TvBrowser.dll'
    if ((Test-Path -LiteralPath $helper -PathType Leaf) -and (Get-FileHash -LiteralPath $helper).Hash -eq $expectedHelper) {
        Write-Output "The inspected TV companion is already installed: $target"
        return
    }
    throw 'A different TV companion installation exists. Preserve it before installing this version.'
}
$stage = [IO.Path]::GetFullPath((Join-Path $baseRoot ('TvBrowser.install-' + [Guid]::NewGuid().ToString('N'))))
if (-not $stage.StartsWith($baseRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging escaped the companion directory.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $prefix = 'UserData/BaseBuilder/TvBrowser/'
    $entries = @($zip.Entries | Where-Object { $_.FullName.StartsWith($prefix, [StringComparison]::Ordinal) })
    if (-not $entries.Count -or $entries.Count -gt 1000 -or ($entries | Measure-Object Length -Sum).Sum -gt 700MB) { throw 'Invalid companion archive size.' }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    foreach ($entry in $entries) {
        $relative = $entry.FullName.Substring($prefix.Length)
        if (-not $relative) { continue }
        if ($relative.Contains(':') -or $relative.Contains('\')) { throw 'Invalid companion archive path.' }
        $destination = [IO.Path]::GetFullPath((Join-Path $stage $relative))
        if (-not $destination.StartsWith($stage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Companion archive path escaped staging.' }
        if ($entry.FullName.EndsWith('/')) { New-Item -ItemType Directory -Path $destination -Force | Out-Null; continue }
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
    }
    if ((Get-FileHash -LiteralPath (Join-Path $stage 'BuildABase.TvBrowser.dll')).Hash -ne $expectedHelper) { throw 'Extracted helper hash mismatch.' }
    foreach ($required in @('BuildABase.TvBrowser.exe','libcef.dll','CefSharp.OffScreen.dll','licenses/CEF-LICENSE.txt','licenses/CefSharp-LICENSE.txt','licenses/Chromium-Credits.html')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stage $required) -PathType Leaf)) { throw "Missing dependency: $required" }
    }
    if (Get-Process -Name 'Schedule I', 'ScheduleI', 'BuildABase.TvBrowser' -ErrorAction SilentlyContinue) { throw 'Game or companion started during extraction.' }
    # Both final paths were resolved and checked above; the construction mod DLL is never extracted.
    Move-Item -LiteralPath $stage -Destination $target
    Write-Output "Installed TV companion: $target"
    Write-Output 'Build-A-Base gameplay was not installed. Companion licences are retained.'
} finally {
    $zip.Dispose()
    if (Test-Path -LiteralPath $stage) {
        if (-not $stage.StartsWith($baseRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging cleanup path.' }
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
