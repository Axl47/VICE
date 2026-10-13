"""Compose review sheets from unedited accepted ordinary-camera Unity captures. Optional argument: output directory. Font substitution may change typography, never image geometry/orientation."""
from PIL import Image,ImageDraw,ImageFont,ImageOps
from pathlib import Path
import json,os,sys
P=Path(__file__).resolve().parents[1];R=P/'Renders'/'Unity';O=Path(sys.argv[1]) if len(sys.argv)>1 else P;O.mkdir(parents=True,exist_ok=True)
BG='#202a25';PANEL='#2c3831';FG='#eee8d7';GOLD='#dfbd6b';MUTED='#b8c2b8'
def font(size,bold=False):
 candidates=[os.environ.get('VICE_REVIEW_FONT_BOLD' if bold else 'VICE_REVIEW_FONT',''),'C:/Windows/Fonts/segoeuib.ttf' if bold else 'C:/Windows/Fonts/segoeui.ttf','/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf' if bold else '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf']
 for candidate in candidates:
  if candidate and Path(candidate).is_file():return ImageFont.truetype(candidate,size)
 raise RuntimeError('Install DejaVu Sans or set VICE_REVIEW_FONT and VICE_REVIEW_FONT_BOLD.')
def text(im,xy,s,size=26,color=FG,bold=False):ImageDraw.Draw(im).text(xy,s,font=font(size,bold),fill=color)
def image(im,name,box):
 x,y,w,h=box;pic=Image.open(R/name).convert('RGB');pic=ImageOps.contain(pic,(w,h),Image.Resampling.LANCZOS);im.paste(pic,(x+(w-pic.width)//2,y+(h-pic.height)//2))
def base(w,h,title,sub):
 im=Image.new('RGB',(w,h),BG);text(im,(42,24),title,43,bold=True);text(im,(44,86),sub,25,MUTED);return im
footer='Unity 2022.3.62f2 editor previews | Standard shared materials, ordinary cameras, Cull Back | NOT GAME SCREENSHOTS'
im=base(2400,1700,'VICE LIFESTYLE / ACCEPTED IMPORTED ARTWORK','Production b6b148e | Actual imported FBXs | Source concepts guide appearance; these views establish Unity readability')
items=[('POOL / THREE-QUARTER','Pool_ThreeQuarter.png'),('POOL / PLAYER VIEW','Pool_Player.png'),('CUE + DECORATIVE RACK','Cue_Rack_Oblique.png'),('DARTS / FRONT +Z','Darts_Front_Ortho.png'),('DARTS / OBLIQUE','Darts_Oblique.png'),('DART / TIP-PIVOT DETAIL','Dart_Detail.png')]
for i,(label,name) in enumerate(items):
 x=40+(i%3)*790;y=145+(i//3)*730;text(im,(x,y),label,26,GOLD,True);image(im,name,(x,y+45,760,650))
text(im,(42,1635),footer,24,MUTED);im.save(O/'Model_Review_ContactSheet.png')
im=base(2400,1750,'VICE LIFESTYLE / BOTH BALL HEMISPHERES','Upright unmirrored numerals 1–15 | Shared 1024px atlas | Diameter 0.05715 m, centre pivots, 288 triangles each')
text(im,(45,140),'FRONT HEMISPHERE / ORDINARY +Z CAMERA',28,GOLD,True);image(im,'Balls_Front.png',(40,180,2320,690))
text(im,(45,900),'OPPOSITE HEMISPHERE / SPHERES ROTATED 180° ABOUT Y',28,GOLD,True);image(im,'Balls_Rear.png',(40,940,2320,690))
text(im,(42,1685),footer,24,MUTED);im.save(O/'Ball_Hemispheres.png')
im=base(2400,1250,'VICE LIFESTYLE / APPROVED HIT MAPPING','Analytical X stays unchanged | Visual adapter maps (-LastX, LastY, 0.055) once | Ordinary +Z camera')
text(im,(45,140),'ANALYTICAL (+0.12, 0) → ROOT (-0.12, 0, 0.055) → 6',28,GOLD,True);image(im,'Darts_Mapped_AnalyticalPositiveX6.png',(40,185,1135,940))
text(im,(1225,140),'ANALYTICAL (-0.12, 0) → ROOT (+0.12, 0, 0.055) → 11',28,GOLD,True);image(im,'Darts_Mapped_AnalyticalNegativeX11.png',(1220,185,1135,940))
text(im,(42,1170),'Reviewer scene demonstrates the approved visual mapping. No gameplay adapter, scoring, panel or packet code is implemented here.',25,MUTED);text(im,(42,1205),footer,23,MUTED);im.save(O/'Mapped_Hit_Proof.png')
im=base(2400,2240,'VICE LIFESTYLE / UNITY IMPORTED DIMENSIONS','Metres | Actual imported orthographic views | Values verified from geometry, not inferred from image pixels')
text(im,(45,145),'POOL / TOP +Y, CAMERA UP +Z',28,GOLD,True);image(im,'Pool_Top_Ortho.png',(40,195,850,990))
text(im,(920,145),'POOL / SIDE +X',28,GOLD,True);image(im,'Pool_Side_Ortho.png',(920,185,1420,500))
for j,line in enumerate(['Exterior X 1.40 × Y 0.86 × Z 2.52','Bed X [-0.56,+0.56], Z [-1.12,+1.12], Y 0.78','Ball radius 0.028575; resting centre Y 0.808575','Six pockets: X ±0.56; Z -1.12, 0, +1.12; Y 0.78','Pocket radius 0.065; corners 0.09, side mouths full 0.18']):text(im,(940,700+j*48),line,28)
text(im,(45,1240),'DARTS / FRONT +Z',28,GOLD,True);image(im,'Darts_Front_Ortho.png',(40,1290,850,800))
text(im,(920,1050),'BOARD + SCORING / IMPORTED GEOMETRY',28,GOLD,True)
for j,line in enumerate(['Backplate Ø0.52, Z [0,0.025]; board Ø0.45, Z [0.025,0.055]','BoardFace (0,0,0.055); wire/number face Z 0.057','Inner bull 0.00635; outer bull 0.01590','Triple radii 0.099 / 0.107; double 0.162 / 0.170','20 at +Y; visible 6 at root -X, 11 at root +X','Clockwise sequence: 20,1,18,4,13,6,10,15,2,17,','3,19,7,16,8,11,14,9,12,5']):text(im,(940,1105+j*48),line,27)
text(im,(920,1510),'ACCESSORIES / PRESERVED CONTRACT',28,GOLD,True)
for j,line in enumerate(['Cue: tip origin, shaft -Z 1.45; max Ø0.032','CueGrip (0,0,-1.05); cue 146 triangles','Rack: inside side 0.30, wall 0.01, height 0.025','Rack is decorative; it does not enclose the code-owned full rack','Balls: Ø0.05715, centre origins, 288 triangles each','Dart: tip Z0, tail +Z0.16; flight span 0.036','DartGrip (0,0,0.06); dart 158 triangles','Table 1,448; board 2,818 triangles; all within budgets']):text(im,(940,1560+j*48),line,27)
text(im,(42,2135),'Views are independently scaled. Preserve Hierarchy; identity root transforms. No scoring, physics or native gameplay is claimed.',24,MUTED);text(im,(42,2175),footer,23,MUTED);im.save(O/'Orthographic_Dimensions.png')
print('Created four labelled sheets from unedited ordinary-camera Unity PNGs.')
