namespace VICE.Lifestyle.Amenities;

// Casual eight-ball v1: no called pockets, break exemption, or competition claims.
internal sealed class PoolMatch
{
    public PoolSimulation Simulation { get; set; } = PoolSimulation.Rack();
    public int Turn { get; set; }
    public int[] Groups { get; set; } = new[] {0,0}; // 0 open, 1 solids, 2 stripes
    public bool BallInHand { get; set; }
    public int Winner { get; set; } = -1;
    public string Feedback { get; set; } = "Casual eight-ball · player 1 breaks · no called pockets";
    internal static int Group(int ball)=>ball is >=1 and <=7?1:ball is >=9 and <=15?2:0;
    internal bool Cleared(int player)=>Groups[player]!=0&&!Simulation.Balls.Any(b=>!b.Pocketed&&Group(b.Number)==Groups[player]);
    internal bool LegalFirst(int first,bool cleared)=>Groups[Turn]==0?first is >=1 and <=15&&first!=8:cleared?first==8:Group(first)==Groups[Turn];
    // Eligibility is captured BEFORE the strike so sinking the last group ball and 8 together loses.
    public bool EightEligible { get; set; }
    internal void Strike(double angle,double power)
    {
        if(Winner>=0)throw new InvalidOperationException("Match finished. Rack again to play.");
        if(BallInHand)throw new InvalidOperationException("Place the cue ball first.");
        EightEligible=Cleared(Turn);Simulation.Strike(angle,power);Feedback="Balls rolling…";
    }
    internal void Place(double x,double z)
    {if(!BallInHand)throw new InvalidOperationException("Cue placement is available only with ball in hand.");Simulation.Place(x,z);BallInHand=false;Feedback="Cue ball placed. Aim and strike.";}
    internal void Resolve(bool solo)
    {
        var shot=Simulation.Shot;
        var foul=shot.Pockets.Contains(0)||!LegalFirst(shot.FirstContact,EightEligible)||!shot.RailAfterContact&&shot.Pockets.Count==0;
        if(shot.Pockets.Contains(8))
        {
            Winner=EightEligible&&!foul?Turn:1-Turn;
            Feedback=$"Player {Winner+1} wins — {(Winner==Turn?"legal eight":"early eight or foul on eight")}.";
            if(solo)Feedback=Winner==Turn?"Practice rack completed.":"Practice lost: early eight or foul on eight.";
            return;
        }
        if(!foul&&Groups[Turn]==0)
        {
            var group=shot.Pockets.Select(Group).FirstOrDefault(g=>g!=0);
            if(group!=0){Groups[Turn]=group;Groups[1-Turn]=3-group;}
        }
        var keep=!foul&&shot.Pockets.Any(n=>Group(n)==Groups[Turn]&&Group(n)!=0);
        if(!solo&&!keep)Turn=1-Turn;
        BallInHand=foul;
        // Mark cue absent for ANY foul; incoming player places anywhere on clear cloth.
        if(foul){var cue=Simulation.Balls.Single(b=>b.Number==0);cue.Pocketed=true;cue.VX=cue.VZ=0;}
        Feedback=foul?"Foul — ball in hand. Click clear cloth to place cue ball.":keep?"Legal pot — continue.":"No own ball potted — next turn.";
    }
}
