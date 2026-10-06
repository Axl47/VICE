using PersonalMods.LSDPreview;

var assertions = 0;
void Check(bool value, string message)
{
    assertions++;
    if (!value) throw new InvalidOperationException(message);
}
void Near(float actual, float expected, string message)
    => Check(Math.Abs(actual - expected) < 0.0001f, message);

var trip = new TripTimeline();
trip.Start(100);
Check(trip.Active && trip.Phase == TripPhase.Rising && trip.Envelope == 0, "Starting must fade in from zero.");
trip.Step(15);
Check(trip.Phase == TripPhase.Peak && trip.Envelope == 1, "The effect should reach a sustained peak.");
trip.Step(60);
Check(trip.Phase == TripPhase.Fading && trip.Envelope == 1, "The comedown must join the peak continuously.");
trip.Step(12.5f);
Near(trip.Envelope, 0.5f, "The comedown midpoint should be half strength.");
trip.Step(12.5f);
Check(!trip.Active && trip.Phase == TripPhase.Inactive && trip.Envelope == 0 && trip.Elapsed == 0,
    "Natural expiration must clear all timing state.");

foreach (var delta in new[] { -1f, 0, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    trip.Start(90);
    trip.Step(4);
    var elapsed = trip.Elapsed;
    var envelope = trip.Envelope;
    trip.Step(delta);
    Check(trip.Elapsed == elapsed && trip.Envelope == envelope, "Invalid frame times must neither advance nor corrupt an effect.");
}
trip.Start(90);
trip.Step(10);
var beforePause = trip.Envelope;
trip.Step(400, suspended: true);
Check(trip.Elapsed == 10 && trip.Envelope == beforePause, "Menus/focus loss must suspend the timeline.");
trip.RequestStop();
Near(trip.Envelope, beforePause, "Stopping must begin at the current strength without a jump.");
trip.Step(1);
Near(trip.Envelope, beforePause * 0.5f, "The short stop fade must retain the current intensity.");
trip.RequestStop();
trip.Step(1);
Check(!trip.Active && trip.Envelope == 0, "Repeated toggles must not restart or extend an in-progress stop fade.");
trip.Start(90);
trip.RequestStop();
Check(!trip.Active, "An immediate second toggle must cancel an effect before its fade-in.");
trip.Start(90);
trip.Step(20);
trip.Reset();
Check(!trip.Active && trip.Envelope == 0 && trip.Elapsed == 0 && trip.Duration == 0,
    "Emergency/scene/death cleanup must leave no timer state.");
trip.Start(90);
trip.Step(float.MaxValue);
Check(!trip.Active && trip.Envelope == 0, "A long stall must expire an effect instead of leaving a stale peak.");

foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    trip.Start(invalid);
    Check(trip.Duration == 180, "Non-finite durations must use the new three-minute default.");
    Check(TripTimeline.Safe(invalid, 0.60f, 0, 1) == 0.60f, "Invalid intensity must use a finite default.");
}
trip.Start(-20);
Check(trip.Duration == 20, "Short/negative durations must be bounded.");
trip.Start(10000);
Check(trip.Duration == 600, "Excessive durations must be bounded.");

Near(TripTimeline.PeripheralVisibility(0), 1, "Peripheral silhouettes should remain visible away from the gaze center.");
Near(TripTimeline.PeripheralVisibility(1), 0, "Looking directly at a silhouette must hide it.");
Near(TripTimeline.PeripheralVisibility(float.NaN), 0, "Invalid gaze data must hide a figure safely.");
var priorVisibility = 1f;
for (var i = 0; i <= 200; i++)
{
    var visibility = TripTimeline.PeripheralVisibility(-1 + i * 0.01f);
    Check(float.IsFinite(visibility) && visibility is >= 0 and <= 1 && visibility <= priorVisibility + 0.0001f,
        "Visibility must decrease continuously as the player turns toward the figure.");
    priorVisibility = visibility;
}

// Irregular-frame runs catch early expiration, discontinuities and dependence on frame counts.
var random = new Random(61006);
for (var run = 0; run < 120; run++)
{
    var duration = 20 + (float)random.NextDouble() * 180;
    trip.Start(duration);
    var wallTime = 0d;
    var previous = 0f;
    var stopping = run % 3 == 0;
    var stopRequested = false;
    var stopTime = 0d;
    while (trip.Active)
    {
        var delta = 0.008f + (float)random.NextDouble() * 0.25f;
        if (random.Next(20) == 0)
        {
            var storedElapsed = trip.Elapsed;
            var storedEnvelope = trip.Envelope;
            trip.Step(30, suspended: true);
            Check(trip.Elapsed == storedElapsed && trip.Envelope == storedEnvelope, "Pauses must be inert at any phase.");
        }
        if (stopping && !stopRequested && wallTime >= duration * 0.3)
        {
            trip.RequestStop();
            stopRequested = true;
            stopTime = wallTime;
        }
        var phaseBefore = trip.Phase;
        wallTime += delta;
        trip.Step(delta);
        Check(float.IsFinite(trip.Envelope) && trip.Envelope is >= 0 and <= 1, "Intensity must always remain finite and bounded.");
        if (phaseBefore == TripPhase.Fading) Check(trip.Envelope <= previous + 0.0001f, "Fade-out must never grow again.");
        if (!trip.Active)
        {
            var expectedEnd = stopRequested ? stopTime + 2 : duration;
            Check(Math.Abs(wallTime - expectedEnd) <= delta + 0.002, "The preview must stop within one frame of its deadline.");
        }
        previous = trip.Envelope;
        if (wallTime > duration + 3) throw new InvalidOperationException("Effect failed to end.");
    }
}
// Vehicle/skateboard camera restrictions are independent of real menus.
foreach (var riding in new[] { false, true })
foreach (var paused in new[] { false, true })
foreach (var focused in new[] { false, true })
foreach (var typing in new[] { false, true })
foreach (var menu in new[] { false, true })
foreach (var canLook in new[] { false, true })
foreach (var cameraUi in new[] { false, true })
{
    var suspend = PreviewVisibility.Suspend(paused, focused, typing, menu, riding, canLook, cameraUi);
    if (paused || !focused || typing || menu) Check(suspend, "Real menus, typing, pause and focus loss must hide effects in every transport mode.");
    else if (riding) Check(!suspend, "Mounted/driving camera restrictions must never hide the preview.");
    else Check(suspend == (!canLook || cameraUi), "Ordinary on-foot input blocks must still hide the preview.");
}
trip.Start(100);
trip.Step(20);
trip.Step(10, PreviewVisibility.Suspend(false, true, false, false, true, false, true));
Near(trip.Elapsed, 30, "The trip must keep advancing during skateboard/car camera restrictions.");
trip.Step(10, PreviewVisibility.Suspend(false, true, false, true, true, false, true));
Near(trip.Elapsed, 30, "Opening an actual menu while riding must suspend the trip.");
var original = new SurfaceTint(0.25f, 0.6f, 1.3f, 0.42f);
Check(ColorBreathing.Sample(original, 7, 20, 0) == original, "Zero color strength must be an exact identity, including HDR color and alpha.");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(ColorBreathing.Sample(original, 7, invalid, 1) == original, "Invalid animation time must preserve native color.");
    Check(ColorBreathing.Sample(original, 7, 20, invalid) == original, "Invalid strength must preserve native color.");
    Near(ColorBreathing.EchoWetMix(invalid, 1), 0, "Invalid audio time must mute added echoes.");
    Near(ColorBreathing.EchoWetMix(20, invalid), 0, "Invalid audio strength must mute added echoes.");
}
foreach (var seed in new[] { int.MinValue, -1, 0, 1, 42, int.MaxValue })
{
    SurfaceTint? prior = null;
    for (var step = 0; step <= 1200; step++)
    {
        var tint = ColorBreathing.Sample(original, seed, step * 0.1f, 1);
        Check(tint.Alpha == original.Alpha, "Material animation must preserve alpha exactly.");
        Check(tint.Red / original.Red is >= 0.7899f and <= 1.2101f
            && tint.Green / original.Green is >= 0.7899f and <= 1.2101f
            && tint.Blue / original.Blue is >= 0.7899f and <= 1.2101f,
            "Channel gain must stay bounded even at maximum strength; HDR values must not be clipped.");
        if (prior is { } previousTint)
            Check(Math.Abs(tint.Red - previousTint.Red) < 0.01f && Math.Abs(tint.Green - previousTint.Green) < 0.01f
                && Math.Abs(tint.Blue - previousTint.Blue) < 0.01f, "Color animation must be continuous across frames.");
        prior = tint;
    }
}
Check(ColorBreathing.Sample(original, 1, 20, 1) != ColorBreathing.Sample(original, 42, 20, 1),
    "Different materials must not all breathe in synchrony.");
var lease = new TintLease(original);
Check(!lease.CanRestore(original), "A material not yet written must never be restored.");
var owned = ColorBreathing.Sample(original, 7, 20, 1);
lease.Observe(original);
lease.Written(owned);
lease.Observe(owned);
Check(lease.Baseline == original && lease.CanRestore(owned), "Our own animation must never accumulate into the baseline.");
var nativeUpdate = new SurfaceTint(0.8f, 0.1f, 0.3f, 0.9f);
Check(!lease.CanRestore(nativeUpdate), "Cleanup must preserve a newer native or other-mod color write.");
lease.Observe(nativeUpdate);
Check(lease.Baseline == nativeUpdate, "Native color changes must become the new baseline.");
owned = ColorBreathing.Sample(lease.Baseline, 7, 30, 1);
lease.Written(owned);
Check(lease.CanRestore(owned) && lease.Baseline == nativeUpdate, "Cleanup after resumed animation must restore the newer native baseline.");
var quiet = 0;
var audible = 0;
for (var step = 0; step <= 1200; step++)
{
    var time = step * 0.1f;
    var wet = ColorBreathing.EchoWetMix(time, 1);
    Check(float.IsFinite(wet) && wet is >= 0 and <= 0.06f, "Even maximum audio strength must retain a faint bounded wet signal.");
    Near(ColorBreathing.EchoWetMix(time, 0), 0, "Zero audio strength must always be dry.");
    Check(ColorBreathing.EchoWetMix(time, 0.6f * 0.35f) <= 0.01261f, "Default added echo must stay below 1.3% wet mix.");
    if (wet == 0) quiet++;
    if (wet > 0.03f) audible++;
}
Check(quiet > 200 && audible > 100, "Echo must swell occasionally with dry intervals between swells.");
Console.WriteLine($"Passed {assertions:N0} LSD preview timing, gaze, transport, color ownership and audio envelope assertions.");
