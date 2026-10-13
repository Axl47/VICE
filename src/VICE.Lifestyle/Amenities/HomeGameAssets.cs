using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object=UnityEngine.Object;

namespace VICE.Lifestyle.Amenities;

internal static class HomeGameAssets
{
    internal const string Resource="VICE.Lifestyle.HomeGames.bundle";
    internal static bool Enabled=true;
    private static bool Failed;
    private static Il2CppAssetBundle? Bundle;
    private static GameObject? Cache;
    private static readonly Dictionary<string,GameObject> Templates=new();
    private static readonly Dictionary<string,Material> Materials=new();
    internal static GameObject? Template(string name)
    {
        if(!Enabled||Failed)return null;
        if(Templates.TryGetValue(name,out var cached)&&cached)return cached;
        try
        {
            if(Bundle is null)
            {
                using var stream=typeof(HomeGameAssets).Assembly.GetManifestResourceStream(Resource)??throw new InvalidDataException("Home-games model bundle is absent; using primitives.");
                using var memory=new MemoryStream();stream.CopyTo(memory);
                Bundle=Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()))??throw new InvalidDataException("Unity could not load home-games artwork.");
                Cache=new("VICE home-games local model templates");Cache.SetActive(false);Object.DontDestroyOnLoad(Cache);
            }
            var prefab=Bundle.LoadAsset<GameObject>("assets/prefabs/"+name+".prefab")??throw new InvalidDataException("Missing home-games visual: "+name);
            var model=Object.Instantiate(prefab,Cache!.transform);model.name=name;model.SetActive(false);
            try
            {
                if(model.GetComponentsInChildren<Collider>(true).Length!=0||model.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                    throw new InvalidDataException("Home-games artwork must not add physics components.");
                var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidDataException("Native URP/Lit is unavailable for home-games artwork.");
                if(!shader.isSupported)throw new InvalidDataException("Native home-games URP/Lit shader is unsupported.");
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=new Il2CppReferenceArray<Material>(renderer.sharedMaterials.ToArray().Select(original=>
                    {
                        if(!original||!original.mainTexture)throw new InvalidDataException("Missing home-games shared material or atlas.");
                        var key=HomeGameVisualContract.CanonicalMaterial(original.name.Replace(" (Instance)","",StringComparison.Ordinal));
                        if(!Materials.TryGetValue(key,out var material))
                        {
                            var surface=HomeGameVisualContract.Surface(key);
                            material=new(shader){name="VICE Home Games "+key};material.SetColor("_BaseColor",Color.white);
                            material.SetTexture("_BaseMap",original.mainTexture);material.SetFloat("_Metallic",surface.Metallic);material.SetFloat("_Smoothness",surface.Smoothness);
                            material.SetFloat("_Surface",0);material.SetFloat("_Cull",2);material.SetFloat("_ReceiveShadows",1);
                            Materials[key]=material;
                        }
                        return material;
                    }).ToArray());
                }
                Templates[name]=model;return model;
            }
            catch{Object.Destroy(model);throw;}
        }
        catch(Exception error){Failed=true;ModSupport.WarnOnce("amenities:model-assets",error.Message+" Remaining visuals use the primitive fallback.");return null;}
    }
    internal static GameObject? Clone(string name,Transform parent)
    {
        var template=Template(name);if(!template)return null;
        var visual=Object.Instantiate(template!,parent);visual.name=template!.name;visual.SetActive(true);
        visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;return visual;
    }
}
