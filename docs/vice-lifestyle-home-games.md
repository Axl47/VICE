# Darts and pool

Lifestyle includes experimental darts 301 and casual eight-ball, with solo practice, two-player seats, and spectators. Models are embedded, with a primitive fallback; no separate model pack is needed. Neither game includes wagers or an AI opponent.

## Access

Set `EnablePrototypes=true` under `VICE.Lifestyle.Amenities` in `UserData/MelonPreferences.cfg`, then reload the save. Herbert's Bleuball's Boutique sells a **$150 Prototype Darts Board** and **$800 Prototype Pool Table**.

Place the board on a wall and the table in an owned property using ordinary furniture controls. Pool reserves 3 × 6 half-metre grid tiles. Disabling listings keeps saved furniture definitions available. Remove these items before uninstalling Lifestyle.

All participants need matching Lifestyle/Core binaries and compatible IL2CPP S1API. Genuine two-player verification remains outstanding.

## Controls

Interact nearby to open the world view. Choose **Solo practice**, or **Two-player lobby** followed by **Join player 2** on another computer. **Spectate / Resume** hides the menu. Player 1 can **Restart / Rack**. There is no same-keyboard hot-seat mode.

| Action | Control |
| --- | --- |
| Aim | Mouse or arrows |
| Throw dart / strike cue | Hold left click or Space, then release |
| Place cue ball with ball in hand | Click or Space on clear cloth |
| Zoom | Wheel |
| Options / cancel charge | Tab |
| Cancel charge | Right click |
| Help | F1 |
| Leave | Leave / Esc |

Darts aim freezes during charging; timed displacement affects the impact preview without random dispersion. Charge for 0.1–3 seconds. Pool reaches full power at 1.5 seconds; release within 3 seconds. Spin/jump shots are unavailable.

Pause closes the view; focus loss cancels charging. Leaving restores camera and controls. Participants must remain within five metres. Departure, death/sleep, lost ownership, disconnect, furniture removal, or scene changes reset the match. Furniture saves normally; scores, seats, and ball state reset on load.

## Darts rules

Start at **301**, straight in and double out, with up to three darts per turn. Singles, doubles, triples, outer bull (25), and inner bull (50, a double) score normally. A negative remainder, remainder of one, or zero without a double busts the turn: restore its starting score and pass. A legal double checkout wins. Solo uses the same rules.

## Casual eight-ball rules

Player 1 breaks a fixed rack. The table starts open; the first legally pocketed group ball assigns solids/stripes. If both groups fall, the first captured group decides. First contact must hit a group ball on an open table, your group after assignment, or the eight only when your group was cleared before the shot.

After first contact, a ball must hit a cushion or be pocketed. Scratches, missing contact, wrong first contact, or no required rail/pot are fouls. Group balls stay down after a foul; fouls do not assign an open table. Every foul gives the incoming player ball in hand anywhere on clear cloth.

A legal own-group pot retains the turn; otherwise pass. Legally sinking the eight after clearing your group wins. An early eight, eight on the break, or foul on the eight loses. Pocketing the last group ball and eight together loses. There are no called shots/pockets or special break exemptions.

For cash Hold'em, see the [poker guide](vice-lifestyle-poker.md). See the [playtest checklist](playtest.md) before relying on multiplayer.
