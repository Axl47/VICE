"""Build owned Echo mixers from the game's Unity runtime serialization schema.

Requires UnityPy. Contains only new mixer/group/snapshot data, with no music.
Echo's type and parameter order match the existing Police chatter mixer.
"""
import argparse
import copy
import hashlib
import json
import pathlib
import struct
import sys
import zlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / '.tools/python'))
import UnityPy

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game', default=r'C:\Program Files (x86)\Steam\steamapps\common\Schedule I')
parser.add_argument('--container-template', type=pathlib.Path, default=ROOT / '.research/assets/moreguns.bundle')
args = parser.parse_args()
data = pathlib.Path(args.game) / 'Schedule I_Data'
game = UnityPy.load(str(data / 'sharedassets0.assets'))
source = next(o for o in game.objects if o.type.name == 'AudioMixerController' and o.read_typetree()['m_Name'] == 'Game audio mixer')
tree = source.read_typetree()
group = source.assets_file.objects[tree['m_MasterGroup']['m_PathID']]
snapshot = source.assets_file.objects[tree['m_StartSnapshot']['m_PathID']]
police = UnityPy.load(str(data / 'sharedassets1.assets'))
police_tree = next(o.read_typetree() for o in police.objects if o.type.name == 'AudioMixerController' and o.read_typetree()['m_Name'] == 'Police chatter mixer')
native_echo = next(e for e in police_tree['m_MixerConstant']['effects'] if e['type'] == 6)
assert len(native_echo['parameterIndices']) == 5

template = UnityPy.load(str(args.container_template))
bundle = next(iter(template.files.values()))
container = next(o for o in template.objects if o.type.name == 'AssetBundle')
asset_file = container.assets_file
asset_file.unity_version = source.assets_file.unity_version
asset_file._enable_type_tree = False
asset_file.types = [container.serialized_type, source.serialized_type, group.serialized_type, snapshot.serialized_type]
asset_file.externals, asset_file.script_types, asset_file.ref_types = [], [], []
asset_file.userInformation = 'Authored Personal LSD Echo mixers'
asset_file.objects = {}

def ptr(path): return {'m_FileID': 0, 'm_PathID': path}
def guid(name):
    words = struct.unpack('<IIII', hashlib.sha256(name.encode()).digest()[:16])
    return {f'data[{i}]': word for i, word in enumerate(words)}
def owned_object(original, path, type_id, value):
    owned = copy.copy(original)
    owned.assets_file, owned.path_id, owned.type_id = asset_file, path, type_id
    owned.save_typetree(value)
    asset_file.objects[path] = owned

preloads, entries = [], []
for slot in range(4):
    mixer_id, group_id, snapshot_id = 2 + slot * 3, 3 + slot * 3, 4 + slot * 3
    name = f'Personal LSD Echo {slot}'
    value = copy.deepcopy(tree)
    value.update(m_Name=name, m_OutputGroup=ptr(0), m_MasterGroup=ptr(group_id), m_Snapshots=[ptr(snapshot_id)],
        m_StartSnapshot=ptr(snapshot_id), m_EnableSuspend=False, m_UpdateMode=1)
    constant = value['m_MixerConstant']
    constant['groupGUIDs'] = [guid(name + '/Master')]
    echo = copy.deepcopy(native_echo)
    echo.update(groupConstantIndex=0, prevEffectIndex=0, parameterIndices=list(range(2, 7)), bypass=False)
    constant['effects'] = [constant['effects'][0], echo]
    constant['effectGUIDs'] = [guid(name + '/Attenuation'), guid(name + '/Echo')]
    constant['snapshots'] = [{'nameHash': zlib.crc32(b'Default'),
        'values': [0.0, 1.0, 220.0, 0.18, 0.0, 1.0, 0.0], 'transitionTypes': [], 'transitionIndices': []}]
    constant['snapshotGUIDs'] = [guid(name + '/Default')]
    constant['groupNameBuffer'] = list(b'Master\0\0')
    constant['snapshotNameBuffer'] = list(b'Default\0')
    constant['exposedParameterNames'] = [zlib.crc32(n.encode()) for n in ('EchoDelay', 'EchoDecay', 'EchoDry', 'EchoWet')]
    constant['exposedParameterIndices'] = [2, 3, 5, 6]
    constant['pluginEffectNameBuffer'] = [0]
    owned_object(source, mixer_id, 1, value)
    owned_object(group, group_id, 2, {'m_Name': 'Master', 'm_AudioMixer': ptr(mixer_id), 'm_GroupID': constant['groupGUIDs'][0], 'm_Children': []})
    owned_object(snapshot, snapshot_id, 3, {'m_Name': 'Default', 'm_AudioMixer': ptr(mixer_id), 'm_SnapshotID': constant['snapshotGUIDs'][0]})
    index = len(preloads)
    preloads.extend([ptr(mixer_id), ptr(group_id), ptr(snapshot_id)])
    entries.append((f'assets/personal/lsdecho{slot}.mixer', {'preloadIndex': index, 'preloadSize': 3, 'asset': ptr(mixer_id)}))
owned_object(container, 1, 0, {'m_Name': 'personal-lsd-echo', 'm_PreloadTable': preloads, 'm_Container': entries,
    'm_MainAsset': {'preloadIndex': 0, 'preloadSize': 0, 'asset': ptr(0)}, 'm_RuntimeCompatibility': 1,
    'm_AssetBundleName': 'personal-lsd-echo', 'm_Dependencies': [], 'm_IsStreamedSceneAssetBundle': False,
    'm_ExplicitDataLayout': 0, 'm_PathFlags': 7, 'm_SceneHashes': []})
bundle.files = {'CAB-personal-lsd-echo': asset_file}
output = ROOT / 'src/LSDPreview/Assets/echo.bundle'
output.write_bytes(bundle.save(packer='lz4'))
check = UnityPy.load(str(output))
assert len(check.objects) == 13
assert sorted(o.type.name for o in check.objects) == sorted(['AssetBundle'] + ['AudioMixerController', 'AudioMixerGroupController', 'AudioMixerSnapshotController'] * 4)
for obj in check.objects:
    if obj.type.name != 'AudioMixerController': continue
    assert obj.read_typetree()['m_MixerConstant']['snapshots'][0]['values'] == [0.0, 1.0, 220.0, 0.18000000715255737, 0.0, 1.0, 0.0]
manifest = {'bundleSha256': hashlib.sha256(output.read_bytes()).hexdigest(),
    'generatorSha256': hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),
    'mixers': 4, 'echoType': 6, 'parameters': ['DelayMs', 'DecayRatio', 'MaxChannels', 'DryMix', 'WetMix'],
    'defaults': {'delayMs': 220, 'decay': 0.18, 'dry': 1, 'wet': 0}, 'containsAudioClips': False}
(output.parent / 'echo.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
print(json.dumps(manifest, indent=2))
