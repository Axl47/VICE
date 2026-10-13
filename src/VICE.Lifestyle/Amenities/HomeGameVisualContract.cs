namespace VICE.Lifestyle.Amenities;

// Presentation only. Analytical coordinates and network state retain their original basis.
internal static class HomeGameVisualContract
{
    internal const double BoardFace=.055, BallCentreY=.808575;
    internal static (double X,double Y,double Z) Dart(double x,double y,bool modelArtwork)=>(modelArtwork?-x:x,y,BoardFace);
    internal static double CueDistance(double seconds,bool tipPivot)=> (tipPivot?.035:.760)+Math.Clamp(seconds,0,1.5)*.08;
    internal static string CanonicalMaterial(string name)=>name is "MountMetal" or "DartMetal"?"WireMetal":name;
    internal static (float Metallic,float Smoothness) Surface(string name)=>name switch
    {
        "Metal"=>(.6f,.52f),"WireMetal"=>(.7f,.32f),"Cloth" or "PocketBlack"=>(0,.25f),"BallAtlas"=>(0,.77f),
        "Wood" or "RailRubber" or "CueWood" or "CueTip" or "RackWood"=>(0,.52f),
        "SisalDark" or "SisalLight" or "RingRed" or "RingGreen" or "Numbers" or "DartPlastic" or "DartFlight"=>(0,.32f),
        _=>throw new InvalidDataException("Unknown home-games material: "+name)
    };
}
