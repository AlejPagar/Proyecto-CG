# Extrae de las 4 teselas LiDAR rejillas de 0.5 m sobre el area "medio" (local -450..574 x -300..724)
import laspy, numpy as np, pyproj, sys
SP=sys.argv[1]
t=pyproj.Transformer.from_crs(25829,25830,always_xy=True)
O=(236154.0,4486437.0); BASE=1040.0
X0,Z0,TAM,C=-450.0,-300.0,1024.0,0.5
N=int(TAM/C)
suelo_s=np.zeros((N,N)); suelo_n=np.zeros((N,N))
edif=np.full((N,N),-1e9,np.float32); veg=np.full((N,N),-1e9,np.float32)
rgb_s=np.zeros((N,N,3)); rgb_n=np.zeros((N,N))
for n in ["744-4486","744-4487","745-4486","745-4487"]:
    las=laspy.read(f"/home/pani/Documentos/UNI/CG/PNOA_2025_CYL_{n}_H29_NPC01.LAZ")
    cl=np.asarray(las.classification)
    keep=np.isin(cl,[2,3,4,5,6])
    x=np.asarray(las.x)[keep]; y=np.asarray(las.y)[keep]; z=np.asarray(las.z)[keep]-BASE; cl=cl[keep]
    r=np.asarray(las.red)[keep]; g=np.asarray(las.green)[keep]; b=np.asarray(las.blue)[keep]
    xe,yn=t.transform(x,y); xe-=O[0]; yn-=O[1]
    i=((yn-Z0)/C).astype(int); j=((xe-X0)/C).astype(int)
    ok=(i>=0)&(i<N)&(j>=0)&(j<N)
    i,j,z,cl,r,g,b=i[ok],j[ok],z[ok],cl[ok],r[ok],g[ok],b[ok]
    m=cl==2
    np.add.at(suelo_s,(i[m],j[m]),z[m]); np.add.at(suelo_n,(i[m],j[m]),1)
    sc=255.0/max(1,r.max())
    np.add.at(rgb_s,(i[m],j[m]),np.stack([r[m],g[m],b[m]],1)*sc); np.add.at(rgb_n,(i[m],j[m]),1)
    m=cl==6; np.maximum.at(edif,(i[m],j[m]),z[m].astype(np.float32))
    m=(cl>=3)&(cl<=5); np.maximum.at(veg,(i[m],j[m]),z[m].astype(np.float32))
    print(n,ok.sum(),flush=True)
suelo=np.where(suelo_n>0,suelo_s/np.maximum(suelo_n,1),np.nan)
rgb=rgb_s/np.maximum(rgb_n,1)[...,None]
np.savez_compressed(SP+"/lidar/pueblo.npz",suelo=suelo,edif=edif,veg=veg,rgb=rgb.astype(np.float32),X0=X0,Z0=Z0,C=C)
print("listo", np.isnan(suelo).mean())
