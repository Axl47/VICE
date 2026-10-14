$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleasePackage.ps1')
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifacts = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$fixture = Join-Path $artifacts ('release-fixtures-' + [Guid]::NewGuid().ToString('N'))
$validator = Join-Path $PSScriptRoot 'Test-VICERelease.ps1'
$count = 0
function Reject-Release([string]$ZipPath, [string]$Expected) {
    $rejected = $false
    try { & $validator -ZipPath $ZipPath -Kind Suite }
    catch {
        if ($_.Exception.Message -notlike $Expected) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Validator accepted invalid fixture: $Expected" }
    $script:count++
}
function Mutate-Zip([string]$Source, [string]$Name, [scriptblock]$Mutation) {
    $target = Join-Path $fixture "$Name.zip"
    Copy-Item -LiteralPath $Source -Destination $target
    $archive = [IO.Compression.ZipFile]::Open($target, [IO.Compression.ZipArchiveMode]::Update)
    try { & $Mutation $archive } finally { $archive.Dispose() }
    return $target
}
try {
    $stage = New-ReleaseStage $fixture 'fixture' 'VICE'
    $expected = @('UserLibs/VICE.Core.dll') + @('Arsenal','Commerce','Enforcement','Interface','Lifestyle','Substances','Syndicate','YouTube' | ForEach-Object { "Mods/VICE.$_.dll" })
    foreach ($relative in $expected) {
        $file = Join-Path $stage $relative
        New-Item -ItemType Directory -Path (Split-Path $file -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($file, "payload fixture: $relative")
    }
    $valid = Complete-ReleaseArchive $stage (Join-Path $fixture 'valid.zip') ([ordered]@{ FormatVersion = 1; Kind = 'Suite'; Tag = 'fixture' })
    & $validator -ZipPath $valid.ZipPath -Kind Suite
    $count++
    $corrupt = Mutate-Zip $valid.ZipPath 'corrupt' {
        param($archive)
        $archive.GetEntry('Mods/VICE.Substances.dll').Delete()
        $writer = [IO.StreamWriter]::new($archive.CreateEntry('Mods/VICE.Substances.dll').Open())
        try { $writer.Write('changed after hashing') } finally { $writer.Dispose() }
    }
    Reject-Release $corrupt '*Release hash mismatch*'
    $missing = Mutate-Zip $valid.ZipPath 'missing' { param($archive) $archive.GetEntry('UserLibs/VICE.Core.dll').Delete() }
    Reject-Release $missing '*Invalid release hash manifest*'
    $extra = Mutate-Zip $valid.ZipPath 'extra' { param($archive) $null = $archive.CreateEntry('Mods/Assembly-CSharp.dll') }
    Reject-Release $extra '*unhashed files*'
    $duplicate = Mutate-Zip $valid.ZipPath 'duplicate' { param($archive) $null = $archive.CreateEntry('Mods/VICE.Substances.dll') }
    Reject-Release $duplicate '*duplicated release ZIP path*'
    $traversal = Mutate-Zip $valid.ZipPath 'traversal' { param($archive) $null = $archive.CreateEntry('../escaped.dll') }
    Reject-Release $traversal '*Invalid or duplicated release ZIP path*'
    $nested = Mutate-Zip $valid.ZipPath 'nested' { param($archive) $archive.GetEntry('VICE/release.json').Delete(); $null = $archive.CreateEntry('Wrapper/VICE/release.json') }
    Reject-Release $nested '*Missing release metadata*'
    # Correctly hashed but unselected DLLs/settings must still be rejected.
    [IO.File]::WriteAllText((Join-Path $stage 'Mods\Personal.LSDGameplay.dll'), 'predecessor')
    $wrongDlls = Complete-ReleaseArchive $stage (Join-Path $fixture 'wrong-dlls.zip') ([ordered]@{ FormatVersion = 1; Kind = 'Suite'; Tag = 'fixture' })
    Reject-Release $wrongDlls.ZipPath '*exactly Core*'
    Remove-Item -LiteralPath (Join-Path $stage 'Mods\Personal.LSDGameplay.dll')
    New-Item -ItemType Directory -Path (Join-Path $stage 'UserData\VICE\Enforcement') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $stage 'UserData\VICE\Enforcement\config.json'), '{}')
    $settings = Complete-ReleaseArchive $stage (Join-Path $fixture 'settings.zip') ([ordered]@{ FormatVersion = 1; Kind = 'Suite'; Tag = 'fixture' })
    Reject-Release $settings.ZipPath '*user data*'
    $badArchive = Join-Path $fixture 'bad-companion.zip'
    [IO.File]::WriteAllBytes($badArchive, [byte[]]@(0))
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'Package-TVCompanion.ps1') -Archive $badArchive -Tag fixture -OutputDirectory $fixture | Out-Null }
    catch { if ($_.Exception.Message -notlike '*SHA-256 does not match*') { throw }; $rejected = $true }
    if (-not $rejected) { throw 'Companion packager accepted an unrecognized archive.' }
    $count++
    Write-Host "PASS: $count release packaging fixture checks."
} finally {
    if (Test-Path -LiteralPath $fixture) {
        $cleanup = (Resolve-Path -LiteralPath $fixture).Path
        if (-not $cleanup.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($cleanup) -notmatch '^release-fixtures-[a-f0-9]{32}$') { throw 'Unsafe release fixture cleanup path.' }
        Remove-Item -LiteralPath $cleanup -Recurse -Force
    }
}
