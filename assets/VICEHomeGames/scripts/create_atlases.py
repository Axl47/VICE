"""Deterministic 1024px original base-colour atlases; requires Pillow."""
from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import math,random
ROOT=Path(__file__).resolve().parents[1]; OUT=ROOT/'Textures';OUT.mkdir(exist_ok=True)
font='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
# 4x4 palette cells, deliberately broad colour variation rather than photographic grain.
colors=['#74482d','#123d2a','#174432','#171b19','#b49a60','#c89a58','#d8d8ca','#704527','#39271e','#96643e','#c8a36d','#ddd1ad','#66573a','#1f2a26','#b9b8a4','#faf6df']
im=Image.new('RGB',(1024,1024));d=ImageDraw.Draw(im);random.seed(47)
for i,c in enumerate(colors):
 x=(i%4)*256;y=(i//4)*256;d.rectangle((x,y,x+255,y+255),fill=c)
 if i in (0,5,7,8,9):
  base=tuple(int(c[j:j+2],16) for j in (1,3,5))
  for k in range(16):
   yy=y+random.randrange(16,242); delta=random.choice([-8,-4,5,9]); col=tuple(max(0,min(255,v+delta)) for v in base)
   d.line((x+8,yy,x+random.randrange(100,248),yy+random.randrange(-5,6)),fill=col,width=random.choice([2,3,5]))
im.save(OUT/'PoolFurniture_BaseColor.png')
# Each ball has its own equirectangular tile; seam at u=0, number islands at u=.25,.75.
im=Image.new('RGB',(1024,1024));d=ImageDraw.Draw(im)
bcols=['#f7f3df','#eab817','#295ab4','#c63826','#713c91','#e97025','#278b54','#89372e','#141b1c']
for n in range(16):
 x=(n%4)*256;y=(n//4)*256;c=bcols[n if n<9 else n-8]
 d.rectangle((x,y,x+255,y+255),fill=c if n<9 else bcols[0])
 if n>=9:d.rectangle((x,y+80,x+255,y+176),fill=c)
 if n:
  for u in (64,192):
   d.ellipse((x+u-26,y+85,x+u+26,y+171),fill=bcols[0])
   text=Image.new('RGBA',(80,100));td=ImageDraw.Draw(text); f=ImageFont.truetype(font,70 if n<10 else 57);td.text((40,50),str(n),font=f,anchor='mm',fill='#161b1c');text=text.resize((40,70))
   im.paste(text,(x+u-20,y+93),text)
im.save(OUT/'PoolBalls_BaseColor.png')
# Front projection is fixed to Unity +X right,+Y up. Clockwise 20 at +Y.
im=Image.new('RGB',(1024,1024),'#191e1c');d=ImageDraw.Draw(im);cx=cy=512;scale=1000/.45
order=[20,1,18,4,13,6,10,15,2,17,3,19,7,16,8,11,14,9,12,5]
for i,n in enumerate(order):
 a=i*18-9; b=a+18
 for ri,ro,col in [(0.0159,.162,'#272b25' if i%2==0 else '#d7cdb1'),(.099,.107,'#b43b32' if i%2==0 else '#34815b'),(.162,.170,'#b43b32' if i%2==0 else '#34815b')]:
  pts=[]
  for r,seq in [(ro,range(13)),(ri,range(12,-1,-1))]:
   for t in seq:
    ang=math.radians(a+(b-a)*t/12);pts.append((cx+r*scale*math.sin(ang),cy-r*scale*math.cos(ang)))
  d.polygon(pts,fill=col)
 ang=math.radians(i*18);r=.197*scale;d.text((cx+r*math.sin(ang),cy-r*math.cos(ang)),str(n),font=ImageFont.truetype(font,35),anchor='mm',fill='#e7dfc7')
for r,col in [(.0159,'#34815b'),(.00635,'#b43b32')]:
 p=r*scale;d.ellipse((cx-p,cy-p,cx+p,cy+p),fill=col)
# Unused corner swatches for backplate, metal, shaft and flights.
for i,c in enumerate(['#383b32','#b9b59c','#252d27','#b63c32','#dfc996','#558872']):
 x=8+(i%3)*40;y=8+(i//3)*40;d.rectangle((x,y,x+31,y+31),fill=c)
im.save(OUT/'Darts_BaseColor.png')
print('Created three original 1024x1024 RGB atlases.')
