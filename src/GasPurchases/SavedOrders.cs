using System.Text.Json;
using System.Text;

namespace PersonalMods.GasPurchases;

internal sealed record OrderItem(string ItemId, int Quantity, string Name);
internal sealed record SavedOrder(string Name, OrderItem[] Items);
internal sealed record OrderBook
{
    public int Version { get; init; } = 1;
    public SavedOrder[] Orders { get; init; } = Array.Empty<SavedOrder>();
    public SavedOrder? LastPurchase { get; init; }
}

internal sealed class SavedOrders
{
    internal const int MaximumOrders = 32;
    private const int MaximumItems = 128;
    private readonly string FilePath;
    private readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    internal OrderBook Book { get; private set; } = new();
    internal string? ReadError { get; private set; }

    internal SavedOrders(string path)
    {
        FilePath = path;
        if (!File.Exists(path)) return;
        try
        {
            if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException("Saved orders file is too large.");
            var book = JsonSerializer.Deserialize<OrderBook>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Saved orders file is empty.");
            ValidateBook(book);
            Book = book;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            ReadError = error.Message; // Preserve a damaged/incompatible file until the owner repairs it.
        }
    }

    internal static SavedOrder Create(string name, IEnumerable<OrderItem> items)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 48 || name.Any(char.IsControl)) throw new ArgumentException("Enter an order name of 1–48 characters.");
        var order = new SavedOrder(name, items.Select(x => new OrderItem(x.ItemId.Trim(), x.Quantity, x.Name)).ToArray());
        ValidateOrder(order);
        return order;
    }

    internal void Save(SavedOrder order)
    {
        ValidateOrder(order);
        var orders = Book.Orders.ToList();
        var index = orders.FindIndex(x => x.Name.Equals(order.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) orders[index] = order;
        else
        {
            if (orders.Count >= MaximumOrders) throw new InvalidOperationException($"You can save up to {MaximumOrders} orders. Delete one to make room.");
            orders.Add(order);
        }
        Commit(Book with { Orders = orders.ToArray() });
    }

    internal void Delete(string name)
        => Commit(Book with { Orders = Book.Orders.Where(x => !x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray() });

    internal void Remember(SavedOrder order)
    {
        ValidateOrder(order);
        Commit(Book with { LastPurchase = order with { Name = "Last purchase" } });
    }

    private void Commit(OrderBook next)
    {
        if (ReadError is not null) throw new IOException("Saved orders could not be read. Repair or rename the orders.json file before saving new orders.");
        ValidateBook(next);
        var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath))!;
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        var content = JsonSerializer.Serialize(next, Json);
        if (Encoding.UTF8.GetByteCount(content) > 1_048_576) throw new InvalidOperationException("Saved orders exceed the file size limit. Remove an order first.");
        File.WriteAllText(temporary, content);
        File.Move(temporary, FilePath, overwrite: true);
        Book = next; // A failed write must not pretend that an order was saved.
    }

    private static void ValidateBook(OrderBook book)
    {
        if (book.Version != 1 || book.Orders is null || book.Orders.Length > MaximumOrders) throw new InvalidDataException("Unsupported saved orders format.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var order in book.Orders)
        {
            ValidateOrder(order);
            if (!names.Add(order.Name)) throw new InvalidDataException("Duplicate saved order names.");
        }
        if (book.LastPurchase is not null) ValidateOrder(book.LastPurchase);
    }

    private static void ValidateOrder(SavedOrder order)
    {
        if (order is null || string.IsNullOrWhiteSpace(order.Name) || order.Name.Length > 48 || order.Name.Any(char.IsControl)
            || order.Items is null || order.Items.Length is < 1 or > MaximumItems) throw new ArgumentException("An order needs a name and at least one item.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in order.Items)
            if (item is null || string.IsNullOrWhiteSpace(item.ItemId) || item.ItemId.Length > 128 || item.ItemId.Any(char.IsControl)
                || item.Quantity is < 1 or > 100_000 || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 256 || !ids.Add(item.ItemId))
                throw new ArgumentException("The saved order contains an invalid or duplicate item.");
    }
}

internal sealed record AvailableOrderItem(string ItemId, string Name, bool Unlocked, int? Stock);
internal sealed record OrderPlan(OrderItem[] Items, string? Error) { internal bool Success => Error is null; }
internal static class OrderPlanner
{
    internal static OrderPlan Plan(SavedOrder order, IReadOnlyDictionary<string, AvailableOrderItem> listings, int maximumQuantity)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (order.Items is null || order.Items.Length == 0) return Failure("This order is empty.");
        foreach (var row in order.Items)
        {
            if (row.Quantity < 1 || row.Quantity > maximumQuantity || !seen.Add(row.ItemId)) return Failure($"Invalid quantity or duplicate item: {row.Name}.");
            if (!listings.TryGetValue(row.ItemId, out var item)) return Failure($"{row.Name} is not sold here.");
            if (!item.Unlocked) return Failure($"{item.Name} is not unlocked or available yet.");
            if (item.Stock is { } stock && stock < row.Quantity) return Failure($"{item.Name}: need {row.Quantity}, only {Math.Max(0, stock)} in stock.");
        }
        return new OrderPlan(order.Items.ToArray(), null);
    }

    private static OrderPlan Failure(string message) => new(Array.Empty<OrderItem>(), message);
}
