namespace VICE.Interface.Hud;

// Periodic native/UI work runs once when due, without catch-up bursts after a
// pause. Position and heading updates can keep running at the rendering rate.
internal sealed class RefreshBudget
{
    private readonly float Interval;
    private float Next = float.NegativeInfinity;

    internal RefreshBudget(float interval)
    {
        if (!float.IsFinite(interval) || interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
        Interval = interval;
    }

    internal bool Take(float now)
    {
        if (!float.IsFinite(now) || now < Next) return false;
        Next = now + Interval;
        return true;
    }

    internal void Reset() => Next = float.NegativeInfinity;
}
