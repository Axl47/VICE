# Future personal LSD feature: closed-eye patterns

Deferred after the S1LSD comparison. The user prefers our current visual implementation and is interested in a control that closes the player's eyes to reveal animated patterns. This is a future feature for **our personal LSD preview**, not an implemented comparison option.

Preserve the accepted open-eye effect and its saved baseline. A held key or an explicit menu button could smoothly darken the world, reveal layered geometric motion and return to normal viewing when released or closed. Agree on the control and toggle/hold behavior when implementing it.

Start with slow, organic circles, spirals and lattices that emerge from darkness rather than a uniform full-screen pattern. Let breathing influence their expansion and brightness. Keep the transition gradual, use limited contrast, and make the effect easy to exit. Avoid resurrecting the abandoned per-material pattern scanning system and its update lag.

Keep normal UI interaction readable and ensure the feature exits on Stop, scene unload and mod shutdown. Decide whether the phone pauses the closed-eye view or remains usable. Audio can become slightly more enclosed during the transition, but should use the working owned mixer route rather than stripped listener filters.

The supplied S1LSD archive contains an MIT closed-eye pattern implementation that can be studied when this feature is scheduled. No pattern runtime, shortcut or button is added by this note. The current comparison's audio inputs keep `ClosedEyes=0`.

Suggested first playtest: stand still at the accepted baseline, enter/exit the closed-eye view, compare low and high strengths, then test movement, dialogue, phone, pause and cleanup. This is a design note, not a promise to port the original mod's whole closed-eye system.
