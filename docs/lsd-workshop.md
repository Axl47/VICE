# Off Register workshop — first playable slice

Personal LSD Workshop 0.1.0 implements the starter manual loop from [the gameplay design](lsd-gameplay-design.md). It requires the beta / IL2CPP game, MelonLoader 0.7.3 and S1API (Forked by Bars) 3.2.1-beta.7. The companion visual treatment is Personal LSD Preview 0.11.1; its approved 0.11 visual/audio parameters are unchanged.

## Play the slice

Use the copied **fifth save slot**. Own and access the Sewer Office through the native game. Find the checkerboard Off Register sticker on a wall near the office entrance and select **Meet the collective**. This adds its message thread and unlocks the Studio phone catalogue.

1. Buy a **Blotter Bench ($350)**, **Blotter drying cabinet ($250)**, **Blank blotter stock ($4)** and **Empty edition sleeves ($1)** from **Oscar's warehouse store**. Buy **Print cartridges ($6)** at either hardware store.
2. Place the bench and cabinet inside the owned Sewer Office. Active machines cannot be picked up or destroyed until their output is collected.
3. Order **Prism Base ($30 per parcel)** through the Studio phone app. Payment uses online balance. Collect the parcel from **Dan's Hardware delivery pallet** after thirty game minutes. A full pallet holds the order; it does not overwrite other deliveries or impose a pickup deadline.
4. Carry one parcel, one blank sheet and one cartridge to the bench. Select a preset or a saved custom edition and start a batch.
5. Stabilize the moving registration cross, then align and print. Arrow keys adjust alignment; the left mouse can position the cross directly. Hold Space or the mouse to advance printing and cutting. Each active stage lasts about twelve seconds.
6. Collect the wet sheet and carry it to the cabinet. Drying takes two game hours and continues while away or after sleeping. Its lamp shows idle, drying or ready. Collect the ready sheet and load it into the bench for cutting.
7. Collect **32–40 Standard-quality tabs**. Mistakes reduce yield, while the surviving tabs keep their quality and edition. Uncollected output remains in its machine when inventory is full.
8. Package matching loose tabs at the bench: one tab uses a native baggie, five use a native jar, and twenty use an edition sleeve. Native bulk packaging supplies the twenty-unit sale/unpacking behavior. Store and sell the resulting native product as usual; the finished edition is listed when first collected.
9. Consume a tab to start the approved visual/audio treatment. **F9** clears it immediately. **F8** remains the manual preview/fade control.

## Artwork and saving

Three presets—Sunburn, Prism and Night Shift—have identical production opportunities. Drawing uses a 64 × 64 canvas, eight colors, brush sizes, erasing, undo and a name. Choose whole-sheet art or a repeated motif. Saving creates an immutable edition: already printed stock keeps its original artwork. This slice allows five custom revisions per save. Reuse them across as many batches as desired.

The first slice uses a screen panel for drawing and registration. The next presentation step is a camera framed on the physical sheet, with drawing directly on it like the graffiti interaction. Assistance, production queues, upgrades, additional formulas/material grades and business receiving remain outside this slice.

Native inventory and placed equipment save normally. Workshop state is stored under the selected save's `Modded/OffRegister/Workshop.json`, with a previous-file copy after subsequent saves. It preserves introduction, edition pixels, order identities, machine GUIDs/stages, yield and drying deadlines. Loading a damaged or unsupported workshop file stops workshop initialization and preserves that file. Reopening an interrupted manual stage does not consume its supplies again. Partial minigame progress restarts; batches and finished output remain.

Keep **both personal LSD DLLs and matching S1API installed** while this save contains studio items. Back up the full save before removing the workshop. The slice uses eight stable native edition IDs across saves; custom names appear in the studio, while inventory names use Studio 01–05 for those revisions.

## Source and checks

Run `scripts/Build-LSDGameplay.ps1` to build both modules, run the workshop rule checks and the preview's timing/render/audio guards, and package the DLLs. `-RebuildArt` regenerates the authored Blender props, transparent inventory icons and Unity bundle using Blender 5.2 and Unity 2022.3.62f2. Editable Blender files are generated under `assets/LSDStudio/Blender`; the generators, rendered PNGs, FBX files and Unity project are retained in the repository. Geometry and icons are authored locally; the runtime uses the installed game's native URP shader and existing packaging/consumption behavior.

The opt-in native diagnostic uses `--lsd-slice-check`, with `--lsd-slice-exercise` or `--lsd-slice-reload` and `--lsd-check-output=<directory>`. Exercise mode intentionally places equipment, changes inventory, advances time and saves **only slot five**. Back up that slot first and restore it after the check. Ordinary launches never run this scenario or auto-load a save.

Multiplayer uses host-authorized transactions, machine ownership, matching edition definitions and replicated artwork/state. A second-player playtest remains required before treating multiplayer as verified. Native customer handover and balance tuning also need manual playtesting alongside the automated quantity and save checks.

On October 7, the native fifth-slot exercise passed introduction, custom drawing, paid ordering and physical pallet delivery, placed equipment, preparation/printing/cutting, drying through a native time skip, full-inventory rejection, 1/5/20-unit packaging, consumption and native saving. A separate fresh launch passed restoration of the placed equipment, package quantities, interrupted batch, immutable pixels, introduction and pending order without duplicate delivery. The restored build also loaded the authored equipment/supply icons successfully. Local evidence is retained under `artifacts/lsd-slice-check` as `exercise.log`, `reload.log`, expected snapshots and pass markers. The diagnostic save is preserved separately; slot five is restored for manual playtesting.

The installed TheRegulars 1.0.0 logs an existing missing `SpecialCustomerData.AdditionalBuyQuantityPerRank` method on this game build and pauses its own engine. The workshop checks pass alongside it; special-customer behavior remains outside this verification.
