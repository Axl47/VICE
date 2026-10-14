using UnityEngine;
using Il2CppScheduleOne.Police;

namespace NACops;

public static class RuntimeImpostor
{
    public static Vector3 targetPosition = new(0, 501, 0);
    public static Vector3 targetRotationEuler = Vector3.zero;
    public static Dictionary<int, Texture2D> createdTextures = new();

    public static Texture2D CreateImpostor(PoliceOfficer officer)
    {
        GameObject visual = null, cameraObject = null, lightObject = null;
        RenderTexture buffer = null;
        var previous = RenderTexture.active;
        Texture2D texture = null;
        var baked = new List<Mesh>();
        try
        {
            var source = officer.Avatar.transform;
            visual = new GameObject("VICE isolated avatar geometry"); visual.layer = 2;
            visual.transform.SetPositionAndRotation(targetPosition, Quaternion.identity);
            visual.transform.localScale = source.lossyScale;
            foreach (var renderer in officer.Avatar.BodyContainer.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeSelf) continue;
                Mesh mesh = null;
                var skinned = renderer.TryCast<SkinnedMeshRenderer>();
                if (skinned is not null) { mesh = new Mesh(); skinned.BakeMesh(mesh); baked.Add(mesh); }
                else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                var copy = new GameObject("Captured geometry"); copy.layer = 2; copy.transform.SetParent(visual.transform, false);
                copy.transform.localPosition = source.InverseTransformPoint(renderer.transform.position);
                copy.transform.localRotation = Quaternion.Inverse(source.rotation) * renderer.transform.rotation;
                var scale = source.lossyScale; var childScale = renderer.transform.lossyScale;
                copy.transform.localScale = new Vector3(childScale.x / scale.x, childScale.y / scale.y, childScale.z / scale.z);
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var drawn = copy.AddComponent<MeshRenderer>(); drawn.sharedMaterials = renderer.sharedMaterials;
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties); drawn.SetPropertyBlock(properties);
                for (var material = 0; material < renderer.sharedMaterials.Length; material++)
                { renderer.GetPropertyBlock(properties, material); drawn.SetPropertyBlock(properties, material); }
            }
            lightObject = new GameObject("VICE isolated avatar light");
            lightObject.transform.position = targetPosition + new Vector3(-5, 3, 0);
            var light = lightObject.AddComponent<Light>(); light.range = 20; light.intensity = 2.2f; light.cullingMask = 1 << 2;
            cameraObject = new GameObject("VICE isolated avatar camera");
            cameraObject.transform.SetPositionAndRotation(targetPosition + new Vector3(-2.3f, 0.9f, 0), Quaternion.Euler(0, 90, 0));
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 2;
            camera.orthographic = true; camera.orthographicSize = 1.1f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            buffer = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGB32); camera.targetTexture = buffer;
            camera.Render(); RenderTexture.active = buffer;
            texture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); texture.Apply();
            var id = officer.transform.root.gameObject.GetInstanceID();
            if (createdTextures.Remove(id, out var old) && old) UnityEngine.Object.Destroy(old);
            createdTextures[id] = texture; return texture;
        }
        catch { if (texture) UnityEngine.Object.Destroy(texture); throw; }
        finally
        {
            RenderTexture.active = previous;
            if (buffer) RenderTexture.ReleaseTemporary(buffer);
            if (visual) { visual.SetActive(false); UnityEngine.Object.Destroy(visual); }
            if (cameraObject) UnityEngine.Object.Destroy(cameraObject);
            if (lightObject) UnityEngine.Object.Destroy(lightObject);
            foreach (var mesh in baked) UnityEngine.Object.Destroy(mesh);
        }
    }
}
