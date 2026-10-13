using System.Reflection;
using System.Text.Json;
using Il2CppFishNet;
using Il2CppFishNet.Broadcast;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using PersonalMods.Networking;

namespace VICE.Enforcement.Information;

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class InformationPacket : Il2CppSystem.Object
{
    public InformationPacket(IntPtr pointer) : base(pointer) { }
    public InformationPacket() : base(ClassInjector.DerivedConstructorPointer<InformationPacket>()) => ClassInjector.DerivedConstructorBody(this);
}
internal static class ReportNetwork
{
    private static readonly SynchronousPacketPayload wire = new(16000);
    private static Il2CppSystem.Action<Writer, InformationPacket>? writer;
    private static Il2CppSystem.Func<Reader, InformationPacket>? reader;
    private static Il2CppSystem.Action<InformationPacket>? receiver;
    private static NetworkManager? manager;
    private static float nextPublish;
    private static bool received;
    internal static void Bind()
    {
        if (manager is not null) return;
        manager = InstanceFinder.NetworkManager;
        writer ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer, InformationPacket>>((Action<Writer, InformationPacket>)Write);
        reader ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader, InformationPacket>>((Func<Reader, InformationPacket>)Read);
        receiver ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<InformationPacket>>((Action<InformationPacket>)Receive);
        GenericWriter<InformationPacket>.Write = writer; GenericReader<InformationPacket>.Read = reader;
        manager.ClientManager.RegisterBroadcast<InformationPacket>(receiver);
    }
    private static void Write(Writer destination, InformationPacket packet) => wire.Write(destination.WriteString);
    private static InformationPacket Read(Reader source)
    {
        try { wire.Read(source.ReadString); }
        catch (Exception error) { ModSupport.WarnOnce("enforcement:report-wire", "Rejected information packet: " + error.Message); }
        return new();
    }
    internal static void Tick(List<ReportMessage>? history)
    {
        if (!InformationContact.Host || history is null || !manager || UnityEngine.Time.realtimeSinceStartup < nextPublish) return;
        nextPublish = UnityEngine.Time.realtimeSinceStartup + 5;
        // Repeated bounded snapshots give late loaders a baseline and retry delivery.
        // Each connected organization's loaded player gets the same text; host is local.
        var payload = JsonSerializer.Serialize(history);
        foreach (var connection in manager.ServerManager.Clients.Values)
        {
            if (!connection.Authenticated || connection.IsLocalClient
                || !Player.PlayerList.ToArray().Any(p => p && p.PlayerLoaded && p.Owner.ClientId == connection.ClientId)) continue;
            try
            {
                wire.Send(payload, () => {
                    var broadcast = manager.ServerManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Single(m => m.Name == "Broadcast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4
                            && m.GetParameters()[0].ParameterType == typeof(NetworkConnection));
                    broadcast.MakeGenericMethod(typeof(InformationPacket)).Invoke(manager.ServerManager,
                        new object[] { connection, new InformationPacket(), true, Channel.Reliable });
                });
            }
            catch (Exception error) { ModSupport.WarnOnce("enforcement:report-broadcast", "Information delivery will retry: " + error.Message); }
        }
    }
    private static void Receive(InformationPacket packet)
    {
        if (!wire.TryTake(out var payload) || InformationContact.Host || !InformationContact.Active) return;
        try
        {
            var messages = JsonSerializer.Deserialize<List<ReportMessage>>(payload) ?? throw new InvalidDataException("Empty report snapshot.");
            if (messages.Count > 20 || messages.Any(m => m is null || !Guid.TryParseExact(m.Id, "N", out _) || m.Text.Length > 512))
                throw new InvalidDataException("Invalid report snapshot.");
            InformationContact.Render(messages, received); received = true;
        }
        catch (Exception error) { ModSupport.WarnOnce("enforcement:report-receive", "Rejected information report: " + error.Message); }
    }
    internal static void End()
    {
        try { if (manager && receiver is not null) manager.ClientManager.UnregisterBroadcast<InformationPacket>(receiver); }
        finally { manager = null; wire.Reset(); received = false; nextPublish = 0; }
    }
}
