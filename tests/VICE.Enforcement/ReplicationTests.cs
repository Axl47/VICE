using VICE.Enforcement.Multiplayer;
using Mono.Cecil;

internal static class ReplicationTests
{
    internal static void Run(Action<bool, string> check, string? assembly)
    {
        check(ReplicationRules.RayFraction(0, 3) == 0 && ReplicationRules.RayFraction(1, 3) == 0.5f && ReplicationRules.RayFraction(2, 3) == 1, "Camera rays must sample eyes, midpoint and center without integer division.");
        check(ReplicationRules.InView(20, 55, 20, false), "Camera range/cone boundary must remain visible.");
        check(!ReplicationRules.InView(21, 0, 20, false), "Cameras cannot detect beyond activation range.");
        check(!ReplicationRules.InView(5, 56, 20, false), "Directional cameras must reject targets outside the cone.");
        check(ReplicationRules.InView(5, 180, 20, true), "Omnidirectional cameras must cover all angles.");
        check(!ReplicationRules.InView(float.NaN, 0, 20, true), "Non-finite camera observations must be rejected.");
        check(ReplicationRules.NormalizeLabels(new[] { "Visible", "DrugDealing", "DrugDealing", "unknown" }).SequenceEqual(new[] { "DrugDealing" }), "Remote visibility labels must be allowlisted and deduplicated.");
        var host = new CameraEvidence(); var guest = new CameraEvidence();
        check(!host.Step(0, 1, true, true, new[] { "DrugDealing" }, 2, 30), "Partial camera detection must not dispatch prematurely.");
        check(host.Step(1, 1, true, true, new[] { "DrugDealing" }, 2, 30), "Sustained sight must trigger the threshold once.");
        check(!host.Step(2, 2, true, true, new[] { "Wanted" }, 2, 30), "The same player must respect camera cooldown.");
        check(guest.Step(2, 2, true, true, new[] { "Vandalizing" }, 2, 30), "Host cooldown must not hide a second player from the camera.");
        check(!guest.Labels.Contains("DrugDealing") && !host.Labels.Contains("Vandalizing"), "Evidence cannot leak between players.");
        var hidden = new CameraEvidence(); hidden.Step(1, 1, true, true, Array.Empty<string>(), 2, 30);
        hidden.Step(2, 1, false, false, Array.Empty<string>(), 2, 30);
        check(hidden.Hits == 0, "Lost sight must decay incomplete detection.");
        var dark = new CameraEvidence(); check(!dark.Step(1, 5, true, false, new[] { "Wanted" }, 2, 30), "Low visibility cannot advance detection when the visibility roll fails.");
        host.Step(200, 0.1f, false, false, Array.Empty<string>(), 2, 30);
        check(host.Labels.Count == 0, "Old camera evidence must expire.");
        var state = new WorldState { Session = Guid.NewGuid().ToString("N"), NoticeSpeed = 2 };
        check(ReplicationRules.Valid(state), "Empty initial world snapshot must be accepted.");
        state.Officers.Add(new() { Id = "NACop_Investigator", ObjectId = 12, Guid = Guid.NewGuid().ToString(), Naked = "{}" });
        check(ReplicationRules.Valid(state), "Investigator identity must be accepted independently of a native donor ID.");
        state.Officers[0].Id = "officer01";
        check(!ReplicationRules.Valid(state), "Snapshots cannot replace a native donor officer.");
        state.Officers[0].Id = "NACop_Investigator";
        state.Cameras.Add(new() { Id = "camera", Target = new[] { 1f, 2f, float.PositiveInfinity } });
        check(!ReplicationRules.Valid(state), "Non-finite camera target positions must be rejected.");
        state.Cameras.Clear(); state.Notices.Add(new() { Player = "guest", Labels = new[] { "arbitrary-crime" } });
        check(!ReplicationRules.Valid(state), "Guest crime notices cannot invent crime labels.");
        if (assembly is null) return;
        using var mod = AssemblyDefinition.ReadAssembly(assembly);
        var clone = mod.MainModule.Types.Single(t => t.Name == "CopInitHelper");
        var nested = clone.NestedTypes.Single(t => t.Name.Contains("InitiateClone"));
        var next = nested.Methods.Single(m => m.Name == "MoveNext");
        check(!next.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Preinitialize_Internal"), "Runtime officers must not share a hard-coded preinitialization ID.");
        check(!next.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Initialize" && m.DeclaringType.Name == "NetworkObject"), "Native FishNet spawning must own network initialization.");
        var camera = mod.MainModule.Types.Single(t => t.Name == "HylandFlockInstance");
        check(!camera.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "Player" && m.Name == "get_Local"), "Camera detection must use a specific player rather than hard-code the host.");
        var render = mod.MainModule.Types.Single(t => t.Name == "RuntimeImpostor").Methods.Single(m => m.Name == "CreateImpostor");
        check(!render.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "PoliceOfficer" && m.Name.Contains("Movement")), "Impostor capture must not change network NPC movement.");
    }
}
