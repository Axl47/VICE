using System.Reflection;
using System.Text.Json;
using Il2CppFishNet;
using Il2CppFishNet.Broadcast;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Core.Avatar;
using Il2CppScheduleOne.Core.Avatar.Properties;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using MelonLoader;
using PersonalMods.Networking;
using UnityEngine;
using Police = NACops.NACops;

namespace VICE.Enforcement.Multiplayer;

[RegisterTypeInIl2Cpp]
[Il2CppImplements(typeof(IBroadcast))]
public sealed class EnforcementWorldPacket : Il2CppSystem.Object
{
    public EnforcementWorldPacket(IntPtr ptr) : base(ptr) { }
    public EnforcementWorldPacket() : base(ClassInjector.DerivedConstructorPointer<EnforcementWorldPacket>()) => ClassInjector.DerivedConstructorBody(this);
}

internal static class EnforcementReplication
{
    internal const ushort CollectionId = 47123;
    private static readonly SynchronousPacketPayload Wire = new(512000);
    private static readonly Dictionary<IntPtr, NPC> Tracked = new();
    private static readonly Dictionary<IntPtr, OfficerState> Appearances = new();
    private static readonly Dictionary<IntPtr, OfficerState> GuestIdentity = new();
    private static readonly Dictionary<string, (ClientObservation Vision, float At)> Observations = new();
    private static readonly Dictionary<int, float> LastInput = new();
    private static readonly List<CameraNotice> Notices = new();
    private static readonly HashSet<string> AppliedNotices = new();
    private static Il2CppSystem.Action<Writer, EnforcementWorldPacket>? writer;
    private static Il2CppSystem.Func<Reader, EnforcementWorldPacket>? reader;
    private static Il2CppSystem.Action<NetworkConnection, EnforcementWorldPacket>? hostReceiver;
    private static Il2CppSystem.Action<EnforcementWorldPacket>? clientReceiver;
    private static NetworkManager? manager;
    private static string session = Guid.NewGuid().ToString("N");
    private static string guestSession = "";
    private static float next, nextTemplate;
    private static int templateFrame;
    private static bool guestCamerasStarted;
    private static bool sceneReady, ownsCollection;
    private static string pendingBreak = "";
    private static object? guestCameraCoroutine;
    internal static GameObject? Template { get; private set; }
    internal static bool TemplateReady => Template && Time.frameCount > templateFrame;
    internal static bool Host => InstanceFinder.NetworkManager && InstanceFinder.NetworkManager.ServerManager.Started;
    internal static bool SessionReady => LoadManager.Instance && LoadManager.Instance.IsGameLoaded && Player.Local && Player.Local.PlayerLoaded;

    internal static void BeginScene() { sceneReady = true; PrepareTemplate(); }

    internal static void PrepareTemplate()
    {
        if (!sceneReady || Template || !Police.currentConfig.Enabled || !InstanceFinder.NetworkManager) return;
        var donor = PoliceOfficer.Officers.ToArray().Where(o => o && o.NPCData is not null && o.NPCData.BasicInfo.ID.StartsWith("officer", StringComparison.OrdinalIgnoreCase)
            && o.NPCData.BasicInfo.ID != "officerPrefab").OrderBy(o => o.NPCData.BasicInfo.ID, StringComparer.Ordinal).FirstOrDefault();
        if (!donor) return;
        var wasActive = donor!.gameObject.activeSelf;
        var data = donor.NPCData.GetDeepCopy();
        GameObject? template = null;
        try
        {
            donor.gameObject.SetActive(false);
            template = UnityEngine.Object.Instantiate(donor.gameObject);
            template.name = "VICE.Enforcement.NetworkOfficerPrefab";
            template.SetActive(false);
            NACops.AvatarUtility.ClearClonedObjects(template);
            var npc = template.GetComponent<NPC>();
            npc._NPCData_k__BackingField = data;
            npc.NPCData.BasicInfo.ID = "officerPrefab";
            npc.BakedGUID = Guid.NewGuid().ToString();
            var net = template.GetComponent<NetworkObject>();
            net.ResetState(); net.SceneId = 0; net.SetIsNetworked(true);
            var prefabs = InstanceFinder.NetworkManager.GetPrefabObjects<Il2CppFishNet.Managing.Object.DefaultPrefabObjects>(CollectionId, true);
            if (prefabs.GetObjectCount() != 0) throw new InvalidOperationException("Enforcement prefab collection is already occupied.");
            prefabs.AddObject(net, true);
            ownsCollection = true;
            net.SpawnableCollectionId = CollectionId; net.PrefabId = 0;
            // The argument is the parent object, not the root itself. Passing
            // this root makes FishNet treat the officer as its own unspawned parent.
            byte index = 0; net.UpdateNetworkBehaviours(null!, ref index);
            net.ParentNetworkObject = null!; net.RuntimeParentNetworkObject = null!; net.RuntimeParentTransform = null!; net.IsNested = false;
            Template = template; templateFrame = Time.frameCount;
        }
        catch
        {
            if (ownsCollection) { InstanceFinder.NetworkManager.RemoveSpawnableCollection(CollectionId); ownsCollection = false; }
            if (template) UnityEngine.Object.Destroy(template); throw;
        }
        finally { if (donor) donor.gameObject.SetActive(wasActive); }
    }

    internal static void Tick()
    {
        if (!Police.currentConfig.Enabled) return;
        var load = LoadManager.Instance;
        // Register the prefab before host setup/network spawning, including guests.
        if (sceneReady && !Template && Time.realtimeSinceStartup >= nextTemplate && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main")
        {
            nextTemplate = Time.realtimeSinceStartup + 0.5f;
            try { PrepareTemplate(); } catch (Exception e) { ModSupport.WarnOnce("enforcement:prefab", "Officer network prefab unavailable: " + e); }
        }
        if (!SessionReady) return;
        Bind();
        if (!Host && !guestCamerasStarted)
        {
            guestCamerasStarted = true;
            guestCameraCoroutine = MelonCoroutines.Start(NACops.MassSurveillance.SetupMassSurveillance());
        }
        if (Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + (Host ? 0.5f : 0.25f);
        try
        {
            if (Host) Publish();
            else { var input = LocalVision(); input.BreakCamera = pendingBreak; input.AcknowledgedNotices = AppliedNotices.TakeLast(128).ToArray(); SendObservation(input); }
        }
        catch (Exception e) { ModSupport.WarnOnce("enforcement:replication-tick", "Police synchronization will retry: " + e.Message); }
    }

    private static void Bind()
    {
        if (manager) return;
        manager = InstanceFinder.NetworkManager;
        writer ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Writer, EnforcementWorldPacket>>((Action<Writer, EnforcementWorldPacket>)((w, _) => Wire.Write(w.WriteString)));
        reader ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Reader, EnforcementWorldPacket>>((Func<Reader, EnforcementWorldPacket>)Read);
        hostReceiver ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NetworkConnection, EnforcementWorldPacket>>((Action<NetworkConnection, EnforcementWorldPacket>)ReceiveInput);
        clientReceiver ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<EnforcementWorldPacket>>((Action<EnforcementWorldPacket>)ReceiveWorld);
        GenericWriter<EnforcementWorldPacket>.Write = writer; GenericReader<EnforcementWorldPacket>.Read = reader;
        manager!.ServerManager.RegisterBroadcast<EnforcementWorldPacket>(hostReceiver, true);
        manager.ClientManager.RegisterBroadcast<EnforcementWorldPacket>(clientReceiver);
    }
    private static EnforcementWorldPacket Read(Reader r)
    {
        try { Wire.Read(r.ReadString); } catch (Exception e) { ModSupport.WarnOnce("enforcement:world-wire", "Rejected police packet: " + e.Message); }
        return new();
    }
    internal static void Track(NPC npc)
    {
        if (Host && npc && npc.GetComponent<NetworkObject>().SpawnableCollectionId == CollectionId) Tracked[npc.Pointer] = npc;
    }
    internal static void AppearanceChanged(PoliceOfficer officer)
    {
        if (!Host || !officer || officer.Avatar.Appearance.AppliedNakedAppearance is null) return;
        Track(officer);
        var items = officer.Avatar.Appearance._wornObjects.ToArray().Where(o => o).Select(o => o.Serialize()).Select(o => new WornObject {
            Id = o.Id, Colors = o.Colors.ToArray().Select(c => new ColorValue { Id = c.PropertyId, Value = new[] { c.Value.r, c.Value.g, c.Value.b, c.Value.a } }).ToList()
        }).ToList();
        Appearances[officer.Pointer] = new() {
            Naked = officer.Avatar.Appearance.AppliedNakedAppearance.ToJson(), Outfit = items,
            Belt = officer.PoliceBelt && officer.PoliceBelt.gameObject.activeSelf, AppearanceRevision = Guid.NewGuid().ToString("N")
        };
    }
    private static void Publish()
    {
        if (!manager) return;
        Notices.RemoveAll(n => !manager.ServerManager.Clients.ContainsKey(n.Connection));
        var state = new WorldState { Session = session, NoticeSpeed = Police.surveillanceConfig.CameraNoticeSpeed, Notices = Notices.ToList() };
        foreach (var npc in Tracked.Values.ToArray())
        {
            if (!npc) { Tracked.Remove(npc.Pointer); Appearances.Remove(npc.Pointer); continue; }
            if (!npc.GetComponent<NetworkObject>().IsSpawned || !npc.NPCData.BasicInfo.ID.StartsWith("NACop_", StringComparison.Ordinal)) continue;
            if (!Appearances.TryGetValue(npc.Pointer, out var appearance)) { AppearanceChanged(npc.Cast<PoliceOfficer>()); if (!Appearances.TryGetValue(npc.Pointer, out appearance)) continue; }
            var snapshot = JsonSerializer.Deserialize<OfficerState>(JsonSerializer.Serialize(appearance))!;
            snapshot.ObjectId = npc.GetComponent<NetworkObject>().ObjectId; snapshot.Guid = npc.GUID.ToString();
            snapshot.Id = npc.NPCData.BasicInfo.ID; snapshot.Name = npc.NPCData.BasicInfo.FirstName;
            snapshot.Visible = npc.gameObject.activeInHierarchy && npc.isVisible;
            state.Officers.Add(snapshot);
        }
        foreach (var camera in NACops.MassSurveillance.allCameras) if (camera) state.Cameras.Add(camera.Snapshot());
        var officers = state.Officers.ToArray();
        foreach (var connection in manager.ServerManager.Clients.Values)
        {
            if (!connection.Authenticated || connection.IsLocalClient) continue;
            state.Notices = Notices.Where(n => n.Connection == connection.ClientId).ToList();
            // Bounded batches retain every officer without exceeding the wire cap
            // when a raid and an extended police pool are present together.
            for (var offset = 0; offset < Math.Max(1, officers.Length); offset += 8)
            {
                state.Officers = officers.Skip(offset).Take(8).ToList();
                try { Send(connection, JsonSerializer.Serialize(state)); } catch (Exception e) { ModSupport.WarnOnce("enforcement:world-send", "Police synchronization will retry: " + e.Message); }
            }
        }
    }
    private static void Send(NetworkConnection connection, string payload) => Wire.Send(payload, () => {
        var method = manager!.ServerManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(m => m.Name == "Broadcast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(NetworkConnection));
        method.MakeGenericMethod(typeof(EnforcementWorldPacket)).Invoke(manager.ServerManager, new object[] { connection, new EnforcementWorldPacket(), true, Channel.Reliable });
    });
    private static void SendObservation(ClientObservation observation)
    {
        try { Wire.Send(JsonSerializer.Serialize(observation), () => manager!.ClientManager.Broadcast(new EnforcementWorldPacket(), Channel.Reliable)); }
        catch (Exception e) { ModSupport.WarnOnce("enforcement:vision-send", "Camera observation will retry: " + e.Message); }
    }
    internal static ClientObservation LocalVision() => new() { Visibility = Player.Local.VisibilityComponent.CurrentVisibility,
        Labels = ReplicationRules.NormalizeLabels(Player.Local.VisibilityComponent.VisualStates.ToArray().Select(v => v.label)) };
    internal static ClientObservation? Vision(Player player)
    {
        if (player.Pointer == Player.Local.Pointer) return LocalVision();
        return Observations.TryGetValue(player.PlayerCode, out var item) && Time.realtimeSinceStartup - item.At < 2 ? item.Vision : null;
    }
    private static void ReceiveInput(NetworkConnection connection, EnforcementWorldPacket packet)
    {
        if (!Wire.TryTake(out var payload) || !Host || !SessionReady || !connection.Authenticated) return;
        var player = Player.PlayerList.ToArray().FirstOrDefault(p => p && p.PlayerLoaded && p.Owner.ClientId == connection.ClientId);
        if (!player) return;
        if (LastInput.TryGetValue(connection.ClientId, out var last) && Time.realtimeSinceStartup - last < 0.1f) return;
        LastInput[connection.ClientId] = Time.realtimeSinceStartup;
        try
        {
            var input = JsonSerializer.Deserialize<ClientObservation>(payload)!;
            if (input is null || !float.IsFinite(input.Visibility) || input.Labels is null || input.Labels.Length > 32 || input.AcknowledgedNotices is null || input.AcknowledgedNotices.Length > 128) return;
            Notices.RemoveAll(n => n.Connection == connection.ClientId && input.AcknowledgedNotices.Contains(n.Id, StringComparer.Ordinal));
            input.Visibility = Mathf.Clamp(input.Visibility, 0, 100); input.Labels = ReplicationRules.NormalizeLabels(input.Labels);
            Observations[player!.PlayerCode] = (input, Time.realtimeSinceStartup);
            if (input.BreakCamera.Length > 0)
            {
                var camera = NACops.MassSurveillance.allCameras.FirstOrDefault(c => c && c.CameraId == input.BreakCamera);
                if (camera && Vector3.Distance(player.Avatar.CenterPoint, camera.transform.position) <= 75) camera!.BreakAuthoritative();
            }
        }
        catch (Exception e) { ModSupport.WarnOnce("enforcement:vision-input", "Rejected camera observation: " + e.Message); }
    }
    internal static void RequestBreak(string id)
    {
        pendingBreak = id;
    }
    private static void ReceiveWorld(EnforcementWorldPacket packet)
    {
        if (!Wire.TryTake(out var payload) || Host || !SessionReady) return;
        try
        {
            var state = JsonSerializer.Deserialize<WorldState>(payload)!;
            if (state is null || !ReplicationRules.Valid(state)) return;
            if (guestSession.Length > 0 && guestSession != state.Session) return;
            guestSession = state.Session;
            foreach (var appearance in state.Officers)
            {
                var npc = NPCManager.NPCRegistry.ToArray().FirstOrDefault(n => n && n.GetComponent<NetworkObject>() is { } no && no.ObjectId == appearance.ObjectId && no.SpawnableCollectionId == CollectionId)?.TryCast<PoliceOfficer>();
                if (!npc) continue; // Native spawn may arrive later; snapshots retry.
                var changed = !GuestIdentity.TryGetValue(npc!.Pointer, out var previous) || previous.AppearanceRevision != appearance.AppearanceRevision;
                GuestIdentity[npc.Pointer] = appearance; RestoreGuestIdentity(npc);
                if (changed)
                {
                    var naked = JsonUtility.FromJson<NakedAppearance>(appearance.Naked);
                    var outfit = appearance.Outfit.Select(w => new SerializedAvatarObject(w.Id, new Il2CppReferenceArray<SerializedColorProperty>(w.Colors.Select(c => new SerializedColorProperty(c.Id, new Color(c.Value[0], c.Value[1], c.Value[2], c.Value[3]))).ToArray()))).ToArray();
                    NACops.AvatarUtility.ReplaceAppearance(npc, naked, outfit);
                    if (npc.PoliceBelt) npc.PoliceBelt.gameObject.SetActive(appearance.Belt);
                    npc.Avatar.SetImpostorTexture(NACops.RuntimeImpostor.CreateImpostor(npc));
                }
                npc.SetVisible(appearance.Visible, false);
                if (appearance.Id == "NACop_Investigator") { NACops.CopInitHelper.investigator = npc; NACops.CopInitHelper.investigatorID = npc.gameObject.GetInstanceID(); PoliceOfficer.Officers.Remove(npc); }
                if (appearance.Id == "NACop_BuyBust") { NACops.CopInitHelper.buyBustCop = npc; NACops.CopInitHelper.buyBustCopID = npc.gameObject.GetInstanceID(); PoliceOfficer.Officers.Remove(npc); }
            }
            foreach (var snapshot in state.Cameras)
            {
                var camera = NACops.MassSurveillance.allCameras.FirstOrDefault(c => c && c.CameraId == snapshot.Id);
                if (camera) camera!.ApplySnapshot(snapshot, state.NoticeSpeed);
                if (snapshot.Id == pendingBreak && (!snapshot.Active || snapshot.Broken)) pendingBreak = "";
            }
            foreach (var notice in state.Notices.Where(n => n.Player == Player.Local.PlayerCode))
                if (AppliedNotices.Add(notice.Id))
                {
                    ApplyOwnerCrimes(notice.Labels);
                    if (notice.RefreshPursuit && Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None)
                    { Player.Local.RecordLastKnownPosition(true); Player.Local.CrimeData.CurrentPursuitLevelDuration = 0; Player.Local.CrimeData.TimeSincePursuitStart = 0; }
                }
        }
        catch (Exception e) { ModSupport.WarnOnce("enforcement:world-receive", "Police snapshot rejected: " + e); }
    }
    internal static void RestoreGuestIdentity(PoliceOfficer npc)
    {
        if (Host || !GuestIdentity.TryGetValue(npc.Pointer, out var state)) return;
        if (npc.NPCData.BasicInfo.ID != state.Id)
        {
            npc._NPCData_k__BackingField = npc.NPCData.GetDeepCopy();
            npc.NPCData.BasicInfo.ID = state.Id; npc.NPCData.BasicInfo.LastName = "";
        }
        npc.NPCData.BasicInfo.FirstName = state.Name;
        npc.BakedGUID = state.Guid; npc.SetGUID(new Il2CppSystem.Guid(state.Guid));
    }
    internal static void NotifyCameraNotice(Player player, List<string> labels)
    {
        if (player.Pointer == Player.Local.Pointer || (!Police.surveillanceConfig.SurveilBaseCrimes && !Police.surveillanceConfig.SurveilCrimeStatus)) return;
        Notices.Add(new() { Player = player.PlayerCode, Connection = player.Owner.ClientId, Labels = Police.surveillanceConfig.SurveilBaseCrimes ? ReplicationRules.NormalizeLabels(labels) : Array.Empty<string>(), RefreshPursuit = Police.surveillanceConfig.SurveilCrimeStatus });
        if (Notices.Count > 64) Notices.RemoveAt(0);
    }
    private static void ApplyOwnerCrimes(string[] labels)
    {
        foreach (var label in labels) switch (label)
        {
            case "DisobeyingCurfew": Player.Local.CrimeData.AddCrime(new ViolatingCurfew()); break;
            case "Vandalizing": Player.Local.CrimeData.AddCrime(new Vandalism()); break;
            case "DrugDealing": Player.Local.CrimeData.AddCrime(new AttemptingToSell()); Player.Local.CrimeData.AddCrime(new DrugTrafficking()); Player.Local.CrimeData.AddCrime(new TransportingIllicitItems()); break;
            case "Wanted": Player.Local.CrimeData.AddCrime(new Evading()); Player.Local.CrimeData.AddCrime(new FailureToComply()); break;
            case "Pickpocketing": Player.Local.CrimeData.AddCrime(new Theft()); break;
            case "DischargingWeapon": Player.Local.CrimeData.AddCrime(new BrandishingWeapon()); Player.Local.CrimeData.AddCrime(new DischargeFirearm()); break;
            case "Brandishing": Player.Local.CrimeData.AddCrime(new BrandishingWeapon()); break;
        }
    }
    internal static void End()
    {
        try
        {
            if (manager && hostReceiver is not null) manager!.ServerManager.UnregisterBroadcast<EnforcementWorldPacket>(hostReceiver);
            if (manager && clientReceiver is not null) manager!.ClientManager.UnregisterBroadcast<EnforcementWorldPacket>(clientReceiver);
        }
        finally
        {
            if (guestCameraCoroutine is not null) MelonCoroutines.Stop(guestCameraCoroutine);
            guestCameraCoroutine = null; guestCamerasStarted = false; pendingBreak = "";
            sceneReady = false;
            if (ownsCollection) InstanceFinder.NetworkManager?.RemoveSpawnableCollection(CollectionId);
            ownsCollection = false;
            if (Template) UnityEngine.Object.Destroy(Template);
            Template = null; manager = null; next = nextTemplate = 0; Wire.Reset();
            Tracked.Clear(); Appearances.Clear(); GuestIdentity.Clear(); Observations.Clear(); LastInput.Clear(); Notices.Clear(); AppliedNotices.Clear();
            session = Guid.NewGuid().ToString("N"); guestSession = "";
        }
    }
}
