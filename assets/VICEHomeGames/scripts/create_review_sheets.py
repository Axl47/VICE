"""Compose labelled sheets from the actual orthographic/offline model renders."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
P=Path(__file__).resolve().parents[1]
F='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
font=lambda n:ImageFont.truetype(F,n)
bg='#202925';fg='#eee6d0';muted='#b0b8ab';accent='#d6b66b'
def fit(im,box):return ImageOps.contain(im,box)
def panel(canvas,path,box,title,lines):
 x,y,w,h=box;d=ImageDraw.Draw(canvas);d.rounded_rectangle((x,y,x+w,y+h),14,fill='#354039');d.text((x+20,y+16),title,font=font(26),fill=accent)
 im=Image.open(P/'Renders'/path).convert('RGB');im=fit(im,(w-24,h-150));canvas.paste(im,(x+(w-im.width)//2,y+58+(h-160-im.height)//2))
 for i,line in enumerate(lines):d.text((x+20,y+h-82+i*26),line,font=font(19),fill=fg)
im=Image.new('RGB',(1800,1560),bg);d=ImageDraw.Draw(im)
d.text((55,32),'VICE LIFESTYLE / ORTHOGRAPHIC DIMENSIONS',font=font(40),fill=fg)
d.text((55,88),'ACTUAL SOURCE MESHES • METRES • UNITY +Y UP • OFFLINE REVIEW',font=font(21),fill=muted)
panel(im,'Pool_Top_Ortho.png',(40,135,630,995),'POOL / TOP (+Y)', ['Exterior X 1.40 × Z 2.52','Bed X 1.12 × Z 2.24; six 0.065-radius mouths'])
panel(im,'Pool_Side_Ortho.png',(695,135,1065,410),'POOL / SIDE (+X)', ['Exterior Y 0.86; bed Y 0.78','Resting ball centre Y 0.808575'])
panel(im,'Darts_Front_Ortho.png',(695,570,660,560),'DARTS / FRONT (+Z)', ['Backplate Ø0.52; scoring board Ø0.45','20 at +Y; clockwise order; +X to viewer right'])
d.rounded_rectangle((1375,570,1760,1130),14,fill='#354039');d.text((1395,595),'SCORING RADII',font=font(24),fill=accent)
for i,t in enumerate(['Inner bull   0.00635','Outer bull   0.01590','Triple in     0.099','Triple out   0.107','Double in   0.162','Double out 0.170','','Wall plane    Z 0','Backplate     Z 0–0.025','Board face   Z 0.055','Wire face     Z 0.057']):d.text((1395,647+i*36),t,font=font(19),fill=fg)
d.rounded_rectangle((40,1155,1760,1460),14,fill='#354039');d.text((62,1178),'ACCESSORIES / EXACT LOCAL CONTRACT',font=font(27),fill=accent)
lines=['BALLS  Ø0.05715 each; independent centre origins; 00 white, 01–07 solid, 08 black, 09–15 striped.', 'CUE  1.45 long × Ø0.032 maximum; tip at origin; shaft extends −Z; CueGrip (0,0,−1.05).', 'RACK  Equilateral inside side 0.30; wall 0.01; height 0.025; centred in XZ. Decorative, not a fitted rack.', 'DART  0.16 tip-to-tail; 0.036 maximum flight span; tip Z 0; tail +Z 0.16; DartGrip (0,0,0.06).', 'POCKETS  X ±0.56; Z ±1.12 and 0; Y 0.78. Cushion gap: corners 0.09; sides full 0.18.']
for i,t in enumerate(lines):d.text((62,1230+i*39),t,font=font(21),fill=fg)
d.text((55,1500),'Views are independently scaled for readability. Values describe exported geometry, not image pixel measurements.',font=font(20),fill=muted)
im.save(P/'Orthographic_Dimensions.png')
# Contact sheet with actual finished meshes. No concept imagery mixed into model evidence.
shots=[('Pool_Isometric.png','POOL / ASSEMBLED'),('Pool_PlayerView.png','POOL / PLAYER VIEW'),('Pool_Accessories.png','ACCESSORIES / NUMBER CHECK'),('Darts_Front_Ortho.png','DARTS / FRONT'),('Darts_Oblique.png','DARTS / DEPTH'),('Dart_Detail.png','DART / TIP PIVOT')]
im=Image.new('RGB',(1800,1530),bg);d=ImageDraw.Draw(im);d.text((40,25),'VICE LIFESTYLE / ACTUAL MODEL REVIEW',font=font(38),fill=fg);d.text((42,80),'Offline Cycles renders of the deliverable meshes • no gameplay screenshot claims',font=font(22),fill=muted)
for i,(name,label) in enumerate(shots):
 x=35+(i%3)*590;y=135+(i//3)*675;d.text((x,y),label,font=font(24),fill=accent);src=fit(Image.open(P/'Renders'/name).convert('RGB'),(560,600));im.paste(src,(x+(560-src.width)//2,y+45+(600-src.height)//2))
im.save(P/'Model_Review_ContactSheet.png')
print('Created orthographic dimensions and actual-model contact sheets.')
