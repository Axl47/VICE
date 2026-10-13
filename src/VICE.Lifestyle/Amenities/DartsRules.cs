namespace VICE.Lifestyle.Amenities;

internal readonly record struct DartHit(int Number, int Multiplier, string Label)
{
    internal int Points => Number * Multiplier;
    internal bool Double => Multiplier == 2;
}

// Coordinates are metres in the face plane: +X right, +Y up viewed by thrower.
internal static class DartsRules
{
    internal const double Radius = .170;
    private static readonly int[] Wedges = {20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5};
    internal static DartHit Score(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new InvalidDataException("Invalid dart coordinate.");
        var r = Math.Sqrt(x*x+y*y);
        if (r > Radius) return new(0,0,"Miss");
        if (r <= .00635) return new(25,2,"Inner bull · 50");
        if (r <= .0159) return new(25,1,"Outer bull · 25");
        var angle = (Math.Atan2(x,y)*180/Math.PI+360+9)%360;
        var value = Wedges[(int)(angle/18)];
        var multiplier = r >= .162 ? 2 : r is >= .099 and <= .107 ? 3 : 1;
        return new(value,multiplier,$"{(multiplier==3?"Triple":multiplier==2?"Double":"Single")} {value} · {value*multiplier}");
    }
    // Timing is a visible periodic displacement, never random dispersion.
    internal static (double X,double Y) Throw(double x,double y,double seconds)
    {
        if (!double.IsFinite(x)||!double.IsFinite(y)||Math.Abs(x)>.25||Math.Abs(y)>.25||!double.IsFinite(seconds)||seconds<.1||seconds>3)
            throw new InvalidDataException("Aim inside the board panel and release between 0.1 and 3 seconds.");
        var phase=seconds*Math.PI*2/.8;
        return (x+Math.Sin(phase)*.012,y+Math.Cos(phase)*.008);
    }
}

internal sealed class DartsMatch
{
    public int[] Remaining { get; set; } = new[] {301,301};
    public int Turn { get; set; }
    public int Darts { get; set; }
    public int TurnStart { get; set; } = 301;
    public int Winner { get; set; } = -1;
    public int Throws { get; set; }
    public double LastX { get; set; }
    public double LastY { get; set; }
    public string Feedback { get; set; } = "301 · straight in, double out · three darts per turn";

    internal void Hit(double x,double y,bool solo)
    {
        if(Winner>=0)throw new InvalidOperationException("Match finished. Restart to play again.");
        var hit=DartsRules.Score(x,y);LastX=x;LastY=y;Throws++;Darts++;
        var next=Remaining[Turn]-hit.Points;
        if(next<0||next==1||next==0&&!hit.Double)
        {
            Remaining[Turn]=TurnStart;Feedback=hit.Label+" — bust; turn score restored";EndTurn(solo);return;
        }
        Remaining[Turn]=next;Feedback=hit.Label;
        if(next==0){Winner=Turn;Feedback+=" — player "+(Turn+1)+" wins!";return;}
        if(Darts==3)EndTurn(solo);
    }
    private void EndTurn(bool solo){Darts=0;if(!solo)Turn=1-Turn;TurnStart=Remaining[Turn];}
}
