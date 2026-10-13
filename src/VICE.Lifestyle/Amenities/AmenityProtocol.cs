namespace VICE.Lifestyle.Amenities;

// Structural limits prevent broken/mismatched peers from poisoning presentation.
internal static class AmenityProtocol
{
    internal static void Validate(AmenityMessage message)
    {
        if(message.Version!=1||!Guid.TryParse(message.Epoch,out _)||message.SyncToken.Length>64||message.Sequence<=0||!double.IsFinite(message.HostTime)||message.AllIds.Count>32||message.Notice.Length>300
            ||message.AllIds.Distinct().Count()!=message.AllIds.Count||message.AllIds.Any(id=>!Guid.TryParse(id,out _)))throw new InvalidDataException("Invalid home-game envelope.");
        if(message.State is not {} s)return;
        if(!Guid.TryParse(s.Id,out _)||!message.AllIds.Contains(s.Id)||!Guid.TryParse(s.Instance,out _)||s.Kind is not ("darts" or "pool")
            ||s.Mode is not ("idle" or "solo" or "duel")||s.Revision<0||s.Seats.Length!=2||s.Seats.Any(a=>a< -1)||s.Seats[0]>=0&&s.Seats[0]==s.Seats[1]
            ||s.Mode=="idle"&&s.Seats.Any(a=>a>=0)||s.Mode!="idle"&&s.Seats[0]<0||s.Mode=="solo"&&s.Seats[1]>=0
            ||s.Charging>=0&&!s.Seats.Contains(s.Charging)||!double.IsFinite(s.ChargeStarted)||!double.IsFinite(s.AimX)||!double.IsFinite(s.AimY)||s.Notice.Length>300)
            throw new InvalidDataException("Invalid home-game state.");
        var d=s.Darts;
        if(d.Remaining.Length!=2||d.Remaining.Any(n=>n is <0 or >301)||d.Turn is <0 or >1||d.Darts is <0 or >3||d.Winner is < -1 or >1
            ||d.TurnStart is <2 or >301||d.Throws<0||!double.IsFinite(d.LastX)||!double.IsFinite(d.LastY)||Math.Abs(d.LastX)>.27||Math.Abs(d.LastY)>.27||d.Feedback.Length>300)
            throw new InvalidDataException("Invalid darts state.");
        var p=s.Pool;var sim=p.Simulation;
        if(p.Turn is <0 or >1||p.Winner is < -1 or >1||p.Groups.Length!=2||p.Groups.Any(g=>g is <0 or >2)||p.Feedback.Length>300
            ||sim.Balls.Count!=16||sim.Balls.Select(b=>b.Number).Distinct().Count()!=16||sim.Balls.Any(b=>b.Number is <0 or >15||!double.IsFinite(b.X)||!double.IsFinite(b.Z)
                ||!double.IsFinite(b.VX)||!double.IsFinite(b.VZ)||Math.Abs(b.X)>.7||Math.Abs(b.Z)>1.3||Math.Abs(b.VX)>5||Math.Abs(b.VZ)>5)
            ||!double.IsFinite(sim.ShotTime)||sim.ShotTime is <0 or >31||sim.Shot.FirstContact is < -1 or >15||sim.Shot.Pockets.Count>16
            ||sim.Shot.Pockets.Any(n=>n is <0 or >15)||sim.Shot.Pockets.Distinct().Count()!=sim.Shot.Pockets.Count)throw new InvalidDataException("Invalid pool state.");
    }
}
