using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1API.Products;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

internal static class StudioAssets
{
    internal static readonly Color32[] Palette = {
        new(240,231,204,255), new(24,29,43,255), new(247,170,56,255), new(235,65,71,255),
        new(62,191,166,255), new(149,95,211,255), new(84,149,225,255), new(252,235,128,255)
    };
    private static Il2CppAssetBundle? Bundle;
    private static Shader? RenderShader;
    private static readonly HashSet<string> BoundModels=new();
    private static GameObject? Cache;
    private static readonly Dictionary<string, GameObject> Visuals = new();
    private static readonly Dictionary<string,GameObject> StoredVisuals=new();
    private static readonly Dictionary<int, Texture2D> ArtTextures = new();
    private static readonly Dictionary<int, Material> ArtMaterials = new();
    private static readonly Dictionary<int, Sprite> Icons = new();
    private static readonly Dictionary<int, Texture2D> TabTextures=new(),SheetTextures=new();
    private static readonly Dictionary<int, Material> SheetMaterials=new();
    private static readonly Dictionary<(int Slot,bool Dry), Sprite> SheetIcons=new();
    private static readonly Dictionary<(int Slot,bool Dry), Texture2D> SheetIconTextures=new();
    private static Sprite? Star,Brand;
    private static readonly Dictionary<string, Sprite> PropIcons = new();
    private static readonly Dictionary<int, string> Signatures = new();
    internal static Sprite Symbol { get { Initialize();return Brand??=Sprite.Create(ArtTextures[2],new Rect(0,0,64,64),new(0.5f,0.5f),64); } }
    internal static Material SurfaceMaterial(Color color) { Initialize();var material=new Material(RenderShader);material.color=color;material.mainTexture=Texture2D.whiteTexture;return material; }
    internal static Sprite DeliveryStar
    {
        get
        {
            if (Star is not null) return Star;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color32[4096];
            var corners=Enumerable.Range(0,10).Select(i=>{
                var angle=MathF.PI/2+i*MathF.PI/5;var radius=i%2==0 ? 29:13;
                return new Vector2(31.5f+MathF.Cos(angle)*radius,31.5f+MathF.Sin(angle)*radius);
            }).ToArray();
            for(var y=0;y<64;y++) for(var x=0;x<64;x++)
            {
                var inside=false;
                for(int i=0,j=9;i<10;j=i++) if ((corners[i].y>y)!=(corners[j].y>y)
                    && x<(corners[j].x-corners[i].x)*(y-corners[i].y)/(corners[j].y-corners[i].y)+corners[i].x) inside=!inside;
                if (inside) pixels[y*64+x]=new(215,105,245,255);
            }
            texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));texture.Apply();
            Star=Sprite.Create(texture,new Rect(0,0,64,64),new(0.5f,0.5f),64);return Star;
        }
    }

    internal static void Initialize()
    {
        if (Bundle is not null) return;
        using var stream = typeof(StudioAssets).Assembly.GetManifestResourceStream("Personal.LSDGameplay.Studio.bundle")
            ?? throw new InvalidOperationException("The studio assets are missing.");
        using var memory = new MemoryStream(); stream.CopyTo(memory);
        Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()))
            ?? throw new InvalidOperationException("Unity could not load the studio assets.");
        Cache = new GameObject("Off Register local assets"); Cache.SetActive(false);
        Object.DontDestroyOnLoad(Cache);
        // Use the live game's URP shader so the world, icon rig and held-item
        // cameras share the same supported pass/constant-buffer layout.
        RenderShader=Shader.Find("Universal Render Pipeline/Unlit") ?? Bundle.LoadAsset<Shader>("assets/studiosurface.shader");
        UpdateArtwork(new WorkshopState());
        File.WriteAllBytes(Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory,"OffRegister.png"),
            ImageConversion.EncodeToPNG(ArtTextures[2]).ToArray());
    }

    internal static GameObject Model(string name)
    {
        Initialize();
        var model=Bundle!.LoadAsset<GameObject>($"assets/prefabs/{name.ToLowerInvariant()}.prefab")
            ?? throw new InvalidOperationException($"Missing studio prop: {name}.");
        if (BoundModels.Add(name)) foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials=new Il2CppReferenceArray<Material>(renderer.sharedMaterials.ToArray().Select(original=>{
                var material=new Material(RenderShader);material.color=original.color;material.mainTexture=original.mainTexture ?? Texture2D.whiteTexture;return material;
            }).ToArray());
        return model;
    }

    internal static GameObject Visual(string name, int slot = 0)
    {
        var key = name + ":" + slot;
        if (Visuals.TryGetValue(key, out var existing) && existing) return existing;
        var visual = Object.Instantiate(Model(name), Cache!.transform);
        visual.name = key; ApplyArtwork(visual, slot, name == "Tab",name=="Sheet"); visual.SetActive(false);
        Visuals[key] = visual; return visual;
    }

    internal static GameObject Stored(string name,int slot=0)
    {
        var key=name+":"+slot;
        if (StoredVisuals.TryGetValue(key,out var cached) && cached) return cached;
        var donor=Il2CppScheduleOne.Registry.GetItem("baggie").TryCast<Il2CppScheduleOne.ItemFramework.StorableItemDefinition>()!.StoredItem;
        var root=Object.Instantiate(donor.gameObject,Cache!.transform);root.SetActive(false);
        root.name="Studio stored "+key;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
        var model=Object.Instantiate(Visual(name,slot),root.transform);model.transform.localPosition=Vector3.zero;
        model.transform.localRotation=Quaternion.identity;model.SetActive(true);
        var stored=root.GetComponent<Il2CppScheduleOne.Storage.StoredItem>();stored.SetOriginalPrefab(donor);
        StoredVisuals[key]=root;return root;
    }
    internal static GameObject StoredSupply(bool ink)
    {
        var key=ink ? "stock-ink":"stock-paper";
        if(StoredVisuals.TryGetValue(key,out var cached) && cached)return cached;
        var donor=Il2CppScheduleOne.Registry.GetItem("baggie").TryCast<Il2CppScheduleOne.ItemFramework.StorableItemDefinition>()!.StoredItem;
        var root=Object.Instantiate(donor.gameObject,Cache!.transform);root.SetActive(false);root.name="Studio stored "+key;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
        var model=Object.Instantiate(Model(ink ? "Bench":"Sheet"),root.transform);model.SetActive(true);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.enabled=renderer.name==(ink ? "InkCartridge":"PaperBody");
        var visible=model.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name==(ink ? "InkCartridge":"PaperBody"));
        model.transform.position+=root.transform.position-visible.transform.TransformPoint(visible.sharedMesh.bounds.center);
        root.GetComponent<Il2CppScheduleOne.Storage.StoredItem>().SetOriginalPrefab(donor);StoredVisuals[key]=root;return root;
    }

    internal static void UpdateArtwork(WorkshopState state)
    {
        // Materials retain their identity so existing placed/stored clones update
        // only when that save's immutable slot is initially hydrated.
        foreach (var art in state.Artworks)
        {
            var signature = Convert.ToBase64String(art.Pixels) + (art.Repeated ? "R" : "S");
            if (Signatures.TryGetValue(art.Slot, out var previous) && previous == signature) continue;
            if (!ArtTextures.TryGetValue(art.Slot, out var texture))
            {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Repeat;
                ArtTextures[art.Slot] = texture;
            }
            var colors = art.Pixels.Select(p => Palette[p]).ToArray();
            texture.SetPixels32(new Il2CppStructArray<Color32>(colors)); texture.Apply(false, false);
            if (!ArtMaterials.TryGetValue(art.Slot, out var material))
            {
                material = new Material(RenderShader);
                material.color = Color.white; material.mainTexture = texture;
                ArtMaterials[art.Slot] = material;
            }
            material.mainTextureScale = art.Repeated ? new(8, 5) : Vector2.one;
            if (!TabTextures.TryGetValue(art.Slot,out var tabTexture))
            { tabTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);tabTexture.filterMode=FilterMode.Point;TabTextures[art.Slot]=tabTexture;Icons[art.Slot]=Sprite.Create(tabTexture,new Rect(0,0,64,64),new(0.5f,0.5f),64); }
            var tabPixels=new Color32[4096];
            for(var y=14;y<50;y++) for(var x=10;x<54;x++)
                tabPixels[y*64+x]=x is 10 or 53 || y is 14 or 49 ? Palette[0] : Palette[art.Pixels[Math.Clamp((y-15)*64/34,0,63)*64+Math.Clamp((x-11)*64/42,0,63)]];
            tabTexture.SetPixels32(new Il2CppStructArray<Color32>(tabPixels));tabTexture.Apply();
            if (!SheetTextures.TryGetValue(art.Slot,out var sheetTexture))
            {
                sheetTexture=new Texture2D(320,200,TextureFormat.RGBA32,false);sheetTexture.filterMode=FilterMode.Point;SheetTextures[art.Slot]=sheetTexture;
                SheetMaterials[art.Slot]=new Material(RenderShader) { color=Color.white,mainTexture=sheetTexture };
            }
            var sheetPixels=new Color32[320*200];
            for(var y=0;y<200;y++) for(var x=0;x<320;x++)
            {
                var sx=art.Repeated ? x%40*64/40 : x*64/320;var sy=art.Repeated ? y%40*64/40 : y*64/200;
                sheetPixels[y*320+x]=(x%40==0 && y%4<2)||(y%40==0 && x%4<2) ? Palette[0] : Palette[art.Pixels[sy*64+sx]];
            }
            sheetTexture.SetPixels32(new Il2CppStructArray<Color32>(sheetPixels));sheetTexture.Apply();
            foreach(var dry in new[]{false,true})UpdateSheetIcon(art,sheetPixels,dry);
            Signatures[art.Slot] = signature;
        }
        // Unused slots must not keep another save's drawing.
        for (var slot = 0; slot < WorkshopRules.ArtSlots; slot++)
            if (state.Artworks.All(a => a.Slot != slot) && ArtTextures.TryGetValue(slot, out var texture))
            { texture.SetPixels32(new Il2CppStructArray<Color32>(Enumerable.Repeat(Palette[0],4096).ToArray())); texture.Apply(); Signatures.Remove(slot); }
    }

    internal static Sprite Icon(int slot)
    {
        if (!Icons.TryGetValue(slot, out var icon))
        {
            var placeholder = new Artwork { Slot = slot, Name = WorkshopRules.EditionLabel(slot) };
            var state = new WorkshopState(); if (slot >= 3) state.Artworks.Add(placeholder);
            UpdateArtwork(state); icon = Icons[slot];
        }
        return icon;
    }

    internal static Sprite PropIcon(string name)
    {
        if (PropIcons.TryGetValue(name,out var icon)) return icon;
        using var stream=typeof(StudioAssets).Assembly.GetManifestResourceStream($"Personal.LSDGameplay.Icons.{name}.png")
            ?? throw new InvalidOperationException($"Missing studio icon: {name}.");
        using var memory=new MemoryStream();stream.CopyTo(memory);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!ImageConversion.LoadImage(texture,new Il2CppStructArray<byte>(memory.ToArray()),false))
            throw new InvalidDataException($"Invalid studio icon: {name}.");
        texture.filterMode=FilterMode.Bilinear;
        icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new(0.5f,0.5f),256);
        PropIcons[name]=icon;return icon;
    }

    internal static Texture2D Texture(int slot) { _ = Icon(slot); return ArtTextures[slot]; }
    private static void UpdateSheetIcon(Artwork art,Color32[] sheet,bool dry)
    {
        var key=(art.Slot,dry);
        if(!SheetIconTextures.TryGetValue(key,out var texture)){
            texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;
            SheetIconTextures[key]=texture;SheetIcons[key]=Sprite.Create(texture,new Rect(0,0,128,128),new(0.5f,0.5f),128);
        }
        var pixels=new Color32[128*128];var border=dry ? new Color32(244,211,128,255):new Color32(74,199,240,255);
        for(var y=27;y<103;y++)for(var x=7;x<121;x++){
            var u=(x-9)/110f;var v=(y-29)/72f;
            var color=sheet[Math.Clamp((int)(v*199),0,199)*320+Math.Clamp((int)(u*319),0,319)];
            if(!dry)color=new((byte)(color.r*0.72f),(byte)(color.g*0.82f),(byte)Math.Min(255,color.b*0.72f+56),255);
            if(x<9 || x>=119 || y<29 || y>=101)color=border;
            else if((x-9)%14<2 || (y-29)%14<2)color=Palette[1];
            pixels[y*128+x]=color;
        }
        // A bold badge survives the inventory's small thumbnail size.
        for(var y=91;y<119;y++)for(var x=94;x<122;x++)if((x-108)*(x-108)+(y-105)*(y-105)<196){
            var mark=dry ? (Math.Abs(y-(205-x))<3 && x is >=99 and <=105)||(Math.Abs(y-(100+(x-105)*1.18f))<3 && x is >=105 and <=116)
                : (x-108)*(x-108)+(y-103)*(y-103)<55 || (y is >=104 and <=114 && Math.Abs(x-108)<(115-y)*0.6f);
            pixels[y*128+x]=mark ? new Color32(250,250,242,255):dry ? new Color32(137,102,37,255):new Color32(27,112,186,255);
        }
        texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));texture.Apply();
    }
    internal static Sprite SheetIcon(int slot,bool dry) { _=Icon(slot);return SheetIcons[(slot,dry)]; }
    internal static Texture2D SheetTexture(int slot) { _=Icon(slot);return SheetTextures[slot]; }
    internal static void ApplyArtwork(GameObject root, int slot, bool tab = false,bool sheet=false)
    {
        _ = Icon(slot);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.name.StartsWith("ArtworkSurface", StringComparison.Ordinal))
            {
                renderer.sharedMaterial = sheet ? SheetMaterials[slot] : ArtMaterials[slot];
                if (tab)
                {
                    // Inventory and loose tabs show the complete artwork or
                    // one motif from the repeated sheet's upper-left tile.
                    var block = new MaterialPropertyBlock();
                    block.SetVector("_MainTex_ST",new(1,1,0,0));
                    block.SetVector("_BaseMap_ST",new(1,1,0,0));
                    renderer.SetPropertyBlock(block);
                }
            }
    }
}
