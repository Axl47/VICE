using S1API.Items;
using S1API.Items.Buildable;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.UI.Shop;
using UnityEngine;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

internal static class AmenityContent
{
    internal const string DartsId="vice.lifestyle:darts",PoolId="vice.lifestyle:pool";
    private static readonly Dictionary<string,S1API.Items.Buildable.BuildableItemDefinition> Definitions=new();
    private static readonly Dictionary<string,Material> Materials=new();
    private static Texture2D? BoardArt;
    internal static string? Kind(string? id)=>id==DartsId?"darts":id==PoolId?"pool":null;
    internal static Material Material(string key,Color color)
    {
        if(Materials.TryGetValue(key,out var existing)&&existing)return existing;
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color")??throw new InvalidOperationException("Prototype shader unavailable.");
        var material=new Material(shader);material.SetColor("_BaseColor",color);material.color=color;Materials[key]=material;return material;
    }
    internal static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
        part.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(part.GetComponent<Collider>());return part;
    }
    internal static void Register()
    {
        foreach(var kind in new[]{"darts","pool"})
        {
            var id=kind=="darts"?DartsId:PoolId;
            if(Definitions.TryGetValue(id,out var definition)){ItemManager.EnsureItemRegistered(definition);continue;}
            var root=HomeGameAssets.Template(kind=="darts"?"dartsboard":"pooltable");var artwork=(bool)root;
            if(!root)
            {
                root=new GameObject(kind=="darts"?"DartsBoard_Root":"PoolTable_Root");root.SetActive(false);Object.DontDestroyOnLoad(root);
                if(kind=="darts")Board(root.transform);else Table(root.transform);
            }
            var builder=FurnitureCreator.CreateBuilder().WithBasicInfo(id,kind=="darts"?"Prototype Darts Board":"Prototype Pool Table",
                "VICE playable prototype — "+(artwork?"model artwork, temporary play panel":"placeholder primitives")+". Interact in an owned property. No wagers.")
                .WithModel(root!).WithPricing(kind=="darts"?150:800).WithStackLimit(1).WithIcon(HomeGameAssets.Icon(kind)??Icon(kind)).WithBuildSound(S1API.Items.Buildable.BuildSoundType.Wood);
            if(kind=="darts")builder.WithPlacement(FurniturePlacementMode.Surface).WithSurfacePlacement(FurnitureSurfaceType.Wall,false);
            else builder.WithFootprint(3,6);
            Definitions[id]=builder.Build();
        }
    }
    internal static void Shops()
    {
        foreach(var shop in ShopInterface.AllShops.ToArray().Where(s=>s&&s.ShopCode=="boutique"))
            foreach(var pair in Definitions)if(shop.GetListing(pair.Key) is null)S1API.Shops.ShopManager.AddToShops(pair.Value,shop.ShopName);
    }
    private static Sprite Icon(string kind)
    {
        var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(var y=0;y<64;y++)for(var x=0;x<64;x++)
        {var inside=kind=="darts"?(x-32)*(x-32)+(y-32)*(y-32)<28*28:x is >10 and <54&&y is >4 and <60;tex.SetPixel(x,y,inside?new Color(.16f,.55f,.35f):Color.clear);}
        tex.Apply();return Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f));
    }
    internal static Texture2D BoardTexture()
    {
        if(BoardArt)return BoardArt!;
        var tex=new Texture2D(512,512,TextureFormat.RGBA32,false);
        var pixels=new Color[512*512];
        for(var y=0;y<512;y++)for(var x=0;x<512;x++)
        {
            var px=(x-255.5)/512*.45;var py=(y-255.5)/512*.45;var radius=Math.Sqrt(px*px+py*py);
            var sector=(int)(((Math.Atan2(px,py)*180/Math.PI+369)%360)/18);
            var ring=radius<=.0159||radius is >=.099 and <=.107||radius>=.162;
            var color=radius>DartsRules.Radius?new Color(.055f,.065f,.075f):ring
                ?(sector%2==0?new Color(.7f,.12f,.1f):new Color(.06f,.5f,.28f))
                :sector%2==0?new Color(.07f,.07f,.065f):new Color(.8f,.74f,.58f);
            if(radius<=.0159)color=radius<=.00635?new Color(.7f,.12f,.1f):new Color(.06f,.5f,.28f);
            pixels[y*512+x]=color;
        }
        tex.SetPixels(pixels);tex.Apply();BoardArt=tex;return tex;
    }
    private static void Board(Transform root)
    {
        var wood=Material("mount",new(.22f,.13f,.08f));
        var back=Primitive(root,"Darts_Backplate",PrimitiveType.Cylinder,new(0,0,.0125f),new(.52f,.0125f,.52f),wood);back.transform.localRotation=Quaternion.Euler(90,0,0);
        var board=Primitive(root,"Darts_Board",PrimitiveType.Cube,new(0,0,.04f),new(.45f,.45f,.03f),Material("boardedge",new(.06f,.065f,.07f)));
        var faceMat=Material("boardface",Color.white);faceMat.SetTexture("_BaseMap",BoardTexture());
        var face=Primitive(root,"Darts_Face",PrimitiveType.Quad,new(0,0,.055f),new(.45f,.45f,1),faceMat);face.transform.localRotation=Quaternion.Euler(0,180,0);
        // Quad viewed from +Z has mirrored local U after the 180-degree turn. Flip U back.
        faceMat.mainTextureScale=new(-1,1);faceMat.mainTextureOffset=new(1,0);
    }
    private static void Table(Transform root)
    {
        var wood=Material("wood",new(.24f,.12f,.055f));var green=Material("cloth",new(.055f,.4f,.25f));var rubber=Material("rail",new(.10f,.21f,.13f));var black=Material("pocket",new(.012f,.012f,.012f));
        Primitive(root,"Pool_Body",PrimitiveType.Cube,new(0,.63f,0),new(1.4f,.24f,2.52f),wood);
        Primitive(root,"Pool_Cloth",PrimitiveType.Cube,new(0,.765f,0),new(1.12f,.03f,2.24f),green);
        foreach(var x in new[]{-.61f,.61f})foreach(var z in new[]{-.61f,.61f})
            Primitive(root,"Pool_RailSide",PrimitiveType.Cube,new(x,.81f,z),new(.1f,.10f,.98f),rubber);
        foreach(var z in new[]{-1.17f,1.17f})Primitive(root,"Pool_RailEnd",PrimitiveType.Cube,new(0,.81f,z),new(.94f,.1f,.1f),rubber);
        foreach(var p in PoolSimulation.Pockets)Primitive(root,"Pool_Pocket",PrimitiveType.Sphere,new((float)p.X,.783f,(float)p.Z),new(.13f,.008f,.13f),black);
        foreach(var x in new[]{-.52f,.52f})foreach(var z in new[]{-.96f,.96f})Primitive(root,"Pool_Leg",PrimitiveType.Cube,new(x,.26f,z),new(.16f,.52f,.16f),wood);
    }
}

internal sealed class AmenityView : IDisposable
{
    internal readonly BuildableItem Item;
    internal readonly Transform Visual;
    private readonly GameObject Runtime;
    private readonly Dictionary<int,GameObject> Balls=new();
    private readonly GameObject[] Darts=new GameObject[3];
    private readonly GameObject Cue;
    private readonly GameObject? Rack;
    private readonly bool CueTipPivot;
    internal readonly bool BoardArtwork;
    private int SeenThrows=-1;
    private double FlightStarted,StrokeStarted=-1;
    private int Flying=-1,PreviousCharging=-1;
    private Vector3 FlightEnd,PreviousCueBall,StrokeOrigin;
    private readonly GameObject? HeldDart;
    internal AmenityView(BuildableItem item,string kind)
    {
        Item=item;Visual=item.transform.Find("FurnitureVisual");Runtime=new("AmenityRuntime");Runtime.transform.SetParent(Visual,false);
        var white=AmenityContent.Material("white",Color.white);
        BoardArtwork=kind=="darts"&&Visual.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="BoardFace");
        var cue=kind=="pool"?HomeGameAssets.Clone("cue",Runtime.transform):null;CueTipPivot=cue;
        Cue=cue??AmenityContent.Primitive(Runtime.transform,"Pool_Cue",PrimitiveType.Cube,Vector3.zero,new(.018f,.018f,1.45f),AmenityContent.Material("cue",new(.7f,.5f,.26f)));Cue.SetActive(false);
        if(kind=="pool")
        {
            Rack=HomeGameAssets.Clone("rack",Runtime.transform);
            if(Rack)Rack!.transform.localPosition=new(0,.78f,-.30f); // Decorative, removed during play; never arranges the balls.
        }
        if(kind=="pool")for(var n=0;n<16;n++)
        {
            var color=n==0?Color.white:n==8?new Color(.03f,.03f,.03f):Color.HSVToRGB(((n-1)%8)/8f,.8f,1);
            var ball=HomeGameAssets.Clone($"ball{n:00}",Runtime.transform);var fallback=!ball;
            ball??=AmenityContent.Primitive(Runtime.transform,$"Pool_Ball_{n:00}",PrimitiveType.Sphere,Vector3.zero,Vector3.one*(float)(PoolSimulation.Radius*2),AmenityContent.Material("ball"+n,color));Balls[n]=ball;
            if(fallback&&n>=9)AmenityContent.Primitive(ball.transform,"Placeholder stripe cap",PrimitiveType.Sphere,new(0,.38f,0),new(.70f,.28f,.70f),white);
        }
        else for(var i=0;i<3;i++)
        {
            var dart=HomeGameAssets.Clone("dart",Runtime.transform);
            // A centre-pivot wrapper preserves the primitive fallback's handle contract.
            Darts[i]=new("DartHandle");Darts[i].transform.SetParent(Runtime.transform,false);
            if(dart){dart!.transform.SetParent(Darts[i].transform,false);dart.transform.localPosition=new(0,0,-.08f);}
            else AmenityContent.Primitive(Darts[i].transform,"Dart_Prototype",PrimitiveType.Cube,Vector3.zero,new(.008f,.008f,.16f),white);
            Darts[i].SetActive(false);
        }
        if(kind=="darts")
        {
            HeldDart=HomeGameAssets.Clone("dart",Runtime.transform)??AmenityContent.Primitive(Runtime.transform,"Held dart",PrimitiveType.Cube,Vector3.zero,new(.008f,.008f,.16f),white);
            HeldDart.SetActive(false);
        }
    }
    internal void Tick(AmenitySession state,float dt)
    {
        if(!Item||!Visual)return;
        if(state.Kind=="pool")
        {
            foreach(var ball in state.Pool.Simulation.Balls)
            {
                if(!Balls.TryGetValue(ball.Number,out var part))continue;part.SetActive(!ball.Pocketed);
                var target=new Vector3((float)ball.X,(float)HomeGameVisualContract.BallCentreY,(float)ball.Z);
                var before=part.transform.localPosition;part.transform.localPosition=state.Moving?Vector3.Lerp(before,target,Math.Min(1,dt*25)):target;
                var roll=part.transform.localPosition-before;
                if(state.Moving&&roll.sqrMagnitude>1e-8f)part.transform.localRotation=Quaternion.AngleAxis(roll.magnitude/(float)PoolSimulation.Radius*Mathf.Rad2Deg,Vector3.Cross(Vector3.up,roll).normalized)*part.transform.localRotation;
                if(state.Mode=="idle")part.transform.localRotation=Quaternion.identity;
            }
            if(PreviousCharging>=0&&state.Charging<0&&state.Moving){StrokeStarted=AmenityNetwork.Now;StrokeOrigin=PreviousCueBall;}
            var stroke=StrokeStarted>=0&&AmenityNetwork.Now-StrokeStarted<.16&&state.Moving;
            Cue.SetActive(state.Charging>=0||stroke);
            if(Rack)Rack!.SetActive(state.Mode=="idle");
            if(state.Charging>=0||stroke)
            {
                var cueBall=state.Pool.Simulation.Balls.Single(b=>b.Number==0);var dir=new Vector3((float)Math.Sin(state.AimX),0,(float)Math.Cos(state.AimX));
                Cue.transform.localRotation=Quaternion.Euler(0,(float)(state.AimX*180/Math.PI),0);
                var clearance=stroke?WorldPlayGeometry.StrokeClearance(AmenityNetwork.Now-StrokeStarted)+(CueTipPivot?0:.725):HomeGameVisualContract.CueDistance(AmenityNetwork.Now-state.ChargeStarted,CueTipPivot);
                Cue.transform.localPosition=(stroke?StrokeOrigin:new Vector3((float)cueBall.X,(float)HomeGameVisualContract.BallCentreY,(float)cueBall.Z))-dir*(float)clearance;
            }
            var currentCue=state.Pool.Simulation.Balls.Single(b=>b.Number==0);PreviousCueBall=new((float)currentCue.X,.808575f,(float)currentCue.Z);PreviousCharging=state.Charging;
        }
        else if(SeenThrows!=state.Darts.Throws)
        {
            if(state.Darts.Throws==0||state.Darts.Throws<SeenThrows){foreach(var d in Darts)d.SetActive(false);Flying=-1;}
            else
            {
                var index=(state.Darts.Throws-1)%3;if(index==0)foreach(var d in Darts)d.SetActive(false);
                var hit=HomeGameVisualContract.Dart(state.Darts.LastX,state.Darts.LastY,BoardArtwork);
                // Handles remain centred; the authored tip child offsets by -0.08. Apply X reflection once.
                Darts[index].SetActive(true);Darts[index].transform.localPosition=new((float)hit.X,(float)hit.Y,(float)hit.Z+.08f);
                if(SeenThrows>=0){Flying=index;FlightStarted=AmenityNetwork.Now;FlightEnd=Darts[index].transform.localPosition;}
            }
            SeenThrows=state.Darts.Throws;
        }
        if(state.Kind=="darts")
        {
            if(HeldDart)
            {HeldDart!.SetActive(state.Charging>=0);HeldDart.transform.localPosition=new(BoardArtwork?-.055f:.055f,-.075f,1.75f+(float)Math.Clamp(AmenityNetwork.Now-state.ChargeStarted,0,3)*.025f);}
            if(Flying>=0)
            {
                var t=(float)WorldPlayGeometry.DartFlight(AmenityNetwork.Now-FlightStarted);Darts[Flying].transform.localPosition=Vector3.Lerp(new(BoardArtwork?-.055f:.055f,-.075f,1.85f),FlightEnd,t);
                Darts[Flying].transform.localRotation=Quaternion.Euler(0,0,t<1?t*360:0);if(t>=1)Flying=-1;
            }
        }
    }
    public void Dispose(){if(Runtime)Object.Destroy(Runtime);}
}
