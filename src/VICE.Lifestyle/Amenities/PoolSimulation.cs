namespace VICE.Lifestyle.Amenities;

internal sealed class PoolBall
{
    public int Number { get; set; }
    public double X { get; set; }
    public double Z { get; set; }
    public double VX { get; set; }
    public double VZ { get; set; }
    public bool Pocketed { get; set; }
}

internal sealed class PoolShot
{
    public int FirstContact { get; set; } = -1;
    public bool RailAfterContact { get; set; }
    public List<int> Pockets { get; set; } = new();
}

// Host-only fixed-step planar simulation. No Unity PhysX or guest outcome decisions.
internal sealed class PoolSimulation
{
    internal const double Radius=.028575, HalfWidth=.56, HalfLength=1.12, PocketRadius=.065, StepSeconds=1.0/240;
    internal static readonly (double X,double Z)[] Pockets={(-.56,-1.12),(.56,-1.12),(-.56,0),(.56,0),(-.56,1.12),(.56,1.12)};
    public List<PoolBall> Balls { get; set; } = new();
    public bool Moving { get; set; }
    public double ShotTime { get; set; }
    public PoolShot Shot { get; set; } = new();

    internal static PoolSimulation Rack()
    {
        var s=new PoolSimulation();s.Balls.Add(new(){Number=0,X=0,Z=-.65});
        // 8 in centre; opposite groups on back corners. Fixed reproducible rack.
        int[] order={1,9,2,3,8,10,11,4,12,5,6,13,7,14,15};var i=0;
        for(var row=0;row<5;row++)for(var col=0;col<=row;col++)
            s.Balls.Add(new(){Number=order[i++],X=(col-row*.5)*(Radius*2+.0003),Z=.45+row*(Radius*2+.0003)*Math.Sqrt(3)/2});
        return s;
    }
    internal bool CanPlace(double x,double z)=>double.IsFinite(x)&&double.IsFinite(z)&&Math.Abs(x)<=HalfWidth-Radius&&Math.Abs(z)<=HalfLength-Radius
        && !Pockets.Any(p=>Distance(x-p.X,z-p.Z)<PocketRadius+Radius)
        && !Balls.Any(b=>b.Number!=0&&!b.Pocketed&&Distance(x-b.X,z-b.Z)<Radius*2+.001);
    internal void Place(double x,double z)
    {
        if(Moving||!CanPlace(x,z))throw new InvalidOperationException("Place the cue ball on clear cloth away from balls and pockets.");
        var cue=Balls.Single(b=>b.Number==0);cue.X=x;cue.Z=z;cue.VX=cue.VZ=0;cue.Pocketed=false;
    }
    internal void Strike(double angle,double power)
    {
        if(Moving||!double.IsFinite(angle)||!double.IsFinite(power)||power is < .05 or > 1)throw new InvalidDataException("Invalid pool strike.");
        var cue=Balls.Single(b=>b.Number==0);if(cue.Pocketed)throw new InvalidOperationException("Place the cue ball first.");
        Shot=new();ShotTime=0;Moving=true;cue.VX=Math.Sin(angle)*power*3.8;cue.VZ=Math.Cos(angle)*power*3.8;
    }
    internal bool Step()
    {
        if(!Moving)return false;ShotTime+=StepSeconds;
        foreach(var b in Balls.Where(b=>!b.Pocketed))
        {
            b.X+=b.VX*StepSeconds;b.Z+=b.VZ*StepSeconds;
            if(Pockets.Any(p=>Distance(b.X-p.X,b.Z-p.Z)<=PocketRadius))
            {b.Pocketed=true;b.VX=b.VZ=0;Shot.Pockets.Add(b.Number);continue;}
            Cushion(b,true);if(!b.Pocketed)Cushion(b,false);
        }
        // Resolve all equal-mass pairs in stable number order; correct overlap before impulse.
        var live=Balls.Where(b=>!b.Pocketed).OrderBy(b=>b.Number).ToArray();
        for(var i=0;i<live.Length;i++)for(var j=i+1;j<live.Length;j++)
        {
            var a=live[i];var b=live[j];var dx=b.X-a.X;var dz=b.Z-a.Z;var distance=Distance(dx,dz);
            if(distance>=Radius*2)continue;
            var nx=distance>1e-9?dx/distance:1;var nz=distance>1e-9?dz/distance:0;
            var overlap=(Radius*2-distance)*.5+.000001;a.X-=nx*overlap;a.Z-=nz*overlap;b.X+=nx*overlap;b.Z+=nz*overlap;
            var closing=(a.VX-b.VX)*nx+(a.VZ-b.VZ)*nz;
            if(closing<=0)continue;
            if(Shot.FirstContact<0&&a.Number==0)Shot.FirstContact=b.Number;
            var impulse=closing*.97;a.VX-=impulse*nx;a.VZ-=impulse*nz;b.VX+=impulse*nx;b.VZ+=impulse*nz;
        }
        foreach(var b in live)
        {
            var speed=Distance(b.VX,b.VZ);var reduced=Math.Max(0,speed-.24*StepSeconds);
            if(reduced<.012||ShotTime>=30){b.VX=b.VZ=0;}
            else{b.VX*=reduced/speed;b.VZ*=reduced/speed;}
        }
        Moving=live.Any(b=>b.VX!=0||b.VZ!=0);return !Moving;
    }
    private void Cushion(PoolBall b,bool xAxis)
    {
        var edge=(xAxis?HalfWidth:HalfLength)-Radius;var p=xAxis?b.X:b.Z;if(Math.Abs(p)<=edge)return;
        var along=xAxis?b.Z:b.X;
        var opening=xAxis?(Math.Abs(along)<.09||Math.Abs(along)>HalfLength-.09):Math.Abs(along)>HalfWidth-.09;
        // Open mouths still have an outer safety bound: escaped centres are captured.
        if(opening)
        {
            if(Math.Abs(p)>(xAxis?HalfWidth:HalfLength)+Radius){b.Pocketed=true;b.VX=b.VZ=0;Shot.Pockets.Add(b.Number);}return;
        }
        if(xAxis){b.X=Math.Sign(p)*edge;b.VX=-Math.Sign(p)*Math.Abs(b.VX)*.8;}
        else{b.Z=Math.Sign(p)*edge;b.VZ=-Math.Sign(p)*Math.Abs(b.VZ)*.8;}
        if(Shot.FirstContact>=0)Shot.RailAfterContact=true;
    }
    private static double Distance(double x,double z)=>Math.Sqrt(x*x+z*z);
}
