# Detecta los escalones de los bancales (muros) dentro de la parcela y los arboles reales (maximos de la altura de vegetacion)
import sys, json, numpy as np
from PIL import Image, ImageDraw
S = sys.argv[1]
r = np.load(f"{S}/lidar/rasters.npz"); mdt, veg = r["mdt"], np.nan_to_num(r["veg"], nan=0)
parcela = [(115.38,69.6),(113.54,66.94),(103.53,68.54),(93.12,69.75),(92.77,70.75),(91.17,76.29),(89.48,84.07),(87.73,95.33),(86.89,102.83),(86.51,106.2),(86.73,107.4),(87.96,114.33),(91.17,114.38),(91.72,117.42),(92.56,121.17),(98.1,120.38),(98.47,122.13),(99.08,125.03),(102.11,123.89),(100.61,119.96),(100.06,118.04),(99.86,111.39),(99.6,102.73),(99.54,100.74),(100.1,98.21),(106.09,89.5),(111.03,84.53),(115.82,78.92),(113.85,73.78),(116.36,71.02)]
def dentro(p, poly):
    x, y = p; c = False
    for i in range(len(poly)):
        x1, y1 = poly[i]; x2, y2 = poly[i - 1]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1: c = not c
    return c
def dist_borde(p, poly):
    best = 1e9
    for i in range(len(poly)):
        a = np.array(poly[i - 1]); b = np.array(poly[i]); ab = b - a
        t = np.clip(np.dot(np.array(p) - a, ab) / ab.dot(ab), 0, 1); best = min(best, np.linalg.norm(np.array(p) - (a + ab * t)))
    return best
def H(x, z):
    fx, fz = x / 0.5, z / 0.5; j, i = int(fx), int(fz); tx, tz = fx - j, fz - i
    return mdt[i, j]*(1-tx)*(1-tz) + mdt[i, j+1]*tx*(1-tz) + mdt[i+1, j]*(1-tx)*tz + mdt[i+1, j+1]*tx*tz
# --- escalones: por columnas de x, maximos de la pendiente norte-sur
puntos = []
for x in np.arange(87.0, 116.5, 0.5):
    zs = np.arange(66.0, 123.0, 0.25)
    h = np.array([H(x, z) for z in zs])
    g = -np.gradient(h, 0.25)                         # positivo si sube hacia el sur
    g = np.convolve(g, np.ones(3) / 3, "same")
    for i in range(2, len(zs) - 2):
        if g[i] > 0.55 and g[i] >= g[i - 1] and g[i] >= g[i + 1]:
            p = (float(x), float(zs[i]))
            if not dentro(p, parcela) or dist_borde(p, parcela) < 0.8: continue
            salto = H(x, zs[i] - 1.2) - H(x, zs[i] + 1.2)
            if salto > 0.4: puntos.append((p[0], p[1], float(salto)))
# --- enlazar puntos de columnas vecinas en polilineas
puntos.sort()
lineas = []
for (x, z, s) in puntos:
    mejor = None
    for L in lineas:
        lx, lz, _ = L[-1]
        if 0 < x - lx <= 1.01 and abs(z - lz) <= 1.3 and (mejor is None or abs(z - lz) < abs(z - mejor[-1][1])): mejor = L
    if mejor is not None: mejor.append((x, z, s))
    else: lineas.append([(x, z, s)])
lineas = [L for L in lineas if len(L) >= 5]
for L in lineas:
    print(f"escalon {L[0][0]:.1f},{L[0][1]:.1f} -> {L[-1][0]:.1f},{L[-1][1]:.1f}  n={len(L)}  salto medio {np.mean([p[2] for p in L]):.2f} m")
# --- arboles: maximos locales de la altura de vegetacion
N = veg.shape[0]; arboles = []
rad = 5
for i in range(rad, N - rad):
    for j in range(rad, N - rad):
        v = veg[i, j]
        if v < 2.5: continue
        win = veg[i - rad:i + rad + 1, j - rad:j + rad + 1]
        if v >= win.max():
            copa = (win > v * 0.5).sum() * 0.25
            arboles.append((j * 0.5, i * 0.5, float(v), float(np.sqrt(copa / np.pi))))
print("arboles detectados", len(arboles))
json.dump({"escalones": [[[round(x, 2), round(z, 2), round(s, 2)] for x, z, s in L] for L in lineas],
           "arboles": [[round(a, 2), round(b, 2), round(c, 2), round(d, 2)] for a, b, c, d in arboles]}, open(f"{S}/lidar/deteccion.json", "w"))
# dibujo sobre el mapa de pendiente
im = Image.open(f"{S}/lidar/pendiente.png").convert("RGB"); d = ImageDraw.Draw(im)
X0, Z1, K = 72.0, 132.0, 12
P = lambda x, z: ((x - X0) * K, (Z1 - z) * K)
for L in lineas: d.line([P(x, z) for x, z, _ in L], fill=(0, 255, 0), width=3)
for x, z, h, cr in arboles:
    if 72 < x < 132 and 56 < z < 132:
        d.ellipse([P(x - cr, z + cr), P(x + cr, z - cr)], outline=(255, 160, 0), width=2); d.text(P(x, z), f"{h:.0f}", fill=(255, 255, 0))
im.save(f"{S}/lidar/deteccion.png")
