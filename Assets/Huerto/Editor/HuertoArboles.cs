using System.Collections.Generic;
using UnityEngine;

namespace Huerto
{
    /// <summary>Arboles procedurales: tronco y ramas recursivas con corteza, y tarjetas de hojas en las puntas.</summary>
    public static class Arboles
    {
        public class Especie
        {
            public string nombre, corteza, hojas;
            public float fuste = 2.2f;          // altura del tronco limpio (fraccion de la altura total)
            public float radio = 0.045f;        // radio del tronco por metro de altura
            public int principales = 4;         // ramas madre que salen de la cruz
            public float aperturaPrincipal = 48f;
            public int niveles = 3, hijos = 4;
            public float apertura = 42f, ratioLargo = 0.62f;
            public float gravedad = 0.05f, luz = 0.06f, tortuosidad = 0.14f;
            public float hoja = 0.9f; public int hojasPorRama = 7;
            public bool seco = false, guia = true;
            public int tallos = 1;
            public float largoPrincipal = 0.62f;
        }

        public static Especie Get(string nombre)
        {
            switch (nombre)
            {
                case "Nogal": return new Especie { nombre = nombre, corteza = "Nogal", hojas = "Nogal", fuste = 0.28f, radio = 0.04f, principales = 4, aperturaPrincipal = 55f, hijos = 4, apertura = 45f, ratioLargo = 0.6f, gravedad = 0.08f, hoja = 1.15f, hojasPorRama = 7, largoPrincipal = 0.66f };
                case "Castano": return new Especie { nombre = nombre, corteza = "Castano", hojas = "Castano", fuste = 0.22f, radio = 0.045f, principales = 5, aperturaPrincipal = 42f, hijos = 4, apertura = 40f, ratioLargo = 0.6f, hoja = 1.0f, hojasPorRama = 9, tallos = 2, largoPrincipal = 0.6f };
                case "Cerezo": return new Especie { nombre = nombre, corteza = "Cerezo", hojas = "Cerezo", fuste = 0.3f, radio = 0.032f, principales = 4, aperturaPrincipal = 36f, hijos = 3, apertura = 38f, ratioLargo = 0.62f, gravedad = 0.03f, luz = 0.1f, hoja = 0.8f, hojasPorRama = 8, largoPrincipal = 0.62f };
                case "Roble": return new Especie { nombre = nombre, corteza = "Roble", hojas = "Roble", fuste = 0.32f, radio = 0.042f, principales = 5, aperturaPrincipal = 45f, hijos = 4, apertura = 44f, ratioLargo = 0.58f, tortuosidad = 0.22f, hoja = 1.15f, hojasPorRama = 8, largoPrincipal = 0.6f };
                case "RobleSeco": return new Especie { nombre = nombre, corteza = "Seco", hojas = null, fuste = 0.35f, radio = 0.04f, principales = 4, aperturaPrincipal = 40f, hijos = 3, apertura = 40f, ratioLargo = 0.55f, tortuosidad = 0.3f, seco = true, largoPrincipal = 0.6f };
                case "Tuya": return new Especie { nombre = nombre, corteza = "Castano", hojas = "Arbusto", fuste = 0.05f, radio = 0.025f, principales = 14, aperturaPrincipal = 22f, niveles = 2, hijos = 3, apertura = 35f, ratioLargo = 0.5f, luz = 0.15f, hoja = 0.75f, hojasPorRama = 10, largoPrincipal = 0.45f };
                default: return new Especie { nombre = "Joven", corteza = "Cerezo", hojas = "Arbusto", fuste = 0.35f, radio = 0.02f, principales = 3, aperturaPrincipal = 32f, niveles = 2, hijos = 3, apertura = 38f, ratioLargo = 0.6f, luz = 0.12f, hoja = 0.65f, hojasPorRama = 6, largoPrincipal = 0.55f };
            }
        }

        class Ctx
        {
            public Especie e; public System.Random r; public MallaBuilder mb; public Vector3 centroCopa; public float alto;
            public int detalle, hojas;
        }

        // Niveles de detalle: el esqueleto y las tiradas aleatorias son siempre las mismas (la silueta no salta al cambiar de LOD);
        // solo se emite menos geometria: ramas finas fuera, menos lados y tramos, y una de cada N hojas mas grande.
        static readonly int[] NivelMaxTubo = { 9, 2, 1 };
        static readonly int[] PasoTramo = { 1, 2, 4 };
        static readonly int[] PasoHoja = { 1, 3, 10 };

        public static Mesh Generar(Especie e, float alto, int semilla, string nombreMalla, int detalle = 0)
        {
            var c = new Ctx { e = e, r = new System.Random(semilla * 7919 + 13), mb = new MallaBuilder(2), alto = alto, detalle = detalle };
            c.centroCopa = new Vector3(0, alto * (e.fuste + (1 - e.fuste) * 0.5f), 0);
            for (int t = 0; t < e.tallos; t++)
            {
                float az = t * 2.4f + Util.Rng(c.r, 0, 1f);
                Vector3 inclin = e.tallos > 1 ? new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az)) * 0.25f : Vector3.zero;
                Vector3 basePos = inclin * 0.6f;
                float radio = alto * e.radio * (e.tallos > 1 ? 0.7f : 1f);
                Vector3 dir = (Vector3.up + inclin + Aleatorio(c.r) * 0.06f).normalized;
                float largoFuste = alto * e.fuste * Util.Rng(c.r, 0.9f, 1.1f);
                var tronco = Rama(c, basePos, dir, largoFuste, radio, radio * 0.82f, 0, false);
                Vector3 cruz = tronco[tronco.Count - 1];
                int n = e.principales;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + Util.Rng(c.r, -0.25f, 0.25f)) / n * Mathf.PI * 2f + az;
                    Vector3 horiz = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float ap = (e.aperturaPrincipal + Util.Rng(c.r, -10f, 10f)) * Mathf.Deg2Rad;
                    Vector3 d = (Vector3.up * Mathf.Cos(ap) + horiz * Mathf.Sin(ap)).normalized;
                    float L = (alto - largoFuste) * e.largoPrincipal * Util.Rng(c.r, 0.85f, 1.1f);
                    Vector3 inicio = cruz - dir * Util.Rng(c.r, 0f, 0.15f) * largoFuste;
                    Rama(c, inicio, d, L, radio * 0.62f, radio * 0.2f, 1, true);
                }
                if (e.guia)
                    Rama(c, cruz, (Vector3.up + Aleatorio(c.r) * 0.15f).normalized, (alto - largoFuste) * 0.7f, radio * 0.6f, radio * 0.15f, 1, true);
                // Tuya: ramas cortas a lo largo de todo el tronco
                if (e.nombre == "Tuya")
                    for (int i = 0; i < 26; i++)
                    {
                        float h = Util.Rng(c.r, 0.1f, 0.85f) * alto;
                        float a = i * 2.4f;
                        Vector3 d = (new Vector3(Mathf.Cos(a), 0.6f, Mathf.Sin(a))).normalized;
                        Rama(c, new Vector3(0, h, 0), d, (alto - h) * 0.3f + 0.4f, radio * 0.35f, radio * 0.1f, 1, true);
                    }
            }
            return c.mb.Construir(nombreMalla);
        }

        static Vector3 Aleatorio(System.Random r) => new Vector3(Util.Rng(r, -1, 1), Util.Rng(r, -1, 1), Util.Rng(r, -1, 1));

        static List<Vector3> Rama(Ctx c, Vector3 inicio, Vector3 dir, float largo, float r0, float r1, int nivel, bool hijos)
        {
            var e = c.e;
            int segs = Mathf.Clamp(Mathf.CeilToInt(largo / 0.45f), 2, 12);
            var pts = new List<Vector3>(); var rad = new List<float>(); var dirs = new List<Vector3>();
            Vector3 p = inicio, d = dir.normalized;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs;
                pts.Add(p); rad.Add(Mathf.Max(0.012f, Mathf.Lerp(r0, r1, t))); dirs.Add(d);
                d = (d + Aleatorio(c.r) * e.tortuosidad + Vector3.down * e.gravedad * nivel + Vector3.up * e.luz).normalized;
                p += d * largo / segs;
            }
            int lados = nivel == 0 ? 10 : nivel == 1 ? 7 : nivel == 2 ? 5 : 4;
            if (c.detalle == 0) c.mb.Tubo(0, pts, rad, lados, 1f, nivel >= 2);
            else if (nivel <= NivelMaxTubo[c.detalle])
            {
                var pr = new List<Vector3>(); var rr = new List<float>();
                for (int s = 0; s < pts.Count; s += PasoTramo[c.detalle]) { pr.Add(pts[s]); rr.Add(rad[s]); }
                if ((pts.Count - 1) % PasoTramo[c.detalle] != 0) { pr.Add(pts[pts.Count - 1]); rr.Add(rad[pts.Count - 1]); }
                c.mb.Tubo(0, pr, rr, Mathf.Max(3, lados - 2 * c.detalle), 1f, false);
            }

            if (hijos && nivel < e.niveles)
            {
                int n = Mathf.Max(2, e.hijos - (nivel - 1));
                float giro = Util.Rng(c.r, 0, 6.28f);
                for (int k = 0; k < n; k++)
                {
                    float t = Mathf.Lerp(0.3f, 0.92f, (k + Util.Rng(c.r, 0.1f, 0.9f)) / n);
                    int idx = Mathf.Min(pts.Count - 2, (int)(t * segs));
                    float f = t * segs - idx;
                    Vector3 pb = Vector3.Lerp(pts[idx], pts[idx + 1], f);
                    Vector3 db = dirs[idx];
                    giro += 2.39996f;
                    Vector3 perp = Vector3.Cross(db, Mathf.Abs(db.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                    perp = Quaternion.AngleAxis(giro * Mathf.Rad2Deg, db) * perp;
                    // las ramas tienden a abrirse hacia fuera de la copa
                    Vector3 haciaFuera = new Vector3(pb.x - c.centroCopa.x, 0, pb.z - c.centroCopa.z).normalized;
                    perp = (perp + haciaFuera * 0.6f).normalized;
                    float ap = (e.apertura + Util.Rng(c.r, -12f, 12f)) * Mathf.Deg2Rad;
                    Vector3 dh = (db * Mathf.Cos(ap) + perp * Mathf.Sin(ap)).normalized;
                    float L = largo * e.ratioLargo * Util.Rng(c.r, 0.75f, 1.1f) * (1f - 0.35f * t);
                    if (e.seco && c.r.NextDouble() < 0.25) L *= 0.45f;   // ramas rotas del roble seco
                    float rb = Mathf.Lerp(r0, r1, t) * 0.68f;
                    Rama(c, pb, dh, L, rb, rb * 0.3f, nivel + 1, true);
                }
            }

            if (!e.seco && e.hojas != null && nivel >= e.niveles - 1)
            {
                int n = Mathf.RoundToInt((nivel == e.niveles ? e.hojasPorRama : e.hojasPorRama * 0.5f) * 2.4f);
                for (int k = 0; k < n; k++)
                {
                    float t = nivel == e.niveles ? Util.Rng(c.r, 0.25f, 1.05f) : Util.Rng(c.r, 0.55f, 1f);
                    int idx = Mathf.Min(pts.Count - 2, (int)(Mathf.Clamp01(t) * segs));
                    Vector3 pb = Vector3.Lerp(pts[idx], pts[idx + 1], Mathf.Clamp01(t * segs - idx)) + Aleatorio(c.r) * 0.12f;
                    Vector3 db = dirs[idx];
                    Vector3 copa = (pb - c.centroCopa).normalized;
                    Vector3 arriba = (db * 0.5f + copa * 0.6f + Aleatorio(c.r) * 0.6f + Vector3.down * 0.25f).normalized;
                    Vector3 lado = Vector3.Cross(arriba, Aleatorio(c.r)).normalized;
                    Vector3 nrm = Vector3.Cross(lado, arriba).normalized;
                    // normal "esferica" para que la copa se ilumine como un volumen
                    Vector3 nCopa = Vector3.Lerp(nrm, (copa + Vector3.up * 0.8f).normalized, 0.8f).normalized;
                    float tam = 0.68f * e.hoja * Util.Rng(c.r, 0.8f, 1.2f) * Mathf.Clamp(c.alto / 9f, 0.6f, 1.25f);
                    // en los LOD lejanos, menos tarjetas pero mas grandes (misma superficie de copa)
                    if (c.hojas++ % PasoHoja[c.detalle] != 0) continue;
                    tam *= Mathf.Sqrt(PasoHoja[c.detalle]) * (c.detalle == 0 ? 1f : 0.85f);
                    Vector3 ancho = lado * tam * 0.5f, alto = arriba * tam;
                    var mb = c.mb;
                    int i0 = mb.Vert(pb - ancho, nCopa, new Vector2(0, 0));
                    int i1 = mb.Vert(pb - ancho + alto, nCopa, new Vector2(0, 1));
                    int i2 = mb.Vert(pb + ancho + alto, nCopa, new Vector2(1, 1));
                    int i3 = mb.Vert(pb + ancho, nCopa, new Vector2(1, 0));
                    mb.Tri(1, i0, i1, i2); mb.Tri(1, i0, i2, i3);
                }
            }
            return pts;
        }
    }
}
