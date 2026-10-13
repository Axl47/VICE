using System.Text;

namespace VICE.Core;

/// <summary>Write-through same-directory replacement; a failed write preserves the previous document.</summary>
public static class AtomicDocument
{
    public static void Write(string path, string contents)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".next";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            var bytes = Encoding.UTF8.GetBytes(contents); stream.Write(bytes); stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
    public static string Within(string root, string relative)
    {
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(parent, relative));
        if (!path.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escapes the save.");
        return path;
    }
}
