using System.Text.Json;

namespace VICE.Lifestyle.Poker;

internal sealed class PokerSeat
{
    public string Player { get; set; } = "";
    public string Name { get; set; } = "";
    public long Stack { get; set; }
    public long Contribution { get; set; }
    public long Wager { get; set; }
    public bool InHand { get; set; }
    public bool Folded { get; set; }
    public bool Leaving { get; set; }
    public bool Acted { get; set; }
    public long LastFaced { get; set; }
    public long Reopen { get; set; }
    public int[] Hole { get; set; } = Array.Empty<int>();
    public bool Revealed { get; set; }
}

internal sealed class PokerPot
{
    public long Amount { get; set; }
    public int[] Eligible { get; set; } = Array.Empty<int>();
    public int[] Winners { get; set; } = Array.Empty<int>();
}

internal sealed class PokerTable
{
    public string Id { get; set; } = "";
    public string Instance { get; set; } = Guid.NewGuid().ToString();
    public long Revision { get; set; }
    public long Hand { get; set; }
    public PokerSeat[] Seats { get; set; } = Enumerable.Range(0, 4).Select(_ => new PokerSeat()).ToArray();
    public string Organizer { get; set; } = "";
    public long SmallBlind { get; set; } = 5;
    public long BigBlind { get; set; } = 10;
    public long MinBuyIn { get; set; } = 200;
    public long MaxBuyIn { get; set; } = 1000;
    public int Timeout { get; set; } = 60;
    public int Dealer { get; set; } = -1;
    public int Turn { get; set; } = -1;
    public string Street { get; set; } = "between";
    public long CurrentBet { get; set; }
    public long RaiseSize { get; set; }
    public double Deadline { get; set; }
    public int[] Deck { get; set; } = Array.Empty<int>();
    public int Drawn { get; set; }
    public List<int> Board { get; set; } = new();
    public List<PokerPot> Pots { get; set; } = new();
    public string Feedback { get; set; } = "Choose blinds and buy-in limits, then take a seat.";
    public bool Active => Street != "between";
    public long Held => Seats.Sum(s => s.Stack + s.Contribution);
    internal int SeatOf(string player) => Array.FindIndex(Seats, s => s.Player == player);
    internal int Next(int seat, Func<PokerSeat, bool> predicate)
    {
        for (var n = 1; n <= 4; n++) { var i = (seat + n + 4) % 4; if (predicate(Seats[i])) return i; }
        return -1;
    }
    internal void Configure(string player, long small, long big, long min, long max, int timeout)
    {
        if (Active || Held > 0 || Organizer.Length > 0 && Organizer != player) throw new InvalidOperationException("Cash out all stacks before changing the session.");
        if (small is < 1 or > 5000 || big < small * 2 || big > 10000 || min < big * 2 || max < min || max > 1000000 || timeout is < 10 or > 300)
            throw new InvalidOperationException("Use whole dollars: BB ≥ 2×SB; buy-in ≥ 2×BB, maximum $1,000,000; timeout 10–300s.");
        Organizer = player; SmallBlind = small; BigBlind = big; MinBuyIn = min; MaxBuyIn = max; Timeout = timeout; Revision++;
    }
    internal void Add(string player, string name, int seat, long amount)
    {
        if (seat is < 0 or > 3 || amount <= 0) throw new InvalidOperationException("Choose a free seat and a positive buy-in.");
        var s = Seats[seat]; var existing = SeatOf(player);
        if (existing >= 0 && existing != seat || s.Player.Length > 0 && s.Player != player || s.Leaving) throw new InvalidOperationException("Seat is occupied or awaiting settlement.");
        if (Active && existing >= 0) throw new InvalidOperationException("Additional cash is allowed between hands.");
        if (s.Stack + amount > MaxBuyIn || existing < 0 && amount < MinBuyIn) throw new InvalidOperationException("Buy-in must fit the table limits.");
        if (Organizer.Length == 0) Organizer = player;
        s.Player = player; s.Name = name; s.Stack += amount; Revision++;
        Feedback = $"{name} bought in for ${amount}." + (Active ? " Joins next hand." : "");
    }
    internal bool CanRaise(int seat) => !Seats[seat].Acted || CurrentBet - Seats[seat].LastFaced >= Seats[seat].Reopen;
    internal long MinimumTo => CurrentBet < BigBlind ? BigBlind : CurrentBet + RaiseSize;
    internal void Start(string player, double now, int[]? testingDeck = null)
    {
        if (Active || Organizer != player) throw new InvalidOperationException("The session organizer starts the next hand between hands.");
        foreach (var s in Seats) { s.InHand = s.Player.Length > 0 && s.Stack > 0 && !s.Leaving; s.Folded = false; s.Contribution = s.Wager = 0; s.Acted = false; s.Revealed = false; s.Hole = Array.Empty<int>(); }
        if (Seats.Count(s => s.InHand) < 2) throw new InvalidOperationException("Two funded players are needed.");
        Deck = testingDeck ?? PokerCards.Shuffle();
        if (Deck.Length != 52 || Deck.Distinct().Count() != 52 || Deck.Any(c => c is < 0 or > 51)) throw new InvalidDataException("Invalid deck.");
        Drawn = 0; Board.Clear(); Pots.Clear(); Hand++; Dealer = Next(Dealer, s => s.InHand); Street = "preflop";
        var dealSeat = Dealer;
        for (var round = 0; round < 2; round++) for (var n = 0; n < Seats.Count(s => s.InHand); n++)
        { dealSeat = Next(dealSeat, s => s.InHand); Seats[dealSeat].Hole = Seats[dealSeat].Hole.Append(Draw()).ToArray(); }
        var small = Seats.Count(s => s.InHand) == 2 ? Dealer : Next(Dealer, s => s.InHand);
        var big = Next(small, s => s.InHand); Pay(small, Math.Min(SmallBlind, Seats[small].Stack)); Pay(big, Math.Min(BigBlind, Seats[big].Stack));
        CurrentBet = BigBlind; RaiseSize = BigBlind;
        Turn = Next(big, CanAct); Deadline = now + Timeout; Feedback = $"Hand {Hand}: seat {Dealer + 1} has the button. Blinds ${SmallBlind}/${BigBlind}.";
        Revision++; Advance(now);
    }
    private int Draw() => Deck[Drawn++];
    private bool CanAct(PokerSeat s) => s.InHand && !s.Folded && s.Stack > 0;
    private bool NeedsAction(PokerSeat s) => CanAct(s) && (!s.Acted || s.Wager < CurrentBet);
    private void Pay(int seat, long amount) { var s = Seats[seat]; s.Stack -= amount; s.Contribution += amount; s.Wager += amount; }
    internal void Act(string player, string action, long to, double now)
    {
        var seat = SeatOf(player);
        if (!Active || seat < 0 || Turn != seat || !CanAct(Seats[seat])) throw new InvalidOperationException("Wait for your turn.");
        var s = Seats[seat]; var call = Math.Max(0, CurrentBet - s.Wager); var previous = CurrentBet;
        switch (action)
        {
            case "fold": s.Folded = true; break;
            case "check": if (call > 0) throw new InvalidOperationException("You must call or fold."); break;
            case "call": Pay(seat, Math.Min(call, s.Stack)); break;
            case "allin": to = s.Wager + s.Stack; goto case "raise";
            case "bet": case "raise":
                if (to <= CurrentBet) { if (action != "allin") throw new InvalidOperationException("A raise must exceed the current wager."); Pay(seat, s.Stack); break; }
                if (!CanRaise(seat)) throw new InvalidOperationException("A short all-in has not reopened your raise rights.");
                if (Seats.Count(CanAct) < 2) throw new InvalidOperationException("No opponent can match another raise.");
                if (to > s.Wager + s.Stack || to < MinimumTo && to != s.Wager + s.Stack) throw new InvalidOperationException($"Minimum raise to ${MinimumTo}, or all-in.");
                var full = to >= MinimumTo; Pay(seat, to - s.Wager); CurrentBet = to;
                if (full) RaiseSize = previous < BigBlind ? Math.Max(BigBlind, to - previous) : to - previous;
                break;
            default: throw new InvalidOperationException("Unknown poker action.");
        }
        s.Acted = true; s.LastFaced = CurrentBet; s.Reopen = RaiseSize;
        Feedback = $"{s.Name}: {action}" + (CurrentBet > previous ? $" to ${CurrentBet}" : "") + ".";
        Turn = Next(seat, NeedsAction); Deadline = now + Timeout; Revision++; Advance(now);
    }
    internal void Leave(string player, double now)
    {
        var seat = SeatOf(player); if (seat < 0) return;
        var s = Seats[seat]; s.Leaving = true;
        if (Organizer == player) Organizer = Seats.FirstOrDefault(x => x.Player.Length > 0 && !x.Leaving)?.Player ?? "";
        if (Active && s.InHand && !s.Folded && s.Stack > 0)
        {
            s.Folded = true; Feedback = s.Name + " left and folded; committed wagers remain in the pot.";
            if (Turn == seat) { Turn = Next(seat, NeedsAction); Deadline = now + Timeout; }
            Advance(now);
        }
        Revision++;
    }
    internal void TimeoutTurn(double now)
    {
        if (!Active || Turn < 0 || now < Deadline) return;
        var s = Seats[Turn]; var action = s.Wager >= CurrentBet ? "check" : "fold"; Act(s.Player, action, 0, now);
        Feedback = s.Name + " timed out: " + action + ". " + Feedback;
    }
    private void Advance(double now)
    {
        while (Active)
        {
            if (Seats.Count(s => s.InHand && !s.Folded) == 1) { Settle(false); return; }
            // A sole player with chips only acts if facing a wager. No betting into an all-in opponent.
            var actors = Seats.Where(CanAct).ToArray();
            if (actors.Length <= 1 && actors.All(s => s.Wager >= Seats.Where(other => other != s && other.InHand && !other.Folded).Select(other => other.Wager).DefaultIfEmpty(0).Max())) Turn = -1;
            else if (Seats.Any(NeedsAction)) { if (Turn < 0 || !NeedsAction(Seats[Turn])) Turn = Next(Dealer, NeedsAction); return; }
            RefundUncalled();
            if (Street == "river") { Settle(true); return; }
            foreach (var s in Seats) { s.Wager = 0; s.Acted = false; s.LastFaced = 0; s.Reopen = BigBlind; }
            CurrentBet = 0; RaiseSize = BigBlind; Draw(); // Burn card.
            if (Street == "preflop") { Street = "flop"; Board.Add(Draw()); Board.Add(Draw()); Board.Add(Draw()); }
            else { Street = Street == "flop" ? "turn" : "river"; Board.Add(Draw()); }
            Turn = Next(Dealer, CanAct); Deadline = now + Timeout;
        }
    }
    internal long RefundUncalled()
    {
        var order = Seats.Select((s, i) => (s, i)).OrderByDescending(p => p.s.Contribution).ToArray();
        var excess = order[0].s.Contribution - order[1].s.Contribution;
        if (excess <= 0) return 0;
        var s = order[0].s; s.Contribution -= excess; s.Wager = Math.Max(0, s.Wager - excess); s.Stack += excess;
        return excess;
    }
    internal List<PokerPot> SidePots()
    {
        var pots = new List<PokerPot>(); long previous = 0;
        foreach (var level in Seats.Select(s => s.Contribution).Where(c => c > 0).Distinct().OrderBy(c => c))
        {
            var eligible = Seats.Select((s, i) => (s, i)).Where(p => p.s.Contribution >= level && p.s.InHand && !p.s.Folded).Select(p => p.i).ToArray();
            var amount = (level - previous) * Seats.Count(s => s.Contribution >= level); previous = level;
            // Folded-only upper layers belong to the preceding contested pot.
            if (eligible.Length == 0 && pots.Count > 0) pots[^1].Amount += amount;
            else pots.Add(new() { Amount = amount, Eligible = eligible });
        }
        return pots;
    }
    private void Settle(bool showdown)
    {
        var total = Held; var returned = RefundUncalled();
        Pots = showdown ? SidePots() : new() { new() { Amount = Seats.Sum(s => s.Contribution), Eligible = Seats.Select((s, i) => (s, i)).Where(p => p.s.InHand && !p.s.Folded).Select(p => p.i).ToArray() } };
        foreach (var pot in Pots)
        {
            if (pot.Eligible.Length == 0) throw new InvalidDataException("Pot without an eligible player.");
            var ranks = pot.Eligible.ToDictionary(i => i, i => showdown ? PokerCards.Rank(Seats[i].Hole.Concat(Board).ToArray()) : 0L);
            var best = ranks.Values.Max(); pot.Winners = pot.Eligible.Where(i => ranks[i] == best).OrderBy(i => (i - Dealer + 3) % 4).ToArray();
            foreach (var i in pot.Winners) Seats[i].Stack += pot.Amount / pot.Winners.Length;
            for (var n = 0; n < pot.Amount % pot.Winners.Length; n++) Seats[pot.Winners[n]].Stack++;
            if (showdown) foreach (var i in pot.Eligible) Seats[i].Revealed = true;
        }
        Feedback = string.Join(" · ", Pots.Select((p, i) => $"{(i == 0 ? "Pot" : "Side pot " + i)} ${p.Amount}: " + string.Join(", ", p.Winners.Select(w => Seats[w].Name + (showdown ? " (" + PokerCards.Description(PokerCards.Rank(Seats[w].Hole.Concat(Board).ToArray())) + ")" : ""))))) + (returned > 0 ? $" · ${returned} uncalled returned" : "");
        foreach (var s in Seats) { s.Contribution = s.Wager = 0; s.InHand = false; }
        Street = "between"; Turn = -1; CurrentBet = 0; Deck = Array.Empty<int>(); Revision++;
        if (Held != total) throw new InvalidDataException("Poker settlement failed conservation.");
    }
    internal long CashOut(string player, long maximum = long.MaxValue)
    {
        var seat = SeatOf(player); if (seat < 0) return 0;
        var s = Seats[seat]; if (Active && s.InHand) throw new InvalidOperationException("Cash-out is queued until the hand settles.");
        if (maximum <= 0) throw new InvalidOperationException("Cash-out must have positive wallet capacity.");
        var amount = Math.Min(s.Stack, maximum); s.Stack -= amount;
        if (s.Stack == 0) Seats[seat] = new(); else s.Leaving = true;
        if (Organizer == player) Organizer = Seats.FirstOrDefault(x => x.Player.Length > 0 && !x.Leaving)?.Player ?? "";
        Feedback = $"{s.Name} cashed out ${amount}." + (s.Stack > 0 ? $" ${s.Stack} remains owed until the native wallet has room." : "");
        Revision++; return amount;
    }
    internal void AbortForRecovery()
    {
        // An interrupted hand has no winner. Return each contribution to its original owner.
        foreach (var s in Seats) { s.Stack += s.Contribution; s.Contribution = s.Wager = 0; s.InHand = false; s.Hole = Array.Empty<int>(); s.Revealed = false; s.Leaving = s.Player.Length > 0; }
        Deck = Array.Empty<int>(); Board.Clear(); Pots.Clear(); Street = "between"; Turn = -1; CurrentBet = 0;
        Feedback = "Interrupted session: each player's own stack and contributions await cash-out."; Revision++;
    }
    internal PokerTable Copy() => JsonSerializer.Deserialize<PokerTable>(JsonSerializer.Serialize(this))!;
}
