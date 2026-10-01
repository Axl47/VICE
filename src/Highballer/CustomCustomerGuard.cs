using System.Collections;
using System.Reflection;
using Il2CppScheduleOne.NPCs;

namespace PersonalMods.Highballer;

internal static class CustomCustomerGuard
{
    private static Type? ApiNpc;

    internal static bool IsCustom(NPC npc)
    {
        if (!npc || npc.TryCast<SpecialCustomer>() is not null) return true;
        ApiNpc ??= AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("S1API.Entities.NPC")).FirstOrDefault(t => t is not null);
        if (ApiNpc is null) return false;
        try
        {
            const BindingFlags fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var all = ApiNpc.GetField("All", fields)?.GetValue(null) as IEnumerable;
            var native = ApiNpc.GetField("S1NPC", fields);
            var custom = ApiNpc.GetField("IsCustomNPC", fields);
            if (all is null || native is null || custom is null)
            {
                ModSupport.WarnOnce("highballer:api-shape", "S1API custom NPC detection has changed; skipping customer automation until compatibility is verified.");
                return true;
            }
            foreach (var entry in all)
            {
                if (entry is null || native.GetValue(entry) is not NPC candidate || candidate.Pointer != npc.Pointer) continue;
                var flag = custom.GetValue(entry);
                return flag is bool enabled ? enabled : flag is not null && Convert.ToInt32(flag) != 0;
            }
            return false;
        }
        catch (Exception error)
        {
            ModSupport.WarnOnce("highballer:custom", $"Unable to classify S1API customer; skipping automation: {error.Message}");
            return true;
        }
    }
}
