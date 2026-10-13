namespace VICE.Lifestyle.Poker;

internal sealed class PokerCommand
{
    public int Version { get; set; } = 1;
    public string Epoch { get; set; } = "";
    public string Token { get; set; } = Guid.NewGuid().ToString();
    public string Table { get; set; } = "";
    public string Instance { get; set; } = "";
    public long Revision { get; set; }
    public string Action { get; set; } = "sync";
    public int Seat { get; set; }
    public long Amount { get; set; }
    public long Small { get; set; }
    public long Big { get; set; }
    public long Minimum { get; set; }
    public long Maximum { get; set; }
    public double Cash { get; set; }
    public int Timeout { get; set; }
    internal void Validate()
    {
        if (Version != 1 || !Guid.TryParse(Token, out _) || Action is not ("sync" or "configure" or "buyin" or "cashout" or "start" or "fold" or "check" or "call" or "bet" or "raise" or "allin" or "leave" or "reserved" or "applied")
            || !double.IsFinite(Cash) || Amount is < 0 or > 4000000000 || Seat is < 0 or > 3 || Revision < 0) throw new InvalidDataException("Invalid poker request.");
        if (Action != "sync" && (!Guid.TryParse(Epoch, out _) || !Guid.TryParse(Table, out _) || !Guid.TryParse(Instance, out _))) throw new InvalidDataException("Stale poker session. Reopen the table.");
    }
}
internal static class PokerRequests
{
    internal static void Check(PokerTable table, PokerCommand command, string epoch)
    {
        command.Validate();
        if (command.Epoch != epoch || command.Table != table.Id || command.Instance != table.Instance)
            throw new InvalidOperationException("This poker request belongs to an earlier session. Reopen controls.");
        if (command.Revision != table.Revision) throw new InvalidOperationException("Table changed. Check the current action and try again.");
    }
}
internal sealed class PokerSeatSnapshot
{
    public string Player { get; set; } = "";
    public string Name { get; set; } = "";
    public long Stack { get; set; }
    public long Wager { get; set; }
    public bool Folded { get; set; }
    public bool InHand { get; set; }
    public bool Leaving { get; set; }
    public int[] Hole { get; set; } = Array.Empty<int>();
}
internal sealed class PokerSnapshot
{
    public string Id { get; set; } = "";
    public string Instance { get; set; } = "";
    public long Revision { get; set; }
    public string Organizer { get; set; } = "";
    public string Street { get; set; } = "between";
    public long Hand { get; set; }
    public long Small { get; set; }
    public long Big { get; set; }
    public long Minimum { get; set; }
    public long Maximum { get; set; }
    public int Dealer { get; set; }
    public int Turn { get; set; }
    public long Wager { get; set; }
    public long MinimumTo { get; set; }
    public bool RaiseAllowed { get; set; }
    public double Remaining { get; set; }
    public List<int> Board { get; set; } = new();
    public long Pot { get; set; }
    public List<PokerPot> Pots { get; set; } = new();
    public PokerSeatSnapshot[] Seats { get; set; } = Array.Empty<PokerSeatSnapshot>();
    public string Feedback { get; set; } = "";
    internal static PokerSnapshot For(PokerTable table, string recipient, double now) => new()
    {
        Id = table.Id, Instance = table.Instance, Revision = table.Revision, Organizer = table.Organizer, Street = table.Street, Hand = table.Hand,
        Small = table.SmallBlind, Big = table.BigBlind, Minimum = table.MinBuyIn, Maximum = table.MaxBuyIn, Dealer = table.Dealer, Turn = table.Turn,
        Wager = table.CurrentBet, MinimumTo = table.MinimumTo, RaiseAllowed = table.SeatOf(recipient) >= 0 && table.CanRaise(table.SeatOf(recipient)),
        Remaining = Math.Max(0, table.Deadline - now), Board = table.Board.ToList(), Pot = table.Seats.Sum(s => s.Contribution),
        Pots = table.Active ? table.SidePots() : table.Pots, Feedback = table.Feedback,
        Seats = table.Seats.Select(s => new PokerSeatSnapshot { Player = s.Player, Name = s.Name, Stack = s.Stack, Wager = s.Wager, Folded = s.Folded,
            InHand = s.InHand, Leaving = s.Leaving, Hole = s.Player == recipient || s.Revealed ? s.Hole.ToArray() : s.Hole.Select(_ => -1).ToArray() }).ToArray()
    };
}
internal sealed class PokerMessage
{
    public int Version { get; set; } = 1;
    public string Epoch { get; set; } = "";
    public long Sequence { get; set; }
    public string Sync { get; set; } = "";
    public string Notice { get; set; } = "";
    public string NoticeToken { get; set; } = "";
    public List<PokerSnapshot> Tables { get; set; } = new();
    public string Wallet { get; set; } = "";
    public PokerCommand? Request { get; set; }
    public double Before { get; set; }
    public double After { get; set; }
}
