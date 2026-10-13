param([Parameter(Mandatory)][string]$Archive)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$fixture = Join-Path $repoRoot ('artifacts\companion-installer-' + [Guid]::NewGuid().ToString('N'))
try {
New-Item -ItemType Directory -Path $fixture | Out-Null
New-Item -ItemType File -Path (Join-Path $fixture 'Schedule I.exe') | Out-Null
$installer = Join-Path $repoRoot 'scripts\Install-TVCompanion.ps1'
$companion = Join-Path $fixture 'UserData\BaseBuilder\TvBrowser'
function Reject-Installation([string]$Expected, [string]$Source = $Archive) {
    $rejected = $false
    try { & $installer -GameDir $fixture -Archive $Source | Out-Null }
    catch {
        if ($_.Exception.Message -notlike $Expected) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Installer accepted invalid fixture: $Expected" }
}
function Missing-File([string]$Relative) {
    $file = [IO.Path]::GetFullPath((Join-Path $companion $Relative))
    if (-not $file.StartsWith($companion + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture path escaped.' }
    $saved = $file + '.fixture-backup'
    Move-Item -LiteralPath $file -Destination $saved
    try { Reject-Installation '*Incomplete companion installation*' }
    finally { Move-Item -LiteralPath $saved -Destination $file }
}
& $installer -GameDir $fixture -Archive $Archive | Out-Null
& $installer -GameDir $fixture -Archive $Archive | Out-Null
Missing-File 'BuildABase.TvBrowser.exe'
$cef = Join-Path $companion 'libcef.dll'
$cefHash = (Get-FileHash -LiteralPath $cef).Hash
$stream = [IO.File]::Open($cef, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
$firstByte = $stream.ReadByte()
$stream.Position = 0
$stream.WriteByte($firstByte -bxor 255)
$stream.Dispose()
try { Reject-Installation '*Different or corrupted companion file*' }
finally {
    $stream = [IO.File]::Open($cef, [IO.FileMode]::Open, [IO.FileAccess]::Write)
    try { $stream.WriteByte($firstByte) } finally { $stream.Dispose() }
}
if ((Get-FileHash -LiteralPath $cef).Hash -ne $cefHash) { throw 'Fixture restore failed.' }
Missing-File 'licenses\CEF-LICENSE.txt'
$wrongArchive = Join-Path $fixture 'wrong.zip'
[IO.File]::WriteAllBytes($wrongArchive, [byte[]]@(0))
Reject-Installation '*SHA-256 does not match*' $wrongArchive
Write-Output 'PASS: 6 companion installer checks: fresh, complete existing, missing executable, corrupted CEF, missing license, wrong archive.'
} finally {
    if (Test-Path -LiteralPath $fixture) {
        $cleanup = (Resolve-Path -LiteralPath $fixture).Path
        $artifacts = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
        if (-not $cleanup.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($cleanup) -notmatch '^companion-installer-[a-f0-9]{32}$') {
            throw 'Fixture cleanup escaped the intended artifacts directory.'
        }
        Remove-Item -LiteralPath $cleanup -Recurse -Force
    }
}
