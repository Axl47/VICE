using Il2CppFishNet;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using UnityEngine;
using Police = NACops.NACops;

namespace VICE.Enforcement.Information;

internal static class InformationContact
{
    internal const string ContactId = "vice:enforcement:local-source";
    private static MelonPreferences_Entry<bool> enabled = null!;
    private static MelonPreferences_Entry<string> contactName = null!;
    private static MelonPreferences_Entry<int> cooldown = null!;
    private static MSGConversation? conversation;
    private static PressureReports? reports;
    private static string? path;
    private static string? lastWritten;
    private static float nextTick;
    private static readonly HashSet<string> rendered = new();
    internal static bool Active { get; private set; }
    internal static bool Host => InstanceFinder.NetworkManager && InstanceFinder.NetworkManager.ServerManager.Started;

    internal static void Initialize()
    {
        var prefs = MelonPreferences.CreateCategory("VICEEnforcementInformation", "VICE Enforcement information contact");
        enabled = prefs.CreateEntry("Enabled", true, description: "Receive broad property-pressure reports. Restart after changing.");
        contactName = prefs.CreateEntry("ContactName", "Local Source", description: "Provisional phone contact name. Restart after changing.");
        cooldown = prefs.CreateEntry("CooldownGameMinutes", 180, description: "Global interval between reports, clamped to 30–1440 game minutes. Host setting.");
    }
    internal static void Tick()
    {
        if (enabled is null || !enabled.Value || !Police.currentConfig.Enabled) { if (Active) End(); return; }
        var load = LoadManager.Instance;
        if (!load || !load.IsGameLoaded || !Player.Local || !Player.Local.PlayerLoaded)
        { if (Active) End(); return; }
        if (Time.realtimeSinceStartup < nextTick) return;
        nextTick = Time.realtimeSinceStartup + 1;
        try
        {
            if (!Active)
            {
                // Enforcement loads heat at the beginning of host Setup, before NPC setup.
                if (Host && !Police.registered) return;
                if (!MessagingManager.Instance || !MessagesApp.Instance || !TimeManager.Instance) return;
                // Heat readiness is set explicitly; guests never touch ActiveSaveInfo.
                if (Host && !HeatReady) return;
                var name = string.IsNullOrWhiteSpace(contactName.Value) ? "Local Source" : contactName.Value.Trim();
                conversation = new MSGConversation(new MessageContactInfo(name[..Math.Min(name.Length, 48)], ContactId, null!, true, false), ContactId);
                // Own history/sync here. Exclude from native messaging replication and save
                // loading: the native custom conversation may register after OnSpawnServer.
                MessagingManager.Instance._senderIdConversationMap.Remove(ContactId);
                conversation.SetIsKnown(true); conversation.EnsureUIExists(); conversation.SetEntryVisibility(true);
                Active = true;
                if (Host)
                {
                    var info = load.ActiveSaveInfo;
                    path = Path.Combine(NACops.ModDataPaths.Root, "InformationData", ReportStore.FileName(info.SaveSlotNumber, info.OrganisationName));
                    ReportState state;
                    try { state = ReportStore.Read(path); }
                    catch (Exception error) { ModSupport.WarnOnce("enforcement:report-load", "Information reports reset after unreadable data: " + error.Message); state = new(); }
                    reports = new(state, cooldown.Value);
                    reports.Start(TimeManager.Instance.GetTotalMinSum(), Snapshot());
                    Render(state.History, false);
                    Persist();
                }
                ReportNetwork.Bind();
            }
            if (Host && reports is not null)
            {
                reports.Advance(TimeManager.Instance.GetTotalMinSum());
                foreach (var (code, heat) in Snapshot()) reports.Observe(code, heat);
                var names = Property.OwnedProperties.ToArray().Where(p => p).GroupBy(p => p.PropertyCode)
                    .ToDictionary(g => g.Key, g => g.First().PropertyName);
                var message = reports.Take(names, NACops.RaidPropertyEvent.raidActive);
                // Persist the sent identity/cooldown before presentation or transport retries.
                Persist();
                if (message is not null) Render(new[] { message }, true);
            }
            ReportNetwork.Tick(reports?.State.History);
        }
        catch (Exception error) { ModSupport.WarnOnce("enforcement:information", "Information contact unavailable: " + error); }
    }
    internal static bool HeatReady { get; set; }
    private static (string Code, int Heat)[] Snapshot()
    {
        var owned = Property.OwnedProperties.ToArray().Where(p => p).Select(p => p.PropertyCode).ToHashSet(StringComparer.Ordinal);
        lock (Police.heatConfigLock) return Police.heatConfig.Where(p => owned.Contains(p.propertyCode)).Select(p => (p.propertyCode, p.propertyHeat)).ToArray();
    }
    internal static void RaidReset(string code)
    {
        // A raid clears upstream heat immediately. That is not a calming observation.
        reports?.Baseline(code, 0);
        Persist();
    }
    internal static void Render(IEnumerable<ReportMessage> messages, bool alert)
    {
        if (!Active || conversation is null) return;
        foreach (var message in messages)
        {
            if (rendered.Contains(message.Id)) continue;
            conversation.SendMessage(new Message(message.Text, Message.ESenderType.Other, true, UnityEngine.Random.Range(1, int.MaxValue)), alert, false);
            rendered.Add(message.Id);
        }
        conversation.HasChanged = false;
        if (!alert) conversation.SetRead(true);
    }
    internal static void Persist()
    {
        if (!Host || path is null || reports is null) return;
        try
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(reports.State);
            if (serialized == lastWritten) return;
            ReportStore.Write(path, reports.State); lastWritten = serialized;
        }
        catch (Exception error) { ModSupport.WarnOnce("enforcement:report-save", "Could not persist information reports: " + error.Message); }
    }
    internal static void End()
    {
        Persist();
        try { ReportNetwork.End(); }
        catch (Exception error) { ModSupport.WarnOnce("enforcement:network-cleanup", "Information receiver cleanup: " + error.Message); }
        if (conversation is not null)
        {
            try
            {
                if (MessagesApp.Instance && MessagesApp.Instance.currentConversation?.Pointer == conversation.Pointer)
                    conversation.SetOpen(false);
                MessagesApp.Conversations?.Remove(conversation);
                MessagesApp.ActiveConversations?.Remove(conversation);
                if (MessagesApp.Instance) MessagesApp.Instance.unreadConversations?.Remove(conversation);
                if (conversation.entry) UnityEngine.Object.Destroy(conversation.entry.gameObject);
                if (conversation.container) UnityEngine.Object.Destroy(conversation.container.gameObject);
                if (MessagingManager.Instance) MessagingManager.Instance._senderIdConversationMap.Remove(ContactId);
            }
            catch (Exception error) { ModSupport.WarnOnce("enforcement:contact-cleanup", "Information contact cleanup: " + error.Message); }
        }
        Active = false; HeatReady = false; conversation = null; reports = null; path = null; lastWritten = null;
        rendered.Clear(); nextTick = 0;
    }
}
