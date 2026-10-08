"""Render the authored meshes for review; these are Blender renders, not game screenshots."""
import bpy
import sys
from mathutils import Vector
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/images/lsd-studio'
OUT.mkdir(parents=True,exist_ok=True)

def artwork():
    # Illustrative edition only: runtime supplies the player's immutable artwork.
    tex=bpy.data.images.new('Illustrative preview edition',width=64,height=64)
    colors=[(.86,.79,.59,1),(.14,.35,.39,1),(.91,.44,.28,1),(.06,.09,.10,1)]
    tex.pixels=[v for y in range(64) for x in range(64)
                for v in colors[((x//8)+(y//8)*3)%4]]
    for mat in bpy.data.materials:
        if mat.name.startswith('Artwork'):
            mat.use_nodes=True
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=tex;node.interpolation='Closest'
            mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])

def setup(scene,centre,span,path,camera_offset=(2,-3,2.1),samples=128):
    bpy.ops.object.camera_add(location=centre+Vector(camera_offset)*span)
    camera=bpy.context.object;camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=span*1.7;scene.camera=camera
    for offset,power,size in [((1,-2,3),180,2),((-2,-1,1.3),100,2),((0,2,2),180,1.5)]:
        light_offset=Vector((offset[0],offset[1],offset[2] if camera_offset[2]>0 else -offset[2]))
        bpy.ops.object.light_add(type='AREA',location=centre+light_offset*span)
        light=bpy.context.object;light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler();light.data.energy=power*span*span;light.data.shape='DISK';light.data.size=size*span
    if scene.world is None:scene.world=bpy.data.worlds.new('Review world')
    scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.20,.25,.28,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=samples;scene.cycles.use_denoising=False;scene.cycles.seed=0
    scene.render.resolution_x=960;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='JPEG';scene.render.image_settings.quality=92;scene.render.image_settings.color_mode='RGB';scene.render.filepath=str(path)
    scene.view_settings.view_transform='AgX'
    bpy.ops.render.render(write_still=True)

def ground(z=-.001):
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,z));o=bpy.context.object
    mat=bpy.data.materials.new('Review ground');mat.diffuse_color=(.095,.13,.15,1);o.data.materials.append(mat)

def one(name,offset=(2,-3,2.1)):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/LSDStudio/Blender'/f'{name}.blend'))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)])
    artwork()
    if offset[2]>0: ground(lo.z-.001)
    setup(bpy.context.scene,(lo+hi)/2,max(hi-lo),OUT/f'{name.lower()}.jpg',offset)

selected=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
if not selected or 'Bench' in selected: one('Bench',(1.5,-2.5,2.3))
if not selected or 'Cabinet' in selected: one('Cabinet',(1.4,-3,1.2))
if not selected or 'Parcel' in selected: one('Parcel',(1.6,-2.8,2))
# Envelope back is shown because the front is deliberately reserved for edition art.
if not selected or 'Packet' in selected: one('Packet',(1.2,-1.8,-2.2))
# Knife close-up directly from the bench, preserving the actual animated geometry.
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'assets/LSDStudio/Blender/Bench.blend'))
for o in bpy.context.scene.objects:o.hide_render=o.name not in ('CutterArm','CutterGrip')
centre=Vector((.58,-.242,.967));setup(bpy.context.scene,centre,.19,OUT/'knife.jpg',(1.2,-2,3),64)
print('LSD_STUDIO_REVIEW_RENDERED '+str(OUT))
