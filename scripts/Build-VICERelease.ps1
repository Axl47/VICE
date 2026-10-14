param(
    [Parameter(Mandatory)][string]$Tag,
    [string]$CompanionArchive = $env:VICE_TV_COMPANION_ARCHIVE,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$AssetSourceDirectory = $env:VICE_ASSET_SOURCE_DIR,
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$OutputDirectory = '',
    [string]$Commit = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
# Keep a clean runner's restore cache inside its checkout, independently of the
# account's global NuGet configuration and the developer's ignored local config.
$env:NUGET_PACKAGES = Join-Path $repoRoot '.tools\nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-plugins'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
if ($Tag -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$') { throw 'Use a filename-safe release tag, for example v0.3.0 or v0.3.0-beta.1.' }
if (-not $CompanionArchive -or -not (Test-Path -LiteralPath $CompanionArchive -PathType Leaf)) { throw 'Set VICE_TV_COMPANION_ARCHIVE to the inspected Build-A-Base 0.4.26 ZIP.' }
if ((Get-FileHash -LiteralPath $CompanionArchive -Algorithm SHA256).Hash -ne 'FD0F421D396BEC8E8425486D4B8B6552E8EA0D0AD9443875582118C246B87712') { throw 'TV companion archive hash mismatch.' }
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'src\VICE.Substances\VICE.Substances.csproj'))) { throw 'The VICE.Substances migration must be present before building the full-suite release.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts\release-' + [Guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force)) { throw 'Use an empty release output directory to prevent stale assets.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& (Join-Path $PSScriptRoot 'Test-VICEReleaseFixtures.ps1')
if ($AssetSourceDirectory) {
    # Only ignored build inputs are copied from the development checkout. Source,
    # game references, outputs and local NuGet configuration remain tag-specific.
    foreach ($relative in @('.research\assets\moreguns.bundle','src\VICE.Lifestyle\Assets\homegames.bundle','src\VICE.Lifestyle\Assets\homegames.contract.json')) {
        $source = [IO.Path]::GetFullPath((Join-Path $AssetSourceDirectory $relative))
        $target = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
        if ($source -ne $target -and (Test-Path -LiteralPath $source -PathType Leaf)) {
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
        }
    }
}
$buildArgs = @{ All = $true; AddOns = @('YouTube'); GameDir = $GameDir; OriginalMoreGuns = $OriginalMoreGuns; OutputDirectory = (Join-Path $OutputDirectory 'checked') }
if ($DotnetPath) { $buildArgs.DotnetPath = $DotnetPath }
$package = @(& (Join-Path $PSScriptRoot 'Build-VICE.ps1') @buildArgs | ForEach-Object {
    if ($_.PSObject.Properties['DirectoryPath']) { $_ } else { Write-Host $_ }
})
if ($package.Count -ne 1) { throw 'Expected one checked VICE export.' }
$suite = & (Join-Path $PSScriptRoot 'Package-VICERelease.ps1') -PackageDir $package[0].DirectoryPath -Tag $Tag -Commit $Commit -OutputDirectory $OutputDirectory
$companion = & (Join-Path $PSScriptRoot 'Package-TVCompanion.ps1') -Archive $CompanionArchive -Tag $Tag -Commit $Commit -OutputDirectory $OutputDirectory
& (Join-Path $PSScriptRoot 'Test-VICERelease.ps1') -ZipPath $suite.ZipPath -Kind Suite
& (Join-Path $PSScriptRoot 'Test-VICERelease.ps1') -ZipPath $companion.ZipPath -Kind YouTubeCompanion
$assets = @($suite.ZipPath, $companion.ZipPath)
$sums = @($assets | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) })
Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Value $sums -Encoding ascii
@"
Download **VICE-$Tag.zip** for all seven VICE gameplay modules, Core and the small YouTube adapter.
Download **VICE-YouTube-Companion-$Tag.zip** only if you want YouTube playback (roughly 489 MiB extracted).

Close the game and extract each chosen ZIP directly into the Schedule I folder, next to Schedule I.exe.
Install MelonLoader 0.7.3 and compatible IL2CPP S1API separately. Steam beta / IL2CPP only.
Remove replaced predecessor mod DLLs before upgrading; see VICE/README.md in the main ZIP.
Existing preferences, settings and saves are preserved. Every multiplayer participant needs matching binaries.

The companion retains its upstream licenses and does not install Build-A-Base gameplay.
See the bundled feature guides and provenance for credits, compatibility and remaining playtest limitations.
Release source: $Commit
"@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'release-notes.md') -Encoding utf8
Write-Host "Release assets ready: $OutputDirectory"
[pscustomobject]@{ DirectoryPath = $OutputDirectory; SuiteZip = $suite.ZipPath; CompanionZip = $companion.ZipPath }
