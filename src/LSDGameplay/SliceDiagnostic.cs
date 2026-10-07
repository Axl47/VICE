using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Tiles;
using MelonLoader;
using UnityEngine;
using Clock=Il2CppScheduleOne.GameTime.TimeManager;

namespace PersonalMods.LSDGameplay;

// Explicit native integration check. Exercise mode requires a caller-supplied
// output directory and a backup; all mutations are restricted to slot five.
internal sealed class SliceDiagnostic
{
    private static bool Started;
    private readonly LSDGameplayMod Mod;
    private readonly string Output;
    private readonly bool Exercise,Reload;
    private int Phase,Art;
    private float Next;
    private bool Completed;
    private bool CapturedScene;
    private BuildableItem? Bench,Cabinet;
    internal SliceDiagnostic(LSDGameplayMod mod)
    {
        Mod=mod;var args=Environment.GetCommandLineArgs();
        Output=args.FirstOrDefault(a=>a.StartsWith("--lsd-check-output="))?[19..] ?? "";
        Exercise=args.Contains("--lsd-slice-exercise") && Output.Length>0;
        Reload=args.Contains("--lsd-slice-reload") && Output.Length>0;
        if (Output.Length>0) Directory.CreateDirectory(Output);
    }
    internal static void TryLoadFifthSlot()
    {
        if (Started || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Menu") return;
        var manager=LoadManager.Instance;
        if (!manager || manager.IsLoading || LoadManager.SaveGames is null || LoadManager.SaveGames.Length<5 || LoadManager.SaveGames[4] is null) return;
        Started=true;MelonLogger.Msg("LSD_SLICE_CHECK loading only copied save slot five.");
        manager.StartGame(LoadManager.SaveGames[4],false,true);
    }
    internal void Tick()
    {
        if (Completed || LoadManager.Instance.TimeSinceGameLoaded<5 || Time.realtimeSinceStartup<Next || SaveManager.Instance.IsSaving) return;
        try { Run(); }
        catch (Exception error)
        {
            Completed=true;MelonLogger.Error("LSD_SLICE_CHECK_FAILED phase="+Phase+" "+error);
            if (Output.Length>0) File.WriteAllText(Path.Combine(Output,"failure.txt"),error.ToString());
            if (Exercise || Reload) Application.Quit();
        }
    }
    private void Run()
    {
        var folder=LoadManager.Instance.LoadedGameFolderPath.TrimEnd(Path.DirectorySeparatorChar);
        Check(Path.GetFileName(folder)=="SaveGame_5","Diagnostic refused a different save slot.");
        if (Phase==0)
        {
            Mod.State.Validate();Check(Mod.World?.Office?.IsOwned==true,"Test requires normally owned Sewer Office.");
            for (var slot=0;slot<WorkshopRules.ArtSlots;slot++) foreach (var packaging in new[] { "","baggie","jar","brick" })
            {
                var item=Content.Create(WorkshopRules.Product(slot),1,packaging).Cast<ProductItemInstance>();
                Check(item.GetTotalAmount()==(packaging switch { "jar"=>5,"brick"=>20,_=>1 }),"Incorrect native sale-unit count.");
                Check(item.Definition.TryCast<ProductDefinition>()!.ConsumeAnimation,"Missing native consumption animation.");
            }
            MelonLogger.Msg($"LSD_SLICE_CHECK native definitions/counts passed; collection={Mod.World!.CollectionName}.");
            if (Reload) { VerifyReload();return; }
            if (!Exercise) { Completed=true;return; }
            foreach (var slot in InventoryTransaction.RegularSlots()) slot.SetStoredItem(null,false);
            Bench=Place(WorkshopRules.Bench,3,2);Cabinet=Place(WorkshopRules.Cabinet,2,2);
            Move(Bench);Mod.World.Tick();
            var camera=PlayerCamera.Instance.Camera;var target=Bench.transform.position+Vector3.up*0.6f;
            PlayerCamera.Instance.OverrideTransform(target+Bench.transform.forward*2.3f+Vector3.up*1.2f,Quaternion.LookRotation(-Bench.transform.forward*2.3f-Vector3.up*1.2f),0.2f,false);
            foreach (var renderer in Bench.GetComponentsInChildren<Renderer>(true))
                MelonLogger.Msg($"LSD_RENDER {renderer.name}, enabled={renderer.enabled}, active={renderer.gameObject.activeInHierarchy}, layer={renderer.gameObject.layer}, mask={camera.cullingMask}, bounds={renderer.bounds}, shader={renderer.sharedMaterial?.shader?.name}, supported={renderer.sharedMaterial?.shader?.isSupported}.");
            Next=Time.realtimeSinceStartup+2.5f;Phase=1;return;
        }
        switch (Phase)
        {
            case 1:
                if (!CapturedScene)
                { CapturedScene=true;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"studio-props.png"));Next=Time.realtimeSinceStartup+2;return; }
                PlayerMovement.Instance.Teleport(Mod.World!.Office!.InteriorSpawnPoint.position,true);
                Action("introduce");Check(Mod.State.Introduced,"Introduction did not persist.");Move(Bench!);
                var pixels=Enumerable.Range(0,4096).Select(i=>(byte)((i/64+i%64)/8%7+1)).ToArray();
                Action("draw",Bench,new() { Name="Diagnostic edition",Pixels=pixels,Repeated=true });Art=Mod.State.Artworks.Last().Slot;
                Action("order",null,new() { Quantity=1 });
                Mod.State.Orders.Last().DueMinute=LSDGameplayMod.Minute;
                Check(Mod.World.DeliverOrders(Mod.State),"Delivery did not reach the physical pallet.");
                Check(!Mod.World.DeliverOrders(Mod.State),"Delivered order was delivered twice.");
                Add(WorkshopRules.Prism,2);Add(WorkshopRules.Paper,2);Add(WorkshopRules.Ink,2);
                Add("baggie",1);Add("jar",1);Add(WorkshopRules.Sleeve,1);
                Action("start",Bench,new() { ArtSlot=Art });
                var supplies=Inventory();var state=Mod.State.Serialize();
                var rejected=Mod.Execute(Player.Local,new() { Action="collect",Machine=Bench!.GUID.ToString() });
                Check(!rejected.Success && Inventory()==supplies && Mod.State.Serialize()==state,"Early collection changed inventory or state.");
                Next=Time.realtimeSinceStartup+7;Phase=2;return;
            case 2:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"bench-menu.png"));
                Action("prepare",Bench,new() { Accuracy=1 });
                Next=Time.realtimeSinceStartup+7;Phase=3;return;
            case 3:
                Action("print",Bench,new() { Accuracy=1 });Action("collect",Bench);Mod.Panel!.Close();
                Check(Count(WorkshopRules.Sheet(false,Art,40))==1,"Wet sheet identity or yield changed.");
                Move(Cabinet!);Action("dry",Cabinet);
                Check(!Cabinet!.CanBePickedUp(out var reason),"Active cabinet could be moved.");
                Clock.Instance.SkipToTimeAndSync(Clock.AddMinutesTo24HourTime(Clock.Instance.CurrentTime,121));Mod.State.Tick(LSDGameplayMod.Minute);
                Check(Mod.State.Machines.Single(m=>m.Id==Cabinet.GUID.ToString()).Stage==MachineStage.DryOutput,"Native time skip did not finish drying.");
                Action("collect",Cabinet);Move(Bench!);Action("cut",Bench);
                Next=Time.realtimeSinceStartup+7;Phase=4;return;
            case 4:
                Action("finishcut",Bench,new() { Accuracy=1 });
                var slots=InventoryTransaction.RegularSlots();var before=slots.Select(s=>s.ItemInstance?.GetCopy(s.Quantity)).ToArray();
                foreach (var slot in slots) slot.SetStoredItem(Content.Create("baggie",20),false);
                var full=Mod.Execute(Player.Local,new() { Action="collect",Machine=Bench!.GUID.ToString() });
                Check(!full.Success && Mod.State.Machines.Single(m=>m.Id==Bench.GUID.ToString()).Stage==MachineStage.TabOutput,"Full inventory consumed machine output.");
                for (var i=0;i<slots.Length;i++) slots[i].SetStoredItem(before[i],false);
                Action("collect",Bench);Check(Count(WorkshopRules.Product(Art))==40,"Incorrect finished tab count.");
                foreach (var count in new[] { 1,5,20 }) Action("pack",Bench,new() { ArtSlot=Art,Quantity=count });
                Check(TotalUnits()==40,"Packaging changed sale-unit count.");
                var native=Il2CppScheduleOne.Registry.GetItem(WorkshopRules.Product(Art)).TryCast<ProductDefinition>()!;
                Check(Il2CppScheduleOne.Product.ProductManager.DiscoveredProducts.ToArray().Any(d=>d.ID==native.ID),"Finished edition was not discoverable for native sales.");
                Check(native.Price>0,"Native product price is unavailable.");
                Player.Local.ConsumeProduct(Content.Create(WorkshopRules.Product(Art),1).Cast<ProductItemInstance>());
                Next=Time.realtimeSinceStartup+3;Phase=5;return;
            case 5:
                Check(Player.Local.ConsumedProduct?.ID==WorkshopRules.Product(Art),"Native consumption did not keep product identity.");
                var preview=MelonMod.RegisteredMelons.First(m=>m.Info.Name=="Personal LSD Visual Preview");
                var effects=preview.GetType().GetField("Effects",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(preview);
                Check(effects is not null,"Consuming a tab did not start the approved preview.");
                Move(Bench!);Action("start",Bench,new() { ArtSlot=Art });Action("order",null,new() { Quantity=1 });
                File.WriteAllText(Path.Combine(Output,"expected-workshop.json"),Mod.State.Serialize());
                File.WriteAllText(Path.Combine(Output,"expected-inventory.txt"),Inventory());
                SaveManager.Instance.Save(folder);Phase=6;Next=Time.realtimeSinceStartup+3;return;
            case 6:
                var path=Path.Combine(folder,"Modded","OffRegister","Workshop.json");
                Check(File.Exists(path),"Workshop file missing after native save.");
                Check(WorkshopState.Deserialize(File.ReadAllText(path)).Machines.Any(m=>m.Stage==MachineStage.Preparing),"In-progress batch missing after save.");
                MelonLogger.Msg("LSD_SLICE_EXERCISE_PASSED introduction, drawing, ordering/delivery, native placement, production, time skip, capacity failure, packaging, consumption and save.");
                File.WriteAllText(Path.Combine(Output,"exercise-passed.txt"),Mod.State.Serialize());Completed=true;Application.Quit();return;
        }
    }
    private void VerifyReload()
    {
        var expected=WorkshopState.Deserialize(File.ReadAllText(Path.Combine(Output,"expected-workshop.json")));
        foreach (var machine in expected.Machines)
        {
            Check(Mod.World!.FindMachine(machine.Id) is not null,"Native placed equipment missing after reload.");
            var restored=Mod.State.Machines.Single(m=>m.Id==machine.Id);
            Check(restored.Stage==machine.Stage && restored.Yield==machine.Yield && restored.ArtSlot==machine.ArtSlot,"Machine stage changed after reload.");
        }
        Check(Mod.State.Artworks.Last().Pixels.SequenceEqual(expected.Artworks.Last().Pixels),"Custom drawing changed after reload.");
        Check(Mod.State.Introduced && Mod.State.Orders.Count==expected.Orders.Count,"Introduction/order history changed after reload.");
        Check(Mod.State.Orders.Any(o=>o.Stage==OrderStage.InTransit),"In-transit order lost after reload.");
        Check(Inventory()==File.ReadAllText(Path.Combine(Output,"expected-inventory.txt")),"Native inventory/package quantities changed after reload.");
        Check(!Mod.World!.DeliverOrders(Mod.State),"Order was duplicated or delivered early after reload.");
        MelonLogger.Msg("LSD_SLICE_RELOAD_PASSED native equipment, packages, in-progress batch, immutable drawing, introduction and order ledger.");
        File.WriteAllText(Path.Combine(Output,"reload-passed.txt"),Mod.State.Serialize());Completed=true;Application.Quit();
    }
    private BuildableItem Place(string id,int width,int depth)
    {
        foreach (var grid in Mod.World!.Office!.Grids) foreach (var tile in grid.Tiles)
        {
            var free=true;
            for (var x=0;x<width;x++) for (var y=0;y<depth;y++)
            { var candidate=grid.GetTile(new Coordinate(tile.x+x,tile.y+y));if (!candidate || !candidate.CanBeBuiltOn()) free=false; }
            if (!free) continue;
            var center=tile.transform.position+grid.transform.right*((width-1)*0.25f)+grid.transform.forward*((depth-1)*0.25f)+Vector3.up*0.7f;
            var volume=new Bounds(center,new(width*0.5f,1.4f,depth*0.5f));
            if (Mod.World.Office.BuildableItems.ToArray().Any(existing=>existing && existing.BoundingCollider && volume.Intersects(existing.BoundingCollider.bounds))) continue;
            var item=BuildManager.Instance.CreateGridItem(Content.Create(id,1),grid,new(tile.x,tile.y),0,Guid.NewGuid().ToString(),null);
            Check(item && item.Initialized,"Native furniture placement failed.");return item;
        }
        throw new InvalidOperationException("No empty footprint for test equipment.");
    }
    private void Move(BuildableItem equipment)=>PlayerMovement.Instance.Teleport(equipment.transform.position-equipment.transform.forward*1.25f,true);
    private void Action(string action,BuildableItem? equipment=null,StudioCommand? command=null)
    {
        command??=new();command.Action=action;command.Machine=equipment?.GUID.ToString() ?? "";
        var reply=Mod.Execute(Player.Local,command);Check(reply.Success,action+": "+reply.Message);MelonLogger.Msg("LSD_SLICE_ACTION "+action+" passed.");
    }
    private static void Add(string id,int quantity)
    { var transaction=new InventoryTransaction(Player.Local);transaction.Add(Content.Create(id,quantity));transaction.Commit(); }
    private static int Count(string id)=>new InventoryTransaction(Player.Local).Count(id);
    private int TotalUnits()=>new InventoryTransaction(Player.Local).Items.Where(i=>i.ID==WorkshopRules.Product(Art)).Sum(i=>i.Cast<ProductItemInstance>().GetTotalAmount());
    private static string Inventory()=>string.Join("\n",PlayerInventory.Instance.GetAllInventorySlots().ToArray().Select(s=>InventoryTransaction.Fingerprint(s.ItemInstance)).Where(s=>s.Length>0).OrderBy(s=>s));
    private static void Check(bool success,string message) { if (!success) throw new InvalidOperationException(message); }
}
