# Tagged GitHub releases from the development PC

`.github/workflows/release.yml` runs whenever a tag is pushed. The tagged source is built and checked on this Windows PC using a self-hosted runner labelled `vice-release`. A separate GitHub-hosted job publishes the finished downloads after every build, installer check and ZIP hash check passes:

| Asset | Contents |
| --- | --- |
| `VICE-<tag>.zip` | Core in `UserLibs`; Commerce, Arsenal, Lifestyle, Interface, Enforcement, Syndicate, Substances and the small YouTube adapter in `Mods`; guides, credits and file hashes. |
| `VICE-YouTube-Companion-<tag>.zip` | Optional Build-A-Base 0.4.26 browser files under `UserData/BaseBuilder/TvBrowser`, including its upstream licenses. No construction gameplay DLL. Approximately 489 MiB extracted. |
| `SHA256SUMS.txt` | SHA-256 for both ZIPs. |

Extract each chosen ZIP's contents directly into the Schedule I folder, next to `Schedule I.exe`. There is no enclosing package folder. Install MelonLoader and compatible IL2CPP S1API separately. The release ZIPs contain no game references, loader, S1API, saves, user settings, or defaults that would overwrite existing configuration. Missing defaults are seeded from embedded resources by the modules. See `VICE/README.md` inside the suite for the predecessor DLLs to remove when upgrading.

YouTube controls remain hidden unless both the adapter and companion are available. The two ZIPs use distinct metadata paths, so extracting the companion preserves the suite's file manifest.

## Register this PC once

1. Open the repository's **Settings → Actions → Runners → New self-hosted runner**, choose **Windows / x64**, and follow GitHub's displayed download/checksum commands. Put the runner in a separate directory, for example `C:\Users\ortaa\Documents\Projects\VICE-ReleaseRunner`. Its `_work` checkout must be separate from the development repository.
2. Run the displayed `config.cmd` registration command with the additional options `--name VICE-Development-PC --labels vice-release`. Use the short-lived registration token shown by GitHub. This repository does not store the token. Do not configure a Windows service when using the foreground launcher below.
3. Start the registered runner from **PowerShell 7**:

```powershell
./scripts/Start-VICEReleaseRunner.ps1 `
    -RunnerDirectory 'C:\Users\ortaa\Documents\Projects\VICE-ReleaseRunner' `
    -CompanionArchive 'C:\Users\ortaa\Downloads\Build-A-Base 0.4.26 - 0.4.7 Beta (IL2CPP) 2792 0.4.26 2026-10-08T04-02Z ScifiAif1.zip'
```

Keep that terminal running when expecting releases. If the PC is offline, sleeping, or the runner is stopped, GitHub queues the build until an eligible runner is available. Only one runner with this label is needed on this device. The foreground launcher supplies PowerShell 7, the local .NET 8 SDK, `VICE_ASSET_SOURCE_DIR` and `VICE_TV_COMPANION_ARCHIVE` to its jobs. A service installation would need the same environment and access to the Unity editor under its service account.

The local prerequisites are the installed beta / IL2CPP game, MelonLoader 0.7.3 generated interop and Cpp2IL metadata, compatible S1API, .NET SDK 8 with its test runtime, and Unity 2022.3.62f2. The launcher reuses this checkout's ignored MoreGuns bundle and verified Lifestyle home-games bundle/contract as build inputs in the runner checkout. If the home-games inputs differ from the tag, the existing builder rebuilds them with Unity. It does not copy DLL outputs, source, `.git`, user data, or local NuGet configuration from development. Substances' owned asset inputs are tracked in the repository.

The game path defaults to this PC's Steam installation. Set repository Actions variable `VICE_GAME_DIR` only for a different path. The original authorized MoreGuns DLL is used as a fallback when its extracted bundle is missing. The browser archive is pinned to the SHA-256 documented in the YouTube guide; an unrelated update is rejected.

## Make a release

Commit and push the changes to release, then tag that commit:

```powershell
git tag v0.3.0
git push origin v0.3.0
```

The tag must include the Substances migration and the workflow. Use 1-80 filename-safe letters, digits, dots, underscores and hyphens, starting with a letter or digit. Individual DLL versions remain those declared in their projects; the tag identifies the whole suite. Tags with a hyphen, such as `v0.3.0-beta.1`, create prereleases.

The workflow verifies the checkout against the existing tag, creates the release as a draft, attaches both ZIPs and their checksums, then publishes it. Failed uploads leave a draft that a rerun can complete. Published releases are never overwritten; make a new tag for new binaries. The Actions workflow can also be dispatched manually with an existing tag. Only finished assets are uploaded, with seven-day Actions artifact retention. Tagged releases retain their downloads independently.

The build job has read-only repository permission, and checkout does not persist credentials. The GitHub-hosted publishing job alone receives write permission. The workflow has no pull-request trigger on this PC. Keep tag creation limited to trusted maintainers, since a tag builds repository code on the device.

## Check locally

```powershell
./scripts/Build-VICERelease.ps1 -Tag v0.3.0-test `
    -CompanionArchive 'C:\path\to\Build-A-Base-0.4.26.zip'
```

This builds all seven modules and YouTube with the unified builder, runs its checks, and validates both final ZIPs without installing or publishing anything. Each run uses a fresh output directory; `-OutputDirectory` must name an empty directory. `-GameDir`, `-DotnetPath`, `-OriginalMoreGuns` and `-AssetSourceDirectory` are available for local overrides. The checked installer-oriented intermediate export stays under `checked/` and is not uploaded.

The companion exporter checks the pinned source archive and helper DLL, extracts only browser files, retains licenses, and writes a hash for every packaged file. The final validator reads ZIP entries directly to check layout, completeness and hashes. `scripts/Test-VICEReleaseFixtures.ps1` exercises rejection of corrupt, missing, extra and incorrectly nested payloads.

## Provenance

Release automation does not establish redistribution rights. The existing [provenance](provenance.md) records personal-use permission for adapted mods and private weapon assets, and the [YouTube guide](vice-lifestyle-youtube.md) records that author permission for redistributing the companion has not been established. Preserve those credits and resolve the actual sharing scope before distributing the resulting suite or companion outside that scope.

GitHub references: [self-hosted runner setup](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners), [runner labels](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/apply-labels), [release creation](https://cli.github.com/manual/gh_release_create) and [release uploads](https://cli.github.com/manual/gh_release_upload).
