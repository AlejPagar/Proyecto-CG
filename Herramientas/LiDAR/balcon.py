import numpy as np, sys
from PIL import Image, ImageDraw, ImageFilter
P=sys.argv[1]
rng=np.random.default_rng(5)
mad=Image.open(f"{P}/dark_planks_diff_2k.jpg").convert("RGB").resize((1024,1024))
madv=np.asarray(mad.rotate(90),np.float32)
# --- Barandilla: 1.2 m de ancho x 1.0 m de alto, 1024x512 (balaustres cada 0.12 m)
W,H=1024,512; px=W/1.2; py=H/1.0
m=Image.new("L",(W,H),0); d=ImageDraw.Draw(m)
d.rectangle([0,0,W,int(0.07*py)],fill=255)            # pasamanos
d.rectangle([0,H-int(0.08*py),W,H],fill=255)          # zocalo
d.rectangle([0,int(0.82*py),W,int(0.86*py)],fill=255) # travesano bajo
for i in range(10):
    x=(i+0.5)*0.12*px; w=0.045*px
    d.rectangle([x-w/2,0,x+w/2,H],fill=255)
col=madv[:H,:W]*np.array([0.62,0.55,0.48])
a=np.asarray(m)
img=np.dstack([np.clip(col,0,255).astype(np.uint8),a])
Image.fromarray(img,"RGBA").save(f"{P}/Barandilla.png")
# --- Geranios en maceta: 512x512 (mata de 0.5 m)
S=512
im=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(im)
for k in range(260):   # hojas redondeadas
    x=rng.normal(S/2,S*0.17); y=rng.uniform(S*0.35,S*0.98)
    r=rng.uniform(10,22); g=rng.uniform(0.75,1.15)
    c=(int(52*g),int(98*g),int(38*g),255)
    d.ellipse([x-r,y-r*0.8,x+r,y+r*0.8],fill=c)
    d.arc([x-r,y-r*0.8,x+r,y+r*0.8],0,360,fill=(int(35*g),int(70*g),int(25*g),255),width=2)
colores=[(205,30,40),(225,60,110),(240,240,235),(200,20,70),(235,90,60)]
for k in range(16):   # umbelas de flores
    cx=rng.normal(S/2,S*0.2); cy=rng.uniform(S*0.08,S*0.55)
    base=colores[rng.integers(len(colores))]
    for j in range(40):
        x=cx+rng.normal(0,14); y=cy+rng.normal(0,11); r=rng.uniform(4,8); g=rng.uniform(0.8,1.1)
        d.ellipse([x-r,y-r,x+r,y+r],fill=(min(255,int(base[0]*g)),min(255,int(base[1]*g)),min(255,int(base[2]*g)),255))
im.save(f"{P}/Geranios.png")
print("ok")
