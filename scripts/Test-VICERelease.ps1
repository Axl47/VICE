param([Parameter(Mandatory)][string]$ZipPath, [Parameter(Mandatory)][ValidateSet('Suite','YouTubeCompanion')][string]$Kind)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
function Read-EntryText($Entry) {
    if (-not $Entry) { throw 'Missing release metadata.' }
    $reader = [IO.StreamReader]::new($Entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}
try {
    $entries = @{}
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        if ($entry.FullName -match '(^/|\\|:|(^|/)\.\.(/|$))' -or $entries.ContainsKey($entry.FullName)) { throw 'Invalid or duplicated release ZIP path.' }
        $entries[$entry.FullName] = $entry
    }
    $metadataDirectory = if ($Kind -eq 'Suite') { 'VICE' } else { 'VICE/YouTube-Companion' }
    $metadata = Read-EntryText $entries["$metadataDirectory/release.json"] | ConvertFrom-Json
    if ($metadata.Kind -ne $Kind -or $metadata.FormatVersion -ne 1) { throw 'Unexpected release metadata.' }
    $manifestFile = "$metadataDirectory/hashes.json"
    $manifest = @(Read-EntryText $entries[$manifestFile] | ConvertFrom-Json)
    $seen = @{}
    foreach ($record in $manifest) {
        if ($seen.ContainsKey($record.File) -or $record.File -eq $manifestFile -or -not $entries.ContainsKey($record.File)) { throw 'Invalid release hash manifest.' }
        $seen[$record.File] = $true
        $stream = $entries[$record.File].Open()
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $hasher.Dispose() }
        if ($hash -ne $record.SHA256) { throw "Release hash mismatch: $($record.File)" }
    }
    if ($seen.Count -ne $entries.Count - 1) { throw 'Release contains unhashed files.' }
    if ($Kind -eq 'Suite') {
        $expected = @('UserLibs/VICE.Core.dll') + @('Arsenal','Commerce','Enforcement','Interface','Lifestyle','Substances','Syndicate','YouTube' | ForEach-Object { "Mods/VICE.$_.dll" })
        $actual = @($entries.Keys | Where-Object { $_ -like '*.dll' })
        if (@(Compare-Object $expected $actual).Count) { throw 'Suite must contain exactly Core, seven gameplay modules and YouTube.' }
        if ($entries.Keys | Where-Object { $_ -notmatch '^(Mods/|UserLibs/|Licenses/|VICE/)' }) { throw 'Suite contains game dependencies or user data.' }
    } else {
        $prefix = 'UserData/BaseBuilder/TvBrowser/'
        if ($entries.Keys | Where-Object { -not $_.StartsWith($prefix, [StringComparison]::Ordinal) -and $_ -notlike 'VICE/*' }) { throw 'Companion contains unrelated gameplay or game files.' }
        foreach ($required in @('BuildABase.TvBrowser.dll','BuildABase.TvBrowser.exe','libcef.dll','CefSharp.OffScreen.dll','licenses/CEF-LICENSE.txt','licenses/CefSharp-LICENSE.txt','licenses/Chromium-Credits.html')) {
            if (-not $entries.ContainsKey($prefix + $required)) { throw "Companion is missing $required" }
        }
    }
    Write-Host "Verified $Kind release ZIP: root layout, exact payload and $($seen.Count) file hashes."
} finally { $zip.Dispose() }
