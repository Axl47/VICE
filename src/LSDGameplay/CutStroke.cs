using System.Numerics;

namespace PersonalMods.LSDGameplay;

// Keep a grabbed stroke through overshoot and release. Drawing remains bounded.
internal sealed class CutStroke
{
    private readonly Vector2 Start,End;
    private bool Dragging;
    internal bool Started {get;private set;}
    internal float Progress {get;private set;}
    private float Score,Duration;
    internal float Accuracy=>Duration>0 ? Score/Duration:1;
    internal bool Complete=>Started && Progress>=0.92f;
    internal CutStroke(float ax,float ay,float bx,float by){Start=new(ax,ay);End=new(bx,by);}
    internal void Sample(float x,float y,bool held,float delta)
    {
        var point=new Vector2(x,y);var direction=End-Start;
        var projected=Vector2.Dot(point-Start,direction)/direction.LengthSquared();
        var t=Math.Clamp(projected,0,1);
        var deviation=Vector2.Distance(point,Start+direction*projected);
        if(held && !Dragging){
            if(!Started && Vector2.Distance(point,Start)<0.2f){Started=true;Dragging=true;}
            else if(Started && deviation<0.2f && t>=Progress-0.2f)Dragging=true;
        }
        // Sample the release position too: a fast stroke can cross the endpoint
        // between the last held frame and the button-up frame.
        if(Dragging){Progress=Math.Max(Progress,t);Score+=Math.Clamp(1-deviation*8,0,1)*delta;Duration+=delta;}
        if(!held)Dragging=false;
    }
    internal void Advance(float delta){Started=true;Progress=Math.Min(1,Progress+delta*1.4f);Score+=delta;Duration+=delta;}
}
