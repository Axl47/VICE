using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Tiles;
using MelonLoader;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PersonalMods.LSDGameplay;

// Opt-in comparison on a caller-provided save copy. No saving or normal-play fixtures.
internal sealed class LightingDiagnostic
{
    private readonly WorkshopFeature Mod;
    private readonly string Output;
    private BuildableItem? Bench,Station;
    private Light? Key;
    private Vector3 Target;
    private int Phase;
    private float Next;
    internal LightingDiagnostic(WorkshopFeature mod,string output){Mod=mod;Output=output;}
    internal bool Tick()
    {
        if(Time.realtimeSinceStartup<Next)return false;
        switch(Phase){
            case 0:
                PlacePair();MoveAndFrame();
                Key=new GameObject("Lighting comparison key").AddComponent<Light>();Key.type=LightType.Point;
                Key.range=6;Key.intensity=5;Key.color=new(1,0.86f,0.7f);Key.shadows=LightShadows.Soft;Key.enabled=false;
                Key.transform.position=Target+Bench!.transform.forward*1.2f+Vector3.up*1.6f;
                Next=Time.realtimeSinceStartup+3;Phase=1;return false;
            case 1:
                DumpRenderers();
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"room-lighting.png"));Next=Time.realtimeSinceStartup+0.6f;Phase=2;return false;
            case 2:
                Key!.enabled=true;Next=Time.realtimeSinceStartup+2;Phase=3;return false;
            case 3:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"warm-key-lighting.png"));Next=Time.realtimeSinceStartup+0.6f;Phase=4;return false;
            case 4:
                Key!.transform.position=Target-Bench!.transform.right*1.5f+Vector3.up*1.1f;Key.color=new(0.6f,0.78f,1);
                Next=Time.realtimeSinceStartup+2;Phase=5;return false;
            case 5:
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"cool-side-lighting.png"));Next=Time.realtimeSinceStartup+0.6f;Phase=6;return false;
            case 6:
                Object.Destroy(Key!.gameObject);
                File.WriteAllText(Path.Combine(Output,"lighting-check-complete.txt"),"Native Blotter Bench and Packaging Station placed side by side; room, warm key and cool side comparisons captured. Isolated save was not saved.");
                MelonLogger.Msg("LSD_LIGHTING_CHECK_CAPTURED side-by-side native furniture and three lighting conditions.");
                Next=Time.realtimeSinceStartup+30;Phase=7;return false;
            default:PlayerCamera.Instance.StopTransformOverride(0,true,true);return true;
        }
    }
    private void PlacePair()
    {
        var office=Mod.World!.Office!;
        if(!office || !office.IsOwned)throw new InvalidOperationException("Lighting test requires the owned Sewer Office.");
        foreach(var grid in office.Grids)foreach(var tile in grid.Tiles){
            var free=true;
            for(var x=0;x<7;x++)for(var y=0;y<2;y++){
                var candidate=grid.GetTile(new Coordinate(tile.x+x,tile.y+y));if(!candidate || !candidate.CanBeBuiltOn())free=false;
            }
            if(!free)continue;
            var center=tile.transform.position+grid.transform.right*1.5f+grid.transform.forward*0.25f+Vector3.up*0.7f;
            var bounds=new Bounds(center,new(3.5f,1.4f,1));
            if(office.BuildableItems.ToArray().Any(item=>item && item.BoundingCollider && bounds.Intersects(item.BoundingCollider.bounds)))continue;
            Bench=BuildManager.Instance.CreateGridItem(Content.Create(WorkshopRules.Bench,1),grid,new(tile.x,tile.y),0,Guid.NewGuid().ToString(),null);
            Station=BuildManager.Instance.CreateGridItem(Content.Create("packagingstation",1),grid,new(tile.x+4,tile.y),0,Guid.NewGuid().ToString(),null);
            if(!Bench || !Station || !Bench.Initialized || !Station.Initialized)throw new InvalidOperationException("Native side-by-side furniture placement failed.");
            Target=center+Vector3.up*0.1f;return;
        }
        throw new InvalidOperationException("No shared footprint for side-by-side furniture lighting test.");
    }
    private void MoveAndFrame()
    {
        PlayerMovement.Instance.Teleport(Target-Bench!.transform.forward*2,true);
        var position=Target+Bench.transform.forward*3.5f+Vector3.up*1.1f;
        PlayerCamera.Instance.OverrideTransform(position,Quaternion.LookRotation(Target-position),0.2f,false);
    }
    private void DumpRenderers()
    {
        var lines=new List<string>();
        foreach(var item in new[]{Bench!,Station!})foreach(var renderer in item.GetComponentsInChildren<Renderer>(true)){
            if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
            lines.Add($"{item.ItemInstance.ID}/{renderer.name}: cast={renderer.shadowCastingMode}, receive={renderer.receiveShadows}, probes={renderer.lightProbeUsage}, reflection={renderer.reflectionProbeUsage}, renderingLayers={renderer.renderingLayerMask}, lightmap={renderer.lightmapIndex}, layer={renderer.gameObject.layer}, bounds={renderer.bounds}");
            foreach(var material in renderer.sharedMaterials){
                if(!material)continue;
                lines.Add($"  material={material.name}, shader={material.shader.name}, supported={material.shader.isSupported}, color={material.color}, keywords={string.Join(",",material.shaderKeywords.ToArray())}");
                var shader=material.shader;
                for(var i=0;i<shader.GetPropertyCount();i++){
                    var property=shader.GetPropertyName(i);var type=shader.GetPropertyType(i);
                    var value=type switch{
                        UnityEngine.Rendering.ShaderPropertyType.Color=>material.GetColor(property).ToString(),
                        UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range=>material.GetFloat(property).ToString(),
                        UnityEngine.Rendering.ShaderPropertyType.Texture=>material.GetTexture(property)?.name??"none",
                        _=>""};
                    lines.Add($"    {property} ({type})={value}");
                }
            }
        }
        File.WriteAllLines(Path.Combine(Output,"renderer-materials.txt"),lines);
        if(Environment.GetCommandLineArgs().Contains("--lsd-lighting-verify")){
            foreach(var model in new[]{"Bench","Cabinet","Parcel","Sheet","Tab","Packet"})
                foreach(var renderer in StudioAssets.Model(model).GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)
                        if(!material || material.shader.name!="Universal Render Pipeline/Lit" || !material.shader.isSupported || material.GetFloat("_ReceiveShadows")!=1 || material.GetFloat("_EnvironmentReflections")!=0)
                            throw new InvalidOperationException($"A physical studio surface differs from native station lighting: {model}/{renderer.name}, material={material?.name}, shader={material?.shader?.name}, supported={material?.shader?.isSupported}, shadows={material?.GetFloat("_ReceiveShadows")}, environment={material?.GetFloat("_EnvironmentReflections")}");
            var reference=Station!.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Table");
            foreach(var renderer in Bench!.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && r.sharedMaterials.Length>0))
                if(renderer.shadowCastingMode!=reference.shadowCastingMode || renderer.receiveShadows!=reference.receiveShadows || renderer.lightProbeUsage!=reference.lightProbeUsage || renderer.reflectionProbeUsage!=reference.reflectionProbeUsage || renderer.renderingLayerMask!=reference.renderingLayerMask)
                    throw new InvalidOperationException("Bench renderer lighting settings differ from native station: "+renderer.name);
            File.WriteAllText(Path.Combine(Output,"lighting-bindings-passed.txt"),"All six physical model families use supported native URP Lit with received shadows and station-matched environment reflection settings. Live bench shadow/probe/rendering-layer settings match the native table.");
        }
        var samples=new List<object>();
        foreach(var pair in new[]{(Item:Bench!,Name:"WorkbenchTop"),(Item:Station!,Name:"Table")}){
            var renderer=pair.Item.GetComponentsInChildren<Renderer>(true).First(r=>r.name==pair.Name);
            var bounds=renderer.bounds;
            foreach(var x in new[]{-0.6f,-0.3f,0f,0.3f,0.6f}){
                var world=bounds.center+Vector3.up*bounds.extents.y+pair.Item.transform.forward*(bounds.extents.z*0.85f)+pair.Item.transform.right*(bounds.extents.x*x);
                var screen=PlayerCamera.Instance.Camera.WorldToScreenPoint(world);
                samples.Add(new{model=pair.Name,x=(int)screen.x,y=Screen.height-1-(int)screen.y});
            }
        }
        File.WriteAllText(Path.Combine(Output,"lighting-samples.json"),System.Text.Json.JsonSerializer.Serialize(new{width=Screen.width,height=Screen.height,samples}));
    }
}
