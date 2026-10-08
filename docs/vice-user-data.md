# VICE user-data folders

VICE Lifestyle 0.1.2 and Commerce 0.1.1 organize their data by module and feature under the game's `UserData/VICE` directory:

```text
UserData/
  VICE/
    Lifestyle/
      Television/
        Images/
        Videos/
        Cache/
          Images/
          Videos/
    Commerce/
      Purchasing/
        orders.json
    MigrationConflicts/    # Created only when legacy and VICE files collide
```

On initialization, Lifestyle moves `UserData/PersonalTV` into `VICE/Lifestyle/Television`. Commerce moves `UserData/PersonalGasPurchases` into `VICE/Commerce/Purchasing`; it also accepts the alternate legacy name `PersonalGuestPurchases` if present. The moves include nested directories and original media files. Moving on the same drive does not create another copy of the video folder.

If the new destination already exists, migration moves missing files into it. Existing VICE files win on a name conflict. The corresponding legacy files are preserved under `VICE/MigrationConflicts/<legacy-folder>-<unique-id>`, retaining their relative paths. The old root disappears only after all its entries have been moved. Repeated initialization preserves new edits, and an interrupted move can resume from its remaining source entries. A blocked path or a linked directory stops migration instead of loading an empty replacement folder.

Place new images and MP4 files directly in the new `Images` and `Videos` folders. Shopping lists continue to load from `orders.json`. The separate standalone Personal.TV and Personal.GasPurchases implementations use their original locations. Native save data in `Modded/PersonalTV`, furniture IDs and preferences in `MelonPreferences.cfg` retain their current identities.

Install the updated `UserLibs/VICE.Core.dll` together with the Commerce and Lifestyle DLLs. Core 0.1.1 supplies the shared migration service. `Build-VICE.ps1 -Modules Commerce,Lifestyle` builds and exports all three. Every VICE build includes the current Core, including single-module and Core-only updates.

The migration tests cover nested media and caches, shopping-list preservation, existing destination folders, both purchase folder names, collision archives, file/directory conflicts, repeated initialization, path validation and resuming a partial move.
