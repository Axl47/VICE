# Validation record

Validated locally September 30–October 1, 2026 against the owner's generated beta assemblies.

| Check | Result |
| --- | --- |
| Six mod projects, x64 Release / .NET 6 | Six-mod build compiled with zero warnings and zero errors; the updated Casino project also compiled cleanly. Casino is 0.1.2, More Guns is 0.2.0; Warehouse and Better Police are 0.1.1; Highballer and Gas Purchases are 0.1.0. |
| Native patch targets | All 49 explicit method/property patch declarations exist. Address sharing was audited across every generated Cpp2IL assembly; only the narrowly reviewed Customer/FishNet writer target remains. A negative regression fixture confirms the former shared Casino hook is rejected, alongside the original police/warehouse targets. |
| Price search and exact package planning | 6,761 assertions passed, including 4,500 seeded randomized comparisons against exhaustive searches. Covers insufficient stock, mixed package sizes, no acceptable offer and integer search boundaries. |
| Saved gas-mart purchase logic | 3,629 assertions passed: JSON reload/update/delete/history, damaged/future/null file protection, failed-write recovery, order capacity, and 1,200 randomized stock/unlock/quantity scenarios. |
| Weapon spin-up timing | 21,998 assertions passed, including 250 irregular-frame-rate scenarios. Covers the fire delay, aim release, re-aiming, reload/unequip/pause/input blocking, duration changes, invalid frame times and scene reset. |
| PowerShell build/asset preparation | Six-mod build and packaging executed successfully; original bundle extraction SHA-256 matches the local baseline. |
| PowerShell install script | Syntax checked; deliberately not executed because the owner chose manual installation. |
| Private package | `PersonalMods-0.2.2.zip` contains exactly six personal mod DLLs, README, playtest/validation/crash/weapon records and SHA-256 hashes. No game/S1API DLLs are bundled. |
| S1API custom-NPC guard | Confirmed `All`, native `S1NPC` and Boolean `IsCustomNPC` fields in 2.5.0 and the installed Bars fork 3.2.1-beta.7. |
| Storage content | Confirmed native item definitions for three rack sizes, four closet sizes including Huge Storage Closet, locker, filing cabinet and display cabinet. |
| Original startup failure | Four local Windows dumps show the same recursive Better Police pursuit hook / native exception-formatting chain. Shared native thunk confirmed in metadata; see `startup-crash.md`. |
| Original Warehouse runtime evidence | Owner's latest log reports ten storage additions, two successful save loads and a joining player with Warehouse 0.1.0/S1API/TheRegulars. This does not validate every warehouse feature. |
| Corrected six-mod startup/save loading | Owner confirmed the mods now run. The local log shows all six DLLs loading and successful save loads. Detailed feature checks, including saved-purchase UI, remain in the live checklist. |
| AK47 reload failure | Owner reported animation without ammo refill. Player.log shows AK47 discarded-magazine `TrashItem.Initialize` throwing; the installed native coroutine sets ammo only after trash creation. The bundle has generic Draggable rather than the beta's required TrashItemDraggable. See `weapon-tuning.md`. |
| More Guns 0.2.0 live checks | Pending manual installation: repaired trash, complete empty-magazine reloads, master/effects mute, barrel animation, model switching and firing shake. Installed DLLs, saves and the live game process were not changed. |
| Casino 0.1.2 diagnosis and scope | Owner reported an unusable bet slider; local log shows three null references in the `GetBetLimits` native trampoline. That sibling-owner detour is removed. Installed native code confirms a dollar-valued slider, unclamped bet assignment and native cash/readiness checks. Cecil inspection confirms the compiled Casino 0.1.2 DLL contains no slot-machine type references. |
| Casino 0.1.2 live checks | Pending manual replacement/restart: both table sliders, affordable wagers above $1,000, payouts and original slot choices. No installed DLLs, slot state, config or live process were changed. |

Compilation checks API availability; it cannot prove native detour behavior, bundle runtime deserialization, animation correctness, wagers/payouts or inventory/save behavior. Use `playtest.md` to verify those in game. No user saves, existing mods or live game process were changed by this work.
