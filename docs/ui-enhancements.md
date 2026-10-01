# UI Enhancements 0.1.0

This independent module adds the agreed payment to the left-side tracked deal list and exact map portrait pins for unlocked customers. Both features default on. It reads existing contract/NPC data and creates local presentation objects; it does not change transactions, customer progression or saves.

## Deal list

`QuestHUDUI.UpdateUI` and `UpdateMainLabel` independently write the native title/subtitle text in this beta. Postfixes on both methods detect a `Contract`, read `Payment`, and insert the amount before the first timing/subtitle line. Ordinary quests are left alone. The amount uses grouped dollar formatting, including fractional amounts when present. Invalid payments are omitted.

Native refreshes rebuild the base text before the addition, so amounts do not accumulate when the time or tracking state changes. Text layout and the HUD shade are rebuilt for long names or large payments. The display is the accepted contract's base payment, before delivery-dependent bonuses or deductions.

## Customer map

The map manager reconciles `Customer.UnlockedCustomers` once a second while the map is open. It also requires the NPC's relationship to be unlocked and the NPC to be active/alive. The native NPC pointer deduplicates entries, including customers with multiple references. Existing potential-customer zones and quest pins remain under the game’s control.

Each marker clones the current `NPCManager.NPCPoIPrefab`, configured while inactive with its NPC, hover name and location before native UI creation. The native portrait prefab supplies its mugshot, tooltip, map-item registration, controller navigation and click-to-focus. Position updates use `POI.UpdatePosition`, which projects world X/Z through the current `MapPositionUtility` origin and conversion factor into the map's existing container. Pan/zoom therefore use the same coordinates as native pins.

Pins follow the NPC every frame while the map is open. An indoor customer uses `LastEnteredDoor` when available. Customers removed from the unlocked list, deactivated or dead lose their pin. Only mod-owned marker objects are destroyed; native `POI.OnDisable` deregisters their UI. Leaving the main scene or quitting clears references so a different save can rebuild its own markers. Markers do not write NPC transforms or spawn network objects.

## Configuration and validation

`PersonalUIEnhancements` contains `Enabled`, `ShowDealPayouts`, `ShowKnownCustomers` and `CustomerMarkerScale`. The two additions can be disabled separately. Icon scale is bounded to 0.35–1.5, with 0.75 as the default/fallback. Restart after editing the configuration.

The three added native hook targets have distinct method bodies in the installed beta and pass the repository's shared-address audit. Compilation and native API inspection do not establish appearance or interaction in the live game. See `playtest.md` for label layout, moving/indoor customers, map navigation and scene cleanup checks. Installation remains manual.
