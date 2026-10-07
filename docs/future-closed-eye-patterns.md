# Personal LSD feature: closed-eye patterns

The first version is implemented in **Personal LSD Preview 0.12.0**, following the design below. Hold **B** during an active trip to close the eyes, then release it to reopen. The feature belongs to our personal preview; the S1LSD comparison is unchanged. See the [preview guide](lsd-preview.md) for settings and installation.

Preserve the accepted open-eye effect and its saved baseline. The owner selected a held B key: closure takes 0.85 seconds, release takes 0.45 seconds. The owned completed-frame shader smoothly darkens the world and reveals localized geometric motion, with no additional pattern textures or per-material scanning.

Start with slow, organic circles, spirals and lattices that emerge from darkness rather than a uniform full-screen pattern. Let breathing influence their expansion and brightness. Keep the transition gradual, use limited contrast, and make the effect easy to exit. Avoid resurrecting the abandoned per-material pattern scanning system and its update lag.

Keep normal UI interaction readable and ensure the feature exits on Stop, scene unload and mod shutdown. Version 0.12.0 immediately clears closure for phone, dialogue, typing, menus, blocked camera input, pause and focus loss. Release B before holding again after interruption. Existing movement and audio behavior continue; enclosed audio is deferred and would need to use the working owned mixer route rather than stripped listener filters.

The supplied S1LSD archive contains an MIT closed-eye implementation available as a reference. This first version uses our authored shader and does not port that runtime. The current comparison's audio inputs keep `ClosedEyes=0`.

First playtest: stand still at the accepted baseline, enter/exit the closed-eye view, compare low and high strengths, then test movement, dialogue, phone, pause and cleanup. Check that a held B does not immediately reclose the eyes after returning from a menu. Further artistic tuning remains subject to this playtest.
