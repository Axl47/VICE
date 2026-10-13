using VICE.Lifestyle.Amenities;

internal static class PresentationTests
{
    private sealed record Controls(bool Main,bool Overlay,bool Look,bool Move,bool Hotbar,bool Equip,bool Typing,bool Cursor,int Lock,double Fov,string Pose);
    internal static void Run(Action<bool,string> check)
    {
        var count=0;
        void Check(bool value,string text){count++;check(value,"Presentation: "+text);}
        foreach(var original in new[]{new Controls(true,true,true,true,true,true,false,false,1,85,"prior player pose"),new Controls(false,false,false,false,false,false,true,true,0,65,"prior cutscene pose")})
        {
            var current=original;var restores=0;
            var lease=new PresentationLease<Controls>(()=>current,()=>current=current with{Main=false,Overlay=false,Look=false,Move=false,Hotbar=false,Equip=false,Typing=true,Cursor=true,Lock=0},snapshot=>{current=snapshot;restores++;});
            Check(lease.Active&&!current.Main&&!current.Move&&current.Typing,"entry acquires rendering and input");
            Check(current.Fov==original.Fov&&current.Pose==original.Pose,"dedicated view leaves native FOV/transform alone");
            lease.Dispose();Check(current==original&&!lease.Active&&restores==1,"normal/disabled prior states restore exactly");
            lease.Dispose();Check(restores==1,"second cleanup does not overwrite newer control ownership");
        }
        var restored=0;
        try{_ = new PresentationLease<int>(()=>42,()=>throw new InvalidOperationException("partial entry"),prior=>{Check(prior==42,"partial failure retains prior snapshot");restored++;});}
        catch(InvalidOperationException){}
        Check(restored==1,"partial acquisition always restores once");
        var fails=new PresentationLease<int>(()=>1,()=>{},_=>{restored++;throw new InvalidOperationException("dead native owner");});
        try{fails.Dispose();}catch(InvalidOperationException){}
        Check(!fails.Active,"restoration error cannot leave lease active");fails.Dispose();Check(restored==2,"failed restoration cannot run a second time");
        var captured=false;
        try{_ = new PresentationLease<int>(()=>throw new InvalidOperationException("not ready"),()=>captured=true,_=>{});}catch(InvalidOperationException){}
        Check(!captured,"capture failure cannot acquire controls");
        var currentOwner=1;var uiCount=1;var overrideActive=false;var enabled=false;
        var handoff=new DeferredPresentationRestore<bool>(true,()=>currentOwner==1,()=>uiCount==0&&!overrideActive,prior=>enabled=prior);
        handoff.Pump();Check(handoff.Pending&&!enabled,"second UI retains control while it is open");
        uiCount=0;overrideActive=true;handoff.Pump();Check(handoff.Pending&&!enabled,"camera-only override retains control without a UI key");
        overrideActive=false;handoff.Pump();Check(!handoff.Pending&&enabled,"closing takeover repairs captured frozen controls");
        var stale=new DeferredPresentationRestore<bool>(false,()=>currentOwner==1,()=>true,prior=>enabled=prior);currentOwner=2;stale.Pump();
        Check(!stale.Pending&&enabled,"scene/player replacement cannot restore an old player's flags");
        foreach(var art in new[]{false,true})foreach(var x in new[]{-.12,0,.12})
        {
            var shown=HomeGameVisualContract.Dart(x,.12,art);var aimed=WorldPlayGeometry.AimFromBoard(shown.X,shown.Y,art);
            Check(Math.Abs(aimed.X-x)<1e-9&&aimed.Y==.12,"world ray -> score -> hit is invertible in each board frame");
        }
        foreach(var artwork in new[]{false,true})
        {
            var analytical=WorldPlayGeometry.KeyboardX(1,true,artwork);var shown=HomeGameVisualContract.Dart(analytical,.1,artwork);
            Check(shown.X<0,"right arrow moves screen-right (root -X) for both board frames");
        }
        foreach(var darts in new[]{false,true})
        {
            var aim=WorldPlayGeometry.Nudge(0,0,1,1,100,darts);Check(Math.Abs(aim.X)<.06&&Math.Abs(aim.Y)<.06,"keyboard catch-up is bounded after a stall");
            for(var i=0;i<1000;i++)aim=WorldPlayGeometry.Nudge(aim.X,aim.Y,1,1,.1,darts);
            Check(aim.X==(darts?.225:.56)&&aim.Y==(darts?.225:1.12),"keyboard target remains on the playable plane");
            Check(WorldPlayGeometry.Zoom(60,100,darts)==(darts?16:38)&&WorldPlayGeometry.Zoom(60,-100,darts)==(darts?40:72),"zoom has readable bounded views");
        }
        Check(WorldPlayGeometry.DartFlight(-1)==0&&WorldPlayGeometry.DartFlight(.24)==1,"flight stops exactly on the board face");
        Check(WorldPlayGeometry.OverheadSize(64)>PoolSimulation.HalfLength+.14,"default overhead view contains the table and all pockets above the HUD");
        Check(WorldPlayGeometry.StrokeClearance(0)==.035&&Math.Abs(WorldPlayGeometry.StrokeClearance(.16)+.010)<1e-9,"cue animation starts behind cue and finishes through the strike point");
        foreach(var material in new[]{"WireMetal","SisalDark","SisalLight","RingRed","RingGreen","Numbers","DartPlastic","DartFlight","BallAtlas"})
        {
            var original=HomeGameVisualContract.Surface(material);var runtime=HomeGameVisualContract.RuntimeSurface(material);
            Check(original.Metallic==runtime.Metallic&&original.Smoothness==runtime.Smoothness&&runtime.Specular&&runtime.Reflections,"darts/ball tuning preserved");
        }
        var wood=HomeGameVisualContract.RuntimeSurface("Wood");Check(!wood.Specular&&!wood.Reflections&&wood.Smoothness<.52,"pool edge highlights have an isolated matte policy");
        var session=new AmenitySession{Id=Guid.NewGuid().ToString()};
        AmenityCommand Command(string action)=>new(){Table=session.Id,Instance=session.Instance,Revision=session.Revision,Action=action};
        session.Execute(4,Command("solo"),100);session.Execute(4,Command("charge"),101);
        session.Execute(5,Command("cancel"),101.1);Check(session.Charging==4,"spectator cannot cancel participant charge");
        var cancel=Command("cancel");cancel.Revision=0;session.Execute(4,cancel,101.2);
        Check(session.Charging==-1&&session.Darts.Throws==0&&session.Darts.Remaining[0]==301,"stale cancellation changes neither score nor turn");
        var rejected=false;try{session.Execute(4,Command("release"),101.3);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"release after menu/focus cancellation cannot fire");
        session.Execute(4,Command("charge"),102);session.Execute(4,Command("leave"),102.1);
        Check(session.Charging==-1&&session.Mode=="idle","exit releases pending charge and table");
        Console.WriteLine($"PASS: {count} presentation lease, aiming/fallback, animation, material and cancellation assertions (pure .NET; no native lifecycle claim).");
    }
}
