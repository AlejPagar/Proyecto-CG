# Quita los arboles cuyo tronco cae dentro de un edificio (o a menos de 1.5 m): copas de patios mal situadas
import numpy as np, sys
from PIL import Image, ImageDraw
from scipy import ndimage
D=sys.argv[1]
X0,Z0,N=-450.0,-300.0,2048
im=Image.new("L",(N,N),0); dr=ImageDraw.Draw(im)
for l in open(D+"/Edificios_Catastro.txt"):
    p=l.strip().split(";")
    pts=[tuple(map(float,q.split(","))) for q in p[2].split()]
    dr.polygon([((x-X0)*2,(z-Z0)*2) for x,z in pts],fill=1)
m=np.array(im,bool)
dist=ndimage.distance_transform_edt(~m)/2
L=[l for l in open(D+"/Arboles_Pueblo_LiDAR.txt") if l.strip()]
out=[]
for l in L:
    x,z,h,r=map(float,l.split(";"))
    i,j=int((z-Z0)*2),int((x-X0)*2)
    if 0<=i<N and 0<=j<N and dist[i,j]<1.5: continue
    out.append(l.strip())
open(D+"/Arboles_Pueblo_LiDAR.txt","w").write("\n".join(out)+"\n")
print(len(L),"->",len(out))
