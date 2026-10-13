using System.Reflection;
using Il2CppFishNet;
using Il2CppFishNet.Serializing;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using UnityEngine;
using VICE.Enforcement.Information;
using Police = NACops.NACops;

[assembly: MelonInfo(typeof(InformationProbe), "VICE isolated information probe", "0.0.1", "local diagnostic")]
[assembly: HarmonyDontPatchAll]
public sealed class InformationProbe : MelonMod
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    private readonly Type contact = typeof(InformationPacket).Assembly.GetType("VICE.Enforcement.Information.InformationContact")!;
    private readonly Type network = typeof(InformationPacket).Assembly.GetType("VICE.Enforcement.Information.ReportNetwork")!;
    private string output = "", code = "";
    private float deadline, next;
    private int phase, session;
    private RectTransform? previousEntry, previousContainer;
    private RepairChecks? repair;
    public override void OnInitializeMelon()
    {
        output = Environment.GetCommandLineArgs().Single(a => a.StartsWith("--information-probe-output="))[27..];
        Check(Path.IsPathFullyQualified(output) && Directory.Exists(Path.Combine(output, "SaveGame_5")), "Isolated copy is required.");
        deadline = Time.realtimeSinceStartup + 240;
        Application.runInBackground = true;
        if (Environment.GetCommandLineArgs().Contains("--enforcement-repair-probe")) repair = new RepairChecks(output);
    }
    public override void OnUpdate()
    {
        if (repair is not null) { repair.Tick(); return; }
        if (phase == 9 || Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 1;
        try
        {
            Check(Time.realtimeSinceStartup < deadline, "Native information probe timed out.");
            var load = LoadManager.Instance; if (!load) return;
            if (phase == 0 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu" && !load.IsLoading)
            {
                if (session > 0)
                {
                    Check(!Active && MessagesApp.Conversations.ToArray().All(c => c.ConversationId != "vice:enforcement:local-source"), "Contact survived menu cleanup.");
                    Check(!previousEntry && !previousContainer, "Contact UI survived menu cleanup.");
                }
                var copy = Path.Combine(output, "SaveGame_5");
                Check(LoadManager.TryLoadSaveInfo(copy, 4, out var info, true), "Copied save is unreadable.");
                Check(LoadManager.SaveGames is null || !LoadManager.SaveGames.ToArray().Any(s => s is not null && Path.GetFullPath(s.SavePath) == copy), "Refused registered live save.");
                phase = 1; load.StartGame(info, false, false); return;
            }
            if (phase == 1 && load.IsGameLoaded && Player.Local && Player.Local.PlayerLoaded && Active)
            {
                Check(Path.GetFullPath(load.LoadedGameFolderPath) == Path.Combine(output, "SaveGame_5"), "Save isolation lost.");
                var conversation = Conversation;
                Check(conversation.IsSenderKnown && conversation.uiCreated && conversation.EntryVisible, "Native phone contact UI unavailable.");
                Check(!MessagingManager.Instance._senderIdConversationMap.ContainsKey(conversation.ConversationId), "Custom history leaked into native replication.");
                Check(!SaveManager.Instance.Saveables.ToArray().Any(s => s.Pointer == conversation.Pointer), "Custom history leaked into native save registry.");
                if (session == 0)
                {
                    Check(conversation.MessageHistoryCount == 0, "Initial baseline dumped phone messages.");
                    code = Property.OwnedProperties.ToArray().First(p => Police.heatConfig.Any(h => h.propertyCode == p.PropertyCode)).PropertyCode;
                    SetHeat(6); phase = 2;
                }
                else
                {
                    Check(MessagesApp.Conversations.ToArray().Count(c => c.ConversationId == conversation.ConversationId) == 1, "Contact thread duplicated after reload.");
                    Check(conversation.MessageHistoryCount == 2 && conversation.Read, "History was not restored silently exactly once.");
                    WireCheck();
                    File.WriteAllText(Path.Combine(output, "passed.txt"), "PASS: native contact/UI, silent first baseline, rise and real calm messages, cooldown, deduplication, isolated save/reload, contact registry/menu/UI cleanup, native generic writer/reader. Two-player delivery remains untested.");
                    phase = 9; Application.Quit();
                }
                return;
            }
            if (phase == 2 && Conversation.MessageHistoryCount == 1)
            {
                Check(Conversation._messageHistory[0].text.Contains("getting attention"), "Rise wording missing.");
                SetHeat(3); phase = 3; return;
            }
            if (phase == 3)
            {
                Check(Conversation.MessageHistoryCount == 1, "Cooldown was bypassed.");
                var policy = contact.GetField("reports", Flags)!.GetValue(null)!;
                var state = policy.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!;
                state.GetType().GetProperty("NextReport")!.SetValue(state, 0L); // Diagnostic expiry in the isolated copy.
                phase = 4; return;
            }
            if (phase == 4 && Conversation.MessageHistoryCount == 2)
            {
                Check(Conversation._messageHistory[1].text.Contains("quiet"), "Calm wording missing.");
                contact.GetMethod("Tick", Flags)!.Invoke(null, null);
                Check(Conversation.MessageHistoryCount == 2, "Repeated polling duplicated a message.");
                SaveManager.Instance.Save(Path.Combine(output, "SaveGame_5")); phase = 5; return;
            }
            if (phase == 5 && !SaveManager.Instance.IsSaving)
            { previousEntry = Conversation.entry; previousContainer = Conversation.container; session++; phase = 0; load.ExitToMenu(null, null, false); }
        }
        catch (Exception error)
        { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); MelonLogger.Error(error); phase = 9; Application.Quit(); }
    }
    private bool Active => (bool)contact.GetProperty("Active", Flags)!.GetValue(null)!;
    private MSGConversation Conversation => (MSGConversation)contact.GetField("conversation", Flags)!.GetValue(null)!;
    private void SetHeat(int heat) { lock (Police.heatConfigLock) Police.heatConfig.Single(h => h.propertyCode == code).propertyHeat = heat; }
    private void WireCheck()
    {
        var wire = network.GetField("wire", Flags)!.GetValue(null)!;
        var payload = "[{\"Id\":\"00000000000000000000000000000001\",\"Text\":\"Native information ✓\"}]";
        var writer = WriterPool.GetWriter(InstanceFinder.NetworkManager);
        try
        {
            wire.GetType().GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(wire, new object[] { payload, (Action)(() => writer.Write(new InformationPacket())) });
            var reader = ReaderPool.GetReader(writer.GetArraySegment(), InstanceFinder.NetworkManager, (Reader.DataSource)0);
            try
            {
                reader.Read<InformationPacket>();
                var args = new object?[] { null };
                Check((bool)wire.GetType().GetMethod("TryTake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(wire, args)! && (string)args[0]! == payload && reader.Remaining == 0, "Native packet round-trip changed the report.");
            }
            finally { reader.Store(); }
        }
        finally { writer.Store(); }
    }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
