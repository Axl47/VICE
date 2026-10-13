# VICE Lifestyle 0.3.0 cash poker prototype

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

## Authority, money and persistence

FishNet authenticates requests and maps the sender's connection to its loaded native Player. Stable native `PlayerCode` identifies funds across reconnects; client-supplied player IDs are never used. The host owns the deck, legal actions, hand evaluation and settlement. Epoch, table instance, revision and request tokens reject stale/duplicate actions. Snapshots are constructed per recipient; they contain only that recipient's private cards and legitimately revealed cards. They never include the deck or other unrevealed hands. The host necessarily knows every hand.

Installed native inspection established that `MoneyManager.cashInstance` reads the **local** `PlayerInventory.cashInstance`. It is unsuitable for charging a guest from the host. The feature instead reads the authenticated player's replicated `_inventory`, cross-checks a targeted owner reservation against that replica, and updates the owner's local cash instance through a targeted message. The inspected layout has eight hotbar slots followed by pocket cash at index 8. A cleared zero-cash slot can receive a payout; physical cash items in hotbar slots are preserved and excluded from the wallet. A different inventory layout fails closed. It updates the native host replica before releasing a transfer. No shared online balance is used.

The durable host record is:

```text
<loaded save>/Modded/VICE/Poker/settlement.json
```

Every accepted game action persists the host state. A money transfer writes an intent containing the proposed table state, native before/after cash and original inventory hash, then atomically checkpoints that player's native `Players/<Player.SaveFolderName>/Inventory.json`, then commits the ledger. The native inventory's other fields are preserved. Each replacement flushes data and keeps a `.bak`. Poker transfers therefore checkpoint the player's current inventory immediately; quitting without a normal game save does not undo the escrow transaction. The rest of the game continues to use its ordinary save behavior.

Native saving waits for outstanding wallet acknowledgements; new poker actions wait for saving to finish. A native save to another folder receives a paired poker checkpoint there. During a short wallet reservation, movement/equipping is suspended and unrelated native cash deltas are deferred until release. Duplicate reservation, application and completion requests cannot repeat a debit or credit. If a process stops between file writes, recovery commits a transfer whose native after-value is present, or cancels an intent whose original inventory file is unchanged. Ambiguous cash, a corrupt ledger or a missing primary ledger with a backup blocks automatic recovery. Preserve those files and resolve the discrepancy before continuing; the code never guesses which party should receive another payment.

On load or host shutdown, an interrupted hand is aborted without a winner: each player retains their own remaining stack plus their own contributions. Those funds are queued for cash-out. Offline funds stay owed under their persistent player identity. Recovery and repeated reconnects do not replay completed transfers. The table cannot be packed up or destroyed while cash or a wallet transfer remains unsettled. If another mod forcibly removes the furniture, its ledger entry survives and the interrupted hand's original funds remain payable.

Native cash is a float with a $1,000,000,000 cap. Table accounting is integer dollars. A native debit/credit is accepted only when the float changes by **exactly** that integer amount. Buy-ins that would round fractional cash, exceed the cap or lack sufficient funds are rejected. Cash-out pays the largest representable whole-dollar amount that fits, retaining any remainder as money owed at the table. If no exact amount fits, nothing is paid. This can matter at large balances or when crossing a float precision boundary; spend pocket cash normally and retry instead of rounding or minting a difference.

Back up or restore the entire save, including **Players and Modded/VICE/Poker**, together while the game is closed. Restoring only a wallet or only the poker ledger breaks the settlement checkpoint. Keep matching mod definitions installed while custom tables or owed cash remain in a save.

## Build and validation

Use the normal `scripts/Build-VICE.ps1 -Modules Lifestyle` export, which preserves the committed 0.2.2 pool/darts cameras and accepted artwork. On a clean checkout without its ignored bundle or Unity, add `-PrimitiveHomeGames` to build the complete poker prototype with pool/darts fallback meshes. The existing Lifestyle test executable covers poker automatically. Add `--exhaustive-poker` to its runtime arguments to enumerate all 2,598,960 five-card combinations. The model-worker contract is [vice-poker-model-worker-prompt.md](vice-poker-model-worker-prompt.md).

Automated checks cover all nine hand categories and kickers/ties, heads-up/four-seat flow, short and cumulative raises, varying-stack all-ins, side pots, odd-dollar splits, uncalled refunds, randomized cash conservation, private snapshots, duplicate/stale requests, departure/all-in eligibility, control lease restoration and journal recovery before/after a native write. Native patch address sharing is checked against the installed Cpp2IL metadata. Build and export fixtures check the current Core/Lifestyle pair.

These checks do **not** establish a genuine multiplayer session, native wallet/save timing, shop/placement usability, world-console clickability, or final camera/card readability. No live game installation or user's live save was changed for this work. Remaining checks must use a closed-game copied save:

1. Buy/place a table; verify the full console and private cards from all four seats, then restore movement/cursor/equipping after leaving and loading another scene.
2. Run two real clients, then four. Buy in from each distinct wallet; verify untouched pocket remainders, matching host replicas, no duplicate charge on retries and no guest private cards in spectator packets.
3. Play full hands with unequal stacks, folds, short all-ins and rebuy/cash-out. Track native cash + table stacks + contributions before/after each transfer.
4. Disconnect/reconnect a caller and an all-in player; sleep/die during a hand; restart the host with an interrupted hand; verify each owed amount is paid exactly once.
5. Save while a transfer is pending, reload, and attempt furniture removal with funds present. Check failed/ambiguous recovery preserves records and blocks another payment.

Final 3D artwork can replace the placeholders later without changing the rule or money code.
