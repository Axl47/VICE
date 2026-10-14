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

namespace VICE.Lifestyle.Poker;

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class PokerPacket : Il2CppSystem.Object
{
    public PokerPacket(IntPtr pointer) : base(pointer) { }
    public PokerPacket() : base(ClassInjector.DerivedConstructorPointer<PokerPacket>()) => ClassInjector.DerivedConstructorBody(this);
}
internal static class PokerNetwork
{
    private static readonly SynchronousPacketPayload Wire = new(65536);
    private static readonly ReliableOutbox<(NetworkConnection? Peer, string Text, string Key)> Outbox = new();
    private static Il2CppSystem.Action<NetworkConnection, PokerPacket>? ServerHandler;
    private static Il2CppSystem.Action<PokerPacket>? ClientHandler;
    private static Il2CppSystem.Action<Writer, PokerPacket>? WriterHandler;
    private static Il2CppSystem.Func<Reader, PokerPacket>? ReaderHandler;
    private static Il2CppFishNet.Managing.NetworkManager? Manager;
    private static MethodInfo? Target;
    private static readonly Dictionary<int, Queue<double>> Rate = new();
    private static string HostEpoch = Guid.NewGuid().ToString();
    internal static string Epoch = "";
    private static string Sync = "";
    private static double NextSync;
    private static long Sequence, Received;
    internal static bool Host => InstanceFinder.NetworkManager && InstanceFinder.NetworkManager.ServerManager.Started;
    internal static double Now => PlaybackClock.LocalNow;
    internal static string LocalPlayer => Player.Local ? Player.Local.PlayerCode : "";
    internal static void Bind()
    {
        var manager = InstanceFinder.NetworkManager; if (!manager || Manager && Manager!.Pointer == manager.Pointer) return;
        Unbind();
        WriterHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer, PokerPacket>>((Action<Writer, PokerPacket>)((w, _) => Wire.Write(w.WriteString)));
        ReaderHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader, PokerPacket>>((Func<Reader, PokerPacket>)(r => { try { Wire.Read(r.ReadString); } catch (Exception e) { ModSupport.WarnOnce("poker:read", e.Message); } return new(); }));
        ServerHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NetworkConnection, PokerPacket>>((Action<NetworkConnection, PokerPacket>)ReceiveRequest);
        ClientHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<PokerPacket>>((Action<PokerPacket>)ReceiveState);
        GenericWriter<PokerPacket>.Write = WriterHandler; GenericReader<PokerPacket>.Read = ReaderHandler;
        manager.ServerManager.RegisterBroadcast<PokerPacket>(ServerHandler, true); manager.ClientManager.RegisterBroadcast<PokerPacket>(ClientHandler);
        Target = manager.ServerManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == "Broadcast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(NetworkConnection)).MakeGenericMethod(typeof(PokerPacket));
        Manager = manager; if (Host) Epoch = HostEpoch;
    }
    internal static void Submit(PokerCommand command)
    {
        Bind(); command.Validate();
        if (Host) { Handle(Player.Local, command, null); return; }
        if (Outbox.Count >= 32) throw new InvalidOperationException("Poker connection is recovering.");
        if (command.Action == "sync") Sync = command.Token;
        Outbox.Enqueue((null, Encode(command), ""));
    }
    private static string Encode<T>(T value)
    { var json = JsonSerializer.Serialize(value); if (json.Length > 65536) throw new InvalidDataException("Poker packet too large."); return json; }
    private static void ReceiveRequest(NetworkConnection peer, PokerPacket packet)
    {
        if (!Wire.TryTake(out var text) || !Host || !peer.Authenticated || PokerFeature.Current?.Ready != true) return;
        try
        {
            if (!Rate.TryGetValue(peer.ClientId, out var times)) Rate[peer.ClientId] = times = new();
            while (times.Count > 0 && Now - times.Peek() > 1) times.Dequeue(); if (times.Count >= 24) return; times.Enqueue(Now);
            var player = Player.PlayerList.ToArray().FirstOrDefault(p => p && p.PlayerLoaded && p.Owner.ClientId == peer.ClientId);
            if (player) Handle(player!, JsonSerializer.Deserialize<PokerCommand>(text) ?? throw new InvalidDataException("Empty request."), peer);
        }
        catch (Exception e) { Send(peer, new() { Notice = Safe(e) }); }
    }
    private static void Handle(Player player, PokerCommand command, NetworkConnection? peer)
    {
        if (!player || PokerFeature.Current?.Ready != true) return;
        try
        {
            command.Validate();
            if (command.Action == "sync") { Publish(player, peer, command.Token); return; }
            if (command.Epoch != HostEpoch) throw new InvalidOperationException("This poker request belongs to an earlier load. Reopen controls.");
            PokerFeature.Current.Execute(player, command); PublishAll();
        }
        catch (Exception e)
        {
            if (peer is null) { PokerFeature.Current?.Panel?.RequestFinished(command.Token); PokerFeature.Current?.Panel?.Notice(Safe(e)); }
            else Send(peer, new() { Notice = Safe(e), NoticeToken = command.Token });
        }
    }
    internal static string Safe(Exception e) => e is InvalidOperationException or InvalidDataException ? e.Message : "Poker action stopped. Check the mod log before moving cash.";
    private static void Publish(Player player, NetworkConnection? peer, string sync = "")
    {
        var tables = PokerFeature.Current!.Tables.Values.Select(t => PokerSnapshot.For(t, player.PlayerCode, Now)).ToList();
        if (peer is null) { PokerFeature.Current.SetSnapshots(tables); return; }
        Send(peer, new() { Sync = sync, Tables = tables }, sync.Length == 0 ? "snapshot" : "");
    }
    internal static void PublishAll()
    {
        if (!Host || PokerFeature.Current?.Ready != true) return;
        if (Player.Local && Player.Local.PlayerLoaded) Publish(Player.Local, null);
        foreach (var p in Player.PlayerList.ToArray().Where(p => p && p.PlayerLoaded && p.Owner.Authenticated && !p.Owner.IsLocalClient)) Publish(p, p.Owner);
    }
    internal static void Wallet(Player player, string phase, PokerCommand command, double before = 0, double after = 0)
    {
        var message = new PokerMessage { Epoch = HostEpoch, Wallet = phase, Request = command, Before = before, After = after };
        if (player.Owner.IsLocalClient) PokerWallet.Receive(message); else Send(player.Owner, message);
    }
    private static void Send(NetworkConnection peer, PokerMessage message, string key = "")
    {
        if (!peer.Authenticated) return; message.Epoch = HostEpoch; message.Sequence = ++Sequence;
        if (key.Length > 0) Outbox.RemoveWhere(e => e.Peer?.ClientId == peer.ClientId && e.Key == key);
        if (Outbox.Count >= 512) throw new InvalidOperationException("Poker network queue is full.");
        Outbox.Enqueue((peer, Encode(message), key));
    }
    private static void ReceiveState(PokerPacket packet)
    {
        if (!Wire.TryTake(out var text) || Host || PokerFeature.Current?.Ready != true) return;
        try
        {
            var m = JsonSerializer.Deserialize<PokerMessage>(text) ?? throw new InvalidDataException("Empty snapshot.");
            if (m.Version != 1 || !Guid.TryParse(m.Epoch, out _) || m.Tables.Count > 32 || m.Notice.Length > 600 || !double.IsFinite(m.Before) || !double.IsFinite(m.After)) throw new InvalidDataException("Invalid poker response.");
            if (m.Sync.Length > 0 && m.Sync == Sync) { Epoch = m.Epoch; Received = 0; Sync = ""; }
            if (m.Epoch != Epoch || m.Sequence <= Received) return; Received = m.Sequence;
            if (m.Wallet.Length > 0) { PokerWallet.Receive(m); return; }
            if (m.Notice.Length > 0) { PokerFeature.Current.Panel?.RequestFinished(m.NoticeToken); PokerFeature.Current.Panel?.Notice(m.Notice); } else PokerFeature.Current.SetSnapshots(m.Tables);
        }
        catch (Exception e) { ModSupport.WarnOnce("poker:snapshot", e.Message); }
    }
    internal static void Tick()
    {
        Bind(); if (!Host && Now >= NextSync && !PokerWallet.Locked) { NextSync = Now + 2; Submit(new()); }
        for (var i = 0; i < 16 && Outbox.Count > 0; i++)
            if (!Outbox.SendNext(Now, e =>
            {
                if (!Manager) throw new InvalidOperationException("Poker network unavailable.");
                if (Host && e.Peer?.Authenticated != true) return;
                Wire.Send(e.Text, () => { if (Host) Target!.Invoke(Manager!.ServerManager, new object[] { e.Peer!, new PokerPacket(), true, Channel.Reliable }); else Manager!.ClientManager.Broadcast(new PokerPacket(), Channel.Reliable); });
            }, (entry, e) =>
            {
                if (!Host) try { PokerFeature.Current?.Panel?.RequestFinished(JsonSerializer.Deserialize<PokerCommand>(entry.Text)?.Token ?? ""); } catch (JsonException) { }
                ModSupport.WarnOnce("poker:send", e.Message);
            })) break;
    }
    private static void Unbind()
    {
        if (Manager) try { Manager!.ServerManager.UnregisterBroadcast<PokerPacket>(ServerHandler); Manager.ClientManager.UnregisterBroadcast<PokerPacket>(ClientHandler); } catch (Exception e) { ModSupport.WarnOnce("poker:unbind", e.Message); }
        Manager = null; Target = null;
    }
    internal static void Reset()
    { Unbind(); Wire.Reset(); Outbox.Clear(); Rate.Clear(); Epoch = Sync = ""; Sequence = Received = 0; NextSync = 0; HostEpoch = Guid.NewGuid().ToString(); }
}
