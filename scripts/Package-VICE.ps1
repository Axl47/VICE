param(
    [ValidateSet('Commerce', 'Arsenal', 'Lifestyle', 'Interface', 'Enforcement', 'Syndicate')]
    [string[]]$Modules = @(),
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$names = @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')
$Modules = @($names | Where-Object { $_ -in $Modules } | Sort-Object)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
function Project-Version([string]$name) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot "src\VICE.$name\VICE.$name.csproj") -Raw
    return [string]$project.Project.PropertyGroup.Version
}
$versions = [ordered]@{ Core = (Project-Version 'Core') }
foreach ($module in $Modules) { $versions[$module] = Project-Version $module }
$label = if ($Modules.Count) { $Modules -join '-' } else { 'Core' }
# A new directory on every export prevents unselected DLLs leaking from old builds.
$package = [IO.Path]::GetFullPath((Join-Path $outputRoot ("VICE-$label-" + [Guid]::NewGuid().ToString('N').Substring(0,8))))
if (-not $package.StartsWith($outputRoot.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package escaped the output directory.' }
$libs = Join-Path $package 'UserLibs'
New-Item -ItemType Directory -Path $libs, (Join-Path $package 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination $libs
foreach ($module in $Modules) {
    $mods = Join-Path $package 'Mods'
    New-Item -ItemType Directory -Path $mods -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "src\VICE.$module\bin\Release\net6.0\VICE.$module.dll") -Destination $mods
    if ($module -in @('Enforcement','Syndicate')) {
        $defaults = Join-Path $package "UserData\VICE\$module"
        New-Item -ItemType Directory -Path $defaults, (Join-Path $package 'Licenses') -Force | Out-Null
        Copy-Item -Path (Join-Path $repoRoot "src\VICE.$module\Defaults\*") -Destination $defaults -Recurse
        $license = if ($module -eq 'Enforcement') { 'LICENSE.NACops' } else { 'LICENSE.CartelEnforcer' }
        $licenseName = if ($module -eq 'Enforcement') { 'NACops-MIT.txt' } else { 'CartelEnforcer-MIT.txt' }
        Copy-Item -LiteralPath (Join-Path $repoRoot "src\VICE.$module\$license") -Destination (Join-Path $package "Licenses\$licenseName")
    }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $package
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') -Filter '*.md' -File | Copy-Item -Destination (Join-Path $package 'docs')
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\README.md') -Destination (Join-Path $package 'docs\vice-core.md')
foreach ($installer in @('Install-VICE.ps1','Install-VICEModules.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $installer) -Destination $package }
if ($Modules -contains 'Lifestyle') { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-TVCompanion.ps1') -Destination $package }
[ordered]@{ FormatVersion = 1; Modules = @($Modules); Versions = $versions } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'package.json') -Encoding utf8
$manifest = @(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {
    [pscustomobject]@{ File = $_.FullName.Substring($package.Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
ConvertTo-Json -InputObject $manifest | Set-Content -LiteralPath (Join-Path $package 'hashes.json') -Encoding utf8
$zip = "$package.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip
[pscustomobject]@{ DirectoryPath = $package; ZipPath = $zip; Modules = @($Modules); CoreVersion = $versions.Core }
