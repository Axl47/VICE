using System.Reflection;

namespace VICE.Core;

public static class DefaultFiles
{
    /// <summary>Seed packaged module defaults once, preserving every existing file.</summary>
    public static void Seed(Assembly assembly, string prefix, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var relative = resource[prefix.Length..].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Default resource escapes the module data folder.");
            if (File.Exists(destination)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException(resource);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            source.CopyTo(output);
        }
    }
}
