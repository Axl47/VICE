# Cash poker

Poker is an ordinary Lifestyle feature, built into `VICE.Lifestyle.dll` with shared file services in `VICE.Core.dll`. It uses primitive furniture, physical card/cash representations and native fonts. No home-games asset bundle, dealer model or additional gameplay DLL is required.

Buy the **$900 Cash Poker Table** at Herbert's Bleuball's Boutique and place it through the normal furniture system in an owned property. Item ID: `vice.lifestyle:poker`. All multiplayer participants need matching Lifestyle/Core binaries and the installed IL2CPP S1API dependency. This is a two-to-four-human-player game; there is no NPC opponent or solo cash practice mode.

## Controls and sessions

Interact with the table to enter a seat-framed world camera. The controls sit along the table edge; the play surface remains the physical table. The native camera, controls, cursor and crosshair are restored on exit. Clicking a seat changes a spectator's viewpoint and selects a free seat for joining. After joining, the camera follows that seat.

Before anyone funds the session, the initiating player can enter small/big blinds and minimum/maximum buy-ins, then choose **Set session**. Defaults are $5/$10 blinds and $200–$1,000 buy-ins. Big blind must be at least twice the small blind; minimum buy-in is at least twice the big blind. Maximum buy-in is $1,000,000 and maximum big blind is $10,000. Once stacks exist, cash out everyone before changing the session's limits.

Enter a whole-dollar amount and choose **Buy in / add**. The player pays from their own native pocket cash; remaining pocket cash stays separate and is shown beside the table stack. The stack cannot be spent in shops, the bank or other native transactions. The first funded player becomes organizer if nobody configured the table. With at least two funded seats, the organizer chooses **Next hand**. The organizer passes to a remaining seated player when they leave.

On your turn, use **Fold**, **Check / call**, **Bet / raise to $**, or **All in**. The amount is a street-total target, including your existing wager. **½ pot**, **Pot**, and **Min** fill practical targets; before a hand, Min selects the minimum buy-in. The host validates every action even if a button's local state is stale. Cards appear automatically from a fresh shuffled deck. Use **Peek** to show your own hole-card faces privately. Public community cards, wagers, cash bundles, stacks, button, turns, timeout and winner/hand descriptions remain visible to spectators.

Placeholder faces use ASCII rank/suit labels so the native shop font needs no special suit glyphs: T/J/Q/K/A for ten through ace, and C/D/H/S for clubs/diamonds/hearts/spades. For example, AS is ace of spades. Diamonds/hearts are red; clubs/spades are black.

Rebuy or add cash between hands, up to the session's maximum stack buy-in. Existing players cannot add cash during a hand. A new player may fund an empty seat during a hand and enters the next hand. Choose **Leave / cash out**, **Close view**, or Escape to leave. A funded player leaves the session, rather than merely hiding their controls. A spectator closes freely. Closing while a buy-in is pending waits for its acknowledgement, then leaves and settles the resulting stack.

Leave during a hand folds a player with remaining chips. Committed wagers remain in the pot. A departing all-in hand remains eligible until settlement. Cash-out waits until that player's hand is safe to settle. Death, sleep, disconnect or moving more than five metres from the table uses the same departure behavior. Offline or unavailable wallets keep their owed cash in the host's save; an authenticated, loaded, awake player receives it on reconnect. Failed wallet attempts retry after a delay, with funds retained.

## Rules

- No-limit Texas Hold'em, 52 distinct cards, two private hole cards, burns and flop/turn/river, best five of seven at showdown.
- Clockwise button rotation. In heads-up, the button posts the small blind and acts first preflop; the other player acts first after the flop. The big blind retains its preflop option.
- A full raise establishes the next minimum raise increment. An under-minimum raise requires an all-in. Short all-ins do not reopen an earlier actor until the total increase they face reaches the full increment applicable when they acted. Completing a short opening wager reaches at least the big blind. An unopposed player cannot raise into opponents who are all-in.
- Side pots include folded contributions but restrict eligibility to nonfolded contributors. Each pot is evaluated and split separately. Odd dollars go clockwise starting left of the button. Uncalled excess returns to its owner. A sole eligible player wins without revealing their cards, including a player who has not yet contributed when all others depart.
- All payouts go into funded table stacks. There is no house fee, rake, seeded prize pool or generated winnings. Starting another hand is explicit so players can rebuy or cash out.

Preferences in `UserData/MelonPreferences.cfg`:

```ini
[VICE.Lifestyle.Poker]
Enabled = true
TurnTimeoutSeconds = 60
```

Enabled controls shop availability; definitions stay registered for saved furniture. Host timeout is clamped to 10–300 real seconds and adopted when a session is configured. Timeout checks when possible and otherwise folds, with feedback. Native saving and wallet reservation pause the table's turn clock. Up to 32 tables may be tracked at once; cash out old tables before adding more.

## Money and saves

The host controls cards, rules, and settlement. Hole cards are private to their owner unless legitimately revealed. Pocket cash and table stacks are separate; payouts return to the original player.

The host settlement record is:

```text
<loaded save>/Modded/VICE/Poker/settlement.json
```

Buy-ins and cash-outs checkpoint native player inventory immediately. Quitting without a normal save does not undo the transfer. Saving waits for outstanding wallet acknowledgements; table actions pause during saving and short wallet reservations.

After a host restart or reload, an interrupted hand is aborted: each player retains their remaining stack plus their own contributions. Offline funds stay owed and are paid on reconnect. Furniture cannot be removed while funds or transfers remain unsettled.

Ambiguous cash or damaged settlement records block automatic recovery. Preserve the files and resolve the discrepancy before continuing; recovery does not guess or repeat a payment.

Native cash must change by exactly the whole-dollar transfer amount. At large balances or fractional-cash boundaries, an inexact buy-in is rejected; cash-out retains any amount that cannot be paid exactly. Spend pocket cash normally and retry.

Back up or restore the entire save, including **Players and Modded/VICE/Poker**, together while the game is closed. Keep matching definitions installed while custom tables or owed cash remain.

## Live checks

Poker remains experimental. Genuine multiplayer wallet/save timing, placement, controls, and camera/card readability require checks on a copied save:

1. Buy/place a table; verify the full console and private cards from all four seats, then restore movement/cursor/equipping after leaving and loading another scene.
2. Run two real clients, then four. Buy in from each distinct wallet; verify untouched pocket remainders, matching host replicas, no duplicate charge on retries and no guest private cards in spectator packets.
3. Play full hands with unequal stacks, folds, short all-ins and rebuy/cash-out. Track native cash + table stacks + contributions before/after each transfer.
4. Disconnect/reconnect a caller and an all-in player; sleep/die during a hand; restart the host with an interrupted hand; verify each owed amount is paid exactly once.
5. Save while a transfer is pending, reload, and attempt furniture removal with funds present. Check failed/ambiguous recovery preserves records and blocks another payment.

See the [build guide](vice-build.md) and [data guide](vice-user-data.md).
