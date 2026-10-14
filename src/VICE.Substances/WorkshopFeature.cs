using Il2CppScheduleOne;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using MelonLoader;
using S1API.Lifecycle;
using S1API.Building;
using S1API.Products;
using UnityEngine;
using Clock = Il2CppScheduleOne.GameTime.TimeManager;
using Money = S1API.Money.Money;


namespace PersonalMods.LSDGameplay;

internal sealed class WorkshopFeature
{
    internal static WorkshopFeature? Current;
    internal WorkshopState State = new();
    internal WorkshopPanel? Panel;
    internal WorkshopWorld? World;
    internal bool Ready;
    private string SavePath="", SaveTarget="", SaveSnapshot="";
    private float NextTick;
    private readonly Dictionary<string,(string Owner,float Started,float Touched)> Leases=new();
    internal static int Minute=>Clock.Instance ? Clock.Instance.GetTotalMinSum() : 0;
    private readonly bool DiagnosticRequested=Environment.GetCommandLineArgs().Contains("--lsd-slice-check");
    private SliceDiagnostic? Diagnostic;
    private bool LoadFailed;
    private DeliveryGuide? Delivery;

    public void Initialize(HarmonyLib.Harmony harmony)
    {
        Current=this;
        if (DiagnosticRequested) Application.runInBackground=true;
        CustomProductSaveProviderRegistry.Register(new EditionSaveProvider());
        GameLifecycle.OnPreLoad+=Preload;
        GameLifecycle.OnLoadComplete+=Loaded;
        GameLifecycle.OnPreSceneChange+=ClearSession;
        GameLifecycle.OnSaveComplete+=SaveComplete;
        BuildEvents.OnBuildableItemInitialized+=RestoreFurniture;
        ModSupport.Patch(harmony,typeof(SaveManager),"Save",typeof(WorkshopFeature),prefix:nameof(SaveStarted),parameters:new[] { typeof(string) });
        WorkshopWorld.Install(harmony);
        StudioPackaging.Install(harmony);
        ModSupport.Patch(harmony,typeof(GameInput),"Exit",typeof(WorkshopFeature),prefix:nameof(WorkshopExit),parameters:new[]{typeof(ExitType)});
        MelonLogger.Msg("Off Register workshop registered. Use the sticker inside your owned Sewer Office; test with save slot five.");
    }

    private void Preload()
    {
        try
        {
            ClearSession();
            LoadFailed=false;
            Content.Register();
            var folder=LoadManager.Instance.LoadedGameFolderPath;
            SavePath=string.IsNullOrEmpty(folder) ? "" : Path.Combine(folder,"Modded","OffRegister","Workshop.json");
            State=File.Exists(SavePath) ? WorkshopState.Deserialize(File.ReadAllText(SavePath)) : new WorkshopState();
            StudioAssets.UpdateArtwork(State);
        }
        catch (Exception error) { LoadFailed=true;Ready=false;MelonLogger.Error("Workshop load stopped without changing its save: "+error); }
    }
    private static bool WorkshopExit()=>Current?.Panel?.HandleNativeExit()??true;

    private static void RestoreFurniture(BuildEventArgs args)
    {
        if (args.ItemId is not (WorkshopRules.Bench or WorkshopRules.Cabinet) || !args.GameObject) return;
        try { PersonalMods.Networking.ReplicatedFurnitureVisuals.Ensure(args.GameObject.GetComponent<Il2CppScheduleOne.EntityFramework.BuildableItem>()); }
        catch (Exception error) { ModSupport.WarnOnce("lsd:furniture-visuals", "Workshop furniture visuals could not be restored: "+error); }
    }

    private void Loaded()
    {
        if (LoadFailed) return;
        try
        {
            // Some load paths create registries after the first pre-load callback.
            Content.Register(); State.Validate();StudioAssets.UpdateArtwork(State);
            World=new WorkshopWorld(); Panel=new WorkshopPanel();Delivery=new DeliveryGuide();
            World.Load();WorkshopNetwork.Bind();Ready=true;
            if(!TutorialProgress.Finished(State) && S1API.Quests.QuestManager.GetQuestByName("First edition") is null)
                S1API.Quests.QuestManager.CreateQuest<StudioTutorial>();
            if (DiagnosticRequested && WorkshopNetwork.Host) Diagnostic=new SliceDiagnostic(this);
            MelonLogger.Msg($"LSD_WORKSHOP_READY editions={State.Artworks.Count}, machines={State.Machines.Count}, orders={State.Orders.Count}.");
        }
        catch (Exception error) { Ready=false;MelonLogger.Error("Workshop initialization failed: "+error); }
    }

    private static void SaveStarted(string saveFolderPath)
    {
        var current=Current;
        if (current?.Ready!=true || !WorkshopNetwork.Host) return;
        current.SaveTarget=Path.Combine(saveFolderPath,"Modded","OffRegister","Workshop.json");
        current.SaveSnapshot=current.State.Serialize();
        var approved=SaveManager.Instance.ApprovedBaseLevelPaths;
        if (!approved.Contains("Modded")) approved.Add("Modded");
    }

    private void SaveComplete()
    {
        if (SaveTarget.Length==0 || SaveSnapshot.Length==0 || !WorkshopNetwork.Host) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SaveTarget)!);
            var temporary=SaveTarget+".tmp";
            File.WriteAllText(temporary,SaveSnapshot);
            if (File.Exists(SaveTarget)) File.Copy(SaveTarget,SaveTarget+".previous",true);
            File.Move(temporary,SaveTarget,true);
            MelonLogger.Msg($"LSD_WORKSHOP_SAVED revision={State.Revision}.");
        }
        catch (Exception error) { MelonLogger.Error("Native save completed, but the workshop state could not be saved: "+error);ShowMessage("Workshop saving failed. Check the mod log before leaving this session."); }
        finally { SaveTarget=SaveSnapshot=""; }
    }

    public void OnUpdate()
    {
        try
        {
            if (DiagnosticRequested && !Ready) SliceDiagnostic.TryLoadFifthSlot();
            if (!Ready || !LoadManager.Instance || !LoadManager.Instance.IsGameLoaded || LoadManager.Instance.IsLoading) return;
            if(Diagnostic?.ExclusiveChecking==true){Diagnostic.Tick();return;}
            WorkshopNetwork.Tick();Panel?.Tick();World?.Tick();Diagnostic?.Tick();CollectiveCatalogue.Tick();
            Delivery?.Tick(World?.DeliveryTarget(State));StudioTutorial.Current?.Tick(State,World!);
            if (SaveManager.Instance && SaveManager.Instance.IsSaving) return;
            if (Time.realtimeSinceStartup<NextTick) return;
            NextTick=Time.realtimeSinceStartup+1;
            if (WorkshopNetwork.Host)
            {
                var changed=State.Tick(Minute);
                changed=StudioTutorial.Observe(State,World!)||changed;
                changed=World!.DeliverOrders(State)||changed;
                if (changed) { StudioAssets.UpdateArtwork(State);WorkshopNetwork.Publish();Panel?.Refresh(); }
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("lsd:workshop-tick", "Workshop update failed: "+error); }
    }

    internal StudioReply Execute(Player player,StudioCommand command)
    {
        var reply=new StudioReply { Token=command.Token };
        var prior=State.Serialize();
        var committed=false;
        try
        {
            if (!Ready || !WorkshopNetwork.Host || !player || !player.PlayerLoaded)
                throw new InvalidOperationException("The workshop is unavailable.");
            // Lease cleanup is idempotent and must also work during saving, death, or equipment removal.
            if(command.Action=="release"){
                if(Leases.TryGetValue(command.Machine,out var lease) && lease.Owner==player.PlayerCode)Leases.Remove(command.Machine);
                reply.Success=true;reply.State=prior;return reply;
            }
            if(player.IsUnconscious || !player.Health.IsAlive)throw new InvalidOperationException("The workshop is unavailable.");
            if (SaveManager.Instance.IsSaving) throw new InvalidOperationException("Saving in progress. Try again when it finishes.");
            if (command.Action=="sync") { reply.Success=true;reply.State=prior;return reply; }
            var office=World!.Office;
            if (office is null || !office || !office.IsOwned) throw new InvalidOperationException("Unlock and own the Sewer Office through normal gameplay first.");
            if (command.Action=="introduce")
            {
                if (!office.DoBoundsContainPoint(player.transform.position)) throw new InvalidOperationException("Find the collective sticker inside the Sewer Office.");
                State.Introduced=true;State.Revision++;World.Introduce();reply.Message="Off Register is now in your phone. Order Prism Base, then buy your printing supplies and equipment.";
            }
            else if (command.Action=="order")
            {
                if (!State.Introduced) throw new InvalidOperationException("Find the Off Register sticker first.");
                if (command.Quantity is < 1 or > 4) throw new InvalidOperationException("Order one to four parcels.");
                var order=World.ReserveOrder(State,command.Quantity);
                if (Money.GetOnlineBalance()<30*command.Quantity) throw new InvalidOperationException("Insufficient online balance.");
                Money.CreateOnlineTransaction("Off Register",-30,command.Quantity,"Prism Base · "+order.Id);
                State.Orders.RemoveAll(o=>o.Stage==OrderStage.Delivered && State.Orders.Count>=64);
                State.Orders.Add(order);State.Revision++;reply.Message=$"Paid ${30*command.Quantity}. Collect in thirty game minutes at {World.CollectionName}.";
                TutorialProgress.RecordAction(State,"order");
                committed=true;
                World.Notify(reply.Message);
            }
            else
            {
                if (!State.Introduced) throw new InvalidOperationException("Introduce yourself through the collective sticker first.");
                var placed=World.FindMachine(command.Machine) ?? throw new InvalidOperationException("Place the equipment in the Sewer Office first.");
                if (placed.ParentProperty!=office || Vector3.Distance(player.transform.position,placed.transform.position)>4)
                    throw new InvalidOperationException("Move closer to the equipment inside the Sewer Office.");
                var machine=State.Machines.SingleOrDefault(m=>m.Id==command.Machine);
                if (machine is null)
                {
                    machine=new() { Id=command.Machine,Property=office.PropertyCode,Kind=placed.ItemInstance.ID==WorkshopRules.Cabinet ? "cabinet" : "bench" };
                    State.Machines.Add(machine);
                }
                Acquire(machine,player.PlayerCode,command.Action);
                var transaction=new InventoryTransaction(player);
                var priorStage=machine.Stage;
                switch (command.Action)
                {
                    case "cabinetbegin":
                        if(machine.Kind!="cabinet")throw new InvalidOperationException("Use the drying cabinet for this interaction.");
                        if(command.Name.Length>0){
                            Require(machine,"cabinet",MachineStage.Empty);
                            if(!WorkshopRules.TrySheet(command.Name,out var isDry,out var previewSlot,out _) || isDry || State.Artworks.All(a=>a.Slot!=previewSlot))
                                throw new InvalidOperationException("Carry a wet printed sheet from the bench.");
                            // Check removal locks without consuming the sheet until the door closes.
                            new InventoryTransaction(player).Remove(command.Name,1);
                        }else{
                            Require(machine,"cabinet",MachineStage.DryOutput);
                            new InventoryTransaction(player).Add(Content.Create(WorkshopRules.Sheet(true,machine.ArtSlot,machine.Yield),1));
                        }
                        break;
                    case "cabinetkeepalive":
                        if(machine.Kind!="cabinet")throw new InvalidOperationException("Use the drying cabinet for this interaction.");
                        break;
                    case "resume": break;
                    case "draw":
                        if (machine.Kind!="bench" || command.Pixels is null) throw new InvalidOperationException("Use the bench to save artwork.");
                        var art=State.AddArt(command.Name,command.Pixels,command.Repeated);
                        reply.Message=$"Saved {art.Name} as {WorkshopRules.EditionLabel(art.Slot)}. Existing editions keep their artwork.";
                        break;
                    case "start":
                        Require(machine,"bench",MachineStage.Empty);
                        if (State.Artworks.All(a=>a.Slot!=command.ArtSlot)) throw new InvalidOperationException("Choose a saved edition.");
                        transaction.Remove(WorkshopRules.Prism,1);transaction.Remove(WorkshopRules.Paper,1);transaction.Remove(WorkshopRules.Ink,1);
                        machine.ArtSlot=command.ArtSlot;machine.Yield=40;machine.Stage=MachineStage.Preparing;StartLease(machine,player.PlayerCode);
                        reply.Message="Supplies loaded. Stabilize the pattern, then print.";break;
                    case "prepare":
                        Require(machine,"bench",MachineStage.Preparing);RequireTime(machine,2);
                        machine.Yield-=WorkshopRules.Loss(command.Accuracy,2);machine.Stage=MachineStage.Printing;StartLease(machine,player.PlayerCode);
                        reply.Message="Preparation complete. Align the sheet and guide the roller.";break;
                    case "print":
                        Require(machine,"bench",MachineStage.Printing);RequireTime(machine,2);
                        machine.Yield-=WorkshopRules.Loss(command.Accuracy,2);machine.Stage=MachineStage.WetOutput;
                        reply.Message=$"Printed a wet sheet with {machine.Yield} usable tabs. Collect it and carry it to the cabinet.";break;
                    case "dry":
                    case "cut":
                        var needsDry=command.Action=="cut";
                        Require(machine,needsDry ? "bench" : "cabinet",MachineStage.Empty);
                        var sheet=transaction.Items.FirstOrDefault(i=>WorkshopRules.TrySheet(i.ID,out var dry,out _,out _) && dry==needsDry && (command.Name.Length==0 || command.Name==i.ID))
                            ?? throw new InvalidOperationException(needsDry ? "Carry a ready sheet from the cabinet." : "Carry a wet printed sheet from the bench.");
                        WorkshopRules.TrySheet(sheet.ID,out _,out var slot,out var yield);
                        if (State.Artworks.All(a=>a.Slot!=slot)) throw new InvalidOperationException("The sheet's artwork revision is unavailable.");
                        transaction.Remove(sheet.ID,1);machine.ArtSlot=slot;machine.Yield=yield;
                        machine.Stage=needsDry ? MachineStage.Cutting : MachineStage.Drying;
                        machine.DueMinute=needsDry ? 0 : Minute+WorkshopRules.DryMinutes;StartLease(machine,player.PlayerCode);
                        reply.Message=needsDry ? "Align the cutting guide and pull the cutter." : "Drying started. Return in two game hours; sleeping also advances it.";break;
                    case "finishcut":
                        Require(machine,"bench",MachineStage.Cutting);RequireTime(machine,2);
                        machine.Yield-=WorkshopRules.Loss(command.Accuracy,4);machine.Stage=MachineStage.TabOutput;
                        reply.Message=$"Cut {machine.Yield} Standard-quality tabs. Collect them, then package or store them.";break;
                    case "collect":
                        var item=machine.Stage switch {
                            MachineStage.WetOutput=>Content.Create(WorkshopRules.Sheet(false,machine.ArtSlot,machine.Yield),1),
                            MachineStage.DryOutput=>Content.Create(WorkshopRules.Sheet(true,machine.ArtSlot,machine.Yield),1),
                            MachineStage.TabOutput=>Content.Create(WorkshopRules.Product(machine.ArtSlot),machine.Yield),
                            _=>throw new InvalidOperationException("The machine has no finished output yet.") };
                        transaction.Add(item);
                        if (machine.Stage==MachineStage.TabOutput) Content.Products[machine.ArtSlot]!.Discover(true);
                        machine.Stage=MachineStage.Empty;machine.DueMinute=0;reply.Message="Output collected.";break;
                    case "pack":
                        throw new InvalidOperationException("Use a packaging station to pack tabs into baggies, jars or edition sleeves.");
                    default: throw new InvalidOperationException("Unknown studio action.");
                }
                State.Revision++;State.Validate();
                reply.Inventory=transaction.Commit();committed=true;TutorialProgress.RecordAction(State,command.Action,priorStage);
            }
            State.Validate();StudioAssets.UpdateArtwork(State);reply.Success=true;
            reply.State=State.Serialize();World?.RefreshMachines(State);WorkshopNetwork.Publish();
        }
        catch (Exception error)
        {
            if (committed)
            {
                reply.Success=true;reply.State=State.Serialize();reply.Message="Action completed. The display could not refresh; close and reopen the studio.";
                MelonLogger.Error("Studio presentation failed after a committed action: "+error);return reply;
            }
            State=WorkshopState.Deserialize(prior);reply.State=prior;
            if (error is InvalidOperationException or InvalidDataException or ArgumentException) reply.Message=error.Message;
            else { MelonLogger.Error("Studio action failed: "+error);reply.Message="The studio action failed. Your batch is held; check the mod log before trying again."; }
        }
        return reply;
    }

    private void Acquire(MachineState machine,string owner,string action)
    {
        if (DiagnosticRequested) MelonLogger.Msg($"LSD_LEASE action={action}, machine={machine.Id}, owner={owner}, exists={Leases.ContainsKey(machine.Id)}, elapsed={(Leases.TryGetValue(machine.Id,out var observed) ? Time.realtimeSinceStartup-observed.Started : -1)}, sameOwner={(Leases.TryGetValue(machine.Id,out observed) && observed.Owner==owner)}.");
        if (Leases.TryGetValue(machine.Id,out var lease) && lease.Owner!=owner && Time.realtimeSinceStartup-lease.Touched<60)
            throw new InvalidOperationException("Another player is using this machine. Try again when they finish.");
        if (!Leases.TryGetValue(machine.Id,out lease) || lease.Owner!=owner || action=="resume") StartLease(machine,owner);
        else Leases[machine.Id]=(owner,lease.Started,Time.realtimeSinceStartup);
    }
    private void StartLease(MachineState machine,string owner)=>Leases[machine.Id]=(owner,Time.realtimeSinceStartup,Time.realtimeSinceStartup);
    private void RequireTime(MachineState machine,float seconds)
    {
        if (!Leases.TryGetValue(machine.Id,out var lease) || Time.realtimeSinceStartup-lease.Started<seconds)
            throw new InvalidOperationException("Complete the manual interaction before finishing this stage."+(DiagnosticRequested ? $" elapsed={Time.realtimeSinceStartup-lease.Started}, owner={lease.Owner}" : ""));
    }
    private static void Require(MachineState machine,string kind,MachineStage stage)
    { if (machine.Kind!=kind || machine.Stage!=stage) throw new InvalidOperationException("The machine's stage changed. Reopen it to continue."); }
    internal void ReceiveState(WorkshopState state, bool initialSnapshot=false)
    {
        if (!initialSnapshot && state.Revision<State.Revision) return;var introduced=State.Introduced;State=state;
        StudioAssets.UpdateArtwork(State);if (!introduced && State.Introduced) World?.Introduce();World?.RefreshMachines(State);Panel?.Refresh();
    }
    internal void Reply(StudioReply reply) { Panel?.OnReply(reply);CollectiveCatalogue.OnReply(reply);ShowMessage(reply.Message); }
    internal void ShowMessage(string message) { if (message.Length>0) Panel?.ShowMessage(message); }
    private void ClearSession()
    {
        Ready=false;Panel?.Dispose();Panel=null;Delivery?.Dispose();Delivery=null;World?.Dispose();World=null;Diagnostic=null;
        Leases.Clear();WorkshopNetwork.Reset();CollectiveCatalogue.Reset();StudioTutorial.Current=null;State=new();SaveTarget=SaveSnapshot="";
    }
    public void OnSceneWasUnloaded(int buildIndex,string sceneName) { if (sceneName=="Main") ClearSession(); }
    public void OnApplicationQuit()=>ClearSession();
    public void OnDeinitializeMelon()
    {
        BuildEvents.OnBuildableItemInitialized-=RestoreFurniture;
        GameLifecycle.OnPreLoad-=Preload;GameLifecycle.OnLoadComplete-=Loaded;
        GameLifecycle.OnPreSceneChange-=ClearSession;GameLifecycle.OnSaveComplete-=SaveComplete;
        ClearSession();Current=null;
    }
}
