using PersonalMods.GasPurchases;
using System.Text.Json;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/order-tests", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
var assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
void Reject(Action action, string message)
{
    try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException) { assertions++; return; }
    throw new InvalidOperationException(message);
}

var path = Path.Combine(root, "orders.json");
var store = new SavedOrders(path);
Check(store.ReadError is null && store.Book.Orders.Length == 0 && !File.Exists(path), "Opening a new store should not write an empty file.");
var mixing = SavedOrders.Create(" Mixing supplies ", new[] { new OrderItem("cuke", 40, "Cuke"), new OrderItem("banana", 20, "Banana") });
store.Save(mixing);
store.Remember(mixing);
var loaded = new SavedOrders(path);
Check(loaded.Book.Orders.Single().Name == "Mixing supplies", "Trim and persist the order name.");
Check(loaded.Book.Orders[0].Items.SequenceEqual(mixing.Items), "Quantities and identifiers must survive a reload.");
Check(loaded.Book.LastPurchase?.Items.SequenceEqual(mixing.Items) == true, "Last successful purchase must survive a restart.");
loaded.Save(SavedOrders.Create("mixing SUPPLIES", new[] { new OrderItem("cuke", 12, "Cuke") }));
Check(loaded.Book.Orders.Length == 1 && loaded.Book.Orders[0].Items[0].Quantity == 12, "Saving an existing name updates the order rather than duplicating it.");
Check(loaded.Book.LastPurchase?.Items[0].Quantity == 40, "Editing a saved order must not alter last-purchase history.");
loaded.Delete("MIXING supplies");
Check(new SavedOrders(path).Book.Orders.Length == 0, "Deleting a named order must persist.");
Check(new SavedOrders(path).Book.LastPurchase is not null, "Deleting a named order must preserve purchase history.");
Reject(() => SavedOrders.Create("", mixing.Items), "Reject empty names.");
Reject(() => SavedOrders.Create("x", Array.Empty<OrderItem>()), "Reject empty baskets.");
Reject(() => SavedOrders.Create("x", new[] { new OrderItem("cuke", 0, "Cuke") }), "Reject zero quantities.");
Reject(() => SavedOrders.Create("x", new[] { new OrderItem("cuke", -1, "Cuke") }), "Reject negative quantities.");
Reject(() => SavedOrders.Create("x", new[] { new OrderItem("cuke", 1, "Cuke"), new OrderItem("CUKE", 2, "Cuke") }), "Reject duplicate identifiers.");

var brokenPath = Path.Combine(root, "broken.json");
File.WriteAllText(brokenPath, "{ damaged JSON");
var broken = new SavedOrders(brokenPath);
Check(broken.ReadError is not null, "Report damaged JSON.");
Reject(() => broken.Save(mixing), "A damaged file must not be silently overwritten.");
Check(File.ReadAllText(brokenPath) == "{ damaged JSON", "Preserve damaged files for recovery.");
var futurePath = Path.Combine(root, "future.json");
File.WriteAllText(futurePath, "{\"version\":99,\"orders\":[]}");
var future = new SavedOrders(futurePath);
Check(future.ReadError is not null, "Reject incompatible future formats.");
Reject(() => future.Remember(mixing), "Purchase history must not overwrite an incompatible format.");
var nullPath = Path.Combine(root, "null.json");
File.WriteAllText(nullPath, "{\"version\":1,\"orders\":[null]}");
Check(new SavedOrders(nullPath).ReadError is not null, "Reject null order records.");
var blocker = Path.Combine(root, "blocker");
File.WriteAllText(blocker, "not a directory");
var unwritable = new SavedOrders(Path.Combine(blocker, "orders.json"));
Reject(() => unwritable.Save(mixing), "Report persistence failures.");
Check(unwritable.Book.Orders.Length == 0, "A failed write must leave the in-memory book unchanged.");
var full = new SavedOrders(Path.Combine(root, "full.json"));
for (var index = 0; index < SavedOrders.MaximumOrders; index++) full.Save(SavedOrders.Create($"Order {index}", mixing.Items));
Reject(() => full.Save(SavedOrders.Create("Extra order", mixing.Items)), "Enforce the saved-order limit.");
full.Save(SavedOrders.Create("Order 0", new[] { new OrderItem("banana", 1, "Banana") }));
Check(full.Book.Orders.Length == SavedOrders.MaximumOrders, "Updating an order at capacity must still work.");

var catalog = new Dictionary<string, AvailableOrderItem>(StringComparer.OrdinalIgnoreCase)
{
    ["cuke"] = new("cuke", "Cuke", true, 40),
    ["banana"] = new("banana", "Banana", true, null)
};
var plan = OrderPlanner.Plan(mixing, catalog, 999);
Check(plan.Success && plan.Items.SequenceEqual(mixing.Items), "Restore exact quantities using available physical stock.");
catalog["cuke"] = new("cuke", "Cuke", true, 39);
plan = OrderPlanner.Plan(mixing, catalog, 999);
Check(!plan.Success && plan.Items.Length == 0 && plan.Error!.Contains("39"), "Insufficient stock must reject the entire basket with a useful reason.");
catalog["cuke"] = new("cuke", "Cuke", false, 100);
Check(!OrderPlanner.Plan(mixing, catalog, 999).Success, "Do not restore locked products.");
catalog.Remove("cuke");
Check(!OrderPlanner.Plan(mixing, catalog, 999).Success, "Do not silently drop missing products.");
catalog["cuke"] = new("cuke", "Cuke", true, null);
Check(!OrderPlanner.Plan(mixing, catalog, 20).Success, "Respect the current game's per-item quantity limit.");
Check(OrderPlanner.Plan(mixing, catalog, 999).Success, "Unlimited-stock listings can restore the full order.");

var random = new Random(1001);
for (var run = 0; run < 1200; run++)
{
    var rows = Enumerable.Range(0, random.Next(1, 12)).Select(i => new OrderItem($"item{i}", random.Next(1, 250), $"Item {i}")).ToArray();
    var recipe = SavedOrders.Create("Random order", rows);
    var listings = new Dictionary<string, AvailableOrderItem>(StringComparer.OrdinalIgnoreCase);
    var expected = true;
    foreach (var row in rows)
    {
        var present = random.Next(10) != 0;
        var unlocked = random.Next(10) != 0;
        int? stock = random.Next(3) == 0 ? null : random.Next(0, 300);
        if (present) listings[row.ItemId] = new(row.ItemId, row.Name, unlocked, stock);
        expected &= present && unlocked && (stock is null || stock >= row.Quantity) && row.Quantity <= 200;
    }
    var before = JsonSerializer.Serialize(recipe);
    var result = OrderPlanner.Plan(recipe, listings, 200);
    Check(result.Success == expected, "Saved-order availability validation disagrees with catalog constraints.");
    Check(JsonSerializer.Serialize(recipe) == before, "Planning must not edit the saved order.");
    Check(expected ? result.Items.SequenceEqual(rows) : result.Items.Length == 0, "Restoration must be exact or rejected as a whole.");
}
Console.WriteLine($"PASS: {assertions} assertions; JSON persistence and 1200 randomized stock/unlock/quantity scenarios.");
