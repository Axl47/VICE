namespace VICE.Lifestyle.Amenities;

internal sealed class AmenityInbox
{
    private readonly Dictionary<int,(double Time,int Count)> Limits=new();
    private readonly Dictionary<int,Queue<string>> Tokens=new();
    internal bool Allow(int actor,double now)
    {
        var window=Limits.GetValueOrDefault(actor);if(now-window.Time>=1)window=(now,0);
        if(window.Count>=20)return false;Limits[actor]=(window.Time,window.Count+1);return true;
    }
    internal bool First(int actor,string token)
    {
        if(!Tokens.TryGetValue(actor,out var tokens))Tokens[actor]=tokens=new();
        if(tokens.Contains(token))return false;tokens.Enqueue(token);while(tokens.Count>128)tokens.Dequeue();return true;
    }
    internal void Prune(IEnumerable<int> connected)
    {var ids=connected.ToHashSet();foreach(var actor in Tokens.Keys.Concat(Limits.Keys).Distinct().Except(ids).ToArray()){Tokens.Remove(actor);Limits.Remove(actor);}}
    internal void Clear(){Tokens.Clear();Limits.Clear();}
}

internal enum AmenityReceipt { Ignore, Sync, Notice, State }

internal sealed class AmenityReplica
{
    private readonly Dictionary<string,double> Pings=new();
    private readonly Dictionary<string,long> Received=new();
    internal string Epoch { get; private set; } = "";
    internal HashSet<string> KnownIds { get; private set; } = new();
    internal void SentSync(string token,double now){Pings[token]=now;foreach(var old in Pings.Where(p=>now-p.Value>10).Select(p=>p.Key).ToArray())Pings.Remove(old);}
    internal AmenityReceipt Accept(AmenityMessage message,out double sentAt,out bool changedEpoch)
    {
        AmenityProtocol.Validate(message);sentAt=0;changedEpoch=false;
        if(message.SyncToken.Length>0)
        {
            if(!Pings.Remove(message.SyncToken,out sentAt))return AmenityReceipt.Ignore;
            if(Epoch!=message.Epoch){Epoch=message.Epoch;Received.Clear();KnownIds.Clear();changedEpoch=true;}
            return AmenityReceipt.Sync;
        }
        if(message.Epoch!=Epoch)return AmenityReceipt.Ignore;
        var key=message.State?.Id??(message.Notice.Length>0?"notice":"catalogue");
        if(message.Sequence<=Received.GetValueOrDefault(key))return AmenityReceipt.Ignore;Received[key]=message.Sequence;
        if(message.Notice.Length>0)return AmenityReceipt.Notice;
        if(message.Sequence>Received.GetValueOrDefault("ids")){Received["ids"]=message.Sequence;KnownIds=message.AllIds.ToHashSet();}
        if(message.State is {} state&&!KnownIds.Contains(state.Id))return AmenityReceipt.Ignore;
        return AmenityReceipt.State;
    }
    internal void Reset(){Pings.Clear();Received.Clear();KnownIds.Clear();Epoch="";}
}
