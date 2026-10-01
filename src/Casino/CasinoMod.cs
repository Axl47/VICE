using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppScheduleOne.Casino;
using MelonLoader;
using PersonalMods;

[assembly: MelonInfo(typeof(PersonalMods.Casino.CasinoMod), "Personal Casino", "0.1.1", "Axl47")]
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
        // Both controllers use one native function. Patch it once and inspect the
        // actual object's native type rather than trusting the wrapper's CLR type.
        ModSupport.Patch(HarmonyInstance, typeof(BlackjackGameController), "GetBetLimits", self, postfix: nameof(BetLimits));
        ModSupport.Patch(HarmonyInstance, typeof(SlotMachine), "Awake", self, postfix: nameof(SlotsReady));
        ModSupport.Patch(HarmonyInstance, typeof(SlotMachine), "HandleInteracted", self, prefix: nameof(SlotsReady));
        // Bet indices retain their meaning and count on all peers; every peer needs the same settings.
        MelonLogger.Msg("Casino limits configured. Multiplayer peers must install matching settings.");
    }

    private static void BetLimits(Il2CppObjectBase __instance, ref float minimum, ref float maximum)
    {
        if (!Enabled.Value || __instance is null) return;
        var configured = __instance.TryCast<RTBGameController>() is not null ? RideTheBusMaximum.Value : BlackjackMaximum.Value;
        maximum = Math.Max(minimum, Math.Clamp(configured, 1, 1_000_000));
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
