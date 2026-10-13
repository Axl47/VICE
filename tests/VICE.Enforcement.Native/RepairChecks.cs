using System.Reflection;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet.Serializing;
using Il2CppScheduleOne.Core.Avatar;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using MelonLoader;
using NACops;
using UnityEngine;
using VICE.Enforcement.Multiplayer;
using Police = NACops.NACops;

internal sealed class RepairChecks(string output)
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    private readonly Type replica = typeof(EnforcementWorldPacket).Assembly.GetType("VICE.Enforcement.Multiplayer.EnforcementReplication")!;
    private readonly float deadline = Time.realtimeSinceStartup + 240;
    private int phase, cycle, sessions;
    private float next;
    private NPC? donor;
    private PoliceOfficer? subject;
    private string donorJson = "";
    internal void Tick()
    {
        if (phase == 9 || Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 0.3f;
        try
        {
            Check(Time.realtimeSinceStartup < deadline, "Repair diagnostic timed out.");
            var load = LoadManager.Instance; if (!load) return;
            if (phase == 0 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu" && !load.IsLoading)
            {
                if (sessions > 0) Check(!CopInitHelper.copBaseClone && !CopInitHelper.investigator && MassSurveillance.allCameras.Count == 0, "Officer/camera state survived save exit.");
                var copy = Path.Combine(output, "SaveGame_5");
                Check(LoadManager.TryLoadSaveInfo(copy, 4, out var info, true), "Isolated save unavailable.");
                Check(LoadManager.SaveGames is null || !LoadManager.SaveGames.ToArray().Any(s => s is not null && Path.GetFullPath(s.SavePath) == copy), "Refused registered live save.");
                phase = 1; load.StartGame(info, false, false); return;
            }
            if (phase == 1 && load.IsGameLoaded && Player.Local && Player.Local.PlayerLoaded && Police.hasInitiatedAllOfficers && MassSurveillance.allCameras.Count > 0)
            {
                Check(Path.GetFullPath(load.LoadedGameFolderPath) == Path.Combine(output, "SaveGame_5"), "Save isolation lost.");
                var template = CopInitHelper.copBaseClone.GetComponent<NetworkObject>();
                Check(template.SceneId == 0 && template.SpawnableCollectionId == 47123 && template.PrefabId == 0, "Runtime template has inherited scene identity.");
                Check(InstanceFinder.NetworkManager.GetPrefabObjects<DefaultPrefabObjects>(47123, false).GetObject(true, 0).Pointer == template.Pointer, "Native prefab collection does not resolve the custom template.");
                var officers = CopInitHelper.generatedOfficerPool.ToArray().Where(o => o && o.NPCData.BasicInfo.ID.StartsWith("NACop_", StringComparison.Ordinal)).Concat(new[] { CopInitHelper.investigator, CopInitHelper.buyBustCop }).ToArray();
                Check(officers.All(o => o && o.Avatar && o.Health && o.Movement), "Runtime officer native graph did not initialize.");
                Check(officers.All(o => o.GetComponent<NetworkObject>().IsNetworked && o.GetComponent<NetworkObject>().IsSpawned && o.GetComponent<NetworkObject>().SceneId == 0), "Runtime officers are not genuinely spawned network objects: " + string.Join("; ", officers.Select(o => { var n = o.GetComponent<NetworkObject>(); return $"{o.NPCData.BasicInfo.ID} id={n.ObjectId} spawned={n.IsSpawned} networked={n.IsNetworked} scene={n.SceneId} parent={n.ParentNetworkObject?.name}"; })));
                Check(officers.Select(o => o.GetComponent<NetworkObject>().ObjectId).Distinct().Count() == officers.Length, "Runtime officers share network ObjectIds.");
                Check(officers.Select(o => o.NPCData.Pointer).Distinct().Count() == officers.Length && officers.Select(o => o.GUID.ToString()).Distinct().Count() == officers.Length, "Runtime officers share data/GUIDs.");
                if (sessions > 0)
                {
                    WireCheck();
                    File.WriteAllText(Path.Combine(output, "repair-passed.txt"), "PASS: two native loaded sessions; registered non-scene officer prefab; independent spawned network ObjectIds/data/GUIDs; six investigator outfit replacements without duplicate hair or donor mutation; nonempty isolated textures without NPC movement; native naked-appearance JSON and packet round-trips; camera initialization and save-exit cleanup. Real two-player delivery remains untested.");
                    phase = 9; Application.Quit(); return;
                }
                subject = CopInitHelper.investigator; subject.gameObject.SetActive(true); subject.SetVisible(true, false);
                subject.Avatar.gameObject.SetActive(true); subject.Avatar.BodyContainer.gameObject.SetActive(true); subject.Movement.enabled = false;
                donor = NPCManager.NPCRegistry.ToArray().First(n => n && n.TryCast<PoliceOfficer>() is null && n.Avatar && n.Avatar.Appearance.AppliedNakedAppearance is not null && n.NPCData?.Appearance?.DefaultOutfit);
                donorJson = donor.Avatar.Appearance.AppliedNakedAppearance.ToJson();
                phase = 2; return;
            }
            if (phase == 2)
            {
                var naked = donor!.Avatar.Appearance.AppliedNakedAppearance.Clone();
                naked.SetHairAndEyebrowColor(cycle % 2 == 0 ? Color.red : Color.blue);
                var decoded = JsonUtility.FromJson<NakedAppearance>(naked.ToJson());
                Check(decoded.IsValid() && decoded.HairColor == naked.HairColor, "Native appearance JSON round-trip failed.");
                AvatarUtility.ReplaceAppearance(subject!, decoded, donor.NPCData.Appearance.DefaultOutfit.AvatarObjects.ToArray());
                phase = 3; return;
            }
            if (phase == 3)
            {
                var position = subject!.transform.position;
                var texture = RuntimeImpostor.CreateImpostor(subject);
                Check(texture.GetPixels32().ToArray().Any(p => p.a > 0), "Captured avatar texture is empty.");
                Check(Vector3.Distance(subject!.transform.position, position) < 0.01f, "Impostor capture moved the network investigator.");
                Check(subject.Avatar.GetComponentsInChildren<HairAvatarObject>(true).Count(h => h.gameObject.activeInHierarchy) <= 1, "Multiple hair objects survived appearance replacement.");
                Check(donor!.Avatar.Appearance.AppliedNakedAppearance.ToJson() == donorJson, "Changing the investigator mutated its civilian donor.");
                if (++cycle < 6) { phase = 2; return; }
                var camera = MassSurveillance.allCameras.First(); camera.ActivateInstance();
                Check(camera.IsActive && camera.cameraLight && camera.lineRenderer, "Camera presentation failed to initialize.");
                camera.BreakAuthoritative(); Check(camera.IsBroken, "Host camera damage did not update authoritative state.");
                camera.DeactivateInstance(); camera.ActivateInstance(); Check(!camera.IsBroken, "Camera rotation did not reset broken state.");
                sessions++; phase = 0; load.ExitToMenu(null, null, false);
            }
        }
        catch (Exception e)
        { File.WriteAllText(Path.Combine(output, "repair-failure.txt"), e.ToString()); MelonLogger.Error("ENFORCEMENT_REPAIR_FAILED " + e); phase = 9; Application.Quit(); }
    }
    private void WireCheck()
    {
        var wire = replica.GetField("Wire", Flags)!.GetValue(null)!;
        var writer = WriterPool.GetWriter(InstanceFinder.NetworkManager);
        try
        {
            const string payload = "{\"native\":\"police + camera ✓\"}";
            wire.GetType().GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(wire, new object[] { payload, (Action)(() => writer.Write(new EnforcementWorldPacket())) });
            var reader = ReaderPool.GetReader(writer.GetArraySegment(), InstanceFinder.NetworkManager, (Reader.DataSource)0);
            try
            {
                reader.Read<EnforcementWorldPacket>(); var args = new object?[] { null };
                Check((bool)wire.GetType().GetMethod("TryTake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(wire, args)! && (string)args[0]! == payload && reader.Remaining == 0, "Native police packet serializer changed the payload.");
            }
            finally { reader.Store(); }
        }
        finally { writer.Store(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
