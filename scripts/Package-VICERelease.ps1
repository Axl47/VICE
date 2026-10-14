param(
    [Parameter(Mandatory)][string]$PackageDir,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Commit = ''
)
. (Join-Path $PSScriptRoot 'ReleasePackage.ps1')
$repoRoot = Split-Path $PSScriptRoot -Parent
$modules = @('Arsenal','Commerce','Enforcement','Interface','Lifestyle','Substances','Syndicate')
& (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $PackageDir -ExpectedModules $modules -ExpectedAddOns YouTube
$source = (Resolve-Path -LiteralPath $PackageDir).Path
$stage = New-ReleaseStage $OutputDirectory $Tag 'VICE'
foreach ($folder in @('Mods','UserLibs','Licenses')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $stage -Recurse
}
Copy-Item -LiteralPath (Join-Path $source 'docs') -Destination (Join-Path $stage 'VICE\docs') -Recurse
# Defaults are embedded and seeded when missing. Do not overwrite user settings on unzip.
@'
# Install VICE

Close Schedule I. Install MelonLoader 0.7.3 and compatible IL2CPP S1API first.
This suite targets the Steam beta / IL2CPP branch; it does not support Mono.
Extract the contents of this ZIP directly into the Schedule I game folder.
Mods/ and UserLibs/ must be next to Schedule I.exe, with no extra enclosing folder.

Before upgrading from predecessor mods, remove their replaced DLLs from Mods:
Personal.Highballer, Personal.GasPurchases, Personal.Warehouse, HighBaller,
HighBaller_IL2CPP, WareHouseAlwaysOpen, Personal.MoreGuns, MoreGuns,
Personal.Casino, Personal.TV, Personal.TVProof, Personal.UIEnhancements,
Personal.BetterPolice, BetterPolice, NACops, NACops-IL2Cpp, Personal.NACops,
Personal.LSDGameplay, Personal.LSDPreview and the original Cartel Enforcer.
Keep one VICE.Core.dll in UserLibs; remove any duplicate from Mods.
Keep existing preferences, UserData and saves. Defaults are created only when missing.
All multiplayer participants need matching suite binaries and compatible S1API.
See VICE/docs for feature guides, provenance and remaining playtest limitations.

YouTube is optional. Its small VICE.YouTube.dll adapter is included here.
Extract the separate VICE-YouTube-Companion ZIP into this same game folder to
enable it. Images, local MP4s, direct video URLs and Relay work without it.
To disable the adapter, close the game and remove Mods/VICE.YouTube.dll.
The companion ZIP contains no Build-A-Base gameplay mod.
'@ | Set-Content -LiteralPath (Join-Path $stage 'VICE\README.md') -Encoding utf8
$metadata = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
Complete-ReleaseArchive $stage (Join-Path $OutputDirectory "VICE-$Tag.zip") ([ordered]@{
    FormatVersion = 1; Kind = 'Suite'; Tag = $Tag; Commit = $Commit
    Modules = $modules; AddOns = @('YouTube'); Versions = $metadata.Versions
    Runtime = 'Windows x64 / Schedule I beta IL2CPP / MelonLoader 0.7.3 / compatible S1API'
})
