using Il2CppScheduleOne.EntityFramework;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Shop;
using S1API.Items;
using S1API.Items.Buildable;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace PersonalMods.TV;

internal static class TVContent
{
    internal const string ItemId = "personal.tv:screen";
    private static GameObject? Model;
    private static S1API.Items.Buildable.BuildableItemDefinition? Definition;
    internal static void Register()
    {
        if(Definition is not null){ItemManager.EnsureItemRegistered(Definition);return;}
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??throw new InvalidOperationException("The game's screen shader is unavailable.");
        Model=new GameObject("Personal TV model");Model.SetActive(false);Object.DontDestroyOnLoad(Model);
        var bodyMaterial=new Material(shader);bodyMaterial.SetColor("_BaseColor",new(0.025f,0.035f,0.045f,1));
        var faceMaterial=new Material(shader);faceMaterial.SetColor("_BaseColor",Color.white);faceMaterial.SetTexture("_BaseMap",Texture2D.blackTexture);
        var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.name="TV bezel";body.transform.SetParent(Model.transform,false);
        // Surface placement points local +Z away from the wall. Keep the original bezel position
        // so the display also clears the wall on screens placed with the previous version.
        body.transform.localPosition=new(0,0,0.06f);body.transform.localScale=new(2.52f,1.47f,0.10f);body.GetComponent<MeshRenderer>().sharedMaterial=bodyMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="TV display";face.transform.SetParent(Model.transform,false);
        face.transform.localPosition=new(0,0,0.12f);face.transform.localRotation=Quaternion.Euler(0,180,0);
        face.transform.localScale=new(2.4f,1.35f,1);face.GetComponent<MeshRenderer>().sharedMaterial=faceMaterial;Object.DestroyImmediate(face.GetComponent<Collider>());
        Definition=FurnitureCreator.CreateBuilder().WithBasicInfo(ItemId,"Wall Screen","A wall-mounted media screen. Interact to choose images, video URLs or Relay episodes.")
            .WithModel(Model).WithPlacement(FurniturePlacementMode.Surface).WithSurfacePlacement(FurnitureSurfaceType.Wall,false)
            .WithPricing(450).WithStackLimit(5).WithIcon(Icon()).WithBuildSound(S1API.Items.Buildable.BuildSoundType.Metal).Build();
    }
    private static Sprite Icon()
    {
        var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
        for(var y=0;y<128;y++)for(var x=0;x<128;x++)
        {
            var inside=x is >= 7 and <=120&&y is >= 30 and <=99;
            var display=x is >= 13 and <=114&&y is >= 36 and <=93;
            texture.SetPixel(x,y,display?new Color(.08f+(x-13)*.001f,.32f+(y-36)*.003f,.49f,1):inside?new Color(.07f,.10f,.13f,1):Color.clear);
        }
        texture.Apply(false,false);return Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f));
    }
    internal static void Shops()
    {
        if(Definition is null)return;
        foreach(var shop in ShopInterface.AllShops.ToArray().Where(s=>s&&s.ShopCode=="boutique"))
            if(shop.GetListing(ItemId) is null)S1API.Shops.ShopManager.AddToShops(Definition,shop.ShopName);
    }
}

internal sealed class TVWorld : IDisposable
{
    internal readonly Dictionary<string,ScreenController> Screens=new();
    private double NextScan;
    internal static void Install(HarmonyLib.Harmony harmony)
    {ModSupport.Patch(harmony,typeof(InteractableObject),"StartInteract",typeof(TVWorld),prefix:nameof(Interact));TVInteraction.Install(harmony);}
    private static bool Interact(InteractableObject __instance)
    {
        var item=__instance.GetComponentInParent<BuildableItem>();
        if(!item||item.ItemInstance?.ID!=TVContent.ItemId||PersonalTVMod.Current?.Ready!=true)return true;
        PersonalTVMod.Current.Panel?.Open(item.GUID.ToString());return false;
    }
    internal void Tick()
    {
        if(TVNetwork.Now>=NextScan){NextScan=TVNetwork.Now+0.5;Scan();}
        foreach(var screen in Screens.Values)screen.Tick();
    }
    internal void Scan()
    {
        var found=new HashSet<string>();
        foreach(var property in Property.Properties.ToArray())
        {
            if(!property)continue;
            foreach(var item in property.BuildableItems.ToArray())
            {
                if(!item||!item.Initialized||item.isGhost||item.IsDestroyed||item.ItemInstance?.ID!=TVContent.ItemId)continue;
                var id=item.GUID.ToString();found.Add(id);
                if(Screens.ContainsKey(id))continue;
                if(TVNetwork.Host)PersonalTVMod.Current!.EnsureScreen(id);
                foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name is not ("TV display" or "TV bezel"))
                    {renderer.enabled=false;if(renderer.gameObject.Pointer!=item.gameObject.Pointer)renderer.gameObject.SetActive(false);}
                var interactions=item.GetComponentsInChildren<InteractableObject>(true);
                if(!interactions.Any(i=>i.gameObject.activeInHierarchy))
                {
                    var interaction=item.gameObject.AddComponent<InteractableObject>();
                    interaction.onHovered=new UnityEvent();interaction.onInteractStart=new UnityEvent();interaction.onInteractEnd=new UnityEvent();
                    interaction.MaxInteractionRange=3;interaction.RequiresUniqueClick=true;interaction.displayLocationCollider=item.BoundingCollider;interaction.SetMessage("Use Wall Screen");
                }
                interactions=item.GetComponentsInChildren<InteractableObject>(true);
                foreach(var interaction in interactions)
                {
                    interaction.SetMessage("Use Wall Screen");
                    // Bind the native event too: runtime-invoked IL2CPP methods can bypass entry-point detours.
                    interaction.onInteractStart=new UnityEvent();
                    interaction.onInteractStart.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>PersonalTVMod.Current?.Panel?.Open(id)))!);
                }
                Screens[id]=new ScreenController(item);
            }
        }
        foreach(var id in Screens.Keys.Where(id=>!found.Contains(id)).ToArray())
        {Screens[id].Dispose();Screens.Remove(id);if(TVNetwork.Host)PersonalTVMod.Current!.RemoveScreen(id);}
    }
    public void Dispose(){foreach(var screen in Screens.Values)screen.Dispose();Screens.Clear();}
}
