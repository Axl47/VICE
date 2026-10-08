namespace PersonalMods.LSDGameplay;

internal enum CabinetStep { OpenDoor, PullTray, TransferSheet, PushTray, CloseDoor, Commit }

// Presentation stays provisional until the closed-door inventory transaction.
internal sealed class CabinetSequence
{
    internal CabinetStep Step { get; private set; }
    internal bool Moving { get; private set; }
    internal float Progress { get; private set; }
    internal bool ReadyToCommit=>Step==CabinetStep.Commit;
    private float Elapsed;
    private float Duration=>Step==CabinetStep.TransferSheet ? 0.7f : Step is CabinetStep.OpenDoor or CabinetStep.CloseDoor ? 0.55f : 0.45f;
    internal void Advance()
    {
        if(Moving || ReadyToCommit)return;
        Moving=true;Elapsed=Progress=0;
    }
    internal void Tick(float seconds)
    {
        if(!Moving || seconds<=0 || !float.IsFinite(seconds))return;
        Elapsed+=seconds;var t=Math.Clamp(Elapsed/Duration,0,1);Progress=t*t*(3-2*t);
        if(t<1)return;
        Moving=false;Step++;Progress=0;
    }
    internal float Door=>Step==CabinetStep.OpenDoor ? Progress : Step==CabinetStep.CloseDoor ? 1-Progress : Step==CabinetStep.Commit ? 0 : 1;
    internal float Tray=>Step==CabinetStep.PullTray ? Progress : Step==CabinetStep.PushTray ? 1-Progress : Step is CabinetStep.TransferSheet ? 1 : 0;
    internal float Transfer=>Step<CabinetStep.TransferSheet ? 0 : Step==CabinetStep.TransferSheet ? Progress : 1;
}
