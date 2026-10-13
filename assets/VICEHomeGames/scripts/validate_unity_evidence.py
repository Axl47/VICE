"""Recheck 183 numeric assertions from saved actual Unity 2022.3.62f2 measurements. No live Unity/game execution or image manipulation."""
import json,math,hashlib,gzip
from pathlib import Path
P=Path(__file__).resolve().parents[1]/'validation'/'unity'
d=json.loads((P/'unity-preserved-report.json').read_text());w=json.loads(gzip.decompress((P/'unity-winding-report.json.gz').read_bytes()).decode('utf-8-sig'));mr=json.loads((P/'material-remap-report.json').read_text())
parts={f['file']:{p['name']:p for p in f['parts']} for f in d['files']};mesh={p['name']:p for p in w['parts']};checks=[]
def vec(v):return [v['x'],v['y'],v['z']]
def near(a,b):return all(abs(x-y)<2e-5 for x,y in zip(a,b))
def check(n,ok,detail=None):checks.append({'check':n,'pass':bool(ok),'detail':detail})
for f in d['files']:
 check(f['file']+' metre scale',f['fileScale']==1 and f['globalScale']==1)
 for p in f['parts']:
  check(f['file']+' '+p['name']+' identity rotation/scale',near(vec(p['localRotation']),[0,0,0]) and near(vec(p['localScale']),[1,1,1]))
  if p['triangles']:check(p['name']+' textures resolved',all(t!='MISSING' for t in p['textures']))
for n,xyz in {'Pocket_NE':[.56,.78,1.12],'Pocket_NW':[-.56,.78,1.12],'Pocket_E':[.56,.78,0],'Pocket_W':[-.56,.78,0],'Pocket_SE':[.56,.78,-1.12],'Pocket_SW':[-.56,.78,-1.12]}.items():check(n+' signed origin',near(vec(parts['PoolTable'][n]['origin']),xyz))
for f,n in [('PoolTable','PoolTable_Root'),('DartsBoard','DartsBoard_Root'),('Dart','Dart_Root'),('PoolAccessories','PoolCue_Root'),('PoolAccessories','PoolRack_Root')]:
 check(n+' preserved',n in parts[f]);check(n+' origin',near(vec(parts[f][n]['origin']),[0,0,0]))
for f,n,lo,hi in [('PoolTable','Pool_Cloth',[-.56,.78,-1.12],[.56,.78,1.12]),('DartsBoard','Darts_Board',[-.225,-.225,.025],[.225,.225,.055]),('DartsBoard','Darts_Backplate',[-.26,-.26,0],[.26,.26,.025]),('PoolAccessories','Pool_Cue',[-.016,-.016,-1.45],[.016,.016,0])]:
 p=parts[f][n];check(n+' imported bounds',near(vec(p['min']),lo) and near(vec(p['max']),hi))
ts=[p for p in parts['PoolTable'].values() if p['triangles']]
lo=[min(p['min'][a] for p in ts) for a in ['x','y','z']];hi=[max(p['max'][a] for p in ts) for a in ['x','y','z']]
check('pool exterior bounds',near(lo,[-.7,0,-1.26]) and near(hi,[.7,.86,1.26]),[lo,hi])
for i in range(16):
 p=parts['PoolAccessories'][f'Pool_Ball_{i:02d}'];check(p['name']+' bounds',near(vec(p['min']),[-.028575]*3) and near(vec(p['max']),[.028575]*3));check(p['name']+' centre',near(vec(p['origin']),[0,0,0]));check(p['name']+' triangle budget',p['triangles']<=320)
for name,y in [('Pool_Rails',.858),('Pool_Cloth',.78)]:
 ts=[t for t in mesh[name]['triangles'] if all(abs(v['y']-y)<1e-6 for v in t['vertices'])]
 check(name+' top faces outward +Y',all(t['geometricNormal']['y']>.99 for t in ts),{'count':len(ts),'negative_y':sum(t['geometricNormal']['y']<-.99 for t in ts)})
for name,z in [('Darts_Board',.055),('Darts_NumberRing',.057),('Darts_Wire',.057)]:
 ts=[t for t in mesh[name]['triangles'] if all(abs(v['z']-z)<1e-6 for v in t['vertices'])]
 check(name+' scoring front outward +Z',all(t['geometricNormal']['z']>.99 for t in ts),{'count':len(ts)})
radii=[math.hypot(v['x'],v['y']) for t in mesh['Darts_Board']['triangles'] for v in t['vertices'] if abs(v['z']-.055)<1e-6]
for r in [.00635,.0159,.099,.107,.162,.170]:check('board scoring radius '+str(r),any(abs(x-r)<1e-6 for x in radii))
n=parts['DartsBoard']['Darts_NumberRing'];check('approved number-ring negative-X UV mapping',n['frontUvMaxReflectedXError']<1e-6)
for n,expected in [('BoardFace',[0,0,.055]),('BoardCentre',[0,0,0])]:check(n+' origin',near(vec(parts['DartsBoard'][n]['origin']),expected))
for n,expected in [('CueTip',[0,0,0]),('CueGrip',[0,0,-1.05])]:check(n+' origin',near(vec(parts['PoolAccessories'][n]['origin']),expected))
check('DartGrip origin',near(vec(parts['Dart']['DartGrip']['origin']),[0,0,.06]))
dt=[p for p in parts['Dart'].values() if p['triangles']];dlo=[min(p['min'][a] for p in dt) for a in ['x','y','z']];dhi=[max(p['max'][a] for p in dt) for a in ['x','y','z']]
check('dart total bounds',near(dlo,[-.018,-.018,0]) and near(dhi,[.018,.018,.16]),[dlo,dhi])
check('eight actual shared darts objects',mr['uniqueDartsObjects']==8 and len(set(m['id'] for m in mr['materials']))==8)
check('twelve summed darts slots',mr['dartsSlots']==12)
wire=[p['materialIds'][i] for f in ['DartsBoard','Dart'] for p in parts[f].values() for i,n in enumerate(p['materials']) if n=='WireMetal'];check('same WireMetal reference across FBXs',len(set(wire))==1,wire)
for f,limit in [('PoolTable',6000),('DartsBoard',4000),('Dart',400)]:check(f+' total triangle budget',sum(p['triangles'] for p in parts[f].values())<=limit)
check('cue triangle budget',parts['PoolAccessories']['Pool_Cue']['triangles']<=500);check('rack triangle budget',parts['PoolAccessories']['Pool_Rack']['triangles']<=300)
report={'commit':'b6b148e0c2bdf171f8eab9da5b4edddcbd1d9a18','scope':'Production artwork directly under assets/VICEHomeGames; isolated Unity 2022.3.62f2 import, ordinary cameras and persistent shared-material remap','checks':checks,'numeric_summary':{'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks)},'visual_result':'ACCEPTED: correct visible clockwise board order and readable glyphs; approved analytical +X hit shown in root -X visible6, opposite hit in11; clean cloth and outward solid faces; both ball hemispheres readable.','limitations':['Standard shader in isolated Built-in Unity editor; native URP binding not verified.','No native game, physics/scoring adapter, installation, saves or multiplayer testing.','Existing superseded Blender review sheets must be replaced by the supplied Unity sheets before treating repository images as final production evidence.'],'mapped_hit':{'analytical':[.12,0],'artwork_root':[-.12,0,.055],'visible_sector':6,'camera':[0,0,2.37],'target':[0,0,.025],'up':[0,1,0],'method':'ordinary Camera Quaternion.LookRotation, no projection reflection or negative scales'},'triangle_counts':{'pool_table':sum(p['triangles'] for p in parts['PoolTable'].values()),'cue':parts['PoolAccessories']['Pool_Cue']['triangles'],'rack':parts['PoolAccessories']['Pool_Rack']['triangles'],'each_ball':288,'board':sum(p['triangles'] for p in parts['DartsBoard'].values()),'dart':sum(p['triangles'] for p in parts['Dart'].values())}}
print('Replaying saved Unity measurements; this Python command does not launch Unity.');print(report['numeric_summary']);print(report['triangle_counts'])
for c in checks:
 if not c['pass']:print('FAIL',c)
