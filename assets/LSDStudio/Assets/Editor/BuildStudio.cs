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
                        "Steel"=>new Color(0.18f,0.23f,0.25f),"Wood"=>new Color(0.31f,0.19f,0.12f),
                        "Paper"=>new Color(0.91f,0.88f,0.77f),"Ink"=>new Color(0.07f,0.085f,0.10f),
                        "Accent"=>new Color(0.11f,0.72f,0.65f),"Lightbox"=>new Color(0.61f,0.83f,0.75f),
                        "Glass"=>new Color(0.13f,0.24f,0.26f),_=>Color.white };
                    if(key=="Prism")material.color=new Color(0.59f,0.24f,0.85f);
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
        Debug.Log("LSD_STUDIO_BUNDLE_BUILT " + destination);
    }
}
