using PersonalMods.Algorithms;

var assertions = 0;
void Check(bool condition, string context)
{
    assertions++;
    if (!condition) throw new InvalidOperationException(context);
}

Check(PriceSearch.HighestAcceptable(1, 100, 0.98, _ => 0.1) is null, "Never offer $1 when no price meets the threshold.");
Check(PriceSearch.HighestAcceptable(1, 100, 0.98, _ => double.NaN) is null, "Reject undefined estimates.");
Check(PriceSearch.HighestAcceptable(1, int.MaxValue, 0.98, _ => 1) == int.MaxValue, "Maximum integer price must not overflow the search.");
Check(PriceSearch.HighestAcceptable(1, 0, 0.98, _ => 1) is null, "An empty budget cannot yield an offer.");
Check(PriceSearch.HighestAcceptable(1, 20, 1.1, _ => 1) is null, "Reject invalid probability thresholds.");
var random = new Random(947);
for (var run = 0; run < 2000; run++)
{
    var cap = random.Next(1, 500);
    var cutoff = random.Next(0, cap + 2);
    var threshold = random.NextDouble();
    double Probability(int price) => Math.Clamp(1.0 - (double)price / Math.Max(cutoff, 1), 0, 1);
    int? expected = null;
    for (var price = 1; price <= cap; price++) if (Probability(price) >= threshold) expected = price;
    Check(PriceSearch.HighestAcceptable(1, cap, threshold, Probability) == expected, "Binary price search differs from exhaustive offers.");
}

var mixed = PackagePlanner.Exact(new[] { new PackageStock(5, 1), new PackageStock(3, 2) }, 6);
Check(mixed is not null && mixed[0] == 0 && mixed[1] == 2, "Mixed packaging must escape the greedy 5-unit dead end.");
Check(PackagePlanner.Exact(new[] { new PackageStock(5, 1) }, 6) is null, "Do not over-deliver or fabricate inventory.");
Check(PackagePlanner.Exact(new[] { new PackageStock(3, 2) }, 9) is null, "Do not exceed a stack's stock.");
Check(PackagePlanner.Exact(new[] { new PackageStock(0, 3) }, 1) is null, "Reject a zero-size package.");
Check(PackagePlanner.Exact(new[] { new PackageStock(1, -1) }, 1) is null, "Reject negative stock.");
Check(PackagePlanner.Exact(Array.Empty<PackageStock>(), 0) is { Length: 0 }, "Empty zero-unit contracts require no packages.");
for (var run = 0; run < 2500; run++)
{
    var stock = Enumerable.Range(0, random.Next(1, 6)).Select(_ => new PackageStock(random.Next(1, 12), random.Next(0, 8))).ToArray();
    var target = random.Next(0, 80);
    bool BruteForce(int index, int remaining)
    {
        if (remaining == 0) return true;
        if (index == stock.Length) return false;
        for (var count = 0; count <= stock[index].Count && count * stock[index].Size <= remaining; count++)
            if (BruteForce(index + 1, remaining - count * stock[index].Size)) return true;
        return false;
    }
    var plan = PackagePlanner.Exact(stock, target);
    Check((plan is not null) == BruteForce(0, target), "Bounded delivery planning differs from exhaustive inventory choices.");
    if (plan is null) continue;
    Check(plan.Length == stock.Length && plan.Select((count, i) => count >= 0 && count <= stock[i].Count).All(x => x), "Plan overdraws inventory.");
    Check(plan.Select((count, i) => count * stock[i].Size).Sum() == target, "Plan changes the contract quantity.");
}
Check(PriceSearch.EstimateChance(0.5, double.NaN, 1, 5, 5, 0, 0) == 0, "Undefined customer estimates cannot be accepted.");
Check(PriceSearch.EstimateChance(0.5, 0.5, 1, 0, 5, 0, 0) == 0, "Invalid initial contracts cannot be optimized.");
Console.WriteLine($"PASS: {assertions} assertions; 4500 randomized comparisons against exhaustive price and packaging choices.");
