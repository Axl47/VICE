using System.Reflection;
using System.Text.Json;
using System.Text.Encodings.Web;
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

namespace VICE.Lifestyle.Television;

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class TVPacket : Il2CppSystem.Object
{
    public TVPacket(IntPtr pointer) : base(pointer) { }
    public TVPacket() : base(ClassInjector.DerivedConstructorPointer<TVPacket>()) => ClassInjector.DerivedConstructorBody(this);
}

internal static class TVNetwork
{
    private static readonly Dictionary<IntPtr,string> Payloads = new();
    private static readonly Dictionary<string,double> Pings = new();
    private static readonly Dictionary<int,(double Start,int Count)> LastCommands = new();
    private static readonly JsonSerializerOptions WireJson=new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private static readonly Dictionary<int,(ImageTransfer Transfer, TVCommand Command)> Uploads = new();
    private static readonly Dictionary<string,ImageTransfer> Downloads = new();
    private static readonly Dictionary<string,double> AssetRequests = new();
    private static readonly Queue<(NetworkConnection? Connection,string Payload)> Outbox = new();
    private static Il2CppSystem.Action<NetworkConnection,TVPacket>? HostHandler;
    private static Il2CppSystem.Action<TVPacket>? ClientHandler;
    private static Il2CppSystem.Action<Writer,TVPacket>? WriterHandler;
    private static Il2CppSystem.Func<Reader,TVPacket>? ReaderHandler;
    private static IntPtr Bound;
    private static double NextSync;
    internal static readonly PlaybackClock Clock = new();
    internal static bool Host => InstanceFinder.NetworkManager && InstanceFinder.NetworkManager.ServerManager.Started;
    internal static double Now => Host ? PlaybackClock.LocalNow : Clock.Now;
    internal static int PeerCount => Host ? InstanceFinder.NetworkManager.ServerManager.Clients.Count : 2;
    internal static int QueuedMessages => Outbox.Count;

    internal static void Bind()
    {
        var manager = InstanceFinder.NetworkManager;
        if (!manager || manager.Pointer == Bound) return;
        WriterHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer,TVPacket>>((Action<Writer,TVPacket>)Write);
        ReaderHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader,TVPacket>>((Func<Reader,TVPacket>)Read);
        HostHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NetworkConnection,TVPacket>>((Action<NetworkConnection,TVPacket>)ReceiveRequest);
        ClientHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TVPacket>>((Action<TVPacket>)ReceiveMessage);
        GenericWriter<TVPacket>.Write = WriterHandler; GenericReader<TVPacket>.Read = ReaderHandler;
        manager.ServerManager.RegisterBroadcast<TVPacket>(HostHandler, true);
        manager.ClientManager.RegisterBroadcast<TVPacket>(ClientHandler);
        Bound = manager.Pointer;
    }
    private static void Write(Writer writer, TVPacket packet) => writer.WriteString(Payloads[packet.Pointer]);
    private static TVPacket Read(Reader reader)
    {
        var payload = reader.ReadString();
        if (payload.Length > MediaRules.MaxWireChars) throw new InvalidDataException("TV packet exceeds its size limit.");
        var packet = new TVPacket(); Payloads[packet.Pointer] = payload; return packet;
    }
    private static string Take(TVPacket packet) { var text = Payloads[packet.Pointer]; Payloads.Remove(packet.Pointer); return text; }
    private static string Encode<T>(T value)
    {
        var text = JsonSerializer.Serialize(value,WireJson);
        if (text.Length > MediaRules.MaxWireChars) throw new InvalidDataException("TV message is too large.");
        return text;
    }
    internal static void Submit(TVCommand command)
    {
        Bind();
        if (Host) { Handle(Player.Local, command, message => TelevisionFeature.Current?.Receive(message)); return; }
        if (command.Action == "sync") Pings[command.Token] = PlaybackClock.LocalNow;
        Outbox.Enqueue((null, Encode(command)));
    }
    internal static void Tick()
    {
        Bind(); var now = PlaybackClock.LocalNow;
        if (!Host && now >= NextSync) { NextSync = now + 5; Submit(new()); }
        for (var i = 0; i < 4 && Outbox.Count > 0; i++)
        {
            var item = Outbox.Dequeue(); var packet = new TVPacket(); Payloads[packet.Pointer] = item.Payload;
            try
            {
                if (Host && item.Connection is { } connection)
                {
                    if (!connection.Authenticated) continue;
                    var manager = InstanceFinder.NetworkManager.ServerManager;
                    var method = manager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(m => m.Name == "Broadcast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(NetworkConnection));
                    method.MakeGenericMethod(typeof(TVPacket)).Invoke(manager, new object[] {connection,packet,true,Channel.Reliable});
                }
                else if (!Host) InstanceFinder.NetworkManager.ClientManager.Broadcast(packet, Channel.Reliable);
            }
            finally { Payloads.Remove(packet.Pointer); }
        }
        foreach (var id in Uploads.Where(p => now - p.Value.Transfer.Started > 90).Select(p => p.Key).ToArray()) Uploads.Remove(id);
        foreach (var hash in Downloads.Where(p => now - p.Value.Started > 90).Select(p => p.Key).ToArray()) Downloads.Remove(hash);
        foreach (var token in Pings.Where(p => now-p.Value > 15).Select(p => p.Key).ToArray()) Pings.Remove(token);
        TVVideoNetwork.Tick();
    }
    private static void ReceiveRequest(NetworkConnection connection, TVPacket packet)
    {
        var payload = Take(packet);
        var mod = TelevisionFeature.Current;
        if (!Host || !connection.Authenticated || mod?.Ready != true) return;
        var player = Player.PlayerList.ToArray().FirstOrDefault(p => p && p.PlayerLoaded && p.Owner.ClientId == connection.ClientId);
        if (!player) return;
        TVCommand? command=null;
        try
        {
            command = JsonSerializer.Deserialize<TVCommand>(payload);
            if (command is null) return;
            if (!Guid.TryParse(command.Token,out _)) throw new InvalidDataException("Invalid request identity.");
            var now = PlaybackClock.LocalNow;
            if (command.Action is not ("imageChunk" or "videoChunk"))
            {
                var window=LastCommands.GetValueOrDefault(connection.ClientId);
                if(now-window.Start>=1)window=(now,0);
                if(window.Count>=20)throw new InvalidOperationException("Please wait a moment between controls.");
                LastCommands[connection.ClientId]=(window.Start,window.Count+1);
            }
            if (TVVideoNetwork.ReceiveRequest(connection, command)) return;
            if (command.Action == "imageNeed") { SendImage(connection, command.Hash); return; }
            if (command.Action == "imageBegin")
            {
                mod.RequireControl(player!, command.Screen);
                if (command.Text.Length > 160) throw new InvalidDataException("Image name is too long.");
                if (mod.Cache.Has(command.Hash)) { command.Media = new() {Kind="image",Source=command.Hash,Title=command.Text}; command.Action="select"; Handle(player!,command,m => Send(connection,m)); return; }
                if (Uploads.ContainsKey(connection.ClientId)) throw new InvalidOperationException("An image upload is already in progress.");
                Uploads[connection.ClientId] = (new ImageTransfer(command.Hash,command.Length,now),command); return;
            }
            if (command.Action == "imageChunk")
            {
                if (!Uploads.TryGetValue(connection.ClientId,out var upload) || upload.Transfer.Hash != command.Hash) return;
                var bytes = upload.Transfer.Add(command.Index, command.Data ?? Array.Empty<byte>(), now);
                if (bytes is null) return;
                Uploads.Remove(connection.ClientId); mod.RequireControl(player!, upload.Command.Screen);
                mod.Cache.Put(bytes, command.Hash);
                upload.Command.Action="select"; upload.Command.Media=new() {Kind="image",Source=command.Hash,Title=upload.Command.Text};
                Handle(player!,upload.Command,m => Send(connection,m)); return;
            }
            Handle(player!,command,m => Send(connection,m),connection);
        }
        catch (Exception error) { Send(connection,new() {Token=command?.Token??"",Success=false,Text=SafeError(error)}); }
    }
    private static void Handle(Player player, TVCommand command, Action<TVMessage> reply, NetworkConnection? connection = null)
    {
        try
        {
            var mod = TelevisionFeature.Current!;
            if (command.Action == "sync") { reply(new() {Token=command.Token,HostTime=Now}); if(connection is not null) SendState(connection); return; }
            mod.RequireControl(player, command.Screen);
            if (command.Action == "relayQuery") { mod.QueryRelay(command,reply); return; }
            var videoSelection = command.Action == "videoSelect";
            if (videoSelection)
            {
                if (command.Media?.Kind != "video") throw new InvalidDataException("Invalid shared video selection.");
                command.Media.Validate(); command.Action = "select";
            }
            mod.Execute(command);
            if (videoSelection) TVVideoNetwork.RegisterOwner(command.Media!, connection);
            reply(new() {Token=command.Token,HostTime=Now}); Publish();
        }
        catch (Exception error) { reply(new() {Token=command.Token,Success=false,Text=SafeError(error)}); }
    }
    internal static string SafeError(Exception error) => error is InvalidDataException or InvalidOperationException ? error.Message : "TV action failed. Check the mod setup or try again.";
    internal static void Send(NetworkConnection connection, TVMessage message)
    { if (!connection.Authenticated) return; message.HostTime=Now; Outbox.Enqueue((connection,Encode(message))); }
    internal static void Publish()
    {
        if (!Host || TelevisionFeature.Current?.Ready != true) return;
        foreach (var pair in InstanceFinder.NetworkManager.ServerManager.Clients)
            if (pair.Value.Authenticated && !pair.Value.IsLocalClient) SendState(pair.Value);
    }
    private static void SendState(NetworkConnection connection)
    {
        var document=TelevisionFeature.Current!.Document;
        var batches=document.Screens.Chunk(4).ToArray();
        if (batches.Length==0) batches=new[] {Array.Empty<ScreenState>()};
        foreach (var batch in batches) Send(connection,new() {Type="state",Screens=batch.Select(s=>s.Clone()).ToList(),AllIds=document.Screens.Select(s=>s.Id).ToList()});
        var favorites=document.Favorites.Chunk(4).ToArray();
        if(favorites.Length==0)favorites=new[]{Array.Empty<MediaSelection>()};
        for(var index=0;index<favorites.Length;index++)Send(connection,new(){Type="favorites",Index=index*4,Favorites=favorites[index].Select(f=>f.Clone()).ToList()});
    }
    private static void ReceiveMessage(TVPacket packet)
    {
        var payload=Take(packet); if (Host || TelevisionFeature.Current?.Ready!=true) return;
        try
        {
            var message=JsonSerializer.Deserialize<TVMessage>(payload) ?? throw new InvalidDataException("Empty TV message.");
            if (Pings.Remove(message.Token,out var sent)) Clock.Observe(message.HostTime,sent,PlaybackClock.LocalNow);
            else Clock.Seed(message.HostTime);
            var mod=TelevisionFeature.Current!;
            if (TVVideoNetwork.ReceiveMessage(message)) return;
            if(message.Type=="imageBegin") { if(Downloads.Count>=4) throw new InvalidDataException("Too many incoming images."); Downloads[message.Hash]=new(message.Hash,message.Length,PlaybackClock.LocalNow); return; }
            if(message.Type=="imageChunk")
            {
                if(!Downloads.TryGetValue(message.Hash,out var transfer))return;
                var bytes=transfer.Add(message.Index,message.Data??Array.Empty<byte>(),PlaybackClock.LocalNow);
                if(bytes is not null){Downloads.Remove(message.Hash);mod.Cache.Put(bytes,message.Hash);AssetRequests.Remove(message.Hash);}
                return;
            }
            mod.Receive(message);
        }
        catch(Exception error){TelevisionFeature.Current?.Notice(SafeError(error));}
    }
    internal static void UploadImage(string screen,string title,byte[] bytes)
    {
        var mod=TelevisionFeature.Current!; var hash=mod.Cache.Put(bytes);
        if(Host){Submit(new(){Action="select",Screen=screen,Media=new(){Kind="image",Source=hash,Title=title}});return;}
        Submit(new(){Action="imageBegin",Screen=screen,Hash=hash,Length=bytes.Length,Text=title});
        for(var index=0;index*MediaRules.ChunkBytes<bytes.Length;index++)
            Submit(new(){Action="imageChunk",Hash=hash,Index=index,Data=bytes.Skip(index*MediaRules.ChunkBytes).Take(MediaRules.ChunkBytes).ToArray()});
    }
    internal static void NeedImage(string hash)
    {
        if(Host || AssetRequests.TryGetValue(hash,out var last)&&PlaybackClock.LocalNow-last<20)return;
        AssetRequests[hash]=PlaybackClock.LocalNow;Submit(new(){Action="imageNeed",Hash=hash});
    }
    private static void SendImage(NetworkConnection connection,string hash)
    {
        var mod=TelevisionFeature.Current!;
        if(!mod.Document.Screens.Any(s=>s.Media.Kind=="image"&&s.Media.Source==hash)||!mod.Cache.Has(hash))return;
        var key=connection.ClientId+":"+hash;
        if(AssetRequests.TryGetValue(key,out var last)&&PlaybackClock.LocalNow-last<10)return;
        AssetRequests[key]=PlaybackClock.LocalNow;
        var bytes=mod.Cache.Read(hash);Send(connection,new(){Type="imageBegin",Hash=hash,Length=bytes.Length});
        for(var index=0;index*MediaRules.ChunkBytes<bytes.Length;index++)
            Send(connection,new(){Type="imageChunk",Hash=hash,Index=index,Data=bytes.Skip(index*MediaRules.ChunkBytes).Take(MediaRules.ChunkBytes).ToArray()});
    }
    internal static void Reset()
    {TVVideoNetwork.Reset();Outbox.Clear();Uploads.Clear();Downloads.Clear();AssetRequests.Clear();Pings.Clear();LastCommands.Clear();Payloads.Clear();Clock.Reset();NextSync=0;}
    internal static void CheckWire<T>(T value)
    {
        Bind();var payload=Encode(value);var packet=new TVPacket();Payloads[packet.Pointer]=payload;
        var writer=WriterPool.GetWriter(InstanceFinder.NetworkManager);
        try
        {
            GenericWriter<TVPacket>.Write.Invoke(writer,packet);
            var reader=ReaderPool.GetReader(writer.GetArraySegment(),InstanceFinder.NetworkManager,(Reader.DataSource)0);
            try{var decoded=GenericReader<TVPacket>.Read.Invoke(reader);if(Take(decoded)!=payload)throw new InvalidDataException("Native TV broadcast serialization changed the payload.");}
            finally{reader.Store();}
        }
        finally{Payloads.Remove(packet.Pointer);writer.Store();}
    }
}
