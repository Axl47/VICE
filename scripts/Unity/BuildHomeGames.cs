using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated editor build: authored FBX bytes remain untouched. The game rebinds
// canonical materials to its native URP/Lit shader after loading the bundle.
public static class BuildHomeGames
{
    static int checks;
    static readonly Dictionary<string,Material> Materials = new Dictionary<string,Material>();
    static readonly List<string> Prefabs = new List<string>();
    static readonly string[] Models = {"PoolTable","PoolAccessories","DartsBoard","Dart"};
    static void Check(bool value,string reason) { checks++; if(!value)throw new InvalidOperationException(reason); }
    public static void Build()
    {
        var args=Environment.GetCommandLineArgs();var repo=Path.GetFullPath(args[Array.IndexOf(args,"-homeGamesRepository")+1]);
        Check(Application.unityVersion=="2022.3.62f2","Use the game's Unity 2022.3.62f2 editor to build these assets.");
        Directory.CreateDirectory("Assets/Prefabs");Directory.CreateDirectory("Assets/SharedMaterials");
        foreach(var atlas in new[]{"PoolFurniture","PoolBalls","Darts"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Models/Textures/"+atlas+"_BaseColor.png");
            importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.anisoLevel=8;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        }
        foreach(var material in new[]{"Wood","Cloth","RailRubber","PocketBlack","Metal","CueWood","CueTip","RackWood","BallAtlas",
            "SisalDark","SisalLight","RingRed","RingGreen","WireMetal","Numbers","DartPlastic","DartFlight"})Material(material);
        foreach(var name in Models)
        {
            var path="Assets/Models/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=1;importer.preserveHierarchy=true;importer.importAnimation=false;importer.addCollider=false;importer.isReadable=true;
            importer.importNormals=ModelImporterNormals.Import;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            foreach(var materialName in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m=>m.name).ToArray())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),materialName),Material(materialName));
            importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);Check(source,"Missing imported model "+name);
            ValidateModel(source,name);
            if(name=="PoolTable")Save(source,"pooltable");
            if(name=="DartsBoard")Save(source,"dartsboard");
            if(name=="Dart")Save(Find(source,"Dart_Root").gameObject,"dart");
            if(name=="PoolAccessories")
            {
                Save(Find(source,"PoolCue_Root").gameObject,"cue");Save(Find(source,"PoolRack_Root").gameObject,"rack");
                for(var i=0;i<16;i++)Save(Find(source,"Pool_Ball_"+i.ToString("00")).gameObject,"ball"+i.ToString("00"));
            }
        }
        // Count actual shared Material objects across both imported FBXs.
        var darts=Models.Where(n=>n=="Dart"||n=="DartsBoard").SelectMany(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/"+n+".fbx").GetComponentsInChildren<Renderer>(true))
            .SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
        Check(darts.Length==8,"Darts must share eight actual material objects across imports.");
        Check(darts.All(m=>Materials.Values.Contains(m)),"Darts importer material remap failed.");
        AssetDatabase.SaveAssets();
        HomeGamesPresentation.Build(repo);
        var output=Path.Combine(repo,"src/VICE.Lifestyle/Assets");Directory.CreateDirectory(output);
        var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="homegames.bundle",assetNames=Prefabs.ToArray()}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        Check(result,"Bundle build failed.");
        // Inspect the built bundle rather than only its source prefabs.
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"homegames.bundle"));Check(bundle,"Bundle reload failed.");
        foreach(var path in Prefabs)
        {
            var prefab=bundle.LoadAsset<GameObject>(path);Check(prefab,"Missing bundled prefab "+path);
            Check(prefab.GetComponentsInChildren<Collider>(true).Length==0&&prefab.GetComponentsInChildren<Rigidbody>(true).Length==0,"Bundle adds physics components.");
            Check(prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m&&m.mainTexture)),"Missing bundled atlas "+path);
        }
        var bundledDarts=new[]{"dartsboard","dart"}.SelectMany(n=>bundle.LoadAsset<GameObject>("assets/prefabs/"+n+".prefab").GetComponentsInChildren<Renderer>(true))
            .SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
        Check(bundledDarts.Length==8,"Built bundle must preserve eight shared darts material objects.");
        var dartHandle=new GameObject("Adapter validation centre handle");
        var dart=Object.Instantiate(bundle.LoadAsset<GameObject>("assets/prefabs/dart.prefab"),dartHandle.transform);
        dart.transform.localPosition=new Vector3(0,0,-.08f);
        foreach(var analyticalX in new[]{-.12f,.12f})
        {
            dartHandle.transform.position=new Vector3(-analyticalX,.03f,.135f);
            Near(Find(dart,"Dart_Root").position,new Vector3(-analyticalX,.03f,.055f),"actual dart wrapper/tip mapping");
        }
        Object.DestroyImmediate(dartHandle);
        var cue=Object.Instantiate(bundle.LoadAsset<GameObject>("assets/prefabs/cue.prefab"));
        foreach(var seconds in new[]{0f,.5f,1.5f})
        {
            cue.transform.position=new Vector3(0,.808575f,-.65f)-Vector3.forward*(.035f+seconds*.08f);
            Near(Find(cue,"CueTip").position,new Vector3(0,.808575f,-.685f-seconds*.08f),"actual tip cue clearance");
        }
        Object.DestroyImmediate(cue);
        bundle.Unload(true);
        var files=Models.Select(n=>"assets/VICEHomeGames/"+n+".fbx").Concat(new[]{"PoolFurniture","PoolBalls","Darts"}.Select(n=>"assets/VICEHomeGames/Textures/"+n+"_BaseColor.png"))
            .Concat(new[]{"scripts/Unity/BuildHomeGames.cs","scripts/Unity/HomeGamesPresentation.cs","scripts/Build-VICEHomeGamesAssets.ps1","assets/VICEHomeGames/validation/unity/accepted-artwork-sha256.json",
                "src/VICE.Lifestyle/Assets/Icons/PoolTable.png","src/VICE.Lifestyle/Assets/Icons/DartsBoard.png"}).Select(p=>ContractFile.Create(repo,p)).ToArray();
        File.WriteAllText(Path.Combine(output,"homegames.contract.json"),JsonUtility.ToJson(new Contract{schema=1,bundle=ContractFile.Create(repo,"src/VICE.Lifestyle/Assets/homegames.bundle"),sources=files},true));
        File.WriteAllText(Path.Combine(repo,"artifacts/vice-home-games-import.json"),JsonUtility.ToJson(new Report{checks=checks,prefabs=Prefabs.Count,uniqueDartsMaterials=darts.Length,unityVersion=Application.unityVersion,notes="Fresh import + built bundle validation; Standard materials rebound to native URP/Lit by Lifestyle. No game or co-op test."},true));
        Debug.Log("VICE_HOME_GAMES_BUNDLE_BUILT checks="+checks+" prefabs="+Prefabs.Count);
    }
    static Material Material(string name)
    {
        if(name=="MountMetal"||name=="DartMetal")name="WireMetal";
        if(Materials.TryGetValue(name,out var existing))return existing;
        var pool=new[]{"Wood","Cloth","RailRubber","PocketBlack","Metal","CueWood","CueTip","RackWood","BallAtlas"}.Contains(name);
        var atlas=name=="BallAtlas"?"PoolBalls":pool?"PoolFurniture":"Darts";
        var path="Assets/SharedMaterials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.name=name;material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Textures/"+atlas+"_BaseColor.png");
        material.SetFloat("_Metallic",name=="Metal"?.6f:name=="WireMetal"?.7f:0);
        material.SetFloat("_Glossiness",name=="BallAtlas"?.77f:name=="Cloth"||name=="PocketBlack"?.25f:pool?.52f:.32f);
        Materials[name]=material;EditorUtility.SetDirty(material);return material;
    }
    static Transform Find(GameObject root,string name)
    {var part=root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==name);Check(part,"Missing exact named part "+name);return part;}
    static void Near(Vector3 value,Vector3 expected,string name)
    {Check(Vector3.Distance(value,expected)<.00002f,name+" coordinate drift: "+value+" expected "+expected);}
    static void BoundsOf(GameObject root,string part,Vector3 min,Vector3 max)
    {
        var transform=Find(root,part);var mesh=transform.GetComponent<MeshFilter>().sharedMesh;
        var vertices=mesh.vertices.Select(v=>root.transform.InverseTransformPoint(transform.TransformPoint(v))).ToArray();
        Near(new Vector3(vertices.Min(v=>v.x),vertices.Min(v=>v.y),vertices.Min(v=>v.z)),min,part+" min");
        Near(new Vector3(vertices.Max(v=>v.x),vertices.Max(v=>v.y),vertices.Max(v=>v.z)),max,part+" max");
    }
    static void ValidateModel(GameObject root,string name)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true)){Near(t.localScale,Vector3.one,t.name+" scale");Check(Quaternion.Angle(t.localRotation,Quaternion.identity)<.001,t.name+" rotation");}
        Check(root.GetComponentsInChildren<Collider>(true).Length==0&&root.GetComponentsInChildren<Rigidbody>(true).Length==0,name+" imports physics");
        if(name=="PoolTable")
        {
            Near(root.transform.InverseTransformPoint(Find(root,"PoolTable_Root").position),Vector3.zero,"table root");
            BoundsOf(root,"Pool_Cloth",new Vector3(-.56f,.78f,-1.12f),new Vector3(.56f,.78f,1.12f));
            var names=new[]{"Pocket_NW","Pocket_NE","Pocket_W","Pocket_E","Pocket_SW","Pocket_SE"};
            var points=new[]{new Vector3(-.56f,.78f,1.12f),new Vector3(.56f,.78f,1.12f),new Vector3(-.56f,.78f,0),new Vector3(.56f,.78f,0),new Vector3(-.56f,.78f,-1.12f),new Vector3(.56f,.78f,-1.12f)};
            for(var i=0;i<names.Length;i++)Near(root.transform.InverseTransformPoint(Find(root,names[i]).position),points[i],names[i]);
        }
        if(name=="DartsBoard")
        {
            Near(root.transform.InverseTransformPoint(Find(root,"BoardFace").position),new Vector3(0,0,.055f),"board face");
            BoundsOf(root,"Darts_Board",new Vector3(-.225f,-.225f,.025f),new Vector3(.225f,.225f,.055f));
        }
        if(name=="PoolAccessories")
        {
            Near(root.transform.InverseTransformPoint(Find(root,"CueTip").position),Vector3.zero,"cue tip");
            BoundsOf(root,"Pool_Cue",new Vector3(-.016f,-.016f,-1.45f),new Vector3(.016f,.016f,0));
            for(var i=0;i<16;i++){var ball="Pool_Ball_"+i.ToString("00");Near(Find(root,ball).localPosition,Vector3.zero,ball+" pivot");BoundsOf(root,ball,Vector3.one*-.028575f,Vector3.one*.028575f);}
        }
        if(name=="Dart"){Near(root.transform.InverseTransformPoint(Find(root,"Dart_Root").position),Vector3.zero,"dart tip");Near(root.transform.InverseTransformPoint(Find(root,"DartGrip").position),new Vector3(0,0,.06f),"dart grip");}
    }
    static void Save(GameObject source,string name)
    {
        var root=new GameObject(name);var child=Object.Instantiate(source,root.transform);child.name=source.name;child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;
        Check(root.GetComponentsInChildren<Renderer>(true).Length>0,"Empty dynamic/static prefab "+name);
        var path="Assets/Prefabs/"+name+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);Prefabs.Add(path);Object.DestroyImmediate(root);
    }
    [Serializable] class Report {public int checks,prefabs,uniqueDartsMaterials;public string unityVersion,notes;}
    [Serializable] class Contract {public int schema;public ContractFile bundle;public ContractFile[] sources;}
    [Serializable] class ContractFile
    {
        public string path,sha256;
        public static ContractFile Create(string repo,string path)
        {using(var hash=System.Security.Cryptography.SHA256.Create())return new ContractFile{path=path,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(repo,path)))).Replace("-","").ToLowerInvariant()};}
    }
}
