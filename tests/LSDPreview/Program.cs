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
    Check(normal is >= 3.7799f and <= 5.4001f, "The default needs a few pixels of visible motion before the focus mask, without returning to broad bends.");
    Near(normal, RippleRange.Pixels(time, 0.6f, 1, 0.5f, 1) * 0.3f, "The usual default must retain 30% of the usual full ripple range.");
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
    Check(Math.Abs(RippleRange.LensBreathing(time, 1, 1, 1)) <= 0.0551f, "Native breathing must have a separate restrained cap.");
    Near(RippleRange.LensBreathing(time, 0, 0.5f, 1), 0, "Zero ripple strength also disables lens breathing.");
    Near(RippleRange.LensBreathing(time, 0.3f, 0, 1), 0, "Zero motion disables breathing.");
}
Check(Math.Abs(RippleRange.LensBreathing(2.4f, 0.3f, 0.5f, 1)) > 0.027f,
    "Default native breathing must restore half the previously visible lens pulse.");
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
    Check(float.IsFinite(wet) && wet is >= 0.7199f and <= 0.9001f, "Single-copy gain must remain bounded throughout its slow swells.");
    Near(AudioBlend.Sample(time, 0), 0, "Zero audio strength must mute the owned return.");
    var normal = AudioBlend.Sample(time, 0.6f * 0.35f);
    Check(normal is >= 0.2351f and <= 0.2941f, "Default return should be audible at 23.5-29.4% of native source volume.");
    if (wet < 0.73f) low++;
    if (wet > 0.85f) stronger++;
}
Check(low > 200 && stronger > 100, "Echo gain must swell sometimes without disappearing between swells.");
foreach (var pitch in new[] { 0.5f, 1f, 2f })
{
    const int rate = 48000, samples = rate * 120;
    Check(DelayedClip.TryPosition(rate * 20, samples, rate, pitch, false, DelayedClip.Lead, out var position),
        "Already-playing music must enter at its delayed cursor without starting the song again.");
    Check(Math.Abs(position - (rate * 20 + (DelayedClip.Lead - DelayedClip.Delay) * rate * pitch)) < 1.1,
        "Scheduled music must account for both pitch and the scheduling lead.");
    Check(DelayedClip.TryPosition(0, samples, rate, pitch, true, DelayedClip.Lead, out position) && position > samples - rate,
        "A looping track near its start must wrap the delay to the end of the clip.");
    Check(!DelayedClip.TryPosition(0, samples, rate, pitch, false, DelayedClip.Lead, out _),
        "A non-looping track must wait for real preceding audio rather than invent an echo at its start.");
}
Check(DelayedClip.Distance(10, 9990, 10000, true) == 20, "Loop sync must compare across the cursor wrap.");
Check(DelayedClip.Distance(10, 9990, 10000, false) == 9980, "A non-loop seek must not be mistaken for a wrap.");
foreach (var pitch in new[] { float.NaN, float.PositiveInfinity, 0, -1, 4 })
    Check(!DelayedClip.TryPosition(100, 1000, 48000, pitch, true, 0, out _), "Invalid playback settings must not schedule a music copy.");
// The engine schedules against the observation's audio-clock deadline;
// delivery at different frame boundaries must not change the requested delay.
foreach (var now in new[] { 7d, 7.016, 7.1, 7.205 })
{
    var voices = new EchoVoices();
    Check(voices.TryReserve(7, now, 0.4, 1, out var slot), "An on-time event must reserve a delayed voice.");
    Check(Math.Abs(slot.Start - 7.22) < 0.000001, "The same observation must replay at +220 ms regardless of scheduling frame.");
    Check(voices.Busy(slot.Index, 7.01), "A scheduled voice must remain reserved before its delayed start.");
}
var late = new EchoVoices();
Check(!late.TryReserve(7, 7.215, 0.4, 1, out _), "A missed preparation deadline must discard an event rather than play it late.");
Check(!late.TryReserve(7, 8, 0.4, 1, out _), "A frame stall must never replay a stale event.");
var pool = new EchoVoices();
var occupied = new HashSet<int>();
for (var index = 0; index < EchoVoices.Capacity; index++)
{
    Check(pool.TryReserve(10, 10, 1, 1, out var slot) && occupied.Add(slot.Index), "Concurrent sounds must each own a distinct voice.");
    Check(pool.Busy(slot.Index, 10.1), "The delay must be included in a voice's reservation.");
}
Check(!pool.TryReserve(10.1, 10.1, 1, 1, out _), "A full pool must drop extra echoes without interrupting existing copies.");
foreach (var index in occupied) Check(pool.Busy(index, 10.1), "A dropped event must not steal any scheduled voice.");
Check(pool.TryReserve(11.28, 11.28, 1, 1, out var reused) && occupied.Contains(reused.Index), "Finished sources must be reusable without growing the pool.");
pool.Reset();
foreach (var index in occupied) Check(!pool.Busy(index, 11.28), "Mute, pause and reset must clear all future and playing reservations.");
Check(pool.TryReserve(12, 12, 2, 0.5f, out var slow), "Native pitch must be accepted for ordinary clips.");
Check(Math.Abs(slow.End - slow.Start - 4.05) < 0.000001, "A half-speed clip must reserve twice its duration plus the cleanup margin.");
pool.Release(slow.Index);
Check(!pool.Busy(slow.Index, 12), "A failed native schedule must release its reservation.");
Check(pool.TryReserve(13, 13, 2, 2, out var fast), "Fast native pitches must remain supported.");
Check(Math.Abs(fast.End - fast.Start - 1.05) < 0.000001, "Double pitch must halve the voice duration.");
Check(!pool.Advance(1) && !pool.Busy(fast.Index, 1), "An audio-device clock reset must discard every old reservation.");
Check(pool.TryReserve(1, 1, 0.4, 1, out _), "A new device clock must be usable after its reset.");
foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
{
    var voices = new EchoVoices();
    Check(!voices.TryReserve(invalid, 1, 0.4, 1, out _), "An invalid event time must never schedule playback.");
    Check(!voices.TryReserve(1, invalid, 0.4, 1, out _), "An invalid audio clock must never schedule playback.");
    Check(!voices.TryReserve(1, 1, invalid, 1, out _), "Invalid clip duration must never schedule playback.");
}
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -1, 0, 0.01f, 4 })
    Check(!new EchoVoices().TryReserve(1, 1, 0.4, invalid, out _), "Invalid or unsupported pitches must leave the return silent.");
Check(!new EchoVoices().TryReserve(1, 1, 31, 1, out _), "Long audio must not pin a short-sound voice indefinitely.");
Check(!new EchoVoices().TryReserve(1, 1, 0, 1, out _), "Empty clips must never schedule a copy.");
var irregular = new EchoVoices();
var deadlines = new double[EchoVoices.Capacity];
var audioTime = 0d;
for (var eventNumber = 0; eventNumber < 1000; eventNumber++)
{
    audioTime += 0.15 + random.NextDouble() * 0.4;
    var delivered = audioTime + random.NextDouble() * 0.05;
    var duration = 0.2 + random.NextDouble() * 0.5;
    var pitch = 0.65f + (float)random.NextDouble();
    Check(irregular.TryReserve(audioTime, delivered, duration, pitch, out var slot), "Ordinary footsteps at irregular frame rates must not exhaust the bounded pool.");
    Check(deadlines[slot.Index] <= delivered, "A voice must not be reused before its preceding delayed clip ends.");
    Check(Math.Abs(slot.Start - audioTime - EchoVoices.DelaySeconds) < 0.000001, "Engine deadlines must not accumulate drift over repeated sounds.");
    deadlines[slot.Index] = slot.End;
}
// A source stays in the native active pool until its one-shot finishes. Many
// one-shots leave timeSamples at zero: a constant cursor must not create a copy
// every frame, while a return from the free pool must count as a fresh sound.
var starts = new SoundStarts();
starts.BeginFrame();
Check(starts.Observe(1, 10, true, 0), "A newly observed pool source must identify one sound start.");
starts.EndFrame();
for (var frame = 0; frame < 20; frame++)
{
    starts.BeginFrame();
    Check(!starts.Observe(1, 10, true, 0), "A playing one-shot with a zero cursor must never be copied repeatedly.");
    starts.EndFrame();
}
starts.BeginFrame();
Check(starts.Observe(1, 11, true, 0), "Changing a pool source's clip must identify a new sound.");
starts.EndFrame();
starts.BeginFrame();
Check(!starts.Observe(1, 11, true, 500), "Ordinary sample progression must not generate an echo event.");
starts.EndFrame();
starts.BeginFrame();
Check(starts.Observe(1, 11, true, 100), "A playback cursor restart must identify a new play.");
starts.EndFrame();
starts.BeginFrame();
Check(!starts.Observe(1, 11, false, 0), "Stopping a source must not generate a copy.");
starts.EndFrame();
starts.BeginFrame();
Check(starts.Observe(1, 11, true, 0), "A stopped source beginning again must be detected.");
starts.EndFrame();
starts.BeginFrame();
starts.EndFrame();
starts.BeginFrame();
Check(starts.Observe(1, 11, true, 0), "Returning from the idle pool must be a fresh onset even for an identical clip.");
starts.EndFrame();
starts.Reset();
starts.BeginFrame();
Check(starts.Observe(1, 11, true, 0), "Reset must erase old observation state so the adapter can establish a new baseline.");
Check(!starts.Observe(0, 11, true, 0) && !starts.Observe(2, 0, true, 0) && !starts.Observe(2, 11, true, -1),
    "Invalid native identities or cursors must never create a sound start.");
starts.EndFrame();
starts.Reset();
starts.BeginFrame();
for (var source = 1; source <= SoundStarts.Capacity; source++)
    Check(starts.Observe(source, 20, true, 0), "The observer must support its bounded active-source capacity.");
Check(!starts.Observe(SoundStarts.Capacity + 1, 20, true, 0), "The observer must not grow without limit.");
starts.EndFrame();
starts.BeginFrame();
starts.EndFrame();
starts.BeginFrame();
Check(starts.Observe(SoundStarts.Capacity + 1, 20, true, 0), "Departed sources must release observation capacity.");
starts.EndFrame();
Console.WriteLine($"Passed {assertions:N0} LSD timing, visibility, peripheral bounds, movement comfort, color ownership, passive sound observation and scheduled echo assertions.");
