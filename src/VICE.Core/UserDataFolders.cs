namespace VICE.Core;

/// <summary>Move module and feature data under VICE without replacing existing data.</summary>
public static class UserDataFolders
{
    public static string Prepare(string userData, string module, string feature, params string[] legacyFolders)
        => PrepareAt(userData, Path.Combine("VICE", Component(module), Component(feature)), legacyFolders);

    public static string PrepareModule(string userData, string module, params string[] legacyFolders)
        => PrepareAt(userData, Path.Combine("VICE", Component(module)), legacyFolders);

    private static string PrepareAt(string userData, string relativeDestination, string[] legacyFolders)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userData));
        var destination = Within(root, Path.Combine(root, relativeDestination));
        if (legacyFolders.Any(name => name.Equals("VICE", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The VICE root cannot be a legacy feature folder.");
        var sources = legacyFolders.Select(name => Within(root, Path.Combine(root, Component(name)))).ToArray();
        // Check the entire tree before moving anything. Never follow junctions or links.
        CheckParents(root, destination);
        if (Directory.Exists(destination)) CheckTree(destination);
        foreach (var source in sources)
        {
            CheckParents(root, source);
            if (File.Exists(source)) throw new IOException("Legacy data path is a file: " + source);
            if (Directory.Exists(source)) CheckTree(source);
        }
        foreach (var source in sources.Where(Directory.Exists))
        {
            var conflicts = Within(root, Path.Combine(root, "VICE", "MigrationConflicts", Path.GetFileName(source) + "-" + Guid.NewGuid().ToString("N")));
            CheckParents(root, conflicts);
            Move(source, destination, conflicts);
        }
        Directory.CreateDirectory(destination);
        return destination;
    }
    private static string Component(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value != Path.GetFileName(value)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new InvalidDataException("Invalid VICE data folder name.");
        return value;
    }
    private static string Within(string root, string path)
    {
        var resolved = Path.GetFullPath(path);
        if (!resolved.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Data migration escapes UserData.");
        return resolved;
    }
    private static void CheckParents(string root, string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current)) throw new IOException("Data folder path is a file: " + current);
            if (Directory.Exists(current) || File.Exists(current)) CheckLink(current);
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new InvalidDataException("Data migration escapes UserData.");
    }
    private static void CheckLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Move linked data folders manually before starting VICE: " + path);
    }
    private static void CheckTree(string path)
    {
        CheckLink(path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            CheckLink(entry);
            if (Directory.Exists(entry)) CheckTree(entry);
        }
    }
    private static void Move(string source, string destination, string conflicts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!Directory.Exists(destination) && !File.Exists(destination))
        {
            try { Directory.Move(source, destination); return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Windows can refuse a whole-tree rename while individual entries
                // are movable. Merge through the same non-overwriting path instead.
                Directory.CreateDirectory(destination);
            }
        }
        if (File.Exists(destination))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(conflicts)!);
            Directory.Move(source, conflicts);
            return;
        }
        foreach (var entry in Directory.EnumerateFileSystemEntries(source).ToArray())
        {
            var name = Path.GetFileName(entry);
            var target = Path.Combine(destination, name);
            var archived = Path.Combine(conflicts, name);
            if (Directory.Exists(entry)) Move(entry, target, archived);
            else if (!File.Exists(target) && !Directory.Exists(target)) File.Move(entry, target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archived)!);
                File.Move(entry, archived);
            }
        }
        Directory.Delete(source); // Empty only; partial failures leave remaining files for a retry.
    }
}
