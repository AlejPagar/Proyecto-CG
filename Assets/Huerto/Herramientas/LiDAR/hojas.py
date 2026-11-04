# Texturas de hojas, hierba y plumero dibujadas por codigo (RGBA, fondo transparente)
import sys, math, random
from PIL import Image, ImageDraw, ImageFilter
OUT = sys.argv[1]
N = 1024

def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)

def leaf_poly(cx, cy, L, W, ang, kind, rnd):
    """Contorno de una hoja con la base en (cx,cy) apuntando en 'ang'."""
    pts = []
    steps = 24
    for side in (1, -1):
        rng = range(steps + 1) if side == 1 else range(steps, -1, -1)
        for i in rng:
            t = i / steps
            if kind == "lanceolada":      # castano: larga y dentada
                w = W * math.sin(math.pi * t) ** 0.8 * (1 + (0.12 if i % 2 else -0.05))
            elif kind == "oval":          # cerezo, foliolo de nogal
                w = W * math.sin(math.pi * t ** 0.85) ** 0.9
            elif kind == "lobulada":      # roble melojo
                w = W * math.sin(math.pi * t) * (0.62 + 0.38 * abs(math.sin(t * math.pi * 4.5)))
            elif kind == "hiedra":
                w = W * (math.sin(math.pi * t) ** 0.5) * (0.7 + 0.3 * abs(math.cos(t * math.pi * 2.5)))
            else:
                w = W * math.sin(math.pi * t)
            x = t * L; y = side * w
            ca, sa = math.cos(ang), math.sin(ang)
            pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return pts

def draw_leaf(d, cx, cy, L, W, ang, kind, base, rnd):
    k = rnd.uniform(0.75, 1.2)
    col = shade(base, k)
    pts = leaf_poly(cx, cy, L, W, ang, kind, rnd)
    d.polygon(pts, fill=col + (255,))
    # mitad en sombra y nervio central para dar volumen
    half = pts[: len(pts) // 2]
    d.polygon([(cx, cy)] + half, fill=shade(col, 0.82) + (255,))
    d.line([(cx, cy), (cx + L * 0.95 * math.cos(ang), cy + L * 0.95 * math.sin(ang))], fill=shade(col, 1.25) + (255,), width=max(1, int(W * 0.08)))

def cluster(name, kind, base, twig, n_twigs, leaves_per, L, W, compound=False, seed=1):
    rnd = random.Random(seed)
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    for t in range(n_twigs):
        a0 = -math.pi / 2 + rnd.uniform(-0.9, 0.9)
        x, y = N / 2 + rnd.uniform(-40, 40), N - 20
        length = rnd.uniform(0.55, 0.9) * N
        pts = [(x, y)]
        a = a0
        segs = 12
        for s in range(segs):
            a += rnd.uniform(-0.12, 0.12)
            x += math.cos(a) * length / segs; y += math.sin(a) * length / segs
            pts.append((x, y))
        d.line(pts, fill=twig + (255,), width=6)
        for i in range(leaves_per):
            s = rnd.uniform(0.15, 1.0)
            idx = min(segs, int(s * segs)); px, py = pts[idx]
            side = 1 if i % 2 else -1
            la = a0 + side * rnd.uniform(0.5, 1.3)
            sc = rnd.uniform(0.7, 1.1)
            if compound:  # hoja compuesta (nogal): raquis con foliolos
                rl = L * 2.2 * sc
                ex, ey = px + math.cos(la) * rl, py + math.sin(la) * rl
                d.line([(px, py), (ex, ey)], fill=twig + (255,), width=3)
                for j in range(7):
                    tt = 0.25 + 0.75 * j / 6
                    qx, qy = px + (ex - px) * tt, py + (ey - py) * tt
                    for sd in (1, -1) if j < 6 else (0,):
                        fa = la + sd * 1.0
                        draw_leaf(d, qx, qy, L * 0.75 * sc, W * 0.75 * sc, fa if sd else la, "oval", base, rnd)
            else:
                draw_leaf(d, px, py, L * sc, W * sc, la, kind, base, rnd)
    im = im.filter(ImageFilter.SMOOTH)
    # rellenar el color de los pixeles transparentes para evitar halos al filtrar
    bg = Image.new("RGBA", im.size, shade(base, 0.6) + (0,))
    bg.alpha_composite(im)
    bg.save(f"{OUT}/{name}.png")

cluster("Hojas_Nogal", "oval", (130, 168, 64), (90, 80, 60), 5, 9, 70, 26, compound=True, seed=3)
cluster("Hojas_Castano", "lanceolada", (94, 142, 54), (85, 70, 50), 6, 26, 190, 42, seed=4)
cluster("Hojas_Cerezo", "oval", (106, 152, 58), (90, 60, 50), 6, 30, 120, 44, seed=5)
cluster("Hojas_Roble", "lobulada", (90, 124, 50), (80, 70, 55), 7, 34, 115, 40, seed=6)
cluster("Hojas_Hiedra", "hiedra", (64, 104, 42), (60, 70, 40), 8, 40, 80, 46, seed=7)
cluster("Hojas_Arbusto", "oval", (98, 140, 54), (80, 70, 50), 8, 40, 70, 30, seed=8)

def grass(name, cols, n, hmin, hmax, seed, plume=False):
    rnd = random.Random(seed)
    im = Image.new("RGBA", (512, 512), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    for i in range(n):
        x = 256 + rnd.gauss(0, 70); hgt = rnd.uniform(hmin, hmax) * 512
        bend = rnd.uniform(-0.35, 0.35); w = rnd.uniform(3, 7)
        c = rnd.choice(cols); pts = []
        for s in range(11):
            t = s / 10
            pts.append((x + bend * hgt * t * t, 511 - hgt * t))
        for s in range(10):
            ww = w * (1 - s / 10) + 1
            d.line([pts[s], pts[s + 1]], fill=shade(c, 0.75 + 0.45 * s / 10) + (255,), width=int(ww))
        if plume and rnd.random() < 0.5:
            tx, ty = pts[-1]
            for k in range(60):
                yy = ty + rnd.uniform(0, 0.25 * hgt); xx = tx + rnd.gauss(0, 6) + bend * 20
                d.line([(xx, yy), (xx + rnd.uniform(-10, 10), yy - rnd.uniform(6, 16))], fill=(230, 215, 180, 255), width=2)
    bg = Image.new("RGBA", im.size, shade(cols[0], 0.7) + (0,)); bg.alpha_composite(im)
    bg.save(f"{OUT}/{name}.png")

grass("Hierba_Seca", [(196, 170, 112), (176, 150, 96), (210, 190, 140), (150, 130, 80)], 70, 0.35, 0.95, 11)
grass("Hierba_Verde", [(110, 120, 60), (130, 135, 70), (95, 105, 50)], 60, 0.3, 0.8, 12)

# Plumero (hierba de la Pampa): espiga crema plumosa
rnd = random.Random(13)
im = Image.new("RGBA", (256, 1024), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for k in range(1400):
    t = rnd.random(); y = 40 + t * 700; w = 70 * math.sin(math.pi * min(1, t * 1.1)) ** 0.7
    x = 128 + rnd.gauss(0, w / 2)
    c = shade((228, 214, 182), rnd.uniform(0.8, 1.08))
    d.line([(x, y), (x + rnd.uniform(-12, 12), y + rnd.uniform(10, 26))], fill=c + (255,), width=2)
d.line([(128, 700), (128, 1023)], fill=(170, 160, 120, 255), width=6)
bg = Image.new("RGBA", im.size, (200, 190, 160, 0)); bg.alpha_composite(im); bg.save(f"{OUT}/Plumero_Espiga.png")
print("ok")
