"""Compile our HLSL and pack it in a minimal UnityFS shader bundle.

Requires Windows d3dcompiler_47, UnityPy and lz4. The game shader supplies only
the Unity serialization/binding layout; every executable shader is replaced
with code compiled from PeripheralWarp.hlsl. A locally prepared asset bundle
supplies the AssetBundle container type. No game textures or meshes are packed.
"""
import argparse
import copy
import ctypes
import hashlib
import json
import pathlib
import struct
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / ".tools/python"))
import UnityPy
import lz4.block
from UnityPy.export.ShaderConverter import ShaderProgram
from UnityPy.streams import EndianBinaryReader

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--game", default=r"C:\Program Files (x86)\Steam\steamapps\common\Schedule I")
parser.add_argument("--container-template", type=pathlib.Path, default=ROOT / ".research/assets/moreguns.bundle")
args = parser.parse_args()

compiler = ctypes.WinDLL("d3dcompiler_47.dll")
compiler.D3DCompile.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_char_p,
    ctypes.c_void_p, ctypes.c_void_p, ctypes.c_char_p, ctypes.c_char_p,
    ctypes.c_uint, ctypes.c_uint, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_void_p)]
compiler.D3DCompile.restype = ctypes.c_long

def consume_blob(pointer):
    table = ctypes.cast(pointer, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    buffer = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(table[3])(pointer)
    size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(table[4])(pointer)
    result = ctypes.string_at(buffer, size)
    ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(table[2])(pointer)
    return result

source = (ROOT / "src/LSDPreview/Assets/PeripheralWarp.hlsl").read_bytes()
def compile_shader(entry, profile):
    code, errors = ctypes.c_void_p(), ctypes.c_void_p()
    status = compiler.D3DCompile(source, len(source), b"PeripheralWarp.hlsl", None, None,
        entry.encode(), profile.encode(), 1 << 15, 0, ctypes.byref(code), ctypes.byref(errors))
    diagnostic = consume_blob(errors).decode() if errors else ""
    if status < 0:
        raise RuntimeError(diagnostic)
    return consume_blob(code)

vertex = compile_shader("Vert", "vs_4_0")
fragment = compile_shader("Frag", "ps_4_0")
game = UnityPy.load(str(pathlib.Path(args.game) / "Schedule I_Data/globalgamemanagers.assets"))
shader_object = next(o for o in game.objects if o.type.name == "Shader" and
    o.read().m_ParsedForm.m_Name == "Shader Graphs/PsychedelicDistortionEffect")
shader = shader_object.read_typetree()
blob = lz4.block.decompress(bytes(shader["compressedBlob"]), uncompressed_size=shader["decompressedLengths"][0][0])
program = ShaderProgram(EndianBinaryReader(blob, endian="<"), shader_object.assets_file.version)
count = struct.unpack_from("<I", blob)[0]
entries = [struct.unpack_from("<III", blob, 4 + i * 12) for i in range(count)]
subprograms = []
for i, (offset, length, segment) in enumerate(entries):
    if segment != 0:
        raise RuntimeError("Unexpected shader program segment")
    old = blob[offset:offset + length]
    code = bytes(program.m_SubPrograms[i].m_ProgramCode)
    # The parameter programs contain binding metadata, not executable DXBC.
    if program.m_SubPrograms[i].m_ProgramType in (15, 17):
        marker = code.find(b"DXBC")
        if marker != 38:
            raise RuntimeError("Unexpected Direct3D platform header")
        replacement = code[:marker] + (vertex if program.m_SubPrograms[i].m_ProgramType == 15 else fragment)
        location = old.find(struct.pack("<I", len(code)) + code)
        if location < 0:
            raise RuntimeError("Program code was not found in its wrapper")
        end = location + 4 + len(code)
        aligned_end = (end + 3) & ~3
        new = old[:location] + struct.pack("<I", len(replacement)) + replacement
        new += b"\0" * ((-len(new)) & 3) + old[aligned_end:]
        subprograms.append(new)
    else:
        subprograms.append(old)
header_size = 4 + count * 12
rebuilt = bytearray(struct.pack("<I", count) + b"\0" * (count * 12))
for i, code in enumerate(subprograms):
    offset = len(rebuilt)
    rebuilt.extend(code)
    struct.pack_into("<III", rebuilt, 4 + i * 12, offset, len(code), 0)
shader["compressedBlob"] = list(lz4.block.compress(bytes(rebuilt), store_size=False))
shader["offsets"] = [[0]]
shader["compressedLengths"] = [[len(shader["compressedBlob"])]]
shader["decompressedLengths"] = [[len(rebuilt)]]
shader["m_Name"] = shader["m_ParsedForm"]["m_Name"] = "Personal/LSDPeripheralWarp"
shader["m_ParsedForm"]["m_FallbackName"] = ""
shader["m_ParsedForm"]["m_CustomEditorName"] = ""
shader["m_Dependencies"] = []
for subshader in shader["m_ParsedForm"]["m_SubShaders"]:
    for shader_pass in subshader["m_Passes"]:
        shader_pass["m_NameIndices"] = [("_BlitTexture" if name == "_GrabPassTransparent" else name, index)
            for name, index in shader_pass["m_NameIndices"]]

template = UnityPy.load(str(args.container_template))
bundle = next(iter(template.files.values()))
container = next(o for o in template.objects if o.type.name == "AssetBundle")
asset_file = container.assets_file
asset_file.unity_version = shader_object.assets_file.unity_version
asset_file._enable_type_tree = False
asset_file.types = [container.serialized_type, shader_object.serialized_type]
asset_file.externals = []
asset_file.script_types = []
asset_file.ref_types = []
asset_file.userInformation = "Authored Personal LSD peripheral shader"
owned_shader = copy.copy(shader_object)
owned_shader.assets_file = asset_file
owned_shader.path_id, owned_shader.type_id = 2, 1
owned_shader.save_typetree(shader)
owned_container = copy.copy(container)
owned_container.path_id, owned_container.type_id = 1, 0
reference = {"m_FileID": 0, "m_PathID": 2}
container_tree = {"m_Name": "personal-lsd-peripheral", "m_PreloadTable": [reference],
    "m_Container": [("assets/personal/lsdperipheral.shader", {"preloadIndex": 0, "preloadSize": 1, "asset": reference})],
    "m_MainAsset": {"preloadIndex": 0, "preloadSize": 0, "asset": {"m_FileID": 0, "m_PathID": 0}},
    "m_RuntimeCompatibility": 1, "m_AssetBundleName": "personal-lsd-peripheral", "m_Dependencies": [],
    "m_IsStreamedSceneAssetBundle": False, "m_ExplicitDataLayout": 0, "m_PathFlags": 7, "m_SceneHashes": []}
owned_container.save_typetree(container_tree)
asset_file.objects = {1: owned_container, 2: owned_shader}
bundle.files = {"CAB-personal-lsd-peripheral": asset_file}
output = ROOT / "src/LSDPreview/Assets/peripheral.bundle"
output.write_bytes(bundle.save(packer="lz4"))
check = UnityPy.load(str(output))
assert sorted(o.type.name for o in check.objects) == ["AssetBundle", "Shader"]
result = next(o.read_typetree() for o in check.objects if o.type.name == "Shader")
packed = lz4.block.decompress(bytes(result["compressedBlob"]), uncompressed_size=result["decompressedLengths"][0][0])
actual = ShaderProgram(EndianBinaryReader(packed, endian="<"), shader_object.assets_file.version)
for sub in actual.m_SubPrograms:
    if sub.m_ProgramType in (15, 17):
        assert bytes(sub.m_ProgramCode)[38:] == (vertex if sub.m_ProgramType == 15 else fragment)
(output.parent / "peripheral.json").write_text(json.dumps({
    "sourceSha256": hashlib.sha256(source).hexdigest(),
    "bundleSha256": hashlib.sha256(output.read_bytes()).hexdigest(),
    "vertexSha256": hashlib.sha256(vertex).hexdigest(),
    "fragmentSha256": hashlib.sha256(fragment).hexdigest(),
    "shader": "Personal/LSDPeripheralWarp", "graphicsApi": "Direct3D11",
    "unityVersion": asset_file.unity_version, "objectCount": 2,
}, indent=2) + "\n", encoding="utf-8")
print(f"Built {output.name}: {output.stat().st_size:,} bytes, two objects, all executable shaders authored.")
