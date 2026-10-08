using PersonalMods.LSDGameplay;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (InvalidDataException) { return; } catch (InvalidOperationException) { return; }
    throw new Exception(message);
}
SynchronousPacketPayloadTests.Run(Check);
InitialStateSyncTests.Run(Check);
var state = new WorkshopState();
var cabinet=new CabinetSequence();
cabinet.Tick(10);
Check(cabinet.Step==CabinetStep.OpenDoor && cabinet.Door==0 && cabinet.Tray==0,"Cabinet advanced without a physical action.");
foreach(var step in new[]{CabinetStep.OpenDoor,CabinetStep.PullTray,CabinetStep.TransferSheet,CabinetStep.PushTray,CabinetStep.CloseDoor}){
    Check(cabinet.Step==step && !cabinet.ReadyToCommit,"Cabinet skipped an interaction or committed early.");
    cabinet.Advance();cabinet.Tick(0.2f);var progress=cabinet.Progress;cabinet.Advance();
    Check(cabinet.Progress==progress && cabinet.Moving,"Repeated input restarted an animation.");
    if(step==CabinetStep.OpenDoor)Check(cabinet.Door>0 && cabinet.Door<1 && cabinet.Tray==0,"Tray moved through the closed door.");
    if(step==CabinetStep.PullTray)Check(cabinet.Door==1 && cabinet.Tray>0 && cabinet.Transfer==0,"Sheet moved before the tray opened.");
    if(step==CabinetStep.TransferSheet)Check(cabinet.Door==1 && cabinet.Tray==1 && cabinet.Transfer>0,"Sheet transfer did not keep the door/tray open.");
    if(step==CabinetStep.CloseDoor)Check(cabinet.Tray==0 && cabinet.Door<1 && !cabinet.ReadyToCommit,"Closing committed before the door was shut.");
    cabinet.Tick(2);
}
Check(cabinet.ReadyToCommit && cabinet.Door==0 && cabinet.Tray==0 && cabinet.Transfer==1,"Cabinet transaction was ready with an open door or tray.");
cabinet.Advance();cabinet.Tick(10);
Check(cabinet.Step==CabinetStep.Commit && !cabinet.Moving,"Cabinet repeated a completed transaction.");
var pausedCabinet=new CabinetSequence();pausedCabinet.Advance();pausedCabinet.Tick(float.NaN);pausedCabinet.Tick(-1);
Check(pausedCabinet.Progress==0,"Invalid frame timing advanced the cabinet.");
var quickCut=new CutStroke(0,0.8f,1,0.8f);
quickCut.Sample(0,0.8f,true,0.016f);quickCut.Sample(1.7f,0.8f,true,0.016f);
Check(quickCut.Complete,"A fast cut beyond the right edge lost its endpoint.");
Check(Math.Abs(quickCut.Accuracy-1)<0.001f,"Accurate endpoint overshoot was treated as an off-line cutting mistake.");
var releasedCut=new CutStroke(0,0,0,1);
releasedCut.Sample(0,0,true,0.016f);releasedCut.Sample(0,0.4f,true,0.016f);releasedCut.Sample(0,1.4f,false,0.016f);
Check(releasedCut.Complete,"The release frame dropped a cut that crossed the endpoint.");
var resumedCut=new CutStroke(0,0,0,1);
resumedCut.Sample(0,0,true,0.016f);resumedCut.Sample(0,0.65f,true,0.016f);resumedCut.Sample(0,0.65f,false,0.016f);
resumedCut.Sample(0,0.65f,true,0.016f);resumedCut.Sample(0,1,true,0.016f);
Check(resumedCut.Complete,"Interrupted cutting could not resume from the current point.");
var untouchedCut=new CutStroke(0,0,1,0);
untouchedCut.Sample(1,0,true,0.016f);
Check(!untouchedCut.Started,"A fresh cut could be skipped by clicking its endpoint.");
var pixels = Enumerable.Repeat((byte)3, 4096).ToArray();
var custom = state.AddArt("First edition", pixels, false);
pixels[0] = 6;
Check(custom.Pixels[0] == 3, "Saved drawing aliased the editor buffer.");
var later = state.AddArt("Second edition", pixels, true);
Check(later.Slot != custom.Slot && custom.Pixels[0] == 3, "Editing repainted existing inventory's revision.");
state.Machines.Add(new() { Id = Guid.NewGuid().ToString(), Property = "seweroffice", Kind = "cabinet", Stage = MachineStage.Drying, ArtSlot = custom.Slot, Yield = 37, DueMinute = 1520 });
state.Orders.Add(new() { Id = Guid.NewGuid().ToString(), Quantity = 4, Shop = "hardware", DueMinute = 1480, Stage = OrderStage.Delivered });
var restored = WorkshopState.Deserialize(state.Serialize());
state.TutorialStep=7;
Check(WorkshopState.Deserialize(state.Serialize()).TutorialStep==7,"Tutorial progress did not survive saving.");
Check(WorkshopState.Deserialize("{\"Version\":1}").TutorialStep==0,"An existing first-slice save did not default its new tutorial state.");
Reject(()=>new WorkshopState{TutorialStep=11}.Validate(),"Invalid tutorial stage accepted.");
var tutorial=new WorkshopState{Introduced=true};
Check(Enumerable.Range(0,TutorialProgress.Count).Where(i=>TutorialProgress.Active(tutorial,i)).SequenceEqual(new[]{0,1,2,3}),"Starting supplies did not appear as four separate checklist entries.");
TutorialProgress.Record(tutorial,TutorialObjective.BuyInk);
TutorialProgress.Record(tutorial,TutorialObjective.BuyPaper);
Check(!TutorialProgress.Active(tutorial,2) && !TutorialProgress.Active(tutorial,3) && TutorialProgress.Active(tutorial,0),"Buying supplies out of order completed the wrong entries.");
tutorial=WorkshopState.Deserialize(tutorial.Serialize());
Check(TutorialProgress.Has(tutorial,TutorialObjective.BuyInk) && TutorialProgress.Has(tutorial,TutorialObjective.BuyPaper),"Individual supply completion was lost on reload.");
TutorialProgress.Record(tutorial,TutorialObjective.BuyBench);TutorialProgress.Record(tutorial,TutorialObjective.BuyCabinet);
Check(TutorialProgress.Active(tutorial,4) && TutorialProgress.Active(tutorial,5),"Placement was not split into two entries.");
TutorialProgress.Record(tutorial,TutorialObjective.PlaceCabinet);
Check(TutorialProgress.Active(tutorial,4) && !TutorialProgress.Active(tutorial,5),"Placing one piece of equipment completed both tasks.");
TutorialProgress.Record(tutorial,TutorialObjective.PlaceBench);
Check(TutorialProgress.Active(tutorial,6),"Placing equipment did not expose the order task.");
TutorialProgress.RecordAction(tutorial,"order");
Check(TutorialProgress.Active(tutorial,7) && !TutorialProgress.Has(tutorial,TutorialObjective.CollectPrism),"Ordering also completed collection.");
TutorialProgress.Record(tutorial,TutorialObjective.CollectPrism);
TutorialProgress.RecordAction(tutorial,"start");
Check(TutorialProgress.Active(tutorial,9) && !TutorialProgress.Has(tutorial,TutorialObjective.RegisterSheet),"Loading a batch also completed registration.");
TutorialProgress.RecordAction(tutorial,"prepare");
Check(TutorialProgress.Active(tutorial,10) && !TutorialProgress.Has(tutorial,TutorialObjective.PrintSheet),"Registration also completed printing.");
TutorialProgress.RecordAction(tutorial,"print");
Check(TutorialProgress.Active(tutorial,11),"Printing skipped wet sheet collection.");
TutorialProgress.RecordAction(tutorial,"collect",MachineStage.WetOutput);
Check(TutorialProgress.Active(tutorial,12),"Collecting a wet sheet skipped loading the cabinet.");
TutorialProgress.RecordAction(tutorial,"dry");
Check(TutorialProgress.Active(tutorial,13) && !TutorialProgress.Has(tutorial,TutorialObjective.CollectDrySheet),"Loading a cabinet also completed dry sheet collection.");
TutorialProgress.RecordAction(tutorial,"collect",MachineStage.DryOutput);
Check(TutorialProgress.Active(tutorial,14),"Collecting a ready sheet skipped loading the cutter.");
TutorialProgress.RecordAction(tutorial,"cut");
Check(TutorialProgress.Active(tutorial,15),"Loading the cutter also completed cutting.");
TutorialProgress.RecordAction(tutorial,"finishcut");
Check(TutorialProgress.Active(tutorial,16),"Cutting skipped tab collection.");
TutorialProgress.RecordAction(tutorial,"collect",MachineStage.TabOutput);
Check(TutorialProgress.Active(tutorial,17) && !TutorialProgress.Finished(tutorial),"Collecting loose tabs completed packaging.");
Check(!TutorialProgress.RecordAction(tutorial,"pack"),"Retired bench packaging completed the tutorial.");
TutorialProgress.Record(tutorial,TutorialObjective.PackageTabs);
Check(TutorialProgress.Finished(tutorial) && tutorial.TutorialStep==10,"Packaging did not finish the tutorial.");
Check(!TutorialProgress.Record(tutorial,TutorialObjective.BuyPaper),"A repeated observation changed completed tutorial progress.");
var earlyPrint=new WorkshopState{Introduced=true};TutorialProgress.RecordAction(earlyPrint,"start");
Check(TutorialProgress.Has(earlyPrint,TutorialObjective.BuyPaper) && !TutorialProgress.Has(earlyPrint,TutorialObjective.BuyCabinet) && !TutorialProgress.Has(earlyPrint,TutorialObjective.PlaceCabinet),"A consumed batch forgot its supplies or invented cabinet ownership.");
Check(earlyPrint.TutorialStep==1,"Loading a batch without a cabinet skipped legacy prerequisite tasks.");
var orderFirst=new WorkshopState{Introduced=true};
TutorialProgress.RecordAction(orderFirst,"order");TutorialProgress.Record(orderFirst,TutorialObjective.CollectPrism);
Check(orderFirst.TutorialStep==1,"Collecting Prism before setup advanced the coarse legacy milestone.");
orderFirst=WorkshopState.Deserialize(orderFirst.Serialize());
Check(orderFirst.TutorialStep==1 && TutorialProgress.Has(orderFirst,TutorialObjective.CollectPrism),"Order-first progress did not retain its independent completion and safe legacy milestone after reload.");
foreach(var objective in new[]{TutorialObjective.BuyBench,TutorialObjective.BuyCabinet,TutorialObjective.BuyPaper,TutorialObjective.BuyInk})TutorialProgress.Record(orderFirst,objective);
Check(orderFirst.TutorialStep==2,"Purchases completed without placement skipped the legacy setup task.");
TutorialProgress.Record(orderFirst,TutorialObjective.PlaceCabinet);
Check(orderFirst.TutorialStep==2,"Placing only the cabinet advanced the legacy setup milestone.");
TutorialProgress.Record(orderFirst,TutorialObjective.PlaceBench);
Check(orderFirst.TutorialStep==4,"Closing the prerequisite gap lost already completed Prism collection.");
var inflated=WorkshopState.Deserialize("{\"Version\":1,\"Introduced\":true,\"TutorialStep\":4,\"TutorialObjectives\":192}");
Check(inflated.TutorialStep==1 && TutorialProgress.Has(inflated,TutorialObjective.CollectPrism),"An inflated prior 0.2.5 milestone was not repaired from its saved objective mask.");
// Every saved legacy milestone must imply only objectives actually completed,
// including masks representing arbitrary out-of-order checklist completion.
for(var mask=0;mask<=TutorialProgress.All;mask++){
    var implied=TutorialProgress.FromLegacy(TutorialProgress.LegacyStep(mask));
    Check((mask&implied)==implied,"A coarse milestone invented prerequisite completion.");
}
for(var legacy=0;legacy<=10;legacy++){
    var migrated=WorkshopState.Deserialize($"{{\"Version\":1,\"Introduced\":true,\"TutorialStep\":{legacy}}}");
    Check(migrated.TutorialObjectives==TutorialProgress.FromLegacy(legacy),"Legacy tutorial progress migrated incorrectly.");
    Check(WorkshopState.Deserialize(migrated.Serialize()).TutorialObjectives==migrated.TutorialObjectives,"Migrated objectives did not survive another reload.");
}
var oldDrying=WorkshopState.Deserialize("{\"Version\":1,\"Introduced\":true,\"TutorialStep\":7}");
Check(TutorialProgress.Active(oldDrying,13) && !TutorialProgress.Has(oldDrying,TutorialObjective.CollectDrySheet),"A legacy drying save was restarted or claimed its dry sheet had been collected.");
Reject(()=>new WorkshopState{TutorialObjectives=1<<TutorialProgress.Count}.Validate(),"Unknown tutorial objective bits accepted.");
Reject(()=>new WorkshopState{TutorialObjectives=-1}.Validate(),"Negative tutorial progress accepted.");
Check(!restored.Tick(1519), "Drying completed early.");
Check(restored.Tick(3000) && restored.Machines[0].Stage == MachineStage.DryOutput, "Sleeping/time skip did not finish drying.");
Check(!restored.Tick(4000) && restored.Machines[0].Yield == 37, "Ready output duplicated or gained quality/yield.");
Check(restored.Orders[0].Stage == OrderStage.Delivered, "Delivered order was reset after load.");
Check(restored.Artworks.Single(a => a.Slot == custom.Slot).Pixels[0] == 3, "Artwork did not survive JSON round trip.");
for (var i = 0; i < 101; i++) {
    var a = i / 100f;
    var yield = 40 - WorkshopRules.Loss(a, 2) - WorkshopRules.Loss(a, 2) - WorkshopRules.Loss(a, 4);
    Check(yield is >= 32 and <= 40, "Manual losses exceeded the bounded allowance.");
}
foreach (var dry in new[] { false, true }) for (var slot = 0; slot < 8; slot++) for (var yield = 36; yield <= 40; yield++) {
    Check(WorkshopRules.TrySheet(WorkshopRules.Sheet(dry, slot, yield), out var d, out var s, out var count)
        && d == dry && s == slot && count == yield, "Physical sheet lost its edition or tab count.");
}
Check(!WorkshopRules.TrySheet("personal.lsd:wet-0-99", out _, out _, out _), "Invalid intermediate yield accepted.");
var unsupported = new WorkshopState { Version = 2 };
Reject(unsupported.Validate, "Future save format accepted.");
Reject(() => WorkshopState.Deserialize(new string('x', WorkshopRules.MaximumPayload+1)), "Oversized snapshot accepted.");
var bad = new WorkshopState(); bad.Artworks[0].Pixels[0] = 9;
Reject(bad.Validate, "Unknown palette index accepted.");
var dup = new WorkshopState(); dup.Artworks.Add(dup.Artworks[0]);
Reject(dup.Validate, "Duplicate immutable art slot accepted.");
while (state.Artworks.Count < 8) state.AddArt("Revision", new byte[4096], false);
Reject(() => state.AddArt("Overflow", new byte[4096], false), "Art slot was reused after reaching capacity.");
Console.WriteLine("Passed tutorial checklists, task transitions, legacy migration, workshop persistence, immutable artwork, time skips, sheet identity, bounded yield and corrupt-save checks.");
