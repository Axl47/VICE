using Mono.Cecil;
using System.Text.RegularExpressions;
using VICE.Syndicate;

var assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
Check(GangTuning.Cooldown(15, 0.5f) == 8, "Odd cooldowns round up.");
Check(GangTuning.Cooldown(30, 0.5f) == 15, "Regional cooldowns halve.");
Check(GangTuning.Cooldown(1, 0.25f) == 1, "Cooldown floor is one hour.");
Check(GangTuning.Cooldown(0, 0.5f) == 0, "Ready events stay ready.");
Check(GangTuning.Cooldown(20, float.NaN) == 10, "Invalid cooldown setting uses default.");
Check(GangTuning.Cooldown(20, 10) == 20, "Cooldown setting is bounded.");
Check(GangTuning.Radius(20, 1.5f) == 30, "Ambush reach expands by 50%.");
Check(GangTuning.Radius(0, 1.5f) == 0, "Zero radius remains zero.");
Check(GangTuning.Radius(20, float.NaN) == 30, "Invalid radius setting uses default.");
Check(GangTuning.Radius(float.MaxValue, 2) == float.MaxValue, "Radius cannot overflow.");
Check(GangTuning.Radius(20, -1) == 20, "Radius setting cannot shrink native reach.");

if (args.Length != 2) throw new ArgumentException("Pass game directory and compiled VICE.Syndicate.dll.");
var gameDir = args[0];
var interopDir = Path.Combine(gameDir, "MelonLoader", "Il2CppAssemblies");
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(interopDir);
resolver.AddSearchDirectory(Path.Combine(gameDir, "MelonLoader", "net6"));
var reader = new ReaderParameters { AssemblyResolver = resolver };
using var game = AssemblyDefinition.ReadAssembly(Path.Combine(interopDir, "Assembly-CSharp.dll"), reader);
using var mod = AssemblyDefinition.ReadAssembly(args[1], reader);
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> list)
{
    foreach (var type in list) { yield return type; foreach (var nested in Types(type.NestedTypes)) yield return nested; }
}
var gameTypes = Types(game.MainModule.Types).ToDictionary(t => t.FullName);
var failures = new List<string>();
var targets = new List<MethodDefinition>();
foreach (var owner in Types(mod.MainModule.Types))
foreach (var attribute in owner.CustomAttributes.Where(a => a.AttributeType.Name == "HarmonyPatch"))
{
    var ctor = attribute.ConstructorArguments;
    if (ctor.Count < 2 || ctor[0].Value is not TypeReference reference || ctor[1].Value is not string name) continue;
    if (!gameTypes.TryGetValue(reference.FullName, out var type)) { failures.Add($"Missing target type {reference.FullName}"); continue; }
    if (ctor.Skip(2).Any(a => a.Type.Name == "MethodType" && Convert.ToInt32(a.Value) == 1)) name = "get_" + name;
    var methods = type.Methods.Where(m => m.Name == name).ToArray();
    var specified = ctor.Skip(2).FirstOrDefault(a => a.Value is CustomAttributeArgument[]);
    if (specified.Value is CustomAttributeArgument[] parameters)
        methods = methods.Where(m => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters.Select(p => ((TypeReference)p.Value).FullName))).ToArray();
    if (methods.Length != 1) { failures.Add($"Missing/ambiguous target {reference.FullName}.{name}: {methods.Length}"); continue; }
    var original = methods[0]; targets.Add(original);
    foreach (var hook in owner.Methods.Where(m => m.Name is "Prefix" or "Postfix" or "Finalizer"))
    foreach (var parameter in hook.Parameters)
    {
        if (parameter.Name.StartsWith("__")) continue;
        var expected = original.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
        if (expected == null || expected.ParameterType.FullName.TrimEnd('&') != parameter.ParameterType.FullName.TrimEnd('&'))
            failures.Add($"{owner.Name}.{hook.Name}: invalid parameter {parameter.Name} ({parameter.ParameterType.FullName}) for {original.FullName}");
    }
}
// Include the three difficulty patches registered through VICE.Core.
foreach (var target in new[] { ("Il2CppScheduleOne.Cartel.CartelActivities", "GetNewCooldown"), ("Il2CppScheduleOne.Cartel.CartelRegionActivities", "GetNewCooldown"), ("Il2CppScheduleOne.Cartel.Ambush", "MinPassed") })
    targets.Add(gameTypes[target.Item1].Methods.Single(m => m.Name == target.Item2));

var rawDir = Path.Combine(gameDir, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Cpp2IL", "cpp2il_out");
var byAddress = new Dictionary<string, List<Symbol>>();
var byMethod = new Dictionary<string, Symbol>();
foreach (var path in Directory.EnumerateFiles(rawDir, "*.dll"))
{
    using var raw = AssemblyDefinition.ReadAssembly(path);
    foreach (var type in Types(raw.MainModule.Types))
    foreach (var method in type.Methods)
    {
        var address = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "AddressAttribute");
        var rva = address?.Fields.FirstOrDefault(f => f.Name == "RVA").Argument.Value as string;
        if (rva is null or "0x0") continue;
        var symbol = new Symbol(type.FullName, method.Name, string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName)), method.ReturnType.FullName, rva);
        if (!byAddress.TryGetValue(rva, out var aliases)) byAddress[rva] = aliases = new();
        aliases.Add(symbol);
        byMethod[method.FullName] = symbol;
    }
}
string Native(string text)
{
    text = text.Replace("Il2CppScheduleOne", "ScheduleOne").Replace("Il2CppFishNet", "FishNet").Replace("Il2CppSystem", "System");
    return Regex.Replace(text, @"Il2CppInterop.Runtime.InteropTypes.Arrays.Il2Cpp(?:Struct|Reference)Array`1<([^<>]+)>", "$1[]");
}
foreach (var original in targets)
{
    var rawName = Native(original.FullName);
    if (!byMethod.TryGetValue(rawName, out var symbol)) { failures.Add($"Native address missing: {rawName}"); continue; }
    var aliases = byAddress[symbol.Rva];
    if (aliases.Count <= 1) continue;
    // A FishNet writer can share the exact owner's body; unrelated owners are never accepted.
    var safeWriter = aliases.Count == 2 && aliases.All(a => a.Type == symbol.Type && a.Parameters == symbol.Parameters && a.ReturnType == symbol.ReturnType
        && (a.Name == symbol.Name || a.Name.StartsWith("RpcWriter___Observers_" + symbol.Name + "_") || a.Name.StartsWith("RpcWriter___Server_" + symbol.Name + "_")));
    if (!safeWriter) failures.Add($"Unsafe shared native target {rawName} at {symbol.Rva}: {string.Join("; ", aliases.Take(4).Select(a => a.Type + "." + a.Name))} ({aliases.Count} aliases)");
    else Console.WriteLine($"Reviewed identical FishNet writer: {rawName}");
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
Check(failures.Count == 0, $"{failures.Count} compatibility failures.");
Check(mod.Name.Name == "VICE.Syndicate", "Module identity is VICE.Syndicate.");
Check(mod.MainModule.Resources.Any(r => r.Name.EndsWith("LICENSE.CartelEnforcer")), "MIT notice is embedded.");
Check(mod.MainModule.Resources.Count(r => r.Name.StartsWith("VICE.Syndicate.Defaults/")) == 9, "All nine release default files are embedded.");
Console.WriteLine($"Passed {assertions} assertions and {targets.Count} beta patch contracts/native addresses.");

record Symbol(string Type, string Name, string Parameters, string ReturnType, string Rva);
