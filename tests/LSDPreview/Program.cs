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
Check(trip.Elapsed == 10 && trip.Envelope == beforePause, "A real pause must suspend the timeline.");
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
// Exhaustive regression checks for the contexts that used to hide the effect.
foreach (var riding in new[] { false, true })
foreach (var paused in new[] { false, true })
foreach (var focused in new[] { false, true })
foreach (var typing in new[] { false, true })
foreach (var menu in new[] { false, true })
foreach (var canLook in new[] { false, true })
foreach (var cameraUi in new[] { false, true })
{
    var suspend = PreviewVisibility.Suspend(paused, focused, typing, menu, riding, canLook, cameraUi);
    Check(suspend == paused, "Only a real pause may suspend perception; focus, phone, dialogue, typing, voice/UI and transport restrictions must not.");
}
trip.Start(100);
trip.Step(20);
trip.Step(10, PreviewVisibility.Suspend(false, true, false, false, true, false, true));
Near(trip.Elapsed, 30, "The trip must keep advancing during skateboard/car camera restrictions.");
trip.Step(10, PreviewVisibility.Suspend(false, true, false, true, true, false, true));
Near(trip.Elapsed, 40, "Opening the phone while riding must keep the trip advancing.");
trip.Step(10, PreviewVisibility.Suspend(false, false, true, true, false, false, true));
Near(trip.Elapsed, 50, "Unfocused gameplay with typing/dialogue/UI restrictions must keep the trip advancing.");
trip.Step(10, PreviewVisibility.Suspend(true, false, true, true, false, false, true));
Near(trip.Elapsed, 50, "A true pause must still preserve trip progress.");
for (var step = 0; step <= 1200; step++)
{
    var time = step * 0.1f;
    var maximum = RippleRange.Pixels(time, 1, 1, 1, 1);
    var normal = RippleRange.Pixels(time, 0.6f, RippleRange.DefaultStrength, 0.5f, 1);
    Check(maximum is >= 12.5999f and <= 18.0001f, "The absolute maximum must remain within eighteen pixels, with gentle continuous breathing.");
    Check(normal is >= 5.9061f and <= 8.4376f, "The stronger default must remain within 5.90625-8.4375 pixels rather than returning to broad bends.");
    Near(normal, RippleRange.Pixels(time, 0.6f, PreviewTuning.PreviousRipple, 0.5f, 1) * 1.25f,
        "Every stationary default ripple sample must be exactly 25% stronger than 0.12.0.");
    Near(RippleRange.Pixels(time, 0.6f, 0, 0.5f, 1), 0, "Zero ripple strength must disable displacement.");
    Near(RippleRange.Pixels(time, 0.6f, 1, 0, 1), 0, "Zero motion strength must disable displacement.");
    Near(RippleRange.Pixels(time, 0, 1, 1, 1), 0, "A pause or reset must disable displacement.");
}
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Near(RippleRange.Pixels(invalid, 1, 1, 1, 1), 0, "Invalid displacement time must remain inert.");
    Near(RippleRange.Pixels(10, 1, invalid, 1, 1), 0, "Invalid displacement strength must remain inert.");
    Near(RippleRange.Pixels(10, invalid, 1, 1, 1), 0, "Invalid intensity must remain inert.");
}
foreach (var fov in new[] { 45f, 65f, 90f, 120f })
{
    var inner = RippleRange.FocusRadius(RippleRange.DefaultCenterDegrees, fov);
    var outer = RippleRange.FocusOuter(inner);
    var previous = 0f;
    for (var step = 0; step <= 200; step++)
    {
        var radius = step / 200f;
        var mask = RippleRange.FocusMask(radius, inner, outer);
        Check(mask is >= 0 and <= 1 && mask >= previous, "Peripheral displacement must increase smoothly outwards.");
        if (radius <= inner) Near(mask, 0, "The entire gaze center must have exactly zero displacement.");
        previous = mask;
    }
    Near(RippleRange.FocusMask(outer, inner, outer), 1, "The outer periphery must receive the bounded full effect.");
}
Check(RippleRange.FocusMask(0.25f, RippleRange.FocusRadius(5, 65), RippleRange.FocusOuter(RippleRange.FocusRadius(5, 65))) > 0.9f,
    "Mid-peripheral scenery must receive visible motion rather than a near-zero fade.");
for (var step = 0; step <= 120; step++)
{
    var time = step * 0.1f;
    Check(Math.Abs(RippleRange.LensBreathing(time, 1, 1, 1)) <= 0.1101f, "Native breathing must retain the earlier restrained cap.");
    Near(RippleRange.LensBreathing(time, 0, 0.5f, 1), 0, "Zero ripple strength also disables lens breathing.");
    Near(RippleRange.LensBreathing(time, 0.3f, 0, 1), 0, "Zero motion disables breathing.");
    Near(RippleRange.LensBreathing(time, RippleRange.DefaultStrength, 0.5f, 1),
        RippleRange.LensBreathing(time, PreviewTuning.PreviousRipple, 0.5f, 1) * 1.25f,
        "The 0.12.1 ripple default must increase native breathing by 25% over 0.12.0, without recalibrating its reference.");
}
Check(Math.Abs(RippleRange.LensBreathing(2.4f, 0.3f, 0.5f, 1)) > 0.054f,
    "Default native breathing must restore the previously visible lens pulse.");
// Imported FBX vertices can be 100x the world scale. Preserve renderer scale
// before fitting; normally sized bodies must pass through without resizing.
Near(GhostSizing.Fit(180 * 0.01f), 1, "A 0.01-scale imported 180-unit character must remain 1.8m tall.");
Near(GhostSizing.Fit(1.75f), 1, "Ordinary native character height must be preserved.");
Near(180 * GhostSizing.Fit(180), 2.1f, "Unexpected giant baked bounds must be limited to adult height.");
Near(0.018f * GhostSizing.Fit(0.018f), 1.25f, "Unexpected tiny captures must not disappear.");
foreach (var height in new[] { float.NaN, float.PositiveInfinity, 0, -1f })
    Near(GhostSizing.Fit(height), 0, "Invalid geometry must not create a figure.");
var comfort30 = new MovementComfort();
var comfort120 = new MovementComfort();
for (var frame = 0; frame < 90; frame++) comfort30.Step(1f / 30, 4, 0);
for (var frame = 0; frame < 360; frame++) comfort120.Step(1f / 120, 4, 0);
Near(comfort30.Factor, comfort120.Factor, "Movement comfort must be independent of the frame rate.");
Check(comfort30.Factor is > 0.62f and < 0.63f, "Walking must soften bending rather than intensify it.");
var walking = comfort30.Factor;
for (var frame = 0; frame < 90; frame++) comfort30.Step(1f / 30, 20, 250);
Check(comfort30.Factor < walking && comfort30.Factor >= 0.25f, "Fast travel/turning must reduce bending further without a strength jump.");
for (var frame = 0; frame < 150; frame++) comfort30.Step(1f / 30, 0, 0);
Check(comfort30.Factor > 0.999f, "Standing still must gently restore the subtle breathing.");
var original = new SurfaceTint(0.25f, 0.6f, 1.3f, 0.42f);
Check(ColorBreathing.Sample(original, 7, 20, 0) == original, "Zero color strength must be an exact identity, including HDR color and alpha.");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(ColorBreathing.Sample(original, 7, invalid, 1) == original, "Invalid animation time must preserve native color.");
    Check(ColorBreathing.Sample(original, 7, 20, invalid) == original, "Invalid strength must preserve native color.");
    Near(AudioBlend.Sample(invalid, 1), 0, "Invalid audio time must mute the return.");
    Near(AudioBlend.Sample(20, invalid), 0, "Invalid audio strength must mute the return.");
}
foreach (var seed in new[] { int.MinValue, -1, 0, 1, 42, int.MaxValue })
{
    SurfaceTint? prior = null;
    for (var step = 0; step <= 1200; step++)
    {
        var tint = ColorBreathing.Sample(original, seed, step * 0.1f, 1);
        Check(tint.Alpha == original.Alpha, "Phone color animation must preserve alpha exactly.");
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
    "Color phases remain available as a future artistic parameter.");
var lease = new TintLease(original);
Check(!lease.CanRestore(original), "A phone graphic not yet written must never be restored.");
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
var low = 0;
var stronger = 0;
for (var step = 0; step <= 1200; step++)
{
    var time = step * 0.1f;
    var wet = AudioBlend.Sample(time, 1);
    Check(float.IsFinite(wet) && wet is >= 0.7199f and <= 0.9001f, "Direct echo wet gain must remain bounded throughout its slow swells.");
    Near(AudioBlend.Sample(time, 0), 0, "Zero audio strength must mute the owned return.");
    var normal = AudioBlend.Sample(time, 0.6f * PreviewTuning.DefaultAudio);
    Near(AudioBlend.Sample(time, 0.6f * PreviewTuning.DefaultAudio, 1), normal * 1.45f,
        "Fully closed eyes must strengthen the default echo by exactly 45%.");
    Near(AudioBlend.Sample(time, 0.6f * PreviewTuning.DefaultAudio, 0.5f), normal * 1.225f,
        "Partial closure must smoothly blend the echo boost.");
    Near(AudioBlend.Sample(time, 0, 1), 0, "Closed eyes must not enable disabled audio.");
    Check(AudioBlend.Sample(time, 1, 1) is >= 0 and <= 0.90f,
        "Closed-eye echo must retain the absolute wet-gain cap at maximum settings.");
    Check(normal is >= 0.2939f and <= 0.3676f, "Default native DSP wet gain must remain 29.4-36.75% before the unchanged dry mix.");
    Near(normal, AudioBlend.Sample(time, 0.6f * PreviewTuning.BaselineAudio) * 1.25f,
        "Each default echo sample must increase the approved wet return by exactly 25%.");
    if (wet < 0.73f) low++;
    if (wet > 0.85f) stronger++;
}
Check(low > 200 && stronger > 100, "Echo gain must swell sometimes without disappearing between swells.");
// Closed eyes have stricter input rules than the continuing open-eye trip.
for (var bits = 0; bits < 256; bits++)
{
    bool Flag(int bit) => (bits & (1 << bit)) != 0;
    Check(ClosedEyeView.CanClose(Flag(0), Flag(1), Flag(2), Flag(3), Flag(4), Flag(5), Flag(6), Flag(7))
        == (bits == ((1 << 0) | (1 << 3) | (1 << 6))),
        "Closed eyes require an active non-stopping trip, focus and unobstructed gameplay input.");
}
var eyes = new ClosedEyeView();
eyes.Step(ClosedEyeView.CloseSeconds / 2, true, true);
Near(eyes.Closure, 0.5f, "Eye closure should fade through a smooth midpoint.");
eyes.Step(ClosedEyeView.CloseSeconds / 2, true, true);
Near(eyes.Closure, 1, "A sustained B hold should fully hide the scene.");
eyes.Step(10, true, true);
Near(eyes.Closure, 1, "Holding longer must not stack or exceed full closure.");
eyes.Step(ClosedEyeView.OpenSeconds / 2, false, true);
Near(eyes.Closure, 0.5f, "Releasing B should smoothly reopen the view.");
eyes.Step(ClosedEyeView.OpenSeconds, false, true);
Near(eyes.Closure, 0, "Release must restore the open-eye image completely.");
Near(AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio, eyes.Closure),
    AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio), "Reopening must restore the original echo strength.");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
    Near(AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio, invalid),
        AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio), "Invalid closure must preserve open-eye audio.");
Near(AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio, 2),
    AudioBlend.Sample(30, 0.6f * PreviewTuning.DefaultAudio, 1), "Closure must clamp to fully closed.");
eyes.Step(0.2f, true, true);
var partialClosure = eyes.Closure;
foreach (var invalid in new[] { -1f, 0, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    eyes.Step(invalid, true, true);
    Near(eyes.Closure, partialClosure, "Invalid transition deltas must retain finite state.");
}
eyes.Step(float.NaN, true, false);
Near(eyes.Closure, 0, "Menus, pause, focus loss or Stop must clear closure even with invalid frame time.");
eyes.Step(10, true, true);
Near(eyes.Closure, 0, "Returning from a blocked context with B still held must not obscure UI again.");
eyes.Step(0.01f, false, true);
eyes.Step(10, true, true);
Near(eyes.Closure, 1, "A fresh hold after release should rearm closed eyes.");
eyes.Reset();
Near(eyes.Closure, 0, "Scene, death, F9, shutdown and replacement cleanup must clear closure.");
eyes.Step(10, true, true);
Near(eyes.Closure, 0, "Cleanup requires a fresh hold when the trip restarts.");
eyes.Step(0.01f, false, true);
eyes.Step(float.MaxValue, true, true);
Near(eyes.Closure, 1, "A long valid frame must clamp to full closure.");
eyes.Step(float.MaxValue, false, true);
Near(eyes.Closure, 0, "A long release frame must clamp to open eyes.");
foreach (var fps in new[] { 30, 60, 144 })
{
    var framedEyes = new ClosedEyeView();
    for (var frame = 0; frame < fps; frame++) framedEyes.Step(1f / fps, true, true);
    Near(framedEyes.Closure, 1, "Eye closure duration must be independent of frame rate.");
    for (var frame = 0; frame < fps; frame++) framedEyes.Step(1f / fps, false, true);
    Near(framedEyes.Closure, 0, "Eye reopening duration must be independent of frame rate.");
}
Console.WriteLine($"Passed {assertions:N0} LSD timing, visibility, flow bounds, movement comfort, color ownership, character sizing, native echo-gain and closed-eye input/transition assertions.");
