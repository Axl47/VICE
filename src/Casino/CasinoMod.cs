using Il2CppScheduleOne.Casino;
using Il2CppScheduleOne.Casino.UI;
using MelonLoader;
using PersonalMods;

[assembly: MelonInfo(typeof(PersonalMods.Casino.CasinoMod), "Personal Casino", "0.1.2", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.Casino;

public sealed class CasinoMod : MelonMod
{
    private static MelonPreferences_Entry<bool> Enabled = null!;
    private static MelonPreferences_Entry<int> BlackjackMaximum = null!, RideTheBusMaximum = null!;

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalCasino");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable higher blackjack and Ride the Bus bets.");
        BlackjackMaximum = ModSupport.Entry(config, "BlackjackMaximumBet", 100_000, "Maximum blackjack wager in dollars (1 to 1,000,000).");
        RideTheBusMaximum = ModSupport.Entry(config, "RideTheBusMaximumBet", 100_000, "Maximum Ride the Bus wager in dollars (1 to 1,000,000).");
        var self = typeof(CasinoMod);
        // GetBetLimits shares a body across unrelated controller wrapper types.
        // Change the actual dollar-valued slider after native panel setup instead.
        ModSupport.Patch(HarmonyInstance, typeof(CasinoGameBetPanel), "Open", self, postfix: nameof(PanelOpened));
        MelonLogger.Msg("Blackjack and Ride the Bus bet sliders configured.");
    }

    private static void PanelOpened(CasinoGameBetPanel __instance, CasinoGameController game)
    {
        if (!Enabled.Value || !__instance || !game) return;
        try
        {
            int configured;
            if (game.TryCast<BlackjackGameController>() is not null) configured = BlackjackMaximum.Value;
            else if (game.TryCast<RTBGameController>() is not null) configured = RideTheBusMaximum.Value;
            else return;
            var slider = __instance._betSlider;
            if (!slider) return;
            slider.maxValue = Math.Max(slider.minValue, Math.Clamp(configured, 1, 1_000_000));
            // Native selection already passes dollar amounts to SetLocalPlayerBet,
            // refreshes the label, and checks funds before allowing readiness.
            __instance.RefreshDisplayedBet();
        }
        catch (Exception error) { ModSupport.WarnOnce("casino:slider", $"Casino bet slider could not be configured: {error.Message}"); }
    }
}
