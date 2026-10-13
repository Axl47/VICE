"""Run: blender -b --python scripts/build_assets.py. Original procedural artwork."""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
P=Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for d in list(bpy.data.materials):bpy.data.materials.remove(d)
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
mats={}; groups={}; current=[]
def mat(name,atlas,rough=.65,metal=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal;p.inputs['Specular IOR Level'].default_value=.12 if name in ['Cloth','PocketBlack'] else .3
 tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(P/'Textures'/atlas),check_existing=True);m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color']);mats[name]=m;return m
for name in ['Wood','Cloth','RailRubber','PocketBlack','Metal','CueWood','CueTip','RackWood']:
 mat(name,'PoolFurniture_BaseColor.png',.75 if name in ['Cloth','PocketBlack'] else .48,.6 if name=='Metal' else 0)
mat('BallAtlas','PoolBalls_BaseColor.png',.23)
for name in ['SisalDark','SisalLight','RingRed','RingGreen','WireMetal','Numbers','MountMetal','DartMetal','DartPlastic','DartFlight']:
 mat(name,'Darts_BaseColor.png',.68,.7 if name in ['WireMetal','MountMetal','DartMetal'] else 0)
def palette(i,u=.5,v=.5):return ((i%4+(.06+.88*u))/4,1-(i//4+(.06+.88*v))/4)
def dartpal(i):return ((24+(i%3)*40)/1024,1-(24+(i//3)*40)/1024)
class Build:
 def __init__(self,name,slots,parent):self.name=name;self.slots=slots;self.parent=parent;self.v=[];self.f=[];self.mi=[];self.uv=[]
 def poly(self,coords,slot=0,uv=None,cell=None,dcell=None):
  k=len(self.v);self.v+=coords;self.f.append(tuple(range(k,k+len(coords))));self.mi.append(slot)
  if uv is None:
   uv=[(0,0),(1,0),(1,1),(0,1)][:len(coords)] if len(coords)<=4 else [(.5+.45*math.cos(i*2*math.pi/len(coords)),.5+.45*math.sin(i*2*math.pi/len(coords))) for i in range(len(coords))]
  if cell is not None:uv=[palette(cell,*p) for p in uv]
  if dcell is not None:uv=[dartpal(dcell)]*len(coords)
  self.uv.append(uv)
 def box(self,lo,hi,slot=0,cell=0):
  a,b,c=lo;d,e,f=hi;v=[(a,b,c),(d,b,c),(d,e,c),(a,e,c),(a,b,f),(d,b,f),(d,e,f),(a,e,f)]
  for ids in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]:self.poly([v[i] for i in ids],slot,cell=cell)
 def cyl(self,center,r,low,high,axis='Y',n=16,slot=0,cell=None,dcell=None,r2=None):
  r2=r if r2 is None else r2
  def pt(angle,rr,t):
   p=(rr*math.cos(angle),t,rr*math.sin(angle)) if axis=='Y' else (rr*math.cos(angle),rr*math.sin(angle),t)
   return tuple(center[j]+p[j] for j in range(3))
  a=[pt(i*2*math.pi/n,r,low) for i in range(n)];b=[pt(i*2*math.pi/n,r2,high) for i in range(n)]
  self.poly(a if axis=='Y' else a[::-1],slot,cell=cell,dcell=dcell);self.poly(b[::-1] if axis=='Y' else b,slot,cell=cell,dcell=dcell)
  for i in range(n):j=(i+1)%n;self.poly(([a[i],a[j],b[j],b[i]][::-1] if axis=='Y' else [a[i],a[j],b[j],b[i]]),slot,cell=cell,dcell=dcell)
 def done(self):
  me=bpy.data.meshes.new(self.name);me.from_pydata(self.v,[],self.f);me.materials.clear()
  for s in self.slots:me.materials.append(mats[s])
  ob=bpy.data.objects.new(self.name,me);bpy.context.collection.objects.link(ob);ob.parent=self.parent
  uv=me.uv_layers.new(name='AtlasUV')
  for p,mi,coords in zip(me.polygons,self.mi,self.uv):
   p.material_index=mi
   for idx,co in zip(p.loop_indices,coords):uv.data[idx].uv=co
  me.update();current.append(ob);return ob
def empty(name,parent=None,pos=(0,0,0)):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;o.location=pos;o.empty_display_size=.06;current.append(o);return o
def begin(name):
 global current;current=[];groups[name]=current
# Pool body
begin('PoolTable');root=empty('PoolTable_Root')
b=Build('Pool_Body',['Wood','Metal'],root);b.box((-.69,.55,-1.25),(.69,.755,1.25),cell=0)
# Raised panel skirts and narrow ochre edge bands.
for sx in (-1,1):
 x=sx*.685;b.box((x-.008,.57,-1.13),(x+.008,.706,1.13),cell=8)
 for z0,z1 in [(-1.11,-.08),(.08,1.11)]:b.box((x-.011,.59,z0),(x+.011,.689,z1),cell=0)
for z in (-1.245,1.245):b.box((-.58,.58,z-.003),(.58,.70,z+.003),cell=8)
b.box((-.694,.735,-1.252),(.694,.75,1.252),1,4);b.done()
# Cloth with six actual edge openings. Boundary walks clockwise in XZ; top triangulation is corrected.
pts=[]
# Trace perimeter with inset circular cutouts using a sampled rectangle/circle union boundary.
# Side boundaries run from corner tangent to side pocket and back, corners are quarter-circle bites.
r=.065
pts.append((-.56+r,.78,-1.12));pts.append((.56-r,.78,-1.12))
for k in range(1,9):a=math.pi-k*math.pi/16;pts.append((.56+r*math.cos(a),.78,-1.12+r*math.sin(a)))
pts.append((.56,.78,-r))
for k in range(1,17):a=-math.pi/2-k*math.pi/16;pts.append((.56+r*math.cos(a),.78,r*math.sin(a)))
pts.append((.56,.78,1.12-r))
for k in range(1,9):a=-math.pi/2-k*math.pi/16;pts.append((.56+r*math.cos(a),.78,1.12+r*math.sin(a)))
pts.append((-.56+r,.78,1.12))
for k in range(1,9):a=-k*math.pi/16;pts.append((-.56+r*math.cos(a),.78,1.12+r*math.sin(a)))
pts.append((-.56,.78,r))
for k in range(1,17):a=math.pi/2-k*math.pi/16;pts.append((-.56+r*math.cos(a),.78,r*math.sin(a)))
pts.append((-.56,.78,-1.12+r))
for k in range(1,9):a=math.pi/2-k*math.pi/16;pts.append((-.56+r*math.cos(a),.78,-1.12+r*math.sin(a)))
b=Build('Pool_Cloth',['Cloth'],root)
for t in tessellate_polygon([[Vector(p) for p in pts]]):
 cs=[pts[p] if isinstance(p,int) else tuple(p) for p in t];b.poly(cs,uv=[palette(1)]*3)
b.done()
# Cushion rubber has exact .09 corner and .18 centre mouth clearances.
b=Build('Pool_Rails',['Wood','RailRubber','Metal'],root)
for s in (-1,1):
 for a,c in [(-1.03,-.09),(.09,1.03)]:
  lo,hi=sorted((s*.592,s*.70));b.box((lo,.78,a),(hi,.858,c),0,0)
  lo,hi=sorted((s*.56,s*.592));b.box((lo,.78,a),(hi,.825,c),1,2)
 for z in [-.78,-.48,-.25,.25,.48,.78]:
  b.poly([(s*.641-.006,.86,z-.011),(s*.641-.006,.86,z+.011),(s*.641+.006,.86,z+.011),(s*.641+.006,.86,z-.011)],2,cell=4)
for s in (-1,1):
 lo,hi=sorted((s*1.152,s*1.26));b.box((-.47,.78,lo),(.47,.858,hi),0,0)
 lo,hi=sorted((s*1.12,s*1.152));b.box((-.47,.78,lo),(.47,.825,hi),1,2)
 for x in [-.28,0,.28]:b.poly([(x-.006,.86,s*1.201-.011),(x-.006,.86,s*1.201+.011),(x+.006,.86,s*1.201+.011),(x+.006,.86,s*1.201-.011)],2,cell=4)
# Outer corner blocks stay outside radius-.065 pocket circle.
for x in (-1,1):
 for z in (-1,1):
  xx=sorted([x*.632,x*.7]);zz=sorted([z*1.04,z*1.26]);b.box((xx[0],.76,zz[0]),(xx[1],.86,zz[1]),2,4)
  xx=sorted([x*.48,x*.632]);zz=sorted([z*1.192,z*1.26]);b.box((xx[0],.76,zz[0]),(xx[1],.86,zz[1]),2,4)
b.done()
b=Build('Pool_Pockets',['PocketBlack','Metal'],root)
for name,x,z in [('NW',-.56,1.12),('NE',.56,1.12),('W',-.56,0),('E',.56,0),('SW',-.56,-1.12),('SE',.56,-1.12)]:
 empty('Pocket_'+name,root,(x,.78,z));b.cyl((x,0,z),.065,.665,.779,n=24,cell=3)
 # Open black rim ring, no cap covering opening.
 for i in range(24):
  aa=i*math.tau/24;bb=(i+1)*math.tau/24
  b.poly([(x+rr*math.cos(t),.785,z+rr*math.sin(t)) for rr,t in [(.065,bb),(.070,bb),(.070,aa),(.065,aa)]],cell=3)
b.done()
b=Build('Pool_Legs',['Wood','Metal'],root)
for x in (-.56,.56):
 for z in (-1.08,1.08):
  b.box((x-.07,.025,z-.07),(x+.07,.56,z+.07),cell=0)
  b.box((x-.078,0,z-.078),(x+.078,.043,z+.078),1,4)
  b.box((x-.075,.485,z-.075),(x+.075,.555,z+.075),cell=8)
b.done()
# Accessories: all separate roots and local-centred balls.
begin('PoolAccessories');root=empty('PoolCue_Root');b=Build('Pool_Cue',['CueWood','CueTip'],root)
b.cyl((0,0,0),.016,-1.45,-1.0,'Z',12,0,8,r2=.014)
b.cyl((0,0,0),.014,-1.0,-.023,'Z',12,0,5,r2=.006)
b.cyl((0,0,0),.006,-.023,-.009,'Z',12,1,6)
b.cyl((0,0,0),.006,-.009,0,'Z',12,1,3);b.done();empty('CueTip',root);empty('CueGrip',root,(0,0,-1.05))
root=empty('PoolRack_Root');b=Build('Pool_Rack',['RackWood'],root)
ri=.30/(2*math.sqrt(3));ro=ri+.01
inner=[(2*ri*math.sin(i*math.tau/3),2*ri*math.cos(i*math.tau/3)) for i in range(3)];outer=[(2*ro*math.sin(i*math.tau/3),2*ro*math.cos(i*math.tau/3)) for i in range(3)]
for i in range(3):j=(i+1)%3
# Four faces per triangle side.
for i in range(3):
 j=(i+1)%3;a=inner[i];c=inner[j];d=outer[i];e=outer[j]
 for coords in [[(a[0],0,a[1]),(c[0],0,c[1]),(c[0],.025,c[1]),(a[0],.025,a[1])],[(d[0],0,d[1]),(d[0],.025,d[1]),(e[0],.025,e[1]),(e[0],0,e[1])],[(a[0],.025,a[1]),(c[0],.025,c[1]),(e[0],.025,e[1]),(d[0],.025,d[1])],[(a[0],0,a[1]),(d[0],0,d[1]),(e[0],0,e[1]),(c[0],0,c[1])]]:b.poly(coords,cell=7)
b.done()
for n in range(16):
 b=Build(f'Pool_Ball_{n:02d}',['BallAtlas'],None);radius=.028575
 def pt(i,j):
  a=i*math.tau/16;t=j*math.pi/10;return (radius*math.sin(t)*math.cos(a),radius*math.cos(t),radius*math.sin(t)*math.sin(a))
 def uv(i,j):return ((n%4+i/16)/4,1-(n//4+j/10)/4)
 for i in range(16):
  b.poly([pt(i,0),pt(i+1,1),pt(i,1)],uv=[uv(i+.5,0),uv(i+1,1),uv(i,1)])
  for j in range(1,9):b.poly([pt(i,j),pt(i+1,j),pt(i+1,j+1),pt(i,j+1)],uv=[uv(i,j),uv(i+1,j),uv(i+1,j+1),uv(i,j+1)])
  b.poly([pt(i,9),pt(i+1,9),pt(i,10)],uv=[uv(i,9),uv(i+1,9),uv(i+.5,10)])
 o=b.done()
 for p in o.data.polygons:p.use_smooth=True
# Darts geometry is filled below after reference inspection.
begin('DartsBoard');root=empty('DartsBoard_Root');empty('BoardCentre',root);empty('BoardFace',root,(0,0,.055))
b=Build('Darts_Backplate',['MountMetal'],root);b.cyl((0,0,0),.26,0,.025,'Z',64,dcell=0);b.done()
b=Build('Darts_Board',['SisalDark','SisalLight','RingRed','RingGreen'],root)
def boarduv(p):return (.5+p[0]/.4608,.5+p[1]/.4608)
def arcpt(r,a,z=.055):return (r*math.sin(a),r*math.cos(a),z)
# Exact scoring boundaries are explicit mesh vertices, four segments per wedge.
bands=[(0,.00635,2),(.00635,.0159,3),(.0159,.099,None),(.099,.107,'colour'),(.107,.162,None),(.162,.170,'colour'),(.170,.225,0)]
for wi in range(20):
 for ri,ro,slot in bands:
  slot=wi%2 if slot is None else (2+wi%2 if slot=='colour' else slot)
  for k in range(4):
   a=math.radians(wi*18-9+k*4.5);c=a+math.radians(4.5)
   pts=[arcpt(ri,a),arcpt(ro,a),arcpt(ro,c),arcpt(ri,c)] if ri else [(0,0,.055),arcpt(ro,a),arcpt(ro,c)]
   b.poly(pts,slot,uv=[boarduv(p) for p in pts])
# Board rear and outer edge.
pts=[arcpt(.225,i*math.tau/80,.025) for i in range(80)];b.poly(pts,0,dcell=0)
for i in range(80):
 a=i*math.tau/80;c=(i+1)*math.tau/80;b.poly([arcpt(.225,a,.025),arcpt(.225,c,.025),arcpt(.225,c),arcpt(.225,a)],0,dcell=0)
b.done()
# Number quads use exact fixed planar atlas projection, with opaque matched background.
b=Build('Darts_NumberRing',['Numbers'],root)
for i in range(20):
 a=i*math.tau/20;x=.197*math.sin(a);y=.197*math.cos(a);w=.018
 pts=[(x-w,y-.010,.057),(x+w,y-.010,.057),(x+w,y+.010,.057),(x-w,y+.010,.057)];b.poly(pts,uv=[boarduv(p) for p in pts])
b.done()
b=Build('Darts_Wire',['WireMetal'],root)
for r in [.00635,.0159,.099,.107,.162,.170,.219]:
 for i in range(80):
  a=i*math.tau/80;c=(i+1)*math.tau/80;b.poly([arcpt(r-.00035,a,.057),arcpt(r+.00035,a,.057),arcpt(r+.00035,c,.057),arcpt(r-.00035,c,.057)],dcell=1)
for i in range(20):
 a=math.radians(i*18-9);dx=.00035*math.cos(a);dy=-.00035*math.sin(a);p=arcpt(.016,a,.057);q=arcpt(.17,a,.057)
 b.poly([(p[0]-dx,p[1]-dy,.057),(p[0]+dx,p[1]+dy,.057),(q[0]+dx,q[1]+dy,.057),(q[0]-dx,q[1]-dy,.057)],dcell=1)
b.done()
b=Build('Darts_Mount',['MountMetal'],root)
# Mount recessed within prescribed bounds and rear plate.
b.cyl((0,.185,0),.008,.002,.015,'Z',12,dcell=1);b.cyl((0,-.185,0),.008,.002,.015,'Z',12,dcell=1);b.done()
begin('Dart');root=empty('Dart_Root');empty('DartGrip',root,(0,0,.06))
b=Build('Dart_Tip',['DartMetal'],root);b.cyl((0,0,0),0,0,.032,'Z',8,dcell=1,r2=.0015);b.done()
b=Build('Dart_Barrel',['DartMetal'],root)
for lo,hi,r1,r2 in [(.032,.039,.002,.004),(.039,.079,.004,.004),(.079,.086,.004,.0025)]:b.cyl((0,0,0),r1,lo,hi,'Z',10,dcell=1,r2=r2)
b.done();b=Build('Dart_Shaft',['DartPlastic'],root);b.cyl((0,0,0),.0025,.086,.128,'Z',8,dcell=2,r2=.0015);b.done()
b=Build('Dart_Flights',['DartFlight'],root)
# Two intersecting thin extruded six-sided vanes, 36mm overall span.
for axis in range(2):
 shape=[(-.001,.116),(-.018,.136),(-.015,.16),(.015,.16),(.018,.136),(.001,.116)]
 def fp(p,h):return (p[0],h,p[1]) if axis==0 else (h,p[0],p[1])
 a=[fp(p,-.0002) for p in shape];c=[fp(p,.0002) for p in shape];b.poly(a[::-1],dcell=5);b.poly(c,dcell=5)
 for i in range(6):j=(i+1)%6;b.poly([a[i],a[j],c[j],c[i]],dcell=5)
b.done()
# Repair winding consistently, preserving intentionally open cloth/wire surfaces.
import bmesh
for os in groups.values():
 for o in os:
  if o.type!='MESH':continue
  bm=bmesh.new();bm.from_mesh(o.data)
  if o.name=='Dart_Barrel':
   bmesh.ops.delete(bm,geom=[f for f in bm.faces if any(all(abs(v.co.z-z)<1e-7 for v in f.verts) for z in [.039,.079])],context='FACES_ONLY')
  
  if o.name not in ['Pool_Body','Pool_Rails','Pool_Legs']:bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
  bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-10);bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES');
  if o.name in ['Pool_Rack','Darts_Board','Dart_Flights'] or o.name.startswith('Pool_Ball_'):bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  bm.to_mesh(o.data);o.data.update();bm.free()
# Open panels get intended outward normals where graph topology cannot infer them.
for name in ['Pool_Cloth','Darts_Board','Darts_NumberRing','Darts_Wire']:
 o=bpy.data.objects[name]
 for poly in o.data.polygons:
  if name=='Pool_Cloth' and poly.normal.y<0:poly.flip()
  elif name!='Pool_Cloth' and all(abs(o.data.vertices[v].co.z-(.055 if name=='Darts_Board' else .057))<1e-7 for v in poly.vertices) and poly.normal.z<0:poly.flip()
# Approved presentation contract: an ordinary Unity camera on +Z sees screen-right at root -X.
# Reflect board artwork only; 6 is root -X and 11 root +X. Scoring stays unchanged.
# Future visual adapter: display (-LastX, LastY, 0.055). This script adds no gameplay code.
for o in groups['DartsBoard']:
 if o.type=='MESH':
  bm=bmesh.new();bm.from_mesh(o.data)
  for v in bm.verts:v.co.x=-v.co.x
  bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(o.data);o.data.update();bm.free()
# Source collections separate all identity/local origins; accessories intentionally overlap at world origin.
for name,os in groups.items():
 col=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(col)
 for o in os:
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o)
# Semantic labels retained in source metadata even when runtime merges identical metal materials.
for name in ['Darts_Backplate','Darts_Mount','Dart_Tip','Dart_Barrel']:
 o=bpy.data.objects[name];o['semantic_material_slots']=[s.name for s in o.data.materials]
manifest={}
for name,os in groups.items():
 manifest[name]={}
 for o in os:
  rec={'type':o.type,'parent':o.parent.name if o.parent else None,'location':list(o.location)}
  if o.type=='MESH':
   o.data.calc_loop_triangles();rec.update(triangles=len(o.data.loop_triangles),materials=[m.name for m in o.data.materials],bounds=[[min(v.co[i] for v in o.data.vertices) for i in range(3)],[max(v.co[i] for v in o.data.vertices) for i in range(3)]])
  manifest[name][o.name]=rec
for im in bpy.data.images:
 if im.source=='FILE':im.filepath='//Textures/'+Path(im.filepath).name
bpy.ops.wm.save_as_mainfile(filepath=str(P/'VICE_HomeGames.blend'))
(P/'validation'/'source_manifest.json').write_text(json.dumps(manifest,indent=2))
# Runtime consolidation authorized by user: source preserves ten exact named materials.
for name in ['Darts_Backplate','Darts_Mount','Dart_Tip','Dart_Barrel']:
 o=bpy.data.objects[name]
 for i,m in enumerate(o.data.materials):
  if m.name in ['MountMetal','DartMetal']:o.data.materials[i]=mats['WireMetal']
# Export-only RH-to-LH bridge. Unity importer reflects FBX X; pre-reflect vertices, pivots, and winding.
for os in groups.values():
 for o in os:
  o.location.x=-o.location.x
  if o.type=='MESH':
   bm=bmesh.new();bm.from_mesh(o.data)
   for v in bm.verts:v.co.x=-v.co.x
   bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
   bmesh.ops.triangulate(bm,faces=list(bm.faces))
   bm.to_mesh(o.data);o.data.update();bm.free()
# Exports carry pre-reflected FBX X, identity transforms and metre metadata; Unity restores canonical signed positions.
for name,os in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in os:o.select_set(True)
 bpy.context.view_layer.objects.active=os[0]
 bpy.ops.export_scene.fbx(filepath=str(P/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',use_space_transform=False,bake_space_transform=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_custom_props=True,use_mesh_modifiers=True,mesh_smooth_type='FACE')
print('SOURCE_AND_EXPORT_COMPLETE')
sys.path.insert(0,str(P/'scripts'))
from sanitize_fbx_paths import sanitize
for name in groups:sanitize(P/(name+'.fbx'))
print('PORTABLE_FBX_PATHS_COMPLETE')
