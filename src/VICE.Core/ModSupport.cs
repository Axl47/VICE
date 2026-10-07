using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace VICE.Core;

// Shared services for the VICE module family.
public static class ModSupport
{
    private static readonly HashSet<string> Reported = new();

    public static bool Patch(HarmonyLib.Harmony harmony, Type target, string method, Type owner,
        string? prefix = null, string? postfix = null, Type[]? parameters = null, string? finalizer = null)
    {
        try
        {
            var original = parameters is null ? AccessTools.Method(target, method) : AccessTools.Method(target, method, parameters);
            if (original is null) throw new MissingMethodException(target.FullName, method);
            HarmonyMethod? Hook(string? name) => name is null ? null : new HarmonyMethod(
                owner.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(owner.FullName, name));
            harmony.Patch(original, prefix: Hook(prefix), postfix: Hook(postfix), finalizer: Hook(finalizer));
            MelonLogger.Msg($"[{harmony.Id}] Patched {target.Name}.{method}");
            return true;
        }
        catch (Exception error)
        {
            WarnOnce($"patch:{harmony.Id}:{target.Name}:{method}", $"{target.Name}.{method} is unavailable: {error.Message}");
            return false;
        }
    }

    public static void WarnOnce(string key, string message)
    {
        if (Reported.Add(key)) MelonLogger.Warning(message);
    }

    public static MelonPreferences_Entry<T> Entry<T>(MelonPreferences_Category category, string name, T value, string description)
        => category.CreateEntry(name, value, description: description);

    /// <summary>Use a legacy value as the initial default; an existing new value always wins.</summary>
    public static MelonPreferences_Entry<T> MigratedEntry<T>(MelonPreferences_Category category, string name, T value, string description, string legacyCategory)
    {
        var legacy = MelonPreferences.CreateCategory(legacyCategory);
        var previous = legacy.GetEntry<T>(name) ?? legacy.CreateEntry(name, value, description: description);
        return Entry(category, name, previous.Value, description);
    }
}

