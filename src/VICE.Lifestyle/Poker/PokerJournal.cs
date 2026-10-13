using System.Text.Json;
using System.Text.Json.Nodes;
using VICE.Core;

namespace VICE.Lifestyle.Poker;

internal static class PokerInventory
{
    internal const int PocketSlot = 8; // Installed native InventorySlotCount=8; GetAllInventorySlots appends cash.
    private static (JsonNode Root, JsonArray Items, JsonNode Pocket) Read(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty native inventory.");
        var items = root["Items"]?.AsArray() ?? throw new InvalidDataException("Native inventory has no Items.");
        if (items.Count != PocketSlot + 1) throw new InvalidDataException("Native inventory layout differs from the inspected eight hotbar slots plus pocket cash.");
        var pocket = JsonNode.Parse(items[PocketSlot]?.GetValue<string>() ?? "{}") ?? throw new InvalidDataException("Pocket slot is empty.");
        var type = pocket["DataType"]?.GetValue<string>(); var id = pocket["ID"]?.GetValue<string>();
        if (!(type == "CashData" && id == "cash") && !(type == "ItemData" && string.IsNullOrEmpty(id) && pocket["Quantity"]?.GetValue<int>() == 0))
            throw new InvalidDataException("Native slot 8 is not pocket cash or its cleared empty slot.");
        return (root, items, pocket);
    }
    internal static double Cash(string json)
    {
        var pocket = Read(json).Pocket;
        var value = pocket["DataType"]?.GetValue<string>() == "CashData" ? pocket["CashBalance"]!.GetValue<double>() : 0;
        if (!double.IsFinite(value) || value < 0 || value > 1000000000) throw new InvalidDataException("Native cash is outside the supported range.");
        return (double)(float)value; // Native JSON may print a short decimal; the actual wallet is a float.
    }
    internal static string WithCash(string json, double amount)
    {
        Cash(json); var (root, items, pocket) = Read(json);
        pocket["DataType"] = "CashData"; pocket["ID"] = "cash"; pocket["Quantity"] = 1; pocket["CashBalance"] = amount;
        items[PocketSlot] = pocket.ToJsonString();
        return root.ToJsonString();
    }
    internal static double Target(double before, long delta)
    {
        var after = (double)(float)(before + delta);
        if (after < 0 || after > 1000000000 || after - before != delta)
            throw new InvalidOperationException("Insufficient cash, wallet cap, or native float precision prevents this exact whole-dollar transfer.");
        return after;
    }
    internal static long CashOutAmount(double before, long owed)
    {
        var maximum = Math.Min(1000000000, before + owed);
        var fraction = before - Math.Floor(before);
        if (fraction > 0)
        {
            var binary = fraction; var bits = 0;
            while (binary != Math.Floor(binary) && bits < 150) { binary *= 2; bits++; }
            // Beyond this magnitude the native float cannot retain the wallet's existing fraction.
            var lastWithFraction = Math.Pow(2, 24 - bits) - 1 + fraction;
            maximum = Math.Min(maximum, lastWithFraction);
        }
        var candidate = (float)maximum;
        if (candidate > maximum) candidate = MathF.BitDecrement(candidate);
        var amount = (double)candidate - before;
        if (amount <= 0 || amount > owed || amount != Math.Floor(amount))
            throw new InvalidOperationException("Owed cash stays at the table until your pocket has room for an exact whole-dollar payout. Spend pocket cash normally, then retry.");
        var dollars = (long)amount; Target(before, dollars); return dollars;
    }
}

internal sealed class PokerTransfer
{
    public string Token { get; set; } = "";
    public string Player { get; set; } = "";
    public string Inventory { get; set; } = "";
    public double Before { get; set; }
    public double After { get; set; }
    public string NativeAfter { get; set; } = "";
    public string NativeBeforeHash { get; set; } = "";
    public PokerTable TableAfter { get; set; } = new();
}
internal sealed class PokerDocument
{
    public int Version { get; set; } = 1;
    public string SaveId { get; set; } = Guid.NewGuid().ToString();
    public Dictionary<string, PokerTable> Tables { get; set; } = new();
    public HashSet<string> Completed { get; set; } = new();
    public PokerTransfer? Transfer { get; set; }
}

// Native pocket cash and the poker ledger form a recoverable write-ahead transaction.
// The host writes only the authenticated player's own Inventory.json cash field.
internal sealed class PokerJournal
{
    internal PokerDocument Document { get; private set; } = new();
    internal string Root { get; }
    private readonly string Path;
    internal PokerJournal(string root)
    {
        Root = System.IO.Path.GetFullPath(root); Path = AtomicDocument.Within(Root, "Modded/VICE/Poker/settlement.json");
        if (!File.Exists(Path) && File.Exists(Path + ".bak")) throw new InvalidDataException("Poker ledger primary is missing while a backup exists. Automatic replay is blocked; preserve both cash and ledger files.");
        if (File.Exists(Path)) Document = JsonSerializer.Deserialize<PokerDocument>(File.ReadAllText(Path)) ?? throw new InvalidDataException("Empty poker ledger.");
        if (Document.Version != 1 || !Guid.TryParse(Document.SaveId, out _) || Document.Tables.Count > 128) throw new InvalidDataException("Unsupported poker ledger.");
        foreach (var pair in Document.Tables)
        {
            var t = pair.Value;
            if (pair.Key != t.Id || !Guid.TryParse(t.Id, out _) || t.Seats.Length != 4 || t.Seats.Any(s => s.Stack < 0 || s.Contribution < 0 || s.Stack + s.Contribution > 4000000000)
                || t.Seats.Where(s => s.Player.Length > 0).Select(s => s.Player).Distinct().Count() != t.Seats.Count(s => s.Player.Length > 0))
                throw new InvalidDataException("Poker ledger has invalid seats or cash. Automatic settlement blocked.");
        }
        Recover();
        foreach (var table in Document.Tables.Values) table.AbortForRecovery();
        Save();
    }
    internal void Save() => AtomicDocument.Write(Path, JsonSerializer.Serialize(Document));
    internal void CheckpointTo(string saveRoot)
    { AtomicDocument.Write(AtomicDocument.Within(saveRoot, "Modded/VICE/Poker/settlement.json"), JsonSerializer.Serialize(Document)); }
    internal bool First(string token) => !Document.Completed.Contains(token);
    internal void Mark(string token) { Document.Completed.Add(token); Save(); }
    internal void Transfer(string token, string player, string relativeInventory, string liveInventory, PokerTable after, long delta)
    {
        if (!First(token)) return;
        if (Document.Transfer is not null) throw new InvalidOperationException("An interrupted wallet transfer needs recovery before more cash can move.");
        var nativePath = AtomicDocument.Within(Root, relativeInventory);
        var before = PokerInventory.Cash(liveInventory); var target = PokerInventory.Target(before, delta);
        var transfer = new PokerTransfer { Token = token, Player = player, Inventory = relativeInventory, Before = before, After = target,
            NativeAfter = PokerInventory.WithCash(liveInventory, target), TableAfter = after,
            NativeBeforeHash = File.Exists(nativePath) ? Hash(File.ReadAllText(nativePath)) : "" };
        ValidateTransfer(transfer); Document.Transfer = transfer;
        Save(); // Intent must exist before touching native cash.
        AtomicDocument.Write(nativePath, Document.Transfer.NativeAfter);
        Commit(); // A crash before this point is recovered by inspecting the native cash value.
    }
    private void Commit()
    {
        var transfer = Document.Transfer!;
        Document.Tables[transfer.TableAfter.Id] = transfer.TableAfter;
        Document.Completed.Add(transfer.Token); Document.Transfer = null; Save();
    }
    private void Recover()
    {
        if (Document.Transfer is not { } transfer) return;
        ValidateTransfer(transfer);
        var inventory = AtomicDocument.Within(Root, transfer.Inventory);
        if (!File.Exists(inventory) && transfer.NativeBeforeHash.Length == 0) { Document.Transfer = null; Save(); return; }
        var native = File.ReadAllText(inventory);
        if (Hash(native) == transfer.NativeBeforeHash) { Document.Transfer = null; Save(); return; }
        var cash = PokerInventory.Cash(native);
        if (cash == transfer.After) { Commit(); return; }
        throw new InvalidDataException("Interrupted poker cash differs from both journal values. Preserve the ledger and inventory; automatic replay is blocked.");
    }
    private void ValidateTransfer(PokerTransfer transfer)
    {
        if (!Document.Tables.TryGetValue(transfer.TableAfter.Id, out var before)) throw new InvalidDataException("Transfer has no original table.");
        var delta = transfer.After - transfer.Before;
        if (!double.IsFinite(delta) || delta != Math.Floor(delta) || transfer.TableAfter.Held - before.Held != -delta || PokerInventory.Cash(transfer.NativeAfter) != transfer.After)
            throw new InvalidDataException("Native wallet and proposed table transfer do not conserve cash.");
        foreach (var owner in before.Seats.Concat(transfer.TableAfter.Seats).Select(s => s.Player).Where(p => p.Length > 0).Distinct())
        {
            var oldFunds = before.Seats.Where(s => s.Player == owner).Sum(s => s.Stack + s.Contribution);
            var newFunds = transfer.TableAfter.Seats.Where(s => s.Player == owner).Sum(s => s.Stack + s.Contribution);
            if (newFunds - oldFunds != (owner == transfer.Player ? -delta : 0)) throw new InvalidDataException("Wallet transfer changed another player's funds.");
        }
    }
    private static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
}
