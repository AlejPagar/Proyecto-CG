"""Musica original del huerto, compuesta y sintetizada aqui (sin muestras de terceros).
Uso: python3 musica.py <carpeta de salida>
  1. El huerto de los abuelos: vals en Re mayor, guitarra y flauta dulce (melodia escrita a mano).
  2. Tarde de verano: La menor, guitarra, celesta y colchon de cuerdas (melodia generada por frases).
  3. Recuerdos: Sol mayor en 6/8, guitarra sola con la melodia en las cuerdas agudas."""
import sys
import numpy as np
import sintesis as S
from sintesis import SR, midi

OUT = sys.argv[1]

# acordes: bajo y tres notas de arriba (voicings de guitarra)
ACORDES = {
    "D": (50, [57, 62, 66]), "G": (43, [55, 59, 62]), "A": (45, [57, 61, 64]), "Bm": (47, [54, 59, 62]),
    "Em": (40, [55, 59, 64]), "F#m": (42, [54, 57, 61]), "A/C#": (49, [57, 61, 64]), "A7": (45, [55, 61, 64]),
    "Am": (45, [57, 60, 64]), "F": (41, [53, 57, 60]), "C": (48, [55, 60, 64]), "Dm": (50, [57, 62, 65]),
    "E": (40, [56, 59, 64]), "E7": (40, [56, 59, 62]), "D7": (50, [57, 60, 66]),
}


class Pista:
    def __init__(self, dur):
        self.x = np.zeros((int(dur * SR), 2))

    def poner(self, y, tiempo, pan=0.0, gan=1.0):
        S.mezclar(self.x, S.panorama(y, pan), int(tiempo * SR), gan)


def humano(r, t, v, dt=0.008, dv=0.1):
    return t + r.uniform(-dt, dt), v * (1 + r.uniform(-dv, dv))


def guitarra_arpegio(p, r, acordes, compas, patron, t0, vel=0.5, bajo=0.85, pan=-0.15, brillo=0.55):
    """Acompanamiento punteado: patron = lista de (tiempo en el compas, indice: -1 bajo, 0..2 notas)."""
    for i, nombre in enumerate(acordes):
        if nombre is None: continue
        partes = nombre.split()   # "Em A": dos acordes en el compas
        for tt, idx in patron:
            ac = partes[min(len(partes) - 1, int(tt / compas * len(partes)))]
            b, notas = ACORDES[ac]
            n = b if idx < 0 else notas[idx]
            t, v = humano(r, t0 + i * compas + tt, bajo if idx < 0 else vel)
            dur = compas - tt + 1.2
            p.poner(S.cuerda(midi(n), dur, brillo=brillo if idx >= 0 else 0.4, t60=4.0 if idx < 0 else 3.0, vel=v), t, pan + (0 if idx < 0 else 0.1 * idx))


def colchon(p, acordes, compas, t0, gan=0.12, bajar=12):
    for i, nombre in enumerate(acordes):
        if nombre is None: continue
        b, notas = ACORDES[nombre.split()[0]]
        fs = [midi(n + 12 - bajar) for n in notas]
        p.poner(S.colchon(fs, compas + 1.6, ataque=0.9), t0 + i * compas - 0.3, 0.0, gan)


def melodia(p, notas, t0, negra, instrumento, pan=0.1, gan=1.0, r=None):
    t = t0
    for n, d in notas:
        if n is not None:
            tt, v = humano(r, t, 1.0, 0.006, 0.08)
            p.poner(instrumento(midi(n), d * negra * 1.02 + 0.05, v), tt, pan, gan)
        t += d * negra
    return t


def masterizar(p, nombre, t60=2.0, mezcla=0.22, cola=4.0):
    x = S.reverb(p.x, t60=t60, mezcla=mezcla, oscuro=6000)
    x = S.filtro(x[:, 0], lo=35, hi=15000), S.filtro(x[:, 1], lo=35, hi=15000)
    x = np.stack(x, axis=1)
    x *= S.ventana(len(x), 0.01, cola)[:, None]
    x = S.limitar(S.normalizar(x, 0.95), 0.8) * 0.9
    S.guardar(f"{OUT}/{nombre}.ogg", x, calidad=5)


# =============== 1. El huerto de los abuelos (vals, Re mayor) ===============
def huerto_abuelos():
    S.semilla(11); r = np.random.default_rng(11)
    negra = 60 / 92; compas = 3 * negra
    intro = ["D", "G", "A", "D"]
    A = ["D", "D", "G", "D", "Bm", "Em", "A", "D"]
    A2 = ["D", "A/C#", "Bm", "G", "D", "Em A", "D", "D"]
    B = ["G", "A", "F#m", "Bm", "G", "D", "Em", "A7"]
    final = ["G", "A7", "D", "D"]
    acordes = intro + A + A2 + B + A + final
    mel_A = [(78, 1), (76, .5), (74, .5), (69, 1), (74, 1.5), (76, .5), (78, 1), (79, 1), (78, .5), (76, .5), (74, 1), (78, 2), (74, 1),
             (83, 1), (81, .5), (78, .5), (74, 1), (79, 1.5), (78, .5), (76, 1), (73, 1), (76, 1), (81, 1), (74, 3)]
    mel_A2 = [(78, 1), (76, .5), (74, .5), (69, 1), (76, 1.5), (74, .5), (73, 1), (74, 1), (78, 1), (83, 1), (81, 2), (79, 1),
              (78, 1), (76, .5), (78, .5), (81, 1), (79, 1), (76, 1), (73, 1), (74, 3), (None, 3)]
    mel_B = [(74, 1), (79, 1), (83, 1), (81, 2), (76, 1), (78, 1), (81, 1), (85, 1), (83, 3),
             (83, 1), (81, .5), (79, .5), (78, 1), (78, 1), (76, .5), (74, .5), (78, 1), (79, 1.5), (78, .5), (76, 1), (76, 1), (73, 1), (69, 1)]
    mel_fin = [(79, 1), (78, .5), (76, .5), (74, 1), (76, 1.5), (73, .5), (69, 1), (74, 6)]
    total = len(acordes) * compas + 6
    p = Pista(total)
    patron = [(0, -1), (negra * 0.5, 0), (negra, 1), (negra * 1.5, 2), (negra * 2, 1), (negra * 2.5, 0)]
    guitarra_arpegio(p, r, acordes, compas, patron, 0.5)
    t = 0.5 + len(intro) * compas
    fl = lambda f, d, v: S.flauta(f, d, v * 0.8)
    t = melodia(p, mel_A, t, negra, fl, 0.12, 0.55, r)
    colchon(p, A2, compas, t, 0.07)
    t = melodia(p, mel_A2, t, negra, fl, 0.12, 0.55, r)
    t = melodia(p, mel_B, t, negra, fl, 0.12, 0.6, r)
    colchon(p, A, compas, t, 0.09)
    t0 = t
    t = melodia(p, mel_A, t, negra, fl, 0.12, 0.55, r)
    melodia(p, [(n + 12 if n else None, d) for n, d in mel_A], t0, negra, lambda f, d, v: S.celesta(f, d + 1.0, v), -0.3, 0.12, r)
    melodia(p, mel_fin, t, negra, fl, 0.12, 0.5, r)
    # acorde final rasgueado lento
    tf = 0.5 + (len(acordes) - 2) * compas
    for k, n in enumerate([38, 45, 50, 57, 62, 66]):
        p.poner(S.cuerda(midi(n), 5.0, brillo=0.45, t60=5.0, vel=0.55), tf + 0.06 * k, -0.2 + 0.08 * k)
    masterizar(p, "Musica_El_huerto_de_los_abuelos")


# =============== 2. Tarde de verano (La menor, 4/4) ===============
ESCALA_LAm = [57, 59, 60, 62, 64, 65, 67, 68, 69, 71, 72, 74, 76, 77, 79, 81, 83, 84, 86, 88]


def frase(r, acordes, escala, lo, hi, inicio, ritmos, final_largo=True, largo=4):
    """Melodia sencilla por compas: tiempo fuerte en nota del acorde, el resto por grados conjuntos."""
    notas = []; n = inicio
    for i, ac in enumerate(acordes):
        b, tonos = ACORDES[ac.split()[0]]
        clases = {x % 12 for x in tonos + [b]}
        ritmo = [largo] if (final_largo and i == len(acordes) - 1) else ritmos[r.integers(len(ritmos))]
        for k, d in enumerate(ritmo):
            if k == 0:
                cands = [x for x in range(lo, hi + 1) if x % 12 in clases]
                n = min(cands, key=lambda x: abs(x - n) + r.uniform(0, 3))
            else:
                j = min(range(len(escala)), key=lambda q: abs(escala[q] - n))
                j = int(np.clip(j + r.choice([-2, -1, -1, 1, 1, 2]), 0, len(escala) - 1))
                n = int(np.clip(escala[j], lo, hi))
            notas.append((n, d))
    return notas


def tarde_verano():
    S.semilla(23); r = np.random.default_rng(23)
    negra = 60 / 76; compas = 4 * negra
    A = ["Am", "F", "C", "G", "Am", "F", "Dm", "E"]
    B = ["F", "G", "Em", "Am", "Dm", "G", "C", "E7"]
    acordes = A + A + B + A + ["Am", "Am"]
    p = Pista(len(acordes) * compas + 7)
    patron = [(0, -1), (negra * 0.5, 0), (negra, 1), (negra * 1.5, 2), (negra * 2, -1), (negra * 2.5, 1), (negra * 3, 2), (negra * 3.5, 0)]
    guitarra_arpegio(p, r, acordes, compas, patron, 0.5, vel=0.42, bajo=0.7, brillo=0.45)
    ritmos = [[2, 2], [1, 1, 2], [3, 1], [1.5, 0.5, 2], [2, 1, 1], [1, 1, 1, 1]]
    fA = frase(r, A[:4], ESCALA_LAm, 72, 86, 76, ritmos, False) + frase(r, A[4:], ESCALA_LAm, 69, 84, 76, ritmos)
    fB = frase(r, B[:4], ESCALA_LAm, 74, 88, 81, ritmos, False) + frase(r, B[4:], ESCALA_LAm, 71, 86, 79, ritmos)
    cel = lambda f, d, v: S.celesta(f, d + 1.4, v)
    t = 0.5 + 8 * compas
    t = melodia(p, fA, t, negra, cel, 0.25, 0.32, r)
    colchon(p, B + A, compas, t, 0.09)
    t = melodia(p, fB, t, negra, cel, 0.25, 0.32, r)
    t = melodia(p, fA, t, negra, lambda f, d, v: S.flauta(f / 2, d, v * 0.7), 0.15, 0.45, r)
    p.poner(S.celesta(midi(81), 4.0, 0.8), t + 0.2, 0.25, 0.3)
    masterizar(p, "Musica_Tarde_de_verano", t60=2.6, mezcla=0.28)


# =============== 3. Recuerdos (Sol mayor, 6/8, guitarra sola) ===============
ESCALA_G = [55, 57, 59, 60, 62, 64, 66, 67, 69, 71, 72, 74, 76, 78, 79, 81]


def recuerdos():
    S.semilla(37); r = np.random.default_rng(37)
    corchea = 60 / 150; compas = 6 * corchea
    A = ["G", "Em", "C", "D", "G", "Bm", "C D", "G"]
    B = ["Em", "C", "G", "D", "Em", "Am", "D", "D7"]
    acordes = ["G", "G"] + A + B + A + ["C", "G"]
    p = Pista(len(acordes) * compas + 7)
    patron = [(0, -1), (corchea, 0), (corchea * 2, 1), (corchea * 3, 2), (corchea * 4, 1), (corchea * 5, 0)]
    guitarra_arpegio(p, r, acordes, compas, patron, 0.5, vel=0.36, bajo=0.7, pan=-0.05, brillo=0.4)
    # melodia en la guitarra (cuerdas agudas), en corcheas: negra con puntillo = 3
    ritmos = [[3, 3], [2, 1, 3], [3, 2, 1], [1, 1, 1, 3]]
    fA = frase(r, [a.split()[0] for a in A], ESCALA_G, 67, 81, 71, ritmos, largo=6)
    fB = frase(r, B, ESCALA_G, 64, 79, 76, ritmos, largo=6)
    gtr = lambda f, d, v: S.cuerda(f, d + 1.5, brillo=0.6, t60=3.0, vel=v * 0.95)
    t = 0.5 + 2 * compas
    t = melodia(p, fA, t, corchea, gtr, 0.2, 0.75, r)
    colchon(p, B, compas, t, 0.06)
    t = melodia(p, fB, t, corchea, gtr, 0.2, 0.75, r)
    t = melodia(p, fA, t, corchea, gtr, 0.2, 0.75, r)
    for k, n in enumerate([43, 50, 55, 59, 62, 67]):
        p.poner(S.cuerda(midi(n), 5.0, brillo=0.4, t60=5.0, vel=0.5), t + compas + 0.07 * k, -0.2 + 0.08 * k)
    masterizar(p, "Musica_Recuerdos", t60=2.3, mezcla=0.25)


if __name__ == "__main__":
    huerto_abuelos()
    tarde_verano()
    recuerdos()
