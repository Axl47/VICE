# Build and install from source

Builds require **.NET SDK 8 or newer**, a local Schedule I **beta / IL2CPP** installation, and MelonLoader-generated assemblies. Launch with MelonLoader once to create `MelonLoader/Il2CppAssemblies`. Lifestyle and Substances require compatible IL2CPP S1API.

Arsenal requires an authorized MoreGuns asset bundle supplied locally through `-OriginalMoreGuns` or an existing extracted bundle. Game assemblies and private assets are not downloaded or included in the repository.

## Select modules

```powershell
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle
./scripts/Build-VICE.ps1 -Modules Enforcement,Syndicate
./scripts/Build-VICE.ps1 -Modules Substances
./scripts/Build-VICE.ps1 -All
./scripts/Build-VICE.ps1                    # Core only
./scripts/Build-VICE.ps1 -Modules Lifestyle -AddOns YouTube
```

Every invocation builds/checks current Core. `-All` selects seven gameplay modules and cannot be combined with `-Modules`. YouTube is explicit; `-All` does not select it. An add-on-only build (`-AddOns YouTube`) checks Lifestyle but exports only Core/adapter, requiring compatible Lifestyle already installed.

Selected modules run tests and native patch checks with warnings treated as errors. Builds create a fresh directory/ZIP under `artifacts`, validate contents/hashes, and run installer fixtures. Both paths are printed.

## Assets and overrides

Lifestyle's embedded darts/pool artwork requires **Unity 2022.3.62f2** on a clean checkout. Later builds reuse a verified bundle. Rebuild with:

```powershell
./scripts/Build-VICEHomeGamesAssets.ps1 -Rebuild -UnityPath <editor-path>
```

For development without Unity or the bundle, use `-Modules Lifestyle -PrimitiveHomeGames`. This retains rules, cameras, and poker with pool/darts fallback meshes. Normal builds embed verified artwork; no loose model pack is installed.

Overrides include `-GameDir`, `-DotnetPath`, `-NugetConfig`, `-OutputDirectory`, and `-OriginalMoreGuns`. Output directories must be fresh. The builder uses the local bundled SDK if present, otherwise `dotnet`.

## Install an export

Close the game, then run:

```powershell
./scripts/Install-VICE.ps1 -PackageDir <export-directory>
# Or build and install:
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle -Install
```

Exports contain selected gameplay DLLs in `Mods`, one Core in `UserLibs`, metadata/hashes, guides, and installer scripts. Installation defaults to packaged selections; `-Modules` and `-AddOns` can limit it to a subset. Core is always updated.

The installer verifies hashes before writing, backs up replaced DLLs outside `Mods`, removes selected predecessors/duplicate Core copies, and restores previous DLLs on installation failure. Unselected modules, settings, saves, media, and shopping lists are preserved. Missing defaults do not replace existing JSON files. Remove original Cartel Enforcer before installing Syndicate.

YouTube exports include the companion installer but require its external pinned archive. A base-only update preserves an installed add-on. To disable YouTube, close the game and remove `Mods/VICE.YouTube.dll`; selections remain compatible.

Tagged packaging uses `scripts/Build-VICERelease.ps1 -Tag <tag> -CompanionArchive <local-archive>` to produce a drop-in suite ZIP and separate browser ZIP. Maintainers can follow [release automation](vice-release.md). Users can follow [installation](../README.md) and [data preservation](vice-user-data.md).
