import numpy as np, sys
from scipy import ndimage
from scipy.interpolate import griddata
from PIL import Image, ImageDraw
SP=sys.argv[1]; DATOS=sys.argv[2]
d=np.load(SP+"/lidar/pueblo.npz")
suelo,edif,veg,rgb=d["suelo"],d["edif"],d["veg"],d["rgb"]
X0,Z0,C=float(d["X0"]),float(d["Z0"]),float(d["C"])
N=suelo.shape[0]
# ---------- MDT a 1 m (nodos de la rejilla del terreno: 1025x1025) ----------
s2=suelo.reshape(N//2,2,N//2,2)
dtm1=np.nanmean(s2,axis=(1,3))                       # celdas de 1 m, centro en +0.5
v=~np.isnan(dtm1)
ii,jj=np.nonzero(v)
gi,gj=np.mgrid[0:N//2,0:N//2]
f=griddata((ii,jj),dtm1[v],(gi,gj),method="linear")
fn=griddata((ii,jj),dtm1[v],(gi,gj),method="nearest")
f=np.where(np.isnan(f),fn,f)
# quitar ruido: mediana 3x3 y suavizado ligero
f=ndimage.median_filter(f,3); f=ndimage.gaussian_filter(f,0.7)
# nodos: posicion k*1m -> media de celdas vecinas
M=N//2
nod=np.zeros((M+1,M+1))
fp=np.pad(f,1,mode="edge")
nod=(fp[:-1,:-1]+fp[1:,:-1]+fp[:-1,1:]+fp[1:,1:])/4
nod.astype(np.float32).tofile(DATOS+"/Alturas_Pueblo_LiDAR.bytes")
print("MDT", nod.shape, nod.min(), nod.max())
def suelo_en(x,z):
    j=np.clip((x-X0),0,M-1e-3); i=np.clip((z-Z0),0,M-1e-3)
    return ndimage.map_coordinates(nod,[[i],[j]],order=1)[0]
# ---------- MDT a 0.5 m para alturas relativas ----------
dtm05=ndimage.zoom(f,2,order=1)
# ---------- Edificios ----------
eds=[]
for l in open(DATOS+"/Edificios_Catastro.txt"):
    p=l.strip().split(";")
    pts=np.array([list(map(float,q.split(","))) for q in p[2].split()])
    eds.append((p[0],int(p[1]),pts))
out=[]; nfit=0
def ajustar(u,v,z):
    best=None
    # tejado a dos aguas con cumbrera paralela a u (z depende de |v-v0|) y a v
    for eje,(a,b) in enumerate([(u,v),(v,u)]):
        lo,hi=b.min(),b.max()
        for v0 in np.linspace(lo+0.25*(hi-lo),hi-0.25*(hi-lo),15):
            X=np.stack([np.ones_like(b),-np.abs(b-v0)],1)
            sol,res,_,_=np.linalg.lstsq(X,z,rcond=None)
            if not (0.08<sol[1]<1.3): continue
            e=np.sqrt(np.mean((X@sol-z)**2))
            if best is None or e<best[0]: best=(e,"G",eje,v0,sol[0],sol[1])
    # faldon unico (plano)
    X=np.stack([np.ones_like(u),u,v],1); sol=np.linalg.lstsq(X,z,rcond=None)[0]
    e=np.sqrt(np.mean((X@sol-z)**2))
    if best is None or e<best[0]-0.08: best=(e,"P",sol)
    return best
for id_,plantas,pts in eds:
    c=pts.mean(0)
    q=pts-c
    S=q.T@q; w,V=np.linalg.eigh(S); eu=V[:,1]; ev=np.array([-eu[1],eu[0]])
    # mascara 0.5 m
    x0,z0=pts.min(0); x1,z1=pts.max(0)
    j0=int((x0-X0)/C)-1; i0=int((z0-Z0)/C)-1; j1=int((x1-X0)/C)+2; i1=int((z1-Z0)/C)+2
    g0=suelo_en(*pts.T) if False else ndimage.map_coordinates(nod,[np.clip(pts[:,1]-Z0,0,M),np.clip(pts[:,0]-X0,0,M)],order=1)
    gmin=float(g0.min())
    rec=None
    if 0<=j0 and 0<=i0 and j1<N and i1<N:
        im=Image.new("L",(j1-j0,i1-i0),0)
        ImageDraw.Draw(im).polygon([((x-X0)/C-j0,(z-Z0)/C-i0) for x,z in pts],fill=1)
        m=np.array(im,bool)
        m=ndimage.binary_erosion(m,iterations=1) if m.sum()>12 else m
        sub=edif[i0:i1,j0:j1]
        ok=m&(sub>-1e8)&(sub>gmin+1.5)
        if ok.sum()>=8:
            ii,jj=np.nonzero(ok)
            px=X0+(jj+j0+0.5)*C; pz=Z0+(ii+i0+0.5)*C; pzv=sub[ok].astype(float)
            # quitar puntos sueltos (chimeneas, antenas)
            med=np.median(pzv); keep=pzv<np.percentile(pzv,97)+0.2
            px,pz,pzv=px[keep],pz[keep],pzv[keep]
            u=(np.stack([px,pz],1)-c)@eu; v=(np.stack([px,pz],1)-c)@ev
            rec=ajustar(u,v,pzv)
    if rec is None:
        # sin LiDAR: por plantas, dos aguas por el eje largo
        h=gmin+plantas*2.9
        uu=q@eu; vv=q@ev; half=(vv.max()-vv.min())/2
        n=ev; k=float(c@n+(vv.max()+vv.min())/2); s=np.tan(np.radians(22))
        out.append(f"{id_};{plantas};G;{n[0]:.4f};{n[1]:.4f};{k:.3f};{h+half*s:.3f};{s:.4f};{gmin:.3f};0;9")
        continue
    nfit+=1
    if rec[1]=="G":
        e,_,eje,v0,r,s=rec
        n=ev if eje==0 else eu
        base=c@n
        out.append(f"{id_};{plantas};G;{n[0]:.4f};{n[1]:.4f};{base+v0:.3f};{r:.3f};{s:.4f};{gmin:.3f};{ok.sum()};{e:.2f}")
    else:
        e,_,sol=rec
        a,bu,bv=sol
        grad=bu*eu+bv*ev; gs=np.linalg.norm(grad)
        if gs<1e-3: grad=ev; gs=1e-3
        n=-grad/gs     # z = r - s*(p.n - k): baja en la direccion n
        k=float(c@n); r=a
        out.append(f"{id_};{plantas};P;{n[0]:.4f};{n[1]:.4f};{k:.3f};{r:.3f};{gs:.4f};{gmin:.3f};{ok.sum()};{e:.2f}")
open(DATOS+"/Edificios_Alturas_LiDAR.txt","w").write("\n".join(out)+"\n")
print("edificios ajustados",nfit,"de",len(eds))
# ---------- Arboles del entorno (maximos locales del MDS de vegetacion) ----------
chm=np.where(veg>-1e8,veg-dtm05,0); chm=np.clip(chm,0,40)
chs=ndimage.gaussian_filter(chm,1.0)
mx=ndimage.maximum_filter(chs,size=9)
pk=(chs==mx)&(chs>3.0)
ii,jj=np.nonzero(pk)
arb=[]
for i,j in zip(ii,jj):
    x=X0+(j+0.5)*C; z=Z0+(i+0.5)*C
    if -2<x<194 and -2<z<194: continue   # el terreno del huerto ya tiene los suyos
    h=float(chm[max(0,i-2):i+3,max(0,j-2):j+3].max())
    # radio: distancia media a la que la copa cae por debajo de 0.45h
    rad=[]
    for di,dj in [(0,1),(1,0),(0,-1),(-1,0),(1,1),(-1,1),(1,-1),(-1,-1)]:
        k=1
        while k<24:
            a,b=i+di*k,j+dj*k
            if not(0<=a<N and 0<=b<N) or chs[a,b]<0.45*h: break
            k+=1
        rad.append(k*C*(1.414 if di and dj else 1))
    arb.append(f"{x:.2f};{z:.2f};{h:.2f};{np.median(rad):.2f}")
open(DATOS+"/Arboles_Pueblo_LiDAR.txt","w").write("\n".join(arb)+"\n")
print("arboles",len(arb))
# ---------- Mapa de superficies a 1 m (para pintar el terreno) ----------
# 0 prado seco, 1 prado verde, 2 tierra, 3 hojarasca bajo arbol, 4 empedrado, 5 bajo edificio
ed1=(edif.reshape(M,2,M,2)>-1e8).any(axis=(1,3))
mask_ed=np.zeros((M,M),bool)
im=Image.new("L",(M,M),0); dr=ImageDraw.Draw(im)
for _,_,pts in eds: dr.polygon([(x-X0,z-Z0) for x,z in pts],fill=1)
mask_ed=np.array(im,bool)|ed1
dist=ndimage.distance_transform_edt(~mask_ed)
chm1=chm.reshape(M,2,M,2).max(axis=(1,3))
rgb1=rgb.reshape(M,2,M,2,3).mean(axis=(1,3))
R,G,B=rgb1[...,0],rgb1[...,1],rgb1[...,2]
cls=np.zeros((M,M),np.uint8)
verde=(G>R*1.04)&(G>B)
cls[verde]=1
cls[(R+G+B)/3>150]=2
cls[(R>G*1.12)&((R+G+B)/3>110)]=2
canopy=ndimage.binary_dilation(chm1>2.5,iterations=1)
cls[canopy]=3
# empedrado: cerca de edificios, sin vegetacion alta y en la zona edificada
dens=ndimage.uniform_filter(mask_ed.astype(float),31)
calle=(dist<9)&(dens>0.12)&(chm1<1.5)&~mask_ed
calle=ndimage.binary_opening(calle,iterations=1)
cls[calle]=4
cls[mask_ed]=5
cls.tofile(DATOS+"/Superficies_Pueblo.bytes")
Image.fromarray((np.array([[200,180,120],[90,140,60],[150,120,90],[60,80,40],[160,160,160],[120,40,40]],np.uint8)[cls])[::-1]).save(SP+"/lidar/superficies.png")
print("superficies", np.bincount(cls.ravel(),minlength=6)/cls.size)
