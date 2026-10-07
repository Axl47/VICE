using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Packaging;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace PersonalMods.LSDGameplay;

internal static class StudioPackaging
{
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        ModSupport.Patch(harmony,typeof(FunctionalProduct),"InitializeVisuals",typeof(StudioPackaging),postfix:nameof(TabBody));
        ModSupport.Patch(harmony,typeof(FunctionalPackaging),"Initialize",typeof(StudioPackaging),postfix:nameof(TabContact));
        ModSupport.Patch(harmony,typeof(PackagingStation),"StartTask",typeof(StudioPackaging),postfix:nameof(TabTask));
    }
    private static bool IsTab(string? id)=>id?.StartsWith(WorkshopRules.Owner+":tab-",StringComparison.Ordinal)==true;
    private static void TabBody(FunctionalProduct __instance,ItemInstance __0)
    {
        if(!IsTab(__0?.ID) || !__instance.Visuals || !__instance.Visuals.VisualsContainer)return;
        var root=__instance.transform;var meshes=__instance.Visuals.VisualsContainer.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh).ToArray();
        if(meshes.Length==0)return;
        var bounds=new Bounds(root.InverseTransformPoint(meshes[0].transform.TransformPoint(meshes[0].sharedMesh.bounds.center)),Vector3.zero);
        foreach(var mesh in meshes){
            var local=mesh.sharedMesh.bounds;
            for(var x=0;x<2;x++)for(var y=0;y<2;y++)for(var z=0;z<2;z++)
                bounds.Encapsulate(root.InverseTransformPoint(mesh.transform.TransformPoint(new Vector3(x==0 ? local.min.x:local.max.x,y==0 ? local.min.y:local.max.y,z==0 ? local.min.z:local.max.z))));
        }
        var collider=__instance.GetComponent<BoxCollider>() ?? __instance.gameObject.AddComponent<BoxCollider>();
        foreach(var previous in __instance.GetComponentsInChildren<Collider>(true))if(previous.Pointer!=collider.Pointer)previous.enabled=false;
        // A paper-thin convex hull can cross a trigger before the native contact
        // timer accepts it. Keep the visible paper small and give it a solid body.
        var size=bounds.size;var scale=root.lossyScale;
        if(size.x<=size.y && size.x<=size.z)size.x=Math.Max(size.x,0.008f/Math.Abs(scale.x));
        else if(size.y<=size.z)size.y=Math.Max(size.y,0.008f/Math.Abs(scale.y));
        else size.z=Math.Max(size.z,0.008f/Math.Abs(scale.z));
        collider.center=bounds.center;collider.size=size;collider.isTrigger=false;collider.enabled=true;
        __instance.DisableGravityWhenDragged=true;__instance.RotationEnabled=false;
        if(__instance.Rb){__instance.Rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;__instance.Rb.constraints|=RigidbodyConstraints.FreezeRotation;__instance.Rb.solverIterations=12;}
    }
    private static void TabContact(FunctionalPackaging __instance,PackagingStation __0)
    {
        if(!__0 || !IsTab(__0.ProductSlot?.ItemInstance?.ID))return;
        // Accept a released tab on a real native trigger contact; held tabs still
        // follow the native rule and cannot be packed until the mouse releases.
        __instance.ProductContactTime=0.01f;
        __instance.ProductContactMaxVelocity=Math.Max(__instance.ProductContactMaxVelocity,1.25f);
    }
    private static void TabTask(PackagingStation __instance,Il2CppScheduleOne.PlayerTasks.Task __result)
    {
        var item=__instance.ProductSlot?.ItemInstance;
        if(!IsTab(item?.ID) || __result?.TryCast<Il2CppScheduleOne.PlayerTasks.PackageProductTask>() is not {} task)return;
        // Jar initialization can overwrite the base packaging contact settings.
        // Configure the finished task, including any subtype-specific setup.
        TabContact(task.Packaging,__instance);
        foreach(var product in task.Products)TabBody(product,item!);
    }
}
