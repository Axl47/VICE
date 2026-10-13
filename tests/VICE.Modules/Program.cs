using System.Reflection;
using System.Reflection.Emit;
using Mono.Cecil;
using VICE.Core;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

Check(ModuleCompatibility.CanInitialize("Test", "Vice.Tests.Absent"), "An absent predecessor must allow initialization.");
AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Vice.Tests.Legacy"), AssemblyBuilderAccess.Run);
Check(!ModuleCompatibility.CanInitialize("Test", "vice.tests.legacy"), "Predecessor checks must ignore case.");
Check(MelonLoader.MelonLogger.LastError.Contains("Vice.Tests.Legacy"), "Conflict errors must identify the predecessor.");
Check(ModuleCompatibility.CanInitialize("Test", "Vice.Tests.Other"), "Unrelated assemblies must not block a module.");

// Bound native UI work at different render rates and avoid catch-up bursts.
foreach (var fps in new[] { 30, 60, 144, 240 })
{
    var budget = new VICE.Interface.Hud.RefreshBudget(0.2f);
    var refreshes = 0;
    for (var frame = 0; frame < fps * 10; frame++) if (budget.Take((float)frame / fps)) refreshes++;
    Check(refreshes >= 44 && refreshes <= 51, $"UI refreshes remain bounded at {fps} FPS: {refreshes}.");
    Check(budget.Take(100), "Returning after a hidden interval refreshes once.");
    Check(!budget.Take(100) && !budget.Take(100.01f), "Returning does not trigger catch-up work.");
    budget.Reset();
    Check(budget.Take(100.01f), "Visibility reset refreshes immediately.");
    Check(!budget.Take(float.NaN) && !budget.Take(float.PositiveInfinity), "Invalid clocks cannot poison the schedule.");
    Check(budget.Take(101), "The schedule recovers after invalid clocks.");
}

if (args.Length < 1) throw new ArgumentException("Pass the repository directory and optional module names to inspect built DLLs.");
var repository = Path.GetFullPath(args[0]);
var callbacks = new Dictionary<string, (string Callback, string Feature, string Method)[]>
{
    ["Commerce"] = new[]
    {
        ("OnInitializeMelon", "TradingFeature", "Initialize"),
        ("OnInitializeMelon", "PurchasingFeature", "Initialize"),
        ("OnInitializeMelon", "WarehouseFeature", "Initialize"),
        ("OnUpdate", "PurchasingFeature", "OnUpdate"),
        ("OnSceneWasUnloaded", "TradingFeature", "OnSceneWasUnloaded"),
        ("OnSceneWasUnloaded", "PurchasingFeature", "OnSceneWasUnloaded"),
        ("OnSceneWasUnloaded", "WarehouseFeature", "OnSceneWasUnloaded"),
        ("OnApplicationQuit", "PurchasingFeature", "OnApplicationQuit")
    },
    ["Arsenal"] = new[]
    {
        ("OnInitializeMelon", "GunsFeature", "Initialize"),
        ("OnLateInitializeMelon", "GunsFeature", "OnLateInitializeMelon"),
        ("OnSceneWasLoaded", "GunsFeature", "OnSceneWasLoaded"),
        ("OnUpdate", "GunsFeature", "OnUpdate"),
        ("OnSceneWasUnloaded", "GunsFeature", "OnSceneWasUnloaded")
    },
    ["Lifestyle"] = new[]
    {
        ("OnInitializeMelon", "CasinoFeature", "Initialize"),
        ("OnInitializeMelon", "TelevisionFeature", "Initialize"),
        ("OnInitializeMelon", "AmenitiesFeature", "Initialize"),
        ("OnInitializeMelon", "PokerFeature", "Initialize"),
        ("OnUpdate", "TelevisionFeature", "OnUpdate"),
        ("OnUpdate", "AmenitiesFeature", "OnUpdate"),
        ("OnUpdate", "PokerFeature", "OnUpdate"),
        ("OnDeinitializeMelon", "TelevisionFeature", "OnDeinitializeMelon"),
        ("OnDeinitializeMelon", "AmenitiesFeature", "OnDeinitializeMelon"),
        ("OnDeinitializeMelon", "PokerFeature", "OnDeinitializeMelon")
    },
    ["Interface"] = new[]
    {
        ("OnInitializeMelon", "HudFeature", "Initialize"),
        ("OnUpdate", "HudFeature", "OnUpdate"),
        ("OnLateUpdate", "HudFeature", "OnLateUpdate"),
        ("OnSceneWasUnloaded", "HudFeature", "OnSceneWasUnloaded"),
        ("OnApplicationQuit", "HudFeature", "OnApplicationQuit"),
        ("OnDeinitializeMelon", "HudFeature", "OnDeinitializeMelon")
    }
};

var selected = args.Length > 1 ? args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase) : callbacks.Keys.ToHashSet();
if (selected.Any(module => !callbacks.Keys.Contains(module, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Unknown module for lifecycle checks.");
foreach (var (module, expected) in callbacks.Where(pair => selected.Contains(pair.Key)))
{
    var dll = Path.Combine(repository, "src", "VICE." + module, "bin", "Release", "net6.0", "VICE." + module + ".dll");
    using var assembly = AssemblyDefinition.ReadAssembly(dll);
    var metadata = assembly.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == "MelonLoader.MelonInfoAttribute").ToArray();
    Check(metadata.Length == 1, module + " must register exactly one Melon entry point.");
    var entryName = ((TypeReference)metadata[0].ConstructorArguments[0].Value).FullName;
    var entry = assembly.MainModule.Types.Single(type => type.FullName == entryName);
    Check(entry.BaseType.FullName == "MelonLoader.MelonMod", module + " entry must be a native Melon mod.");
    Check(assembly.MainModule.Types.Count(type => type.BaseType?.FullName == "MelonLoader.MelonMod") == 1,
        module + " must not embed additional Melon mod instances.");
    Check(assembly.MainModule.AssemblyReferences.Any(reference => reference.Name == "VICE.Core"), module + " must use Core.");
    Check(!assembly.MainModule.AssemblyReferences.Any(reference => reference.Name.StartsWith("Personal.", StringComparison.Ordinal)),
        module + " must contain its features rather than depend on legacy DLLs.");
    Check(!assembly.MainModule.Types.Any(type => type.Name == "ModSupport"), module + " must not embed the legacy support helper.");
    foreach (var (callback, feature, method) in expected)
    {
        var body = entry.Methods.Single(candidate => candidate.Name == callback).Body;
        Check(body.Instructions.Any(instruction => instruction.Operand is MethodReference called
            && called.DeclaringType.Name == feature && called.Name == method),
            $"{module}.{callback} must forward {feature}.{method}.");
    }
    foreach (var feature in expected.Select(value => value.Feature).Distinct())
        Check(assembly.MainModule.Types.Single(type => type.Name == feature).BaseType.FullName == "System.Object",
            feature + " must be a plain feature, with no unregistered Melon lifecycle.");
    Check(entry.Methods.Single(method => method.Name == "OnDeinitializeMelon").Body.Instructions
        .Any(instruction => instruction.Operand is MethodReference called && called.Name == "UnpatchSelf"),
        module + " must release its own Harmony hooks.");
    if (module == "Arsenal") Check(assembly.MainModule.Resources.Any(resource => resource.Name == "Personal.MoreGuns.bundle"),
        "Arsenal must preserve the embedded private weapon bundle lookup.");
    if (module == "Lifestyle")
    {
        Check(!assembly.MainModule.AssemblyReferences.Any(reference=>reference.Name=="VICE.YouTube"),"Base Lifestyle has a hard dependency on its optional add-on.");
        Check(!assembly.MainModule.Types.Any(type=>type.Name is "YouTubeCompanion" or "YouTubePlayback" or "YouTubeChapters" or "YouTubeStatus"),"Base Lifestyle still embeds the browser implementation.");
        Check(!assembly.MainModule.Types.SelectMany(type=>type.Methods).SelectMany(method=>method.Body?.Instructions??Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(instruction=>instruction.Operand is MethodReference called && ((called.DeclaringType.FullName=="System.Diagnostics.Process"&&called.Name=="Start")||called.DeclaringType.FullName is "System.IO.Pipes.NamedPipeClientStream" or "System.IO.MemoryMappedFiles.MemoryMappedFile")),"Base TV starts a browser or uses companion transport.");
    }
    if (module == "Interface")
    {
        var minimap = assembly.MainModule.Types.Single(type => type.Name == "PlayerMinimap");
        Check(!minimap.Methods.SelectMany(method => method.Body?.Instructions ?? Enumerable.Empty<Mono.Cecil.Cil.Instruction>())
            .Any(instruction => instruction.Operand is MethodReference called && called.Name == "FindObjectsOfType"),
            "Minimap updates and reconciliation must not search the scene.");
        var registry = assembly.MainModule.Types.Single(type => type.Name == "MapPinRegistry");
        Check(registry.Methods.Any(method => method.Name == "Added") && registry.Methods.Any(method => method.Name == "Removed"),
            "Runtime POI creation/removal must update the cached registry.");
    }
}
Console.WriteLine($"VICE module registration/lifecycle/Core checks passed ({checks} assertions).");

// Exercise conflict detection without loading the game or MelonLoader into this process.
namespace MelonLoader
{
    internal static class MelonLogger
    {
        internal static string LastError = "";
        internal static void Error(string message) => LastError = message;
    }
}
