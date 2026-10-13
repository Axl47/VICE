namespace VICE.Lifestyle.Poker;

// Restoration is registered before acquisition, including a partially failed entry.
internal sealed class PresentationLease<T> : IDisposable
{
    private readonly T Snapshot;
    private readonly Action<T> Restore;
    internal bool Active { get; private set; }
    internal PresentationLease(Func<T> capture,Action acquire,Action<T> restore)
    {
        Snapshot=capture();Restore=restore;Active=true;
        try{acquire();}catch{Dispose();throw;}
    }
    public void Dispose(){if(!Active)return;Active=false;Restore(Snapshot);}
}


