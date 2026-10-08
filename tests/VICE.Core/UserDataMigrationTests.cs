using VICE.Core;

internal static class UserDataMigrationTests
{
    internal static void Run(string root, Action<bool, string> check)
    {
        static void Write(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
        void Reject(Action action, string message)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidDataException or IOException) { check(true, message); return; }
            check(false, message);
        }
        var oldTv = Path.Combine(root, "PersonalTV");
        Write(Path.Combine(oldTv, "Images", "poster.png"), "original image");
        Write(Path.Combine(oldTv, "Videos", "movie.mp4"), "original video");
        Write(Path.Combine(oldTv, "Cache", "Images", "asset.png"), "cached image");
        var tv = UserDataFolders.Prepare(root + Path.DirectorySeparatorChar, "Lifestyle", "Television", "PersonalTV");
        check(tv == Path.Combine(root, "VICE", "Lifestyle", "Television"), "TV data did not use the VICE module/feature layout.");
        check(!Directory.Exists(oldTv), "Completed TV migration left its personal folder behind.");
        check(File.ReadAllText(Path.Combine(tv, "Images", "poster.png")) == "original image", "TV migration changed an image.");
        check(File.ReadAllText(Path.Combine(tv, "Videos", "movie.mp4")) == "original video", "TV migration changed a video.");
        check(File.ReadAllText(Path.Combine(tv, "Cache", "Images", "asset.png")) == "cached image", "TV migration lost its image cache.");
        check(UserDataFolders.Prepare(root, "Lifestyle", "Television", "PersonalTV") == tv, "Repeated TV initialization changed its destination.");

        var purchases = Path.Combine(root, "VICE", "Commerce", "Purchasing");
        var oldPurchases = Path.Combine(root, "PersonalGasPurchases");
        Write(Path.Combine(purchases, "orders.json"), "new shopping lists");
        Write(Path.Combine(oldPurchases, "orders.json"), "old shopping lists");
        Write(Path.Combine(oldPurchases, "nested", "keep.txt"), "keep this");
        Write(Path.Combine(root, "PersonalGuestPurchases", "guest.txt"), "alternate legacy name");
        UserDataFolders.Prepare(root, "Commerce", "Purchasing", "PersonalGasPurchases", "PersonalGuestPurchases");
        check(File.ReadAllText(Path.Combine(purchases, "orders.json")) == "new shopping lists", "Migration overwrote existing VICE shopping lists.");
        check(File.ReadAllText(Path.Combine(purchases, "nested", "keep.txt")) == "keep this", "Migration did not merge a missing nested file.");
        check(File.ReadAllText(Path.Combine(purchases, "guest.txt")) == "alternate legacy name", "Alternate purchase folder did not migrate.");
        check(!Directory.Exists(oldPurchases) && !Directory.Exists(Path.Combine(root, "PersonalGuestPurchases")), "Purchase migration left old folders behind.");
        var conflicts = Path.Combine(root, "VICE", "MigrationConflicts");
        var archived = Directory.GetFiles(conflicts, "orders.json", SearchOption.AllDirectories);
        check(archived.Length == 1 && File.ReadAllText(archived[0]) == "old shopping lists", "Migration did not preserve a conflicting legacy list.");
        UserDataFolders.Prepare(root, "Commerce", "Purchasing", "PersonalGasPurchases");
        check(Directory.GetFiles(conflicts, "orders.json", SearchOption.AllDirectories).Length == 1, "Repeated migration duplicated conflict files.");
        Write(Path.Combine(purchases, "orders.json"), "new edit after migration");
        UserDataFolders.Prepare(root, "Commerce", "Purchasing", "PersonalGasPurchases");
        check(File.ReadAllText(Path.Combine(purchases, "orders.json")) == "new edit after migration", "Reinitialization lost edits made after migration.");

        // A partly completed migration resumes by moving only remaining legacy entries.
        Write(Path.Combine(oldTv, "Images", "additional.png"), "resumed image");
        Write(Path.Combine(oldTv, "Images", "poster.png"), "conflicting image");
        Write(Path.Combine(tv, "blocked"), "VICE file");
        Write(Path.Combine(oldTv, "blocked", "nested.txt"), "legacy directory");
        Write(Path.Combine(tv, "folder", "keep.txt"), "VICE directory");
        Write(Path.Combine(oldTv, "folder"), "legacy file");
        UserDataFolders.Prepare(root, "Lifestyle", "Television", "PersonalTV");
        check(File.ReadAllText(Path.Combine(tv, "Images", "additional.png")) == "resumed image", "Partial migration did not resume.");
        check(File.ReadAllText(Path.Combine(tv, "Images", "poster.png")) == "original image", "Image collision overwrote VICE media.");
        check(Directory.GetFiles(conflicts, "poster.png", SearchOption.AllDirectories).Any(f => File.ReadAllText(f) == "conflicting image"), "Conflicting image was lost.");
        check(File.ReadAllText(Path.Combine(tv, "blocked")) == "VICE file" && Directory.GetFiles(conflicts, "nested.txt", SearchOption.AllDirectories).Any(f => File.ReadAllText(f) == "legacy directory"), "File/directory collision lost data.");
        check(File.ReadAllText(Path.Combine(tv, "folder", "keep.txt")) == "VICE directory" && Directory.GetFiles(conflicts, "folder", SearchOption.AllDirectories).Any(f => File.ReadAllText(f) == "legacy file"), "Directory/file collision lost data.");

        Reject(() => UserDataFolders.Prepare(root, "../escape", "Television", "PersonalTV"), "Module traversal was accepted.");
        Reject(() => UserDataFolders.Prepare(root, "Lifestyle", "..", "PersonalTV"), "Feature traversal was accepted.");
        Reject(() => UserDataFolders.Prepare(root, "Lifestyle", "Television", "../escape"), "Legacy traversal was accepted.");
        Reject(() => UserDataFolders.Prepare(root, "Lifestyle", "Television", "VICE"), "VICE root was accepted as its own legacy source.");
        Write(Path.Combine(root, "BrokenLegacy"), "a file, not a folder");
        Reject(() => UserDataFolders.Prepare(root, "Commerce", "Broken", "BrokenLegacy"), "A legacy file silently became an empty data folder.");
        check(File.ReadAllText(Path.Combine(root, "BrokenLegacy")) == "a file, not a folder", "Invalid legacy path was modified.");
        Write(Path.Combine(root, "VICE", "BlockedModule"), "blocked parent");
        Write(Path.Combine(root, "UntouchedLegacy", "data.txt"), "untouched source");
        Reject(() => UserDataFolders.Prepare(root, "BlockedModule", "Feature", "UntouchedLegacy"), "Blocked destination did not fail before moving data.");
        check(File.ReadAllText(Path.Combine(root, "UntouchedLegacy", "data.txt")) == "untouched source", "Failed preflight moved legacy data.");
    }
}
