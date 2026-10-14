# VICE Substances

Substances includes the **Off Register** LSD workshop and its visual/audio consumption effects in one module. It requires the IL2CPP game, MelonLoader, compatible IL2CPP S1API, and `VICE.Core.dll` in `UserLibs`.

Install `VICE.Substances.dll` in `Mods` using the suite package or [installer](vice-build.md). Remove replaced standalone workshop/effect DLLs according to the package's upgrade instructions; loading both implementations prevents Substances initialization.

## Playing

Follow the [workshop guide](lsd-workshop.md) to meet the collective, buy equipment, order supplies, create edition artwork, print and dry sheets, cut tabs, and package them at native stations.

Consume an Off Register tab to start or refresh the local visual/audio effects. Hold **B** for closed-eye patterns during treatment. Save drawings with **Save edition** and use normal production controls and drying time.

## Settings and saves

Effect preferences remain under `PersonalLSDPreview` in `UserData/MelonPreferences.cfg` for compatibility. Restart after editing. Workshop state is in the loaded save's `Modded/OffRegister/Workshop.json`, preserving artwork, equipment, batches, orders, and tutorial progress.

Item IDs and save formats retain their legacy identities so existing content can load. Back up the full save before updating and keep Substances installed while workshop items remain. Artwork, models, icons, and effects are embedded; no separate model pack is needed.

## Multiplayer

The host authorizes inventory/production transactions and shares workshop state and artwork. Every participant needs matching Substances binaries and compatible S1API. A genuine second-player session and loaded-NPC hallucination appearance still require playtesting.

See the [build guide](vice-build.md), [compatibility guide](compatibility.md), and [playtest checklist](playtest.md).
