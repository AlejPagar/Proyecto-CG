"""Texturas de hojas, flores y numeros de los carteles de los bancales (RGBA, fondo transparente).
Uso: python3 plantas.py <carpeta de salida>
Las hojas se dibujan con la base del peciolo abajo en el centro: en Unity la tarjeta se ancla en el tallo por v = 0."""
import math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 512
OUT = sys.argv[1]
rng = random.Random(7)


def lienzo():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def ruido(img, fuerza=18, escala=6):
    a = np.asarray(img).astype(np.float32)
    n = np.random.default_rng(3).normal(0, 1, (S // escala + 1, S // escala + 1))
    n = np.asarray(Image.fromarray(((n - n.min()) / (n.max() - n.min()) * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC)).astype(np.float32) / 255 - 0.5
    a[..., :3] += n[..., None] * fuerza
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def foliolo(d, cx, cy, ang, largo, ancho, color, dentado=0.0, ondas=0.0, nervio=None):
    """Hoja ovada desde (cx, cy) hacia el angulo 'ang' (radianes, 0 = arriba)."""
    pts = []
    n = 60
    for lado in (1, -1):
        rango = range(n + 1) if lado == 1 else range(n, -1, -1)
        for i in rango:
            t = i / n
            w = math.sin(math.pi * t ** 0.8) * ancho * 0.5 * (1 + ondas * math.sin(t * 40))
            if dentado and 0.1 < t < 0.95:
                w *= 1 - dentado * (0.5 + 0.5 * math.sin(t * 70))
            x = lado * w
            y = t * largo
            px = cx + x * math.cos(ang) + y * math.sin(ang)
            py = cy + x * math.sin(ang) - y * math.cos(ang)
            pts.append((px, py))
    d.polygon(pts, fill=color)
    if nervio:
        tx, ty = cx + largo * math.sin(ang), cy - largo * math.cos(ang)
        d.line([(cx, cy), (tx, ty)], fill=nervio, width=max(2, int(ancho * 0.05)))
        for k in range(1, 7):
            t = k / 7.5
            bx, by = cx + t * largo * math.sin(ang), cy - t * largo * math.cos(ang)
            for lado in (1, -1):
                a2 = ang + lado * 0.9
                L = ancho * 0.42 * math.sin(math.pi * t)
                d.line([(bx, by), (bx + L * math.sin(a2), by - L * math.cos(a2))], fill=nervio, width=max(1, int(ancho * 0.02)))


def sombrear(img, claro=1.12, oscuro=0.8):
    """Degradado suave del peciolo a la punta y un poco de volumen."""
    a = np.asarray(img).astype(np.float32)
    a[..., :3] *= np.linspace(claro, oscuro, S)[:, None, None]   # punta (arriba) mas clara
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def guardar(img, nombre):
    img = ruido(img)
    img = img.filter(ImageFilter.SMOOTH)
    img.save(f"{OUT}/{nombre}.png")


# ---- Tomate / patata: hoja compuesta con foliolos dentados ----
for nombre, verde, nerv in [("Hoja_Compuesta", (62, 112, 44, 255), (110, 150, 80, 255)), ("Hoja_Patata", (70, 118, 52, 255), (120, 160, 90, 255))]:
    img = lienzo(); d = ImageDraw.Draw(img)
    cx = S / 2
    d.line([(cx, S), (cx, 40)], fill=(80, 120, 50, 255), width=10)
    for k, t in enumerate([0.3, 0.52, 0.72]):
        y = S - t * (S - 40)
        for lado in (1, -1):
            L = 150 * (1.05 - t * 0.35)
            foliolo(d, cx, y, lado * 1.15 + rng.uniform(-0.1, 0.1), L, L * 0.55, verde, dentado=0.12 if nombre == "Hoja_Compuesta" else 0.0, nervio=nerv)
    foliolo(d, cx, 120, 0, 110, 70, verde, dentado=0.12 if nombre == "Hoja_Compuesta" else 0.0, nervio=nerv)
    guardar(sombrear(img), nombre)

# ---- Pimiento: hoja simple lisa y brillante ----
img = lienzo(); d = ImageDraw.Draw(img)
d.line([(S / 2, S), (S / 2, S - 70)], fill=(70, 110, 45, 255), width=12)
foliolo(d, S / 2, S - 60, 0, S - 90, 250, (48, 104, 38, 255), nervio=(96, 150, 70, 255))
guardar(sombrear(img, 1.2, 0.85), "Hoja_Simple")

# ---- Judia: hoja trifoliada ----
img = lienzo(); d = ImageDraw.Draw(img)
d.line([(S / 2, S), (S / 2, S - 200)], fill=(80, 120, 50, 255), width=10)
for a in (-1.0, 1.0):
    foliolo(d, S / 2, S - 190, a, 210, 150, (74, 126, 50, 255), nervio=(120, 165, 85, 255))
foliolo(d, S / 2, S - 200, 0, 300, 200, (74, 126, 50, 255), nervio=(120, 165, 85, 255))
guardar(sombrear(img), "Hoja_Trifoliada")

# ---- Lechuga: hoja ancha y ondulada, clara en la base ----
img = lienzo(); d = ImageDraw.Draw(img)
foliolo(d, S / 2, S - 4, 0, S - 20, 380, (128, 182, 72, 255), ondas=0.05, nervio=(205, 225, 160, 255))
a = np.asarray(img).astype(np.float32)
y = np.linspace(0, 1, S)[:, None]
blanco = np.clip((y - 0.55) / 0.45, 0, 1)[..., None]
a[..., :3] = a[..., :3] * (1 - blanco * 0.45) + np.array([215, 230, 170]) * blanco * 0.45
guardar(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)), "Hoja_Lechuga")

# ---- Calabaza: hoja palmeada de cinco lobulos ----
img = lienzo(); d = ImageDraw.Draw(img)
d.line([(S / 2, S), (S / 2, S - 150)], fill=(90, 130, 60, 255), width=12)
for a in (-1.25, -0.62, 0, 0.62, 1.25):
    L = 300 if a == 0 else 250 if abs(a) < 1 else 190
    foliolo(d, S / 2, S - 140, a, L, L * 0.75, (66, 112, 46, 255), dentado=0.06, nervio=(140, 175, 110, 255))
guardar(sombrear(img), "Hoja_Calabaza")

# ---- Berza: hoja grande verde azulada con nervios blancos ----
img = lienzo(); d = ImageDraw.Draw(img)
foliolo(d, S / 2, S - 4, 0, S - 16, 400, (86, 128, 108, 255), ondas=0.03, nervio=(200, 215, 200, 255))
guardar(sombrear(img), "Hoja_Berza")

# ---- Cebolla: hoja tubular (tira verde con brillo) ----
img = lienzo(); d = ImageDraw.Draw(img)
for i in range(S):
    t = i / S
    w = 70 * (1 - t ** 1.5) + 6
    d.line([(S / 2 - w, S - i), (S / 2 + w, S - i)], fill=(78, 128, 60, 255))
a = np.asarray(img).astype(np.float32)
x = np.abs(np.linspace(-1, 1, S))[None, :]
a[..., :3] *= (1.25 - 0.5 * x)[..., None]
guardar(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)), "Hoja_Cebolla")

# ---- Flores: estrella de cinco petalos ----
for nombre, petalo, centro in [("Flor_Amarilla", (245, 205, 40, 255), (200, 140, 20, 255)), ("Flor_Blanca", (245, 245, 238, 255), (230, 200, 60, 255))]:
    img = lienzo(); d = ImageDraw.Draw(img)
    for k in range(5):
        a = k / 5 * 2 * math.pi
        foliolo(d, S / 2, S / 2, a, S * 0.46, S * 0.3, petalo)
    d.ellipse([S / 2 - 40, S / 2 - 40, S / 2 + 40, S / 2 + 40], fill=centro)
    guardar(img, nombre)

# ---- Numeros de los carteles (blanco con borde oscuro) ----
fuente = None
for f in ["/usr/share/fonts/TTF/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/noto/NotoSans-Bold.ttf", "/usr/share/fonts/liberation/LiberationSans-Bold.ttf"]:
    try:
        fuente = ImageFont.truetype(f, 380); break
    except OSError:
        pass
for n in range(1, 10):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.text((S / 2, S / 2), str(n), font=fuente, fill=(240, 236, 225, 255), anchor="mm", stroke_width=14, stroke_fill=(30, 24, 18, 255))
    img.save(f"{OUT}/Numero_{n}.png")
print("ok")
