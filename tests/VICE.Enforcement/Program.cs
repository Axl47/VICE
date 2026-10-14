using System.Text.Json;
using VICE.Enforcement.Information;
using PersonalMods.Networking;
using Mono.Cecil;

var assertions = 0;
void Check(bool result, string message) { assertions++; if (!result) throw new Exception(message); }
var names = new Dictionary<string, string> { ["bungalow"] = "Bungalow", ["barn"] = "Barn" };
PressureReports New(int heat = 0) { var p = new PressureReports(new(), 180); p.Start(100, new[] { ("bungalow", heat), ("barn", 0) }); return p; }
var policy = New(20);
Check(policy.Take(names, false) is null, "Initial high heat must be a silent baseline.");
policy.Observe("bungalow", 11);
Check(policy.Take(names, false) is null, "Heavy band must retain hysteresis above 10.");
policy.Observe("bungalow", 10);
Check(policy.Take(names, false)?.Text.Contains("eased") == true, "Settling to attention must send appropriate wording.");
policy.Observe("bungalow", 3); policy.Advance(279);
Check(policy.Take(names, false) is null, "Global cooldown must gate quiet reports.");
policy.Advance(280);
Check(policy.Take(names, false)?.Text.Contains("quiet") == true, "Real calm transition should be delivered at cooldown expiry.");
Check(policy.Take(names, false) is null, "A calm state must not report repeatedly.");
policy = New(); policy.Observe("bungalow", 6);
Check(policy.Take(names, false)?.Text.Contains("getting attention") == true, "Meaningful rise should report.");
policy.Observe("bungalow", 4); policy.Advance(300);
Check(policy.Take(names, false) is null, "Attention hysteresis must retain 4–5 heat.");
policy.Observe("bungalow", 14); policy.Observe("bungalow", 10);
Check(policy.Take(names, false) is null, "Rise then return to last reported band must cancel pending warning.");
policy.Observe("bungalow", 14); policy.Observe("barn", 14);
Check(policy.Take(names, true) is null, "No reports during an active raid.");
Check(policy.Take(names, false) is not null, "Priority warning should release after raid.");
Check(policy.Take(names, false) is null, "Multiple properties cannot bypass global cooldown.");
policy.Advance(480);
Check(policy.Take(names, false) is not null, "Second property remains pending and eventually delivers.");
policy = New(); policy.Observe("bungalow", 6); policy.Take(names, false);
policy.Observe("bungalow", 14); policy.Advance(150);
var restored = JsonSerializer.Deserialize<ReportState>(JsonSerializer.Serialize(policy.State))!;
var reload = new PressureReports(restored, 180); reload.Start(10, new[] { ("bungalow", 14), ("barn", 0) });
reload.Advance(139);
Check(reload.Take(names, false) is null, "Reload/rewound clock cannot bypass remaining cooldown.");
reload.Advance(140);
Check(reload.Take(names, false) is not null, "Pending warning and cooldown must survive serialization.");
reload.Baseline("bungalow", 0); reload.Advance(500);
Check(reload.Take(names, false) is null, "Raid heat reset must not imply quiet.");
policy = New(); policy.Observe("bungalow", 6); policy.Observe("bungalow", 3);
Check(policy.Take(names, false) is null, "Unreported transient pressure must not produce a quiet message.");
policy = New(14); policy.Observe("bungalow", 3); policy.Observe("barn", 6);
Check(policy.Take(names, false)?.Text.StartsWith("Barn") == true, "A rise takes priority over a calming report.");
policy.Observe("new-property", 20);
Check(policy.State.Properties["new-property"].Current == policy.State.Properties["new-property"].Reported, "New property must establish silent baseline.");
policy = New(); policy.Observe("bungalow", 6); policy.Take(names, false); policy.Observe("bungalow", 14);
reload = new(JsonSerializer.Deserialize<ReportState>(JsonSerializer.Serialize(policy.State))!, 180);
reload.Start(200, new[] { ("bungalow", 0), ("barn", 0) }); reload.Advance(500);
Check(reload.Take(names, false) is null, "Reload with rolled-back heat silently reconciles stale pending data.");
Check(ReportStore.FileName(1, "A B") != ReportStore.FileName(1, "a_b"), "Organization identity cannot collide after sanitization.");
Check(ReportStore.FileName(1, "A") != ReportStore.FileName(2, "A"), "Save slots must remain isolated.");
policy.State.Properties["not-owned"] = new() { Current = Pressure.Heavy, Reported = Pressure.Quiet };
policy.Start(100, new[] { ("bungalow", 14), ("barn", 0) });
Check(!policy.State.Properties.ContainsKey("not-owned"), "A load must discard properties outside its current heat/ownership snapshot.");
Check(policy.Take(new Dictionary<string, string>(), false) is null, "A report must never reveal an unowned/unresolved property.");
var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "vice-information-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var path = Path.Combine(root, ReportStore.FileName(1, "test"));
    ReportStore.Write(path, policy.State);
    Check(ReportStore.Read(path).History.Single().Id == policy.State.History.Single().Id, "Disk round-trip must preserve delivered identity.");
    ReportStore.Write(path, restored);
    Check(!File.Exists(path + ".tmp"), "Atomic replacement should finish cleanly.");
    Check(ReportStore.Read(Path.Combine(root, "absent.json")).History.Count == 0, "Unseen save must have empty history.");
    File.WriteAllText(path, "{\"Version\":99}");
    try { ReportStore.Read(path); Check(false, "Unknown format must be rejected."); } catch (InvalidDataException) { assertions++; }
}
finally { Directory.Delete(root, true); }
var wire = new SynchronousPacketPayload(16000);
var text = JsonSerializer.Serialize(restored.History);
wire.Send(text, () => wire.Write(value => wire.Read(() => value)));
Check(wire.TryTake(out var decoded) && decoded == text, "Wire contract must preserve the report snapshot.");
wire.Read(() => text); wire.Reset();
Check(!wire.TryTake(out _), "Session cleanup must discard received packets.");
if (args.Length == 2)
{
    using var mod = AssemblyDefinition.ReadAssembly(args[0]);
    using var game = AssemblyDefinition.ReadAssembly(Path.Combine(args[1], "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));
    var contact = mod.MainModule.Types.Single(t => t.Name == "InformationContact");
    var end = contact.Methods.Single(m => m.Name == "End");
    Check(end.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "ReportNetwork" && m.Name == "End"), "Session exit must release report networking.");
    var exit = mod.MainModule.Types.Single(t => t.FullName == "NACops.NACops").Methods.Single(m => m.Name == "ExitPreTask");
    Check(exit.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "InformationContact" && m.Name == "End"), "Every existing exit hook must clean contact state.");
    var render = contact.Methods.Single(m => m.Name == "Render");
    Check(render.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "SendMessage"), "Reports must use native message UI.");
    foreach (var (type, method, count) in new[] { ("MSGConversation", "SendMessage", 3), ("MessageContactInfo", ".ctor", 5), ("TimeManager", "GetTotalMinSum", 0) })
        Check(game.MainModule.Types.Single(t => t.Name == type).Methods.Any(m => m.Name == method && m.Parameters.Count == count), "Required native API is missing: " + type + "." + method);
}
ReplicationTests.Run(Check, args.Length > 0 ? args[0] : null);
Console.WriteLine($"PASS: {assertions} Enforcement pressure, multiplayer, camera geometry, persistence, wire and lifecycle/API assertions.");
