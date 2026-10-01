using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Casino;
using MelonLoader;
using PersonalMods;

[assembly: MelonInfo(typeof(PersonalMods.Casino.CasinoMod), "Personal Casino", "0.1.0", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.Casino;

public sealed class CasinoMod : MelonMod
{
    private static MelonPreferences_Entry<bool> Enabled = null!;
    private static MelonPreferences_Entry<int> BlackjackMaximum = null!, RideTheBusMaximum = null!, SlotMultiplier = null!;
    private static int[]? OriginalSlotBets;

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalCasino");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Enable higher casino bets.");
        BlackjackMaximum = ModSupport.Entry(config, "BlackjackMaximumBet", 100_000, "Maximum blackjack wager in dollars (1 to 1,000,000).");
        RideTheBusMaximum = ModSupport.Entry(config, "RideTheBusMaximumBet", 100_000, "Maximum Ride the Bus wager in dollars (1 to 1,000,000).");
        SlotMultiplier = ModSupport.Entry(config, "SlotBetMultiplier", 100, "Multiply the existing slot-machine wager ladder (1 to 1000; each wager capped at 1,000,000).");
        var self = typeof(CasinoMod);
        ModSupport.Patch(HarmonyInstance, typeof(BlackjackGameController), "GetBetLimits", self, postfix: nameof(BlackjackLimits));
        ModSupport.Patch(HarmonyInstance, typeof(RTBGameController), "GetBetLimits", self, postfix: nameof(RideTheBusLimits));
        ModSupport.Patch(HarmonyInstance, typeof(SlotMachine), "Awake", self, postfix: nameof(SlotsReady));
        ModSupport.Patch(HarmonyInstance, typeof(SlotMachine), "HandleInteracted", self, prefix: nameof(SlotsReady));
        // Bet indices retain their meaning and count on all peers; every peer needs the same settings.
        MelonLogger.Msg("Casino limits configured. Multiplayer peers must install matching settings.");
    }

    private static int Limit(int minimum, int configured) => Math.Max(minimum, Math.Clamp(configured, 1, 1_000_000));

    private static void BlackjackLimits(BlackjackGameController __instance, ref float minimum, ref float maximum)
    {
        if (!Enabled.Value) return;
        maximum = Math.Max(minimum, Limit(BlackjackGameController.MinimumBet, BlackjackMaximum.Value));
    }

    private static void RideTheBusLimits(RTBGameController __instance, ref float minimum, ref float maximum)
    {
        if (!Enabled.Value) return;
        maximum = Math.Max(minimum, Limit(RTBGameController.MinimumBet, RideTheBusMaximum.Value));
    }

    private static void SlotsReady(SlotMachine __instance)
    {
        if (!__instance || SlotMachine.BetAmounts is null) return;
        OriginalSlotBets ??= SlotMachine.BetAmounts.ToArray();
        var factor = Math.Clamp(SlotMultiplier.Value, 1, 1000);
        var amounts = Enabled.Value ? OriginalSlotBets.Select(x => (int)Math.Clamp((long)x * factor, 1, 1_000_000)).ToArray() : OriginalSlotBets;
        SlotMachine.BetAmounts = new Il2CppStructArray<int>(amounts);
        if (__instance.BetAmountLabel) __instance.BetAmountLabel.text = $"${__instance.currentBetAmount:N0}";
    }

}
