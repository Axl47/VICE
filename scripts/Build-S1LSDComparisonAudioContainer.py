"""Wrap the authored mixer payload in the game's proven runtime container.

The local editor emits a payload without an AssetBundle object and with a
non-player version suffix. Keep all owned mixer/group/snapshot data intact,
add the container, and use the known-working player serialization metadata.
Requires the local UnityPy library; normal builds use the checked-in bundle.
"""
import copy
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / '.tools/python'))
import UnityPy

output = ROOT / 'src/S1LSDComparison/Assets/comparison-audio.bundle'
authored = UnityPy.load(str(output))
objects = [o for o in authored.objects if o.type.name != 'AssetBundle']
assert len(objects) == 9
assert sorted(o.type.name for o in objects) == sorted([
    'AudioMixerController', 'AudioMixerGroupController', 'AudioMixerSnapshotController'] * 3)
trees = {o.path_id: o.read_typetree() for o in objects}
template = UnityPy.load(str(ROOT / 'src/LSDPreview/Assets/echo.bundle'))
bundle = next(iter(template.files.values()))
container = next(o for o in template.objects if o.type.name == 'AssetBundle')
asset_file = container.assets_file
asset_file._enable_type_tree = False
asset_file.types = [container.serialized_type]
asset_file.externals, asset_file.script_types, asset_file.ref_types = [], [], []
asset_file.userInformation = 'Authored S1LSD comparison mixer data; runtime container'
asset_file.objects = {}

for original in objects:
    type_id = next((i for i, t in enumerate(asset_file.types) if t.class_id == original.serialized_type.class_id), None)
    if type_id is None:
        type_id = len(asset_file.types)
        asset_file.types.append(original.serialized_type)
    owned = copy.copy(original)
    owned.assets_file, owned.type_id = asset_file, type_id
    owned.save_typetree(trees[original.path_id])
    asset_file.objects[owned.path_id] = owned

def ptr(path): return {'m_FileID': 0, 'm_PathID': path}
entries, preloads = [], []
for slot in range(3):
    mixer = next(o for o in objects if trees[o.path_id].get('m_Name') == f'S1LSD Comparison Audio {slot}')
    tree = trees[mixer.path_id]
    paths = [mixer.path_id, tree['m_MasterGroup']['m_PathID'], tree['m_StartSnapshot']['m_PathID']]
    assert all(path in asset_file.objects for path in paths)
    index = len(preloads)
    preloads.extend(ptr(path) for path in paths)
    entries.append((f'assets/comparison/audio{slot}.mixer', {'preloadIndex': index, 'preloadSize': 3, 'asset': ptr(mixer.path_id)}))
    constant = tree['m_MixerConstant']
    assert [e['type'] for e in constant['effects']] == [-2, 17, 6, 12, 3]
    assert len(constant['exposedParameterNames']) == 29

owned_container = copy.copy(container)
owned_container.path_id, owned_container.type_id = 1, 0
owned_container.save_typetree({'m_Name': 's1lsd-comparison-audio', 'm_PreloadTable': preloads, 'm_Container': entries,
    'm_MainAsset': {'preloadIndex': 0, 'preloadSize': 0, 'asset': ptr(0)}, 'm_RuntimeCompatibility': 1,
    'm_AssetBundleName': 's1lsd-comparison-audio', 'm_Dependencies': [], 'm_IsStreamedSceneAssetBundle': False,
    'm_ExplicitDataLayout': 0, 'm_PathFlags': 7, 'm_SceneHashes': []})
asset_file.objects[1] = owned_container
bundle.files = {'CAB-s1lsd-comparison-audio': asset_file}
output.write_bytes(bundle.save(packer='lz4'))
check = UnityPy.load(str(output))
assert len(check.objects) == 10
for obj in check.objects:
    if obj.type.name != 'AssetBundle':
        assert obj.read_typetree() == trees[obj.path_id], 'Authored mixer data changed'
assert len(check.container) == 3
print('PASS: runtime audio container contains three owned mixers; all authored effect data preserved.')
