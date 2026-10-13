"""Replace FBX absolute Texture/Video path strings with portable relative paths.
Uses Blender's bundled lossless FBX parser/encoder, preserving property types.
"""
from io_scene_fbx import parse_fbx,encode_bin
from pathlib import Path
METHODS={'B':'add_bool','C':'add_char','Z':'add_int8','Y':'add_int16','I':'add_int32','L':'add_int64','F':'add_float32','D':'add_float64','R':'add_bytes','S':'add_string','i':'add_int32_array','l':'add_int64_array','f':'add_float32_array','d':'add_float64_array','b':'add_bool_array','c':'add_byte_array'}
def sanitize(path):
 tree,version=parse_fbx.parse(str(path))
 def clone(src):
  dst=encode_bin.FBXElem(src.id)
  for typ,val in zip(src.props_type,src.props):
   if isinstance(val,bytes) and b'/Textures/' in val:val=b'Textures/'+val.replace(b'\\',b'/').split(b'/Textures/')[-1]
   getattr(dst,METHODS[chr(typ)])(val)
  dst.elems=[clone(c) for c in src.elems];return dst
 encode_bin.write(str(path),clone(tree),version)
if __name__=='__main__':
 for p in Path(__file__).resolve().parents[1].glob('*.fbx'):sanitize(p)
