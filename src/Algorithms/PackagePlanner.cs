namespace PersonalMods.Algorithms;

internal readonly record struct PackageStock(int Size, int Count);

internal static class PackagePlanner
{
    private sealed record Step(int Stock, int Count, Step? Previous);

    // Exact bounded knapsack. Binary splitting keeps large stacks efficient and avoids greedy dead ends.
    internal static int[]? Exact(IReadOnlyList<PackageStock> stock, int target)
    {
        if (target < 0 || target > 100_000 || stock.Any(x => x.Size <= 0 || x.Count < 0)) return null;
        var result = new int[stock.Count];
        if (target == 0) return result;
        var reachable = new bool[target + 1];
        var steps = new Step?[target + 1];
        reachable[0] = true;
        for (var index = 0; index < stock.Count; index++)
        {
            var remaining = Math.Min(stock[index].Count, target / stock[index].Size);
            for (var block = 1; remaining > 0; block *= 2)
            {
                var take = Math.Min(block, remaining);
                var units = take * stock[index].Size;
                remaining -= take;
                for (var amount = target; amount >= units; amount--)
                {
                    if (reachable[amount] || !reachable[amount - units]) continue;
                    reachable[amount] = true;
                    steps[amount] = new Step(index, take, steps[amount - units]);
                }
            }
        }
        if (!reachable[target]) return null;
        for (var step = steps[target]; step is not null; step = step.Previous) result[step.Stock] += step.Count;
        return result;
    }
}
