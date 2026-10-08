using System.Reflection;
using VICE.Core;

if (args.Length > 0 && args[0] == "--migrate-userdata")
{
    if (args.Length != 2) throw new ArgumentException("Pass the UserData directory to migrate.");
    Console.WriteLine(UserDataFolders.Prepare(args[1], "Lifestyle", "Television", "PersonalTV"));
    Console.WriteLine(UserDataFolders.Prepare(args[1], "Commerce", "Purchasing", "PersonalGasPurchases", "PersonalGuestPurchases"));
    return;
}

var assertions = 0;
void Check(bool result, string message)
{
    assertions++;
    if (!result) throw new InvalidOperationException(message);
}
var scope = new SubscriptionScope();
var released = new List<int>();
scope.Add(() => released.Add(1));
scope.Add(() => throw new InvalidOperationException("Native event owner was destroyed."));
scope.Add(() => released.Add(3));
scope.Dispose();
Check(released.SequenceEqual(new[] { 3, 1 }), "One destroyed owner must not prevent later cleanup; release in reverse order.");
scope.Dispose();
Check(released.Count == 2, "Cleanup must be idempotent.");
scope.Add(() => released.Add(4));
scope.Dispose();
Check(released.SequenceEqual(new[] { 3, 1, 4 }), "Next save may reuse a cleaned scope.");

var publisher = new Publisher();
var calls = 0;
for (var save = 0; save < 3; save++)
{
    Action listener = () => calls++;
    publisher.Event += listener;
    scope.Add(() => publisher.Event -= listener);
    publisher.Fire();
    Check(calls == save + 1, "Only the current save's handler should run.");
    scope.Dispose();
    publisher.Fire();
    Check(calls == save + 1, "Old save's handler must be detached.");
}

var root = Path.Combine(Path.GetTempPath(), "vice-core-test-" + Guid.NewGuid().ToString("N"));
try
{
    DefaultFiles.Seed(Assembly.GetExecutingAssembly(), "TestDefaults/", root);
    var settings = Path.Combine(root, "settings.json");
    Check(File.ReadAllText(settings).Contains("true"), "First launch should seed embedded defaults.");
    File.WriteAllText(settings, "user configuration");
    DefaultFiles.Seed(Assembly.GetExecutingAssembly(), "TestDefaults/", root);
    Check(File.ReadAllText(settings) == "user configuration", "Relaunch must preserve the user's edits.");
    UserDataMigrationTests.Run(Path.Combine(root, "UserData"), Check);
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"VICE Core lifecycle/default-file/data-migration checks passed ({assertions} assertions).");

sealed class Publisher
{
    public event Action? Event;
    public void Fire() => Event?.Invoke();
}

// The pure lifecycle test records the error path without loading MelonLoader.
namespace VICE.Core
{
    internal static class ModSupport
    {
        internal static void WarnOnce(string key, string message) { }
    }
}
