$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$package = Join-Path $repoRoot 'artifacts\PersonalMods-0.2.2'
$mods = Join-Path $package 'Mods'
$docs = Join-Path $package 'docs'
$null = New-Item -ItemType Directory -Path $mods, $docs -Force
foreach ($module in @('Warehouse', 'BetterPolice', 'Highballer', 'Casino', 'MoreGuns', 'GasPurchases')) {
    $dll = Join-Path $repoRoot "src\$module\bin\Release\net6.0\Personal.$module.dll"
    if (-not (Test-Path -LiteralPath $dll)) { throw "Build all projects before packaging; missing $dll" }
    Copy-Item -LiteralPath $dll -Destination $mods -Force
}
$readme = Join-Path $package 'README.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $readme -Force
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') -Filter '*.md' | Copy-Item -Destination $docs -Force
$hashes = Get-ChildItem -LiteralPath $mods -Filter '*.dll' | Get-FileHash -Algorithm SHA256 | Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}}, Hash
if (@($hashes).Count -ne 6) { throw 'The package Mods folder must contain exactly the six personal DLLs.' }
$hashFile = Join-Path $package 'hashes.json'
$hashes | ConvertTo-Json | Set-Content -LiteralPath $hashFile -Encoding utf8
$zip = "$package.zip"
Compress-Archive -LiteralPath $mods, $docs, $readme, $hashFile -DestinationPath $zip -Force
Write-Output "Personal package: $zip"
