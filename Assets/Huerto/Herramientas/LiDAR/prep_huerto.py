import os, sys, re, json, numpy as np
from PIL import Image
S, P = sys.argv[1], sys.argv[2]
X0, Y0, SIZE, RES = float(os.environ["HUERTO_X0"]), float(os.environ["HUERTO_Y0"]), 192.0, 1025
im = Image.open(f"{S}/catastro/mdt_big.tif")
dem = np.array(im).astype(np.float64)
sx, sy = im.tag_v2[33550][:2]; tx, ty = im.tag_v2[33922][3:5]

def blur(a, sigma):
    r = int(3 * sigma) + 1; k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2); k /= k.sum()
    a = np.pad(a, r, mode="edge")
    a = np.apply_along_axis(lambda v: np.convolve(v, k, "valid"), 0, a)
    return np.apply_along_axis(lambda v: np.convolve(v, k, "valid"), 1, a)

dem = blur(dem, 0.8)  # quita el escalonado de 1 m del MDT
# muestreo bilineal en la rejilla del terreno (fila 0 = sur, como TerrainData)
xs = X0 + np.arange(RES) * SIZE / (RES - 1); ys = Y0 + np.arange(RES) * SIZE / (RES - 1)
cx = (xs - tx) / sx - 0.5; cy = (ty - ys) / sy - 0.5
X, Y = np.meshgrid(cx, cy)
x0 = np.clip(np.floor(X).astype(int), 0, dem.shape[1] - 2); y0 = np.clip(np.floor(Y).astype(int), 0, dem.shape[0] - 2)
fx = X - x0; fy = Y - y0
h = (dem[y0, x0] * (1 - fx) * (1 - fy) + dem[y0, x0 + 1] * fx * (1 - fy) + dem[y0 + 1, x0] * (1 - fx) * fy + dem[y0 + 1, x0 + 1] * fx * fy)
h = blur(h, 8.0)
print("alturas", h.min(), h.max())
h.astype("<f4").tofile(f"{P}/Datos/Alturas_MDT05.bytes")
# parcelas en coordenadas locales (x este, z norte)
t = open(f"{S}/catastro/bbox.gml", encoding="latin-1").read()
out = {}
for m in re.finditer(r'<cp:CadastralParcel gml:id="ES\.SDGC\.CP\.([^"]+)">(.*?)</cp:CadastralParcel>', t, re.S):
    rc, b = m.groups()
    rings = []
    for pl in re.findall(r"<gml:posList[^>]*>([^<]+)", b):
        v = list(map(float, pl.split()))
        rings.append([[round(v[i] - X0, 2), round(v[i + 1] - Y0, 2)] for i in range(0, len(v), 2)])
    if any(0 <= x <= SIZE and 0 <= z <= SIZE for r in rings for x, z in r):
        out[rc] = {"area": int(re.search(r'areaValue uom="m2">(\d+)', b).group(1)), "anillos": rings}
json.dump({"origenUTM30N": [X0, Y0], "tamano": SIZE, "parcelas": out}, open(f"{P}/Datos/Parcelas_Catastro.json", "w"))
print(len(out), "parcelas")
Image.open(f"{S}/catastro/orto_terreno.jpg").save(f"{P}/Texturas/Ortofoto_PNOA.jpg", quality=92)
