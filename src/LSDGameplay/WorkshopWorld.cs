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
    private readonly Dictionary<string,CabinetView> Cabinets=new();
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
        ModSupport.Patch(harmony,typeof(Il2CppScheduleOne.ObjectScripts.PackagingStation),"StartTask",self,prefix:nameof(SleeveStaging));
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
            foreach (var id in new[] { WorkshopRules.Paper,WorkshopRules.Bench,WorkshopRules.Cabinet })
                ShopManager.AddToShops(ItemManager.GetDefinition(id)!,warehouse!.ShopName);
        if(warehouse && warehouse!.GetListing(WorkshopRules.Sleeve) is null){
            var sleeve=new ShopListing{Item=Il2CppScheduleOne.Registry.GetItem(WorkshopRules.Sleeve).Cast<Il2CppScheduleOne.ItemFramework.StorableItemDefinition>(),name="Empty edition sleeve"};
            warehouse.Listings.Add(sleeve);sleeve.Initialize(warehouse);warehouse.CreateListingUI(sleeve);
        }
        foreach (var supplyShop in shops.Where(s=>Key(s.ShopCode).Contains("hardware") || Key(s.ShopName).Contains("hardware")))
            ShopManager.AddToShops(ItemManager.GetDefinition(WorkshopRules.Ink)!,supplyShop.ShopName);
        if (!warehouse || !hardware || !CollectionShop)
            throw new InvalidOperationException("The warehouse, hardware supply shop or hardware pallet could not be verified. See LSD_SHOP diagnostics.");
        var info=new MessageContactInfo("Off Register",WorkshopRules.Owner+":collective",StudioAssets.Symbol,true,false);
        Conversation=new MSGConversation(info,WorkshopRules.Owner+":collective");
        Conversation.SetIsKnown(WorkshopFeature.Current!.State.Introduced);
        if (WorkshopFeature.Current.State.Introduced) { Conversation.EnsureUIExists();Conversation.SetEntryVisibility(true); }
        if (WorkshopFeature.Current.State.Introduced)
        {
            Notify("Off Register. Paper from the warehouse, cartridges from the hardware store. Your edition. Your rules.",false);
            foreach (var order in WorkshopFeature.Current.State.Orders.TakeLast(3))
                Notify($"{order.Quantity} Prism Base parcel(s) · {order.Stage} · {CollectionName}",false);
        }
        Scan();
    }

    private static string Key(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static void SleeveStaging(Il2CppScheduleOne.ObjectScripts.PackagingStation __instance)
    {
        if(__instance.PackagingSlot?.ItemInstance?.ID!=WorkshopRules.Sleeve
            || __instance.ProductSlot?.ItemInstance?.ID.StartsWith(WorkshopRules.Owner+":tab-",StringComparison.Ordinal)!=true)return;
        var native=__instance.ActiveProductAlignments?.ToArray();
        if(native is null || native.Length==0 || native.Length>=20)return;
        var points=new Transform[20];Array.Copy(native,points,native.Length);
        for(var i=native.Length;i<points.Length;i++){
            var source=native[i%native.Length];var point=new GameObject("Studio sleeve staging "+i);
            point.transform.SetParent(__instance.Container,false);
            point.transform.SetPositionAndRotation(source.position+__instance.transform.forward*(0.075f*(i/native.Length)),source.rotation);
            points[i]=point.transform;
        }
        __instance.ActiveProductAlignments=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Transform>(points);
    }
    internal BuildableItem? FindMachine(string id)=>Machines.TryGetValue(id,out var item) && item ? item : null;
    internal CabinetView Cabinet(BuildableItem item)
    {
        var id=item.GUID.ToString();
        if(!Cabinets.TryGetValue(id,out var view)){view=new CabinetView(item);Cabinets[id]=view;}
        return view;
    }

    internal void Tick()
    {
        if (Time.realtimeSinceStartup<NextScan) return;
        NextScan=Time.realtimeSinceStartup+2;Scan();
    }

    private void Scan()
    {
        if (Office is null || !Office || !Office.IsOwned) return;
        if (WorkshopFeature.Current!.State.Introduced)
        { if (Sticker) Object.Destroy(Sticker);Sticker=null;StickerInteraction=null; }
        if (!Sticker && !WorkshopFeature.Current.State.Introduced)
        {
            var anchor=Office.InteriorSpawnPoint ? Office.InteriorSpawnPoint : Office.SpawnPoint;
            if (!anchor) throw new InvalidOperationException("Sewer Office entrance anchor is unavailable.");
            Sticker=GameObject.CreatePrimitive(PrimitiveType.Cube);Sticker.name="Off Register sticker";
            var origin=anchor.position+anchor.forward*0.65f+Vector3.up*1.4f;
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
            StickerInteraction=AddInteraction(Sticker,"Off Register");
            var focus=new GameObject("Sticker centre");focus.transform.SetParent(Sticker.transform,false);
            StickerInteraction.displayLocationPoint=focus.transform;
        }
        Machines.Clear();
        foreach (var item in Office.BuildableItems)
        {
            if (!item || !item.Initialized || item.isGhost || item.IsDestroyed || item.ItemInstance is null || !Equipment(item.ItemInstance.ID)) continue;
            PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(item);
            var id=item.GUID.ToString();Machines[id]=item;
            var interactables=item.GetComponentsInChildren<InteractableObject>(true);
            if (interactables.Length==0) AddInteraction(item.gameObject,item.ItemInstance.ID==WorkshopRules.Bench ? "Use Blotter Bench" : "Open drying cabinet");
            else foreach (var interactable in interactables) interactable.SetMessage(item.ItemInstance.ID==WorkshopRules.Bench ? "Use Blotter Bench" : "Open drying cabinet");
        }
        foreach(var id in Cabinets.Keys.Where(id=>!Machines.ContainsKey(id)).ToArray()){Cabinets[id].Dispose();Cabinets.Remove(id);}
        RefreshMachines(WorkshopFeature.Current!.State);
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
            if(item.ItemInstance.ID==WorkshopRules.Cabinet)Cabinet(item).Refresh(machine);
            else {
                foreach (var transform in item.GetComponentsInChildren<Transform>(true))
                    if (transform.name is "Paper" or "PrintedSheet" || transform.name.StartsWith("ArtworkSurface",StringComparison.Ordinal))
                        transform.gameObject.SetActive(show && (!transform.name.StartsWith("ArtworkSurface",StringComparison.Ordinal) || machine!.Stage!=MachineStage.Preparing));
                if (machine is not null) StudioAssets.ApplyArtwork(item.gameObject,machine.ArtSlot,sheet:true);
            }
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="StatusLamp"))
            {
                var lamp=machine?.Stage==MachineStage.Drying ? 1 : machine?.Stage==MachineStage.DryOutput ? 2 : 0;
                if (!Lamps.TryGetValue(lamp,out var material))
                { material=new Material(renderer.sharedMaterial);material.color=lamp switch { 1=>new(1,0.61f,0.12f),2=>new(0.22f,0.85f,0.52f),_=>new(0.16f,0.22f,0.24f) };Lamps[lamp]=material; }
                renderer.sharedMaterial=material;
            }
        }
    }

    internal void Introduce()
    {
        Conversation?.SetIsKnown(true);Conversation?.EnsureUIExists();Conversation?.SetEntryVisibility(true);
        Notify("Saw our sticker? Good. Order Prism Base through Off Register on your phone. Pick up at "+CollectionName+". Get a bench, cabinet and paper from the warehouse. Cartridges from the hardware store.");
        if (Sticker) Object.Destroy(Sticker);Sticker=null;StickerInteraction=null;
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
            return new() { Id=Guid.NewGuid().ToString(),Quantity=quantity,Shop=CollectionShop.ShopCode,Bay=bay,DueMinute=WorkshopFeature.Minute+WorkshopRules.DeliveryMinutes };
        }
        throw new InvalidOperationException("The pallet has no unreserved space. Collect existing deliveries before ordering again.");
    }

    internal bool DeliverOrders(WorkshopState state)
    {
        var changed=false;
        foreach (var order in state.Orders)
        {
            if (order.Stage==OrderStage.Delivered || WorkshopFeature.Minute<order.DueMinute) continue;
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
                Notify($"{order.Quantity} Prism Base parcel(s) ready at {CollectionName}. Follow the purple star.");
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
    internal void Animate(BuildableItem? item,string task,float progress,SheetView sheet,int cutIndex=0)
    {
        if (item is null || !item) return;
        var parts=item.GetComponentsInChildren<Transform>(true).Where(p=>p.name is "Roller" or "RollerHandle" or "CutterArm" or "CutterGrip").ToArray();
        foreach (var part in parts)
        {
            if (!PartOrigins.TryGetValue(part.Pointer,out var origin))
            { origin=(part.localPosition,part.localRotation);PartOrigins[part.Pointer]=origin; }
        }
        Vector3 OriginalPoint(Transform part,Vector3 point){var origin=PartOrigins[part.Pointer];return part.parent.TransformPoint(origin.Position+origin.Rotation*Vector3.Scale(point,part.localScale));}
        if(task=="print"){
            var roller=parts.First(p=>p.name=="Roller");var original=OriginalPoint(roller,roller.GetComponent<MeshFilter>().sharedMesh.bounds.center);
            var target=sheet.Point(new(0.5f,sheet.PrintCoordinate(progress)));
            var offset=Vector3.ProjectOnPlane(target-original,sheet.Normal);
            foreach(var part in parts.Where(p=>p.name.StartsWith("Roller",StringComparison.Ordinal)))part.position=OriginalPoint(part,Vector3.zero)+offset;
        }
        if(task=="finishcut"){
            var arm=parts.First(p=>p.name=="CutterArm");var grip=parts.First(p=>p.name=="CutterGrip");
            var bounds=arm.GetComponent<MeshFilter>().sharedMesh.bounds;
            var length=bounds.extents;var extent=length.x>length.y && length.x>length.z ? new Vector3(length.x,0,0):length.y>length.z ? new Vector3(0,length.y,0):new Vector3(0,0,length.z);
            var first=OriginalPoint(arm,bounds.center+extent);var second=OriginalPoint(arm,bounds.center-extent);
            var handle=OriginalPoint(grip,grip.GetComponent<MeshFilter>().sharedMesh.bounds.center);var pivot=(first-handle).sqrMagnitude>(second-handle).sqrMagnitude ? first:second;
            var(start,end)=SheetView.CutLine(cutIndex);
            var direction=(sheet.Point(end)-sheet.Point(start)).normalized;
            var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.ProjectOnPlane(pivot-handle,sheet.Normal).normalized,direction,sheet.Normal),sheet.Normal);
            var target=sheet.Point(Vector2.Lerp(start,end,progress))+sheet.Normal*0.003f;
            foreach(var part in new[]{arm,grip}){
                var origin=PartOrigins[part.Pointer];part.position=target+rotation*(OriginalPoint(part,Vector3.zero)-pivot);
                part.rotation=rotation*part.parent.rotation*origin.Rotation;
            }
        }
    }
    internal void ResetAnimation(BuildableItem? item)
    {
        if (item is null || !item) return;
        foreach (var part in item.GetComponentsInChildren<Transform>(true))
            if (PartOrigins.TryGetValue(part.Pointer,out var origin)) { part.localPosition=origin.Position;part.localRotation=origin.Rotation; }
    }
    internal Transform? DeliveryTarget(WorkshopState state)
    {
        if (!CollectionShop || CollectionShop!.DeliveryBays is null) return null;
        foreach (var order in state.Orders.AsEnumerable().Reverse())
        {
            if (order.Bay>=CollectionShop.DeliveryBays.Length) continue;
            var pallet=CollectionShop.DeliveryBays[order.Bay];if (!pallet) continue;
            if (order.Stage!=OrderStage.Delivered || pallet.ItemSlots.ToArray().Any(s=>s.ItemInstance?.ID==WorkshopRules.Prism)) return pallet.transform;
        }
        return null;
    }
    private static bool Interact(InteractableObject __instance)
    {
        var current=WorkshopFeature.Current;
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
        if (!grid || !grid.ParentProperty || grid.ParentProperty!=WorkshopFeature.Current?.World?.Office || !grid.ParentProperty.IsOwned)
            __instance._validPosition=false;
    }
    private static bool CreateGridItem(NativeItem item,Grid grid,ref GridItem __result)
    {
        if (item is null || !Equipment(item.ID)) return true;
        if (grid && grid.ParentProperty && grid.ParentProperty.TryCast<SewerOffice>() is not null && grid.ParentProperty.IsOwned) return true;
        __result=null!;WorkshopFeature.Current?.ShowMessage("Place this equipment inside the owned Sewer Office.");return false;
    }
    private static bool Active(BuildableItem item)=>item && item.ItemInstance is not null && Equipment(item.ItemInstance.ID)
        && WorkshopFeature.Current?.State.Machines.Any(m=>m.Id==item.GUID.ToString() && m.Stage!=MachineStage.Empty)==true;
    private static bool CanRemove(BuildableItem __instance,ref string reason,ref bool __result)
    {
        if (!Active(__instance)) return true;reason="Collect or finish this machine's batch before moving it.";__result=false;return false;
    }
    private static bool Destroy(BuildableItem __instance)
    {
        if (Active(__instance)) return false;
        if (__instance && __instance.ItemInstance is not null && Equipment(__instance.ItemInstance.ID) && WorkshopNetwork.Host && WorkshopFeature.Current is { } mod)
        { if (mod.State.Machines.RemoveAll(m=>m.Id==__instance.GUID.ToString())>0) { mod.State.Revision++;WorkshopNetwork.Publish(); } }
        return true;
    }
    public void Dispose() { if (Sticker) Object.Destroy(Sticker);foreach (var view in Cabinets.Values)view.Dispose();Cabinets.Clear();foreach (var material in Lamps.Values) Object.Destroy(material);Lamps.Clear();Machines.Clear();Office=null;Conversation=null; }
}
