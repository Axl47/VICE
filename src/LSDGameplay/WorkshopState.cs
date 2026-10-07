using System.Text.Json;

namespace PersonalMods.LSDGameplay;

public enum MachineStage { Empty, Preparing, Printing, WetOutput, Drying, DryOutput, Cutting, TabOutput }
public enum OrderStage { InTransit, WaitingForSpace, Delivered }

public sealed class Artwork
{
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public byte[] Pixels { get; set; } = new byte[4096];
    public bool Repeated { get; set; }
}

public sealed class MachineState
{
    public string Id { get; set; } = "";
    public string Property { get; set; } = "";
    public string Kind { get; set; } = "bench";
    public MachineStage Stage { get; set; }
    public int ArtSlot { get; set; }
    public int Yield { get; set; } = WorkshopRules.SheetTabs;
    public int DueMinute { get; set; }
}

public sealed class CollectiveOrder
{
    public string Id { get; set; } = "";
    public int Quantity { get; set; }
    public int DueMinute { get; set; }
    public string Shop { get; set; } = "";
    public int Bay { get; set; }
    public OrderStage Stage { get; set; }
}

public sealed class WorkshopState
{
    public int Version { get; set; } = 1;
    public long Revision { get; set; }
    public bool Introduced { get; set; }
    public int TutorialStep { get; set; }
    public int? TutorialObjectives { get; set; }
    public List<Artwork> Artworks { get; set; } = PresetArt.Create();
    public List<MachineState> Machines { get; set; } = new();
    public List<CollectiveOrder> Orders { get; set; } = new();

    public void Validate()
    {
        if (Version != 1 || Revision < 0 || TutorialStep is < 0 or > 10 || TutorialObjectives is < 0 or > TutorialProgress.All || Artworks is null || Machines is null || Orders is null)
            throw new InvalidDataException("Unsupported or incomplete workshop save. Keep the save and restore a compatible mod version.");
        if (Artworks.Count is < 3 or > WorkshopRules.ArtSlots || Machines.Count > 64 || Orders.Count > 128)
            throw new InvalidDataException("Workshop save exceeds the slice's content limits.");
        if (Artworks.Any(a=>a is null) || Machines.Any(m=>m is null) || Orders.Any(o=>o is null))
            throw new InvalidDataException("Workshop save contains an empty record.");
        if (Artworks.Select(a => a.Slot).Distinct().Count() != Artworks.Count || Machines.Select(m => m.Id).Distinct().Count() != Machines.Count
            || Orders.Select(o => o.Id).Distinct().Count() != Orders.Count)
            throw new InvalidDataException("Duplicate artwork, machine or order identity.");
        foreach (var art in Artworks) WorkshopRules.ValidateArt(art);
        foreach (var machine in Machines)
        {
            if (!Guid.TryParse(machine.Id, out _) || string.IsNullOrWhiteSpace(machine.Property) || machine.Property.Length > 80
                || machine.Kind is not ("bench" or "cabinet") || !Enum.IsDefined(typeof(MachineStage), machine.Stage)
                || machine.Yield is < 32 or > WorkshopRules.SheetTabs || machine.DueMinute < 0
                || Artworks.All(a => a.Slot != machine.ArtSlot)) throw new InvalidDataException("Invalid saved machine state.");
            if (machine.Kind == "cabinet" && machine.Stage is not (MachineStage.Empty or MachineStage.Drying or MachineStage.DryOutput))
                throw new InvalidDataException("Invalid cabinet stage.");
            if (machine.Kind == "bench" && machine.Stage is MachineStage.Drying or MachineStage.DryOutput)
                throw new InvalidDataException("Invalid bench stage.");
        }
        foreach (var order in Orders)
            if (!Guid.TryParse(order.Id, out _) || order.Quantity is < 1 or > 4 || order.DueMinute < 0 || string.IsNullOrEmpty(order.Shop)
                || order.Shop.Length > 80 || order.Bay < 0 || !Enum.IsDefined(typeof(OrderStage), order.Stage)) throw new InvalidDataException("Invalid saved collective order.");
    }

    public Artwork AddArt(string name, byte[] pixels, bool repeated)
    {
        var slot = Enumerable.Range(3, WorkshopRules.ArtSlots - 3).FirstOrDefault(s => Artworks.All(a => a.Slot != s), -1);
        if (slot < 0) throw new InvalidOperationException("All five studio revisions are in use. Existing editions stay immutable; use a saved edition.");
        var art = new Artwork { Slot = slot, Name = name.Trim(), Pixels = (byte[])pixels.Clone(), Repeated = repeated };
        WorkshopRules.ValidateArt(art);
        Artworks.Add(art); Revision++;
        return art;
    }

    public bool Tick(int minute)
    {
        var changed = false;
        foreach (var machine in Machines)
            if (machine.Stage == MachineStage.Drying && minute >= machine.DueMinute)
            { machine.Stage = MachineStage.DryOutput; changed = true; }
        if (changed) Revision++;
        return changed;
    }

    public string Serialize() { Validate(); return JsonSerializer.Serialize(this); }
    public static WorkshopState Deserialize(string json)
    {
        if (json.Length > WorkshopRules.MaximumPayload) throw new InvalidDataException("Workshop snapshot is too large.");
        var state = JsonSerializer.Deserialize<WorkshopState>(json) ?? throw new InvalidDataException("Empty workshop save.");
        state.Validate();state.TutorialObjectives??=TutorialProgress.FromLegacy(state.TutorialStep);
        // Repair inflated coarse progress from an earlier 0.2.5 build when its
        // independently saved objectives show an unfinished prerequisite.
        if(state.Introduced || state.TutorialStep>0 || state.TutorialObjectives>0)
            state.TutorialStep=TutorialProgress.LegacyStep(state.TutorialObjectives.Value);
        return state;
    }
}

public static class WorkshopRules
{
    public const int ArtSlots = 8, SheetTabs = 40, DryMinutes = 120, DeliveryMinutes = 30, MaximumPayload = 131072;
    public const string Owner = "personal.lsd", Bench = Owner + ":bench", Cabinet = Owner + ":cabinet",
        Prism = Owner + ":prism", Paper = Owner + ":paper", Ink = Owner + ":ink", Sleeve = Owner + ":sleeve";
    public static string Product(int slot) => $"{Owner}:tab-{slot}";
    public static string Sheet(bool dry, int slot, int yield) => $"{Owner}:{(dry ? "dry" : "wet")}-{slot}-{yield}";
    public static string EditionLabel(int slot) => slot switch { 0 => "Sunburn", 1 => "Prism", 2 => "Night Shift", _ => $"Studio {slot - 2:00}" };
    public static int Loss(float accuracy, int maximum)
    {
        if (!float.IsFinite(accuracy) || accuracy is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(accuracy));
        return (int)MathF.Round((1 - accuracy) * maximum);
    }
    public static void ValidateArt(Artwork art)
    {
        if (art.Slot is < 0 or >= ArtSlots || string.IsNullOrWhiteSpace(art.Name) || art.Name.Length > 32
            || art.Name.Any(char.IsControl) || art.Pixels is null || art.Pixels.Length != 4096 || art.Pixels.Any(p => p > 7))
            throw new InvalidDataException("Artwork requires a short name and a 64 by 64 drawing from the studio palette.");
    }
    public static bool TrySheet(string id, out bool dry, out int art, out int yield)
    {
        dry = false; art = yield = 0;
        if (!id.StartsWith(Owner + ":", StringComparison.Ordinal)) return false;
        var pieces = id[(Owner.Length + 1)..].Split('-');
        if (pieces.Length != 3 || pieces[0] is not ("wet" or "dry") || !int.TryParse(pieces[1], out art)
            || !int.TryParse(pieces[2], out yield) || art is < 0 or >= ArtSlots || yield is < 36 or > SheetTabs) return false;
        dry = pieces[0] == "dry"; return true;
    }
}

public static class PresetArt
{
    public static List<Artwork> Create()
    {
        var result = new List<Artwork>();
        for (var slot = 0; slot < 3; slot++)
        {
            var art = new Artwork { Slot = slot, Name = WorkshopRules.EditionLabel(slot), Repeated = slot == 2 };
            for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
            {
                var dx = x - 31.5; var dy = y - 31.5; var radius = Math.Sqrt(dx * dx + dy * dy);
                art.Pixels[y * 64 + x] = slot switch {
                    0 => (byte)(radius < 16 ? 3 : ((int)(Math.Atan2(dy, dx) * 9 + radius * 0.25) & 1) == 0 ? 2 : 0),
                    1 => (byte)(1 + ((x + y) / 9 % 6)),
                    _ => (byte)((Math.Abs(dx) + Math.Abs(dy) < 21) ? 5 : (x / 8 + y / 8) % 2 == 0 ? 1 : 7)
                };
            }
            result.Add(art);
        }
        return result;
    }
}
