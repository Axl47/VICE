# Shared helpers for the two drop-in release archives. No installed game files are changed.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ReleaseStage([string]$OutputDirectory, [string]$Tag, [string]$Kind) {
    if ($Tag -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$') {
        throw 'Release tags must contain 1-80 letters, digits, dots, underscores or hyphens, starting with a letter or digit.'
    }
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $stage = Join-Path $output ("$Kind-$Tag-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $stage 'VICE') -Force | Out-Null
    return $stage
}

function Complete-ReleaseArchive([string]$Stage, [string]$ZipPath, $Metadata, [string]$MetadataDirectory = 'VICE') {
    New-Item -ItemType Directory -Path (Join-Path $Stage $MetadataDirectory) -Force | Out-Null
    $Metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Stage "$MetadataDirectory/release.json") -Encoding utf8
    $manifestPath = [IO.Path]::GetFullPath((Join-Path $Stage "$MetadataDirectory/hashes.json"))
    $manifest = @(Get-ChildItem -LiteralPath $Stage -Recurse -File | Where-Object FullName -ne $manifestPath | Sort-Object FullName | ForEach-Object {
        [ordered]@{ File = $_.FullName.Substring($Stage.Length + 1).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    ConvertTo-Json -InputObject $manifest -Depth 4 | Set-Content -LiteralPath (Join-Path $Stage "$MetadataDirectory/hashes.json") -Encoding utf8
    if (Test-Path -LiteralPath $ZipPath) { throw "Release asset already exists. Use a fresh output directory: $ZipPath" }
    [IO.Compression.ZipFile]::CreateFromDirectory($Stage, $ZipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    return [pscustomobject]@{ DirectoryPath = $Stage; ZipPath = $ZipPath }
}
