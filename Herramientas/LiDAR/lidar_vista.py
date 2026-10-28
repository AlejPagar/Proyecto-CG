# Paneles de la zona del huerto: relieve sombreado del MDT, vegetacion y foto cenital, con la parcela y los muros actuales
import sys, numpy as np
from PIL import Image, ImageDraw
S = sys.argv[1]
r = np.load(f"{S}/lidar/rasters.npz")
mdt, veg, rgb = r["mdt"], r["veg"], r["rgb"]
X0, X1, Z0, Z1, K = 72.0, 132.0, 56.0, 132.0, 12   # zona y escala (px por metro)
parcela = [(115.38,69.6),(113.54,66.94),(103.53,68.54),(93.12,69.75),(92.77,70.75),(91.17,76.29),(89.48,84.07),(87.73,95.33),(86.89,102.83),(86.51,106.2),(86.73,107.4),(87.96,114.33),(91.17,114.38),(91.72,117.42),(92.56,121.17),(98.1,120.38),(98.47,122.13),(99.08,125.03),(102.11,123.89),(100.61,119.96),(100.06,118.04),(99.86,111.39),(99.6,102.73),(99.54,100.74),(100.1,98.21),(106.09,89.5),(111.03,84.53),(115.82,78.92),(113.85,73.78),(116.36,71.02)]
muros = [[(86.4,99.5),(101,99.5)],[(88.3,83.9),(107.8,89.5)],[(90.6,77.5),(115.7,77.5)]]
def px(xx, zz): return ((xx - X0) * K, (Z1 - zz) * K)
def recorte(a, res):
    i0, i1 = int(Z0 / res), int(Z1 / res); j0, j1 = int(X0 / res), int(X1 / res)
    return a[i0:i1, j0:j1][::-1]
def dibujar(im, rejilla=True):
    d = ImageDraw.Draw(im)
    if rejilla:
        for v in range(int(X0), int(X1) + 1, 5): d.line([px(v, Z0), px(v, Z1)], fill=(255, 255, 255, 60) if v % 10 else (255, 255, 255, 140), width=1)
        for v in range(int(Z0), int(Z1) + 1, 5): d.line([px(X0, v), px(X1, v)], fill=(255, 255, 255, 60) if v % 10 else (255, 255, 255, 140), width=1)
        for v in range(int(X0) // 10 * 10 + 10, int(X1), 10): d.text((px(v, Z1)[0] + 2, 2), str(v), fill=(255, 255, 0))
        for v in range(int(Z0) // 10 * 10 + 10, int(Z1), 10): d.text((2, px(X0, v)[1] + 2), str(v), fill=(255, 255, 0))
    d.line([px(*p) for p in parcela + [parcela[0]]], fill=(255, 40, 40), width=3)
    for m in muros: d.line([px(*p) for p in m], fill=(0, 200, 255), width=2)
    return im
# 1) relieve sombreado (sol del noroeste) + curvas de 0,5 m
a = recorte(mdt, 0.5)
gy, gx = np.gradient(a, 0.5)
som = np.clip(0.5 + (-gx * 0.7 + gy * 0.7) * 1.4, 0, 1)
curvas = (np.floor(a / 0.5) % 2) * 0.12
img = Image.fromarray((np.stack([som + curvas] * 3, -1).clip(0, 1) * 255).astype(np.uint8)).resize((int((X1 - X0) * K), int((Z1 - Z0) * K)), Image.BICUBIC).convert("RGBA")
dibujar(img).save(f"{S}/lidar/relieve.png")
# 2) pendiente (donde hay muros/escalones la pendiente se dispara)
pend = np.degrees(np.arctan(np.hypot(gx, gy)))
pi = Image.fromarray((np.clip(pend / 50, 0, 1) * 255).astype(np.uint8)).resize(img.size, Image.BICUBIC).convert("RGBA")
dibujar(pi).save(f"{S}/lidar/pendiente.png")
# 3) foto cenital del LiDAR (color de los puntos)
fo = Image.fromarray(recorte(rgb, 0.25)).resize(img.size, Image.NEAREST).convert("RGBA")
dibujar(fo).save(f"{S}/lidar/cenital.png")
# 4) altura de la vegetacion
v = np.nan_to_num(recorte(veg, 0.5), nan=0)
col = np.zeros(v.shape + (3,)); t = np.clip(v / 15, 0, 1)
col[..., 0] = t; col[..., 1] = np.clip(1.4 * t, 0, 1) * (v > 0.3); col[..., 2] = (v > 0.3) * 0.2
vi = Image.fromarray((col * 255).astype(np.uint8)).resize(img.size, Image.NEAREST).convert("RGBA")
dibujar(vi).save(f"{S}/lidar/vegetacion.png")
print(img.size)
