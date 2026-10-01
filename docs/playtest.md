# Live playtest

Target: Schedule I 0.4.7f7 IL2CPP beta, MelonLoader 0.7.3. Build/startup results are recorded separately in `validation.md`. Run these checks on a copied/test save.

1. Launch with the five personal DLLs, S1API and TheRegulars. Confirm all five load in `MelonLoader/Latest.log` without personal-mod patch/asset warnings.
2. Visit the warehouse during the day and after midnight. Test the pedestrian entrance and roller doors. Check Oscar's small/medium/large racks, small/medium/large/huge storage closets, lockers and filing/display cabinets. Purchase/place an item; return to the menu and reload to check catalog persistence.
3. Start a police chase. Check near/far speed, detection to the sides, occlusion through a wall, arrest timing, aim and nearby backup. End pursuit and confirm normal movement speed returns.
4. Open an ordinary customer's counteroffer. Check the displayed estimate/budget and manually modify product, quantity and price. Deliver exact packages, then try insufficient/mixed packaging. Verify unmatched deliveries remain manual and inventory does not change when no exact plan exists. Confirm special customer deals from TheRegulars stay under that mod's control.
5. Check blackjack/Ride the Bus upper slider values. Bet/resolve within available funds; verify deduction/payout. Inspect slots and repeat after reload to rule out multiplier compounding.
6. Buy both custom guns and matching magazines. Check icons, first/third-person models, automatic fire, ammo decrement, reload and minigun aim windup. Confirm ordinary guns still work. Save/reload with a custom gun and confirm persistence.
7. For multiplayer, repeat weapon/wager checks with matching DLLs/config on every peer. Multiplayer is not established by a solo startup check.

Report the failed step and relevant `MelonLoader/Latest.log` lines. Settings are in `UserData/MelonPreferences.cfg`; restart after changes.
