using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

// Pointer strokes and guides use the physical bench mesh's UV plane.
internal sealed class SheetView : IDisposable
{
    private readonly BuildableItem Bench;
    private readonly GameObject Sheet,Lines;
    private readonly MeshFilter Filter;
    private readonly Material Paper;
    private readonly Dictionary<Color,Material> GuideMaterials=new();
    private readonly List<LineRenderer> Guides=new();
    private int GuideCount;
    private Texture2D? Printed;
    private Color32[]? PrintSource;
    private int PrintedRows=-1;
    private bool PrintAscending=true,PrintDirectionSet;
    private readonly Vector3 SourcePosition,SourceScale;
    private readonly Quaternion SourceRotation;
    internal Vector3 Origin,X,Y,Normal;
    internal float ScreenXSign=1,ScreenYSign=1;

    internal SheetView(BuildableItem bench,int slot)
    {
        Bench=bench;
        var source=bench.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.StartsWith("ArtworkSurface",StringComparison.Ordinal));
        Sheet=new GameObject("Studio working sheet");
        Sheet.transform.position=source.transform.position;Sheet.transform.rotation=source.transform.rotation;Sheet.transform.localScale=source.transform.lossyScale;
        Sheet.transform.SetParent(bench.transform,true);
        SourcePosition=Sheet.transform.localPosition;SourceRotation=Sheet.transform.localRotation;SourceScale=Sheet.transform.localScale;
        Filter=Sheet.AddComponent<MeshFilter>();Filter.sharedMesh=source.sharedMesh;
        Paper=StudioAssets.SurfaceMaterial(Color.white);Paper.mainTexture=StudioAssets.SheetTexture(slot);
        Sheet.AddComponent<MeshRenderer>().sharedMaterial=Paper;
        Lines=new GameObject("Studio physical guides");Lines.transform.SetParent(bench.transform,false);
        Frame(false);
    }
    internal void SetArtwork(int slot)=>Paper.mainTexture=StudioAssets.SheetTexture(slot);
    internal void SetDrawing(Texture2D drawing)=>Paper.mainTexture=drawing;
    internal void BeginPrinting()
    {
        if(PrintDirectionSet)return;
        UpdateControlDirections();PrintAscending=ScreenYSign>=0;PrintDirectionSet=true;
    }
    internal float PrintProgress(float y)=>PrintAscending ? y:1-y;
    internal float PrintCoordinate(float progress)=>PrintAscending ? progress:1-progress;
    internal Texture2D CheckPrintTexture=>Printed!;
    internal void Print(int slot,float progress)
    {
        if(Printed is null){Printed=new Texture2D(320,200,TextureFormat.RGBA32,false);Printed.filterMode=FilterMode.Point;}
        PrintSource??=StudioAssets.SheetTexture(slot).GetPixels32().ToArray();
        var rows=Math.Clamp((int)(progress*200),0,200);if(rows==PrintedRows)return;PrintedRows=rows;
        var pixels=(Color32[])PrintSource.Clone();Array.Fill(pixels,StudioAssets.Palette[0],PrintAscending ? rows*320:0,(200-rows)*320);
        Printed.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(pixels));Printed.Apply();Paper.mainTexture=Printed;
    }
    internal void Frame(bool cutting)
    {
        Sheet.transform.localPosition=SourcePosition;Sheet.transform.localRotation=SourceRotation;Sheet.transform.localScale=SourceScale;UpdatePlane();
        if(cutting) {
            var board=Bench.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="CutterBase");
            Sheet.transform.localScale=SourceScale*0.6f;UpdatePlane();
            Sheet.transform.position+=board.bounds.center+Normal*0.025f-(Origin+(X+Y)*0.5f);
        } else Sheet.transform.position+=Normal*0.004f;
        UpdatePlane();var target=Origin+(X+Y)*0.5f;
        var position=target+Normal*(cutting ? 0.43f:0.62f)-Y.normalized*0.12f;
        var camera=PlayerCamera.Instance;
        camera.OverrideTransform(position,Quaternion.LookRotation(target-position,Y.normalized),0.25f,false);
        // Screen directions, rather than UV conventions, determine arrow input.
        var view=camera.Camera;
        ScreenXSign=MathF.Sign((view.WorldToScreenPoint(Origin+X)-view.WorldToScreenPoint(Origin)).x);
        ScreenYSign=MathF.Sign((view.WorldToScreenPoint(Origin+Y)-view.WorldToScreenPoint(Origin)).y);
        if(ScreenXSign==0)ScreenXSign=1;if(ScreenYSign==0)ScreenYSign=1;ClearGuides();
    }
    private void UpdatePlane()
    {
        var vertices=Filter.sharedMesh.vertices.ToArray();var uv=Filter.sharedMesh.uv.ToArray();
        Vector3 Corner(float x,float y) {
            var index=Enumerable.Range(0,uv.Length).OrderBy(i=>(uv[i]-new Vector2(x,y)).sqrMagnitude).First();
            return Filter.transform.TransformPoint(vertices[index]);
        }
        Origin=Corner(0,0);X=Corner(1,0)-Origin;Y=Corner(0,1)-Origin;
        Normal=Filter.transform.TransformDirection(Filter.sharedMesh.normals[0]).normalized;
    }
    internal Vector3 Point(Vector2 uv)=>Origin+X*uv.x+Y*uv.y+Normal*0.002f;
    internal bool Pointer(out Vector2 uv,float margin=0)
    {
        uv=default;var mouse=Mouse.current;if(mouse is null)return false;
        return Project(mouse.position.ReadValue(),out uv,margin);
    }
    internal bool Project(Vector2 screen,out Vector2 uv,float margin=0)
    {
        uv=default;var ray=PlayerCamera.Instance.Camera.ScreenPointToRay(screen);
        if(!new Plane(Normal,Origin).Raycast(ray,out var distance))return false;
        var local=ray.GetPoint(distance)-Origin;
        uv=new(Vector3.Dot(local,X)/X.sqrMagnitude,Vector3.Dot(local,Y)/Y.sqrMagnitude);
        return uv.x>=-margin && uv.x<=1+margin && uv.y>=-margin && uv.y<=1+margin;
    }
    internal void UpdateControlDirections()
    {
        var camera=PlayerCamera.Instance.Camera;
        ScreenXSign=MathF.Sign((camera.WorldToScreenPoint(Origin+X)-camera.WorldToScreenPoint(Origin)).x);
        ScreenYSign=MathF.Sign((camera.WorldToScreenPoint(Origin+Y)-camera.WorldToScreenPoint(Origin)).y);
    }
    internal void VerifyProjection()
    {
        foreach(var expected in new[]{new Vector2(0.15f,0.2f),new Vector2(0.8f,0.7f)}){
            var screen=PlayerCamera.Instance.Camera.WorldToScreenPoint(Point(expected));
            if(!Project(new(screen.x,screen.y),out var actual) || Vector2.Distance(expected,actual)>0.025f)
                throw new InvalidOperationException("Physical sheet pointer projection does not match its UV artwork.");
        }
    }
    internal void ClearGuides(){foreach(var guide in Guides)if(guide)guide.gameObject.SetActive(false);GuideCount=0;}
    private void Line(Vector2 a,Vector2 b,Color color,float width=0.0016f)
    {
        LineRenderer line;
        if(GuideCount<Guides.Count){line=Guides[GuideCount];line.gameObject.SetActive(true);}
        else{var root=new GameObject("Sheet guide");root.transform.SetParent(Lines.transform,false);line=root.AddComponent<LineRenderer>();Guides.Add(line);}
        GuideCount++;
        if(!GuideMaterials.TryGetValue(color,out var material)){material=StudioAssets.SurfaceMaterial(color);GuideMaterials[color]=material;}
        line.useWorldSpace=true;line.positionCount=2;line.sharedMaterial=material;
        line.startWidth=line.endWidth=width;line.SetPosition(0,Point(a));line.SetPosition(1,Point(b));
    }
    internal void Registration(Vector2 control)
    {
        ClearGuides();var centre=new Vector2(0.5f,0.5f);
        Line(centre-new Vector2(0.08f,0),centre+new Vector2(0.08f,0),new(0.7f,1,0.3f));
        Line(centre-new Vector2(0,0.08f),centre+new Vector2(0,0.08f),new(0.7f,1,0.3f));
        Line(control-new Vector2(0.045f,0),control+new Vector2(0.045f,0),new(1,0.25f,0.35f));
        Line(control-new Vector2(0,0.045f),control+new Vector2(0,0.045f),new(1,0.25f,0.35f));
    }
    internal void Roller(float progress){ClearGuides();var y=PrintCoordinate(progress);Line(new(0,y),new(1,y),new(0.3f,0.95f,0.8f),0.008f);}
    internal static (Vector2 Start,Vector2 End) CutLine(int index)=>index<7
        ? (new((index+1)/8f,0),new((index+1)/8f,1)) : (new(0,(index-6)/5f),new(1,(index-6)/5f));
    internal void CutGuide(int index,float progress)
    {
        ClearGuides();for(var i=0;i<11;i++) {
            var(start,end)=CutLine(i);
            Line(start,end,i<index ? new(0.25f,0.8f,0.65f) : i==index ? new(1,0.1f,0.65f) : new(0.4f,0.4f,0.38f),i==index ? 0.0035f:0.0008f);
        }
        if(index<11){var(a,b)=CutLine(index);var cursor=Vector2.Lerp(a,b,progress);Line(cursor-new Vector2(0.018f,0),cursor+new Vector2(0.018f,0),Color.white,0.004f);}
    }
    public void Dispose(){Object.Destroy(Lines);Object.Destroy(Sheet);Object.Destroy(Paper);if(Printed)Object.Destroy(Printed);foreach(var material in GuideMaterials.Values)Object.Destroy(material);GuideMaterials.Clear();}
}
