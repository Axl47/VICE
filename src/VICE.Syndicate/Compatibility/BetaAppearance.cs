using Avatar = Il2CppScheduleOne.AvatarFramework.Avatar;
using Il2CppScheduleOne.Core.Avatar;
using UnityEngine;
namespace VICE.Syndicate.Compatibility;
internal static class BetaAppearance
{
    internal static void CopyAppearance(Avatar source, Avatar target)
    {
        var naked = source.Appearance.AppliedNakedAppearance?.Clone();
        if (naked != null) target.Appearance.ApplyNakedAppearance(naked);
        var outfit = new Il2CppSystem.Collections.Generic.List<SerializedAvatarObject>();
        foreach (var item in source.Appearance._wornObjects) if (item) outfit.Add(item.Serialize().Clone());
        target.Appearance.ApplyOutfit(outfit);
    }
    internal static void ConfigureBoss(Avatar avatar)
    {
        var appearance = avatar.Appearance;
        var naked = appearance.AppliedNakedAppearance?.Clone();
        if (naked != null) { naked.Height = 2f; naked.Weight = 1f; appearance.ApplyNakedAppearance(naked); }
        var outfit = new Il2CppSystem.Collections.Generic.List<SerializedAvatarObject>();
        var ids = new HashSet<string>();
        foreach (var item in Resources.FindObjectsOfTypeAll<AvatarObject>())
        {
            var label = item.Name?.Replace(" ", "").ToLowerInvariant() ?? "";
            if (label is "bulletproofvest" or "goldchain" or "polex")
            {
                if (!ids.Add(item.Id)) continue;
                outfit.Add(item.SerializeWithPrimaryColor(label == "bulletproofvest" ? new Color(0.1f, 0.5f, 0.1f) : new Color(0.96f, 0.79f, 0.23f)));
            }
        }
        if (outfit.Count > 0) appearance.ApplyOutfit(outfit);
    }
}
