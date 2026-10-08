# VICE module migration

Commerce, Arsenal, Lifestyle, and Interface each replace their personal predecessor DLLs with one MelonLoader entry point. Gameplay settings, custom item IDs, embedded weapon resource names, saved shopping lists, and TV save-state identities are preserved. Commerce 0.1.1 and Lifestyle 0.1.2 move their personal user-data folders into the [shared VICE folder structure](vice-user-data.md).

VICE Lifestyle adds [on-demand multiplayer sharing of local TV videos](vice-lifestyle-video-sharing.md). Selecting an MP4 shares metadata; Play transfers that file through the host and waits for connected peers before starting playback. All peers must update Lifestyle together. The separate Personal.TV implementation retains its original behavior.

| Module | Feature code | Predecessors |
| --- | --- | --- |
| Commerce | `Trading`, `Purchasing`, `Warehouse` | Personal.Highballer, Personal.GasPurchases, Personal.Warehouse |
| Arsenal | `Guns` | Personal.MoreGuns |
| Lifestyle | `Casino`, `Television` | Personal.Casino, Personal.TV |
| Interface | `Hud` | Personal.UIEnhancements |

The feature classes no longer inherit `MelonMod`. Their owning module passes its Harmony instance at initialization and forwards their existing update, scene, shutdown, and cleanup callbacks. Each entry point releases its own Harmony hooks on deinitialization. The native item and data formats are unchanged; TV's runtime/network types now use the Lifestyle namespace, so multiplayer peers must update together.

## Shared Core

All four projects set `UseViceCore=true` and reference `VICE.Core.csproj`. Their `ModSupport` alias resolves to Core's public implementation; no personal copy of the helper is embedded in the DLLs.

`VICE.Core.ModuleCompatibility.CanInitialize(string module, params string[] legacyAssemblies)` checks the names of loaded assemblies, ignoring case. It returns false and logs the conflicting names if a predecessor is already loaded. Call it before installing replacement hooks. File replacement remains the installer's responsibility. This helper is shared by the four migrated modules and can be reused by future migrations.

Gas-mart completion listeners now belong to `SubscriptionScope` and are detached during save/shutdown cleanup, rather than only dropping the dictionary that recorded them. Television uses a module-lifetime scope for its global S1API lifecycle listeners. It retains its existing per-save teardown for screens, media decoders, relay jobs, and network state. Those scopes have different lifetimes and are kept separate.

Core remains independent of Unity/game-specific UI and item types. Weapon code, media handling, transaction planning, and HUD code stay with their features; sharing the support infrastructure does not move unrelated gameplay into Core.

## Build and verification

`scripts/Build-VICEModules.ps1` builds only these four modules and their Core dependency. It audits native patch addresses separately for each module, runs the existing pure transaction/purchase/weapon checks, and runs the adapted TV state/Relay checks.

`tests/VICE.Modules` inspects the built DLLs with Cecil. It verifies one Melon entry point per DLL, ordinary feature classes, the Core dependency, the absence of embedded personal support helpers or legacy assembly dependencies, expected lifecycle forwarding, hook teardown, and the weapon resource lookup. It also exercises Core's predecessor detection in an isolated test process.

The installer fixture checks selective and full DLL migration, retained police/syndicate/LSD/S1API files, preserved shopping/media data, installed hashes, and rejection of a corrupt package before any installation changes. Feature data migrates on module initialization; `tests/VICE.Core` covers folder moves, merges, collision preservation and repeated initialization. Fixtures and DLL backups remain under ignored `artifacts` directories.

Compile/metadata checks do not execute Harmony in the game. Check the four modules on a copied save after installing: use each feature, leave and reload the save, and watch for duplicate events or missing custom items. TV multiplayer, perceived audio/video behavior, and live Relay remain separate playtests.

## Other work

The existing Enforcement, Syndicate, and LSD projects retain their separate workflows. No Substances project or DLL is created in this migration. The original untracked PersonalTV research source/test/build files are left intact; the suite builds the adapted implementation under `VICE.Lifestyle`.
