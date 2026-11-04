# Tarjetas de hojas a partir de hojas fotografiadas (Poly Haven island_tree_02, CC0)
import numpy as np, sys, math
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from scipy import ndimage
SRC=sys.argv[1]; OUT=sys.argv[2]
atlas=np.asarray(Image.open(SRC).convert("RGB"))
m=atlas.max(-1)>28
m=ndimage.binary_opening(m,iterations=2)
lab,n=ndimage.label(m)
hojas=[]
for k,sl in enumerate(ndimage.find_objects(lab)):
    sub=lab[sl]==k+1
    if sub.sum()<20000: continue
    rgb=atlas[sl].copy()
    a=(ndimage.gaussian_filter(sub.astype(float),1.0)*255).astype(np.uint8)
    im=Image.fromarray(np.dstack([rgb,a]),"RGBA")
    hojas.append(im)
print("hojas extraidas",len(hojas))
def silueta(w,h,tipo,rng):
    mk=Image.new("L",(w,h),0); d=ImageDraw.Draw(mk)
    pts=[]
    N=80
    for i in range(N+1):
        t=i/N                       # 0 base -> 1 punta
        if tipo=="lobulada":
            r=math.sin(math.pi*min(1,t*1.03))**0.7*(0.6+0.4*abs(math.sin(t*math.pi*4.5+0.3))**0.6)
        elif tipo=="oval":
            r=math.sin(math.pi*t)**0.7*(1+0.04*math.sin(t*90))
        else:
            r=math.sin(math.pi*t)
        pts.append((r,t))
    poly=[(w/2+r*w/2,h-t*h) for r,t in pts]+[(w/2-r*w/2,h-t*h) for r,t in reversed(pts)]
    d.polygon(poly,fill=255)
    return mk.filter(ImageFilter.GaussianBlur(1))
def hoja(rng,tipo,color,largo,brillo):
    src=hojas[rng.integers(len(hojas))]
    if tipo=="lanceolada": w,h=int(largo*0.32),int(largo)
    elif tipo=="oval": w,h=int(largo*0.6),int(largo)
    elif tipo=="lobulada": w,h=int(largo*0.62),int(largo)
    elif tipo=="oval_l": w,h=int(largo*0.42),int(largo)
    else: w,h=int(largo*0.42),int(largo)
    im=src.resize((max(4,w),max(4,h)),Image.LANCZOS)
    if tipo in ("lobulada","oval"):
        # textura de hoja real recortada con la silueta de la especie
        tex=src.crop((src.width*0.36,src.height*0.3,src.width*0.64,src.height*0.7)).resize((w,h))
        a=np.minimum(np.asarray(silueta(w,h,tipo,rng)),255)
        im=Image.fromarray(np.dstack([np.asarray(tex)[...,:3],a]),"RGBA")
    arr=np.asarray(im).astype(np.float32)
    rgb=arr[...,:3]; a=arr[...,3:]
    lum=rgb.mean(-1,keepdims=True); mu=lum[a[...,0]>128].mean() if (a>128).any() else 1
    tgt=np.array(color)*rng.uniform(0.85,1.15,3)**0.3
    rgb=np.clip((mu+(lum-mu)*0.9 + (rgb-lum)*0.35)/mu*tgt*brillo,0,255)
    return Image.fromarray(np.dstack([rgb,a]).astype(np.uint8),"RGBA")
def tarjeta(nombre,tipo,color,largo,denso,compuesta=False,seed=1):
    rng=np.random.default_rng(seed)
    S=1024
    img=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(img)
    # ramitas: varias desde abajo con brotes laterales
    ramas=[]
    def rama(x0,y0,ang,L,ancho,nivel):
        pts=[(x0,y0)]
        for s in range(10):
            ang+=rng.normal(0,0.07); x0+=math.cos(ang)*L/10; y0-=math.sin(ang)*L/10; pts.append((x0,y0))
        ramas.append((pts,ang))
        d.line(pts,fill=(72,58,42,255),width=int(ancho))
        if nivel==0:
            for k in range(3):
                i=int(rng.uniform(3,8)); px,py=pts[i]
                sg=1 if rng.random()<0.5 else -1
                rama(px,py,ang+sg*rng.uniform(0.5,0.9),L*rng.uniform(0.35,0.5),ancho*0.6,1)
    for r in range(4):
        rama(S/2+rng.normal(0,20),S,math.radians(90+rng.normal(0,20)),rng.uniform(0.45,0.62)*S,rng.uniform(6,9),0)
    hojas_pos=[]
    for pts,_ in ramas:
        for s in range(2,len(pts)):
            x,y=pts[s]; px,py=pts[s-1]
            dirr=math.degrees(math.atan2(-(y-py),x-px))
            for q in range(denso):
                lado=1 if rng.random()<0.5 else -1
                phi=dirr+lado*rng.uniform(25,65)
                hojas_pos.append((x,y,phi,rng.uniform(0.75,1.2)))
            if s==len(pts)-1: hojas_pos.append((x,y,dirr,1.1))
    rng.shuffle(hojas_pos)
    hojas_pos.sort(key=lambda h:h[3])    # las pequenas (fondo) primero
    for i,(x,y,phi,esc) in enumerate(hojas_pos):
        brillo=0.6+0.5*(i/len(hojas_pos))   # las del fondo mas oscuras: sombra propia de la copa
        if compuesta:
            # hoja compuesta (nogal): raquis con foliolos opuestos y uno terminal
            L=largo*esc*2.4
            hoja_img=Image.new("RGBA",(int(L*0.95),int(L)),(0,0,0,0))
            cx=hoja_img.width/2
            ImageDraw.Draw(hoja_img).line([(cx,L),(cx,L*0.08)],fill=(95,100,55,255),width=4)
            for k in range(3):
                yk=L*(0.72-k*0.22)
                for sgn in (-1,1):
                    f=hoja(rng,"oval_l",color,largo*esc*0.8*(1-0.1*k),brillo).rotate(-sgn*62,expand=True,resample=Image.BICUBIC)
                    hoja_img.alpha_composite(f,(int(cx+sgn*f.width*0.42-f.width/2),int(yk-f.height/2)))
            f=hoja(rng,"oval_l",color,largo*esc*0.85,brillo)
            hoja_img.alpha_composite(f,(int(cx-f.width/2),0))
            h_img=hoja_img
        else:
            h_img=hoja(rng,tipo,color,largo*esc,brillo)
        r=h_img.rotate(phi-90,expand=True,resample=Image.BICUBIC)
        Lh=h_img.height*0.5
        cxp=x+math.cos(math.radians(phi))*Lh; cyp=y-math.sin(math.radians(phi))*Lh
        if not (S*0.1<cxp<S*0.9 and S*0.08<cyp<S*0.92): continue
        ox,oy=int(cxp-r.width/2),int(cyp-r.height/2)
        if -r.width<ox<S and -r.height<oy<S:
            img.alpha_composite(r,(max(0,ox),max(0,oy)),(max(0,-ox),max(0,-oy)))
    a=np.asarray(img)[...,3]
    print(nombre,"cobertura",(a>128).mean().round(2))
    img.save(f"{OUT}/Hojas_{nombre}.png")
tarjeta("Castano","lanceolada",(66,100,36),200,2,seed=11)
tarjeta("Nogal","lanceolada",(92,122,48),95,1,compuesta=True,seed=12)
tarjeta("Roble","lobulada",(62,88,36),130,2,seed=13)
tarjeta("Cerezo","oval",(70,102,40),120,2,seed=14)
tarjeta("Arbusto","oval",(64,92,36),85,3,seed=15)
