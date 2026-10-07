using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;

namespace PersonalMods.LSDGameplay;

public sealed class SlotChange
{
    public int Index { get; set; }
    public string Before { get; set; } = "";
    public string Id { get; set; } = "";
    public int Quantity { get; set; }
    public string Packaging { get; set; } = "";
    public int Quality { get; set; } = 2;
}

internal sealed class InventoryTransaction
{
    private readonly ItemSlot[] Slots;
    private readonly ItemInstance?[] Original, Pending;
    private readonly bool Local;

    internal InventoryTransaction(Player player)
    {
        Local = player.IsLocalPlayer;
        Slots = Local ? RegularSlots() : player._inventory.ToArray();
        if (Slots.Length!=RegularSlots().Length) throw new InvalidOperationException("The peer inventory layout does not match the workshop's regular slots.");
        Original = Slots.Select(s => s.ItemInstance?.GetCopy(s.Quantity)).ToArray();
        Pending = Original.Select(i => i?.GetCopy(i.Quantity)).ToArray();
    }
    internal static ItemSlot[] RegularSlots()=>PlayerInventory.Instance.hotbarSlots.ToArray().Select(s=>s.Cast<ItemSlot>()).ToArray();

    internal static string Fingerprint(ItemInstance? item)
    {
        if (item is null || item.Quantity<=0) return "";
        var product=item.TryCast<ProductItemInstance>();
        var quality=item.TryCast<QualityItemInstance>();
        return $"{item.ID}|{item.Quantity}|{quality?.Quality.ToString() ?? ""}|{product?.PackagingID ?? ""}";
    }

    internal IEnumerable<ItemInstance> Items => Pending.Where(i=>i is not null).Select(i=>i!);
    internal int Count(string id, Func<ItemInstance,bool>? matches=null)
        => Items.Where(i=>i.ID==id && (matches is null || matches(i))).Sum(i=>i.Quantity);

    internal void Remove(string id,int quantity,Func<ItemInstance,bool>? matches=null)
    {
        if (quantity<=0 || Pending.Where((item,index)=>!Slots[index].IsRemovalLocked && item is not null && item.ID==id && (matches is null || matches(item))).Sum(item=>item!.Quantity)<quantity)
            throw new InvalidOperationException("Missing or locked supplies: "+id+".");
        for (var i=0;i<Pending.Length && quantity>0;i++)
        {
            var item=Pending[i]; if (Slots[i].IsRemovalLocked || item is null || item.ID!=id || (matches is not null && !matches(item))) continue;
            var take=Math.Min(item.Quantity,quantity); quantity-=take;
            Pending[i]=item.Quantity==take ? null : item.GetCopy(item.Quantity-take);
        }
    }

    internal void Add(ItemInstance item)
    {
        var remaining=item.Quantity;
        for (var pass=0;pass<2;pass++) for (var i=0;i<Pending.Length && remaining>0;i++)
        {
            var current=Pending[i];
            if ((pass==0 && current is null) || (pass==1 && current is not null)) continue;
            if (current is not null && !current.CanStackWith(item,false)) continue;
            if (!Slots[i].DoesItemMatchHardFilters(item) || Slots[i].IsAddLocked) continue;
            var capacity=Math.Max(0,item.Definition.StackLimit-(current?.Quantity ?? 0));
            var add=Math.Min(capacity,remaining); if (add==0) continue;
            Pending[i]=(current ?? item).GetCopy((current?.Quantity ?? 0)+add); remaining-=add;
        }
        if (remaining>0) throw new InvalidOperationException("Inventory is full. Make room and try again; the output stays in the machine.");
    }

    internal List<SlotChange> Commit()
    {
        var changes=new List<SlotChange>();
        for (var i=0;i<Slots.Length;i++)
        {
            if (Fingerprint(Original[i])==Fingerprint(Pending[i])) continue;
            if (Fingerprint(Slots[i].ItemInstance)!=Fingerprint(Original[i])) throw new InvalidOperationException("Inventory changed during the workshop action. Try again.");
            var product=Pending[i]?.TryCast<ProductItemInstance>();
            var quality=Pending[i]?.TryCast<QualityItemInstance>();
            changes.Add(new() { Index=i,Before=Fingerprint(Original[i]),Id=Pending[i]?.ID ?? "",Quantity=Pending[i]?.Quantity ?? 0,
                Packaging=product?.PackagingID ?? "",Quality=quality is null ? 2 : (int)quality.Quality });
        }
        var applied=new List<int>();
        try
        {
            foreach (var change in changes)
            { applied.Add(change.Index); Slots[change.Index].SetStoredItem(Pending[change.Index],!Local); }
        }
        catch
        {
            foreach (var index in applied) Slots[index].SetStoredItem(Original[index],!Local);
            throw;
        }
        return Local ? new() : changes;
    }

    internal static void ApplyRemote(List<SlotChange> changes)
    {
        var slots=RegularSlots();
        foreach (var change in changes)
        {
            if (change.Index<0 || change.Index>=slots.Length || Fingerprint(slots[change.Index].ItemInstance)!=change.Before)
                throw new InvalidOperationException("Workshop inventory changed before the host reply. Close the workshop and reconnect to restore the host's saved inventory.");
        }
        foreach (var change in changes)
            slots[change.Index].SetStoredItem(change.Quantity==0 ? null : Content.Create(change.Id,change.Quantity,change.Packaging,change.Quality),false);
    }
}
