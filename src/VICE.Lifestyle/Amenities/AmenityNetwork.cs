using System.Reflection;
using System.Text.Json;
using Il2CppFishNet;
using Il2CppFishNet.Broadcast;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using PersonalMods.Networking;
using VICE.Lifestyle.Television;

namespace VICE.Lifestyle.Amenities;

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class AmenityPacket : Il2CppSystem.Object
{
    public AmenityPacket(IntPtr pointer):base(pointer){}
    public AmenityPacket():base(ClassInjector.DerivedConstructorPointer<AmenityPacket>())=>ClassInjector.DerivedConstructorBody(this);
}

internal static class AmenityNetwork
{
    private const int MaxChars=24576;
    private static readonly SynchronousPacketPayload Wire=new(MaxChars);
    private static readonly ReliableOutbox<(NetworkConnection? Connection,string Payload,string Key)> Outbox=new();
    private static readonly AmenityInbox Inbox=new();
    private static readonly AmenityReplica Replica=new();
    private static Il2CppSystem.Action<NetworkConnection,AmenityPacket>? ServerHandler;
    private static Il2CppSystem.Action<AmenityPacket>? ClientHandler;
    private static Il2CppSystem.Action<Writer,AmenityPacket>? WriterHandler;
    private static Il2CppSystem.Func<Reader,AmenityPacket>? ReaderHandler;
    private static Il2CppFishNet.Managing.NetworkManager? BoundManager;
    private static MethodInfo? TargetedBroadcast;
    private static readonly PlaybackClock Clock=new();
    private static double NextSync;
    private static long Sequence;
    private static string Epoch=Guid.NewGuid().ToString();
    internal static bool Host=>InstanceFinder.NetworkManager&&InstanceFinder.NetworkManager.ServerManager.Started;
    internal static double Now=>Host?PlaybackClock.LocalNow:Clock.Now;
    internal static int LocalActor=>Player.Local&&Player.Local.Owner is {} owner?owner.ClientId:-1;

    internal static void Bind()
    {
        var manager=InstanceFinder.NetworkManager;if(!manager)return;
        if(BoundManager&&BoundManager!.Pointer==manager.Pointer)return;
        Unbind();
        WriterHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer,AmenityPacket>>((Action<Writer,AmenityPacket>)((w,p)=>Wire.Write(w.WriteString)));
        ReaderHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader,AmenityPacket>>((Func<Reader,AmenityPacket>)(r=>
        {try{Wire.Read(r.ReadString);}catch(Exception e){MelonLogger.Warning("Home games rejected packet: "+e.GetType().Name);}return new();}));
        ServerHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NetworkConnection,AmenityPacket>>((Action<NetworkConnection,AmenityPacket>)ReceiveRequest);
        ClientHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<AmenityPacket>>((Action<AmenityPacket>)ReceiveState);
        GenericWriter<AmenityPacket>.Write=WriterHandler;GenericReader<AmenityPacket>.Read=ReaderHandler;
        manager.ServerManager.RegisterBroadcast<AmenityPacket>(ServerHandler,true);manager.ClientManager.RegisterBroadcast<AmenityPacket>(ClientHandler);
        TargetedBroadcast=manager.ServerManager.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance)
            .Single(m=>m.Name=="Broadcast"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==4&&m.GetParameters()[0].ParameterType==typeof(NetworkConnection)).MakeGenericMethod(typeof(AmenityPacket));
        BoundManager=manager;
    }
    private static string Encode<T>(T value)
    {var text=JsonSerializer.Serialize(value);if(text.Length>MaxChars)throw new InvalidDataException("Home-game packet too large.");return text;}
    internal static void Submit(AmenityCommand command)
    {
        Bind();command.Validate();
        if(Host){Handle(Player.Local,command,null);return;}
        if(Outbox.Count>=32)throw new InvalidOperationException("Wait for home-game connection to recover.");
        if(command.Action=="sync")Replica.SentSync(command.Token,PlaybackClock.LocalNow);
        Outbox.Enqueue((null,Encode(command),""));
    }
    private static void ReceiveRequest(NetworkConnection connection,AmenityPacket packet)
    {
        if(!Wire.TryTake(out var text)||!Host||!connection.Authenticated||AmenitiesFeature.Current?.Ready!=true)return;
        try
        {
            var player=Player.PlayerList.ToArray().FirstOrDefault(p=>p&&p.PlayerLoaded&&p.Owner.ClientId==connection.ClientId);if(!player)return;
            if(!Inbox.Allow(connection.ClientId,Now))return;
            Handle(player!,JsonSerializer.Deserialize<AmenityCommand>(text)??throw new InvalidDataException("Empty request."),connection);
        }
        catch(Exception e){SendNotice(connection,Safe(e));}
    }
    private static void Handle(Player player,AmenityCommand command,NetworkConnection? connection)
    {
        var feature=AmenitiesFeature.Current;if(feature?.Ready!=true||!player)return;
        try
        {
            command.Validate();var actor=player.Owner.ClientId;
            if(command.Action=="sync")
            {
                if(connection is not null)
                {Queue(connection,new(){SyncToken=command.Token,Sequence=++Sequence,HostTime=Now},"");Publish(connection);}
                return;
            }
            if(!Inbox.First(actor,command.Token)){if(connection is not null)Publish(connection);return;}
            feature.Execute(player,command);if(command.Action!="heartbeat")Publish();
        }
        catch(Exception e)
        {
            if(connection is not null){SendNotice(connection,Safe(e));Publish(connection);}
            else feature.Panel?.Notice(Safe(e));
        }
    }
    internal static string Safe(Exception error)=>error is InvalidDataException or InvalidOperationException?error.Message:"Home-game action failed. Check the mod log.";
    private static void SendNotice(NetworkConnection connection,string notice)=>Queue(connection,new(){Notice=notice,Sequence=++Sequence,HostTime=Now},"");
    private static void Queue(NetworkConnection connection,AmenityMessage message,string key)
    {
        if(!connection.Authenticated)return;
        message.Epoch=Epoch;
        // Supersede unsent snapshots; reliable command order is retained and memory remains bounded.
        if(key.Length>0)Outbox.RemoveWhere(e=>e.Connection?.ClientId==connection.ClientId&&e.Key==key);
        if(Outbox.Count<512)Outbox.Enqueue((connection,Encode(message),key));
    }
    internal static void Publish(NetworkConnection? only=null,bool activeOnly=false)
    {
        var feature=AmenitiesFeature.Current;if(!Host||feature?.Ready!=true)return;
        var peers=new List<NetworkConnection>();
        if(only is not null)peers.Add(only);
        else foreach(var pair in InstanceFinder.NetworkManager.ServerManager.Clients)peers.Add(pair.Value);
        var ids=feature.States.Keys.ToList();
        foreach(var peer in peers.Where(p=>p.Authenticated&&!p.IsLocalClient))
        {
            // Catalogue sent even with no tables, so removed furniture is reconciled on guests.
            if(!activeOnly)Queue(peer,new(){HostTime=Now,Sequence=++Sequence,AllIds=ids},"catalogue");
            foreach(var state in feature.States.Values.Where(s=>!activeOnly||s.Moving||s.Charging>=0))
                Queue(peer,new(){HostTime=Now,Sequence=++Sequence,AllIds=ids,State=state},state.Id);
        }
    }
    private static void ReceiveState(AmenityPacket packet)
    {
        if(!Wire.TryTake(out var text)||Host||AmenitiesFeature.Current?.Ready!=true)return;
        try
        {
            var message=JsonSerializer.Deserialize<AmenityMessage>(text)??throw new InvalidDataException("Empty state.");
            var receipt=Replica.Accept(message,out var sent,out var changedEpoch);
            if(receipt==AmenityReceipt.Ignore)return;
            if(receipt==AmenityReceipt.Sync)
            {
                // An outstanding nonce authenticates the current load epoch; old queued saves cannot seed state.
                if(changedEpoch){AmenitiesFeature.Current!.States.Clear();Clock.Reset();}
                Clock.Observe(message.HostTime,sent,PlaybackClock.LocalNow);return;
            }
            Clock.Seed(message.HostTime);var feature=AmenitiesFeature.Current!;
            if(receipt==AmenityReceipt.Notice){feature.Panel?.Notice(message.Notice);return;}
            // Catalogue ordering is independent of per-table snapshots.
            foreach(var id in feature.States.Keys.Except(Replica.KnownIds).ToArray())feature.States.Remove(id);
            if(message.State is {} state)feature.States[state.Id]=state;
        }
        catch(Exception e){ModSupport.WarnOnce("amenities:packet:"+e.GetType().Name,"Home-game state rejected: "+e.Message);}
    }
    internal static void Tick()
    {
        Bind();if(!Host&&PlaybackClock.LocalNow>=NextSync){NextSync=PlaybackClock.LocalNow+2;Submit(new());}
        if(Host){var actors=new List<int>();foreach(var pair in InstanceFinder.NetworkManager.ServerManager.Clients)actors.Add(pair.Key);Inbox.Prune(actors);}
        for(var i=0;i<16&&Outbox.Count>0;i++)
            if(!Outbox.SendNext(PlaybackClock.LocalNow,e=>
            {
                if(!BoundManager)throw new InvalidOperationException("Network unavailable.");
                if(Host&&e.Connection?.Authenticated!=true)return;
                Wire.Send(e.Payload,()=>
                {
                    if(Host)TargetedBroadcast!.Invoke(BoundManager!.ServerManager,new object[]{e.Connection!,new AmenityPacket(),true,Channel.Reliable});
                    else BoundManager!.ClientManager.Broadcast(new AmenityPacket(),Channel.Reliable);
                });
            },(e,error)=>{AmenitiesFeature.Current?.Panel?.Notice("Home-game connection failed; reopen controls.");ModSupport.WarnOnce("amenities:send",error.Message);}))break;
    }
    private static void Unbind()
    {
        if(BoundManager)
        {
            try{BoundManager!.ServerManager.UnregisterBroadcast<AmenityPacket>(ServerHandler);BoundManager.ClientManager.UnregisterBroadcast<AmenityPacket>(ClientHandler);}
            catch(Exception e){ModSupport.WarnOnce("amenities:unbind",e.Message);}
        }
        BoundManager=null;TargetedBroadcast=null;
    }
    internal static void Reset(){Unbind();Wire.Reset();Outbox.Clear();Inbox.Clear();Replica.Reset();Sequence=0;NextSync=0;Clock.Reset();Epoch=Guid.NewGuid().ToString();}
}
