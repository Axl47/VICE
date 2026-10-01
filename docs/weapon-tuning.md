# More Guns 0.2.0

Requested changes target the existing AK47 and minigun on the IL2CPP 0.4.7f7 beta. Install only the updated `Personal.MoreGuns.dll` when the earlier six-mod package is already working.

| Behavior | New default |
| --- | --- |
| AK47 bullet spread | 1.4–4.2 degrees, down from 2–6. |
| Minigun bullet spread | 2–4 degrees, down from 4–8. |
| Bullet impact force | AK47 130, minigun 195: both 35% below the previous player-weapon values. Damage is unchanged. |
| Weapon sound gain | AK47 0.4×, minigun 0.25× the recovered original sound levels, followed by the game's master/effects settings. |
| Firing camera movement | Short native shake with strength setting 0.5. The original jolt can be restored with 1 or disabled with 0. |
| Minigun spin-up | The bundle's `MiniGun Windup` barrel animation loops and accelerates while aiming. Reload, unequip and input blocking stop it; aim release plays spin-down sound. |

The reload animation was working, but the owner's log shows `TrashItem.Initialize` throwing while the AK47 magazine is discarded. Inspection of the installed native reload coroutine confirms that it consumes the spare before creating trash and sets the weapon's ammo afterward. The old bundle provides generic `Draggable`; the beta requires `TrashItemDraggable`. The prefab is repaired in memory, including its renamed `Id`, body, interaction and trash references. If the prefab cannot be repaired, cosmetic discard is skipped while normal reload remains available.

The beta also renamed audio-controller fields. The patch explicitly restores the bundle's fire/reload/spin levels and applies the native master/effects calculation. Repeated refreshes use a fixed base level, so volume cannot compound downward. Both local and avatar weapon sounds are configured.

Avatar replacement clears stale models before equipping the next one and supplies an empty equip-clip array when required by the beta. This addresses the model-switch errors in the owner's log. Camera customization is restricted to each custom weapon's synchronous native firing call; a finalizer restores that scope even if firing throws.

Build/API/native-address audits and automated spin-up timing checks are recorded in `validation.md`. The new trash component, sound controls, animation appearance and camera strength still require the owner's next in-game run; see step 6 of `playtest.md`.
