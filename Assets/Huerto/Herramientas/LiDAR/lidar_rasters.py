# Rasters a partir de la nube LiDAR (coordenadas locales de la escena): MDT, MDS, alturas de vegetacion y foto RGB cenital
import sys, numpy as np
from PIL import Image, ImageDraw
S = sys.argv[1]
d = np.load(f"{S}/lidar/escena.npz")
x, z, y, c = d["x"], d["z"], d["y"], d["c"]
R = 0.5; N = int(192 / R) + 1          # rejilla 0,5 m, fila 0 = sur
def idx(xx, zz):
    i = np.clip(np.round(zz / R).astype(int), 0, N - 1); j = np.clip(np.round(xx / R).astype(int), 0, N - 1)
    return i, j
def rellenar(a, valido, iters=200):
    """Rellena huecos por difusion (convolucion normalizada)."""
    a = np.where(valido, a, 0.0); w = valido.astype(float)
    acc, wacc = a.copy(), w.copy()
    for _ in range(iters):
        if (wacc > 0).all(): break
        pa = np.pad(acc, 1, mode="edge"); pw = np.pad(wacc, 1, mode="edge")
        s = sum(np.roll(np.roll(pa, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1))[1:-1, 1:-1]
        sw = sum(np.roll(np.roll(pw, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1))[1:-1, 1:-1]
        nuevo = (wacc == 0) & (sw > 0)
        acc = np.where(nuevo, s / np.maximum(sw, 1e-9), acc); wacc = np.where(nuevo, 1.0, wacc)
    return acc
# MDT: media de puntos de suelo por celda
g = c == 2
i, j = idx(x[g], z[g])
suma = np.zeros((N, N)); cnt = np.zeros((N, N))
np.add.at(suma, (i, j), y[g]); np.add.at(cnt, (i, j), 1)
mdt = rellenar(suma / np.maximum(cnt, 1), cnt > 0)
# MDS: maximo de todo (sin ruido)
i, j = idx(x, z)
mds = np.full((N, N), -1e9); np.maximum.at(mds, (i, j), y)
mds = rellenar(np.where(mds > -1e8, mds, 0), mds > -1e8)
mds = np.maximum(mds, mdt)
# Alturas de vegetacion (clases 3,4,5) y edificios (6)
def maxclase(cl):
    m = np.isin(c, cl); i, j = idx(x[m], z[m]); a = np.full((N, N), np.nan)
    tmp = np.full((N, N), -1e9); np.maximum.at(tmp, (i, j), y[m]); a[tmp > -1e8] = tmp[tmp > -1e8]
    return a
veg = maxclase([3, 4, 5]) - mdt; edi = maxclase([6]) - mdt
# Foto cenital RGB: color del punto mas alto de cada celda de 0,25 m
R2 = 0.25; N2 = int(192 / R2)
i2 = np.clip((z / R2).astype(int), 0, N2 - 1); j2 = np.clip((x / R2).astype(int), 0, N2 - 1)
orden = np.argsort(y); rgb = np.zeros((N2, N2, 3), np.uint8)
rgb[i2[orden], j2[orden]] = np.stack([d["r"], d["g"], d["b"]], 1)[orden].clip(0, 255).astype(np.uint8)
np.savez_compressed(f"{S}/lidar/rasters.npz", mdt=mdt, mds=mds, veg=veg, edi=edi, rgb=rgb)
print("MDT", mdt.min(), mdt.max(), "celdas con suelo", (cnt > 0).mean())
