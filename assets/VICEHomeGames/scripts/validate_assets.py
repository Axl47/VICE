"""Independent binary FBX + native Blender contract checks.
Run: blender -b --factory-startup --python scripts/validate_assets.py -- [asset_dir]
Includes a real Blender FBX import round-trip. No Unity importer is available;
this does not claim a Unity editor round-trip.
"""
import bpy, sys, os, json, math, re
from pathlib import Path
from mathutils import Matrix, Vector, Euler
from io_scene_fbx import parse_fbx
BASE=Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv and len(sys.argv)>sys.argv.index('--')+1 else Path(__file__).resolve().parents[1]
REPORT={'method':'Independent Blender 4.3 binary FBX parser plus actual Blender FBX importer round-trip, normalizing Blender Z-up import coordinates back to declared Y-up FBX frame. Physical dimensions calculated from node matrices and UnitScaleFactor. Native blend identity transforms checked separately. No Unity editor round-trip performed.','checks':[],'files':{},'notes':['A 0.30 m inside rack is decorative; a five-row rack of 0.05715 m balls requires approximately 0.328787 m inside side length.','Ten requested board/dart logical material names exceed the eight assembled material cap; README must document the consolidation mapping.']}
def check(label,ok,detail=None):
 REPORT['checks'].append({'check':label,'pass':bool(ok),'detail':detail})
def near(a,b,tol=2e-5):return abs(a-b)<=tol
def vecnear(a,b,tol=2e-5):return len(a)==len(b) and all(near(x,y,tol) for x,y in zip(a,b))
def child(e,key):return next((x for x in e.elems if x.id==key),None)
def prop(e,key,default=None):
 p=child(e,b'Properties70')
 if p:
  x=next((x for x in p.elems if x.props[0]==key),None)
  if x:return x.props[4:] if len(x.props)>5 else x.props[4]
 return default
def val(e,key,default=None):
 c=child(e,key);return c.props[0] if c else default
def name(e):return e.props[1].split(b'\x00')[0].decode()
def bounds(vs):return [[min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)]] if vs else None
def dims(bb):return [bb[1][i]-bb[0][i] for i in range(3)]
def read_fbx(fn):
 tree,version=parse_fbx.parse(str(BASE/fn)); objects=child(tree,b'Objects'); cs=child(tree,b'Connections')
 objs={e.props[0]:e for e in objects.elems}; models={i:e for i,e in objs.items() if e.id==b'Model'}
 con=[e.props for e in cs.elems if e.id==b'C']; parents={c[1]:c[2] for c in con if c[0]==b'OO' and c[1] in models}
 gs=child(tree,b'GlobalSettings'); unit=prop(gs,b'UnitScaleFactor',1)/100
 axes={k.decode():prop(gs,k) for k in [b'UpAxis',b'UpAxisSign',b'FrontAxis',b'FrontAxisSign',b'CoordAxis',b'CoordAxisSign']}
 check(fn+' Y-up axis metadata',axes=={'UpAxis':1,'UpAxisSign':1,'FrontAxis':2,'FrontAxisSign':1,'CoordAxis':0,'CoordAxisSign':1},axes)
 def transform(i):
  m=models[i];t=prop(m,b'Lcl Translation',[0,0,0]);r=prop(m,b'Lcl Rotation',[0,0,0]);s=prop(m,b'Lcl Scaling',[1,1,1]);
  local=Matrix.LocRotScale(Vector(t),Euler([math.radians(v) for v in r],'XYZ').to_quaternion(),Vector(s))
  return transform(parents[i])@local if parents.get(i) in models else local
 result={};mats={}
 for i,m in models.items():
  n=name(m);t=prop(m,b'Lcl Translation',[0,0,0]);r=prop(m,b'Lcl Rotation',[0,0,0]);s=prop(m,b'Lcl Scaling',[1,1,1]);
  check(fn+' '+n+' applied rotation/scale',vecnear(r,[0,0,0]) and vecnear(s,[1,1,1]),{'rotation':r,'scale':s})
  geos=[objs[c[1]] for c in con if c[0]==b'OO' and c[2]==i and c[1] in objs and objs[c[1]].id==b'Geometry']
  material=[name(objs[c[1]]) for c in con if c[0]==b'OO' and c[2]==i and c[1] in objs and objs[c[1]].id==b'Material']
  w=transform(i);verts=[];faces=[];tri=0
  for g in geos:
   a=val(g,b'Vertices',[]);vs=[tuple(a[j:j+3]) for j in range(0,len(a),3)];verts += [tuple((w@Vector(v))*unit) for v in vs];face=[]
   for index in val(g,b'PolygonVertexIndex',[]):
    face.append(-index-1 if index<0 else index)
    if index<0: faces.append(face);tri+=len(face)-2;face=[]
   check(fn+' '+n+' UV map',child(g,b'LayerElementUV') is not None)
  result[n]={'type':m.props[2].decode(),'parent':name(models[parents[i]]) if parents.get(i) in models else None,'translation':list(t),'world_origin':list(w.translation*unit),'triangles':tri,'bounds':bounds(verts),'materials':material,'vertices':verts,'faces':faces}
 check(fn+' no animation or rig',not any(e.id in (b'AnimationStack',b'AnimationLayer',b'Deformer') for e in objs.values()))
 check(fn+' mesh/empty types only',all(d['type'] in ('Mesh','Null') for d in result.values()))
 # Recursively inspect all serialized filename variants, including legacy absolute fields.
 def filename_checks(element):
  if element.id.lower() in (b'filename',b'relativefilename'):
   for value in element.props:
    if isinstance(value,bytes):
     path=value.decode(errors='replace')
     absolute=path.startswith(('/',chr(92))) or bool(re.match(r'^[A-Za-z]:',path)) or '://' in path
     check(fn+' all filename fields relative '+element.id.decode(),not absolute,path)
  for sub in element.elems:filename_checks(sub)
 filename_checks(tree)
 # Verify relative texture references and no embedded payload.
 for e in objs.values():
  if e.id in (b'Texture',b'Video'):
   rel=val(e,b'RelativeFilename',b'').decode(errors='replace');absolute=val(e,b'FileName',b'').decode(errors='replace')
   check(fn+' external relative texture '+name(e),bool(rel) and not os.path.isabs(rel) and not (len(rel)>1 and rel[1]==':'),{'relative':rel,'FileName':absolute})
   check(fn+' no embedded texture '+name(e),child(e,b'Content') is None)
 REPORT['files'][fn]={'fbx_version':version,'unit_metres':unit,'axes':axes,'objects':{n:{k:v for k,v in d.items() if k not in ('vertices','faces')} for n,d in result.items()}}
 return result

def required(data,names,label):check(label+' exact names',set(data)==set(names),{'missing':sorted(set(names)-set(data)),'extra':sorted(set(data)-set(names))})
def bbcheck(data,n,lo,hi):
 if n in data:check(n+' exact bounds',vecnear(data[n]['bounds'][0],lo) and vecnear(data[n]['bounds'][1],hi),data[n]['bounds'])
def empty(data,n,xyz,parent=None):
 if n in data:check(n+' pivot/empty',data[n]['type']=='Null' and vecnear(data[n]['world_origin'],xyz) and (parent is None or data[n]['parent']==parent),data[n]['world_origin'])
def meshparents(data,root,names):
 for n in names:
  if n in data:check(n+' mesh parent',data[n]['type']=='Mesh' and data[n]['parent']==root,data[n]['parent'])
def total(data,names,limit,label):
 count=sum(data[n]['triangles'] for n in names if n in data);check(label+' triangle budget',count<=limit,{'triangles':count,'maximum':limit})
def ball_uv_checks(obj,label,coordinate_matrix=None):
 # On +Z-facing number island, increasing U must run +X. Opposite hemisphere
 # must run -X. Increasing V must run +Y on both sides, ensuring upright digits.
 obj.data.calc_loop_triangles();uv=obj.data.uv_layers.active
 for sign in [1,-1]:
  tested=[]
  for tri in obj.data.loop_triangles:
   ps=[(coordinate_matrix@obj.data.vertices[i].co if coordinate_matrix is not None else obj.data.vertices[i].co) for i in tri.vertices];center=sum(ps,Vector())/3
   if sign*center.z<.023 or abs(center.y)>.009:continue
   us=[uv.data[i].uv for i in tri.loops];a=us[1]-us[0];b=us[2]-us[0];det=a.x*b.y-b.x*a.y
   if abs(det)<1e-12:continue
   pu=((ps[1]-ps[0])*b.y-(ps[2]-ps[0])*a.y)/det
   pv=((ps[2]-ps[0])*a.x-(ps[1]-ps[0])*b.x)/det
   tested.append(pu.x*sign>0 and pv.y>0)
  check(label+' number hemisphere '+str(sign)+' upright/unmirrored UV',len(tested)>0 and all(tested),{'sample_triangles':len(tested)})

def importer_roundtrip(raw_by_file):
 from bpy_extras.io_utils import axis_conversion
 # FBX importer converts declared -Z forward/Y up into Blender +Y forward/Z up.
 native_to_import=axis_conversion(from_forward='-Z',from_up='Y',to_forward='Y',to_up='Z').to_4x4()
 import_to_native=native_to_import.inverted()
 for fn,raw in raw_by_file.items():
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.fbx(filepath=str(BASE/fn),use_anim=False)
  imported={o.name:o for o in bpy.context.scene.objects}
  check(fn+' actual import exact object names',set(imported)==set(raw))
  for n,d in raw.items():
   if n not in imported:continue
   o=imported[n];w=import_to_native@o.matrix_world
   check(fn+' actual import '+n+' origin',vecnear(w.translation,d['world_origin']),list(w.translation))
   check(fn+' actual import '+n+' hierarchy',(o.parent.name if o.parent else None)==d['parent'])
   if o.type=='MESH':
    vs=[tuple(w@v.co) for v in o.data.vertices];bb=bounds(vs)
    check(fn+' actual import '+n+' bounds',vecnear(bb[0],d['bounds'][0]) and vecnear(bb[1],d['bounds'][1]),bb)
    o.data.calc_loop_triangles();check(fn+' actual import '+n+' triangles',len(o.data.loop_triangles)==d['triangles'],len(o.data.loop_triangles))
    check(fn+' actual import '+n+' UV present',o.data.uv_layers.active is not None)
    if n.startswith('Pool_Ball_'):ball_uv_checks(o,fn+' imported '+n,w)
    check(fn+' actual import '+n+' materials',set(m.name for m in o.data.materials)==set(d['materials']),[m.name for m in o.data.materials])
  # Imported texture references must resolve locally, with no packed copies.
  for image in bpy.data.images:
   if image.source=='FILE':check(fn+' actual import texture resolves '+image.name,Path(bpy.path.abspath(image.filepath)).is_file() and not image.packed_file,bpy.path.abspath(image.filepath))

def run():
 expected=['VICE_HomeGames.blend','PoolTable.fbx','PoolAccessories.fbx','DartsBoard.fbx','Dart.fbx']
 for f in expected:check('required file '+f,(BASE/f).is_file())
 if not all((BASE/f).is_file() for f in expected):return
 bpy.ops.wm.open_mainfile(filepath=str(BASE/'VICE_HomeGames.blend'))
 check('Blender metric scale 1',bpy.context.scene.unit_settings.system=='METRIC' and near(bpy.context.scene.unit_settings.scale_length,1))
 for o in bpy.data.objects:
  if o.type in ('MESH','EMPTY'):check('native '+o.name+' identity rotation/scale',vecnear(o.rotation_euler,[0,0,0]) and vecnear(o.scale,[1,1,1]))
 cloth=bpy.data.objects.get('Pool_Cloth')
 if cloth:check('Cloth top normals face +Y',all(p.normal.y>.999 for p in cloth.data.polygons),{'minimum_normal_Y':min(p.normal.y for p in cloth.data.polygons)})
 rack=bpy.data.objects.get('Pool_Rack')
 if rack:
  radii=sorted(set(round(math.hypot(v.co.x,v.co.z),7) for v in rack.data.vertices));check('Rack exact .30 inner side and .01 wall thickness',len(radii)==2 and near(radii[0]*math.sqrt(3),.30) and near((radii[1]-radii[0])/2,.01),radii)
  check('Rack height .025',near(max(v.co.y for v in rack.data.vertices)-min(v.co.y for v in rack.data.vertices),.025))
 native_board=bpy.data.objects.get('Darts_Board')
 if native_board:
  expected_order=[20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5]
  for wi,number in enumerate(expected_order):
   a=math.radians(wi*18)
   for radius,kind in [(.05,'single'),(.103,'triple'),(.166,'double')]:
    hit,location,normal,face=native_board.ray_cast(Vector((radius*math.sin(a),radius*math.cos(a),.1)),Vector((0,0,-1)))
    expected=(['SisalDark','SisalLight'] if kind=='single' else ['RingRed','RingGreen'])[wi%2]
    actual=native_board.data.materials[native_board.data.polygons[face].material_index].name if hit else None
    check('Wedge '+str(number)+' '+kind+' clockwise colour',hit and actual==expected and normal.z>.99,actual)
  uv=native_board.data.uv_layers.active
  front=[p for p in native_board.data.polygons if all(near(native_board.data.vertices[i].co.z,.055) for i in p.vertices)]
  check('Board front UV fixed +X right +Y up',all(vecnear(uv.data[li].uv,(.5+native_board.data.vertices[native_board.data.loops[li].vertex_index].co.x/.4608,.5+native_board.data.vertices[native_board.data.loops[li].vertex_index].co.y/.4608)) for p in front for li in p.loop_indices))
 rails=bpy.data.objects.get('Pool_Rails')
 if rails:
  samples=[(sx*.57,z) for sx in [-1,1] for z in [-.08,0,.08]]+[(sx*x,sz*z) for sx in [-1,1] for sz in [-1,1] for x,z in [(.57,1.08),(.51,1.13)]]
  check('Rail actual ray intersections leave pocket mouths open',not any(rails.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))[0] for x,z in samples))
 if cloth:
  captures=[(-.56,1.12),(.56,1.12),(-.56,0),(.56,0),(-.56,-1.12),(.56,-1.12)]
  check('Cloth has open capture centres',not any(cloth.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))[0] for x,z in captures))
 table=read_fbx('PoolTable.fbx'); acc=read_fbx('PoolAccessories.fbx'); board=read_fbx('DartsBoard.fbx');dart=read_fbx('Dart.fbx')
 tm=['Pool_Body','Pool_Cloth','Pool_Rails','Pool_Pockets','Pool_Legs'];pk={'Pocket_NW':[-.56,.78,1.12],'Pocket_NE':[.56,.78,1.12],'Pocket_W':[-.56,.78,0],'Pocket_E':[.56,.78,0],'Pocket_SW':[-.56,.78,-1.12],'Pocket_SE':[.56,.78,-1.12]}
 required(table,['PoolTable_Root']+tm+list(pk),'PoolTable');empty(table,'PoolTable_Root',[0,0,0]);meshparents(table,'PoolTable_Root',tm)
 for n,p in pk.items():empty(table,n,p,'PoolTable_Root')
 tv=[v for n in tm if n in table for v in table[n]['vertices']];check('Pool exterior 1.40 x 0.86 x 2.52',vecnear(bounds(tv)[0],[-.7,0,-1.26]) and vecnear(bounds(tv)[1],[.7,.86,1.26]),bounds(tv));total(table,tm,6000,'PoolTable')
 if 'Pool_Cloth' in table:
  b=table['Pool_Cloth']['bounds'];check('Cloth exact X/Z extents and top .78',vecnear([b[0][0],b[1][0],b[0][2],b[1][2],b[1][1]],[-.56,.56,-1.12,1.12,.78]),b)
 if 'Pool_Rails' in table:
  vs=table['Pool_Rails']['vertices'];r=.028575
  check('Rails do not intrude code centre rectangle',not any(abs(x)<.56-r-1e-5 and abs(z)<1.12-r-1e-5 for x,y,z in vs))
  # Cushion vertices at playing boundary must stop at aperture limits.
  edge=[v for v in vs if abs(abs(v[0])-.56)<1e-5]
  check('Side mouths full .18 gap at cushion edge',bool(edge) and not any(abs(v[2])<.09-1e-5 for v in edge))
  check('Side corner mouths .09 at cushion edge',bool(edge) and not any(abs(v[2])>1.03+1e-5 for v in edge))
  end=[v for v in vs if abs(abs(v[2])-1.12)<1e-5]
  check('End corner mouths .09 at cushion edge',bool(end) and not any(abs(v[0])>.47+1e-5 for v in end))
 balls=['Pool_Ball_%02d'%i for i in range(16)]
 required(acc,['PoolCue_Root','Pool_Cue','CueTip','CueGrip','PoolRack_Root','Pool_Rack']+balls,'PoolAccessories')
 empty(acc,'PoolCue_Root',[0,0,0]);empty(acc,'CueTip',[0,0,0],'PoolCue_Root');empty(acc,'CueGrip',[0,0,-1.05],'PoolCue_Root');empty(acc,'PoolRack_Root',[0,0,0]);meshparents(acc,'PoolCue_Root',['Pool_Cue']);meshparents(acc,'PoolRack_Root',['Pool_Rack'])
 bbcheck(acc,'Pool_Cue',[-.016,-.016,-1.45],[.016,.016,0]);total(acc,['Pool_Cue'],500,'Cue');total(acc,['Pool_Rack'],300,'Rack')
 for n in balls:
  bbcheck(acc,n,[-.028575]*3,[.028575]*3)
  if bpy.data.objects.get(n):ball_uv_checks(bpy.data.objects[n],'native '+n)
  if n in acc:check(n+' centre origin',vecnear(acc[n]['world_origin'],[0,0,0]));total(acc,[n],320,n)
 bm=['Darts_Backplate','Darts_Board','Darts_NumberRing','Darts_Wire','Darts_Mount'];required(board,['DartsBoard_Root','BoardFace','BoardCentre']+bm,'DartsBoard');empty(board,'DartsBoard_Root',[0,0,0]);empty(board,'BoardFace',[0,0,.055],'DartsBoard_Root');empty(board,'BoardCentre',[0,0,0],'DartsBoard_Root');meshparents(board,'DartsBoard_Root',bm)
 bbcheck(board,'Darts_Backplate',[-.26,-.26,0],[.26,.26,.025]);bbcheck(board,'Darts_Board',[-.225,-.225,.025],[.225,.225,.055]);total(board,bm,4000,'Darts board')
 if 'Darts_Board' in board:
  radii=[math.hypot(v[0],v[1]) for v in board['Darts_Board']['vertices'] if near(v[2],.055)]
  for r in [.00635,.0159,.099,.107,.162,.170]:check('Analytical scoring radius '+str(r),any(near(x,r,1e-6) for x in radii))
 for n in ['Darts_NumberRing','Darts_Wire']:
  if n in board:
   b=board[n]['bounds'];check(n+' face layer <= .002',b[0][2]>=.055-2e-5 and b[1][2]<=.059+2e-5 and dims(b)[2]<=.002+2e-5,b)
 dm=['Dart_Tip','Dart_Barrel','Dart_Shaft','Dart_Flights'];required(dart,['Dart_Root','DartGrip']+dm,'Dart');empty(dart,'Dart_Root',[0,0,0]);empty(dart,'DartGrip',[0,0,.06],'Dart_Root');meshparents(dart,'Dart_Root',dm);total(dart,dm,400,'Dart')
 dv=[v for n in dm if n in dart for v in dart[n]['vertices']];b=bounds(dv);check('Dart total length and positive Z',near(b[0][2],0) and near(b[1][2],.16),b);check('Dart maximum flight span .036',near(max(dims(b)[:2]),.036),dims(b))
 tmat=set(m for d in list(table.values())+list(acc.values()) for m in d['materials']);dmat=set(m for d in list(board.values())+list(dart.values()) for m in d['materials']);check('Assembled table/accessories <=12 materials',len(tmat)<=12,sorted(tmat));check('Assembled board/dart <=8 materials',len(dmat)<=8,sorted(dmat))
 for p in (BASE/'Textures').glob('*.png'):
  im=bpy.data.images.load(str(p),check_existing=False);check(p.name+' 1024 square',list(im.size)==[1024,1024],list(im.size));bpy.data.images.remove(im)
 check('Three atlas PNGs',len(list((BASE/'Textures').glob('*.png')))>=3)
 importer_roundtrip({'PoolTable.fbx':table,'PoolAccessories.fbx':acc,'DartsBoard.fbx':board,'Dart.fbx':dart})
try:run()
except Exception as e:
 import traceback
 check('validator completed',False,traceback.format_exc())
REPORT['passed']=all(c['pass'] for c in REPORT['checks']);REPORT['summary']={'passed':sum(c['pass'] for c in REPORT['checks']),'failed':sum(not c['pass'] for c in REPORT['checks'])}
(BASE/'validation').mkdir(exist_ok=True)
(BASE/'validation'/'independent_validation.json').write_text(json.dumps(REPORT,indent=2))
print('INDEPENDENT VALIDATION:',REPORT['summary'])
for c in REPORT['checks']:
 if not c['pass']:print('FAIL:',c['check'],c['detail'])
