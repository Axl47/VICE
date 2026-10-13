"""Offline RH Blender authoring previews. These do not certify Unity LH-camera glyph orientation; authoritative Unity review images live separately in Renders/."""
import bpy,math,sys
from pathlib import Path
from mathutils import Vector,Matrix
P=Path(__file__).resolve().parents[1];(P/'SourceAuthoringRenders').mkdir(exist_ok=True);bpy.ops.wm.open_mainfile(filepath=str(P/'VICE_HomeGames.blend'))
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=64;s.cycles.use_denoising=False;s.render.resolution_x=1400;s.render.resolution_y=1100;s.render.resolution_percentage=100
s.world.color=(.22,.22,.22);s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.4
s.render.image_settings.file_format='PNG';s.render.film_transparent=False
# Explicit environment; Blender world remains Y-up for contract-space artwork.
world=bpy.data.worlds.new('ReviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.24,.26,.25,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;s.world=world
mat=bpy.data.materials.new('ReviewBackdrop');mat.diffuse_color=(.13,.15,.145,1);mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.13,.15,.145,1);mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.86
me=bpy.data.meshes.new('ReviewGround');me.from_pydata([(-200,-.006,-200),(-200,-.006,200),(200,-.006,200),(200,-.006,-200)],[],[(0,1,2,3)]);ground=bpy.data.objects.new('ReviewGround',me);s.collection.objects.link(ground);me.materials.append(mat)
camd=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',camd);s.collection.objects.link(cam);s.camera=cam
lamps=[]
def light(name,pos,power,size):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,.5,0))-o.location).to_track_quat('-Z','Y').to_euler();lamps.append(o)
light('ReviewKey',(1.3,4.1,-1.7),450,4);light('ReviewFill',(-2.8,2,1.8),270,3);light('ReviewRim',(1,3.1,3.6),320,2)
def visible(names):
 for name in ['PoolTable','PoolAccessories','DartsBoard','Dart']:
  bpy.data.collections[name].hide_render=name not in names

def render(name,pos,target,ortho=None):
 if '--' in sys.argv and name not in sys.argv[sys.argv.index('--')+1:]:return
 cam.location=pos;f=(Vector(target)-cam.location).normalized();up=Vector((0,0,1)) if abs(f.y)>.999 else Vector((0,1,0));r=f.cross(up).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();camd.type='ORTHO' if ortho else 'PERSP';camd.ortho_scale=ortho or 4;camd.lens=48;s.render.filepath=str(P/'SourceAuthoringRenders'/name);bpy.ops.render.render(write_still=True)
# Position original display objects only in this unsaved scene.
visible(['PoolTable','PoolAccessories'])
for n in range(16):
 o=bpy.data.objects[f'Pool_Ball_{n:02d}'];o.location=(0,.808575,-.60) if n==0 else (((n-1)%5-2)*.09,.808575,.35+((n-1)//5)*.09);o.rotation_euler[0]=math.pi/2
cue=bpy.data.objects['PoolCue_Root'];cue.location=(.48,.89,-.20);cue.rotation_euler=(0,.2,.02)
rack=bpy.data.objects['PoolRack_Root'];rack.location=(0,.781,.70)
render('Pool_Isometric.png',(3.1,2.6,-3.6),(0,.48,0),3.65)
render('Pool_PlayerView.png',(0,1.24,-2.45),(0,.80,.22),None)
visible(['PoolTable']);render('Pool_Top_Ortho.png',(0,4,0),(0,.4,0.0001),3.5)
render('Pool_Side_Ortho.png',(3,.43,0),(0,.43,0),2.9)
# Accessories, laid out so labels/numbers can be inspected.
visible(['PoolAccessories']);ground.hide_render=True
for n in range(16):
 o=bpy.data.objects[f'Pool_Ball_{n:02d}'];o.location=(((n%8)-3.5)*.095,.06,(n//8)*.12-.03);o.rotation_euler=(math.pi/2,math.pi,0)
cue.location=(.66,.03,.28);cue.rotation_euler=(0,math.pi/2,0);rack.location=(0,.015,.51)
render('Pool_Accessories.png',(.0,1.7,.40),(0,0,.28),1.7)
# Two actual UV hemispheres, straight-on and upright, for every numbered ball.
visible(['PoolAccessories']);ground.hide_render=True
for o in bpy.data.collections['PoolAccessories'].objects:
 if o.type=='MESH' and not o.name.startswith('Pool_Ball_'):o.hide_render=True
for n in range(16):
 o=bpy.data.objects[f'Pool_Ball_{n:02d}'];o.location=(((n%8)-3.5)*.091,.15-(n//8)*.09,0);o.rotation_euler=(0,0,0)
 rear=o.copy();rear.data=o.data;bpy.data.collections['PoolAccessories'].objects.link(rear);rear.location=(((n%8)-3.5)*.091,-.08-(n//8)*.09,0);rear.rotation_euler=(0,math.pi,0)
render('Ball_Hemispheres.png',(0,0,2),(0,0,0),.84)
# Board is vertical in XY, front +Z. Rear-ground replaced with wall.
visible(['DartsBoard']);ground.hide_render=True
for o in lamps:
 o.location=Vector((o.location.x,o.location.z,o.location.y));o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
render('Darts_Front_Ortho.png',(0,0,2),(0,0,.025),.70)
render('Darts_Oblique.png',(.65,.30,1.1),(0,0,.025),.62)
visible(['Dart']);dart=bpy.data.objects['Dart_Root'];dart.rotation_euler=(0,0,0);dart.location=(0,0,0)
render('Dart_Detail.png',(.22,.16,.18),(0,0,.08),.195)
print('RENDERS_COMPLETE')
