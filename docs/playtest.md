# Gameplay check checklist

Use a copied save and current VICE package with matching dependencies. Automated builds check code/package contracts; they do not replace live gameplay checks. Confirm selected modules and Core load without errors in `MelonLoader/Latest.log`.

## Commerce

- Visit the warehouse by day and after midnight. Buy/place storage furniture, then reload and check listings.
- Test counteroffers with changed products, quantities, and prices. Deliver exact packages, then insufficient/mixed packaging. Unsupported deliveries should remain manual without losing inventory.
- At both gas-marts, save/load a cart, check out, and use **Repeat last**. Restart and verify persistence. Update/delete lists; unavailable products should preserve the cart. Insufficient money/capacity should block checkout.

## Enforcement and Syndicate

- Test pursuits in streets, multi-entrance buildings, and single-entrance corridors. Check speed, detection/occlusion, flanking, spaced support, backup, arrest, and recovery after pursuit ends or a lead officer disappears.
- Check officer appearances, heat, raids, cameras, and Local Source. Reports should avoid duplicates across cooldowns/reloads/calm transitions.
- Check hostile/truced cartel states, scheduled events, quests, persuasion, and stolen inventory/customer persistence. Debug encounters do not establish scheduled frequency.

## Arsenal

- Buy both guns and spares. Check icons/models, automatic fire, ammo, empty/partial reloads, and no-spare handling. Capacities are 30 for AK47 and 400 for minigun.
- Check spin-up resets after aim release/reload/switching, volume sliders, shake, ordinary weapons, discarded magazines, and saved-item persistence.

## Lifestyle

- Resolve affordable Blackjack and Ride the Bus wagers above the native limit. Check sliders, deductions/payouts, unaffordable bets, and reloads. Slots should retain original choices/payouts.
- Buy/place a Wall Screen. Test images, MP4s, direct URLs, and configured Relay. Check pause/seek/volume, favorites/Remember, distance suspension, save/reload, and removal.
- Check darts/pool placement, aim/charge/cancel, solo rules, two-player turns, spectators, leaving, and restored controls. Include busts/double-out, fouls/ball-in-hand, and eight-ball outcomes.
- Check YouTube installation, playback, captions/chapters, Retry, and helper cleanup. A joining player should stop playback.
- Follow the [poker checks](vice-lifestyle-poker.md): distinct wallets, private cards, side pots, cash conservation, reconnects, interrupted hands, and saving during transfers. Back up inventories and settlements together.

## Interface

- Compare tracked total/unit payments with accepted terms. Check long labels, completed deals, and ordinary quests.
- Check portraits, indoor entrance positions, map navigation, and newly unlocked customers.
- Check minimap alignment, facing, clipping, phone/pause visibility, HUD scales, and size/range settings. It should work before opening the phone.
- Disable displays independently. Change saves and disconnect/rejoin peers; markers should not duplicate or remain stale.

## Substances

- Follow the [workshop loop](lsd-workshop.md): introduction/tutorial, paid deliveries, artwork, registration/printing, drying, cutting, packaging, and sales.
- Check interrupted stages, full inventory, saved artwork/batches/orders, and reload without duplicate deliveries. Consume a tab and check effects, held-B patterns, and cleanup.

## Multiplayer and reporting

Repeat applicable checks on separate host/guest computers with matching binaries, including late joins, reconnects, and save changes. Genuine multiplayer checks remain outstanding for several features; solo startup/local transport tests are insufficient.

Report the failed check, reproduction steps, game/module/dependency versions, and relevant `MelonLoader/Latest.log` lines. Restart after editing preferences.
