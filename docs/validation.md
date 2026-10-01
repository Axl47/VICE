# Validation record

Validated locally September 30–October 1, 2026 against the owner's generated beta assemblies.

| Check | Result |
| --- | --- |
| Seven mod projects, x64 Release / .NET 6 | Full seven-mod build compiled with zero warnings and zero errors. Casino is 0.1.2, More Guns is 0.2.0; Warehouse and Better Police are 0.1.1; Highballer, Gas Purchases and UI Enhancements are 0.1.0. |
| Native patch targets | All 52 explicit method/property patch declarations exist. Address sharing was audited across every generated Cpp2IL assembly; only the narrowly reviewed Customer/FishNet writer target remains. The three added UI targets have distinct bodies. A negative regression fixture confirms the former shared Casino hook is rejected, alongside the original police/warehouse targets. |
| Price search and exact package planning | 6,761 assertions passed, including 4,500 seeded randomized comparisons against exhaustive searches. Covers insufficient stock, mixed package sizes, no acceptable offer and integer search boundaries. |
| Saved gas-mart purchase logic | 3,629 assertions passed: JSON reload/update/delete/history, damaged/future/null file protection, failed-write recovery, order capacity, and 1,200 randomized stock/unlock/quantity scenarios. |
| Weapon spin-up timing | 21,998 assertions passed, including 250 irregular-frame-rate scenarios. Covers the fire delay, aim release, re-aiming, reload/unequip/pause/input blocking, duration changes, invalid frame times and scene reset. |
| PowerShell build/asset preparation | Seven-mod build and packaging executed successfully; original bundle extraction SHA-256 matches the local baseline. All six scripts parse without syntax errors. |
| PowerShell install script | Syntax checked; deliberately not executed because the owner chose manual installation. |
| Private package | `PersonalMods-0.3.0.zip` contains exactly seven personal mod DLLs, README, playtest/validation/crash/weapon/UI records and SHA-256 hashes. No game/S1API DLLs are bundled. |
| Existing module behavior | Cecil comparison against the 0.2.2 package confirms all six earlier modules retain identical runtime methods, fields and exception handling, including Casino's original slot behavior. Rebuilt DLL hashes change because SDK informational-version metadata now records the newer Git revision. |
| S1API custom-NPC guard | Confirmed `All`, native `S1NPC` and Boolean `IsCustomNPC` fields in 2.5.0 and the installed Bars fork 3.2.1-beta.7. |
| Storage content | Confirmed native item definitions for three rack sizes, four closet sizes including Huge Storage Closet, locker, filing cabinet and display cabinet. |
| Original startup failure | Four local Windows dumps show the same recursive Better Police pursuit hook / native exception-formatting chain. Shared native thunk confirmed in metadata; see `startup-crash.md`. |
| Original Warehouse runtime evidence | Owner's latest log reports ten storage additions, two successful save loads and a joining player with Warehouse 0.1.0/S1API/TheRegulars. This does not validate every warehouse feature. |
| Corrected six-mod startup/save loading | Owner confirmed the mods now run. The local log shows all six DLLs loading and successful save loads. Detailed feature checks, including saved-purchase UI, remain in the live checklist. |
| AK47 reload failure | Owner reported animation without ammo refill. Player.log shows AK47 discarded-magazine `TrashItem.Initialize` throwing; the installed native coroutine sets ammo only after trash creation. The bundle has generic Draggable rather than the beta's required TrashItemDraggable. See `weapon-tuning.md`. |
| More Guns 0.2.0 live checks | Pending manual installation: repaired trash, complete empty-magazine reloads, master/effects mute, barrel animation, model switching and firing shake. Installed DLLs, saves and the live game process were not changed. |
| Casino 0.1.2 diagnosis and scope | Owner reported an unusable bet slider; local log shows three null references in the `GetBetLimits` native trampoline. That sibling-owner detour is removed. Installed native code confirms a dollar-valued slider, unclamped bet assignment and native cash/readiness checks. Cecil inspection confirms the compiled Casino 0.1.2 DLL contains no slot-machine type references. |
| Casino 0.1.2 live checks | Pending manual replacement/restart: both table sliders, affordable wagers above $1,000, payouts and original slot choices. No installed DLLs, slot state, config or live process were changed. |
| UI Enhancements native UI inspection | Confirmed `Contract.Payment`, both independent native deal-label refresh bodies, `Customer.UnlockedCustomers`, relationship unlock state, and native portrait creation/hover/navigation/projection/teardown. The compiled UI DLL has no embedded assets and uses read access to contract/NPC state. |
| UI Enhancements 0.1.0 live checks | Pending manual installation: payout layout/timing refreshes, moving/indoor customers, newly unlocked customers, mouse/controller map interaction, independent preferences and cleanup across saves. No installed DLLs, saves or live process were changed. |

Compilation checks API availability; it cannot prove native detour behavior, bundle runtime deserialization, animation correctness, wagers/payouts, UI appearance/interaction or inventory/save behavior. Use `playtest.md` to verify those in game. No user saves, existing mods or live game process were changed by this work.
