param([Parameter(Mandatory)][string]$PackageDir, [string[]]$ExpectedModules = @())
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackageDir).Path
$metadata = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw | ConvertFrom-Json
if ($metadata.FormatVersion -ne 1) { throw 'Unsupported VICE package format.' }
$expected = @('UserLibs\VICE.Core.dll') + @($ExpectedModules | Sort-Object -Unique | ForEach-Object { "Mods\VICE.$_.dll" })
$actual = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.dll' | ForEach-Object { $_.FullName.Substring($root.Length + 1) })
if (@(Compare-Object $expected $actual).Count) { throw 'Package does not contain exactly Core and the selected modules.' }
if (@(Compare-Object @($ExpectedModules | Sort-Object -Unique) @($metadata.Modules | Sort-Object)).Count) { throw 'Package module metadata differs from the selection.' }
foreach ($module in @('Enforcement','Syndicate')) {
    if (Test-Path -LiteralPath (Join-Path $root "UserData\VICE.$module")) { throw 'Package uses a legacy top-level module data folder.' }
    $defaults = Join-Path $root "UserData\VICE\$module"
    if ($module -in $ExpectedModules) {
        if (-not (Test-Path -LiteralPath (Join-Path $defaults 'config.json') -PathType Leaf)) { throw "Missing VICE/$module defaults." }
    } elseif (Test-Path -LiteralPath $defaults) { throw 'Package contains defaults for an unselected module.' }
}
foreach ($name in @('Core') + $ExpectedModules) {
    $relative = if ($name -eq 'Core') { 'UserLibs\VICE.Core.dll' } else { "Mods\VICE.$name.dll" }
    $actualVersion = [version][Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $relative)).FileVersion
    if ($actualVersion.ToString(3) -ne $metadata.Versions.$name) { throw "Package version metadata differs from built DLL: $name" }
}
$manifest = @(Get-Content -LiteralPath (Join-Path $root 'hashes.json') -Raw | ConvertFrom-Json)
$seen = @{}
foreach ($record in $manifest) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $record.File))
    if (-not $source.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($source)) { throw 'Invalid or duplicated package manifest path.' }
    $seen[$source] = $true
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $record.SHA256) { throw "Package hash mismatch: $($record.File)" }
}
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object Name -ne 'hashes.json') {
    if (-not $seen.ContainsKey($file.FullName)) { throw "Unhashed package file: $($file.FullName)" }
}
Write-Host "Verified export: one Core and $($ExpectedModules.Count) selected modules; all files hashed."
