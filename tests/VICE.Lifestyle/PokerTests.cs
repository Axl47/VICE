using System.Text.Json;
using VICE.Core;
using VICE.Lifestyle.Poker;

internal static class PokerTests
{
    private static int C(string rank, int suit = 0) => "23456789TJQKA".IndexOf(rank, StringComparison.Ordinal) + suit * 13;
    private static void Reject(Action action, Action<bool, string> check, string message)
    { try { action(); } catch (Exception e) when (e is InvalidOperationException or InvalidDataException) { check(true, message); return; } check(false, message); }
    private static PokerTable Table(params long[] stacks)
    {
        var table = new PokerTable { Id = Guid.NewGuid().ToString() }; table.Configure("p0", 5, 10, 20, 1000000, 30);
        for (var i = 0; i < stacks.Length; i++) table.Add("p" + i, "Player " + i, i, stacks[i]);
        return table;
    }
    internal static void Run(Action<bool, string> provided)
    {
        var assertions = 0;
        void check(bool condition, string message) { provided(condition, message); assertions++; }
        var hands = new[]
        {
            new[] { C("A"), C("J",1), C("9",2), C("7",3), C("3") },
            new[] { C("A"), C("A",1), C("9",2), C("7",3), C("3") },
            new[] { C("A"), C("A",1), C("9",2), C("9",3), C("3") },
            new[] { C("A"), C("A",1), C("A",2), C("7",3), C("3") },
            new[] { C("A"), C("2",1), C("3",2), C("4",3), C("5") },
            new[] { C("A"), C("J"), C("9"), C("7"), C("3") },
            new[] { C("A"), C("A",1), C("A",2), C("7",3), C("7") },
            new[] { C("A"), C("A",1), C("A",2), C("A",3), C("3") },
            new[] { C("A"), C("K"), C("Q"), C("J"), C("T") }
        };
        var ranks = hands.Select(h => PokerCards.Rank(h)).ToArray();
        check(ranks.Select(r => (int)(r / 759375)).SequenceEqual(Enumerable.Range(0,9)), "All nine hand categories.");
        check(ranks.Zip(ranks.Skip(1), (a,b) => a < b).All(x => x), "Category ordering.");
        check(PokerCards.Rank(C("A"), C("2",1), C("3",2), C("4",3), C("5"), C("6",1), C("K",2)) / 50625 % 15 == 6, "Best seven-card straight uses six, not wheel.");
        check(PokerCards.Rank(C("A"),C("A",1),C("A",2),C("K"),C("K",1),C("K",2),C("2")) == PokerCards.Rank(C("A"),C("A",1),C("A",2),C("K"),C("K",1)), "Two trips selects aces full of kings.");
        check(PokerCards.Rank(C("A"),C("K"),C("Q"),C("J"),C("T"),C("2",1),C("3",2)) == PokerCards.Rank(C("A"),C("K"),C("Q"),C("J"),C("T"),C("4",1),C("5",2)), "Board royal ties irrespective of hole kickers.");
        check(PokerCards.Rank(C("K"),C("K",1),C("A"),C("J",2),C("9",3)) > PokerCards.Rank(C("K"),C("K",1),C("A"),C("T",2),C("9",3)), "Pair tie resolved by second kicker.");
        Reject(() => PokerCards.Rank(0,0,1,2,3), check, "Duplicate cards rejected.");
        for (var i=0;i<64;i++) check(PokerCards.Shuffle().OrderBy(c=>c).SequenceEqual(Enumerable.Range(0,52)), "Fresh shuffled deck is a permutation.");

        var t = Table(200,200); t.Start("p0",0,Enumerable.Range(0,52).ToArray());
        Reject(()=>Table(200,200).Configure("p0",long.MaxValue,10,20,1000,60),check,"Overflowing blind inputs rejected.");
        check(t.Dealer==0 && t.Turn==0 && t.Seats[0].Wager==5 && t.Seats[1].Wager==10, "Heads-up dealer is SB and acts first preflop.");
        t.Act("p0","call",0,1); check(t.Turn==1 && t.Street=="preflop", "Big blind gets option after call.");
        t.Act("p1","check",0,2); check(t.Street=="flop" && t.Turn==1 && t.Board.Count==3, "Heads-up nondealer acts first postflop.");
        while(t.Active) t.Act(t.Seats[t.Turn].Player,"check",0,3);
        check(t.Held==400 && t.Board.Count==5 && t.Seats.All(s=>s.Contribution==0), "Heads-up hand settles and conserves cash.");
        t.Start("p0",4); check(t.Dealer==1 && t.Turn==1,"Dealer rotates in heads-up.");
        t.Leave("p1",5); check(!t.Active && t.Held==400 && !t.Seats[0].Revealed, "Leaving folds, sole winner is not required to reveal.");

        t=Table(1000,1000,1000,1000);t.Start("p0",0);
        check(t.Turn==3&&t.Seats[1].Wager==5&&t.Seats[2].Wager==10,"Four-seat blind/action order.");
        Reject(()=>t.Act("p0","call",0,0),check,"Out-of-turn request rejected.");
        Reject(()=>t.Act("p3","raise",19,0),check,"Under-minimum non-all-in rejected.");
        t.Act("p3","raise",100,1);check(t.RaiseSize==90&&t.MinimumTo==190,"Minimum raise uses full raise increment.");
        t.Act("p0","call",0,2);t.Seats[1].Stack=145; // Short all-in to 150.
        var held=t.Held;t.Act("p1","allin",0,3);t.Act("p2","call",0,4);
        check(t.Turn==3&&!t.CanRaise(3)&&!t.CanRaise(0),"Short all-in does not reopen prior callers.");
        Reject(()=>t.Act("p3","raise",240,5),check,"Closed raise rights enforced.");
        t.Act("p3","call",0,5);t.Act("p0","call",0,6);check(t.Street=="flop"&&t.Held==held,"Short raise calls close street without losing cash.");
        t=Table(1000,1000,1000,1000);t.Start("p0",0);t.Act("p3","raise",100,1);t.Act("p0","call",0,2);
        t.Seats[1].Stack=145;t.Seats[2].Stack=190;t.Act("p1","allin",0,3);t.Act("p2","allin",0,4);
        check(t.CurrentBet==200&&t.CanRaise(3)&&t.CanRaise(0),"Cumulative short all-ins reopen after full increment faced.");
        t.Act("p3","raise",290,5);check(t.RaiseSize==90,"Full raise after cumulative shorts.");

        t=Table(20,40,100,200);t.Start("p0",0);var all=t.Held;
        while(t.Active) { var s=t.Seats[t.Turn];t.Act(s.Player,"allin",0,1); }
        check(t.Held==all&&t.Pots.Select(p=>p.Amount).SequenceEqual(new long[]{80,60,120}),"Three all-in pot tiers and uncalled excess returned.");
        check(t.Pots[0].Eligible.Length==4&&t.Pots[1].Eligible.Length==3&&t.Pots[2].Eligible.Length==2,"Side-pot eligibility respects different stacks.");
        t=Table(200,200,200);t.Start("p0",0);t.Act("p0","raise",50,1);t.Act("p1","fold",0,2);t.Act("p2","fold",0,3);
        check(t.Held==600&&t.Pots.Sum(p=>p.Amount)==25&&t.Seats[0].Stack==215,"Folded blinds remain; excess raise refunded.");
        t=Table(20,20);t.Seats[1].Stack=3;t.Start("p0",0);
        check(!t.Active&&t.Held==23,"Heads-up short blind runs out without a phantom call.");
        t=Table(100,20);t.Seats[1].Stack=6;t.Start("p0",0);
        check(t.CurrentBet==6&&t.Turn==0&&t.CurrentBet-t.Seats[0].Wager==1,"Sole active player faces the actual short blind, not a nominal full blind.");
        t.Act("p0","call",0,1);check(!t.Active&&t.Held==106,"Calling a short heads-up blind settles without over-collecting.");
        t=Table(200,200,200,200);t.Start("p0",0);t.Leave("p3",1);t.Leave("p1",1);t.Leave("p2",1);
        check(!t.Active&&t.Held==800&&t.Seats[0].Stack==210,"Sole eligible player can win blinds without having contributed or revealed.");

        // An intentionally tied board with a folded small blind makes an odd-dollar split.
        t=Table(100,100,100);t.Start("p0",0);t.Act("p0","call",0,1);t.Act("p1","fold",0,2);t.Act("p2","check",0,3);
        t.Board=new(){C("A"),C("K"),C("Q"),C("J"),C("T")};t.Street="river";
        t.Seats[0].Hole=new[]{C("2",1),C("3",1)};t.Seats[2].Hole=new[]{C("4",2),C("5",2)};
        while(t.Active)t.Act(t.Seats[t.Turn].Player,"check",0,4);
        check(t.Seats[2].Stack==103&&t.Seats[0].Stack==102&&t.Seats[1].Stack==95,"Odd dollar goes first clockwise left of button.");
        t=Table(200,200);t.Start("p0",0);t.Act("p0","allin",0,1);t.Leave("p0",2);
        check(!t.Seats[0].Folded&&t.Seats[0].Leaving,"Departing all-in retains eligibility.");t.Act("p1","call",0,3);check(t.Held==400,"All-in disconnect settles without refund duplication.");
        t=Table(200,200);t.Start("p0",0);t.TimeoutTurn(31);check(!t.Active&&t.Seats[0].Folded,"Timeout folds when facing blind.");
        t=Table(200,200);t.Start("p0",0);t.Act("p0","call",0,1);t.TimeoutTurn(32);check(t.Street=="flop","Timeout checks when possible.");
        Reject(()=>t.Add("p0","Player",0,20),check,"Mid-hand additional cash rejected.");
        t.Add("p2","Player 2",2,50);check(!t.Seats[2].InHand&&t.Seats[2].Stack==50,"New arrival waits for next hand.");
        var own=PokerSnapshot.For(t,"p0",32);var guest=PokerSnapshot.For(t,"p1",32);var spectator=PokerSnapshot.For(t,"nobody",32);
        check(own.Seats[0].Hole.All(c=>c>=0)&&own.Seats[1].Hole.All(c=>c==-1),"Owner receives only own private cards.");
        check(guest.Seats[0].Hole.All(c=>c==-1)&&guest.Seats[1].Hole.All(c=>c>=0),"Guest snapshot is recipient-specific.");
        check(spectator.Seats.SelectMany(s=>s.Hole).All(c=>c==-1)&&!JsonSerializer.Serialize(spectator).Contains("Deck"),"Spectator gets no hidden cards or deck.");
        var original=t.Held;var stacks=t.Seats.Select(s=>s.Stack+s.Contribution).ToArray();t.AbortForRecovery();
        check(t.Held==original&&t.Seats.Select(s=>s.Stack).SequenceEqual(stacks)&&t.Seats.Where(s=>s.Player.Length>0).All(s=>s.Leaving),"Interrupted hand restores each owner's funds for cash-out.");
        var epoch=Guid.NewGuid().ToString();var request=new PokerCommand{Epoch=epoch,Table=t.Id,Instance=t.Instance,Revision=t.Revision,Action="start"};
        PokerRequests.Check(t,request,epoch);
        request.Revision--;Reject(()=>PokerRequests.Check(t,request,epoch),check,"Stale revision cannot act or charge.");request.Revision++;
        Reject(()=>PokerRequests.Check(t,request,Guid.NewGuid().ToString()),check,"Previous save epoch rejected.");
        request.Instance=Guid.NewGuid().ToString();Reject(()=>PokerRequests.Check(t,request,epoch),check,"Replaced table instance rejected.");
        request.Action="inventcash";Reject(request.Validate,check,"Unknown actions cannot mint stacks.");
        var restored=0;
        using(var lease=new PresentationLease<int>(()=>42,()=>{},s=>{check(s==42,"Camera/control snapshot retained.");restored++;})) {lease.Dispose();lease.Dispose();}
        check(restored==1,"Control restoration runs once.");
        Reject(()=>new PresentationLease<int>(()=>1,()=>throw new InvalidOperationException("partial"),_=>restored++),check,"Partially failed acquisition restores controls.");
        check(restored==2,"Partial camera acquisition releases ownership.");

        var random=new Random(1776);
        for(var run=0;run<250;run++)
        {
            t=Table(Enumerable.Range(0,2+run%3).Select(_=>(long)random.Next(20,500)).ToArray());t.Start("p0",0);var total=t.Held;var count=0;
            while(t.Active)
            {
                var seat=t.Turn;var s=t.Seats[seat];var call=t.CurrentBet-s.Wager;var choices=random.Next(6);
                if(choices==0)t.Act(s.Player,"fold",0,count);
                else if(choices==1&&t.CanRaise(seat)&&t.Seats.Count(x=>x.InHand&&!x.Folded&&x.Stack>0)>1&&s.Wager+s.Stack>t.CurrentBet)t.Act(s.Player,"allin",0,count);
                else if(choices==2&&t.CanRaise(seat)&&s.Wager+s.Stack>=t.MinimumTo&&t.Seats.Count(x=>x.InHand&&!x.Folded&&x.Stack>0)>1)t.Act(s.Player,"raise",t.MinimumTo,count);
                else t.Act(s.Player,call>0?"call":"check",0,count);
                check(t.Held==total&&t.Seats.All(x=>x.Stack>=0&&x.Contribution>=0),"Random legal action conserves every dollar.");
                if(++count>300)throw new Exception("Poker hand did not terminate.");
            }
            check(t.Pots.All(p=>p.Winners.All(w=>p.Eligible.Contains(w)))&&t.Pots.Sum(p=>p.Amount)<=total,"Random payouts only to eligible hands.");
        }
        JournalChecks(check);
        Console.WriteLine($"PASS: {assertions} poker ranking, betting, private snapshot, conservation and settlement recovery checks.");
    }
    internal static void Exhaustive(Action<bool, string> check)
    {
        var histogram = new long[9];
        for(var a=0;a<48;a++)for(var b=a+1;b<49;b++)for(var c=b+1;c<50;c++)for(var d=c+1;d<51;d++)for(var e=d+1;e<52;e++)
            histogram[PokerCards.Rank(a,b,c,d,e)/759375]++;
        check(histogram.SequenceEqual(new long[]{1302540,1098240,123552,54912,10200,5108,3744,624,40}),"Exhaustive 2,598,960 five-card category distribution.");
        Console.WriteLine("PASS: exhaustive 2,598,960 five-card hand distribution matches all nine known counts.");
    }
    private static void JournalChecks(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"VICE-Poker-"+Guid.NewGuid());Directory.CreateDirectory(root);
        const string relative="Players/Player_test/Inventory.json";
        const string empty="{\"DataType\":\"ItemData\",\"DataVersion\":0,\"GameVersion\":\"0.4.7f11\",\"ID\":\"\",\"Quantity\":0}";
        var items=Enumerable.Repeat(empty,9).ToArray();items[8]="{\"DataType\":\"CashData\",\"ID\":\"cash\",\"Quantity\":1,\"CashBalance\":1000}";
        var inventory=JsonSerializer.Serialize(new{Items=items});
        var native=AtomicDocument.Within(root,relative);AtomicDocument.Write(native,inventory);
        try
        {
            items[0]="{\"DataType\":\"CashData\",\"ID\":\"cash\",\"Quantity\":1,\"CashBalance\":777}";
            items[8]=empty;var zero=JsonSerializer.Serialize(new{Items=items});
            check(PokerInventory.Cash(zero)==0&&PokerInventory.Cash(PokerInventory.WithCash(zero,200))==200,"Cleared zero-cash slot can receive cash-out.");
            check(JsonDocument.Parse(PokerInventory.WithCash(zero,200)).RootElement.GetProperty("Items")[0].GetString()==items[0],"Physical cash in a hotbar slot is preserved and never used as the pocket wallet.");
            Reject(()=>PokerInventory.Cash("{\"Items\":[]}"),check,"Changed native inventory layout blocked.");
            var journal=new PokerJournal(root);var t=Table(200,200);journal.Document.Tables[t.Id]=t; journal.Save();
            var invalid=t.Copy();invalid.Seats[0].Stack+=100;
            Reject(()=>journal.Transfer("bad","p0",relative,inventory,invalid,-50),check,"Wallet and stack imbalance rejected before native write.");
            invalid=t.Copy();invalid.Seats[1].Stack+=100;
            Reject(()=>journal.Transfer("wrong-owner","p0",relative,inventory,invalid,-100),check,"Transfer cannot credit another owner's stack.");
            check(journal.Document.Transfer is null&&PokerInventory.Cash(File.ReadAllText(native))==1000,"Rejected transfer leaves no native debit or prepared intent.");
            var after=t.Copy();after.Add("p0","Player0",0,100);var token="p0/"+Guid.NewGuid();journal.Transfer(token,"p0",relative,inventory,after,-100);
            check(PokerInventory.Cash(File.ReadAllText(native))==900&&journal.Document.Tables[t.Id].Held==500,"Write-through cash debit matches stack credit.");
            journal.Transfer(token,"p0",relative,inventory,after,-100);check(PokerInventory.Cash(File.ReadAllText(native))==900,"Duplicate transfer cannot debit twice.");
            check(!journal.First(token),"Durable duplicate request marker.");
            journal=new(root);check(journal.Document.Tables[t.Id].Held==500&&journal.Document.Tables[t.Id].Seats[0].Leaving,"Reload retains offline owed stack.");
            after=journal.Document.Tables[t.Id].Copy();var paid=after.CashOut("p0");inventory=File.ReadAllText(native);
            journal.Transfer("p0/"+Guid.NewGuid(),"p0",relative,inventory,after,paid);check(PokerInventory.Cash(File.ReadAllText(native))==1200&&journal.Document.Tables[t.Id].Held==200,"Cash-out transfers entire remainder once.");
            Reject(()=>PokerInventory.Target(2,-3),check,"Insufficient native wallet rejected.");
            Reject(()=>PokerInventory.Target(8388607.5,1),check,"Fractional native precision loss rejected.");
            check(PokerInventory.CashOutAmount(999999936,100)==64,"Cash-out respects native wallet cap and leaves an owed remainder.");
            check(PokerInventory.CashOutAmount(1000,50000001)==50000000,"Cash-out preserves an unrepresentable odd-dollar remainder.");
            check(PokerInventory.CashOutAmount(4194303.5,5000000)==4194304,"Cash-out stops before losing an existing native half dollar.");
            var partial=Table(200,200);partial.Leave("p0",0);var paidPartial=partial.CashOut("p0",64);
            check(paidPartial==64&&partial.Seats[0].Stack==136&&partial.Seats[0].Leaving&&partial.Held+paidPartial==400,"Partial native payout retains durable exact remainder.");
            Reject(()=>AtomicDocument.Within(root,"../outside"),check,"Journal cannot write outside save root.");
            var intent=new PokerTransfer{Token="crash",Player="p1",Inventory=relative,Before=1200,After=1100,NativeAfter=PokerInventory.WithCash(File.ReadAllText(native),1100),TableAfter=after.Copy()};
            intent.TableAfter.Seats[1].Stack+=100;journal.Document.Transfer=intent;journal.Save();AtomicDocument.Write(native,intent.NativeAfter);
            journal=new(root);check(journal.Document.Transfer is null&&journal.Document.Completed.Contains("crash")&&journal.Document.Tables[t.Id].Held==300,"Crash after native write commits stack exactly once.");
            journal=new(root);check(journal.Document.Tables[t.Id].Held==300,"Repeated recovery cannot replay payout.");
            var saveAs=Path.Combine(root,"CopiedSave");AtomicDocument.Write(AtomicDocument.Within(saveAs,relative),File.ReadAllText(native));journal.CheckpointTo(saveAs);
            var paired=new PokerJournal(saveAs);check(paired.Document.Tables[t.Id].Held==300&&PokerInventory.Cash(File.ReadAllText(AtomicDocument.Within(saveAs,relative)))==1100,"Save to another folder carries paired stack and wallet checkpoint.");
            // A crash before the native write cancels intent by the exact original-file hash.
            var old=File.ReadAllText(native);var intended=journal.Document.Tables[t.Id].Copy();intended.Seats[1].Stack+=100;
            intent=new(){Token="before",Player="p1",Inventory=relative,Before=1100,After=1000,NativeAfter=PokerInventory.WithCash(old,1000),TableAfter=intended,NativeBeforeHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(old)))};
            journal.Document.Transfer=intent;journal.Save();journal=new(root);check(journal.Document.Transfer is null&&!journal.Document.Completed.Contains("before")&&PokerInventory.Cash(File.ReadAllText(native))==1100,"Crash before native write preserves original wallet and ledger.");
            intent.NativeBeforeHash="wrong";journal.Document.Transfer=intent;journal.Save();AtomicDocument.Write(native,PokerInventory.WithCash(old,777));
            Reject(()=>new PokerJournal(root),check,"Ambiguous native cash quarantines recovery.");
        }
        finally { Directory.Delete(root,true); }
    }
}
