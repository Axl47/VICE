namespace PersonalMods.Algorithms;

internal static class PriceSearch
{
    // Probability is monotonically non-increasing in price for a fixed product and quantity.
    // A null result is deliberately distinct from $1: some offers have no acceptable price.
    internal static int? HighestAcceptable(int minimum, int maximum, double threshold, Func<int, double> probability)
    {
        if (minimum < 1 || maximum < minimum || !double.IsFinite(threshold) || threshold is < 0 or > 1) return null;
        var best = (int?)null;
        var low = minimum;
        var high = maximum;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var chance = probability(middle);
            if (double.IsFinite(chance) && chance >= threshold)
            {
                best = middle;
                if (middle == int.MaxValue) break;
                low = middle + 1;
            }
            else high = middle - 1;
        }
        return best;
    }

    internal static double EstimateChance(double initialValue, double currentValue, double enjoyment,
        int initialQuantity, int quantity, double addiction, double relationship)
    {
        if (initialQuantity <= 0 || quantity <= 0 || !double.IsFinite(initialValue + currentValue + enjoyment + addiction + relationship)) return 0;
        var quantityFactor = Math.Clamp(1 - Math.Abs(Math.Pow((double)quantity / initialQuantity, 0.6) - 1), 0, 1);
        if (currentValue * quantityFactor > initialValue) return 1;
        if (currentValue < 0.12) return 0;
        var originalAppeal = enjoyment * initialValue;
        var counterAppeal = Math.Clamp((enjoyment + 1) / 2, 0, 1) * quantityFactor * currentValue;
        if (counterAppeal > originalAppeal) return 1;
        var penalty = Math.Clamp((originalAppeal - counterAppeal) / 0.2, 0, 1);
        var bonus = 0.2 * Math.Clamp(Math.Max(addiction, relationship), 0, 1);
        return Math.Clamp((0.9 - penalty + bonus) / 0.9, 0, 1);
    }
}
