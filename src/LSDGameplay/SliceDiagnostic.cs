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
    private readonly bool SheetCheck;
    private readonly bool PackagingCheck;
    private readonly bool PrismCheck;
    private readonly bool ArtCheck;
    private readonly bool ArtGallery;
    private readonly List<GameObject> ArtDisplay=new();
    private readonly Dictionary<IntPtr,bool> ArtVisibility=new();
    private Il2CppScheduleOne.ObjectScripts.PackagingStation? PhysicsStation;
    private Il2CppScheduleOne.PlayerTasks.PackageProductTask? PhysicsTask;
    private int PhysicsCase,PhysicsTab;
    private int Phase,Art;
    private float Next;
    private bool Completed;
    private bool CapturedScene;
    private BuildableItem? Bench,Cabinet;
    private int EquippedBefore;
    private bool EquippingBefore,HotbarBefore;
    private float CutDeadline,StageDeadline;
    internal SliceDiagnostic(LSDGameplayMod mod)
    {
        Mod=mod;var args=Environment.GetCommandLineArgs();
        Output=args.FirstOrDefault(a=>a.StartsWith("--lsd-check-output="))?[19..] ?? "";
        Exercise=args.Contains("--lsd-slice-exercise") && Output.Length>0;
        Reload=args.Contains("--lsd-slice-reload") && Output.Length>0;
        SheetCheck=args.Contains("--lsd-sheet-check") && Output.Length>0;
        PackagingCheck=args.Contains("--lsd-packaging-check") && Output.Length>0;
        PrismCheck=args.Contains("--lsd-prism-check") && Output.Length>0;
        ArtGallery=args.Contains("--lsd-art-gallery") && Output.Length>0;
        ArtCheck=(args.Contains("--lsd-art-check") && Exercise) || ArtGallery;
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
            if (Exercise || Reload || SheetCheck || PackagingCheck || PrismCheck || ArtGallery) Application.Quit();
        }
    }
    private void Run()
    {
        var folder=LoadManager.Instance.LoadedGameFolderPath.TrimEnd(Path.DirectorySeparatorChar);
        Check(Path.GetFileName(folder)=="SaveGame_5","Diagnostic refused a different save slot.");
        if (Phase==0)
        {
            Mod.State.Validate();Check(Mod.World?.Office?.IsOwned==true,"Test requires normally owned Sewer Office.");
            VerifyPrism();
            if(PrismCheck){File.WriteAllText(Path.Combine(Output,"prism-check-passed.txt"),"Wrapped parcel restored and native Prism Base definition is Illegal; paper and cartridges stay Legal.");MelonLogger.Msg("LSD_PRISM_CHECK_PASSED restored wrapped parcel, native contraband flag, legal ordinary supplies.");Completed=true;Application.Quit();return;}
            Check(Il2CppScheduleOne.UI.Shop.ShopInterface.AllShops.ToArray().Any(s=>s.GetListing(WorkshopRules.Sleeve) is {} listing && listing.Item.ID==WorkshopRules.Sleeve && listing.Price==1),"Warehouse does not sell the native edition sleeve.");
            for (var slot=0;slot<WorkshopRules.ArtSlots;slot++) foreach (var packaging in new[] { "","baggie","jar","brick",WorkshopRules.Sleeve })
            {
                var item=Content.Create(WorkshopRules.Product(slot),1,packaging).Cast<ProductItemInstance>();
                Check(item.GetTotalAmount()==(packaging switch { "jar"=>5,"brick" or WorkshopRules.Sleeve=>20,_=>1 }),"Incorrect native sale-unit count.");
                Check(item.Definition.TryCast<ProductDefinition>()!.ConsumeAnimation,"Missing native consumption animation.");
            }
            MelonLogger.Msg($"LSD_SLICE_CHECK native definitions/counts passed; collection={Mod.World!.CollectionName}.");
            if(ArtGallery){
                Bench=Mod.World.Office!.BuildableItems.ToArray().First(i=>i && i.ItemInstance?.ID==WorkshopRules.Bench);
                Cabinet=Mod.World.Office.BuildableItems.ToArray().First(i=>i && i.ItemInstance?.ID==WorkshopRules.Cabinet);
                Move(Bench);BeginArtReview();Next=Time.realtimeSinceStartup+2.5f;Phase=400;return;
            }
            if(SheetCheck || PackagingCheck){
                Bench=Mod.World.Office!.BuildableItems.ToArray().First(i=>i && i.ItemInstance?.ID==WorkshopRules.Bench);
                Move(Bench);
                EquippedBefore=PlayerInventory.Instance.EquippedSlotIndex;EquippingBefore=PlayerInventory.Instance.EquippingEnabled;HotbarBefore=PlayerInventory.Instance.HotbarEnabled;
                Mod.Panel!.OpenMachine(Bench);Next=Time.realtimeSinceStartup+1;Phase=200;return;
            }
            if (Reload) { VerifyReload();return; }
            if (!Exercise) { Completed=true;return; }
            foreach (var slot in InventoryTransaction.RegularSlots()) slot.SetStoredItem(null,false);
            Bench=Place(WorkshopRules.Bench,3,2);Cabinet=Place(WorkshopRules.Cabinet,2,2);
            Move(Bench);Mod.World.Tick();
            var camera=PlayerCamera.Instance.Camera;var target=Bench.transform.position+Vector3.up*0.6f;
            PlayerCamera.Instance.OverrideTransform(target+Bench.transform.forward*2.3f+Vector3.up*1.2f,Quaternion.LookRotation(-Bench.transform.forward*2.3f-Vector3.up*1.2f),0.2f,false);
            foreach (var renderer in Bench.GetComponentsInChildren<Renderer>(true))
                MelonLogger.Msg($"LSD_RENDER {renderer.name}, enabled={renderer.enabled}, active={renderer.gameObject.activeInHierarchy}, layer={renderer.gameObject.layer}, mask={camera.cullingMask}, bounds={renderer.bounds}, shader={renderer.sharedMaterial?.shader?.name}, supported={renderer.sharedMaterial?.shader?.isSupported}.");
            if(ArtCheck)BeginArtReview();
            Next=Time.realtimeSinceStartup+2.5f;Phase=ArtCheck ? 400:1;return;
        }
        switch (Phase)
        {
            case 400:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"bench-in-game.png"));
                Next=Time.realtimeSinceStartup+0.5f;Phase=410;return;
            case 410:
                RestoreArtVisibility();HideOtherArt(Cabinet!);
                FrameArt(Cabinet!,new(0,0.68f,0),1.5f,0.35f);Next=Time.realtimeSinceStartup+2;Phase=401;return;
            case 401:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"cabinet-in-game.png"));
                Next=Time.realtimeSinceStartup+0.5f;Phase=411;return;
            case 411:
                RestoreArtVisibility();HideOtherArt(Bench!);
                ShowArtSupplies();FrameArt(Bench!,new(0,0.98f,0),0.8f,0.65f);Next=Time.realtimeSinceStartup+2;Phase=402;return;
            case 402:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"supplies-in-game.png"));
                Next=Time.realtimeSinceStartup+0.5f;Phase=412;return;
            case 412:
                foreach(var display in ArtDisplay)UnityEngine.Object.Destroy(display);ArtDisplay.Clear();
                RestoreArtVisibility();
                Add(WorkshopRules.Prism,1);
                PlayerInventory.Instance.Equip(PlayerInventory.Instance.hotbarSlots.ToArray().First(s=>s.ItemInstance?.ID==WorkshopRules.Prism));
                FrameArt(Bench!,new(0,0.7f,0),2.0f,0.25f);Next=Time.realtimeSinceStartup+2;Phase=403;return;
            case 403:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"prism-held-in-game.png"));
                Next=Time.realtimeSinceStartup+0.5f;Phase=413;return;
            case 413:
                PlayerInventory.Instance.Equip(null);
                if(ArtGallery){File.WriteAllText(Path.Combine(Output,"art-gallery-passed.txt"),"Six imported model contracts and live furniture/stored/held views captured.");Completed=true;Application.Quit();return;}
                Next=Time.realtimeSinceStartup+1;Phase=1;return;
            case 200:
                VerifyPrintGeometry();ScreenCapture.CaptureScreenshot(Path.Combine(Output,"roller-halfway.png"));Next=Time.realtimeSinceStartup+0.7f;Phase=201;return;
            case 201:
                Mod.Panel!.CheckSheet.Frame(true);Next=Time.realtimeSinceStartup+0.7f;Phase=202;return;
            case 202:
                VerifyCutterGeometry();ScreenCapture.CaptureScreenshot(Path.Combine(Output,"cutter-raised.png"));Next=Time.realtimeSinceStartup+0.7f;Phase=203;return;
            case 203:
                Mod.Panel!.Close();VerifyControls();VerifySheetIcons();File.WriteAllText(Path.Combine(Output,"sheet-check-passed.txt"),"Screen-up roller, matching reveal, knife following all cut lines, tolerant edge projection, distinct sheet icons and hotbar restoration passed.");
                MelonLogger.Msg("LSD_SHEET_CHECK_PASSED screen-up roller/reveal, knife path, overshoot, sheet icons and hotbar restoration.");
                if(PackagingCheck){PhysicsStation=Place("packagingstation",3,2).Cast<Il2CppScheduleOne.ObjectScripts.PackagingStation>();BeginPhysicsCase();return;}
                Completed=true;Application.Quit();return;
            case 210:
                Check(PhysicsTask!.Packaging.PackedProducts.Count==PhysicsTab+1,"Released tab missed the native package trigger: case="+PhysicsCase+", tab="+PhysicsTab+", position="+PhysicsTask.Products[PhysicsTab].transform.position);
                PhysicsTab++;
                if(PhysicsTab<PhysicsTask.Products.Count){DropPhysicsTab();return;}
                Check(PhysicsTask.Packaging.IsFull,"Contact-packed container did not become full.");
                PhysicsTask.Packaging.Seal();PhysicsTask.ReachedOutput();
                var packed=PhysicsStation!.OutputSlot.ItemInstance?.TryCast<ProductItemInstance>();
                Check(packed is not null && packed.GetTotalAmount()==(PhysicsCase<5 ? 1:5),"Physics contact packing changed native output quantities.");
                PhysicsStation.OutputSlot.SetStoredItem(null,false);PhysicsCase++;
                if(PhysicsCase<7){BeginPhysicsCase();return;}
                VerifyNativePackagingUntouched();
                File.WriteAllText(Path.Combine(Output,"packaging-check-passed.txt"),"Five baggie and two five-tab jar tasks accepted released tabs through real native physics trigger contacts, then sealed with exact quantities.");
                MelonLogger.Msg("LSD_PACKAGING_PHYSICS_PASSED five baggies, two jars, 15 released tabs via native contacts and sealing.");Completed=true;Application.Quit();return;
            case 1:
                if (!CapturedScene)
                { CapturedScene=true;ScreenCapture.CaptureScreenshot(Path.Combine(Output,"studio-props.png"));Next=Time.realtimeSinceStartup+2;return; }
                PlayerCamera.Instance.StopTransformOverride(0,true,true);
                PlayerMovement.Instance.Teleport(Mod.World!.Office!.InteriorSpawnPoint.position,true);
                Action("introduce");Check(Mod.State.Introduced,"Introduction did not persist.");Move(Bench!);
                CollectiveCatalogue.OpenCatalogue();
                Next=Time.realtimeSinceStartup+1;Phase=10;return;
            case 10:
                Check(CollectiveCatalogue.CheckIsOpen,"Sticker catalogue action did not open the native phone app: "+CollectiveCatalogue.CheckPhoneState);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"phone.png"));Next=Time.realtimeSinceStartup+0.5f;Phase=100;return;
            case 100:
                CollectiveCatalogue.CheckClose();Next=Time.realtimeSinceStartup+0.5f;Phase=101;return;
            case 101:
                Check(PlayerInventory.Instance.EquippingEnabled && PlayerInventory.Instance.HotbarEnabled,"Native phone close did not restore inventory controls.");
                Mod.Panel!.OpenMachine(Bench!);Mod.Panel.CheckDrawing("Diagnostic edition");
                Next=Time.realtimeSinceStartup+0.8f;Phase=111;return;
            case 111:
                Mod.Panel!.CheckProjection();
                Mod.Panel.CheckPointer(new(0.15f,0.2f),true,false);Next=Time.realtimeSinceStartup+0.8f;Phase=11;return;
            case 11:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"drawing.png"));Next=Time.realtimeSinceStartup+0.5f;Phase=110;return;
            case 110:
                Mod.Panel!.CheckSaveDrawing();
                Art=Mod.State.Artworks.Last().Slot;
                Check(Mod.State.Artworks.Last().Pixels[12*64+9]==3 && Mod.State.Artworks[0].Pixels[12*64+9]!=3,"Drawing input did not paint the physical sheet or changed the original edition.");
                Mod.Panel.Close();
                Action("order",null,new() { Quantity=1 });
                Mod.State.Orders.Last().DueMinute=LSDGameplayMod.Minute;
                Check(Mod.World!.DeliverOrders(Mod.State),"Delivery did not reach the physical pallet.");
                Check(!Mod.World.DeliverOrders(Mod.State),"Delivered order was delivered twice.");
                Add(WorkshopRules.Prism,2);Add(WorkshopRules.Paper,2);Add(WorkshopRules.Ink,2);
                Add("baggie",1);Add("jar",1);Add(WorkshopRules.Sleeve,1);
                var selected=PlayerInventory.Instance.hotbarSlots.ToArray().First(s=>s.ItemInstance?.ID=="baggie");
                PlayerInventory.Instance.Equip(selected);EquippedBefore=PlayerInventory.Instance.EquippedSlotIndex;
                EquippingBefore=PlayerInventory.Instance.EquippingEnabled;HotbarBefore=PlayerInventory.Instance.HotbarEnabled;
                Mod.Panel!.OpenMachine(Bench!);Mod.Panel.CheckCommand(new(){Action="start",ArtSlot=Art});
                var supplies=Inventory();var state=Mod.State.Serialize();
                var rejected=Mod.Execute(Player.Local,new() { Action="collect",Machine=Bench!.GUID.ToString() });
                Check(!rejected.Success && Inventory()==supplies && Mod.State.Serialize()==state,"Early collection changed inventory or state.");
                Next=Time.realtimeSinceStartup+0.5f;Phase=2;return;
            case 2:
                Check(Mod.Panel!.CurrentTask=="prepare","Registration controls did not open on the physical sheet.");
                Mod.Panel.CheckProjection();ScreenCapture.CaptureScreenshot(Path.Combine(Output,"registration.png"));
                Mod.Panel.CheckPointer(new(0.5f,0.5f),true,false);
                StageDeadline=Time.realtimeSinceStartup+20;Next=Time.realtimeSinceStartup+3.2f;Phase=3;return;
            case 3:
                if(Mod.Panel!.CurrentTask=="prepare" && Time.realtimeSinceStartup<StageDeadline){Next=Time.realtimeSinceStartup+1;return;}
                Check(Mod.Panel!.CurrentTask=="print","Physical registration did not advance to the roller interaction.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"printing.png"));Mod.Panel.CheckPointer(new(0,0),false,true);
                StageDeadline=Time.realtimeSinceStartup+20;Next=Time.realtimeSinceStartup+4.5f;Phase=30;return;
            case 30:
                if(Mod.Panel!.CurrentTask=="print" && Time.realtimeSinceStartup<StageDeadline){Next=Time.realtimeSinceStartup+1;return;}
                Check(Mod.State.Machines.Single(m=>m.Id==Bench!.GUID.ToString()).Stage==MachineStage.WetOutput,"Roller input did not produce a wet sheet.");
                Mod.Panel!.Close();VerifyControls();Action("collect",Bench);
                Check(Count(WorkshopRules.Sheet(false,Art,40))==1,"Wet sheet identity or yield changed.");
                Move(Cabinet!);Action("dry",Cabinet);
                Check(!Cabinet!.CanBePickedUp(out var reason),"Active cabinet could be moved.");
                Mod.Panel.OpenMachine(Cabinet);Action("testfinish",Cabinet);Mod.Panel.Close();VerifyControls();
                Check(Mod.State.Machines.Single(m=>m.Id==Cabinet.GUID.ToString()).Stage==MachineStage.DryOutput,"F9 test skip did not finish drying.");
                Action("collect",Cabinet);Move(Bench!);Mod.Panel.OpenMachine(Bench!);Mod.Panel.CheckCommand(new(){Action="cut"});
                Next=Time.realtimeSinceStartup+0.5f;Phase=31;return;
            case 31:
                Check(Mod.Panel!.CurrentTask=="finishcut","Distinct cutting interaction did not open.");Mod.Panel.CheckProjection();
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"cutting.png"));Mod.Panel.CheckPointer(new(0,0),false,true);
                CutDeadline=Time.realtimeSinceStartup+30;Next=Time.realtimeSinceStartup+9;Phase=4;return;
            case 4:
                if(Mod.State.Machines.Single(m=>m.Id==Bench!.GUID.ToString()).Stage!=MachineStage.TabOutput && Time.realtimeSinceStartup<CutDeadline){Next=Time.realtimeSinceStartup+1;return;}
                Check(Mod.State.Machines.Single(m=>m.Id==Bench!.GUID.ToString()).Stage==MachineStage.TabOutput,"Cutting strokes did not produce tabs: "+Mod.Panel!.CheckProgress);
                Mod.Panel!.Close();VerifyControls();
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
                VerifyPackagingStation();
                Player.Local.ConsumeProduct(Content.Create(WorkshopRules.Product(Art),1).Cast<ProductItemInstance>());
                Next=Time.realtimeSinceStartup+3;Phase=5;return;
            case 5:
                Check(Player.Local.ConsumedProduct?.ID==WorkshopRules.Product(Art),"Native consumption did not keep product identity.");
                var preview=MelonMod.RegisteredMelons.First(m=>m.Info.Name=="Personal LSD Visual Preview");
                var effects=preview.GetType().GetField("Effects",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(preview);
                Check(effects is not null,"Consuming a tab did not start the approved preview.");
                Check(StudioTutorial.Current is not null && Mod.State.TutorialStep==10,$"First-edition tutorial did not follow production and packaging: native={StudioTutorial.Current is not null}, step={Mod.State.TutorialStep}.");
                Move(Bench!);Action("start",Bench,new() { ArtSlot=Art });Action("order",null,new() { Quantity=1 });
                File.WriteAllText(Path.Combine(Output,"expected-workshop.json"),Mod.State.Serialize());
                File.WriteAllText(Path.Combine(Output,"expected-inventory.txt"),Inventory());
                SaveManager.Instance.Save(folder);Phase=6;Next=Time.realtimeSinceStartup+3;return;
            case 6:
                var path=Path.Combine(folder,"Modded","OffRegister","Workshop.json");
                Check(File.Exists(path),"Workshop file missing after native save.");
                Check(WorkshopState.Deserialize(File.ReadAllText(path)).Machines.Any(m=>m.Stage==MachineStage.Preparing),"In-progress batch missing after save.");
                MelonLogger.Msg("LSD_SLICE_EXERCISE_PASSED physical-sheet projection, distinct registration/roller/cuts, hotbar restoration, F9 drying, station packaging, tutorial, capacity failure, consumption and save.");
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
        Check(Mod.State.TutorialStep==expected.TutorialStep,"Tutorial progress changed after reload.");
        Check(Mod.State.Orders.Any(o=>o.Stage==OrderStage.InTransit),"In-transit order lost after reload.");
        Check(Inventory()==File.ReadAllText(Path.Combine(Output,"expected-inventory.txt")),"Native inventory/package quantities changed after reload.");
        Check(!Mod.World!.DeliverOrders(Mod.State),"Order was duplicated or delivered early after reload.");
        MelonLogger.Msg("LSD_SLICE_RELOAD_PASSED native equipment, packages, in-progress batch, immutable drawing, introduction and order ledger.");
        File.WriteAllText(Path.Combine(Output,"reload-passed.txt"),Mod.State.Serialize());Completed=true;Application.Quit();
    }
    private void VerifyControls()
    {
        var inventory=PlayerInventory.Instance;
        Check(inventory.HotbarEnabled==HotbarBefore && inventory.EquippingEnabled==EquippingBefore && inventory.EquippedSlotIndex==EquippedBefore,
            "Closing a workshop interaction did not restore native hotbar, equipping and selected item.");
    }
    private void VerifyPrintGeometry()
    {
        var sheet=Mod.Panel!.CheckSheet;sheet.VerifyProjection();sheet.BeginPrinting();
        var camera=PlayerCamera.Instance.Camera;
        var start=sheet.Point(new(0.5f,sheet.PrintCoordinate(0)));var end=sheet.Point(new(0.5f,sheet.PrintCoordinate(1)));
        Check(camera.WorldToScreenPoint(end).y>camera.WorldToScreenPoint(start).y,"Printing input still runs from the top of the screen to the bottom.");
        Check(Math.Abs(sheet.PrintProgress(sheet.PrintCoordinate(0.4f))-0.4f)<0.001f,"Print input and reveal coordinates disagree.");
        foreach(var progress in new[]{0f,0.5f,1f}){
            Mod.World!.Animate(Bench,"print",progress,sheet);
            var roller=Bench!.GetComponentsInChildren<MeshFilter>(true).First(p=>p.name=="Roller");
            var center=roller.transform.TransformPoint(roller.sharedMesh.bounds.center);
            var target=sheet.Point(new(0.5f,sheet.PrintCoordinate(progress)));
            Check(Vector3.ProjectOnPlane(center-target,sheet.Normal).magnitude<0.005f,"Physical roller is out of alignment with the reveal line.");
        }
        sheet.Print(0,0.5f);sheet.Roller(0.5f);Mod.World!.Animate(Bench,"print",0.5f,sheet);
        var texture=sheet.CheckPrintTexture;var source=StudioAssets.SheetTexture(0).GetPixels32().ToArray();var actual=texture.GetPixels32().ToArray();
        var visibleY=(int)(sheet.PrintCoordinate(0.25f)*199);var hiddenY=(int)(sheet.PrintCoordinate(0.75f)*199);
        Check(Enumerable.Range(0,320).Any(x=>!source[visibleY*320+x].Equals(StudioAssets.Palette[0])),"Reveal check needs a nonblank artwork row.");
        Check(Enumerable.Range(0,320).All(x=>actual[visibleY*320+x].Equals(source[visibleY*320+x])) && Enumerable.Range(0,320).All(x=>actual[hiddenY*320+x].Equals(StudioAssets.Palette[0])),"Artwork is revealed ahead of the roller instead of behind it.");
        // Reuse one view for multiple batches, including the same progress and a repeated edition.
        foreach(var slot in new[]{0,1,0}){
            sheet.SetArtwork(slot);sheet.BeginPrinting();sheet.Print(slot,1);
            Check(sheet.CheckPrintTexture.GetPixels32().ToArray().SequenceEqual(StudioAssets.SheetTexture(slot).GetPixels32().ToArray()),"A reused sheet view retained another batch's print pixels.");
            sheet.SetArtwork(slot);sheet.BeginPrinting();sheet.Print(slot,0);
            Check(sheet.CheckPrintTexture.GetPixels32().ToArray().All(p=>p.Equals(StudioAssets.Palette[0])),"A new batch retained the previous print coverage.");
        }
        sheet.SetArtwork(0);sheet.BeginPrinting();sheet.Print(0,0.5f);
    }
    private static void VerifyArtModels()
    {
        foreach(var name in new[]{"Bench","Cabinet","Parcel","Packet","Sheet","Tab"}){
            var model=StudioAssets.Model(name);var meshes=model.GetComponentsInChildren<MeshFilter>(true);
            Check(meshes.Length>0 && meshes.All(m=>m.sharedMesh && m.sharedMesh.vertexCount>0),"Empty imported art model: "+name);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                Check(renderer.sharedMaterials.ToArray().All(m=>m && m.shader && m.shader.isSupported),"Unsupported imported material: "+name+"/"+renderer.name);
            if(name=="Bench" || name=="Cabinet")Check(meshes.Any(m=>m.name=="StaticDetails"),"Old placeholder geometry remains in bundle: "+name);
            MelonLogger.Msg("LSD_ART_MODEL_PASSED "+name+" meshes="+meshes.Length);
        }
    }
    private static void FrameArt(BuildableItem item,Vector3 localTarget,float distance,float height)
    {
        var target=FurnitureVisual(item).TransformPoint(localTarget);
        var side=0.35f;
        var position=target+item.transform.forward*distance+item.transform.right*distance*side+Vector3.up*height;
        PlayerCamera.Instance.OverrideTransform(position,Quaternion.LookRotation(target-position),0.2f,false);
    }
    private void ShowArtSupplies()
    {
        // A temporary review tabletop keeps the small registered stored views
        // clear of the bench's roller/knife. It is never written to the save.
        SetArtVisibility(Bench!,false);
        var tabletop=GameObject.CreatePrimitive(PrimitiveType.Cube);
        tabletop.name="Studio art review tabletop";tabletop.transform.position=FurnitureVisual(Bench!).TransformPoint(new(0,0.88f,0));
        tabletop.transform.rotation=Bench!.transform.rotation;tabletop.transform.localScale=new(1.4f,0.025f,0.75f);
        tabletop.GetComponent<Renderer>().sharedMaterial=StudioAssets.SurfaceMaterial(new(0.25f,0.3f,0.32f));
        tabletop.GetComponent<Collider>().enabled=false;ArtDisplay.Add(tabletop);
        var ids=new[]{WorkshopRules.Prism,WorkshopRules.Ink,WorkshopRules.Paper,WorkshopRules.Sleeve,WorkshopRules.Sheet(true,0,40)};
        for(var i=0;i<ids.Length;i++){
            var definition=Il2CppScheduleOne.Registry.GetItem(ids[i]).Cast<Il2CppScheduleOne.ItemFramework.StorableItemDefinition>();
            var visual=UnityEngine.Object.Instantiate(definition.StoredItem.gameObject);
            visual.transform.position=FurnitureVisual(Bench!).TransformPoint(new Vector3((i%3-1)*0.46f,0.895f,(i/3-0.5f)*0.36f));
            visual.transform.rotation=Bench!.transform.rotation;visual.SetActive(true);ArtDisplay.Add(visual);
        }
        var tab=UnityEngine.Object.Instantiate(StudioAssets.Visual("Tab"));
        tab.transform.position=FurnitureVisual(Bench!).TransformPoint(new Vector3(0.46f,0.895f,0.18f));
        tab.transform.rotation=Bench!.transform.rotation;tab.transform.localScale=Vector3.one*0.45f;tab.SetActive(true);ArtDisplay.Add(tab);
    }
    private void BeginArtReview(){VerifyArtModels();HideOtherArt(Bench!);FrameArt(Bench!,new(0,0.62f,0),1.7f,0.65f);}
    private void HideOtherArt(BuildableItem selected){foreach(var item in Mod.World!.Office!.BuildableItems.ToArray())if(item && item.Pointer!=selected.Pointer)SetArtVisibility(item,false);}
    private void RestoreArtVisibility(){foreach(var item in Mod.World!.Office!.BuildableItems.ToArray())if(item)SetArtVisibility(item,true);}
    private void SetArtVisibility(BuildableItem item,bool restore)
    {
        foreach(var renderer in item.GetComponentsInChildren<Renderer>(true)){
            if(restore){if(ArtVisibility.Remove(renderer.Pointer,out var enabled))renderer.enabled=enabled;}
            else{ArtVisibility.TryAdd(renderer.Pointer,renderer.enabled);renderer.enabled=false;}
        }
    }
    private static Transform FurnitureVisual(BuildableItem item)=>item.GetComponentsInChildren<Transform>(true).First(t=>t.name=="FurnitureVisual");
    private void VerifyCutterGeometry()
    {
        var sheet=Mod.Panel!.CheckSheet;sheet.CutGuide(6,0.92f);Mod.World!.ResetAnimation(Bench);
        var parts=Bench!.GetComponentsInChildren<Transform>(true);var arm=parts.First(p=>p.name=="CutterArm");var grip=parts.First(p=>p.name=="CutterGrip");
        var original=arm.InverseTransformPoint(grip.position);var resting=grip.position;
        foreach(var cut in Enumerable.Range(0,11)){
            var(a,b)=SheetView.CutLine(cut);var positions=new List<Vector3>();
            foreach(var progress in new[]{0f,0.5f,1f}){
                Mod.World.Animate(Bench,"finishcut",progress,sheet,cut);positions.Add(grip.position);
                Check(Vector3.Distance(arm.InverseTransformPoint(grip.position),original)<0.001f,"Knife handle moves separately from its blade.");
            }
            var travel=positions[2]-positions[0];var expected=sheet.Point(b)-sheet.Point(a);
            Check(Vector3.Distance(travel,expected)<0.001f,"Knife travel does not follow the active cut direction.");
        }
        foreach(var uv in new[]{new Vector2(0.875f,1.12f),new Vector2(1.12f,0.8f),new Vector2(0.125f,-0.12f)}){
            var screen=PlayerCamera.Instance.Camera.WorldToScreenPoint(sheet.Point(uv));
            Check(!sheet.Project(new(screen.x,screen.y),out _) && sheet.Project(new(screen.x,screen.y),out var projected,0.2f) && Vector2.Distance(projected,uv)<0.025f,"Cut pointer is dropped when dragging slightly beyond the paper edge.");
        }
        Mod.World.Animate(Bench,"finishcut",0.35f,sheet,6);
    }
    private void VerifySheetIcons()
    {
        var wet=StudioAssets.SheetIcon(0,false);var dry=StudioAssets.SheetIcon(0,true);
        Check(wet.Pointer!=dry.Pointer && !wet.texture.GetPixels32().ToArray().SequenceEqual(dry.texture.GetPixels32().ToArray()),"Wet and ready sheet inventory icons are identical.");
        Check(Il2CppScheduleOne.Registry.GetItem(WorkshopRules.Sheet(false,0,40)).Icon.Pointer==wet.Pointer && Il2CppScheduleOne.Registry.GetItem(WorkshopRules.Sheet(true,0,40)).Icon.Pointer==dry.Pointer,"Native sheet definitions do not use the stage-specific icons.");
        File.WriteAllBytes(Path.Combine(Output,"wet-sheet-icon.png"),ImageConversion.EncodeToPNG(wet.texture).ToArray());
        File.WriteAllBytes(Path.Combine(Output,"dry-sheet-icon.png"),ImageConversion.EncodeToPNG(dry.texture).ToArray());
        VerifyPrism();
    }
    private static void VerifyPrism()
    {
        Check(S1API.Items.ItemManager.GetDefinition(WorkshopRules.Prism)!.LegalStatus==S1API.Items.LegalStatus.Illegal,"Prism Base lacks the native illegal/contraband flag.");
        foreach(var id in new[]{WorkshopRules.Paper,WorkshopRules.Ink})Check(S1API.Items.ItemManager.GetDefinition(id)!.LegalStatus==S1API.Items.LegalStatus.Legal,"An ordinary studio supply was marked contraband.");
        var parts=StudioAssets.Visual("Parcel").GetComponentsInChildren<Transform>(true);
        Check(parts.Any(p=>p.name=="WrappedParcel") && parts.Any(p=>p.name=="ParcelBand") && !parts.Any(p=>p.name.StartsWith("BrickStud",StringComparison.Ordinal)),"Prism Base's wrapped parcel model was not restored.");
    }
    private void BeginPhysicsCase()
    {
        var packaging=PhysicsCase<5 ? "baggie":"jar";var count=packaging=="baggie" ? 1:5;
        PhysicsStation!.ProductSlot.SetStoredItem(Content.Create(WorkshopRules.Product(0),count),false);
        PhysicsStation.PackagingSlot.SetStoredItem(Content.Create(packaging,1),false);
        PhysicsTask=PhysicsStation.StartTask().Cast<Il2CppScheduleOne.PlayerTasks.PackageProductTask>();PhysicsTab=0;
        Check(PhysicsTask.Packaging.ProductContactTime<=0.01f,"Thin tab native contact interval was not configured.");
        foreach(var collider in PhysicsTask.Packaging.GetComponentsInChildren<Collider>(true))
            MelonLogger.Msg($"LSD_PACKAGE_COLLIDER {packaging} {collider.name}, enabled={collider.enabled}, trigger={collider.isTrigger}, bounds={collider.bounds}");
        DropPhysicsTab();
    }
    private void DropPhysicsTab()
    {
        var product=PhysicsTask!.Products[PhysicsTab];var body=product.GetComponent<BoxCollider>();
        Check(body && body.enabled && !body.isTrigger && body.bounds.size.x<0.05f,"Tab lacks its small solid packaging collider.");
        var trigger=PhysicsTask.Packaging.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled && c.isTrigger).OrderByDescending(c=>c.bounds.size.sqrMagnitude).First();
        product._IsHeld_k__BackingField=true;PhysicsTask.Packaging.OnTriggerStay(body);
        Check(!PhysicsTask.Packaging.productContactTime.ContainsKey(product) || PhysicsTask.Packaging.productContactTime[product]==0,"Native packaging accepted a tab while it was held.");
        product._IsHeld_k__BackingField=false;
        var point=trigger.bounds.center+trigger.transform.right*((PhysicsCase+PhysicsTab)%3-1)*Math.Min(trigger.bounds.extents.x*0.35f,0.006f);
        product.transform.position+=point-body.bounds.center;product.Rb.position=product.transform.position;
        product.Rb.velocity=Vector3.down*0.4f;product.Rb.angularVelocity=Vector3.zero;product.Rb.WakeUp();
        Physics.SyncTransforms();
        MelonLogger.Msg($"LSD_PACKAGE_DROP case={PhysicsCase}, tab={PhysicsTab}, body={body.bounds}, trigger={trigger.bounds}, velocity={product.Rb.velocity}");
        Next=Time.realtimeSinceStartup+0.45f;Phase=210;
    }
    private void VerifyNativePackagingUntouched()
    {
        var ordinary=Il2CppScheduleOne.Registry.Instance.GetAllItems().ToArray().First(i=>i.TryCast<Il2CppScheduleOne.Product.ShroomDefinition>() is not null && !i.ID.StartsWith(WorkshopRules.Owner+":",StringComparison.Ordinal));
        PhysicsStation!.ProductSlot.SetStoredItem(ordinary.GetDefaultInstance(1).Cast<Il2CppScheduleOne.ItemFramework.ItemInstance>(),false);
        PhysicsStation.PackagingSlot.SetStoredItem(Content.Create("baggie",1),false);
        var task=PhysicsStation.StartTask().Cast<Il2CppScheduleOne.PlayerTasks.PackageProductTask>();
        Check(task.Packaging.ProductContactTime>0.01f,"LSD tab timing leaked into an ordinary native packaging task.");task.StopTask();
    }
    private void VerifyPackagingStation()
    {
        var station=Place("packagingstation",3,2).Cast<Il2CppScheduleOne.ObjectScripts.PackagingStation>();
        var definition=Il2CppScheduleOne.Registry.GetItem(WorkshopRules.Product(Art)).Cast<ProductDefinition>();
        var scaffold=definition.FunctionalProduct;
        var colliders=scaffold.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.enabled).ToArray();
        Check(colliders.Length>0 && colliders.All(c=>c.sharedMesh.bounds.size.x*c.transform.lossyScale.x<0.04f),"Packaging tabs retain the oversized donor collider.");
        foreach(var packaging in new[]{"baggie","jar",WorkshopRules.Sleeve}){
            var count=packaging=="baggie" ? 1:packaging=="jar" ? 5:20;
            station.ProductSlot.SetStoredItem(Content.Create(WorkshopRules.Product(Art),count),false);
            station.PackagingSlot.SetStoredItem(Content.Create(packaging,1),false);
            Check(station.GetState(Il2CppScheduleOne.ObjectScripts.PackagingStation.EMode.Package)==Il2CppScheduleOne.ObjectScripts.PackagingStation.EState.CanBegin,"Packaging station rejected "+packaging);
            MelonLogger.Msg("LSD_MANUAL_STATION starting "+packaging+", native staging="+station.ActiveProductAlignments.Length);
            var task=station.StartTask().Cast<Il2CppScheduleOne.PlayerTasks.PackageProductTask>();
            Check(task.Packaging && task.Products.Count==count,"Manual station did not create all tabs for "+packaging);
            Check(task.Packaging!.gameObject.activeInHierarchy,"Manual packaging scaffold is inactive for "+packaging);
            foreach(var product in task.Products.ToArray())task.Packaging.PackProduct(product);
            Check(task.Packaging.IsFull && task.Packaging.PackedProducts.Count==count,"Manual packing failed for "+packaging);
            task.Packaging.Seal();Check(task.Packaging.IsSealed,"Manual sealing failed for "+packaging);task.ReachedOutput();
            var output=station.OutputSlot.ItemInstance?.TryCast<ProductItemInstance>();
            Check(output is not null && output.PackagingID==packaging && output.GetTotalAmount()==count,"Native station changed edition/package units for "+packaging);
            station.OutputSlot.SetStoredItem(null,false);
        }
        MelonLogger.Msg("LSD_STATION_CHECK manual baggie, jar and edition sleeve tasks, packing and sealing passed; small convex tab colliders verified.");
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
