# Beta compatibility findings

Target assemblies were generated from the installed IL2CPP 0.4.7f7 beta with MelonLoader 0.7.3.

## Warehouse

The beta retains `DarkMarket.ShouldBeOpen`, the access zone and roller door checks, but no longer exposes the old `DarkMarketMainDoor.OpenDoor` hook. The mod changes the opening decision and unlock condition, allowing the normal update to open the physical warehouse. It does not force the physical `IsOpen` getter.

The furniture shop object is `Oscar's Store`. Listings are added before initialization, with per-shop Start/Open retries when the registry becomes available later. Duplicate IDs are checked on each shop.

Current assets contain definitions for `smallstoragerack`, `mediumstoragerack`, `largestoragerack`, `locker`, `filingcabinet`, `displaycabinet`, `smallstoragecloset`, `mediumstoragecloset`, `largestoragecloset` and `hugestoragecloset`. The `Storage Rack Mk2 (Huge)` model belongs to `HugeStorageCloset_Built`; its purchasable item is **Huge Storage Closet**. All four closet sizes are included.

## Better Police

The old movement fields were replaced with `NPCSpeedController`. Pursuit bonuses multiply a saved baseline and restore it when pursuit ends. Radio backup uses existing active officers on the host, with recursion protection and a cooldown.

Horizontal/vertical vision fields are compiled constants. The mod adds an angular sight check for police only, including wall/vehicle occlusion. Notice time uses the mutable state multiplier; templates are left intact to avoid double multiplication. Aim/hit chance are adjusted on each officer's active ranged weapon. Obsolete body-search/leniency patches were omitted.

## Highballer

Highballer optimizes customer contracts, not casino bets. Counteroffer prices now use `AmountSelector`, quantity limits are instance data, and order days are returned through a passed collection. New offers and accepted counteroffers use the current lifecycle.

The original custom-NPC check tested an integer. Both S1API 2.5.0 and the owner's current Bars fork 3.2.1-beta.7 expose a Boolean, so custom customers could previously slip into automation. Both layouts were inspected. The optional reflection check handles the Boolean and skips special customer classes. An unavailable custom-NPC check conservatively skips automation.

Handover now has separate mode classes and `Open_Contract`. Exact bounded package planning replaces greedy selection, prevalidates transfers, journals changes for rollback, and submits through the native screen only after native validation reaches full success. Multi-product counteroffers remain manual.

## Casino

Blackjack/Ride the Bus maximum bet fields are compiled constants. The mod changes the actual `GetBetLimits` output. Slots use a mutable shared ladder; original values are captured once per process to avoid compounding after reloads. Funding/payout logic stays native.

## More Guns

The authorized MoreGuns 1.4.2 bundle contains two guns: AK47 and minigun. Old registry/shop flags could be set before async asset loading completed. The new code loads synchronously, isolates failures per gun, and checks IDs per registry/shop.

Definitions are cloned from current native integer definitions and given bundle models/icons, explicit legacy equipping and full-magazine defaults. Reload mode uses the new enum, and minigun manual reload is enabled. New fields such as headshot damage/cocking defaults are supplied explicitly. Avatar overrides apply only to the four custom model paths. Fire/windup state is per equipped instance; global camera/reticle changes are omitted.

Native legacy equipping remains in this beta. The preserved bundle still needs live animation/geometry checks after the refactor. Compilation alone does not establish gameplay or multiplayer correctness.
