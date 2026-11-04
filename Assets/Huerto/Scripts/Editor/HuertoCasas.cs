using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Huerto
{
    /// <summary>
    /// Casas de La Alberca: planta de Catastro, alturas y tejado ajustados a la nube LiDAR PNOA 2025.
    /// Arquitectura serrana: planta baja de granito con portones de madera, plantas altas de entramado
    /// (pies derechos de castano con relleno de revoque ocre o encalado) que vuelan sobre la calle apoyadas
    /// en canes, balconadas de madera con geranios, aleros grandes de teja arabe y chimeneas encaladas.
    /// </summary>
    public static class Casas
    {
        // submeshes
        const int Granito = 0, EntOcre = 1, EntBlanco = 2, Cal = 3, Teja = 4, Viga = 5, Puerta = 6, Vidrio = 7, Mamposteria = 8, Barandilla = 9, Flores = 10, TejaNueva = 11;
        const int NumSub = 12;
        // centro del casco antiguo (plaza e iglesia); hacia las afueras las casas son mas modernas y encaladas
        static readonly Vector2 Casco = new Vector2(250f, 250f);
        const float Alero = 0.6f;

        enum Estilo { EntramadoOcre, EntramadoBlanco, Encalada, Piedra, Moderna }

        /// <summary>Tejado ajustado al LiDAR. Dos aguas (G): y = r - s|p.n - k|; un agua (P): y = r - s(p.n - k).</summary>
        class Tejado
        {
            public bool dosAguas; public Vector2 n; public float k, r, s;
            public float Y(Vector2 p) { float d = Vector2.Dot(p, n) - k; return dosAguas ? r - s * Mathf.Abs(d) : r - s * d; }
        }

        public static Material[] Materiales()
        {
            var bar = Util.Tex("Casas/Barandilla.png", hojas: true, max: 1024);
            Repetir(bar);
            return new[]
            {
                Util.Mat("Casa_Granito", Util.Tex("Casas/rustic_stone_wall_diff_2k.jpg"), Util.Tex("Casas/rustic_stone_wall_nor_gl_2k.jpg", normal: true), new Color(0.95f, 0.93f, 0.9f), 0.1f, repeticion: 0.4f),
                Util.Mat("Casa_Entramado_Ocre", Util.Tex("Casas/Entramado_Ocre_diff.jpg"), Util.Tex("Casas/Entramado_Ocre_nor.jpg", normal: true), Color.white, 0.08f),
                Util.Mat("Casa_Entramado_Blanco", Util.Tex("Casas/Entramado_Blanco_diff.jpg"), Util.Tex("Casas/Entramado_Blanco_nor.jpg", normal: true), Color.white, 0.08f),
                Util.Mat("Casa_Cal", Util.Tex("white_rough_plaster_diff_2k.jpg"), Util.Tex("white_rough_plaster_nor_gl_2k.jpg", normal: true), new Color(0.97f, 0.96f, 0.93f), 0.08f, repeticion: 0.4f),
                Util.Mat("Casa_Teja_Arabe", Util.Tex("Casas/ceramic_roof_01_diff_2k.jpg"), Util.Tex("Casas/ceramic_roof_01_nor_gl_2k.jpg", normal: true), new Color(0.78f, 0.68f, 0.6f), 0.12f, repeticion: 0.5f),
                Util.Mat("Casa_Vigas", Util.Tex("Casas/dark_planks_diff_2k.jpg"), Util.Tex("Casas/dark_planks_nor_gl_2k.jpg", normal: true), new Color(0.6f, 0.53f, 0.46f), 0.1f, repeticion: 0.6f),
                Util.Mat("Casa_Porton", Util.Tex("Casas/weathered_brown_planks_diff_2k.jpg"), Util.Tex("Casas/weathered_brown_planks_nor_gl_2k.jpg", normal: true), new Color(0.75f, 0.62f, 0.5f), 0.12f, repeticion: 0.7f),
                Util.Mat("Casa_Vidrio", null, null, new Color(0.03f, 0.035f, 0.04f), 0.6f),
                Util.Mat("Casa_Mamposteria", Util.Tex("Casas/stone_wall_diff_2k.jpg"), Util.Tex("Casas/stone_wall_nor_gl_2k.jpg", normal: true), new Color(0.9f, 0.88f, 0.84f), 0.08f, repeticion: 0.35f),
                Util.Mat("Casa_Barandilla", bar, null, Color.white, 0.1f, recorte: true, dobleCara: true),
                Util.Mat("Casa_Geranios", Util.Tex("Casas/Geranios.png", hojas: true, max: 512), null, Color.white, 0.2f, recorte: true, dobleCara: true),
                Util.Mat("Casa_Teja_Nueva", Util.Tex("clay_roof_tiles_diff_2k.jpg"), Util.Tex("clay_roof_tiles_nor_gl_2k.jpg", normal: true), new Color(0.95f, 0.8f, 0.72f), 0.15f, repeticion: 0.5f),
            };
        }

        static void Repetir(Texture2D t)
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
            if (ti != null && ti.wrapModeU != TextureWrapMode.Repeat) { ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        }

        static Dictionary<string, Tejado> LeerTejados()
        {
            var dic = new Dictionary<string, Tejado>();
            string ruta = $"{Util.Raiz}/Datos/Edificios_Alturas_LiDAR.txt";
            if (!File.Exists(ruta)) return dic;
            foreach (var l in File.ReadAllLines(ruta))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var c = l.Split(';');
                float F(int i) => float.Parse(c[i], CultureInfo.InvariantCulture);
                dic[c[0]] = new Tejado { dosAguas = c[2] == "G", n = new Vector2(F(3), F(4)).normalized, k = F(5), r = F(6), s = F(7) };
            }
            return dic;
        }

        // ---------------- Rejilla para buscar vecinos (medianeras) ----------------
        class Rejilla
        {
            readonly Dictionary<Vector2Int, List<int>> celdas = new Dictionary<Vector2Int, List<int>>();
            readonly List<Vector2[]> polis;
            const float T = 20f;
            public Rejilla(List<Vector2[]> p)
            {
                polis = p;
                for (int i = 0; i < p.Count; i++)
                {
                    Vector2 a = p[i][0], b = p[i][0];
                    foreach (var q in p[i]) { a = Vector2.Min(a, q); b = Vector2.Max(b, q); }
                    for (int x = Mathf.FloorToInt(a.x / T); x <= Mathf.FloorToInt(b.x / T); x++)
                        for (int z = Mathf.FloorToInt(a.y / T); z <= Mathf.FloorToInt(b.y / T); z++)
                        {
                            var k = new Vector2Int(x, z);
                            if (!celdas.TryGetValue(k, out var l)) celdas[k] = l = new List<int>();
                            l.Add(i);
                        }
                }
            }
            public bool Dentro(Vector2 p, int excepto)
            {
                if (!celdas.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(p.y / T)), out var l)) return false;
                foreach (var i in l) if (i != excepto && Util.DentroPoligono(p, polis[i])) return true;
                return false;
            }
        }

        public static void Crear(List<Datos.Poligono> edificios, System.Func<Vector2, float> suelo, Transform padre, Material[] mats, System.Func<string, bool> generar = null)
        {
            var tejados = LeerTejados();
            // plantas limpias y en sentido antihorario
            var polis = new List<Vector2[]>();
            foreach (var e in edificios) polis.Add(Limpiar(e.pts));
            var rejilla = new Rejilla(polis);
            var grupos = new Dictionary<Vector2Int, MallaBuilder>();
            int conLidar = 0, hechas = 0;
            for (int i = 0; i < edificios.Count; i++)
            {
                // las que no se ven no se construyen, pero siguen contando como vecinas para las medianeras
                if (generar != null && !generar(edificios[i].id)) continue;
                var pts = polis[i];
                if (pts.Length < 3 || Area(pts) < 4f) continue;
                Vector2 c = Vector2.zero; foreach (var p in pts) c += p; c /= pts.Length;
                var celda = new Vector2Int(Mathf.FloorToInt(c.x / 120f), Mathf.FloorToInt(c.y / 120f));
                if (!grupos.TryGetValue(celda, out var mb)) { mb = new MallaBuilder(NumSub); grupos[celda] = mb; }
                var rnd = new System.Random(Util.Hash(edificios[i].id));
                if (tejados.TryGetValue(edificios[i].id, out var tj)) conLidar++;
                else tj = null;
                Edificio(mb, pts, i, edificios[i].valor, tj, suelo, rejilla, rnd);
                hechas++;
            }
            foreach (var g in grupos)
            {
                string nombre = $"Casas_{g.Key.x}_{g.Key.y}";
                var malla = Util.Guardar(g.Value.Construir(nombre), nombre);
                var go = Util.Objeto(nombre, padre, malla, mats);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                go.AddComponent<MeshCollider>().sharedMesh = malla;
            }
            Debug.Log($"Huerto: {hechas} de {edificios.Count} edificios construidos ({conLidar} con tejado medido en el LiDAR)");
        }

        // ---------------- Geometria plana ----------------
        static float Area(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++) { var q = p[i]; var r = p[(i + 1) % p.Length]; a += q.x * r.y - r.x * q.y; }
            return a * 0.5f;
        }

        static Vector2[] Limpiar(Vector2[] pts)
        {
            var l = new List<Vector2>();
            foreach (var p in pts) if (l.Count == 0 || Vector2.Distance(l[l.Count - 1], p) > 0.08f) l.Add(p);
            while (l.Count > 2 && Vector2.Distance(l[0], l[l.Count - 1]) < 0.08f) l.RemoveAt(l.Count - 1);
            // quitar vertices casi alineados
            for (int i = 0; i < l.Count && l.Count > 3;)
            {
                Vector2 a = l[(i - 1 + l.Count) % l.Count], b = l[i], c = l[(i + 1) % l.Count];
                float cr = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(cr) < 0.02f * Vector2.Distance(a, c)) l.RemoveAt(i); else i++;
            }
            var r = l.ToArray();
            if (Area(r) < 0) System.Array.Reverse(r);
            return r;
        }

        static Vector2 Fuera(Vector2 a, Vector2 b) { var d = (b - a).normalized; return new Vector2(d.y, -d.x); }

        /// <summary>Desplaza cada arista hacia fuera su distancia d[i] y recalcula los vertices como interseccion.</summary>
        static Vector2[] Desplazar(Vector2[] p, float[] d)
        {
            // si alguna arista se invierte (poligono que se cruza) se reduce el vuelo
            foreach (var f in new[] { 1f, 0.5f, 0.25f })
            {
                var dd = new float[d.Length];
                for (int i = 0; i < d.Length; i++) dd[i] = d[i] * f;
                var r = DesplazarSimple(p, dd);
                bool ok = true;
                for (int i = 0; i < p.Length && ok; i++)
                {
                    int j = (i + 1) % p.Length;
                    if (Vector2.Dot(r[j] - r[i], p[j] - p[i]) <= 0.02f * (p[j] - p[i]).sqrMagnitude) ok = false;
                }
                if (ok && Area(r) > 0) return r;
            }
            return (Vector2[])p.Clone();
        }

        static Vector2[] DesplazarSimple(Vector2[] p, float[] d)
        {
            int n = p.Length;
            var r = new Vector2[n];
            float dmax = 0f; foreach (var x in d) dmax = Mathf.Max(dmax, x);
            for (int i = 0; i < n; i++)
            {
                int j = (i - 1 + n) % n;
                Vector2 oj = Fuera(p[j], p[i]), oi = Fuera(p[i], p[(i + 1) % n]);
                float cj = Vector2.Dot(p[i], oj) + d[j], ci = Vector2.Dot(p[i], oi) + d[i];
                float det = oj.x * oi.y - oj.y * oi.x;
                Vector2 q;
                if (Mathf.Abs(det) < 0.2f) q = p[i] + (oi + oj).normalized * Mathf.Max(d[i], d[j]);
                else q = new Vector2((cj * oi.y - ci * oj.y) / det, (oj.x * ci - oi.x * cj) / det);
                if (Vector2.Distance(q, p[i]) > dmax * 2.5f + 0.01f) q = p[i] + (oi * d[i] + oj * d[j]);
                r[i] = q;
            }
            return r;
        }

        /// <summary>Recorta un poligono por el semiplano s*(p.n - k) >= 0 (Sutherland-Hodgman).</summary>
        static List<Vector2> Recortar(IList<Vector2> poly, Vector2 n, float k, float s)
        {
            var res = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = s * (Vector2.Dot(a, n) - k), db = s * (Vector2.Dot(b, n) - k);
                if (da >= 0) res.Add(a);
                if ((da >= 0) != (db >= 0)) res.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return res;
        }

        /// <summary>Triangulacion por orejas de un poligono antihorario.</summary>
        static List<int> Triangular(IList<Vector2> p)
        {
            var tri = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            int guardia = 0;
            while (idx.Count > 3 && guardia++ < 500)
            {
                bool hecho = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    float cr = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cr <= 1e-5f) continue;
                    bool vacia = true;
                    foreach (var j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (EnTriangulo(p[j], a, b, c)) { vacia = false; break; }
                    }
                    if (!vacia) continue;
                    tri.Add(ia); tri.Add(ib); tri.Add(ic);
                    idx.RemoveAt(i); hecho = true; break;
                }
                if (!hecho) break;
            }
            for (int i = 1; i + 1 < idx.Count; i++) { tri.Add(idx[0]); tri.Add(idx[i]); tri.Add(idx[i + 1]); }
            return tri;
        }

        static bool EnTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float s2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float s3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
            return s1 > 0 && s2 > 0 && s3 > 0;
        }

        static Vector3 V3(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>Quad orientado hacia 'cara' (ordena los vertices para que el frente mire alli).</summary>
        static void QuadHacia(MallaBuilder mb, int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 cara)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), cara) < 0) mb.Quad(s, d, c, b, a, ud, uc, ub, ua);
            else mb.Quad(s, a, b, c, d, ua, ub, uc, ud);
        }

        static void Caja(MallaBuilder mb, int s, Vector3 c, Vector3 eje, float largo, float alto, float fondo, Vector3 fuera)
        {
            mb.Caja(s, c, eje.normalized * largo * 0.5f, Vector3.up * alto * 0.5f, fuera.normalized * fondo * 0.5f);
        }

        // ---------------- Edificio ----------------
        static void Edificio(MallaBuilder mb, Vector2[] pts, int indice, int plantasCatastro, Tejado tj, System.Func<Vector2, float> suelo, Rejilla rejilla, System.Random rnd)
        {
            int n = pts.Length;
            var g = new float[n];
            float gmin = float.MaxValue, gmax = float.MinValue;
            for (int i = 0; i < n; i++) { g[i] = suelo(pts[i]); gmin = Mathf.Min(gmin, g[i]); gmax = Mathf.Max(gmax, g[i]); }
            Vector2 c = Vector2.zero; foreach (var p in pts) c += p; c /= n;

            if (tj == null)
            {
                // sin datos LiDAR: dos aguas por el eje largo y altura por numero de plantas
                float sxx = 0, sxz = 0, szz = 0;
                foreach (var p in pts) { var q = p - c; sxx += q.x * q.x; sxz += q.x * q.y; szz += q.y * q.y; }
                float ang = 0.5f * Mathf.Atan2(2 * sxz, sxx - szz);
                var nrm = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang));
                float v0 = float.MaxValue, v1 = float.MinValue;
                foreach (var p in pts) { float v = Vector2.Dot(p, nrm); v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v); }
                float s = Mathf.Tan(22f * Mathf.Deg2Rad);
                tj = new Tejado { dosAguas = true, n = nrm, k = (v0 + v1) * 0.5f, s = s, r = gmin + Mathf.Clamp(plantasCatastro, 1, 4) * 2.8f + (v1 - v0) * 0.5f * s };
            }
            // el alero no puede quedar por debajo de 2,3 m sobre el suelo
            float aleroMin = float.MaxValue;
            foreach (var p in pts) aleroMin = Mathf.Min(aleroMin, tj.Y(p));
            if (aleroMin < gmin + 2.3f) { tj.r += gmin + 2.3f - aleroMin; aleroMin = gmin + 2.3f; }
            float alto = aleroMin - gmin;
            int plantas = Mathf.Clamp(Mathf.RoundToInt(alto / 2.75f), 1, 5);
            float hp = alto / plantas;

            // estilo de la casa
            Estilo estilo;
            double e = rnd.NextDouble();
            if (rnd.NextDouble() < 0.85 * Util.Smooth(140f, 260f, Vector2.Distance(c, Casco))) estilo = Estilo.Moderna;
            else if (plantas == 1) estilo = e < 0.75 ? Estilo.Piedra : Estilo.Encalada;
            else estilo = e < 0.45 ? Estilo.EntramadoOcre : e < 0.65 ? Estilo.EntramadoBlanco : e < 0.82 ? Estilo.Encalada : Estilo.Piedra;
            bool moderna = estilo == Estilo.Moderna;
            int subAlta = estilo == Estilo.EntramadoOcre ? EntOcre : estilo == Estilo.EntramadoBlanco ? EntBlanco : estilo == Estilo.Encalada || moderna ? Cal : Granito;
            int subBaja = moderna && rnd.NextDouble() < 0.6 ? Cal : Granito;
            int subTeja = moderna ? TejaNueva : Teja;
            bool entramado = estilo == Estilo.EntramadoOcre || estilo == Estilo.EntramadoBlanco;
            float vueloPlanta = entramado ? Util.Rng(rnd, 0.28f, 0.42f) : estilo == Estilo.Encalada ? 0.18f : 0f;

            // aristas a la calle (no medianeras)
            var exterior = new bool[n]; var largo = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                largo[i] = Vector2.Distance(a, b);
                Vector2 m = (a + b) * 0.5f, o = Fuera(a, b);
                exterior[i] = !rejilla.Dentro(m + o * 0.5f, indice) && !rejilla.Dentro(Vector2.Lerp(a, b, 0.2f) + o * 0.5f, indice) && !rejilla.Dentro(Vector2.Lerp(a, b, 0.8f) + o * 0.5f, indice);
            }
            // portal en la fachada exterior mas larga
            int aristaPuerta = -1;
            for (int i = 0; i < n; i++) if (exterior[i] && largo[i] > 2.2f && (aristaPuerta < 0 || largo[i] > largo[aristaPuerta])) aristaPuerta = i;

            float[] Vuelos(int planta)
            {
                var d = new float[n];
                for (int i = 0; i < n; i++) d[i] = exterior[i] && largo[i] > 2f ? Mathf.Min(planta * vueloPlanta, 0.65f) : 0f;
                return d;
            }

            Vector2[] previo = pts;
            float[] dPrevio = new float[n];
            for (int k = 0; k < plantas; k++)
            {
                float[] dk = Vuelos(k);
                var P = k == 0 ? pts : Desplazar(pts, dk);
                float yb = k == 0 ? gmin - 0.8f : gmin + k * hp;
                bool ultima = k == plantas - 1;
                bool balconPlanta = k >= 1 && rnd.NextDouble() < (entramado ? 0.45 : 0.25);
                for (int i = 0; i < n; i++)
                {
                    Vector2 A = P[i], B = P[(i + 1) % n];
                    float L = Vector2.Distance(A, B);
                    if (L < 0.05f) continue;
                    Vector2 o2 = Fuera(A, B); Vector3 o = new Vector3(o2.x, 0, o2.y);
                    int sub = !exterior[i] ? (moderna ? Cal : Mamposteria) : k == 0 ? subBaja : subAlta;
                    // puntos de la parte alta: siguen el tejado en la ultima planta (hastiales)
                    var lista = new List<Vector2> { A };
                    if (ultima && tj.dosAguas)
                    {
                        float da = Vector2.Dot(A, tj.n) - tj.k, db = Vector2.Dot(B, tj.n) - tj.k;
                        if (da * db < 0) lista.Add(Vector2.Lerp(A, B, da / (da - db)));
                    }
                    lista.Add(B);
                    float yTopPlano = gmin + (k + 1) * hp;
                    float uAcum = 0f;
                    for (int j = 0; j + 1 < lista.Count; j++)
                    {
                        Vector2 a = lista[j], b = lista[j + 1];
                        float l = Vector2.Distance(a, b);
                        float ta = ultima ? tj.Y(a) - 0.02f : yTopPlano, tb = ultima ? tj.Y(b) - 0.02f : yTopPlano;
                        Vector2 ua0, ua1, ub0, ub1;
                        if (sub == EntOcre || sub == EntBlanco)
                        {
                            ua0 = new Vector2(uAcum / 2.4f, 0); ua1 = new Vector2(uAcum / 2.4f, (ta - yb) / hp);
                            ub0 = new Vector2((uAcum + l) / 2.4f, 0); ub1 = new Vector2((uAcum + l) / 2.4f, (tb - yb) / hp);
                        }
                        else
                        {
                            ua0 = new Vector2(uAcum, yb); ua1 = new Vector2(uAcum, ta); ub0 = new Vector2(uAcum + l, yb); ub1 = new Vector2(uAcum + l, tb);
                        }
                        QuadHacia(mb, sub, V3(a, yb), V3(a, ta), V3(b, tb), V3(b, yb), ua0, ua1, ub1, ub0, o);
                        uAcum += l;
                    }
                    if (!exterior[i]) continue;

                    // vuelo: tablazon inferior, viga y canes
                    if (k >= 1 && dk[i] > dPrevio[i] + 0.05f)
                    {
                        Vector2 A0 = previo[i], B0 = previo[(i + 1) % n];
                        QuadHacia(mb, Viga, V3(A0, yb), V3(A, yb), V3(B, yb), V3(B0, yb), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(L, 0.5f), new Vector2(L, 0), Vector3.down);
                        Caja(mb, Viga, V3((A + B) * 0.5f, yb + 0.08f) + o * 0.04f, V3(B - A, 0), L, 0.18f, 0.12f, o);
                        float salida = dk[i] - dPrevio[i] + 0.12f;
                        int nc = Mathf.FloorToInt(Vector2.Distance(A0, B0) / 0.6f);
                        for (int j = 1; j < nc; j++)
                        {
                            Vector2 q = Vector2.Lerp(A0, B0, j / (float)nc);
                            Caja(mb, Viga, V3(q, yb - 0.09f) + o * (salida * 0.5f - 0.02f), V3(B - A, 0), 0.12f, 0.16f, salida, o);
                        }
                    }

                    if (L < 1.4f) continue;
                    Vector3 eje = V3(B - A, 0).normalized;
                    if (k == 0)
                    {
                        if (i == aristaPuerta)
                        {
                            float t = Util.Rng(rnd, 0.3f, 0.7f);
                            Vector2 q = Vector2.Lerp(A, B, t);
                            float gq = suelo(q);
                            float an = L > 3f ? Util.Rng(rnd, 1.1f, 1.6f) : 0.95f, al = Mathf.Min(2.3f, hp - 0.3f + (gmin - gq));
                            if (al > 1.7f) Porton(mb, V3(q, gq), eje, o, an, al, rnd);
                        }
                        if (L > 4.5f && rnd.NextDouble() < 0.7)
                        {
                            float t = i == aristaPuerta ? (rnd.NextDouble() < 0.5 ? 0.12f : 0.88f) : Util.Rng(rnd, 0.3f, 0.7f);
                            Vector2 q = Vector2.Lerp(A, B, t);
                            Ventana(mb, V3(q, suelo(q) + 1.45f), eje, o, 0.5f, 0.6f, false, rnd);
                        }
                        continue;
                    }
                    // plantas altas: ventanas o balconada
                    bool balcon = balconPlanta && L > 2.8f;
                    int nv = Mathf.Max(1, Mathf.FloorToInt((L - 0.4f) / 2.6f));
                    float topePlanta = ultima ? Mathf.Min(tj.Y(A), tj.Y(B)) : yTopPlano;
                    for (int j = 0; j < nv; j++)
                    {
                        float t = (j + 0.5f) / nv + Util.Rng(rnd, -0.04f, 0.04f);
                        if (rnd.NextDouble() < 0.15 && nv > 1) continue;
                        Vector2 q = Vector2.Lerp(A, B, t);
                        float h = balcon ? 1.95f : Util.Rng(rnd, 1.0f, 1.25f);
                        float y0 = balcon ? yb + 0.1f : yb + 0.85f;
                        if (y0 + h > topePlanta - 0.2f) h = topePlanta - 0.2f - y0;
                        if (h < 0.6f) continue;
                        Ventana(mb, V3(q, y0 + h * 0.5f), eje, o, balcon ? 0.9f : Util.Rng(rnd, 0.7f, 0.9f), h, !balcon && rnd.NextDouble() < 0.55, rnd);
                    }
                    if (balcon) Balcon(mb, A, B, yb, o, rnd);
                }
                previo = P; dPrevio = dk;
            }

            // ---------------- Tejado ----------------
            var dTop = Vuelos(plantas - 1);
            var dTejado = new float[n];
            for (int i = 0; i < n; i++) dTejado[i] = dTop[i] + (exterior[i] ? Alero : 0.05f);
            var R = Desplazar(pts, dTejado);
            var piezas = new List<List<Vector2>>();
            if (tj.dosAguas) { piezas.Add(Recortar(R, tj.n, tj.k, 1f)); piezas.Add(Recortar(R, tj.n, tj.k, -1f)); }
            else piezas.Add(new List<Vector2>(R));
            Vector2 tdir = new Vector2(-tj.n.y, tj.n.x);
            float largoPend = Mathf.Sqrt(1 + tj.s * tj.s);
            float rMin = float.MaxValue, rMax = float.MinValue;
            foreach (var pieza in piezas)
            {
                if (pieza.Count < 3) continue;
                var tri = Triangular(pieza);
                Vector2 cp = Vector2.zero; foreach (var p in pieza) cp += p; cp /= pieza.Count;
                float lado = tj.dosAguas ? Mathf.Sign(Vector2.Dot(cp, tj.n) - tj.k) : 1f;
                Vector3 nrmPlano = new Vector3(tj.n.x * tj.s * lado, 1f, tj.n.y * tj.s * lado).normalized;
                int[] arriba = new int[pieza.Count], abajo = new int[pieza.Count];
                for (int j = 0; j < pieza.Count; j++)
                {
                    var p = pieza[j];
                    float y = tj.Y(p);
                    var uv = new Vector2(Vector2.Dot(p, tdir), Mathf.Abs(Vector2.Dot(p, tj.n) - tj.k) * largoPend);
                    arriba[j] = mb.Vert(V3(p, y + 0.05f), nrmPlano, uv);
                    abajo[j] = mb.Vert(V3(p, y - 0.1f), -nrmPlano, uv);
                    if (tj.dosAguas && Mathf.Abs(Vector2.Dot(p, tj.n) - tj.k) < 1e-3f) { float u = Vector2.Dot(p, tdir); rMin = Mathf.Min(rMin, u); rMax = Mathf.Max(rMax, u); }
                }
                for (int t = 0; t < tri.Count; t += 3)
                {
                    // poligono antihorario en (x,z) => visto desde arriba en Unity el orden horario es (a, c, b)
                    mb.Tri(subTeja, arriba[tri[t]], arriba[tri[t + 2]], arriba[tri[t + 1]]);
                    mb.Tri(Viga, abajo[tri[t]], abajo[tri[t + 1]], abajo[tri[t + 2]]);
                }
                // canto del alero
                for (int j = 0; j < pieza.Count; j++)
                {
                    Vector2 a = pieza[j], b = pieza[(j + 1) % pieza.Count];
                    if (tj.dosAguas && Mathf.Abs(Vector2.Dot(a, tj.n) - tj.k) < 1e-3f && Mathf.Abs(Vector2.Dot(b, tj.n) - tj.k) < 1e-3f) continue;
                    float l = Vector2.Distance(a, b); if (l < 0.02f) continue;
                    var o2 = Fuera(a, b);
                    QuadHacia(mb, Viga, V3(a, tj.Y(a) - 0.1f), V3(a, tj.Y(a) + 0.05f), V3(b, tj.Y(b) + 0.05f), V3(b, tj.Y(b) - 0.1f),
                        new Vector2(0, 0), new Vector2(0, 0.15f), new Vector2(l, 0.15f), new Vector2(l, 0), new Vector3(o2.x, 0, o2.y));
                }
            }
            // cumbrera de tejas
            if (tj.dosAguas && rMax > rMin)
            {
                Vector2 m = tj.n * tj.k + tdir * ((rMin + rMax) * 0.5f);
                mb.Caja(subTeja, V3(m, tj.r + 0.1f), V3(tdir, 0) * (rMax - rMin) * 0.5f, Vector3.up * 0.08f, V3(tj.n, 0) * 0.16f, 1f);
            }
            // chimenea encalada con sombrerete de teja
            if (Area(pts) > 30f && rnd.NextDouble() < 0.55)
            {
                Vector2 q = tj.dosAguas && rMax > rMin ? tj.n * (tj.k + Util.Rng(rnd, -0.6f, 0.6f)) + tdir * Mathf.Lerp(rMin, rMax, Util.Rng(rnd, 0.2f, 0.8f)) : c;
                if (Util.DentroPoligono(q, pts))
                {
                    float y0 = tj.Y(q) - 0.4f, h = Util.Rng(rnd, 1.1f, 1.6f);
                    var ex = V3(tdir, 0); var ez = V3(tj.n, 0);
                    mb.Caja(Cal, V3(q, y0 + (h + 0.4f) * 0.5f), ex * 0.3f, Vector3.up * (h + 0.4f) * 0.5f, ez * 0.3f, 1f);
                    mb.Caja(Teja, V3(q, y0 + h + 0.4f + 0.18f), ex * 0.42f, Vector3.up * 0.05f, ez * 0.42f, 1f);
                    foreach (var s in new[] { -1f, 1f }) mb.Caja(Cal, V3(q + tdir * 0.22f * s, y0 + h + 0.4f + 0.07f), ex * 0.06f, Vector3.up * 0.07f, ez * 0.25f, 1f);
                }
            }
        }

        static void Porton(MallaBuilder mb, Vector3 baseCentro, Vector3 eje, Vector3 o, float ancho, float alto, System.Random rnd)
        {
            Vector3 c = baseCentro + Vector3.up * alto * 0.5f;
            // hoja de madera
            Vector3 a = c - eje * ancho * 0.5f + o * 0.03f, b = c + eje * ancho * 0.5f + o * 0.03f;
            QuadHacia(mb, Puerta, a - Vector3.up * alto * 0.5f, a + Vector3.up * alto * 0.5f, b + Vector3.up * alto * 0.5f, b - Vector3.up * alto * 0.5f,
                new Vector2(0, 0), new Vector2(0, alto), new Vector2(ancho, alto), new Vector2(ancho, 0), o);
            // jambas y dintel de granito
            Caja(mb, Granito, c - eje * (ancho * 0.5f + 0.14f) + o * 0.06f, eje, 0.28f, alto, 0.14f, o);
            Caja(mb, Granito, c + eje * (ancho * 0.5f + 0.14f) + o * 0.06f, eje, 0.28f, alto, 0.14f, o);
            Caja(mb, Granito, c + Vector3.up * (alto * 0.5f + 0.17f) + o * 0.07f, eje, ancho + 0.7f, 0.34f, 0.16f, o);
            // umbral
            Caja(mb, Granito, baseCentro + Vector3.up * 0.06f + o * 0.18f, eje, ancho + 0.5f, 0.14f, 0.4f, o);
        }

        static void Ventana(MallaBuilder mb, Vector3 c, Vector3 eje, Vector3 o, float ancho, float alto, bool contraventanas, System.Random rnd)
        {
            Vector3 up = Vector3.up;
            Vector3 a = c - eje * ancho * 0.5f + o * 0.02f, b = c + eje * ancho * 0.5f + o * 0.02f;
            QuadHacia(mb, Vidrio, a - up * alto * 0.5f, a + up * alto * 0.5f, b + up * alto * 0.5f, b - up * alto * 0.5f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, o);
            const float m = 0.08f;
            Caja(mb, Viga, c - eje * (ancho * 0.5f) + o * 0.04f, eje, m, alto + m, 0.08f, o);
            Caja(mb, Viga, c + eje * (ancho * 0.5f) + o * 0.04f, eje, m, alto + m, 0.08f, o);
            Caja(mb, Viga, c + up * (alto * 0.5f) + o * 0.04f, eje, ancho + m, m, 0.08f, o);
            Caja(mb, Viga, c - up * (alto * 0.5f) + o * 0.06f, eje, ancho + 0.16f, m, 0.14f, o);
            Caja(mb, Viga, c + o * 0.045f, eje, 0.05f, alto, 0.05f, o);   // parteluz
            if (contraventanas)
            {
                float w = ancho * 0.5f;
                foreach (var s in new[] { -1f, 1f })
                {
                    Vector3 ca = c + eje * s * (ancho * 0.5f + m * 0.5f) + o * 0.05f, cb = ca + eje * s * w;
                    QuadHacia(mb, Puerta, ca - up * alto * 0.5f, ca + up * alto * 0.5f, cb + up * alto * 0.5f, cb - up * alto * 0.5f,
                        new Vector2(0, 0), new Vector2(0, alto), new Vector2(w, alto), new Vector2(w, 0), o);
                }
            }
        }

        static void Balcon(MallaBuilder mb, Vector2 A, Vector2 B, float yb, Vector3 o, System.Random rnd)
        {
            float L = Vector2.Distance(A, B);
            float f = Mathf.Clamp(Util.Rng(rnd, 0.55f, 0.9f), 0f, (L - 0.3f) / L);
            float t0 = 0.5f - f * 0.5f + Util.Rng(rnd, -0.05f, 0.05f), t1 = t0 + f;
            t0 = Mathf.Clamp(t0, 0.05f, 0.95f); t1 = Mathf.Clamp(t1, 0.05f, 0.95f);
            Vector3 a = V3(Vector2.Lerp(A, B, t0), yb), b = V3(Vector2.Lerp(A, B, t1), yb);
            Vector3 eje = (b - a).normalized; float l = Vector3.Distance(a, b);
            const float fondo = 0.6f, altoBar = 0.95f;
            // suelo del balcon y canes
            Caja(mb, Viga, (a + b) * 0.5f + o * fondo * 0.5f + Vector3.up * 0.04f, eje, l, 0.08f, fondo, o);
            int nc = Mathf.Max(2, Mathf.FloorToInt(l / 0.7f) + 1);
            for (int j = 0; j < nc; j++)
                Caja(mb, Viga, Vector3.Lerp(a, b, j / (float)(nc - 1)) + o * (fondo * 0.5f) - Vector3.up * 0.08f, eje, 0.1f, 0.14f, fondo + 0.1f, o);
            // barandilla de balaustres (textura recortada) y pasamanos
            Vector3 fa = a + o * fondo, fb = b + o * fondo;
            float y0 = 0.08f;
            QuadHacia(mb, Barandilla, fa + Vector3.up * y0, fa + Vector3.up * altoBar, fb + Vector3.up * altoBar, fb + Vector3.up * y0,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(l / 1.2f, 1), new Vector2(l / 1.2f, 0), o);
            foreach (var p in new[] { a, b })
            {
                Vector3 pf = p + o * fondo;
                QuadHacia(mb, Barandilla, p + Vector3.up * y0, p + Vector3.up * altoBar, pf + Vector3.up * altoBar, pf + Vector3.up * y0,
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(fondo / 1.2f, 1), new Vector2(fondo / 1.2f, 0), eje);
            }
            Caja(mb, Viga, (fa + fb) * 0.5f + Vector3.up * (altoBar + 0.03f), eje, l + 0.08f, 0.06f, 0.1f, o);
            // tejadillo de la balconada en algunas casas
            if (rnd.NextDouble() < 0.25)
            {
                // a y b ya estan a la altura del suelo del balcon
                Vector3 c = (a + b) * 0.5f + o * (fondo * 0.5f) + Vector3.up * 2.35f;
                Caja(mb, Viga, c, eje, l + 0.2f, 0.06f, fondo + 0.25f, o);
                foreach (var p in new[] { fa, fb }) Caja(mb, Viga, p + Vector3.up * (altoBar + (2.35f - altoBar) * 0.5f), eje, 0.09f, 2.35f - altoBar, 0.09f, o);
            }
            // macetas de geranios colgadas del pasamanos
            if (rnd.NextDouble() < 0.6)
            {
                int nm = Mathf.FloorToInt(l / 0.55f);
                for (int j = 0; j < nm; j++)
                {
                    if (rnd.NextDouble() < 0.25) continue;
                    Vector3 c = Vector3.Lerp(fa, fb, (j + 0.5f) / nm) + o * 0.12f + Vector3.up * (altoBar - 0.1f);
                    float s = Util.Rng(rnd, 0.42f, 0.6f);
                    for (int q = 0; q < 2; q++)
                    {
                        Vector3 d = Quaternion.AngleAxis(45f + 90f * q + Util.Rng(rnd, -15f, 15f), Vector3.up) * eje * s * 0.5f;
                        mb.Quad(Flores, c - d - Vector3.up * s * 0.35f, c - d + Vector3.up * s * 0.6f, c + d + Vector3.up * s * 0.6f, c + d - Vector3.up * s * 0.35f,
                            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    }
                }
            }
        }
    }
}
