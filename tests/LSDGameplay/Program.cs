using PersonalMods.LSDGameplay;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (InvalidDataException) { return; } catch (InvalidOperationException) { return; }
    throw new Exception(message);
}
var state = new WorkshopState();
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
Console.WriteLine("Passed workshop persistence, immutable artwork, time skips, sheet identity, bounded yield and corrupt-save checks.");
