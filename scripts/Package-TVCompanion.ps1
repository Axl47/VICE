param(
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Commit = ''
)
. (Join-Path $PSScriptRoot 'ReleasePackage.ps1')
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$expectedArchive = 'FD0F421D396BEC8E8425486D4B8B6552E8EA0D0AD9443875582118C246B87712'
$expectedHelper = '4D39A2EC63F1090E9C65C02F8A9DB457E518975113AE7C4CAFFD87594F3235AB'
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchive) {
    throw 'Use the inspected Build-A-Base 0.4.26 IL2CPP archive. Its SHA-256 does not match.'
}
$stage = New-ReleaseStage $OutputDirectory $Tag 'VICE-YouTube-Companion'
$prefix = 'UserData/BaseBuilder/TvBrowser/'
$zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entries = @($zip.Entries | Where-Object { $_.FullName.StartsWith($prefix, [StringComparison]::Ordinal) })
    if (-not $entries.Count -or $entries.Count -gt 1000 -or ($entries | Measure-Object Length -Sum).Sum -gt 700MB) { throw 'Invalid companion archive size.' }
    $seen = @{}
    foreach ($entry in $entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        if ($entry.FullName.Contains(':') -or $entry.FullName.Contains('\')) { throw 'Invalid companion archive path.' }
        $destination = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
        $companionRoot = [IO.Path]::GetFullPath((Join-Path $stage $prefix))
        if (-not $destination.StartsWith($companionRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($destination)) { throw 'Invalid or duplicated companion archive path.' }
        $seen[$destination] = $true
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
    }
} finally { $zip.Dispose() }
$browser = Join-Path $stage 'UserData\BaseBuilder\TvBrowser'
if ((Get-FileHash -LiteralPath (Join-Path $browser 'BuildABase.TvBrowser.dll') -Algorithm SHA256).Hash -ne $expectedHelper) { throw 'Extracted helper hash mismatch.' }
foreach ($required in @('BuildABase.TvBrowser.exe','libcef.dll','CefSharp.OffScreen.dll','licenses/CEF-LICENSE.txt','licenses/CefSharp-LICENSE.txt','licenses/Chromium-Credits.html')) {
    if (-not (Test-Path -LiteralPath (Join-Path $browser $required) -PathType Leaf)) { throw "Missing companion dependency: $required" }
}
@'
# Optional YouTube companion

Install the matching VICE suite first, then close Schedule I and the companion.
Extract this ZIP directly into the Schedule I game folder, next to Schedule I.exe.
The browser installs under UserData/BaseBuilder/TvBrowser; its licenses are retained.
This ZIP contains only the pinned Build-A-Base 0.4.26 TV companion and VICE metadata.
See VICE/docs/vice-lifestyle-youtube.md in the main suite for controls and limitations.
Keep the bundled licenses with the companion. Source inspection does not establish
author redistribution permission; see the repository provenance before sharing.
'@ | Set-Content -LiteralPath (Join-Path $stage 'VICE\YouTube-Companion.md') -Encoding utf8
Complete-ReleaseArchive $stage (Join-Path $OutputDirectory "VICE-YouTube-Companion-$Tag.zip") ([ordered]@{
    FormatVersion = 1; Kind = 'YouTubeCompanion'; Tag = $Tag; Commit = $Commit
    CompanionVersion = 'Build-A-Base 0.4.26 IL2CPP'; SourceSHA256 = $expectedArchive; HelperSHA256 = $expectedHelper
}) 'VICE/YouTube-Companion'
