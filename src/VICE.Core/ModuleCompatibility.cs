using MelonLoader;

namespace VICE.Core;

/// <summary>Rejects predecessor assemblies before a replacement module installs its hooks.</summary>
public static class ModuleCompatibility
{
    public static bool CanInitialize(string module, params string[] legacyAssemblies)
    {
        var conflicts = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(name => name is not null && legacyAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (conflicts.Length == 0) return true;
        MelonLogger.Error($"{module} cannot initialize alongside {string.Join(", ", conflicts)}. Remove the predecessor mods using the replacement module's installer before restarting the game.");
        return false;
    }
}
