namespace PersonalMods.LSDPreview;

internal readonly record struct EchoSlot(int Index, double Start, double End);

// Engine DSP deadlines, shared by the native adapter and scheduling checks.
// Reserving a slot includes its delay: concurrent events cannot steal a voice
// that has been scheduled but has not started yet.
internal sealed class EchoVoices
{
    internal const double DelaySeconds = 0.22, MinimumLeadSeconds = 0.01;
    internal const int Capacity = 12;
    private readonly double[] Ends = new double[Capacity];
    private double LastTime = double.NaN;

    internal bool Advance(double now)
    {
        if (!double.IsFinite(now) || (double.IsFinite(LastTime) && now < LastTime))
        { Reset(); LastTime = now; return false; }
        LastTime = now;
        return true;
    }

    internal bool TryReserve(double eventTime, double now, double seconds, float pitch, out EchoSlot slot)
    {
        slot = default;
        if (!Advance(now) || !double.IsFinite(eventTime) || eventTime > now || eventTime < 0
            || !double.IsFinite(seconds) || seconds <= 0 || seconds > 30
            || !float.IsFinite(pitch) || pitch < 0.1f || pitch > 3) return false;
        var start = eventTime + DelaySeconds;
        if (start < now + MinimumLeadSeconds) return false;
        var index = Array.FindIndex(Ends, end => end <= now);
        if (index < 0) return false;
        var endTime = start + seconds / pitch + 0.05;
        Ends[index] = endTime;
        slot = new EchoSlot(index, start, endTime);
        return true;
    }

    internal bool Busy(int index, double now) => Ends[index] > now;
    internal void Release(int index) => Ends[index] = 0;
    internal void Reset() { Array.Clear(Ends); LastTime = double.NaN; }
}
