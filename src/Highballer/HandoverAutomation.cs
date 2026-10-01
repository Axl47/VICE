using System.Collections;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Handover;
using MelonLoader;
using PersonalMods.Algorithms;
using UnityEngine;

namespace PersonalMods.Highballer;

internal static class HandoverAutomation
{
    private static int Generation;
    private sealed record Transfer(ItemSlot Source, ItemSlot Destination, ItemInstance Original, ItemInstance Delivery, int Count);

    internal static void Initialize(HarmonyLib.Harmony harmony)
        => ModSupport.Patch(harmony, typeof(HandoverScreen), "Open_Contract", typeof(HandoverAutomation), postfix: nameof(Opened));

    internal static void Reset() => Generation++;

    private static void Opened(HandoverScreen __instance, Contract contract)
    {
        if (HighballerMod.Enabled.Value && HighballerMod.AutoHandover.Value && __instance && contract)
            MelonCoroutines.Start(FillNextFrame(__instance, contract, Generation));
    }

    private static IEnumerator FillNextFrame(HandoverScreen screen, Contract contract, int generation)
    {
        yield return null;
        if (generation != Generation || !screen || !screen.IsOpen || HighballerMod.IsBusy || !HighballerMod.AutoHandover.Value) yield break;
        try { Fill(screen, contract); }
        catch (Exception error) { ModSupport.WarnOnce("highballer:handover", $"Automatic handover unavailable; continue using the native screen: {error.Message}"); }
    }

    private static void Fill(HandoverScreen screen, Contract contract)
    {
        var mode = screen._contractMode;
        if (!mode || mode.CurrentContract?.Pointer != contract.Pointer || !mode.CurrentCustomer || !HighballerMod.CanHandle(mode.CurrentCustomer) || !PlayerInventory.Instance) return;
        var inventory = PlayerInventory.Instance.GetAllInventorySlots();
        if (inventory is null) return;
        var expectations = contract.ProductList?.entries;
        if (expectations is null || expectations.Count == 0 || screen._customerSlots is null) return;
        // Avoid mixing auto-filled items with a handover the player already started manually.
        foreach (var slot in screen._customerSlots) if (slot.ItemInstance is not null) return;
        var minimumQuality = StandardsMethod.GetCorrespondingQuality(mode.CurrentCustomer.CustomerData.Standards);
        var reserved = new Dictionary<IntPtr, int>();
        var selected = new List<(ItemSlot Slot, int Count)>();
        foreach (var expected in expectations)
        {
            var candidates = new List<ItemSlot>();
            foreach (var slot in inventory)
            {
                var product = slot.ItemInstance?.TryCast<ProductItemInstance>();
                if (slot.IsRemovalLocked || product is null || product.Definition.ID != expected.ProductID || !product.AppliedPackaging || product.Quality < minimumQuality) continue;
                candidates.Add(slot);
            }
            candidates.Sort((a, b) => a.ItemInstance.Cast<ProductItemInstance>().Quality.CompareTo(b.ItemInstance.Cast<ProductItemInstance>().Quality));
            var stock = candidates.Select(slot => new PackageStock(slot.ItemInstance.Cast<ProductItemInstance>().AppliedPackaging.Quantity,
                slot.Quantity - reserved.GetValueOrDefault(slot.Pointer))).ToArray();
            var plan = PackagePlanner.Exact(stock, expected.Quantity);
            if (plan is null) return; // Exact packages unavailable; no inventory changes.
            for (var i = 0; i < plan.Length; i++)
            {
                if (plan[i] <= 0) continue;
                reserved[candidates[i].Pointer] = reserved.GetValueOrDefault(candidates[i].Pointer) + plan[i];
            }
        }
        foreach (var slot in inventory)
            if (reserved.TryGetValue(slot.Pointer, out var count) && count > 0) selected.Add((slot, count));
        if (selected.Count == 0 || selected.Count > screen._customerSlots.Length) return;
        var transfers = new List<Transfer>();
        for (var index = 0; index < selected.Count; index++)
        {
            var (source, count) = selected[index];
            var destination = screen._customerSlots[index];
            var delivery = source.ItemInstance.GetCopy(count);
            if (destination.IsAddLocked || destination.GetCapacityForItem(delivery, false) < count) return;
            transfers.Add(new Transfer(source, destination, source.ItemInstance.GetCopy(source.Quantity), delivery, count));
        }
        var attempted = new List<Transfer>();
        try
        {
            foreach (var transfer in transfers)
            {
                attempted.Add(transfer); // Journal before native calls so partial failures can be restored.
                transfer.Source.ChangeQuantity(-transfer.Count, false);
                transfer.Destination.SetStoredItem(transfer.Delivery, false);
            }
            screen.HandoverItemsChanged();
        }
        catch
        {
            foreach (var transfer in attempted.AsEnumerable().Reverse())
            {
                transfer.Destination.ClearStoredInstance(false);
                transfer.Source.SetStoredItem(transfer.Original, false);
            }
            throw;
        }
        // Use the game's own validation and callback. A failed validation leaves the filled screen available.
        if (!mode.GetError(out _) && mode.GetSuccessChance() >= 1f) screen.DoneButtonPressed();
    }
}
