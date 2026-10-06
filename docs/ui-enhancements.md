# UI Enhancements 0.1.2

This independent module adds total/per-unit payments to tracked deals, map portrait pins for unlocked customers and compass markers for other multiplayer players. All three features default on. It reads existing contract/NPC/player data and creates local presentation objects; it does not change transactions, progression or saves.

## Deal list

`QuestHUDUI.UpdateUI` and `UpdateMainLabel` independently write the native title/subtitle text in this beta. Postfixes on both methods detect a `Contract`, read `Payment`, and insert pricing before the first timing/subtitle line. Ordinary quests are left alone. The total uses grouped dollar formatting, including fractional amounts when present. Invalid payments are omitted.

The additional unit price divides `Payment` by `ProductList.GetTotalQuantity()` and rounds to one decimal, for example `$1,000 ($33.3/unit)` for 30 units. It uses requested product units, not inventory package counts. Multiple requested products produce an average across their combined quantity. An absent/empty product list or non-positive total quantity shows only the total payment, preventing division by zero.

Native refreshes rebuild the base text before the addition, so amounts do not accumulate when the time or tracking state changes. Text layout and the HUD shade are rebuilt for long names or large payments. The display is the accepted contract's base payment, before delivery-dependent bonuses or deductions.

## Customer map

The map manager reconciles `Customer.UnlockedCustomers` once a second while the map is open. It also requires the NPC's relationship to be unlocked and the NPC to be active/alive. The native NPC pointer deduplicates entries, including customers with multiple references. Existing potential-customer zones and quest pins remain under the game’s control.

Each marker clones the current `NPCManager.NPCPoIPrefab`, configured while inactive with its NPC, hover name and location before native UI creation. The native portrait prefab supplies its mugshot, tooltip, map-item registration, controller navigation and click-to-focus. Position updates use `POI.UpdatePosition`, which projects world X/Z through the current `MapPositionUtility` origin and conversion factor into the map's existing container. Pan/zoom therefore use the same coordinates as native pins.

Pins follow the NPC every frame while the map is open. An indoor customer uses `LastEnteredDoor` when available. Customers removed from the unlocked list, deactivated or dead lose their pin. Only mod-owned marker objects are destroyed; native `POI.OnDisable` deregisters their UI. Leaving the main scene or quitting clears references so a different save can rebuild its own markers. Markers do not write NPC transforms or spawn network objects.

## Multiplayer compass

After the native `CompassManager.Update`, the manager reconciles `Player.PlayerList` every half-second. It excludes the local player, inactive objects and players whose networking has stopped. Remote `PlayerLoaded` is not used as an eligibility gate because it is not a replicated readiness flag. The local player and player camera must be ready before creating elements.

Each marker registers through `CompassManager.AddElement` with a blue diamond. Version 0.1.2 uses that element's native `DistanceLabel` for the player name, retaining its parent, vertical position, height, font and alignment. Only its width is increased to at least 110 UI units; long names truncate with an ellipsis and rich text is disabled. The separate label above the icon is removed because it was cropped by the compass bounds in live testing.

Native `UpdateElement` still handles direction placement. Immediately afterward, the mod replaces the distance text with the cached player name on that owned element, so native refreshes cannot leave the number in place. Mission/quest labels retain their normal distance text. The marker applies the bounded opacity from native `GetCompassData`, so it fades at the compass's direction limits and follows the normal hidden/disabled compass canvas.

Targets are mod-owned, non-networked transforms updated from each peer's replicated position. They survive a disconnect until their elements are deregistered, preventing native compass updates from following destroyed player transforms. Removal uses the exact owned `Element`, then destroys its UI and target. Other compass/quest elements are not removed. Missing players, a replacement compass, the main-scene unload and application exit clean up the owned objects. Host and joining clients can display markers independently without new RPCs or save data.

## Configuration and validation

`PersonalUIEnhancements` contains `Enabled`, `ShowDealPayouts`, `ShowKnownCustomers`, `CustomerMarkerScale` and `ShowMultiplayerPlayers`. The three additions can be disabled separately. `ShowDealPayouts` controls both total and per-unit pricing. Customer icon scale is bounded to 0.35–1.5, with 0.75 as the default/fallback. Restart after editing the configuration.

The four UI hook targets have distinct method bodies in the installed beta and pass the repository's shared-address audit. Native player/compass accessors and registration/projection/teardown APIs were inspected. Compilation and native API inspection do not establish appearance or multiplayer behavior in the live game. See `playtest.md` for pricing, compass, map navigation and scene cleanup checks. Installation remains manual.
