namespace VICE.Arsenal.Guns;

// Input/visual timing shared by the native hooks, independent of Unity frame rate.
internal sealed class SpinUpState
{
    internal float Elapsed { get; private set; }
    internal bool Active { get; private set; }
    internal bool Ready { get; private set; }
    internal float Progress { get; private set; }

    internal void Step(bool aiming, bool blocked, float deltaTime, float duration)
    {
        duration = float.IsFinite(duration) ? Math.Clamp(duration, 0, 10) : 0;
        if (blocked || !aiming)
        {
            Reset();
            Ready = !blocked && duration == 0;
            return;
        }
        Active = true;
        var delta = float.IsFinite(deltaTime) ? Math.Max(0, deltaTime) : 0;
        Elapsed = Math.Min(Elapsed + delta, duration);
        Ready = duration == 0 || Elapsed >= duration;
        Progress = duration == 0 ? 1 : Elapsed / duration;
    }

    internal void Reset() { Elapsed = 0; Active = false; Ready = false; Progress = 0; }
}
