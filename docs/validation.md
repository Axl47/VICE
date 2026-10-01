# Validation record

Validated locally September 30–October 1, 2026 against the owner's generated beta assemblies.

| Check | Result |
| --- | --- |
| Five mod projects, x64 Release / .NET 6 | Compiled with zero warnings and zero errors. |
| Native patch targets | All 45 explicit method/property targets exist in the generated beta metadata. Runtime detours have not been exercised. |
| Price search and exact package planning | 6,761 assertions passed, including 4,500 seeded randomized comparisons against exhaustive searches. Covers insufficient stock, mixed package sizes, no acceptable offer and integer search boundaries. |
| PowerShell build/asset preparation | Executed successfully; original bundle extraction SHA-256 matches the local baseline. |
| PowerShell install script | Syntax checked; deliberately not executed because the owner chose manual installation. |
| Private package | Contains exactly five personal mod DLLs, README, playtest/validation records and SHA-256 hashes. No game/S1API DLLs are bundled. |
| S1API custom-NPC guard | Confirmed `All`, native `S1NPC` and Boolean `IsCustomNPC` fields in 2.5.0 and the installed Bars fork 3.2.1-beta.7. |
| Storage content | Confirmed native item definitions for three rack sizes, four closet sizes including Huge Storage Closet, locker, filing cabinet and display cabinet. |
| Live startup/gameplay with the personal mods | Not run. The owner's existing game session remained running; installation and playtest were left to the owner as requested. |

Compilation checks API availability; it cannot prove native detour behavior, bundle runtime deserialization, animation correctness, wagers/payouts or inventory/save behavior. Use `playtest.md` to verify those in game. No user saves, existing mods or live game process were changed by this work.
