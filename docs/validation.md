# Validation record

Validated locally September 30–October 1, 2026 against the owner's generated beta assemblies.

| Check | Result |
| --- | --- |
| Six mod projects, x64 Release / .NET 6 | Compiled with zero warnings and zero errors. Warehouse, Better Police and Casino are 0.1.1; the other three mods are 0.1.0. |
| Native patch targets | All 48 explicit method/property patch declarations exist. Address sharing was audited across every generated Cpp2IL assembly; only two narrowly reviewed shared targets remain. The original police/warehouse targets are rejected by the new audit. |
| Price search and exact package planning | 6,761 assertions passed, including 4,500 seeded randomized comparisons against exhaustive searches. Covers insufficient stock, mixed package sizes, no acceptable offer and integer search boundaries. |
| Saved gas-mart purchase logic | 3,629 assertions passed: JSON reload/update/delete/history, damaged/future/null file protection, failed-write recovery, order capacity, and 1,200 randomized stock/unlock/quantity scenarios. |
| PowerShell build/asset preparation | Six-mod build and packaging executed successfully; original bundle extraction SHA-256 matches the local baseline. |
| PowerShell install script | Syntax checked; deliberately not executed because the owner chose manual installation. |
| Private package | `PersonalMods-0.2.0.zip` contains exactly six personal mod DLLs, README, playtest/validation/crash records and SHA-256 hashes. No game/S1API DLLs are bundled. |
| S1API custom-NPC guard | Confirmed `All`, native `S1NPC` and Boolean `IsCustomNPC` fields in 2.5.0 and the installed Bars fork 3.2.1-beta.7. |
| Storage content | Confirmed native item definitions for three rack sizes, four closet sizes including Huge Storage Closet, locker, filing cabinet and display cabinet. |
| Original startup failure | Four local Windows dumps show the same recursive Better Police pursuit hook / native exception-formatting chain. Shared native thunk confirmed in metadata; see `startup-crash.md`. |
| Original Warehouse runtime evidence | Owner's latest log reports ten storage additions, two successful save loads and a joining player with Warehouse 0.1.0/S1API/TheRegulars. This does not validate every warehouse feature. |
| Corrected six-mod startup and saved-purchase UI | Pending manual installation and a fresh game run. The owner's game session remained running; no installed DLLs or process were changed. |

Compilation checks API availability; it cannot prove native detour behavior, bundle runtime deserialization, animation correctness, wagers/payouts or inventory/save behavior. Use `playtest.md` to verify those in game. No user saves, existing mods or live game process were changed by this work.
