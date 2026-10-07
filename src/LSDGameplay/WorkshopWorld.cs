using HarmonyLib;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Tiles;
using Il2CppScheduleOne.UI.Shop;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using S1API.Items;
using S1API.Shops;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;
using NativeItem=Il2CppScheduleOne.ItemFramework.ItemInstance;
using Grid=Il2CppScheduleOne.Tiles.Grid;
using ShopManager=S1API.Shops.ShopManager;

namespace PersonalMods.LSDGameplay;

internal sealed class WorkshopWorld : IDisposable
{
    internal Property? Office;
    private ShopInterface? CollectionShop;
    private GameObject? Sticker;
    private InteractableObject? StickerInteraction;
    private MSGConversation? Conversation;
    private readonly Dictionary<string,BuildableItem> Machines=new();
    private readonly Dictionary<IntPtr,(Vector3 Position,Quaternion Rotation)> PartOrigins=new();
    private readonly Dictionary<int,Material> Lamps=new();
    private float NextScan;
    internal string CollectionName=>CollectionShop ? CollectionShop!.ShopName+" delivery pallet" : "Hardware store delivery pallet unavailable";

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        var self=typeof(WorkshopWorld);
        ModSupport.Patch(harmony,typeof(InteractableObject),"StartInteract",self,prefix:nameof(Interact));
        ModSupport.Patch(harmony,typeof(BuildUpdate_Grid),"CheckIntersections",self,postfix:nameof(CheckPlacement));
        ModSupport.Patch(harmony,typeof(BuildManager),"CreateGridItem",self,prefix:nameof(CreateGridItem));
        ModSupport.Patch(harmony,typeof(BuildableItem),"CanBePickedUp",self,prefix:nameof(CanRemove));
        ModSupport.Patch(harmony,typeof(BuildableItem),"CanBeDestroyed",self,prefix:nameof(CanRemove));
        ModSupport.Patch(harmony,typeof(BuildableItem),"Destroy_Server",self,prefix:nameof(Destroy));
    }

    internal void Load()
    {
        Office=Property.Properties.ToArray().FirstOrDefault(p=>p && (p.TryCast<SewerOffice>() is not null || p.PropertyName.Equals("Sewer Office",StringComparison.OrdinalIgnoreCase)));
        var shops=ShopInterface.AllShops.ToArray();
        foreach (var shop in shops) MelonLogger.Msg($"LSD_SHOP code={shop.ShopCode}, name={shop.ShopName}, bays={shop.DeliveryBays?.Length ?? 0}.");
        var warehouse=shops.FirstOrDefault(s=>new[] { "warehouse","oscar","darkmarket","warehouseinterface","oscarshop","oscarinterface","oscarsstore" }
            .Any(key=>Key(s.ShopCode)==key || Key(s.ShopName)==key || Key(s.gameObject.name)==key));
        var hardware=shops.FirstOrDefault(s=>s.ShopCode=="dans_hardware")
            ?? shops.FirstOrDefault(s=>Key(s.ShopCode).Contains("hardware") || Key(s.ShopName).Contains("hardware"));
        CollectionShop=hardware && hardware!.DeliveryBays is { Length:>0 } ? hardware : null;
        if (warehouse)
            foreach (var id in new[] { WorkshopRules.Paper,WorkshopRules.Sleeve,WorkshopRules.Bench,WorkshopRules.Cabinet })
                ShopManager.AddToShops(ItemManager.GetDefinition(id)!,warehouse!.ShopName);
        foreach (var supplyShop in shops.Where(s=>Key(s.ShopCode).Contains("hardware") || Key(s.ShopName).Contains("hardware")))
            ShopManager.AddToShops(ItemManager.GetDefinition(WorkshopRules.Ink)!,supplyShop.ShopName);
        if (!warehouse || !hardware || !CollectionShop)
            throw new InvalidOperationException("The warehouse, hardware supply shop or hardware pallet could not be verified. See LSD_SHOP diagnostics.");
        var info=new MessageContactInfo("Off Register",WorkshopRules.Owner+":collective",StudioAssets.Symbol,true,false);
        Conversation=new MSGConversation(info,WorkshopRules.Owner+":collective");
        Conversation.SetIsKnown(LSDGameplayMod.Current!.State.Introduced);
        if (LSDGameplayMod.Current.State.Introduced) { Conversation.EnsureUIExists();Conversation.SetEntryVisibility(true); }
        if (LSDGameplayMod.Current.State.Introduced)
        {
            Notify("Off Register. Paper from the warehouse, cartridges from the hardware store. Your edition. Your rules.",false);
            foreach (var order in LSDGameplayMod.Current.State.Orders.TakeLast(3))
                Notify($"Order {order.Id[..8]} · {order.Quantity} Prism Base · {order.Stage} · {CollectionName}",false);
        }
        Scan();
    }

    private static string Key(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    internal BuildableItem? FindMachine(string id)=>Machines.TryGetValue(id,out var item) && item ? item : null;

    internal void Tick()
    {
        if (Time.realtimeSinceStartup<NextScan) return;
        NextScan=Time.realtimeSinceStartup+2;Scan();
    }

    private void Scan()
    {
        if (Office is null || !Office || !Office.IsOwned) return;
        if (!Sticker)
        {
            var anchor=Office.InteriorSpawnPoint ? Office.InteriorSpawnPoint : Office.SpawnPoint;
            if (!anchor) throw new InvalidOperationException("Sewer Office entrance anchor is unavailable.");
            Sticker=GameObject.CreatePrimitive(PrimitiveType.Cube);Sticker.name="Off Register sticker";
            var origin=anchor.position+Vector3.up*1.45f;
            var mounted=false;
            foreach (var direction in new[] { anchor.right,-anchor.right,anchor.forward,-anchor.forward })
                if (Physics.Raycast(origin,direction,out var hit,3,-1,QueryTriggerInteraction.Ignore)
                    && Math.Abs(hit.normal.y)<0.3f && !hit.collider.GetComponentInParent<BuildableItem>())
                {
                    Sticker.transform.position=hit.point+hit.normal*0.018f;
                    Sticker.transform.rotation=Quaternion.LookRotation(-hit.normal,Vector3.up);mounted=true;break;
                }
            if (!mounted)
            { Sticker.transform.position=origin+anchor.right*1.1f;Sticker.transform.rotation=anchor.rotation; }
            Sticker.transform.localScale=new(0.32f,0.32f,0.012f);
            var material=new Material(StudioAssets.Model("Parcel").GetComponentInChildren<Renderer>().sharedMaterial);
            material.mainTexture=StudioAssets.Texture(2);material.color=Color.white;
            Sticker.GetComponent<Renderer>().sharedMaterial=material;
            StickerInteraction=AddInteraction(Sticker,"Meet Off Register");
        }
        Machines.Clear();
        foreach (var item in Office.BuildableItems)
        {
            if (!item || !item.Initialized || item.isGhost || item.IsDestroyed || item.ItemInstance is null || !Equipment(item.ItemInstance.ID)) continue;
            var id=item.GUID.ToString();Machines[id]=item;
            var interactables=item.GetComponentsInChildren<InteractableObject>(true);
            if (interactables.Length==0) AddInteraction(item.gameObject,item.ItemInstance.ID==WorkshopRules.Bench ? "Use Blotter Bench" : "Open drying cabinet");
            else foreach (var interactable in interactables) interactable.SetMessage(item.ItemInstance.ID==WorkshopRules.Bench ? "Use Blotter Bench" : "Open drying cabinet");
        }
        RefreshMachines(LSDGameplayMod.Current!.State);
    }

    private static InteractableObject AddInteraction(GameObject root,string message)
    {
        var interactable=root.AddComponent<InteractableObject>();
        interactable.onHovered=new UnityEvent();interactable.onInteractStart=new UnityEvent();interactable.onInteractEnd=new UnityEvent();
        interactable.MaxInteractionRange=2.5f;interactable.RequiresUniqueClick=true;
        interactable.displayLocationCollider=root.GetComponent<Collider>();
        interactable.SetMessage(message);return interactable;
    }

    internal void RefreshMachines(WorkshopState state)
    {
        foreach (var pair in Machines)
        {
            var item=pair.Value;if (!item) continue;
            var machine=state.Machines.FirstOrDefault(m=>m.Id==pair.Key);
            var show=machine is not null && machine.Stage is not MachineStage.Empty;
            foreach (var transform in item.GetComponentsInChildren<Transform>(true))
                if (transform.name is "Paper" or "PrintedSheet" || transform.name.StartsWith("ArtworkSurface",StringComparison.Ordinal))
                    transform.gameObject.SetActive(show && (!transform.name.StartsWith("ArtworkSurface",StringComparison.Ordinal) || machine!.Stage!=MachineStage.Preparing));
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="StatusLamp"))
            {
                var lamp=machine?.Stage==MachineStage.Drying ? 1 : machine?.Stage==MachineStage.DryOutput ? 2 : 0;
                if (!Lamps.TryGetValue(lamp,out var material))
                { material=new Material(renderer.sharedMaterial);material.color=lamp switch { 1=>new(1,0.61f,0.12f),2=>new(0.22f,0.85f,0.52f),_=>new(0.16f,0.22f,0.24f) };Lamps[lamp]=material; }
                renderer.sharedMaterial=material;
            }
            if (machine is not null) StudioAssets.ApplyArtwork(item.gameObject,machine.ArtSlot);
        }
    }

    internal void Introduce()
    {
        Conversation?.SetIsKnown(true);Conversation?.EnsureUIExists();Conversation?.SetEntryVisibility(true);
        Notify("Saw our sticker? Good. Order Prism Base through Off Register on your phone. Pick up at "+CollectionName+". Get a bench, cabinet and paper from the warehouse. Cartridges from the hardware store.");
    }
    internal void Notify(string message,bool alert=true)
    {
        try { Conversation?.EnsureUIExists();Conversation?.SendMessage(new Message(message,(Message.ESenderType)1,true,UnityEngine.Random.Range(1,int.MaxValue)),alert,false); }
        catch (Exception error) { ModSupport.WarnOnce("lsd:message", "Collective notification unavailable: "+error); }
    }

    internal CollectiveOrder ReserveOrder(WorkshopState state,int quantity)
    {
        if (CollectionShop is null || !CollectionShop || CollectionShop.DeliveryBays is not { Length:>0 }) throw new InvalidOperationException("The collection pallet is unavailable.");
        var bays=CollectionShop.DeliveryBays;
        for (var bay=0;bay<bays.Length;bay++)
        {
            var storage=bays[bay];if (!storage) continue;
            var pending=state.Orders.Where(o=>o.Shop==CollectionShop.ShopCode && o.Bay==bay && o.Stage!=OrderStage.Delivered).Sum(o=>o.Quantity);
            if (storage.HowManyCanFit(Content.Create(WorkshopRules.Prism,1))-pending<quantity) continue;
            if (state.Orders.Count>=128) throw new InvalidOperationException("The order ledger is full.");
            return new() { Id=Guid.NewGuid().ToString(),Quantity=quantity,Shop=CollectionShop.ShopCode,Bay=bay,DueMinute=LSDGameplayMod.Minute+WorkshopRules.DeliveryMinutes };
        }
        throw new InvalidOperationException("The pallet has no unreserved space. Collect existing deliveries before ordering again.");
    }

    internal bool DeliverOrders(WorkshopState state)
    {
        var changed=false;
        foreach (var order in state.Orders)
        {
            if (order.Stage==OrderStage.Delivered || LSDGameplayMod.Minute<order.DueMinute) continue;
            var shop=ShopInterface.AllShops.ToArray().FirstOrDefault(s=>s.ShopCode==order.Shop);
            var storage=shop && shop!.DeliveryBays is not null && order.Bay<shop.DeliveryBays.Length ? shop.DeliveryBays[order.Bay] : null;
            var parcel=Content.Create(WorkshopRules.Prism,order.Quantity);
            if (storage is null || !storage || !storage.CanItemFit(parcel,order.Quantity))
            {
                if (order.Stage!=OrderStage.WaitingForSpace)
                { order.Stage=OrderStage.WaitingForSpace;changed=true;Notify("Your parcel is ready, but the collection pallet is full. Clear some space; we will hold it for you."); }
                continue;
            }
            var originals=storage.ItemSlots.ToArray().Select(s=>s.ItemInstance?.GetCopy(s.Quantity)).ToArray();
            var countBefore=originals.Where(i=>i?.ID==WorkshopRules.Prism).Sum(i=>i!.Quantity);
            try
            {
                storage.InsertItem(parcel,true);
                var after=storage.ItemSlots.ToArray().Where(s=>s.ItemInstance?.ID==WorkshopRules.Prism).Sum(s=>s.Quantity);
                if (after!=countBefore+order.Quantity) throw new InvalidOperationException("Pallet insertion did not preserve the expected parcel quantity.");
                order.Stage=OrderStage.Delivered;changed=true;
                Notify($"Order {order.Id[..8]} is at {CollectionName}. {order.Quantity} Prism Base parcel(s). No pickup deadline.");
            }
            catch
            {
                for (var i=0;i<originals.Length;i++) storage.ItemSlots[i].SetStoredItem(originals[i],false);
                throw;
            }
        }
        if (changed) state.Revision++;return changed;
    }

    internal static bool Equipment(string id)=>id is WorkshopRules.Bench or WorkshopRules.Cabinet;
    internal void Animate(BuildableItem? item,string task,float progress,float x,float y)
    {
        if (item is null || !item) return;
        foreach (var part in item.GetComponentsInChildren<Transform>(true))
        {
            if (part.name is not ("Roller" or "RollerHandle" or "CutterArm" or "CutterGrip")) continue;
            if (!PartOrigins.TryGetValue(part.Pointer,out var origin))
            { origin=(part.localPosition,part.localRotation);PartOrigins[part.Pointer]=origin; }
            if (task=="print" && part.name.StartsWith("Roller",StringComparison.Ordinal))
                part.localPosition=origin.Position+new Vector3((x-0.5f)*0.02f,0,-progress*0.36f);
            if (task=="finishcut" && part.name.StartsWith("Cutter",StringComparison.Ordinal))
                part.localRotation=origin.Rotation*Quaternion.Euler(progress*22,0,0);
        }
    }
    private static bool Interact(InteractableObject __instance)
    {
        var current=LSDGameplayMod.Current;
        if (current?.Ready!=true) return true;
        if (current.World?.StickerInteraction?.Pointer==__instance.Pointer)
        { current.Panel?.ShowIntroduction();return false; }
        var item=__instance.GetComponentInParent<BuildableItem>();
        if (!item || item.ItemInstance is null || !Equipment(item.ItemInstance.ID)) return true;
        current.Panel?.OpenMachine(item);return false;
    }
    private static void CheckPlacement(BuildUpdate_Grid __instance)
    {
        if (__instance.ItemInstance is null || !Equipment(__instance.ItemInstance.ID)) return;
        var grid=__instance.GetHoveredGrid();
        if (!grid || !grid.ParentProperty || grid.ParentProperty!=LSDGameplayMod.Current?.World?.Office || !grid.ParentProperty.IsOwned)
            __instance._validPosition=false;
    }
    private static bool CreateGridItem(NativeItem item,Grid grid,ref GridItem __result)
    {
        if (item is null || !Equipment(item.ID)) return true;
        if (grid && grid.ParentProperty && grid.ParentProperty.TryCast<SewerOffice>() is not null && grid.ParentProperty.IsOwned) return true;
        __result=null!;LSDGameplayMod.Current?.ShowMessage("Place this equipment inside the owned Sewer Office.");return false;
    }
    private static bool Active(BuildableItem item)=>item && item.ItemInstance is not null && Equipment(item.ItemInstance.ID)
        && LSDGameplayMod.Current?.State.Machines.Any(m=>m.Id==item.GUID.ToString() && m.Stage!=MachineStage.Empty)==true;
    private static bool CanRemove(BuildableItem __instance,ref string reason,ref bool __result)
    {
        if (!Active(__instance)) return true;reason="Collect or finish this machine's batch before moving it.";__result=false;return false;
    }
    private static bool Destroy(BuildableItem __instance)
    {
        if (Active(__instance)) return false;
        if (__instance && __instance.ItemInstance is not null && Equipment(__instance.ItemInstance.ID) && WorkshopNetwork.Host && LSDGameplayMod.Current is { } mod)
        { if (mod.State.Machines.RemoveAll(m=>m.Id==__instance.GUID.ToString())>0) { mod.State.Revision++;WorkshopNetwork.Publish(); } }
        return true;
    }
    public void Dispose() { if (Sticker) Object.Destroy(Sticker);foreach (var material in Lamps.Values) Object.Destroy(material);Lamps.Clear();Machines.Clear();Office=null;Conversation=null; }
}
