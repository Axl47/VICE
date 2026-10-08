namespace PersonalMods.Networking;

// A load-complete callback can precede the remote player becoming eligible for
// requests. Retry at a bounded rate until an authoritative snapshot arrives.
internal sealed class InitialStateSync
{
    private bool received;
    private double next;
    internal bool Awaiting => !received;
    internal bool Take(double now)
    {
        if (received || !double.IsFinite(now) || now < next) return false;
        next = now + 2;
        return true;
    }
    internal void Received() => received = true;
    internal void Reset() { received = false; next = 0; }
}
