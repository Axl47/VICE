using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Compass;
using Il2CppScheduleOne.UI.Phone.Map;
using MelonLoader;

[assembly: MelonInfo(typeof(PersonalMods.UIEnhancements.UIEnhancementsMod), "Personal UI Enhancements", "0.2.2", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.UIEnhancements;

public sealed class UIEnhancementsMod : MelonMod
{
    internal static MelonPreferences_Entry<bool> Enabled = null!, DealPayouts = null!, CustomerMarkers = null!, PlayerCompassMarkers = null!, MinimapEnabled = null!;
    internal static MelonPreferences_Entry<float> MarkerScale = null!, MinimapSize = null!, MinimapRange = null!, MinimapIconScale = null!;
    private static KnownCustomerMap? CustomerMap;
    private static MultiplayerCompass? PlayerCompass;
    private static PlayerMinimap? Minimap;
    private static float NextMinimapAttempt;

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalUIEnhancements");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable the personal UI additions.");
        DealPayouts = ModSupport.Entry(config, "ShowDealPayouts", true, "Show agreed payments and average price per unit in the left-side tracked deal list.");
        CustomerMarkers = ModSupport.Entry(config, "ShowKnownCustomers", true, "Show portrait markers at unlocked customers' current map locations.");
        MarkerScale = ModSupport.Entry(config, "CustomerMarkerScale", 0.75f, "Known-customer icon scale relative to the native portrait marker (0.35 to 1.5).");
        PlayerCompassMarkers = ModSupport.Entry(config, "ShowMultiplayerPlayers", true, "Show blue compass markers with player names in the native distance-label position.");
        MinimapEnabled = ModSupport.Entry(config, "ShowMinimap", true, "Show a square, player-centered map in the top-right gameplay HUD.");
        MinimapSize = ModSupport.Entry(config, "MinimapSize", 240f, "Square minimap width/height in HUD units, from 160 to 480.");
        MinimapRange = ModSupport.Entry(config, "MinimapRange", 160f, "World distance across the minimap, from 60 to 500. Smaller values zoom in.");
        MinimapIconScale = ModSupport.Entry(config, "MinimapIconScale", 1.55f, "Minimap symbol size multiplier, from 0.5 to 3. Search-zone footprints keep their native size.");
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
        if (!__instance) return;
        Minimap?.MapVisibilityChanged();
        if (!open) return;
        try
        {
            EnsureCustomerMap(__instance);
            CustomerMap!.Refresh();
        }
        catch (Exception error) { ModSupport.WarnOnce("ui:map-open", $"Known-customer map unavailable: {error.Message}"); }
    }

    public override void OnUpdate()
    {
        // Customer POIs must also exist when only the minimap is visible; the
        // player never needs to open the phone once just to initialize them.
        if (Enabled.Value && CustomerMarkers.Value && PlayerMinimap.Ready)
        {
            try { EnsureCustomerMap(MapApp.Instance); }
            catch (Exception error) { ModSupport.WarnOnce("ui:map-init", $"Known-customer map initialization failed: {error.Message}"); }
        }
        try { CustomerMap?.Tick(); }
        catch (Exception error) { ModSupport.WarnOnce("ui:map-tick", $"Known-customer map update failed: {error.Message}"); }
    }

    private static void EnsureCustomerMap(MapApp map)
    {
        if (CustomerMap is not null && CustomerMap.BelongsTo(map)) return;
        CustomerMap?.Dispose();
        CustomerMap = new KnownCustomerMap(map);
    }

    public override void OnLateUpdate()
    {
        try
        {
            if (!PlayerMinimap.Ready) { Minimap?.Dispose(); Minimap = null; return; }
            var hud = HUD.Instance;
            var map = MapApp.Instance;
            if (Minimap is null || !Minimap.BelongsTo(hud, map))
            {
                Minimap?.Dispose(); Minimap = null;
                if (UnityEngine.Time.realtimeSinceStartup < NextMinimapAttempt) return;
                Minimap = new PlayerMinimap(hud, map);
            }
            Minimap.Tick();
        }
        catch (Exception error)
        {
            Minimap?.Dispose(); Minimap = null;
            NextMinimapAttempt = UnityEngine.Time.realtimeSinceStartup + 2;
            ModSupport.WarnOnce("ui:minimap", $"Player minimap unavailable: {error.Message}");
        }
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
        Minimap?.Dispose();
        Minimap = null;
        NextMinimapAttempt = 0;
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (sceneName == "Main") Cleanup(); }
    public override void OnApplicationQuit() => Cleanup();
    public override void OnDeinitializeMelon() => Cleanup();
}
