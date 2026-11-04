"""Lista o extrae parte de un .unitypackage sin importarlo entero (conserva los GUID de los .meta).
Uso:
  python3 unitypackage.py lista <paquete> [filtro]
  python3 unitypackage.py extrae <paquete> <proyecto> --base <ruta> --destino <ruta>
                                 [--incluye prefijo ...] [--excluye texto ...] [--max 2048]
Un .unitypackage es un tar.gz con una carpeta por GUID que guarda 'pathname', 'asset' y 'asset.meta'.
--base/--destino recolocan las rutas (Assets/Pack/... -> Assets/Terceros/Pack/...); --incluye son
prefijos relativos a la base; --excluye descarta rutas que contengan ese texto; --max reduce las
texturas PNG/TGA mas grandes a ese tamano y las guarda en PNG (el .meta, y con el el GUID, se conserva)."""
import io, os, sys, tarfile


def entradas(ruta):
    tar = tarfile.open(ruta, "r:gz")
    datos = {}
    for m in tar.getmembers():
        partes = m.name.lstrip("./").split("/")
        if len(partes) != 2:
            continue
        guid, nombre = partes
        d = datos.setdefault(guid, {})
        if nombre == "pathname":
            d["pathname"] = tar.extractfile(m).read().decode("utf-8").splitlines()[0].strip()
        elif nombre == "asset":
            d["asset"] = m
        elif nombre == "asset.meta":
            d["meta"] = m
    return tar, datos


def lista(ruta, filtro=""):
    tar, datos = entradas(ruta)
    total = 0
    for guid, d in sorted(datos.items(), key=lambda x: x[1].get("pathname", "")):
        p = d.get("pathname", "?")
        if filtro.lower() in p.lower():
            tam = d["asset"].size if "asset" in d else 0
            total += tam
            print(f"{tam / 1048576:8.2f} MB  {p}")
    print(f"total {total / 1048576:.1f} MB")


def reducir(datos_bytes, ext, maximo):
    """Devuelve (bytes, extension) con la textura reducida a 'maximo' px en PNG, o None si no hace falta."""
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    img = Image.open(io.BytesIO(datos_bytes))
    if max(img.size) <= maximo and ext == ".png":
        return None
    if max(img.size) > maximo:
        k = maximo / max(img.size)
        img = img.resize((max(1, round(img.size[0] * k)), max(1, round(img.size[1] * k))), Image.LANCZOS)
    if img.mode not in ("RGB", "RGBA", "L", "LA"):
        img = img.convert("RGBA")
    out = io.BytesIO()
    img.save(out, "PNG", optimize=True)
    return out.getvalue(), ".png"


def dentro(rel, prefijo):
    """La ruta esta dentro del prefijo incluido, o es una de sus carpetas padre (para conservar sus .meta)."""
    return rel == prefijo or rel.startswith(prefijo + "/") or prefijo.startswith(rel + "/")


def extrae(ruta, proyecto, base, destino, incluye, excluye, maximo):
    tar, datos = entradas(ruta)
    base = base.rstrip("/"); destino = destino.rstrip("/")
    n = bytes_total = 0
    for guid, d in datos.items():
        p = d.get("pathname", "")
        if not (p == base or p.startswith(base + "/")):
            continue
        rel = p[len(base):].lstrip("/")
        if incluye and rel and not any(dentro(rel, x.rstrip("/")) for x in incluye):
            continue
        if any(x in p for x in excluye):
            continue
        q = os.path.normpath(destino + ("/" + rel if rel else ""))
        if not q.startswith("Assets" + os.sep) or ".." in q.split(os.sep):
            print("saltado (fuera de Assets):", p)
            continue
        dst = os.path.join(proyecto, q)
        if "asset" in d:
            contenido = tar.extractfile(d["asset"]).read()
            ext = os.path.splitext(dst)[1].lower()
            if maximo and ext in (".png", ".tga"):
                r = reducir(contenido, ext, maximo)
                if r is not None:
                    contenido, nueva = r
                    dst = os.path.splitext(dst)[0] + nueva
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, "wb") as f:
                f.write(contenido)
            bytes_total += len(contenido)
        else:
            os.makedirs(dst, exist_ok=True)
        if "meta" in d:
            with open(dst + ".meta", "wb") as f:
                f.write(tar.extractfile(d["meta"]).read())
        n += 1
    print(f"extraidos {n} elementos, {bytes_total / 1048576:.1f} MB -> {destino}")


if __name__ == "__main__":
    a = sys.argv
    if a[1] == "lista":
        lista(a[2], a[3] if len(a) > 3 else "")
    elif a[1] == "extrae":
        opciones = {"--base": None, "--destino": None, "--max": "0"}
        listas = {"--incluye": [], "--excluye": []}
        actual = None
        for x in a[4:]:
            if x in opciones or x in listas:
                actual = x
            elif actual in opciones:
                opciones[actual] = x; actual = None
            elif actual in listas:
                listas[actual].append(x)
        extrae(a[2], a[3], opciones["--base"], opciones["--destino"], listas["--incluye"], listas["--excluye"], int(opciones["--max"]))
