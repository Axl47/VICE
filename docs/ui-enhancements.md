# VICE Interface

Interface adds four local HUD/map displays. All default on and can be disabled separately. They read existing game data without changing transactions, progression, or saves.

## Displays

- **Minimap:** a square, player-centered view at the top right. North stays up; the arrow follows camera direction. It shows native icons and eligible customer portraits, hides with the phone/pause screen/HUD, and follows movement and teleports.
- **Tracked deals:** agreed total payment and average price per requested unit beside each deal. For example, $1,000 for 30 units shows $33.3/unit. Delivery bonuses/deductions are excluded; mixed products use their combined quantity.
- **Customer map:** portraits for unlocked, active, living customers. Indoor customers appear at their last entered door. Full-map hover, focus, panning, zooming, and controller navigation use native controls.
- **Multiplayer compass:** other players' names beside their markers. Long names truncate; ordinary quest markers keep distance labels.

Each player installs Interface to see these displays.

## Configuration

Edit `PersonalUIEnhancements` in `UserData/MelonPreferences.cfg`, then restart. The category name is retained for compatibility.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | true | Enable Interface. |
| `ShowMinimap` | true | Show the minimap. |
| `MinimapSize` | 240 | HUD width, bounded to 160–480. |
| `MinimapRange` | 160 | World units across, bounded to 60–500. Smaller values zoom in. |
| `MinimapIconScale` | 1.55 | Symbol scale, bounded to 0.5–3. Search zones keep world coverage. |
| `ShowDealPayouts` | true | Show total and per-unit payments. |
| `ShowKnownCustomers` | true | Show portraits on both maps. |
| `CustomerMarkerScale` | 0.75 | Portrait scale, bounded to 0.35–1.5. |
| `ShowMultiplayerPlayers` | true | Show multiplayer compass markers. |

Invalid numeric values use defaults. Disabling one display leaves the others available.

## Troubleshooting

Check phone/pause transitions, HUD scales, save changes, and disconnect/rejoin behavior on a copied save. Include relevant `MelonLoader/Latest.log` lines when reporting missing, duplicated, or stale markers. See the [playtest checklist](playtest.md).
