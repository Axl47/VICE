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
    private static readonly Dictionary<string, Sprite> PropIcons = new();
    private static readonly Dictionary<int, string> Signatures = new();
    internal static Sprite Symbol { get { Initialize();return Icon(2); } }

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
        visual.name = key; ApplyArtwork(visual, slot, name == "Tab"); visual.SetActive(false);
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
            if (!Icons.ContainsKey(art.Slot)) Icons[art.Slot] = Sprite.Create(texture, new Rect(0, 0, 64, 64), new(0.5f, 0.5f), 64);
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
    internal static void ApplyArtwork(GameObject root, int slot, bool tab = false)
    {
        _ = Icon(slot);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.name.StartsWith("ArtworkSurface", StringComparison.Ordinal))
            {
                renderer.sharedMaterial = ArtMaterials[slot];
                if (tab)
                {
                    // A tab shows the centre crop of a sheet, or one complete
                    // motif when the edition uses repeating artwork.
                    var repeated = ArtMaterials[slot].mainTextureScale.x > 1;
                    var block = new MaterialPropertyBlock();
                    block.SetVector("_MainTex_ST", repeated ? new(1,1,0,0) : new(0.125f,0.2f,0.4375f,0.4f));
                    block.SetVector("_BaseMap_ST", repeated ? new(1,1,0,0) : new(0.125f,0.2f,0.4375f,0.4f));
                    renderer.SetPropertyBlock(block);
                }
            }
    }
}
