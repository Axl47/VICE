using PersonalMods.MoreGuns;

var assertions = 0;
void Check(bool condition, string context)
{
    assertions++;
    if (!condition) throw new InvalidOperationException(context);
}

var spin = new SpinUpState();
spin.Step(true, false, 1.5f, 2);
Check(spin.Active && !spin.Ready, "Holding aim should animate, but cannot fire before spin-up completes.");
spin.Step(false, false, 0.1f, 2);
Check(!spin.Active && !spin.Ready && spin.Elapsed == 0, "Releasing aim must discard partial spin-up.");
spin.Step(true, false, 1.5f, 2);
Check(!spin.Ready, "Re-aiming must restart the entire delay.");
spin.Step(true, false, 0.5f, 2);
Check(spin.Ready && spin.Progress == 1, "Fire becomes available when spin-up completes.");
foreach (var action in new[] { "reload", "unequip", "pause", "shop input" })
{
    spin.Step(true, true, 30, 2);
    Check(!spin.Active && !spin.Ready && spin.Elapsed == 0, $"{action} must stop spinning even after the gun was ready.");
    spin.Step(true, false, 0.1f, 2);
    Check(!spin.Ready, $"{action} must not preserve ready-to-fire state.");
    spin.Step(true, false, 2, 2);
}
spin.Reset();
spin.Step(true, false, float.NaN, 2);
spin.Step(true, false, float.PositiveInfinity, 2);
spin.Step(true, false, -1, 2);
Check(spin.Elapsed == 0 && !spin.Ready, "Invalid frame times cannot skip windup or reverse time.");
spin.Step(true, false, 20, 2);
Check(spin.Elapsed == 2 && spin.Progress == 1, "A long frame is clamped at the configured duration.");
spin.Step(true, false, 0, 4);
Check(!spin.Ready && spin.Progress == 0.5f, "Increasing the delay must update readiness.");
spin.Step(true, false, 0, 1);
Check(spin.Ready && spin.Elapsed == 1, "Shortening the delay must clamp the existing progress.");
spin.Reset();
spin.Step(false, false, 0, 0);
Check(spin.Ready && !spin.Active, "Zero windup permits ordinary firing without holding aim.");
spin.Step(false, true, 0, 0);
Check(!spin.Ready, "Blocked input must remain blocked even with windup disabled.");

// Exercise timing across low/high and irregular frame rates. Completion may fall
// on the next frame, but must never arrive early or depend on frames counted.
var random = new Random(4702);
for (var run = 0; run < 250; run++)
{
    spin.Reset();
    var duration = 0.2f + (float)random.NextDouble() * 4;
    var elapsed = 0d;
    var maxFrame = 0d;
    while (!spin.Ready)
    {
        var delta = 0.004f + (float)random.NextDouble() * 0.1f;
        elapsed += delta;
        maxFrame = Math.Max(maxFrame, delta);
        spin.Step(true, false, delta, duration);
        if (elapsed < duration - 0.0001) Check(!spin.Ready, "A shot cannot start early at an irregular frame rate.");
        Check(spin.Progress is >= 0 and <= 1, "Animation progress stays within its usable range.");
        if (elapsed > duration + 1) throw new InvalidOperationException("Spin-up failed to complete.");
    }
    Check(elapsed <= duration + maxFrame + 0.0001, "Spin-up finishes within one frame of the configured delay.");
    spin.Step(true, false, 1, duration);
    Check(spin.Ready && spin.Elapsed == duration, "Holding aim after firing preserves full barrel speed.");
    spin.Reset();
    Check(!spin.Ready && !spin.Active, "A scene change clears ready-to-fire state.");
}
Console.WriteLine($"Passed {assertions:N0} weapon spin-up assertions.");
