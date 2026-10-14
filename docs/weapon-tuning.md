# VICE Arsenal weapons and tuning

Arsenal adds an **AK47 ($15,000, 30 rounds)** and **minigun ($75,000, 400 rounds)** to the arms dealer, with matching spare magazines. Both support automatic firing and magazine reloads. Purchase rank restrictions default off.

Hold aim to spin up the minigun before firing. Releasing aim, reloading, switching weapons, or blocked input resets the delay.

## Configuration

Edit `UserData/MelonPreferences.cfg` while the game is closed. Category `PersonalMoreGuns` controls module enablement; `PersonalMoreGuns_ak47` and `PersonalMoreGuns_minigun` control each weapon. These names are retained for compatibility.

| Setting | AK47 default | Minigun default |
| --- | --- | --- |
| `SpreadMultiplier` | 0.7 | 0.5 |
| `ImpactForceMultiplier` | 0.65 | 0.65 |
| `SoundVolumeMultiplier` | 0.4 | 0.25 |
| `CameraShakeMultiplier` | 0.5 | 0.5 |
| `RequirePurchaseRank` | false | false |

Lower spread tightens shots. Impact force changes knockback. Sound gain also follows master/effects sliders. Camera shake can be disabled with 0 or restored to its original strength with 1. Other settings cover prices, damage, fire interval, magazine size, and windup duration.

## Saves and troubleshooting

Keep Arsenal installed while custom guns, magazines, or discarded magazines exist in a save. `Enabled=false` stops shop/input additions while retaining item definitions for loading.

After updates, check full/partial reloads, weapon switching, minigun spin-up, audio sliders, and saved-item persistence on a copied save. Report relevant `MelonLoader/Latest.log` errors. See the [playtest checklist](playtest.md) and [asset credits](provenance.md).
