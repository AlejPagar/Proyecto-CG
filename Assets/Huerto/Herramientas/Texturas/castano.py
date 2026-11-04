"""Hojas y amentos del castano comun (Castanea sativa) para las tarjetas de hojas de los castanos de la Asset Store,
que traen hoja palmeada y flor de castano de Indias. Mismo encuadre que las originales (2048 px, el peciolo
abajo en el centro y todo en la mitad inferior), asi encajan en las mismas tarjetas.
Uso: python3 castano.py <carpeta de salida>"""
import math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 2048
OUT = sys.argv[1]
rng = random.Random(11)


def hoja(d, x0, y0, ang, largo, ancho, color, nervio):
    """Hoja lanceolada con el borde aserrado (dientes agudos) desde (x0, y0) hacia 'ang' (0 = arriba)."""
    n = 90
    pts = []
    for lado in (1, -1):
        idx = range(n + 1) if lado == 1 else range(n, -1, -1)
        for i in idx:
            t = i / n
            w = ancho * 0.5 * math.sin(math.pi * min(1, t * 1.05) ** 0.75) * (1 - 0.25 * t)
            if 0.06 < t < 0.97:
                diente = (t * 22) % 1.0          # un diente cada 1/22 del largo
                w *= 1 + 0.16 * (diente if diente < 0.75 else (1 - diente) * 3)
            x, y = lado * w, t * largo
            pts.append((x0 + x * math.cos(ang) + y * math.sin(ang), y0 + x * math.sin(ang) - y * math.cos(ang)))
    d.polygon(pts, fill=color)
    tx, ty = x0 + largo * math.sin(ang), y0 - largo * math.cos(ang)
    d.line([(x0, y0), (tx, ty)], fill=nervio, width=max(3, int(ancho * 0.04)))
    # nervios secundarios paralelos y rectos, muy marcados en el castano
    for k in range(1, 15):
        t = k / 16
        bx, by = x0 + t * largo * math.sin(ang), y0 - t * largo * math.cos(ang)
        L = ancho * 0.45 * math.sin(math.pi * t ** 0.8)
        for lado in (1, -1):
            a2 = ang + lado * 0.85
            d.line([(bx, by), (bx + L * math.sin(a2), by - L * math.cos(a2))], fill=nervio, width=2)


def ramita(d, puntos, grosor, color):
    for (a, b) in zip(puntos, puntos[1:]):
        d.line([a, b], fill=color, width=grosor)


def acabar(img, nombre):
    a = np.asarray(img).astype(np.float32)
    # variacion de tono y sombreado suave de la base (mas oscura) a la punta
    ruido = np.random.default_rng(5).normal(0, 1, (S // 16, S // 16))
    ruido = np.asarray(Image.fromarray(((ruido - ruido.min()) / np.ptp(ruido) * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC)).astype(np.float32) / 255 - 0.5
    a[..., :3] += ruido[..., None] * 22
    a[..., :3] *= np.linspace(1.12, 0.86, S)[:, None, None]
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.SMOOTH)
    img.save(f"{OUT}/{nombre}.png")


VERDE = [(58, 98, 34, 255), (66, 108, 38, 255), (52, 90, 30, 255)]
NERVIO = (96, 132, 62, 255)
MADERA = (92, 70, 50, 255)

# 1. Ramita con hojas alternas (sustituye a la ramita de hojas palmeadas)
img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
cx = S / 2
eje = [(cx + 20 * math.sin(i * 0.7), S - 1 - i * 105) for i in range(10)]
ramita(d, eje, 16, MADERA)
for i, (x, y) in enumerate(eje[1:9]):
    lado = 1 if i % 2 == 0 else -1
    ang = lado * rng.uniform(0.75, 1.1) + rng.uniform(-0.1, 0.1)
    largo = rng.uniform(470, 580) * (1 - i * 0.04)
    hoja(d, x, y, ang, largo, largo * 0.27, VERDE[i % 3], NERVIO)
hoja(d, eje[-1][0], eje[-1][1], rng.uniform(-0.15, 0.15), 500, 135, VERDE[0], NERVIO)
acabar(img, "Castano_Hoja_1")

# 2. Ramillete de hojas que salen del mismo punto (sustituye a la hoja palmeada suelta)
img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
x0, y0 = 1075, S - 60
ramita(d, [(1075, S - 1), (x0, y0)], 14, MADERA)
for k, ang in enumerate((-0.95, -0.45, 0.02, 0.5, 0.98)):
    largo = 650 - abs(ang) * 120
    hoja(d, x0, y0, ang + rng.uniform(-0.06, 0.06), largo, largo * 0.26, VERDE[k % 3], NERVIO)
acabar(img, "Castano_Hoja_2")

# 3. Amentos: espigas finas color crema que salen en junio entre las hojas (sustituye a la flor de castano de Indias)
img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
x0, y0 = 1020, S - 40
ramita(d, [(1020, S - 1), (x0, y0)], 14, MADERA)
for k, ang in enumerate((-0.7, 0.75)):
    hoja(d, x0, y0, ang, 470, 125, VERDE[k], NERVIO)
for k in range(9):
    ang = -0.55 + k * 0.14 + rng.uniform(-0.05, 0.05)
    largo = rng.uniform(560, 760)
    pasos = 80
    for i in range(pasos):
        t = i / pasos
        curva = 0.18 * t * t * (1 if ang > 0 else -1)      # se arquean un poco hacia fuera
        a = ang + curva
        x = x0 + t * largo * math.sin(a); y = y0 - t * largo * math.cos(a)
        r = 13 * (1 - 0.35 * t)
        for j in range(3):
            ox, oy = rng.uniform(-r, r), rng.uniform(-r, r)
            c = (232 + rng.randint(-12, 10), 214 + rng.randint(-15, 10), 140 + rng.randint(-20, 20), 255)
            d.ellipse([x + ox - r * 0.55, y + oy - r * 0.55, x + ox + r * 0.55, y + oy + r * 0.55], fill=c)
acabar(img, "Castano_Amento")
print("ok")
