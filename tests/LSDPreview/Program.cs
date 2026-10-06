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
    Check(maximum is >= 4.1999f and <= 6.0001f, "The absolute maximum must remain within six pixels, with gentle continuous breathing.");
    Check(normal is >= 0.3779f and <= 0.5401f, "Default displacement must remain below 0.54 pixels even at the edge and peak.");
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
    var inner = RippleRange.FocusRadius(12, fov);
    var outer = Math.Max(inner + 0.18f, 0.48f);
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
var audible = 0;
for (var step = 0; step <= 1200; step++)
{
    var time = step * 0.1f;
    var wet = AudioBlend.Sample(time, 1);
    Check(float.IsFinite(wet) && wet is >= 0.1049f and <= 0.3501f, "Echo feedback must remain below unity at every strength and phase.");
    Near(AudioBlend.Sample(time, 0), 0, "Zero audio strength must mute the owned return.");
    Check(AudioBlend.Sample(time, 0.6f * 0.35f) <= 0.07351f, "Default echo return must stay at or below 7.35% of captured audio.");
    if (wet < 0.11f) low++;
    if (wet > 0.25f) audible++;
}
Check(low > 200 && audible > 100, "Echo strength must swell occasionally while keeping a quiet return between swells.");
var clock = new EchoClock();
Check(clock.Take(0, 48000) == 0, "Audio capture must begin with an empty clock.");
var timeSum = 0d;
var totalFrames = 0;
for (var frame = 0; frame < 1000; frame++)
{
    timeSum += 0.004 + random.NextDouble() * 0.03;
    var frames = clock.Take(timeSum, 48000);
    Check(frames >= 0 && frames <= EchoClock.HistoryFrames && frames % EchoClock.PacketFrames == 0,
        "Audio chunks must fit the recent output history at irregular frame rates.");
    totalFrames += frames;
    Check(frames + clock.PendingFrames <= EchoClock.HistoryFrames, "Captured packets and their deferred tail must fit the available listener history.");
    Check(Math.Abs(Math.Floor(timeSum * 48000) - clock.PendingFrames - totalFrames) <= 1,
        "Pending samples must shift the capture window so consecutive packets do not skip or duplicate the deferred tail.");
}
Check(Math.Abs(totalFrames - timeSum * 48000) < EchoClock.PacketFrames,
    "Irregular frame timing must neither duplicate samples nor lose more than one partial packet.");
Check(clock.Take(timeSum + 2, 48000) == -1, "A stall must request clearing rather than replaying stale samples.");
clock.Reset();
Check(clock.Take(30, 48000) == 0, "Resume/reset must start a fresh capture timeline.");
Check(clock.Take(29, 48000) == -1, "A device clock reset must discard stale audio.");
// An impulse in recent stereo output must reappear once, in the proper
// channels, at dry-time + 220 ms, including when the owned clip wraps.
foreach (var playback in new[] { 1536, 95872, 20 })
{
    var inputLeft = new float[1024];
    var inputRight = new float[1024];
    var packet = new float[EchoClock.PacketFrames * 2];
    var loop = new float[96000 * 2];
    inputLeft[300] = 1;
    inputRight[300] = -0.5f;
    for (var start = 0; start < 1024; start += EchoClock.PacketFrames)
    {
        EchoBuffer.CopyStereo(i => inputLeft[i], i => inputRight[i], (i, value) => packet[i] = value,
            start, EchoClock.PacketFrames);
        var destination = EchoBuffer.Destination(playback, 10560, 1024, start, 96000);
        for (var sample = 0; sample < packet.Length; sample++) loop[(destination * 2 + sample) % loop.Length] = packet[sample];
    }
    var expected = (playback - 1024 + 300 + 10560) % 96000;
    Near(loop[expected * 2], 1, "The left impulse must have the intended delay.");
    Near(loop[expected * 2 + 1], -0.5f, "The right impulse must preserve its channel and polarity.");
    Check(loop.Count(value => value != 0) == 2, "A stereo impulse must not leave extra samples in the owned loop.");
}
var unsafeSamples = new[] { float.NaN, float.PositiveInfinity, -5f, 5f };
var safeStereo = new float[8];
EchoBuffer.CopyStereo(i => unsafeSamples[i], i => unsafeSamples[i], (i, value) => safeStereo[i] = value, 0, 4);
Check(safeStereo.SequenceEqual(new[] { 0f, 0, 0, 0, -1, -1, 1, 1 }), "Invalid/over-range capture data must never reach the audio return.");
Console.WriteLine($"Passed {assertions:N0} LSD timing, visibility, peripheral bounds, movement comfort, color ownership and PCM echo assertions.");
