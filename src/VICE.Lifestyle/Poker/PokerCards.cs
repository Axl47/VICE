using System.Security.Cryptography;

namespace VICE.Lifestyle.Poker;

// Cards 0..51: clubs, diamonds, hearts, spades; ranks 2..Ace in each suit.
internal static class PokerCards
{
    internal static string Name(int card) => card < 0 ? "??" : "23456789TJQKA"[card % 13].ToString() + "CDHS"[card / 13];
    internal static int[] Shuffle()
    {
        var deck = Enumerable.Range(0, 52).ToArray();
        for (var i = 51; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
        return deck;
    }
    internal static long Rank(params int[] cards)
    {
        if (cards.Length is < 5 or > 7 || cards.Any(c => c is < 0 or > 51) || cards.Distinct().Count() != cards.Length)
            throw new InvalidDataException("Hand needs five to seven distinct cards.");
        long best = 0;
        for (var a = 0; a < cards.Length - 4; a++) for (var b = a + 1; b < cards.Length - 3; b++)
        for (var c = b + 1; c < cards.Length - 2; c++) for (var d = c + 1; d < cards.Length - 1; d++)
        for (var e = d + 1; e < cards.Length; e++) best = Math.Max(best, Five(new[] { cards[a], cards[b], cards[c], cards[d], cards[e] }));
        return best;
    }
    private static long Five(int[] cards)
    {
        var ranks = cards.Select(c => c % 13 + 2).OrderByDescending(r => r).ToArray();
        var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToArray();
        var flush = cards.All(c => c / 13 == cards[0] / 13);
        var straight = ranks.Distinct().Count() == 5 && ranks[0] - ranks[4] == 4 ? ranks[0] : ranks.SequenceEqual(new[] { 14, 5, 4, 3, 2 }) ? 5 : 0;
        var category = flush && straight > 0 ? 8 : groups[0].Count() == 4 ? 7 : groups[0].Count() == 3 && groups[1].Count() == 2 ? 6
            : flush ? 5 : straight > 0 ? 4 : groups[0].Count() == 3 ? 3 : groups[0].Count() == 2 && groups[1].Count() == 2 ? 2 : groups[0].Count() == 2 ? 1 : 0;
        var keys = category is 8 or 4 ? new[] { straight } : category is 5 or 0 ? ranks : groups.Select(g => g.Key).ToArray();
        long value = category;
        for (var i = 0; i < 5; i++) value = value * 15 + (i < keys.Length ? keys[i] : 0);
        return value;
    }
    internal static string Description(long rank) => new[] { "high card", "pair", "two pair", "three of a kind", "straight", "flush", "full house", "four of a kind", "straight flush" }[(int)(rank / 759375)];
}
