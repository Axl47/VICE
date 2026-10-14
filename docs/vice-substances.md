# VICE Substances

VICE Substances 0.1.0 combines the Off Register workshop (Personal LSD Gameplay 0.2.10) and its visual/audio treatment (Personal LSD Preview 0.12.1). It requires the installed IL2CPP game, MelonLoader and `S1API.Il2Cpp.MelonLoader.dll`, plus the shared `UserLibs/VICE.Core.dll`.

Install `Mods/VICE.Substances.dll` using the unified package installer. The installer backs up and removes `Personal.LSDGameplay.dll` and `Personal.LSDPreview.dll` only when Substances is selected. If either predecessor is loaded alongside Substances, Core rejects Substances initialization before it registers content, subscribes to events or installs hooks.

## Gameplay and controls

The workshop retains its introduction, Studio phone catalogue, supplies and delivery orders, immutable edition artwork, physical registration/printing/cutting, cabinet door/tray interactions, drying time, native packaging and sales, furniture and tutorial. Follow [the workshop guide](lsd-workshop.md) for the production loop.

Consume an Off Register tab to start or refresh the local visual/audio treatment. Hold **B** during the treatment for the existing closed-eye patterns. F8 manual previews, F9 effect resets, and F9 production/drying skips have been removed. Save drawings with **Save edition** and complete production through its normal controls. The network command for the old production skip has also been removed. Pause, death, scene changes, shutdown, expiration and invalid player context still suspend or clean up effects as before.

## Compatibility and data

- Native item and furniture IDs remain `personal.lsd:*`, including `personal.lsd:bench`, `personal.lsd:cabinet`, edition tabs, sheets, supplies and sleeves. The custom product save provider remains `personal.lsd:edition-v1`, descriptor version 1.
- Workshop state stays in each save's `Modded/OffRegister/Workshop.json`. Its schema, artwork pixels, machine GUIDs/batches, orders, introduction and tutorial migration rules are unchanged. Native save integration and `.previous` backup behavior are retained. Installation does not write saves.
- All visual/audio settings remain in the existing `[PersonalLSDPreview]` category in `UserData/MelonPreferences.cfg`, including `DefaultsRevision`. Settings are reused in place; copying them into a new category would create competing settings. Existing predecessor default upgrades still apply normally. No separate predecessor UserData folder contains gameplay state or media that needs moving.
- Host authority, inventory transactions, action leases, replay protection, initial-state synchronization and replicated furniture visuals are retained. The public `PersonalMods.LSDGameplay.StudioPacket`, `StudioCommand` and `StudioReply` types and wire payloads retain their identities. All peers should use the same Substances release; a live mixed-version multiplayer session has not been certified.
- Embedded studio artwork, inventory icons, shader and echo mixer keep their original resource names. Source/artwork paths under `src/LSDGameplay` and `src/LSDPreview` remain the shared inputs for the merged project so the existing authored asset contracts and generators continue to work. Those directories no longer provide Melon entry points.

`SubstancesMod` is the only `MelonMod` in the DLL. It forwards initialization, update, late update, scene load/unload, quit and deinitialization to plain `WorkshopFeature` and `PreviewFeature` objects and supplies its Harmony instance. Deinitialization releases subscriptions, owned visual/audio objects, session state and the module's Harmony hooks. No predecessor assemblies are dependencies.

## Build and package

```powershell
./scripts/Build-VICE.ps1 -Modules Substances
./scripts/Build-VICE.ps1 -Modules Substances -Install
./scripts/Build-VICE.ps1 -All -AddOns YouTube
./scripts/Package-VICE.ps1 -All -AddOns YouTube
```

`-All` now selects seven gameplay modules: Commerce, Arsenal, Lifestyle, Interface, Enforcement, Syndicate and Substances. YouTube remains an explicit add-on. `Package-VICE.ps1` packages existing Release builds and returns `DirectoryPath` and `ZipPath`. Substances contributes only `Mods/VICE.Substances.dll`; its assets are embedded and it has no packaged settings defaults or external content. The output DLL is `src/VICE.Substances/bin/Release/net6.0/VICE.Substances.dll`. Its project is included in `ScheduleOne.Mods.sln`.

Both legacy build scripts now delegate to `Build-VICE.ps1 -Modules Substances`. `Build-LSDGameplay.ps1 -RebuildArt` retains the studio artwork rebuild before delegating. Direct builds of the retired predecessor projects fail with a migration message so they cannot silently create obsolete release DLLs.

The unified build runs Core tests, workshop rules/persistence/network tests, 138,077 effect assertions, compiled registration/lifecycle checks, artwork contracts and embedded hashes, native audio/render constraints, exact directory/ZIP packaging checks and isolated installer fixtures. `Check-VICESubstances.ps1 -GameDir <game> -BuiltDll <dll>` can validate an existing candidate. Tests confirm the DLL has one Melon entry, depends on Core, rejects both predecessors, preserves save/wire/resource identities and contains no F8/F9 bindings or production-skip command.

For a sandbox that restricts temporary-directory renames, set `TEMP` and `TMP` to a writable directory inside the checkout before running tests. This does not affect the game installation.

## Native validation

```powershell
./scripts/Test-VICESubstancesNative.ps1 -Mode Menu
./scripts/Test-VICESubstancesNative.ps1 -Mode Migration -Save5 '<existing fifth-slot save with workshop data>'
```

These checks run the installed executable with a separate MelonLoader profile, containing only S1API, Core and the candidate module. Menu mode validates the GPU shader, completed-frame presentation, closed-eye shader, final-listener echo and restored native mixer routing. Migration mode copies an existing workshop save, verifies saved artwork and batches, placed furniture, customized predecessor preferences, native packaging quantities, tab consumption and saving through the unified module. It writes only the copied save. Evidence and before/after isolation hashes are saved under `artifacts/substances-native-*`. It leaves production Mods, UserLibs, UserData and the source save unchanged. Command-line diagnostics remain explicit validation tools and have no gameplay hotkeys.

The installed game's public `Player.ConsumeProduct` path can bypass `ConsumeProductInternal`; the unified treatment observes both exclusive native targets. Both callbacks feed the same bounded consumption event, with local-player, tab-ID and native instance checks, so repeated consumption refreshes the existing treatment rather than creating stacked instances.

The October 13, 2026 migration check passed on an existing predecessor save with four editions, two placed machines and an interrupted batch. A second fresh launch also passed after loading the save written by Substances. Evidence is in `artifacts/substances-native-migration-2d809b1f2e604e589cda236b2418afce/Evidence` and `artifacts/substances-native-migration-3887394256c44993b6d114f4899acb91/Evidence`. Both isolation proofs report unchanged source saves and production files. The installed S1API logs its native sleeve composite-icon fallback; the workshop's authored icons, models and resource hashes are preserved. A live second-player session and loaded-NPC hallucination appearance remain playtests, beyond the retained pure/network contracts and isolated native checks.

Final native shader/frame/audio evidence is in `artifacts/substances-native-menu-cbc592ba91354104bf46649a50a7e993/Evidence` with the same unchanged-production proof. The candidate DLL SHA-256 is `3C2DC7519729660F312B2996BE89FBD6194BB15E2F8773CBAD20EC7F5D711511`. The saved-data comparison in the first migration run's `workshop-preservation.json` confirms that version, revision, introduction, artwork, machines and orders all match the predecessor save exactly.
