# Off Register workshop

The workshop is included in [VICE Substances](vice-substances.md), with embedded models and consumption effects. It requires the beta / IL2CPP game, MelonLoader, compatible S1API, and Core.

## Getting started

Own and access the Sewer Office. Find the checkerboard Off Register sticker near its entrance and choose **Meet the collective**. This adds its message thread, opens the Studio phone catalogue, and starts the **First edition** tutorial. Existing introduced saves receive the tutorial too, with progress inferred from equipment and stock.

## Production loop

1. Buy a **Blotter Bench ($350)**, **drying cabinet ($250)**, **blank blotter stock ($4)**, and **empty edition sleeves ($1)** from Oscar's warehouse store. Buy **print cartridges ($6)** at either hardware store. Place equipment in the owned Sewer Office.
2. Order **Prism Base ($30 per parcel)** through the Studio phone app, paid from the online account. After thirty game minutes, follow the purple star to Dan's Hardware delivery pallet. A full pallet holds the order until space is available.
3. Bring one parcel, one blank sheet, and one cartridge to the bench. Use **Edition ▶** to choose a preset/saved design, **Draw** to edit, and **Create batch** to begin.
4. **Register** by dragging the red mark onto the green cross and holding steady. Arrow keys are an alternative. **Print** by dragging the roller from the lower to upper screen edge; Space rolls steadily.
5. Collect the wet sheet. At the cabinet, choose **Load wet sheet**, then click the door handle, tray lip, sheet, tray lip, and handle to load and close it. Space advances each action. Drying takes two game hours, including time away or asleep.
6. **Collect dry sheet** uses the same door/tray sequence. The lamp shows idle/drying/ready. Wet sheets have a blue tint and water-drop badge; dry sheets have a warm border and check badge. Full inventory leaves output in the cabinet; Esc before completion restores its contents.
7. Use **Load dry sheet** at the bench, then **Cut** along eleven guide lines, starting at each white cursor. Release and grab near the current point to resume. Space advances cuts. Collect **32–40 Standard-quality tabs**; mistakes reduce yield. Full inventory leaves output in the machine.
8. Package matching loose tabs at a native station: one per baggie, five per jar, or twenty per edition sleeve. Put sleeves in the packaging input, pack/seal normally, and sell the native product. Existing brick-packaged editions remain compatible.
9. Consume a tab to start the visual/audio effects. Hold **B** during treatment for closed-eye patterns.

Leaving restores camera, movement, and the selected hotbar item. Active machines cannot be removed until output is collected. The bench has no packaging shortcuts; production and drying use normal controls and timing.

## Artwork and saves

Draw directly on the physical sheet, then **Save edition** to create an immutable design. Previously printed stock keeps its original artwork. Five custom revisions are available per save and can be reused across batches.

Workshop state is in `Modded/OffRegister/Workshop.json` inside the loaded save, preserving tutorial progress, artwork, orders, machines, batches, yield, and drying deadlines. Damaged/unsupported files stop initialization and are preserved. Interrupted stages do not consume supplies again; partial minigame progress restarts.

Keep Substances and compatible S1API installed while workshop items remain. Back up the full save before updates/removal. Multiplayer uses host-authorized transactions and shared artwork/state with matching binaries. Genuine second-player checks, customer handover, and balance tuning still require playtesting.

See the [playtest checklist](playtest.md), [user-data guide](vice-user-data.md), and [build guide](vice-build.md).
