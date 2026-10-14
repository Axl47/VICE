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

namespace PersonalMods.LSDGameplay;

public sealed class StudioCommand
{
    public string Token { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = "sync";
    public string Machine { get; set; } = "";
    public int ArtSlot { get; set; }
    public float Accuracy { get; set; }
    public string Name { get; set; } = "";
    public byte[]? Pixels { get; set; }
    public bool Repeated { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class StudioReply
{
    public string Token { get; set; } = "";
    public string State { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Success { get; set; }
    public List<SlotChange> Inventory { get; set; } = new();
}

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class StudioPacket : Il2CppSystem.Object
{
    public StudioPacket(IntPtr pointer) : base(pointer) { }
    public StudioPacket() : base(ClassInjector.DerivedConstructorPointer<StudioPacket>())
        => ClassInjector.DerivedConstructorBody(this);
}

internal static class WorkshopNetwork
{
    private static readonly SynchronousPacketPayload Wire=new(WorkshopRules.MaximumPayload+8192);
    private static readonly InitialStateSync InitialSync=new();
    private static readonly Dictionary<string,StudioReply> Completed=new();
    private static readonly HashSet<string> Applied=new();
    private static readonly Dictionary<int,float> LastRequest=new();
    private static Il2CppSystem.Action<NetworkConnection,StudioPacket>? HostHandler;
    private static Il2CppSystem.Action<StudioPacket>? ClientHandler;
    private static Il2CppSystem.Action<Writer,StudioPacket>? WriterHandler;
    private static Il2CppSystem.Func<Reader,StudioPacket>? ReaderHandler;
    private static IntPtr ManagerPointer;
    internal static bool Host=>InstanceFinder.NetworkManager && InstanceFinder.NetworkManager.ServerManager.Started;

    internal static void Bind()
    {
        var manager=InstanceFinder.NetworkManager;
        if (!manager || manager.Pointer==ManagerPointer) return;
        WriterHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer,StudioPacket>>((Action<Writer,StudioPacket>)Write);
        ReaderHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader,StudioPacket>>((Func<Reader,StudioPacket>)Read);
        HostHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NetworkConnection,StudioPacket>>((Action<NetworkConnection,StudioPacket>)ReceiveRequest);
        ClientHandler??=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<StudioPacket>>((Action<StudioPacket>)ReceiveReply);
        GenericWriter<StudioPacket>.Write=WriterHandler; GenericReader<StudioPacket>.Read=ReaderHandler;
        manager.ServerManager.RegisterBroadcast<StudioPacket>(HostHandler,true);
        manager.ClientManager.RegisterBroadcast<StudioPacket>(ClientHandler);
        ManagerPointer=manager.Pointer;
    }

    internal static void Reset() { Completed.Clear();Applied.Clear();LastRequest.Clear();Wire.Reset();InitialSync.Reset(); }
    internal static void Tick()
    {
        if (!Host && Player.Local && Player.Local.PlayerLoaded && InitialSync.Take(UnityEngine.Time.realtimeSinceStartup))
            Submit(new StudioCommand());
    }
    private static void Write(Writer writer,StudioPacket packet)=>Wire.Write(writer.WriteString);
    private static StudioPacket Read(Reader reader)
    {
        try { Wire.Read(reader.ReadString); }
        catch (Exception error) { MelonLogger.Warning("Rejected studio packet: "+error.GetType().Name); }
        return new StudioPacket();
    }

    internal static void Submit(StudioCommand command)
    {
        Bind();
        if (Host) { Accept(Execute(Player.Local,command)); return; }
        Wire.Send(JsonSerializer.Serialize(command),()=>InstanceFinder.NetworkManager.ClientManager.Broadcast(new StudioPacket(),Channel.Reliable));
    }

    private static StudioReply Execute(Player player,StudioCommand command)
    {
        if (!Guid.TryParse(command.Token,out _)) throw new InvalidDataException("Invalid action identity.");
        var key=player.PlayerCode+":"+command.Token;
        if (Completed.TryGetValue(key,out var existing)) return existing;
        var reply=WorkshopFeature.Current!.Execute(player,command);
        if (Completed.Count>=256) Completed.Remove(Completed.Keys.First());
        Completed[key]=reply;return reply;
    }

    private static void ReceiveRequest(NetworkConnection connection,StudioPacket packet)
    {
        if (!Wire.TryTake(out var payload)) return;
        if (!Host || !connection.Authenticated || WorkshopFeature.Current?.Ready!=true) return;
        var player=Player.PlayerList.ToArray().FirstOrDefault(p=>p && p.Owner.ClientId==connection.ClientId && p.PlayerLoaded);
        if (player is null || !player) return;
        try
        {
            var now=UnityEngine.Time.realtimeSinceStartup;
            var command=JsonSerializer.Deserialize<StudioCommand>(payload) ?? throw new InvalidDataException("Empty workshop request.");
            if(command.Action!="release"){
                if (LastRequest.TryGetValue(connection.ClientId,out var last) && now-last<0.10f) return;
                LastRequest[connection.ClientId]=now;
            }
            Send(connection,Execute(player,command));
        }
        catch (Exception error) { MelonLogger.Warning("Rejected studio request: "+error.Message); }
    }

    private static void Send(NetworkConnection connection,StudioReply reply)
    {
        Wire.Send(JsonSerializer.Serialize(reply),()=>
        {
            var manager=InstanceFinder.NetworkManager.ServerManager;
            var method=manager.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).Single(m=>m.Name=="Broadcast" && m.IsGenericMethodDefinition
                && m.GetParameters().Length==4 && m.GetParameters()[0].ParameterType==typeof(NetworkConnection));
            method.MakeGenericMethod(typeof(StudioPacket)).Invoke(manager,new object[] { connection,new StudioPacket(),true,Channel.Reliable });
        });
    }

    internal static void Publish()
    {
        if (!Host || WorkshopFeature.Current?.Ready!=true) return;
        var state=WorkshopFeature.Current.State.Serialize();
        foreach (var pair in InstanceFinder.NetworkManager.ServerManager.Clients)
            if (pair.Value.Authenticated && !pair.Value.IsLocalClient)
                Send(pair.Value,new StudioReply { Success=true,State=state });
    }

    private static void ReceiveReply(StudioPacket packet)
    {
        if (!Wire.TryTake(out var payload)) return;
        if (Host || WorkshopFeature.Current?.Ready!=true) return;
        try { Accept(JsonSerializer.Deserialize<StudioReply>(payload) ?? throw new InvalidDataException("Empty workshop reply.")); }
        catch (Exception error) { WorkshopFeature.Current?.ShowMessage(error.Message); }
    }

    private static void Accept(StudioReply reply)
    {
        if (reply.Token.Length>0 && !Applied.Add(reply.Token)) return;
        if (!Host && reply.Inventory.Count>0) InventoryTransaction.ApplyRemote(reply.Inventory);
        if (!Host && reply.Success && reply.State.Length>0)
        {
            WorkshopFeature.Current!.ReceiveState(WorkshopState.Deserialize(reply.State), InitialSync.Awaiting);
            InitialSync.Received();
        }
        WorkshopFeature.Current?.Reply(reply);
    }

    internal static void CheckWire<T>(T value)
    {
        Bind();var payload=JsonSerializer.Serialize(value);
        var writer=WriterPool.GetWriter(InstanceFinder.NetworkManager);
        try
        {
            Wire.Send(payload,()=>writer.Write(new StudioPacket()));
            var reader=ReaderPool.GetReader(writer.GetArraySegment(),InstanceFinder.NetworkManager,(Reader.DataSource)0);
            try
            {
                reader.Read<StudioPacket>();
                if (!Wire.TryTake(out var decoded)||decoded!=payload||reader.Remaining!=0)
                    throw new InvalidDataException("Native studio broadcast serialization changed the payload.");
            }
            finally { reader.Store(); }
        }
        finally { writer.Store(); }
    }
}
