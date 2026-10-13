using System.Text.Json.Serialization;

namespace VICE.Lifestyle.Amenities;

internal sealed class AmenityCommand
{
    public int Version { get; set; } = 1;
    public string Token { get; set; } = Guid.NewGuid().ToString();
    public string Table { get; set; } = "";
    public string Instance { get; set; } = "";
    public long Revision { get; set; }
    public string Action { get; set; } = "sync";
    public double X { get; set; }
    public double Y { get; set; }
    internal void Validate()
    {
        if(Version!=1||!Guid.TryParse(Token,out _)||Action.Length>16||Table.Length>64||Instance.Length>64
            ||!double.IsFinite(X)||!double.IsFinite(Y))throw new InvalidDataException("Invalid home-game request.");
        if(Action!="sync"&&(!Guid.TryParse(Table,out _)||!Guid.TryParse(Instance,out _)))throw new InvalidDataException("Unknown home game.");
    }
}

internal sealed class AmenityMessage
{
    public int Version { get; set; } = 1;
    public string Epoch { get; set; } = Guid.NewGuid().ToString();
    public string SyncToken { get; set; } = "";
    public long Sequence { get; set; }
    public double HostTime { get; set; }
    public string Notice { get; set; } = "";
    public List<string> AllIds { get; set; } = new();
    public AmenitySession? State { get; set; }
}

internal sealed class AmenitySession
{
    public string Id { get; set; } = "";
    public string Instance { get; set; } = Guid.NewGuid().ToString();
    public string Kind { get; set; } = "darts";
    public long Revision { get; set; }
    public string Mode { get; set; } = "idle";
    public int[] Seats { get; set; } = new[] {-1,-1};
    public DartsMatch Darts { get; set; } = new();
    public PoolMatch Pool { get; set; } = new();
    public int Charging { get; set; } = -1;
    public double ChargeStarted { get; set; }
    public double AimX { get; set; }
    public double AimY { get; set; }
    public string Notice { get; set; } = "Choose solo practice or invite a second player.";
    [JsonIgnore] internal Dictionary<int,double> Heartbeats { get; } = new();
    [JsonIgnore] internal bool Solo=>Mode=="solo";
    [JsonIgnore] internal bool Playable=>Mode=="solo"||Mode=="duel"&&Seats[1]>=0;
    [JsonIgnore] internal int Turn=>Kind=="darts"?Darts.Turn:Pool.Turn;
    [JsonIgnore] internal bool Finished=>Kind=="darts"?Darts.Winner>=0:Pool.Winner>=0;
    [JsonIgnore] internal bool Moving=>Kind=="pool"&&Pool.Simulation.Moving;
    [JsonIgnore] internal string Feedback=>Mode=="idle"?Notice:!Playable?"Waiting for player 2. They can interact and Join.":Kind=="darts"?Darts.Feedback:Pool.Feedback;

    internal void Execute(int actor,AmenityCommand command,double now)
    {
        command.Validate();
        if(actor<0||!double.IsFinite(now))throw new InvalidDataException("Invalid participant or host clock.");
        if(command.Table!=Id||command.Instance!=Instance)throw new InvalidOperationException("Table changed. Reopen its controls.");
        if(command.Action=="heartbeat")
        {if(Seats.Contains(actor)&&actor>=0)Heartbeats[actor]=now;return;}
        // Leave must work even when the client's snapshot is stale.
        if(command.Action=="leave")
        {if(Seats.Contains(actor)&&actor>=0)Reset("Player left — table released.");return;}
        if(command.Revision!=Revision)throw new InvalidOperationException("Turn changed. Wait for the host's latest state.");
        switch(command.Action)
        {
            case "solo":case "duel":
                if(Mode!="idle")throw new InvalidOperationException("This table is busy. Spectators can watch.");
                Mode=command.Action;Seats[0]=actor;Heartbeats[actor]=now;ResetGames();Revision++;break;
            case "join":
                if(Mode!="duel"||Seats[1]>=0||Seats[0]==actor)throw new InvalidOperationException("No second-player seat is available.");
                Seats[1]=actor;Heartbeats[actor]=now;Revision++;break;
            case "restart":
                if(Seats[0]!=actor||Mode=="idle")throw new InvalidOperationException("Player 1 can restart this table.");
                ResetGames();Revision++;break;
            case "charge":
                RequireTurn(actor);if(Charging>=0)throw new InvalidOperationException("A shot is already charging.");
                if(Kind=="darts"&&(Math.Abs(command.X)>.25||Math.Abs(command.Y)>.25))throw new InvalidDataException("Aim outside panel.");
                if(Kind=="pool"&&Pool.BallInHand)throw new InvalidOperationException("Place the cue ball first.");
                Charging=actor;ChargeStarted=now;AimX=command.X;AimY=command.Y;break;
            case "release":
                RequireTurn(actor);if(Charging!=actor)throw new InvalidOperationException("Hold to charge before releasing.");
                var duration=now-ChargeStarted;Charging=-1;
                if(Kind=="darts")
                {var hit=DartsRules.Throw(AimX,AimY,duration);Darts.Hit(hit.X,hit.Y,Solo);}
                else{if(duration<.1||duration>3)throw new InvalidOperationException("Hold between 0.1 and 3 seconds.");Pool.Strike(AimX,Math.Clamp(duration/1.5,.05,1));}
                Revision++;break;
            case "place":
                RequireTurn(actor);if(Kind!="pool")throw new InvalidOperationException("Only pool has ball in hand.");
                Pool.Place(command.X,command.Y);Revision++;break;
            default:throw new InvalidDataException("Unknown home-game action.");
        }
        Heartbeats[actor]=now;
    }
    internal void RequireTurn(int actor)
    {
        if(!Playable||Finished||Moving||actor<0||Seats[Turn]!=actor)throw new InvalidOperationException("Wait for your turn at a ready table.");
    }
    internal bool Prune(Func<int,bool> present,double now)
    {
        if(Mode!="idle"&&Seats.Where(a=>a>=0).Any(a=>!present(a)||now-Heartbeats.GetValueOrDefault(a)>10))
        {Reset("Participant disconnected, moved away or closed controls — table released.");return true;}
        if(Charging>=0&&now-ChargeStarted>3){Charging=-1;Notice="Charge expired. Hold and release within 3 seconds.";return true;}
        return false;
    }
    internal void Reset(string notice){Mode="idle";Seats=new[]{-1,-1};Heartbeats.Clear();ResetGames();Notice=notice;Revision++;}
    private void ResetGames(){Charging=-1;Darts=new();Pool=new();}
}
