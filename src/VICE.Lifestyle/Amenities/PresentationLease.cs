namespace VICE.Lifestyle.Amenities;

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

internal static class WorldPlayGeometry
{
    internal static double KeyboardX(double right,bool darts,bool artwork)=>darts&&!artwork?-right:right;
    internal static (double X,double Y) AimFromBoard(double localX,double localY,bool artwork)=>(artwork?-localX:localX,localY);
    internal static (double X,double Y) Nudge(double x,double y,double dx,double dy,double dt,bool darts)
    {
        var speed=darts?.12:.55;
        return(Math.Clamp(x+dx*speed*Math.Clamp(dt,0,.1),darts?-.225:-.56,darts?.225:.56),
            Math.Clamp(y+dy*speed*Math.Clamp(dt,0,.1),darts?-.225:-1.12,darts?.225:1.12));
    }
    internal static double Zoom(double fov,double wheel,bool darts)=>Math.Clamp(fov-wheel*2,darts?16:38,darts?40:72);
    internal static double OverheadSize(double fov)=>1.46*Math.Clamp(fov,38,72)/64;
    internal static double DartFlight(double elapsed)=>Math.Clamp(elapsed/.24,0,1);
    internal static double StrokeClearance(double elapsed)
    {var t=Math.Clamp(elapsed/.16,0,1);return .035-(1-Math.Pow(1-t,3))*.045;}
}

// A second UI may have captured our locked state. Restore after it releases its
// ownership, but never write a previous player's state into a new scene/player.
internal sealed class DeferredPresentationRestore<T>(T snapshot,Func<bool> sameOwner,Func<bool> canRestore,Action<T> restore)
{
    internal bool Pending { get; private set; } = true;
    internal void Pump()
    {
        if(!Pending)return;
        if(!sameOwner()){Pending=false;return;}
        if(!canRestore())return;
        Pending=false;restore(snapshot);
    }
}
