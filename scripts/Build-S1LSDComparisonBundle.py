"""Wrap the supplied S1LSD shader data in a loadable UnityFS asset bundle.

The source release calls a raw serialized shader file a .bundle and has no
AssetBundle object/container. Unity cannot load it with LoadFromMemory. This
script adds only a container, preserving the perception shader tree/bytecode.
Requires the existing local UnityPy library; normal builds use the checked-in
result and do not need Python or Unity.
"""
import copy
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / ".tools/python"))
import UnityPy

assets = ROOT / "src/S1LSDComparison/Assets"
original_path = assets / "s1lsd_shaders.original.bundle"
original = UnityPy.load(str(original_path))
shader = next(o for o in original.objects if o.type.name == "Shader"
              and o.read().m_ParsedForm.m_Name == "Hidden/S1LSD/Perception")
shader_tree = shader.read_typetree()
template = UnityPy.load(str(ROOT / "src/LSDPreview/Assets/peripheral.bundle"))
bundle = next(iter(template.files.values()))
container = next(o for o in template.objects if o.type.name == "AssetBundle")
asset_file = container.assets_file
if asset_file.unity_version != shader.assets_file.unity_version:
    raise RuntimeError("Shader and container schemas have different Unity versions")
asset_file._enable_type_tree = False
asset_file.types = [container.serialized_type, shader.serialized_type]
asset_file.externals = []
asset_file.script_types = []
asset_file.ref_types = []
asset_file.userInformation = "MIT S1LSD perception shader; disposable comparison container"
owned_shader = copy.copy(shader)
owned_shader.assets_file = asset_file
owned_shader.path_id, owned_shader.type_id = 2, 1
owned_shader.save_typetree(shader_tree)
owned_container = copy.copy(container)
owned_container.path_id, owned_container.type_id = 1, 0
reference = {"m_FileID": 0, "m_PathID": 2}
owned_container.save_typetree({
    "m_Name": "personal-s1lsd-comparison", "m_PreloadTable": [reference],
    "m_Container": [("assets/s1lsd/perception.shader", {"preloadIndex": 0, "preloadSize": 1, "asset": reference})],
    "m_MainAsset": {"preloadIndex": 0, "preloadSize": 0, "asset": {"m_FileID": 0, "m_PathID": 0}},
    "m_RuntimeCompatibility": 1, "m_AssetBundleName": "personal-s1lsd-comparison",
    "m_Dependencies": [], "m_IsStreamedSceneAssetBundle": False,
    "m_ExplicitDataLayout": 0, "m_PathFlags": 7, "m_SceneHashes": []
})
asset_file.objects = {1: owned_container, 2: owned_shader}
bundle.files = {"CAB-personal-s1lsd-comparison": asset_file}
output = assets / "s1lsd_shaders.bundle"
output.write_bytes(bundle.save(packer="lz4"))
check = UnityPy.load(str(output))
assert sorted(o.type.name for o in check.objects) == ["AssetBundle", "Shader"]
adapted = next(o.read_typetree() for o in check.objects if o.type.name == "Shader")
assert adapted == shader_tree, "Original shader tree or executable programs changed"
manifest_path = assets / "provenance.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
manifest.update({
    "originalShaderBundleSha256": hashlib.sha256(original_path.read_bytes()).hexdigest(),
    "shaderBundleSha256": hashlib.sha256(output.read_bytes()).hexdigest(),
    "perceptionProgramBlobSha256": hashlib.sha256(bytes(adapted["compressedBlob"])).hexdigest(),
    "unityVersion": asset_file.unity_version,
    "bundleAdaptation": "Added a UnityFS AssetBundle/container to the raw supplied shader file. The complete perception shader tree, including compiled programs, is unchanged."
})
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Built {output.name}: {output.stat().st_size:,} bytes; original perception shader preserved exactly.")
