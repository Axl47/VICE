namespace VICE.Enforcement.Compatibility;

internal static class Events
{
    internal static void Subscribe(Action attach, Action detach)
    {
        attach();
        global::NACops.NACops.SaveSubscriptions.Add(detach);
    }
}
