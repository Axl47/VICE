using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildStudio
{
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/StudioSurface.shader");
        foreach (var name in new[] { "Bench", "Cabinet", "Parcel", "Sheet", "Tab", "Packet" })
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Models/"+name+".fbx");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + name + ".fbx");
            if (!source) throw new System.InvalidOperationException("Missing Blender model: " + name);
            var root = Object.Instantiate(source);
            root.name = name;
            if(name=="Bench" || name=="Cabinet"){
                var required=name=="Bench" ? new[]{"StaticDetails","CutterArm","CutterGrip"}
                    :new[]{"StaticDetails","CabinetDoor","CabinetHandle","CabinetHinges","ActiveDryingTray","ActiveTrayLip"};
                var parts=new System.Collections.Generic.HashSet<string>();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))parts.Add(renderer.name);
                foreach(var part in required)if(!parts.Contains(part))throw new System.InvalidOperationException("Obsolete studio model: "+name+" missing "+part);
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var materials = new Material[originals.Length];
                for (var i = 0; i < originals.Length; i++)
                {
                    var key = originals[i] ? originals[i].name : "Steel";
                    var path = "Assets/Prefabs/" + key + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material)
                    {
                        material = new Material(shader) { name = key };
                        AssetDatabase.CreateAsset(material,path);
                    }
                    material.shader=shader;
                    material.color=key switch {
                        "Steel"=>new Color(0.25f,0.30f,0.32f),"Wood"=>new Color(0.37f,0.29f,0.20f),
                        "Paper"=>new Color(0.91f,0.87f,0.74f),"Ink"=>new Color(0.055f,0.070f,0.080f),
                        "Accent"=>new Color(0.19f,0.40f,0.47f),"Lightbox"=>new Color(0.67f,0.82f,0.76f),
                        "Glass"=>new Color(0.13f,0.24f,0.26f),
                        "Enamel"=>new Color(0.62f,0.66f,0.63f),"Edge"=>new Color(0.77f,0.80f,0.77f),
                        "Kraft"=>new Color(0.61f,0.39f,0.16f),"KraftEdge"=>new Color(0.74f,0.52f,0.25f),
                        "Label"=>new Color(0.85f,0.80f,0.65f),"Warning"=>new Color(0.87f,0.55f,0.18f),_=>Color.white };
                    EditorUtility.SetDirty(material);
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        var destination = Path.GetFullPath(Path.Combine(Application.dataPath,"../../..","src/LSDGameplay/Assets"));
        Directory.CreateDirectory(destination);
        var paths = new string[7];
        var names = new[] { "Bench", "Cabinet", "Parcel", "Sheet", "Tab", "Packet" };
        for (var i=0;i<6;i++) paths[i]="Assets/Prefabs/"+names[i]+".prefab";
        paths[6]="Assets/StudioSurface.shader";
        var result = BuildPipeline.BuildAssetBundles(destination,new[] { new AssetBundleBuild { assetBundleName="studio.bundle",assetNames=paths } },BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if (!result) throw new System.InvalidOperationException("Studio bundle build failed.");
        var repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var sources=new System.Collections.Generic.List<ContractFile>();
        foreach(var name in names)sources.Add(ContractFile.Create(repo,"assets/LSDStudio/Assets/Models/"+name+".fbx"));
        foreach(var name in new[]{"Bench","Cabinet","Ink","Paper","Prism","Sleeve"})sources.Add(ContractFile.Create(repo,"src/LSDGameplay/Assets/Icons/"+name+".png"));
        sources.Add(ContractFile.Create(repo,"scripts/Build-LSDStudio.py"));
        sources.Add(ContractFile.Create(repo,"assets/LSDStudio/Assets/Editor/BuildStudio.cs"));
        var contract=new StudioContract{schema=2,bundle=ContractFile.Create(repo,"src/LSDGameplay/Assets/studio.bundle"),sources=sources.ToArray()};
        File.WriteAllText(Path.Combine(destination,"studio.contract.json"),JsonUtility.ToJson(contract,true));
        Debug.Log("LSD_STUDIO_BUNDLE_BUILT " + destination);
    }
    [System.Serializable] private sealed class StudioContract {public int schema;public ContractFile bundle;public ContractFile[] sources;}
    [System.Serializable] private sealed class ContractFile
    {
        public string path,sha256;
        public static ContractFile Create(string repo,string relative)
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return new ContractFile{path=relative,sha256=System.BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(repo,relative)))).Replace("-","").ToLowerInvariant()};
        }
    }
}
