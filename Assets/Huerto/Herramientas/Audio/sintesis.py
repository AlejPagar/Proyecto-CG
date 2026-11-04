"""Utilidades de sintesis de audio con numpy (sin scipy): filtros en frecuencia, reverberacion,
cuerda pulsada (Karplus-Strong), flauta, campanas y guardado a OGG con ffmpeg.
Los filtros se aplican multiplicando el espectro (FFT): son de fase cero y, si no se rellena con ceros,
circulares, que es justo lo que hace falta para que los bucles de ambiente empalmen sin corte."""
import os, subprocess, tempfile, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(1)


def t(dur):
    return np.arange(int(dur * SR)) / SR


def semilla(s):
    global rng
    rng = np.random.default_rng(s)


def ruido(n):
    return rng.standard_normal(n)


def respuesta(f, lo=None, hi=None, orden=2):
    """Paso banda tipo Butterworth (modulo) en las frecuencias f."""
    h = np.ones_like(f)
    with np.errstate(divide="ignore", invalid="ignore"):
        if lo:
            h *= 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-6)) ** (2 * orden))
        if hi:
            h *= 1 / np.sqrt(1 + (f / hi) ** (2 * orden))
    return h


def filtro(x, lo=None, hi=None, orden=2, circular=False, picos=()):
    """Filtra por FFT. picos: (frecuencia, anchura relativa, ganancia lineal extra)."""
    n = len(x)
    m = n if circular else 1 << int(np.ceil(np.log2(n + 2048)))
    X = np.fft.rfft(x, m)
    f = np.fft.rfftfreq(m, 1 / SR)
    h = respuesta(f, lo, hi, orden)
    for f0, q, g in picos:
        h = h * (1 + g * np.exp(-0.5 * ((f - f0) / (f0 * q)) ** 2))
    return np.fft.irfft(X * h, m)[:n]


def rosa(n, circular=True, pendiente=0.5):
    """Ruido de color: pendiente 0.5 = rosa, 1 = marron."""
    X = np.fft.rfft(ruido(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    X *= 1 / np.maximum(f, 20) ** pendiente
    y = np.fft.irfft(X, n)
    return y / (np.abs(y).max() + 1e-9)


def env(n, ataque, caida, forma=1.0):
    """Envolvente de ataque lineal y caida exponencial (tiempos en s)."""
    tt = np.arange(n) / SR
    a = np.clip(tt / max(ataque, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(tt - ataque, 0) / max(caida, 1e-4)) ** forma


def ventana(n, entra, sale):
    """Rampa de entrada y salida (s) en coseno."""
    w = np.ones(n)
    a, b = int(entra * SR), int(sale * SR)
    if a: w[:a] = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, a))
    if b: w[-b:] = 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, b))
    return w


def tono(frec, amp=None, armonicos=((1, 1.0),)):
    """Oscilador con frecuencia variable (array) integrando la fase."""
    fase = 2 * np.pi * np.cumsum(frec) / SR
    y = sum(a * np.sin(k * fase) for k, a in armonicos)
    return y if amp is None else y * amp


def mezclar(dst, src, pos, gan=1.0, circular=False):
    """Suma src en dst a partir de la muestra pos (con vuelta al principio si es circular)."""
    n = len(src)
    if circular:
        n = min(n, len(dst))
        idx = (pos + np.arange(n)) % len(dst)
        dst[idx] += src[:n] * gan
        return
    a = max(pos, 0); b = min(pos + n, len(dst))
    if b > a:
        dst[a:b] += src[a - pos:b - pos] * gan


def panorama(x, pan):
    """Mono a estereo con ley de potencia constante (pan -1 izquierda, 1 derecha)."""
    a = (pan + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def reverb(x, t60=2.2, mezcla=0.25, oscuro=5000, circular=False, predelay=0.02):
    """Reverberacion por convolucion con una respuesta de ruido decreciente (distinta en cada canal)."""
    est = x if x.ndim == 2 else np.stack([x, x], axis=1)
    n = len(est)
    largo = int(t60 * SR)
    sal = np.zeros_like(est)
    for c in range(2):
        tt = np.arange(largo) / SR
        ir = ruido(largo) * np.exp(-6.9 * tt / t60)
        ir = filtro(ir, lo=120, hi=oscuro, orden=1)
        # las colas se oscurecen: mezcla con una version mas filtrada que domina al final
        ir2 = filtro(ir, hi=oscuro * 0.35, orden=1)
        k = np.clip(tt / t60, 0, 1)
        ir = ir * (1 - k) + ir2 * k
        pd = int(predelay * SR)
        ir = np.concatenate([np.zeros(pd), ir])
        ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
        m = n if circular else 1 << int(np.ceil(np.log2(n + len(ir))))
        if circular and len(ir) > n:
            ir = ir[:n]
        y = np.fft.irfft(np.fft.rfft(est[:, c], m) * np.fft.rfft(ir, m), m)[:n]
        sal[:, c] = y
    return est * (1 - mezcla) + sal * mezcla * 1.6


def normalizar(x, pico=0.89):
    m = np.abs(x).max()
    return x * (pico / m) if m > 0 else x


def limitar(x, umbral=0.85):
    """Limitador suave (tanh) por encima del umbral."""
    s = np.sign(x); a = np.abs(x)
    sobre = a > umbral
    a[sobre] = umbral + (1 - umbral) * np.tanh((a[sobre] - umbral) / (1 - umbral))
    return s * a


def guardar(ruta, x, calidad=5):
    """Guarda en OGG Vorbis (ffmpeg) pasando por un WAV temporal de 16 bits."""
    x = np.asarray(x, dtype=np.float64)
    canales = 1 if x.ndim == 1 else x.shape[1]
    datos = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
        tmp = f.name
    with wave.open(tmp, "wb") as w:
        w.setnchannels(canales); w.setsampwidth(2); w.setframerate(SR); w.writeframes(datos)
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", str(calidad), ruta], check=True)
    os.remove(tmp)
    print(f"{ruta}  {len(x) / SR:.1f} s")


# ---------------- Instrumentos ----------------
def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def cuerda(f, dur, brillo=0.6, t60=3.5, pos=0.17, vel=1.0):
    """Cuerda pulsada (Karplus-Strong vectorizado por periodos) con el tono exacto por remuestreo.
    El lazo usa un paso bajo de tres coeficientes (0.25, 0.5, 0.25): sonido calido de cuerda de nailon."""
    N = max(4, int(round(SR / f - 1)))
    f_real = SR / (N + 1)
    salida = int(dur * SR)
    n = int(salida * f / f_real) + N + 3
    exc = rng.uniform(-1, 1, N)
    # cuanto mas suave la pulsacion, menos armonicos lleva la excitacion
    exc = filtro(exc, hi=min(f * (3 + 14 * brillo), 7000), orden=1, circular=True)
    d = max(1, int(pos * N))
    exc = exc - 0.8 * np.roll(exc, d)
    exc -= exc.mean()
    y = np.zeros(n)
    y[:N] = exc
    g = 0.001 ** (1 / (f_real * t60))
    for k in range(1, n // N + 1):
        a0 = k * N; b0 = min(a0 + N, n)
        if a0 >= n: break
        ini = max(a0, N + 2)
        if a0 < N + 2:
            for i in range(a0, ini):
                y[i] = g * (0.25 * y[i - N] + 0.5 * y[i - N - 1] + 0.25 * (y[i - N - 2] if i - N - 2 >= 0 else 0.0))
        if ini >= b0: continue
        y[ini:b0] = g * (0.25 * y[ini - N:b0 - N] + 0.5 * y[ini - N - 1:b0 - N - 1] + 0.25 * y[ini - N - 2:b0 - N - 2])
    pos_s = np.arange(salida) * f / f_real
    out = np.interp(pos_s, np.arange(n), y)
    out *= ventana(salida, 0.0008, min(0.08, dur * 0.3))
    return out * vel / (np.abs(exc).max() + 1e-9)


def flauta(f, dur, vel=1.0, vibrato=0.006, aliento=0.06):
    """Flauta dulce: seno con algo de armonicos, vibrato retardado y soplo de ruido."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    vib = 1 + vibrato * np.sin(2 * np.pi * 5.2 * tt + rng.uniform(0, 6)) * np.clip((tt - 0.25) / 0.4, 0, 1)
    fr = f * vib
    y = tono(fr, armonicos=((1, 1.0), (2, 0.22), (3, 0.09), (4, 0.03)))
    soplo = filtro(ruido(n), lo=f * 0.8, hi=f * 3.5, orden=2)
    soplo /= np.abs(soplo).max() + 1e-9
    a = np.clip(tt / 0.06, 0, 1) ** 1.5
    r = np.clip((dur - tt) / 0.12, 0, 1)
    chiff = np.exp(-tt / 0.03)
    return (y * (1 + 0.04 * np.sin(2 * np.pi * 2.3 * tt)) + soplo * (aliento + 0.25 * chiff)) * a * r * vel


def celesta(f, dur, vel=1.0):
    """Lamina metalica suave (caja de musica / celesta)."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    y = (np.sin(2 * np.pi * f * tt) * np.exp(-tt / 1.4)
         + 0.25 * np.sin(2 * np.pi * f * 2.0 * tt) * np.exp(-tt / 0.5)
         + 0.08 * np.sin(2 * np.pi * f * 4.07 * tt) * np.exp(-tt / 0.15)
         + 0.03 * np.sin(2 * np.pi * f * 9.2 * tt) * np.exp(-tt / 0.05))
    return y * np.clip(tt / 0.003, 0, 1) * ventana(n, 0, min(0.2, dur * 0.3)) * vel


def colchon(fs, dur, vel=1.0, ataque=1.5, brillo=1800):
    """Colchon de cuerdas: sierras desafinadas y filtradas para un acorde."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    y = np.zeros(n)
    for f in fs:
        for d in (-0.004, 0.0, 0.0045):
            fase = rng.uniform(0, 1)
            y += 2 * ((f * (1 + d) * tt + fase) % 1) - 1
    y = filtro(y, lo=90, hi=brillo, orden=2)
    return y / (len(fs) * 3) * ventana(n, ataque, min(2.0, dur * 0.4)) * vel


def campana(f, dur=7.0, vel=1.0):
    """Campana de iglesia: parciales inarmonicos (hum, prime, tercera menor, quinta, nominal...)."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    parciales = [(0.5, 0.55, 5.5), (1.0, 0.75, 3.5), (1.183, 0.5, 2.6), (1.506, 0.3, 2.0), (2.0, 0.55, 1.7),
                 (2.514, 0.22, 1.1), (2.662, 0.18, 0.9), (3.011, 0.14, 0.7), (4.166, 0.09, 0.45), (5.433, 0.05, 0.3)]
    y = np.zeros(n)
    for r, a, tau in parciales:
        bat = 1 + 0.15 * np.sin(2 * np.pi * rng.uniform(0.3, 1.2) * tt)   # batido de las parejas de modos
        y += a * np.sin(2 * np.pi * f * r * tt + rng.uniform(0, 6)) * np.exp(-tt / tau) * bat
    golpe = filtro(ruido(n), lo=800, hi=5000) * np.exp(-tt / 0.012) * 0.3
    return (y + golpe) * np.clip(tt / 0.002, 0, 1) * vel
