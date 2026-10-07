namespace VICE.Core;

/// <summary>Owns event subscriptions for one loaded save. Dispose before entering another save.</summary>
public sealed class SubscriptionScope : IDisposable
{
    private readonly Stack<Action> releases = new();

    public void Add(Action release) => releases.Push(release);

    public void Dispose()
    {
        while (releases.TryPop(out var release))
        {
            try { release(); }
            catch (Exception error) { ModSupport.WarnOnce("vice:unsubscribe:" + error.GetType().Name, $"Event cleanup failed: {error.Message}"); }
        }
    }
}
