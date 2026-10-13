using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Separate URP diagnostics and transparent inventory renders, never game screenshots.
public static class HomeGamesPresentation
{
    static GameObject Stage;
    static Camera Camera;
    static UniversalRenderPipelineAsset Pipeline;
    static readonly List<Material> TemporaryMaterials=new List<Material>();
    static readonly List<string> IconChecks=new List<string>();
    public static void Build(string repo)
    {
        var output=Path.Combine(repo,"artifacts/home-games-presentation");Directory.CreateDirectory(output);
        var icons=Path.Combine(repo,"src/VICE.Lifestyle/Assets/Icons");Directory.CreateDirectory(icons);
        var previousPipeline=GraphicsSettings.defaultRenderPipeline;var previousQuality=QualitySettings.renderPipeline;
        var previousActive=RenderTexture.active;
        Stage=new GameObject("Isolated home-games presentation diagnostics");
        try
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            ResourceReloader.TryReloadAllNullIn(renderer,"Packages/com.unity.render-pipelines.universal");
            Pipeline=UniversalRenderPipelineAsset.Create(renderer);Pipeline.supportsHDR=false;Pipeline.msaaSampleCount=4;
            GraphicsSettings.defaultRenderPipeline=Pipeline;QualitySettings.renderPipeline=Pipeline;
            var cameraRoot=new GameObject("Diagnostics camera");cameraRoot.transform.SetParent(Stage.transform);Camera=cameraRoot.AddComponent<Camera>();
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.035f,.045f,.05f,0);Camera.cullingMask=1<<30;
            Camera.nearClipPlane=.025f;Camera.farClipPlane=30;Camera.allowHDR=false;Camera.allowMSAA=true;
            var cameraData=cameraRoot.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=false;cameraData.antialiasing=AntialiasingMode.None;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
            var lightRoot=new GameObject("Soft broad key");lightRoot.transform.SetParent(Stage.transform);var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1.1f;light.color=new Color(1,.96f,.9f);light.shadows=LightShadows.Soft;light.cullingMask=1<<30;lightRoot.transform.rotation=Quaternion.Euler(38,145,0);
            var table=Make("pooltable");Bind(table,true);AuditNormals(table,output);
            Camera.orthographic=true;Camera.orthographicSize=1.65f;Camera.transform.position=new Vector3(3.4f,3.5f,-4);Camera.transform.LookAt(new Vector3(0,.43f,0));
            Save(Render(1024,512,4),Path.Combine(icons,"PoolTable.png"));table.SetActive(false);
            var board=Make("dartsboard");Bind(board,false);Camera.orthographicSize=.32f;Camera.transform.position=new Vector3(.16f,.055f,1.8f);Camera.transform.LookAt(new Vector3(0,0,.028f));
            Save(Render(1024,512,4),Path.Combine(icons,"DartsBoard.png"));board.SetActive(false);table.SetActive(true);
            Camera.orthographic=false;Camera.fieldOfView=64;Camera.transform.position=new Vector3(0,2.45f,-2.15f);Camera.transform.LookAt(new Vector3(0,.78f,.05f));
            Camera.backgroundColor=new Color(.12f,.14f,.16f,1);
            var sky=new Cubemap(16,TextureFormat.RGBA32,false);
            foreach(CubemapFace face in Enum.GetValues(typeof(CubemapFace)))
            {if(face==CubemapFace.Unknown)continue;sky.SetPixels(Enumerable.Repeat(face==CubemapFace.PositiveY?new Color(.85f,.9f,1):new Color(.5f,.55f,.6f),256).ToArray(),face);}
            sky.Apply();RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflection=sky;RenderSettings.reflectionIntensity=.8f;
            foreach(var candidate in new[]{false,true})foreach(var aa in new[]{1,4})
            {
                Bind(table,candidate);
                Save(Render(1280,1280,aa),Path.Combine(output,"Pool_"+(candidate?"CandidateMatte":"AcceptedScalars")+"_MSAA"+aa+".png"));
            }
            Bind(table,false,true,false);Save(Render(1280,1280,4),Path.Combine(output,"Pool_DirectSpecularOff_MSAA4.png"));
            Bind(table,false,false,true);Save(Render(1280,1280,4),Path.Combine(output,"Pool_EnvironmentReflectionOff_MSAA4.png"));
            Bind(table,true);light.shadows=LightShadows.None;Save(Render(1280,1280,4),Path.Combine(output,"Pool_CandidateMatte_Shadowless_MSAA4.png"));light.shadows=LightShadows.Soft;
            RenderSettings.customReflection=null;Object.DestroyImmediate(sky);
            // Ordinary world cameras with the new handles, using the exact imported prefab pivots.
            Camera.rect=new Rect(0,.18f,1,.82f);Camera.ResetAspect();
            Bind(table,true);var balls=Rack();var cue=Make("cue");Bind(cue,true);cue.transform.position=new Vector3(0,.808575f,-.685f);
            Save(RenderWorld(),Path.Combine(output,"Pool_World_Angled.png"));
            Camera.transform.position=new Vector3(0,2.45f,0);Camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,.78f,.05f)-Camera.transform.position,Vector3.forward);
            Camera.orthographic=true;Camera.orthographicSize=1.46f;
            Save(RenderWorld(),Path.Combine(output,"Pool_World_Overhead.png"));
            foreach(var b in balls)Object.DestroyImmediate(b);Object.DestroyImmediate(cue);table.SetActive(false);board.SetActive(true);Bind(board,false);
            Camera.orthographic=false;Camera.fieldOfView=22;Camera.transform.position=new Vector3(0,0,2.37f);Camera.transform.LookAt(new Vector3(0,0,.055f));
            foreach(var analyticalX in new[]{-.12f,0,.12f})
            {var dart=Make("dart");Bind(dart,false);dart.transform.position=new Vector3(-analyticalX,analyticalX==0?.12f:0,.055f);}
            Save(RenderWorld(),Path.Combine(output,"Darts_World_20_6_11.png"));
            File.WriteAllLines(Path.Combine(output,"Icon_Alpha_Validation.txt"),IconChecks);
            File.WriteAllText(Path.Combine(output,"README.txt"),"Unity 2022.3.62f2 / URP 14.0.12 diagnostic renders, not Schedule I screenshots. Pool comparison varies material policy and MSAA independently with unchanged geometry/normals, camera and lighting. Transparent 512px inventory icons use 1024px supersampling plus 4x MSAA. Direct light + ambient lighting are a controlled diagnostic; native exposure, reflection probes, AA, culling and UI still need an authorized isolated game test. Candidate table surfaces suppress direct specular on wood/rubber/cloth and suppress environment reflection on eight furniture materials; ball/darts tuning is unchanged. World view images show imported pivots and reserve the lower 18% for native score/menu controls; they do not test the game camera/control lifecycle.");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline=previousPipeline;QualitySettings.renderPipeline=previousQuality;RenderTexture.active=previousActive;
            foreach(var m in TemporaryMaterials)if(m)Object.DestroyImmediate(m);TemporaryMaterials.Clear();
            if(Stage)Object.DestroyImmediate(Stage);if(Pipeline)Object.DestroyImmediate(Pipeline);
        }
    }
    static GameObject Make(string name)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+name+".prefab");if(!prefab)throw new InvalidOperationException("Missing presentation prefab "+name);
        var root=Object.Instantiate(prefab,Stage.transform);foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;return root;
    }
    static void Bind(GameObject root,bool candidate,bool specularOff=false,bool reflectionOff=false)
    {
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
            {
                var name=source.name.Replace("Preview ","");var pool=new[]{"Wood","Cloth","RailRubber","PocketBlack","Metal","CueWood","CueTip","RackWood"}.Contains(name);
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Preview "+name};TemporaryMaterials.Add(mat);
                mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture);
                var metallic=name=="Metal"?.6f:name=="WireMetal"?.7f:0;
                var smooth=name=="BallAtlas"?.77f:name=="Cloth"||name=="PocketBlack"?.25f:pool?.52f:.32f;
                var spec=true;
                if(candidate&&pool)
                {
                    metallic=name=="Metal"?.25f:0;smooth=name=="RailRubber"?.14f:name=="Wood"||name=="RackWood"?.22f:name=="Metal"?.28f:.25f;
                    spec=name=="Metal"||name=="CueWood"||name=="CueTip";
                    mat.SetFloat("_EnvironmentReflections",0);mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
                if(pool&&reflectionOff){mat.SetFloat("_EnvironmentReflections",0);mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");}
                if(pool&&specularOff)spec=false;
                mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smooth);
                mat.SetFloat("_SpecularHighlights",spec?1:0);if(!spec)mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");return mat;
            }).ToArray();
        }
    }
    static List<GameObject> Rack()
    {
        var balls=new List<GameObject>();int[] order={1,9,2,3,8,10,11,4,12,5,6,13,7,14,15};var i=0;
        var cue=Make("ball00");Bind(cue,false);cue.transform.position=new Vector3(0,.808575f,-.65f);balls.Add(cue);
        for(var row=0;row<5;row++)for(var col=0;col<=row;col++)
        {var ball=Make("ball"+order[i++].ToString("00"));Bind(ball,false);ball.transform.position=new Vector3((col-row*.5f)*(.05715f+.0003f),.808575f,.45f+row*(.05715f+.0003f)*Mathf.Sqrt(3)/2);balls.Add(ball);}
        return balls;
    }
    static Texture2D Render(int size,int finalSize,int aa)
    {
        Pipeline.msaaSampleCount=aa;
        var target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32){antiAliasing=aa};target.Create();
        var output=new RenderTexture(finalSize,finalSize,0,RenderTextureFormat.ARGB32);output.Create();
        try
        {
            Camera.targetTexture=target;Camera.Render();Graphics.Blit(target,output);RenderTexture.active=output;
            var texture=new Texture2D(finalSize,finalSize,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,finalSize,finalSize),0,0);texture.Apply();return texture;
        }
        finally{Camera.targetTexture=null;RenderTexture.active=null;target.Release();output.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(output);}
    }
    static Texture2D RenderWorld()
    {
        Pipeline.msaaSampleCount=4;
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();
        try
        {
            Camera.targetTexture=target;Camera.ResetAspect();Camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();return texture;
        }
        finally{Camera.targetTexture=null;RenderTexture.active=null;target.Release();Object.DestroyImmediate(target);}
    }
    static void Save(Texture2D texture,string path)
    {
        if(texture.width==512)
        {
            var pixels=texture.GetPixels();var transparent=pixels.Count(p=>p.a<.01f);var opaque=pixels.Count(p=>p.a>.99f);
            if(transparent<10000||opaque<10000)throw new InvalidOperationException("Inventory thumbnail lacks real transparent/opaque coverage: "+path);
            IconChecks.Add(Path.GetFileName(path)+": 512x512 RGBA; transparent="+transparent+" opaque="+opaque);
        }
        File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
    }
    static void AuditNormals(GameObject root,string output)
    {
        var rows=new List<string>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;var normals=mesh.normals;var vertices=mesh.vertices;var triangles=mesh.triangles;var minDot=1f;
            for(var i=0;i<triangles.Length;i+=3)
            {
                var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];var geometric=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized;
                minDot=Mathf.Min(minDot,Vector3.Dot(geometric,(normals[a]+normals[b]+normals[c]).normalized));
            }
            rows.Add(filter.name+": vertices="+vertices.Length+", normals="+normals.Length+", unit range="+normals.Min(n=>n.magnitude).ToString("F6")+".."+normals.Max(n=>n.magnitude).ToString("F6")+", min averaged-normal/geometric dot="+minDot.ToString("F6"));
        }
        File.WriteAllLines(Path.Combine(output,"Imported_Normal_Audit.txt"),rows);
    }
}
