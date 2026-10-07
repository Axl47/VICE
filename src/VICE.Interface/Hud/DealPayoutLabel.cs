using System.Globalization;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using UnityEngine;
using UnityEngine.UI;

namespace VICE.Interface.Hud;

internal static class DealPayoutLabel
{
    internal static void Refresh(QuestHUDUI ui)
    {
        if (!ui || !ui.MainLabel || !ui.Quest) return;
        var contract = ui.Quest.TryCast<Contract>();
        if (contract is null || !contract || !float.IsFinite(contract.Payment) || contract.Payment < 0) return;
        var label = ui.MainLabel;
        var original = label.text;
        if (string.IsNullOrEmpty(original)) return;
        // Insert before the timing/subtitle line, preserving the game's current text.
        var lineEnd = original.IndexOf('\n');
        if (lineEnd < 0) lineEnd = original.Length;
        if (lineEnd > 0 && original[lineEnd - 1] == '\r') lineEnd--;
        var payment = contract.Payment.ToString("#,0.##", CultureInfo.InvariantCulture);
        var value = $"${payment}";
        if (contract.ProductList?.entries is { Count: > 0 })
        {
            var quantity = contract.ProductList.GetTotalQuantity();
            if (quantity > 0)
            {
                var unitPrice = (contract.Payment / (double)quantity).ToString("#,0.0", CultureInfo.InvariantCulture);
                value += $" (${unitPrice}/unit)";
            }
        }
        label.text = original.Insert(lineEnd, $" <size=85%><color=#8FE3A5>· {value}</color></size>");
        label.enableWordWrapping = true;
        label.ForceMeshUpdate();
        // Long names/large payments may wrap; recalculate the row and its shade.
        if (ui.hudUILayout)
        {
            var layout = ui.hudUILayout.GetComponent<RectTransform>();
            if (layout) LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
            if (ui.Shade) ui.UpdateShade();
        }
    }
}
