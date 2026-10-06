using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Compass;
using Il2CppScheduleOne.UI.Phone.Map;
using MelonLoader;

[assembly: MelonInfo(typeof(PersonalMods.UIEnhancements.UIEnhancementsMod), "Personal UI Enhancements", "0.1.2", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.UIEnhancements;

public sealed class UIEnhancementsMod : MelonMod
{
    internal static MelonPreferences_Entry<bool> Enabled = null!, DealPayouts = null!, CustomerMarkers = null!, PlayerCompassMarkers = null!;
    internal static MelonPreferences_Entry<float> MarkerScale = null!;
    private static KnownCustomerMap? CustomerMap;
    private static MultiplayerCompass? PlayerCompass;

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalUIEnhancements");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable the personal UI additions.");
        DealPayouts = ModSupport.Entry(config, "ShowDealPayouts", true, "Show agreed payments and average price per unit in the left-side tracked deal list.");
        CustomerMarkers = ModSupport.Entry(config, "ShowKnownCustomers", true, "Show portrait markers at unlocked customers' current map locations.");
        MarkerScale = ModSupport.Entry(config, "CustomerMarkerScale", 0.75f, "Known-customer icon scale relative to the native portrait marker (0.35 to 1.5).");
        PlayerCompassMarkers = ModSupport.Entry(config, "ShowMultiplayerPlayers", true, "Show blue compass markers with player names in the native distance-label position.");
        var self = typeof(UIEnhancementsMod);
        // Both native methods rebuild the title independently; neither calls the other.
        ModSupport.Patch(HarmonyInstance, typeof(QuestHUDUI), "UpdateUI", self, postfix: nameof(DealUpdated));
        ModSupport.Patch(HarmonyInstance, typeof(QuestHUDUI), "UpdateMainLabel", self, postfix: nameof(DealUpdated));
        ModSupport.Patch(HarmonyInstance, typeof(MapApp), "SetOpen", self, postfix: nameof(MapOpened));
        ModSupport.Patch(HarmonyInstance, typeof(CompassManager), "Update", self, postfix: nameof(CompassUpdated));
    }

    private static void DealUpdated(QuestHUDUI __instance)
    {
        if (!Enabled.Value || !DealPayouts.Value) return;
        try { DealPayoutLabel.Refresh(__instance); }
        catch (Exception error) { ModSupport.WarnOnce("ui:deal", $"Deal payout label unavailable: {error.Message}"); }
    }

    private static void MapOpened(MapApp __instance, bool open)
    {
        if (!open || !__instance) return;
        try
        {
            if (CustomerMap is null || !CustomerMap.BelongsTo(__instance))
            {
                CustomerMap?.Dispose();
                CustomerMap = new KnownCustomerMap(__instance);
            }
            CustomerMap.Refresh();
        }
        catch (Exception error) { ModSupport.WarnOnce("ui:map-open", $"Known-customer map unavailable: {error.Message}"); }
    }

    public override void OnUpdate()
    {
        try { CustomerMap?.Tick(); }
        catch (Exception error) { ModSupport.WarnOnce("ui:map-tick", $"Known-customer map update failed: {error.Message}"); }
    }

    private static void CompassUpdated(CompassManager __instance)
    {
        if (!__instance) return;
        try
        {
            if (PlayerCompass is null || !PlayerCompass.BelongsTo(__instance))
            {
                PlayerCompass?.Dispose();
                PlayerCompass = new MultiplayerCompass(__instance);
            }
            PlayerCompass.Tick();
        }
        catch (Exception error) { ModSupport.WarnOnce("ui:compass", $"Multiplayer compass markers unavailable: {error.Message}"); }
    }

    private static void Cleanup()
    {
        CustomerMap?.Dispose();
        CustomerMap = null;
        PlayerCompass?.Dispose();
        PlayerCompass = null;
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (sceneName == "Main") Cleanup(); }
    public override void OnApplicationQuit() => Cleanup();
}
