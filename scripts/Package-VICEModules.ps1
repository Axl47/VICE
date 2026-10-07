$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$package = Join-Path $repoRoot 'artifacts\VICE.Modules-0.1.0'
$mods = Join-Path $package 'Mods'
$libs = Join-Path $package 'UserLibs'
$docs = Join-Path $package 'docs'
$coreDocs = Join-Path $package 'src\VICE.Core'
New-Item -ItemType Directory -Path $mods, $libs, $docs, $coreDocs -Force | Out-Null
$files = @('UserLibs\VICE.Core.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination $libs -Force
foreach ($module in @('Commerce', 'Arsenal', 'Lifestyle', 'Interface')) {
    $relative = "Mods\VICE.$module.dll"
    Copy-Item -LiteralPath (Join-Path $repoRoot "src\VICE.$module\bin\Release\net6.0\VICE.$module.dll") -Destination (Join-Path $package $relative) -Force
    $files += $relative
}
if (@(Get-ChildItem -LiteralPath $mods -File).Count -ne 4 -or @(Get-ChildItem -LiteralPath $libs -File).Count -ne 1) {
    throw 'The VICE package must contain exactly four gameplay DLLs and one shared Core DLL.'
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $package -Force
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') -Filter '*.md' -File | Copy-Item -Destination $docs -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\README.md') -Destination $coreDocs -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -Destination $package -Force
$files | ForEach-Object {
    [pscustomobject]@{ File = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $package $_) -Algorithm SHA256).Hash }
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'hashes.json') -Encoding utf8
$zip = "$package.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
Write-Output "VICE package: $zip"
