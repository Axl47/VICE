using System.Text.Json;
using VICE.Lifestyle.Amenities;

internal static class AmenityTests
{
    internal static void Run(Action<bool,string> check)
    {
        var count=0;
        void Check(bool value,string text){count++;check(value,"Amenities: "+text);}
        void Reject(Action action,string text)
        {try{action();}catch(Exception e)when(e is InvalidDataException or InvalidOperationException){Check(true,text);return;}Check(false,text);}
        int[] wedges={20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5};
        for(var i=0;i<20;i++)foreach(var (radius,mult) in new[]{(.05,1),(.103,3),(.166,2),(.15,1),(.18,0)})
        {
            var angle=i*Math.PI/10;var hit=DartsRules.Score(Math.Sin(angle)*radius,Math.Cos(angle)*radius);
            Check(hit.Points==wedges[i]*mult,$"wedge {wedges[i]} × {mult} scored incorrectly");
        }
        Check(DartsRules.Score(0,0).Points==50&&DartsRules.Score(0,0).Double,"inner bull is a double");
        Check(DartsRules.Score(.00635,0).Points==50&&DartsRules.Score(.006351,0).Points==25,"bull boundary ownership");
        Check(DartsRules.Score(.0159,0).Points==25&&DartsRules.Score(.015901,0).Number==6,"outer bull boundary ownership");
        Check(DartsRules.Score(0,.099).Multiplier==3&&DartsRules.Score(0,.107).Multiplier==3,"triple edges inclusive");
        Check(DartsRules.Score(0,.162).Multiplier==2&&DartsRules.Score(0,.17).Multiplier==2&&DartsRules.Score(0,.170001).Points==0,"double and miss edges");
        Check(DartsRules.Score(Math.Sin(9*Math.PI/180+.000001)*.05,Math.Cos(9*Math.PI/180+.000001)*.05).Number==1,"clockwise wedge edge");
        Reject(()=>DartsRules.Score(double.NaN,0),"NaN dart rejected");Reject(()=>DartsRules.Throw(.26,0,.5),"out of panel rejected");
        Reject(()=>DartsRules.Throw(0,0,3.01),"expired throw rejected");
        var timed=DartsRules.Throw(0,0,.2);Check(Math.Abs(timed.X-.012)<1e-8&&Math.Abs(timed.Y)<1e-8,"timing displacement pinned");
        var darts=new DartsMatch();darts.Hit(0,.103,false);darts.Hit(0,.103,false);darts.Hit(0,.103,false);
        Check(darts.Remaining[0]==121&&darts.Turn==1&&darts.Darts==0&&darts.TurnStart==301,"three darts advance turn");
        darts=new(){Remaining=new[]{61,301},TurnStart=61};darts.Hit(0,.103,false);
        Check(darts.Remaining[0]==61&&darts.Turn==1,"leaving one busts");
        darts=new(){Remaining=new[]{40,301},TurnStart=40};darts.Hit(0,.05,false);darts.Hit(0,.05,false);
        Check(darts.Remaining[0]==40&&darts.Turn==1,"single checkout bust restores whole turn");
        darts=new(){Remaining=new[]{40,301},TurnStart=40};darts.Hit(0,.166,false);Check(darts.Winner==0,"double out wins");
        Reject(()=>darts.Hit(0,0,false),"finished darts reject throws");
        darts=new(){Remaining=new[]{50,301},TurnStart=50};darts.Hit(0,0,true);Check(darts.Winner==0,"inner bull checkout");
        darts=new(){Remaining=new[]{20,301},TurnStart=20};darts.Hit(0,.103,true);Check(darts.Remaining[0]==20&&darts.Turn==0&&darts.Darts==0,"solo bust stays player one");
        darts=new();for(var i=0;i<3;i++)darts.Hit(.2,.2,true);Check(darts.Turn==0&&darts.Remaining[0]==301,"solo misses end turn");

        var pool=new PoolMatch();Check(pool.Simulation.Balls.Count==16&&pool.Simulation.Balls.Single(b=>b.Number==8).X==0,"complete eight-centred rack");
        Check(!pool.Simulation.CanPlace(double.NaN,0)&&!pool.Simulation.CanPlace(.56,0)&&!pool.Simulation.CanPlace(0,.45),"cue placement validates limits and overlap");
        Reject(()=>pool.Place(0,0),"ball in hand needed");pool.Strike(0,1);var steps=0;
        while(pool.Simulation.Moving&&steps++<8000)pool.Simulation.Step();
        Check(!pool.Simulation.Moving&&steps<8000,"maximum break settles within budget");
        Check(pool.Simulation.Shot.FirstContact==1,"break records first contact");
        Check(pool.Simulation.Balls.All(b=>double.IsFinite(b.X)&&double.IsFinite(b.Z)&&Math.Abs(b.X)<.7&&Math.Abs(b.Z)<1.3),"break remains bounded");
        var other=PoolSimulation.Rack();other.Strike(0,1);while(other.Moving)other.Step();
        Check(JsonSerializer.Serialize(other)==JsonSerializer.Serialize(pool.Simulation),"fixed simulation reproducible");
        pool.Resolve(false);Check(pool.Winner<0,"ordinary break doesn't finish game");
        // Six pocket capture regions and cushion rebound without Unity.
        foreach(var pocket in PoolSimulation.Pockets)
        {
            var sim=PoolSimulation.Rack();foreach(var b in sim.Balls)b.Pocketed=true;
            var cue=sim.Balls[0];cue.Pocketed=false;cue.X=pocket.X*.97;cue.Z=pocket.Z*.97;sim.Moving=true;
            sim.Step();Check(cue.Pocketed&&sim.Shot.Pockets.SequenceEqual(new[]{0}),"six pocket capture, no duplicate scratch");
        }
        var cushion=PoolSimulation.Rack();foreach(var b in cushion.Balls)b.Pocketed=b.Number!=0;
        cushion.Place(.52,.3);cushion.Strike(Math.PI/2,.2);for(var i=0;i<40;i++)cushion.Step();Check(cushion.Balls[0].VX<0,"side cushion reflects");
        var collision=PoolSimulation.Rack();foreach(var b in collision.Balls)b.Pocketed=b.Number!=0&&b.Number!=1;
        var one=collision.Balls.Single(b=>b.Number==1);one.X=0;one.Z=0;collision.Place(0,-.2);collision.Strike(0,.2);
        for(var i=0;i<100;i++)collision.Step();Check(collision.Shot.FirstContact==1&&one.VZ>0,"equal-mass transfer records cue contact");
        Reject(()=>collision.Strike(0,.5),"moving strike rejected");
        var railMiss=new PoolMatch();railMiss.Simulation.Shot=new(){FirstContact=1};railMiss.Resolve(false);
        Check(railMiss.BallInHand&&railMiss.Turn==1,"no rail and no pot foul");
        var assignment=new PoolMatch();assignment.Simulation.Shot=new(){FirstContact=9,Pockets=new(){9}};assignment.Resolve(false);
        Check(assignment.Groups.SequenceEqual(new[]{2,1})&&assignment.Turn==0&&!assignment.BallInHand,"first legal pot assigns groups and retains turn");
        assignment.Simulation.Shot=new(){FirstContact=1,RailAfterContact=true};assignment.Resolve(false);Check(assignment.BallInHand&&assignment.Turn==1,"wrong group first is foul");
        assignment.Place(0,-.5);Check(!assignment.BallInHand&&!assignment.Simulation.Balls[0].Pocketed,"cue restored after foul");
        var scratch=new PoolMatch();scratch.Simulation.Shot=new(){FirstContact=1,Pockets=new(){0,1}};scratch.Resolve(false);
        Check(scratch.Groups[0]==0&&scratch.BallInHand&&scratch.Turn==1,"scratch does not assign groups");
        var early=new PoolMatch();early.Simulation.Shot=new(){FirstContact=8,Pockets=new(){8}};early.Resolve(false);Check(early.Winner==1,"early eight loses including break");
        var win=new PoolMatch{Groups=new[]{1,2}};foreach(var b in win.Simulation.Balls.Where(b=>PoolMatch.Group(b.Number)==1))b.Pocketed=true;
        win.Strike(0,.1);win.Simulation.Moving=false;win.Simulation.Shot=new(){FirstContact=8,Pockets=new(){8}};win.Resolve(false);Check(win.Winner==0,"legal eight wins after group cleared");
        var eightScratch=new PoolMatch{Groups=new[]{1,2},EightEligible=true};eightScratch.Simulation.Shot=new(){FirstContact=8,Pockets=new(){8,0}};eightScratch.Resolve(false);Check(eightScratch.Winner==1,"scratch on eight loses");
        var together=new PoolMatch{Groups=new[]{1,2}};together.Simulation.Shot=new(){FirstContact=1,Pockets=new(){1,8}};together.Resolve(false);Check(together.Winner==1,"last own ball plus eight together loses");
        var practice=new PoolMatch();practice.Simulation.Shot=new(){FirstContact=-1};practice.Resolve(true);Check(practice.Turn==0&&practice.BallInHand,"solo foul stays same player");
        Reject(()=>PoolSimulation.Rack().Strike(double.PositiveInfinity,.5),"nonfinite strike rejected");

        var session=new AmenitySession{Id=Guid.NewGuid().ToString()};
        AmenityCommand Command(string action,double x=0,double y=0)=>new(){Action=action,Table=session.Id,Instance=session.Instance,Revision=session.Revision,X=x,Y=y};
        session.Execute(4,Command("duel"),100);Reject(()=>session.Execute(5,Command("solo"),100),"busy table rejects new solo");
        Reject(()=>session.Execute(4,Command("charge"),100),"duel waits for second participant");session.Execute(5,Command("join"),101);
        Reject(()=>session.Execute(6,Command("join"),101),"full seats reject spectator join");Reject(()=>session.Execute(5,Command("charge"),101),"other player's turn rejected");
        Reject(()=>session.Execute(6,Command("restart"),101),"spectator restart rejected");
        var stale=Command("charge");session.Execute(4,Command("restart"),101);Reject(()=>session.Execute(4,stale,101),"stale action rejected");
        var impostor=Command("charge");impostor.Instance=Guid.NewGuid().ToString();Reject(()=>session.Execute(4,impostor,101),"old table instance rejected");
        session.Execute(4,Command("charge",0,.10),102);session.Execute(4,Command("release"),102.4);
        Check(session.Darts.Throws==1,"host-timed dart changes score once");Reject(()=>session.Execute(4,Command("release"),102.5),"unpaired release rejected");
        session.Execute(6,Command("leave"),102.5);Check(session.Mode=="duel","spectator leaving retains table");
        Check(session.Prune(a=>a!=5,103)&&session.Mode=="idle","disconnect clears both seats");
        session.Execute(4,Command("solo"),110);session.Execute(4,Command("charge"),110);Check(session.Prune(a=>true,114)&&session.Charging==-1,"charge timeout frees input");
        Check(session.Prune(a=>true,121)&&session.Mode=="idle","heartbeat expiry releases table");
        session.Execute(4,Command("solo"),130);session.Execute(4,Command("heartbeat"),139);Check(!session.Prune(a=>true,140),"heartbeat sustains participant");
        var leave=Command("leave");leave.Revision=0;session.Execute(4,leave,140);Check(session.Mode=="idle","stale leave always releases seat");
        // Authoritative settled shot advances rule revision, never a guest supplied outcome.
        session.Kind="pool";session.Execute(4,Command("solo"),150);session.Execute(4,Command("charge",0),150);session.Execute(4,Command("release"),151.5);
        Check(session.Moving,"pool release starts host simulation");Reject(()=>session.Execute(4,Command("charge"),152),"second shot blocked while moving");
        var message=new AmenityMessage{Sequence=1,HostTime=100,AllIds=new(){session.Id},State=session};
        var wire=JsonSerializer.Serialize(message);Check(wire.Length<24576&&!wire.Contains("Heartbeats"),"wire state fits limit and excludes host lease internals");
        var decoded=JsonSerializer.Deserialize<AmenityMessage>(wire)!;AmenityProtocol.Validate(decoded);Check(decoded.State!.Moving&&decoded.State.Seats[0]==4,"late-join snapshot includes moving simulation and seat");
        decoded.Version=2;Reject(()=>AmenityProtocol.Validate(decoded),"protocol version mismatch rejected");decoded.Version=1;
        decoded.State.Pool.Simulation.Balls[0].X=double.NaN;Reject(()=>AmenityProtocol.Validate(decoded),"nonfinite authoritative snapshot rejected");
        decoded=JsonSerializer.Deserialize<AmenityMessage>(wire)!;decoded.State!.Seats[1]=4;Reject(()=>AmenityProtocol.Validate(decoded),"duplicate participant rejected");
        Reject(()=>new AmenityCommand{Token="bad"}.Validate(),"invalid idempotency identity rejected");
        var inbox=new AmenityInbox();var token=Guid.NewGuid().ToString();
        Check(inbox.First(4,token)&&!inbox.First(4,token)&&inbox.First(5,token),"duplicate delivery scoped to authenticated actor");
        for(var i=0;i<20;i++)Check(inbox.Allow(4,100),"bounded command burst");
        Check(!inbox.Allow(4,100.5)&&inbox.Allow(4,101),"rate limit resets after one second");
        inbox.Prune(new[]{5});Check(inbox.First(4,token),"disconnected actor inbox pruned");
        var replica=new AmenityReplica();
        Check(replica.Accept(message,out _,out _)==AmenityReceipt.Ignore,"state ignored before session handshake");
        var sync=new AmenityMessage{Epoch=message.Epoch,Sequence=2,HostTime=101,SyncToken=Guid.NewGuid().ToString()};
        Check(replica.Accept(sync,out _,out _)==AmenityReceipt.Ignore,"unsolicited handshake rejected");
        replica.SentSync(sync.SyncToken,100);Check(replica.Accept(sync,out var sent,out var changed)==AmenityReceipt.Sync&&changed&&sent==100,"nonce establishes current load epoch and clock sample");
        Check(replica.Accept(message,out _,out _)==AmenityReceipt.State&&replica.KnownIds.Contains(session.Id),"current host snapshot accepted");
        Check(replica.Accept(message,out _,out _)==AmenityReceipt.Ignore,"duplicate snapshot ignored");
        var catalogue=new AmenityMessage{Epoch=message.Epoch,Sequence=10,HostTime=102};
        Check(replica.Accept(catalogue,out _,out _)==AmenityReceipt.State&&replica.KnownIds.Count==0,"removed furniture catalogue reconciles");
        message.Sequence=9;Check(replica.Accept(message,out _,out _)==AmenityReceipt.Ignore,"older table snapshot cannot resurrect removed furniture");
        var next=new AmenityMessage{Epoch=Guid.NewGuid().ToString(),Sequence=1,HostTime=200,SyncToken=Guid.NewGuid().ToString()};
        replica.SentSync(next.SyncToken,199);Check(replica.Accept(next,out _,out changed)==AmenityReceipt.Sync&&changed,"next save accepts reset sequence with fresh nonce");
        message.Sequence=100;Check(replica.Accept(message,out _,out _)==AmenityReceipt.Ignore,"old save epoch ignored even at higher sequence");
        replica.Reset();Check(replica.KnownIds.Count==0&&replica.Epoch.Length==0,"scene cleanup resets replica");
        foreach(var solo in new[]{true,false})
        {
            session=new(){Id=Guid.NewGuid().ToString()};double time=1000;
            session.Execute(4,Command(solo?"solo":"duel"),time);if(!solo)session.Execute(5,Command("join"),time);
            void ThrowAt(int actor,double x,double y)
            {
                time+=1;session.Execute(actor,Command("charge",x,y+.008),time);session.Execute(actor,Command("release"),time+.4);
            }
            for(var i=0;i<3;i++)ThrowAt(4,0,.103);
            if(!solo)for(var i=0;i<3;i++)ThrowAt(5,.2,.2);
            ThrowAt(4,0,.103);ThrowAt(4,Math.Sin(12*Math.PI/10)*.103,Math.Cos(12*Math.PI/10)*.103);ThrowAt(4,0,.166);
            Check(session.Darts.Remaining[0]==0&&session.Darts.Winner==0,solo?"full solo 301 via host charge/release":"full two-seat 301 via host charge/release");
        }
        Reject(()=>session.Execute(-1,Command("restart"),1000),"negative participant rejected");
        for(var trial=0;trial<12;trial++)
        {
            var sim=PoolSimulation.Rack();sim.Strike((trial-6)*.18,.1+trial*.08);var ticks=0;
            while(sim.Moving&&ticks++<7500)sim.Step();
            Check(!sim.Moving&&sim.Balls.All(b=>double.IsFinite(b.X)&&double.IsFinite(b.Z)&&Math.Abs(b.X)<.7&&Math.Abs(b.Z)<1.3),"angled low/high power shots settle and stay bounded");
        }
        Console.WriteLine($"PASS: {count} darts, casual eight-ball, fixed simulation, participant/turn and snapshot contract assertions.");
    }
}
