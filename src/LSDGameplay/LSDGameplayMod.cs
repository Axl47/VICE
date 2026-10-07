using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using MelonLoader;
using S1API.Lifecycle;
using S1API.Products;
using UnityEngine;
using UnityEngine.InputSystem;
using Clock = Il2CppScheduleOne.GameTime.TimeManager;
using Money = S1API.Money.Money;

[assembly: MelonInfo(typeof(PersonalMods.LSDGameplay.LSDGameplayMod), "Personal LSD Workshop", "0.2.3", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.LSDGameplay;

public sealed class LSDGameplayMod : MelonMod
{
    internal static LSDGameplayMod? Current;
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

    public override void OnInitializeMelon()
    {
        Current=this;
        if (DiagnosticRequested) Application.runInBackground=true;
        CustomProductSaveProviderRegistry.Register(new EditionSaveProvider());
        GameLifecycle.OnPreLoad+=Preload;
        GameLifecycle.OnLoadComplete+=Loaded;
        GameLifecycle.OnPreSceneChange+=ClearSession;
        GameLifecycle.OnSaveComplete+=SaveComplete;
        ModSupport.Patch(HarmonyInstance,typeof(SaveManager),"Save",typeof(LSDGameplayMod),prefix:nameof(SaveStarted),parameters:new[] { typeof(string) });
        WorkshopWorld.Install(HarmonyInstance);
        StudioPackaging.Install(HarmonyInstance);
        LoggerInstance.Msg("Off Register workshop registered. Use the sticker inside your owned Sewer Office; test with save slot five.");
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
        catch (Exception error) { LoadFailed=true;Ready=false;LoggerInstance.Error("Workshop load stopped without changing its save: "+error); }
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
            if(State.TutorialStep<10 && S1API.Quests.QuestManager.GetQuestByName("First edition") is null)
                S1API.Quests.QuestManager.CreateQuest<StudioTutorial>();
            if (!WorkshopNetwork.Host) WorkshopNetwork.Submit(new StudioCommand());
            if (DiagnosticRequested && WorkshopNetwork.Host) Diagnostic=new SliceDiagnostic(this);
            LoggerInstance.Msg($"LSD_WORKSHOP_READY editions={State.Artworks.Count}, machines={State.Machines.Count}, orders={State.Orders.Count}.");
        }
        catch (Exception error) { Ready=false;LoggerInstance.Error("Workshop initialization failed: "+error); }
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
            LoggerInstance.Msg($"LSD_WORKSHOP_SAVED revision={State.Revision}.");
        }
        catch (Exception error) { LoggerInstance.Error("Native save completed, but the workshop state could not be saved: "+error);ShowMessage("Workshop saving failed. Check the mod log before leaving this session."); }
        finally { SaveTarget=SaveSnapshot=""; }
    }

    public override void OnUpdate()
    {
        try
        {
            if (DiagnosticRequested && !Ready) SliceDiagnostic.TryLoadFifthSlot();
            if (!Ready || !LoadManager.Instance || !LoadManager.Instance.IsGameLoaded || LoadManager.Instance.IsLoading) return;
            if(Panel?.IsOpen!=true && Keyboard.current?.f9Key.wasPressedThisFrame==true && World?.Office
                && World!.Office!.DoBoundsContainPoint(Player.Local.transform.position))
                WorkshopNetwork.Submit(new StudioCommand{Action="testfinish"});
            Panel?.Tick();World?.Tick();Diagnostic?.Tick();CollectiveCatalogue.Tick();
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
            if (!Ready || !WorkshopNetwork.Host || !player || !player.PlayerLoaded || player.IsUnconscious || !player.Health.IsAlive)
                throw new InvalidOperationException("The workshop is unavailable.");
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
                committed=true;
                World.Notify(reply.Message);
            }
            else if(command.Action=="testfinish")
            {
                if(!player.IsLocalPlayer || !office.DoBoundsContainPoint(player.transform.position))
                    throw new InvalidOperationException("The temporary F9 test skip is available to the host inside the Sewer Office.");
                foreach(var machine in State.Machines){
                    if(machine.Stage==MachineStage.Drying){machine.DueMinute=Minute;machine.Stage=MachineStage.DryOutput;}
                    if(machine.Id==command.Machine && machine.Stage is MachineStage.Preparing or MachineStage.Printing)machine.Stage=MachineStage.WetOutput;
                    if(machine.Id==command.Machine && machine.Stage==MachineStage.Cutting)machine.Stage=MachineStage.TabOutput;
                }
                State.Revision++;reply.Message="Test skip: current manual stage and cabinet drying completed. Collect output normally.";
            }
            else if (command.Action=="release")
            { if (Leases.TryGetValue(command.Machine,out var lease) && lease.Owner==player.PlayerCode) Leases.Remove(command.Machine); }
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
                switch (command.Action)
                {
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
                        if (machine.Kind!="bench" || command.Quantity is not (1 or 5 or 20) || State.Artworks.All(a=>a.Slot!=command.ArtSlot))
                            throw new InvalidOperationException("Choose an edition and a package size at the bench.");
                        var productId=WorkshopRules.Product(command.ArtSlot);
                        var loose=transaction.Items.FirstOrDefault(i=>i.ID==productId && i.TryCast<ProductItemInstance>() is { } p && !p.AppliedPackaging)?.TryCast<ProductItemInstance>()
                            ?? throw new InvalidOperationException("Carry loose tabs of the selected edition.");
                        transaction.Remove(productId,command.Quantity,i=>i.TryCast<ProductItemInstance>() is { } p && !p.AppliedPackaging && p.Quality==loose.Quality);
                        var container=command.Quantity switch { 1=>"baggie",5=>"jar",_=>WorkshopRules.Sleeve };
                        transaction.Remove(container,1);
                        transaction.Add(Content.Create(productId,1,command.Quantity switch { 1=>"baggie",5=>"jar",_=>WorkshopRules.Sleeve },(int)loose.Quality));
                        reply.Message=$"Packaged {command.Quantity} matching tabs. Native unpacking preserves the edition and quantity.";break;
                    default: throw new InvalidOperationException("Unknown studio action.");
                }
                State.Revision++;State.Validate();
                reply.Inventory=transaction.Commit();committed=true;
            }
            State.Validate();StudioAssets.UpdateArtwork(State);reply.Success=true;
            reply.State=State.Serialize();World?.RefreshMachines(State);WorkshopNetwork.Publish();
        }
        catch (Exception error)
        {
            if (committed)
            {
                reply.Success=true;reply.State=State.Serialize();reply.Message="Action completed. The display could not refresh; close and reopen the studio.";
                LoggerInstance.Error("Studio presentation failed after a committed action: "+error);return reply;
            }
            State=WorkshopState.Deserialize(prior);reply.State=prior;
            if (error is InvalidOperationException or InvalidDataException or ArgumentException) reply.Message=error.Message;
            else { LoggerInstance.Error("Studio action failed: "+error);reply.Message="The studio action failed. Your batch is held; check the mod log before trying again."; }
        }
        return reply;
    }

    private void Acquire(MachineState machine,string owner,string action)
    {
        if (DiagnosticRequested) LoggerInstance.Msg($"LSD_LEASE action={action}, machine={machine.Id}, owner={owner}, exists={Leases.ContainsKey(machine.Id)}, elapsed={(Leases.TryGetValue(machine.Id,out var observed) ? Time.realtimeSinceStartup-observed.Started : -1)}, sameOwner={(Leases.TryGetValue(machine.Id,out observed) && observed.Owner==owner)}.");
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
    internal void ReceiveState(WorkshopState state)
    {
        if (state.Revision<State.Revision) return;var introduced=State.Introduced;State=state;
        StudioAssets.UpdateArtwork(State);if (!introduced && State.Introduced) World?.Introduce();World?.RefreshMachines(State);Panel?.Refresh();
    }
    internal void Reply(StudioReply reply) { Panel?.OnReply(reply);ShowMessage(reply.Message); }
    internal void ShowMessage(string message) { if (message.Length>0) Panel?.ShowMessage(message); }
    private void ClearSession()
    {
        Ready=false;Panel?.Dispose();Panel=null;Delivery?.Dispose();Delivery=null;World?.Dispose();World=null;Diagnostic=null;
        Leases.Clear();WorkshopNetwork.Reset();StudioTutorial.Current=null;State=new();SaveTarget=SaveSnapshot="";
    }
    public override void OnSceneWasUnloaded(int buildIndex,string sceneName) { if (sceneName=="Main") ClearSession(); }
    public override void OnApplicationQuit()=>ClearSession();
}
