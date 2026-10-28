# Textura de fachada entramada (1 planta de alto x 2.4 m de ancho) a partir de texturas fotograficas
import numpy as np, sys
from PIL import Image, ImageDraw, ImageFilter
P=sys.argv[1]
S=2048; W=2.4; H=2.7          # metros que representa la textura
px=S/W; py=S/H
def carga(n,m): return np.asarray(Image.open(f"{P}/{n}_{m}_2k.jpg").convert("RGB"),np.float32)/255
yeso={"Ocre":carga("clay_plaster","diff"),"Blanco":carga("white_plaster_rough_01","diff")}
yeso_n={"Ocre":carga("clay_plaster","nor_gl"),"Blanco":carga("white_plaster_rough_01","nor_gl")}
mad=carga("dark_planks","diff"); mad_n=carga("dark_planks","nor_gl")
# vetas verticales: rotar madera 90 grados
madv=np.rot90(mad).copy(); madv_n=np.rot90(mad_n).copy()
madv_n=np.stack([1-madv_n[...,1],madv_n[...,0],madv_n[...,2]],-1)  # rotar tambien el vector normal
# mascara de madera + altura (relieve)
m=Image.new("L",(S,S),0); d=ImageDraw.Draw(m)
rng=np.random.default_rng(3)
# vigas horizontales (abajo: durmiente, arriba: carrera)
d.rectangle([0,S-int(0.16*py),S,S],fill=255)
d.rectangle([0,0,S,int(0.13*py)],fill=255)
# pies derechos: separacion 0.30-0.45 m, deben cerrar en 2.4 m
xs=[0.0]; 
while True:
    nx=xs[-1]+rng.uniform(0.3,0.45)
    if nx>W-0.25: break
    xs.append(nx)
for x in xs:
    w=rng.uniform(0.09,0.13)
    a=int((x-w/2)*px); b=int((x+w/2)*px)
    d.rectangle([a,0,b,S],fill=255)
    if a<0: d.rectangle([S+a,0,S,S],fill=255)
# tornapuntas diagonales en un vano
x1,x2=xs[1],xs[3]
d.line([(x1*px,S-0.16*py),(x2*px,0.13*py)],fill=255,width=int(0.11*px))
mask=np.asarray(m,np.float32)/255
blur=np.asarray(m.filter(ImageFilter.GaussianBlur(10)),np.float32)/255
# normal del relieve (madera sobresale)
h=np.asarray(m.filter(ImageFilter.GaussianBlur(5)),np.float32)/255*6
gy,gx=np.gradient(h)
def tex_rep(t,sx,sy):   # repite la textura con escala (metros que cubre la textura original)
    ys=(np.arange(S)[:,None]*sy)%t.shape[0]; xs_=(np.arange(S)[None,:]*sx)%t.shape[1]
    return t[ys.astype(int),xs_.astype(int)]
for nombre in ["Ocre","Blanco"]:
    # yeso: textura de 2 m -> escala
    y=tex_rep(yeso[nombre],2048/(2.0*px),2048/(2.0*py)); yn=tex_rep(yeso_n[nombre],2048/(2.0*px),2048/(2.0*py))
    mv=tex_rep(madv,2048/(3.0*px),2048/(3.0*py)); mvn=tex_rep(madv_n,2048/(3.0*px),2048/(3.0*py))
    def tono(t,obj,contraste):
        lum=t.mean(-1,keepdims=True); m=lum.mean()
        return np.clip((m+(lum-m)*contraste)/m*np.array(obj)/255,0,1)
    mv=tono(mv,(78,66,54),0.9)   # castano viejo, gris parduzco
    y=tono(y,(196,176,140) if nombre=="Ocre" else (228,224,214),0.8)
    ao=1-0.35*np.clip(blur-mask,0,1)[...,None]     # sombra de contacto junto a la madera
    col=np.clip((y*(1-mask[...,None])+mv*mask[...,None])*ao,0,1)
    Image.fromarray((col*255).astype(np.uint8)).save(f"{P}/Entramado_{nombre}_diff.jpg",quality=92)
    n=(yn*(1-mask[...,None])+mvn*mask[...,None])*2-1
    n[...,0]+= -gx*1.0; n[...,1]+= gy*1.0
    n/=np.linalg.norm(n,axis=-1,keepdims=True)
    Image.fromarray(((n*0.5+0.5)*255).astype(np.uint8)).save(f"{P}/Entramado_{nombre}_nor.jpg",quality=95)
print("ok",len(xs))
