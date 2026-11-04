"""Efectos y ambiente del huerto, sintetizados (sin grabaciones de terceros).
Uso: python3 sonidos.py <carpeta Assets/Huerto/Audio>
Ambiente/: bucles que empalman sin corte (todo se genera de forma circular) y cantos sueltos de aves y campanas.
Efectos/: pisadas por tipo de suelo, salto, aterrizaje, trepar y los sonidos de la tableta y de las tareas del huerto."""
import sys
import numpy as np
import sintesis as S
from sintesis import SR

OUT = sys.argv[1]
AMB, EFE = f"{OUT}/Ambiente", f"{OUT}/Efectos"


def n_(d):
    return int(d * SR)


def granos(n, cantidad, lo, hi, largo=0.002, amp_exp=1.0, env_t=None, circular=False):
    """Crujidos: muchos impulsos cortos de ruido filtrado repartidos al azar (con densidad segun env_t)."""
    y = np.zeros(n)
    g = n_(largo) + 1
    if env_t is not None:
        p = env_t / env_t.sum()
        pos = S.rng.choice(n, cantidad, p=p)
    else:
        pos = S.rng.integers(0, n, cantidad)
    for q in pos:
        imp = S.ruido(g) * np.exp(-np.arange(g) / (g / 3)) * S.rng.exponential(1.0) ** amp_exp
        S.mezclar(y, imp, int(q), 1.0, circular)
    return S.filtro(y, lo=lo, hi=hi, orden=2, circular=circular)


def golpe(n, f0=90, caida=0.04, ruido_hi=400):
    """Golpe sordo del talon: seno grave que baja de tono mas un poco de ruido grave."""
    tt = np.arange(n) / SR
    fr = f0 * (1 + 0.8 * np.exp(-tt / 0.01))
    s = S.tono(fr) * np.exp(-tt / caida)
    r = S.filtro(S.ruido(n), hi=ruido_hi) * np.exp(-tt / (caida * 0.6))
    return s * 0.7 + r / (np.abs(r).max() + 1e-9) * 0.5


def acabar(y, pico=0.9, entra=0.0005, sale=0.02):
    y = y * S.ventana(len(y), entra, sale)
    return S.normalizar(y, pico)


# ======================= PISADAS =======================
def pisada(sup, k, fuerte=1.0):
    S.semilla(100 + k * 7 + sum(map(ord, sup)))
    n = n_(0.42)
    tt = np.arange(n) / SR
    # dos contactos: talon y, ~70 ms despues, la planta
    d2 = n_(S.rng.uniform(0.055, 0.085))
    y = np.zeros(n)
    if sup == "hierba":
        for off, a in ((0, 1.0), (d2, 0.7)):
            m = n - off
            e = S.env(m, 0.012, 0.07)
            sw = S.filtro(S.ruido(m), lo=1200, hi=7000) * e
            cr = granos(m, 140, 2500, 9000, 0.0015, 1.5, e)
            th = golpe(m, 75, 0.03, 300)
            S.mezclar(y, sw / (np.abs(sw).max() + 1e-9) * 0.45 + cr / (np.abs(cr).max() + 1e-9) * 0.35 + th * 0.35 * fuerte, off, a)
    elif sup == "tierra":
        for off, a in ((0, 1.0), (d2, 0.8)):
            m = n - off
            e = S.env(m, 0.004, 0.06)
            cr = granos(m, 420, 600, 6000, 0.0012, 1.8, e)
            th = golpe(m, 85, 0.035, 500)
            S.mezclar(y, cr / (np.abs(cr).max() + 1e-9) * 0.6 + th * 0.55 * fuerte, off, a)
    elif sup == "hojarasca":
        for off, a in ((0, 1.0), (d2, 0.9)):
            m = n - off
            e = S.env(m, 0.01, 0.11)
            cr = granos(m, 260, 1800, 11000, 0.0025, 2.2, e)
            sw = S.filtro(S.ruido(m), lo=2500, hi=9000) * S.env(m, 0.02, 0.09)
            th = golpe(m, 70, 0.03, 300)
            S.mezclar(y, cr / (np.abs(cr).max() + 1e-9) * 0.65 + sw / (np.abs(sw).max() + 1e-9) * 0.2 + th * 0.3 * fuerte, off, a)
    else:  # piedra: golpe seco con resonancia y un arenilla
        for off, a in ((0, 1.0), (d2, 0.75)):
            m = n - off
            tt2 = np.arange(m) / SR
            click = S.filtro(S.ruido(m), lo=700, hi=6000, picos=((1400 + S.rng.uniform(-200, 300), 0.15, 3.0),)) * np.exp(-tt2 / 0.012)
            gr = granos(m, 60, 2000, 8000, 0.001, 1.5, S.env(m, 0.002, 0.04))
            th = golpe(m, 110, 0.02, 800)
            S.mezclar(y, click / (np.abs(click).max() + 1e-9) * 0.7 + gr / (np.abs(gr).max() + 1e-9) * 0.25 + th * 0.4 * fuerte, off, a)
    return acabar(y, 0.85 * min(1.0, fuerte))


for sup in ("hierba", "tierra", "hojarasca", "piedra"):
    for k in range(5):
        S.guardar(f"{EFE}/Pisada_{sup}_{k + 1}.ogg", pisada(sup, k), 5)
    for k in range(2):
        S.guardar(f"{EFE}/Aterrizaje_{sup}_{k + 1}.ogg", pisada(sup, 10 + k, 1.6) * 1.0, 5)

# ======================= SALTO Y TREPAR =======================
S.semilla(5)
n = n_(0.35); tt = np.arange(n) / SR
whoosh = S.filtro(S.ruido(n), lo=300, hi=2500) * np.sin(np.pi * np.clip(tt / 0.3, 0, 1)) ** 2
S.guardar(f"{EFE}/Salto.ogg", acabar(whoosh, 0.6), 5)

n = n_(1.1); tt = np.arange(n) / SR
mano = S.filtro(S.ruido(n), lo=200, hi=3000) * np.exp(-tt / 0.02)
raspado = S.filtro(S.ruido(n), lo=500, hi=5000) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 7 * tt))) * S.ventana(n, 0.15, 0.3) * np.clip((tt - 0.1) / 0.1, 0, 1)
gr = granos(n, 300, 1500, 7000, 0.001, 1.5, S.ventana(n, 0.2, 0.3) + 0.01)
y = mano / np.abs(mano).max() * 0.8 + raspado / np.abs(raspado).max() * 0.35 + gr / np.abs(gr).max() * 0.3
y += np.concatenate([np.zeros(n_(0.55)), golpe(n - n_(0.55), 80, 0.04)]) * 0.5   # rodilla arriba
S.guardar(f"{EFE}/Trepar.ogg", acabar(y, 0.75), 5)

# ======================= TABLETA =======================
S.semilla(9)


def campanita(fs, separacion, dur=0.9, vol=0.5):
    y = np.zeros(n_(dur + separacion * len(fs)))
    for i, f in enumerate(fs):
        nota = S.celesta(f, dur, 1.0)
        S.mezclar(y, nota, n_(i * separacion), 1.0)
    return y * vol


def clic(f=2200, dur=0.06, cuerpo=0.4):
    n = n_(dur); tt = np.arange(n) / SR
    c = S.filtro(S.ruido(n), lo=1500, hi=8000) * np.exp(-tt / 0.0025)
    s = np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.012) * cuerpo
    return acabar(c / np.abs(c).max() * 0.6 + s, 0.7, 0.0002, 0.01)


n = n_(0.9); tt = np.arange(n) / SR
sube = S.filtro(S.ruido(n), lo=600, hi=5000) * np.sin(np.pi * np.clip(tt / 0.25, 0, 1)) ** 2 * 0.15
y = sube + np.concatenate([np.zeros(n_(0.12)), campanita([S.midi(88), S.midi(95)], 0.09, 0.6, 0.35)])[:n]
S.guardar(f"{EFE}/Tableta_abrir.ogg", acabar(y, 0.6), 5)
y = clic(1600, 0.05) * 0.5
y = np.concatenate([y, np.zeros(n_(0.6))]); S.mezclar(y, campanita([S.midi(90), S.midi(83)], 0.08, 0.5, 0.3), n_(0.02))
S.guardar(f"{EFE}/Tableta_cerrar.ogg", acabar(y, 0.5), 5)
S.guardar(f"{EFE}/Clic.ogg", clic(2300, 0.05, 0.35) * 0.6, 5)
S.guardar(f"{EFE}/Pestana.ogg", clic(1700, 0.07, 0.6) * 0.6, 5)
y = np.zeros(n_(0.12)); S.mezclar(y, clic(2600, 0.04, 0.3), 0); S.mezclar(y, clic(1900, 0.05, 0.3), n_(0.045))
S.guardar(f"{EFE}/Interruptor.ogg", acabar(y, 0.55), 5)
S.guardar(f"{EFE}/Aviso_bueno.ogg", acabar(campanita([S.midi(84), S.midi(88), S.midi(91)], 0.11, 1.0, 0.5), 0.55), 5)
S.guardar(f"{EFE}/Aviso_malo.ogg", acabar(campanita([S.midi(81), S.midi(77)], 0.18, 1.1, 0.5), 0.5), 5)

# ======================= TAREAS DEL HUERTO =======================
# Regar: chorro de la regadera sobre la tierra con burbujas
S.semilla(21)
n = n_(2.2); tt = np.arange(n) / SR
forma = S.ventana(n, 0.15, 0.5)
chorro = S.filtro(S.ruido(n), lo=400, hi=7000, picos=((900, 0.4, 1.5),)) * forma
burb = np.zeros(n)
for _ in range(160):
    f0 = S.rng.uniform(500, 2200); m = n_(S.rng.uniform(0.02, 0.06)); t2 = np.arange(m) / SR
    b = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 2.5 * t2 / t2[-1])) / SR) * np.exp(-t2 / (t2[-1] / 3))
    S.mezclar(burb, b, int(S.rng.uniform(0.1, 1.8) * SR), S.rng.uniform(0.2, 1.0))
salpica = granos(n, 900, 1500, 9000, 0.0015, 1.5, forma + 0.01)
y = chorro / np.abs(chorro).max() * 0.35 + burb / np.abs(burb).max() * 0.35 + salpica / np.abs(salpica).max() * 0.3
S.guardar(f"{EFE}/Regar.ogg", acabar(S.reverb(y, 0.6, 0.12)[:, 0], 0.7, 0.01, 0.3), 5)

# Cosechar: tiron entre las hojas, la pieza que se suelta y cae en la cesta, y una campanita alegre
S.semilla(22)
n = n_(1.6); tt = np.arange(n) / SR
hojas = granos(n, 500, 1500, 9000, 0.002, 2, S.env(n, 0.05, 0.12) + 1e-4)
pop = S.tono(220 * (1 + 1.5 * np.exp(-tt / 0.008))) * np.exp(-tt / 0.03)
cesta = np.zeros(n); S.mezclar(cesta, golpe(n_(0.3), 120, 0.05, 900) + granos(n_(0.3), 80, 1000, 5000, 0.002, 1.5, S.env(n_(0.3), 0.003, 0.05)) * 3, n_(0.32))
y = hojas / np.abs(hojas).max() * 0.4 + np.concatenate([np.zeros(n_(0.12)), pop[:n - n_(0.12)]]) * 0.6 + cesta * 0.5
S.mezclar(y, campanita([S.midi(79), S.midi(86)], 0.1, 0.8, 0.45), n_(0.45))
S.guardar(f"{EFE}/Cosechar.ogg", acabar(y, 0.7, 0.002, 0.2), 5)

# Plantar: dos paladas en la tierra
S.semilla(23)
n = n_(1.3); y = np.zeros(n)
for off in (0.0, 0.55):
    m = n_(0.5); e = S.env(m, 0.003, 0.08)
    pala = S.filtro(S.ruido(m), lo=1500, hi=6000, picos=((2600, 0.08, 4),)) * np.exp(-np.arange(m) / SR / 0.01)
    t_ = granos(m, 500, 300, 4000, 0.002, 1.8, e)
    S.mezclar(y, pala / np.abs(pala).max() * 0.4 + t_ / np.abs(t_).max() * 0.6 + golpe(m, 70, 0.05) * 0.4, n_(off))
S.guardar(f"{EFE}/Plantar.ogg", acabar(y, 0.75, 0.001, 0.1), 5)

# ======================= AMBIENTE (bucles circulares) =======================
L = 60.0
N = n_(L)
tt = np.arange(N) / SR


def lfo(ciclos, fase=0.0):
    """Oscilacion lenta con un numero entero de ciclos en el bucle (empalma)."""
    return np.sin(2 * np.pi * ciclos * tt / L + fase)


# Viento suave entre los arboles: rumor grave y hojas que se mueven en las rachas
S.semilla(31)
canales = []
for c in range(2):
    rach = 0.55 + 0.25 * lfo(3, c) + 0.15 * lfo(7, 1 + c) + 0.1 * lfo(13, 2.5 * c)
    rach = np.clip(rach, 0.1, 1.2)
    grave = S.filtro(S.rosa(N, pendiente=1.0), lo=40, hi=420, circular=True)
    hojas = S.filtro(S.ruido(N), lo=1800, hi=8000, circular=True)
    temblor = S.filtro(S.ruido(N), hi=6, circular=True)
    hojas *= 0.6 + 0.4 * np.abs(temblor) / np.abs(temblor).max()
    canales.append(grave / np.abs(grave).max() * rach * 0.8 + hojas / np.abs(hojas).max() * rach ** 2.5 * 0.18)
y = np.stack(canales, axis=1)
S.guardar(f"{AMB}/Amb_Viento.ogg", S.normalizar(y, 0.7), 4)

# Lluvia sobre las hojas y la tierra (30 s)
S.semilla(32)
Nl = n_(30)
canales = []
for c in range(2):
    base = S.filtro(S.rosa(Nl, pendiente=0.3), lo=300, hi=10000, circular=True)
    gotas = np.zeros(Nl)
    for _ in range(9000):
        m = n_(S.rng.uniform(0.002, 0.008)); g = S.ruido(m) * np.exp(-np.arange(m) / (m / 4))
        S.mezclar(gotas, g, int(S.rng.integers(0, Nl)), S.rng.exponential(0.5), circular=True)
    gotas = S.filtro(gotas, lo=1200, hi=9000, circular=True)
    plic = np.zeros(Nl)
    for _ in range(260):
        m = n_(0.03); t2 = np.arange(m) / SR; f0 = S.rng.uniform(1200, 3500)
        S.mezclar(plic, np.sin(2 * np.pi * np.cumsum(f0 * (1 + 0.6 * t2 / t2[-1])) / SR) * np.exp(-t2 / 0.006), int(S.rng.integers(0, Nl)), S.rng.uniform(0.2, 1), circular=True)
    canales.append(base / np.abs(base).max() * 0.5 + gotas / np.abs(gotas).max() * 0.45 + plic / np.abs(plic).max() * 0.15)
S.guardar(f"{AMB}/Amb_Lluvia.ogg", S.normalizar(np.stack(canales, axis=1), 0.8), 4)

# Chicharras (cigarras) al calor del verano: zumbido de clics muy rapidos en frases que suben y bajan
S.semilla(33)
y = np.zeros((N, 2))
for k in range(6):
    fpulso = S.rng.uniform(180, 260); fc = S.rng.uniform(4500, 7000)
    portadora = S.filtro(S.ruido(N), lo=fc * 0.75, hi=fc * 1.3, orden=3, circular=True)
    pulso = (0.5 + 0.5 * np.sin(2 * np.pi * np.round(fpulso * L) / L * tt)) ** 4
    frases = np.clip(0.5 + 0.7 * np.sin(2 * np.pi * S.rng.integers(2, 6) * tt / L + S.rng.uniform(0, 6)), 0, 1) ** 1.5
    v = portadora / np.abs(portadora).max() * pulso * frases * S.rng.uniform(0.4, 1.0)
    y += S.panorama(v, S.rng.uniform(-0.8, 0.8))
S.guardar(f"{AMB}/Amb_Chicharras.ogg", S.normalizar(S.reverb(y, 1.0, 0.25, circular=True), 0.6), 4)

# Grillos: chirridos de 3-4 pulsos de un tono puro hacia 4,5 kHz
S.semilla(34)
y = np.zeros((N, 2))
for k in range(5):
    f0 = S.rng.uniform(4200, 4900); periodo = S.rng.uniform(0.45, 0.8); pul = S.rng.integers(3, 5)
    ch = np.zeros(n_(0.15)); t2 = np.arange(len(ch)) / SR
    for i in range(pul):
        m = n_(0.016); tp = np.arange(m) / SR
        S.mezclar(ch, np.sin(2 * np.pi * f0 * tp) * np.sin(np.pi * tp / tp[-1]) ** 2, n_(i * 0.03))
    v = np.zeros(N); t0 = S.rng.uniform(0, periodo)
    while t0 < L:
        S.mezclar(v, ch, n_(t0), S.rng.uniform(0.7, 1.0), circular=True)
        t0 += periodo * S.rng.uniform(0.95, 1.05)
    v *= 0.6 + 0.4 * (0.5 + 0.5 * lfo(S.rng.integers(1, 4), S.rng.uniform(0, 6)))
    y += S.panorama(v * S.rng.uniform(0.25, 1.0), S.rng.uniform(-0.9, 0.9))
S.guardar(f"{AMB}/Amb_Grillos.ogg", S.normalizar(S.reverb(y, 1.2, 0.3, circular=True), 0.5), 4)


# ======================= AVES (cantos sueltos, mono para sonar en 3D) =======================
def silaba(f_ini, f_fin, dur, forma="lin", arm=0.15, curva=None):
    n = n_(dur); t2 = np.linspace(0, 1, n)
    if curva is not None:
        fr = curva(t2)
    elif forma == "lin":
        fr = f_ini + (f_fin - f_ini) * t2
    else:
        fr = f_ini + (f_fin - f_ini) * np.sin(np.pi * t2)
    am = np.sin(np.pi * t2) ** 1.5
    return S.tono(fr, am, ((1, 1.0), (2, arm)))


def canto(silabas, pre=0.05):
    return np.concatenate([np.zeros(n_(pre))] + silabas + [np.zeros(n_(0.4))])


def ave(nombre, y, lo=800):
    y = S.filtro(y, lo=lo, hi=11000)
    S.guardar(f"{AMB}/Ave_{nombre}.ogg", acabar(y, 0.8, 0.001, 0.05), 5)


S.semilla(41)
# Mirlo: frases aflautadas y variadas entre 1,6 y 3 kHz, con un final chirriante
for k in range(3):
    sil = []
    for i in range(S.rng.integers(5, 8)):
        a = S.rng.uniform(1600, 2800); b = a * S.rng.uniform(0.75, 1.3)
        sil.append(silaba(a, b, S.rng.uniform(0.08, 0.2), "arco" if S.rng.random() < 0.4 else "lin", 0.1))
        sil.append(np.zeros(n_(S.rng.uniform(0.02, 0.07))))
    for i in range(S.rng.integers(3, 6)):
        a = S.rng.uniform(5000, 7000)
        sil.append(silaba(a, a * 0.7, 0.025, arm=0.05) * 0.4); sil.append(np.zeros(n_(0.015)))
    ave(f"Mirlo_{k + 1}", canto(sil))
# Gorrion: "chip" repetidos
for k in range(2):
    sil = []
    for i in range(S.rng.integers(4, 8)):
        f = S.rng.uniform(3300, 4200)
        sil.append(silaba(f, f, 0.07, curva=lambda x, f=f: f * (1 + 0.25 * np.sin(np.pi * x) - 0.3 * x)) * S.rng.uniform(0.6, 1))
        sil.append(np.zeros(n_(S.rng.uniform(0.12, 0.3))))
    ave(f"Gorrion_{k + 1}", canto(sil))
# Jilguero: gorjeo rapido y tintineante
for k in range(2):
    sil = []
    for i in range(S.rng.integers(18, 28)):
        a = S.rng.uniform(3000, 7000)
        sil.append(silaba(a, a * S.rng.uniform(0.7, 1.4), S.rng.uniform(0.025, 0.06), arm=0.05) * S.rng.uniform(0.5, 1))
        sil.append(np.zeros(n_(S.rng.uniform(0.005, 0.03))))
    ave(f"Jilguero_{k + 1}", canto(sil))
# Carbonero: "ti-chu ti-chu ti-chu"
sil = []
for i in range(5):
    sil += [silaba(6200, 5800, 0.07, arm=0.05), np.zeros(n_(0.03)), silaba(3900, 3500, 0.09, arm=0.05), np.zeros(n_(0.08))]
ave("Carbonero", canto(sil))
# Cuco: "cu-cu" (tercera mayor descendente) repetido, hueco y aflautado
sil = []
for i in range(4):
    sil += [silaba(690, 650, 0.2, arm=0.08) * 1.0, np.zeros(n_(0.07)), silaba(550, 520, 0.32, arm=0.08) * 0.85, np.zeros(n_(0.55))]
ave("Cuco", canto(sil), 300)
# Paloma torcaz: "cu-cuuu-cu, cu-cu" grave y algo ronco
sil = []
for rep in range(2):
    for d, f in ((0.25, 430), (0.55, 470), (0.25, 430), (0.2, 410), (0.25, 410)):
        v = silaba(f, f * 0.93, d, "arco", 0.35)
        v = v + S.filtro(S.ruido(len(v)), lo=300, hi=900) * np.sin(np.pi * np.linspace(0, 1, len(v))) * 0.08
        sil += [v, np.zeros(n_(0.12))]
    sil.append(np.zeros(n_(0.8)))
ave("Torcaz", canto(sil), 250)
# Abubilla: "up-up-up" grave y hueco
sil = []
for rep in range(3):
    for i in range(3):
        sil += [silaba(560, 500, 0.11, "arco", 0.05), np.zeros(n_(0.12))]
    sil.append(np.zeros(n_(0.9)))
ave("Abubilla", canto(sil), 250)

# Fondo de pajaros lejanos (bucle circular de 60 s)
S.semilla(42)
y = np.zeros((N, 2))
for _ in range(110):
    tipo = S.rng.random()
    if tipo < 0.5:
        a = S.rng.uniform(2500, 6000); v = silaba(a, a * S.rng.uniform(0.6, 1.4), S.rng.uniform(0.04, 0.12), arm=0.05)
    else:
        v = np.concatenate([np.concatenate([silaba(f, f * 0.8, 0.04, arm=0.05), np.zeros(n_(0.03))]) for f in S.rng.uniform(3000, 6500, S.rng.integers(3, 9))])
    v = S.filtro(v, lo=2000, hi=8000)
    pos = int(S.rng.integers(0, N))
    pan = S.rng.uniform(-0.9, 0.9)
    est = S.panorama(v, pan) * S.rng.uniform(0.15, 0.6)
    idx = (pos + np.arange(len(est))) % N
    y[idx] += est
S.guardar(f"{AMB}/Amb_Pajaros.ogg", S.normalizar(S.reverb(y, 1.6, 0.45, circular=True, oscuro=4000), 0.5), 4)

# Campanas de la iglesia (un toque de cada campana; el juego las repite)
S.semilla(51)
S.guardar(f"{AMB}/Campana_grave.ogg", acabar(S.campana(S.midi(57), 7.0), 0.85, 0.001, 0.5), 5)
S.guardar(f"{AMB}/Campana_aguda.ogg", acabar(S.campana(S.midi(64), 6.0), 0.85, 0.001, 0.5), 5)
print("ok")
