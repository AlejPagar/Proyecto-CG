# Matas de hierba con briznas fotografiadas (Poly Haven grass_medium_02, CC0)
import numpy as np, sys, math
from PIL import Image
from scipy import ndimage
SRC=sys.argv[1]; OUT=sys.argv[2]
atlas=np.asarray(Image.open(SRC).convert("RGB"))
m=atlas.max(-1)>22
lab,n=ndimage.label(m)
briznas=[]
for k,sl in enumerate(ndimage.find_objects(lab)):
    sub=lab[sl]==k+1
    h,w=sub.shape
    if sub.sum()<3000 or h<300: continue
    a=(ndimage.gaussian_filter(sub.astype(float),0.7)*255).astype(np.uint8)
    briznas.append(Image.fromarray(np.dstack([atlas[sl],a]),"RGBA"))
print("briznas",len(briznas))
def mata(nombre,colores,nb,alto,seed):
    rng=np.random.default_rng(seed); S=512
    img=Image.new("RGBA",(S,S),(0,0,0,0))
    for i in range(nb):
        b=briznas[rng.integers(len(briznas))]
        if rng.random()<0.5: b=b.transpose(Image.FLIP_LEFT_RIGHT)
        H=int(S*alto*rng.uniform(0.45,1.0)); W=max(3,int(b.width*H/b.height*rng.uniform(0.8,1.3)))
        b=b.resize((W,H),Image.LANCZOS)
        arr=np.asarray(b).astype(np.float32)
        rgb=arr[...,:3]; lum=rgb.mean(-1,keepdims=True); mu=max(1,lum[arr[...,3]>128].mean())
        col=np.array(colores[rng.integers(len(colores))])*rng.uniform(0.85,1.15)
        rgb=np.clip((mu+(lum-mu)*0.8)/mu*col,0,255)
        # base mas oscura (sombra entre las briznas)
        g=np.linspace(1.0,0.55,H)[:,None,None]
        rgb=rgb*g
        b=Image.fromarray(np.dstack([rgb,arr[...,3:]]).astype(np.uint8),"RGBA")
        ang=rng.normal(0,14)
        r=b.rotate(ang,expand=True,resample=Image.BICUBIC,center=(W/2,H))
        x=int(S/2+rng.normal(0,S*0.12)-r.width/2); y=S-r.height+int(rng.uniform(0,6))
        img.alpha_composite(r,(max(0,x),max(0,y)),(max(0,-x),max(0,-y)))
    img.save(f"{OUT}/{nombre}.png"); print(nombre,(np.asarray(img)[...,3]>128).mean().round(2))
mata("Hierba_Seca",[(196,170,112),(176,150,98),(210,190,140),(150,138,96),(160,150,100)],70,0.95,3)
mata("Hierba_Verde",[(96,118,52),(120,130,60),(84,104,44),(150,145,85)],60,0.95,4)
