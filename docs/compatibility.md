# Beta compatibility findings

Target assemblies were generated from the installed IL2CPP 0.4.7f7 beta with MelonLoader 0.7.3.

## Warehouse

The beta retains `DarkMarket.ShouldBeOpen`, the access zone and roller door checks, but no longer exposes the old `DarkMarketMainDoor.OpenDoor` hook. The mod changes the opening decision, allowing the normal update to open the physical warehouse. Rank bypass runs in the warehouse's own update and uses its native unlock notification on the host. The `Unlocked` getter shares code with unrelated curfew/sewer/checkpoint flags and must not be detoured.

The furniture shop object is `Oscar's Store`. Listings are added before initialization, with per-shop Start/Open retries when the registry becomes available later. Duplicate IDs are checked on each shop.

Current assets contain definitions for `smallstoragerack`, `mediumstoragerack`, `largestoragerack`, `locker`, `filingcabinet`, `displaycabinet`, `smallstoragecloset`, `mediumstoragecloset`, `largestoragecloset` and `hugestoragecloset`. The `Storage Rack Mk2 (Huge)` model belongs to `HugeStorageCloset_Built`; its purchasable item is **Huge Storage Closet**. All four closet sizes are included.

## Better Police

The old movement fields were replaced with `NPCSpeedController`. Pursuit bonuses multiply a saved baseline and restore it when pursuit ends. Radio backup uses existing active officers on the host, with recursion protection and a cooldown. The original `OnActiveTick` detour caused the startup crash because its native thunk is shared with thousands of constructors. Version 0.1.1 hooks the distinct pursuit `BehaviourUpdate` body instead. See [the crash investigation](startup-crash.md).

Version 0.2.0 adds host squad plans and intercepts the distinct `CombatBehaviour.SetDestination(Vector3,bool,float)` body only for active police pursuits. Complete NavMesh paths gate detours; native combat/search remains responsible for attacking and losing the player. Rival gang tuning hooks the distinct static global/regional cooldown generators and `Ambush.MinPassed`. It keeps native progression, truce state, spawn pools and player eligibility. `CanNewActivityBegin` is a shared constant-return body and is deliberately not patched. See [the police/gang notes](police-coordination.md).

Horizontal/vertical vision fields are compiled constants. The mod adds an angular sight check for police only, including wall/vehicle occlusion. Notice time uses the mutable state multiplier; templates are left intact to avoid double multiplication. Aim/hit chance are adjusted on each officer's active ranged weapon. Obsolete body-search/leniency patches were omitted.

## Highballer

Highballer optimizes customer contracts, not casino bets. Counteroffer prices now use `AmountSelector`, quantity limits are instance data, and order days are returned through a passed collection. New offers and accepted counteroffers use the current lifecycle.

The original custom-NPC check tested an integer. Both S1API 2.5.0 and the owner's current Bars fork 3.2.1-beta.7 expose a Boolean, so custom customers could previously slip into automation. Both layouts were inspected. The optional reflection check handles the Boolean and skips special customer classes. An unavailable custom-NPC check conservatively skips automation.

Handover now has separate mode classes and `Open_Contract`. Exact bounded package planning replaces greedy selection, prevalidates transfers, journals changes for rollback, and submits through the native screen only after native validation reaches full success. Multi-product counteroffers remain manual.

## Casino

Blackjack/Ride the Bus maximum bet fields are compiled constants. Their `GetBetLimits` methods share one native body, but the IL2CPP bridge marshals the instance using the detoured method's declaring class before calling Harmony. The earlier single blackjack hook therefore remained unsafe for the sibling Ride the Bus controller. The owner's log contains null references in that native-to-managed trampoline during casino use.

Casino 0.1.2 removes that detour and adjusts only `CasinoGameBetPanel.Open` after native initialization. The slider holds actual dollar values; native `BetSliderChanged` and `SetLocalPlayerBet` transfer that value without a separate $1,000 ceiling. Native label refresh, cash/readiness validation, network bet data and payouts remain in use. The address audit now rejects the earlier shared controller target. Slider interaction and high-wager resolution still need a fresh game test.

All slot-machine hooks, wager-table changes and the `SlotBetMultiplier` preference were removed at the owner's request. A leftover preference in an existing config is ignored; after restarting with 0.1.2, slot machines use the original game behavior.

## More Guns

The authorized MoreGuns 1.4.2 bundle contains two guns: AK47 and minigun. Old registry/shop flags could be set before async asset loading completed. The new code loads synchronously, isolates failures per gun, and checks IDs per registry/shop.

Definitions are cloned from current native integer definitions and given bundle models/icons, explicit legacy equipping and full-magazine defaults. Reload mode uses the new enum, and minigun manual reload is enabled. New fields such as headshot damage/cocking defaults are supplied explicitly. Avatar overrides apply only to the four custom model paths; stale Unity wrappers are cleared before replacing a model.

The owner's AK47 reload log contains a `TrashItem.Initialize` null reference. Native reload consumes the spare before spawning discarded trash, then refills the gun afterward. The beta requires `TrashItemDraggable`, whereas the old prefab contains generic `Draggable`; 0.2.0 replaces that component in memory and restores the renamed trash ID and required references. An unavailable discard prefab is disabled so cosmetics cannot block reload.

Legacy audio controller fields also changed names. Their intended base volumes are restored explicitly, routed through the current master/effects volume calculation and main game mixer, and reduced independently for each gun. The minigun's existing legacy barrel animation is looped during aim spin-up, with speed increasing toward readiness. Fire/reload animations retain control during their actions. Camera tuning applies only inside these custom guns' native `Fire` call and is restored by a Harmony finalizer, including exception paths. See [weapon tuning](weapon-tuning.md).

Native legacy equipping remains in this beta. The preserved bundle still needs live animation/geometry checks after the refactor. Compilation alone does not establish gameplay or multiplayer correctness.

## Saved gas-mart purchases

Both `GasStationInterface (Central)` and `GasStationInterface (West)` expose the existing `ShopInterface`/`Cart` APIs (`gas_mart_central` and `gas_mart_west`). Controls are attached after the native shop opens, and only to those gas-marts.

Named lists and the last successful purchase store item IDs and quantities in a separate local JSON file. Restoring a list validates all native listings, unlocks, physical stock and quantity limits before replacing the cart. Native cart methods determine current prices and checkout problems. Purchase history is recorded through the native order-completion event; failed checkout attempts leave history intact. Loading a list does not submit a purchase.

## Deal and map UI

The current beta exposes tracked deal rows as `QuestHUDUI` bound to a `Contract` with a readable `Payment`. Its `UpdateUI` and `UpdateMainLabel` write the title separately; both distinct native bodies are patched after their normal work to preserve timing updates. Long labels trigger a layout/shade refresh.

Known customer pins reuse `NPCManager.NPCPoIPrefab` and the native `POI` creation, hover, map projection and deregistration lifecycle. Only unlocked, active, living customer NPCs are included; render visibility/distance do not hide an otherwise eligible customer. Indoor positions use the last entered door. `MapApp.SetOpen` has a distinct native body and initializes the local manager. No relationship getters, customer unlock methods, global POI lifecycle methods or shared generic app methods are detoured. See [the UI notes](ui-enhancements.md).

UI Enhancements 0.1.1 reads accepted product quantities through `ProductList.GetTotalQuantity` for the one-decimal unit value. Multiplayer markers use the native compass prefab, `AddElement`/exact-element `RemoveElement`, `UpdateElement` and `GetCompassData`. A postfix on the distinct `CompassManager.Update` body refreshes owned targets and presentation; no player lifecycle, camera getter or generic singleton method is detoured. Native replicated positions drive local-only markers on host and client.

UI Enhancements 0.1.2 removes the clipped elevated name label and writes the player name into its owned element's native `DistanceLabel` after the native element refresh. It preserves the existing label hierarchy and vertical geometry. The current 0.4.7f9 generated assemblies expose the same APIs, and all 57 repository detour declarations pass the fresh native address audit; no new detours are added.

UI Enhancements 0.2.0 adds a minimap through the managed `OnLateUpdate` callback without new patches. It reads the local HUD, selected native map sprite, `MapPositionUtility` projection and existing POI icon graphics. Fresh 0.4.7f9 native inspection confirms the same origin-relative X/Z conversion is used by `POI.UpdatePosition`, and `MapApp.Start` selects the tutorial/main sprite. The minimap owns plain UI graphics and does not clone map/POI behaviours or change native map controls. All 57 existing patch declarations still pass the current native address-sharing audit.

UI Enhancements 0.2.1 was compiled and audited against regenerated 0.4.7f10 assemblies. The installed native player-UI prefab has a foreground `Image` sibling outside `IconContainer`, which the original local-player first-image copy missed; both local/remote players now mirror the complete POI glyph in native draw order. Border pixel colors were read from the installed map sprites. Map geometry now composes local transforms below their common ancestor, avoiding a hidden phone's zero world scale; visibility follows the phone rather than the potentially stale map-app flag. The existing `MapApp.SetOpen` hook handles both close/open refreshes; no new patches are added, and all 57 declarations pass the fresh native audit.

UI Enhancements 0.2.2 adds live minimap player direction against the same 0.4.7f10 beta. Current native `POI.UpdatePosition` rotates the inner `IconContainer` from world forward; native projection maps world X/Z to map X/Y. The copied glyph substitutes that one local rotation using horizontal camera direction for the local player and the native POI direction for peers. Native geometry is read only; map orientation and non-player icon copying are unchanged. No new detours or game assets are bundled.
