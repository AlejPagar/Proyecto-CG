using System.Collections.Generic;
using Huerto.Gemelo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Huerto
{
    /// <summary>
    /// Bancales de cultivo del gemelo digital: siete bancales en los huecos libres de la parcela 39
    /// (sin arboles, rocas ni muros encima), cada uno con su cultivo tipico de la sierra y un escenario de clima distinto.
    /// Genera tambien las mallas de las plantas por etapa, la tierra labrada, los carteles, la lluvia local y la tableta.
    /// </summary>
    public static class Cultivos
    {
        public struct Bancal { public int numero; public string nombre; public Rect area; public string cultivo, clima; public Color color; }

        // Rectangulos en coordenadas del huerto (x este, z norte), elegidos sobre la vista cenital con los arboles del LiDAR y las rocas
        public static readonly Bancal[] Bancales =
        {
            new Bancal { numero = 1, nombre = "Bancal alto, junto al cerezo", area = Rect.MinMaxRect(99.0f, 74.8f, 102.0f, 78.6f), cultivo = "Tomate", clima = "Año típico (2021)", color = new Color(0.85f, 0.33f, 0.31f) },
            new Bancal { numero = 2, nombre = "Bancal alto, centro", area = Rect.MinMaxRect(103.0f, 74.8f, 106.0f, 78.6f), cultivo = "Pimiento", clima = "Ola de calor en julio", color = new Color(0.94f, 0.68f, 0.31f) },
            new Bancal { numero = 3, nombre = "Bancal alto, bajo la roca", area = Rect.MinMaxRect(99.0f, 79.6f, 102.0f, 83.0f), cultivo = "Patata", clima = "Año lluvioso (2025)", color = new Color(0.56f, 0.42f, 0.75f) },
            new Bancal { numero = 4, nombre = "Bancal alto, lindero este", area = Rect.MinMaxRect(105.8f, 79.6f, 108.8f, 83.2f), cultivo = "Calabaza", clima = "Cambio climático (+2 °C)", color = new Color(0.9f, 0.49f, 0.13f) },
            new Bancal { numero = 5, nombre = "Bancal medio, oeste", area = Rect.MinMaxRect(90.5f, 88.0f, 93.3f, 92.8f), cultivo = "Judia", clima = "Helada tardía en mayo", color = new Color(0.36f, 0.72f, 0.36f) },
            new Bancal { numero = 6, nombre = "Bancal medio, este", area = Rect.MinMaxRect(93.8f, 88.0f, 96.6f, 92.8f), cultivo = "Cebolla", clima = "Año seco (2005)", color = new Color(0.79f, 0.69f, 0.22f) },
            new Bancal { numero = 7, nombre = "Bajo el castaño, junto a la caseta", area = Rect.MinMaxRect(96.2f, 96.2f, 99.4f, 99.0f), cultivo = "Lechuga", clima = "Sombra de los árboles", color = new Color(0.16f, 0.62f, 0.56f) },
        };

        public static bool EnBancal(float x, float z, float margen)
        {
            foreach (var b in Bancales)
                if (x > b.area.xMin - margen && x < b.area.xMax + margen && z > b.area.yMin - margen && z < b.area.yMax + margen) return true;
            return false;
        }

        // ---------------- Suelo: tierra en los bancales y sin hierba ----------------
        public static void PrepararSuelo(Terrain t)
        {
            var data = t.terrainData;
            int ar = data.alphamapResolution;
            var mapa = data.GetAlphamaps(0, 0, ar, ar);
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float wx = (x + 0.5f) / ar * Datos.Tam, wz = (z + 0.5f) / ar * Datos.Tam;
                    float w = 0f;
                    foreach (var b in Bancales)
                    {
                        float dx = Mathf.Max(b.area.xMin - wx, wx - b.area.xMax, 0f), dz = Mathf.Max(b.area.yMin - wz, wz - b.area.yMax, 0f);
                        w = Mathf.Max(w, 1f - Util.Smooth(0.1f, 0.6f, Mathf.Sqrt(dx * dx + dz * dz)));
                    }
                    if (w <= 0f) continue;
                    for (int l = 0; l < mapa.GetLength(2); l++) mapa[z, x, l] *= 1f - w;
                    mapa[z, x, 1] += w;
                }
            data.SetAlphamaps(0, 0, mapa);
            int dr = data.detailResolution;
            for (int capa = 0; capa < data.detailPrototypes.Length; capa++)
            {
                var det = data.GetDetailLayer(0, 0, dr, dr, capa);
                for (int z = 0; z < dr; z++)
                    for (int x = 0; x < dr; x++)
                        if (EnBancal((x + 0.5f) / dr * Datos.Tam, (z + 0.5f) / dr * Datos.Tam, 0.35f)) det[z, x] = 0;
                data.SetDetailLayer(0, 0, capa, det);
            }
            EditorUtility.SetDirty(data);
        }

        // ---------------- Primitivas ----------------
        static Vector3 Rot(Vector3 v, float grados) => Quaternion.Euler(0, grados, 0) * v;

        static void Tallo(MallaBuilder mb, IList<Vector3> pts, float r0, float r1, int s = 0, int lados = 5)
        {
            var rad = new List<float>();
            for (int i = 0; i < pts.Count; i++) rad.Add(Mathf.Lerp(r0, r1, i / (float)Mathf.Max(1, pts.Count - 1)));
            mb.Tubo(s, pts, rad, lados, 1f, true);
        }

        /// <summary>Tarjeta de hoja anclada en 'base', hacia 'dir', doblada hacia abajo 'caida' grados en la mitad.</summary>
        static void Hoja(MallaBuilder mb, Vector3 base_, Vector3 dir, float largo, float ancho, float caida, int s = 1)
        {
            dir.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, dir);
            if (lateral.sqrMagnitude < 0.01f) lateral = Vector3.right;
            lateral.Normalize();
            Vector3 dir2 = Quaternion.AngleAxis(caida, lateral) * dir;
            Vector3 mid = base_ + dir * largo * 0.5f, punta = mid + dir2 * largo * 0.5f;
            Vector3 w = lateral * ancho * 0.5f;
            Vector3 n1 = Vector3.Cross(lateral, dir).normalized, n2 = Vector3.Cross(lateral, dir2).normalized;
            if (n1.y < 0) n1 = -n1; if (n2.y < 0) n2 = -n2;
            n1 = Vector3.Lerp(n1, Vector3.up, 0.45f).normalized; n2 = Vector3.Lerp(n2, Vector3.up, 0.45f).normalized;
            int a = mb.Vert(base_ - w, n1, new Vector2(0, 0)), b = mb.Vert(base_ + w, n1, new Vector2(1, 0));
            int c = mb.Vert(mid - w, n1, new Vector2(0, 0.5f)), d = mb.Vert(mid + w, n1, new Vector2(1, 0.5f));
            int e = mb.Vert(punta - w, n2, new Vector2(0, 1)), f = mb.Vert(punta + w, n2, new Vector2(1, 1));
            mb.Tri(s, a, c, d); mb.Tri(s, a, d, b); mb.Tri(s, c, e, f); mb.Tri(s, c, f, d);
        }

        static void Esfera(MallaBuilder mb, int s, Vector3 c, Vector3 r, int anillos = 6, int lados = 8)
        {
            int b0 = mb.v.Count, fila = lados + 1;
            for (int i = 0; i <= anillos; i++)
            {
                float th = Mathf.Lerp(0.001f, Mathf.PI - 0.001f, i / (float)anillos);
                for (int j = 0; j <= lados; j++)
                {
                    float ph = j / (float)lados * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    mb.Vert(c + Vector3.Scale(n, r), n, new Vector2(j / (float)lados, i / (float)anillos));
                }
            }
            for (int i = 0; i < anillos; i++)
                for (int j = 0; j < lados; j++)
                {
                    int a = b0 + i * fila + j, b = a + 1, cc = a + fila, d = cc + 1;
                    mb.Tri(s, a, b, cc); mb.Tri(s, b, d, cc);
                }
        }

        /// <summary>Flor: dos tarjetas cruzadas con la textura de flor.</summary>
        static void Flor(MallaBuilder mb, Vector3 c, float tam)
        {
            for (int k = 0; k < 2; k++)
            {
                Vector3 u = Rot(Vector3.right, k * 90f + 20f) * tam * 0.5f, v = (Vector3.up * 0.6f + Rot(Vector3.forward, k * 90f + 20f) * 0.8f).normalized * tam * 0.5f;
                Vector3 n = Vector3.Cross(u, v).normalized; if (n.y < 0) n = -n;
                int a = mb.Vert(c - u - v, n, new Vector2(0, 0)), b = mb.Vert(c + u - v, n, new Vector2(1, 0)), d = mb.Vert(c + u + v, n, new Vector2(1, 1)), e = mb.Vert(c - u + v, n, new Vector2(0, 1));
                mb.Tri(3, a, e, d); mb.Tri(3, a, d, b);
            }
        }

        static float R(System.Random r, float a, float b) => Util.Rng(r, a, b);

        // ---------------- Plantas por cultivo y etapa ----------------
        // submallas: 0 tallo, 1 hoja, 2 fruto verde, 3 flor, 4 tutor, 5 fruto maduro
        static MallaBuilder Planta(string cultivo, int etapa)
        {
            var mb = new MallaBuilder(6);
            var r = new System.Random(cultivo.GetHashCode() * 31 + etapa * 7);
            switch (cultivo)
            {
                case "Tomate": Tomate(mb, etapa, r); break;
                case "Pimiento": Pimiento(mb, etapa, r); break;
                case "Judia": Judia(mb, etapa, r); break;
                case "Patata": Patata(mb, etapa, r); break;
                case "Lechuga": Lechuga(mb, etapa, r); break;
                case "Cebolla": Cebolla(mb, etapa, r); break;
                case "Calabaza": Calabaza(mb, etapa, r); break;
                default: Berza(mb, etapa, r); break;
            }
            return mb;
        }

        static List<Vector3> TalloRecto(Vector3 desde, float alto, System.Random r, float desvio = 0.03f, int tramos = 5)
        {
            var pts = new List<Vector3>();
            Vector3 p = desde;
            for (int i = 0; i <= tramos; i++) { pts.Add(p); p += new Vector3(R(r, -desvio, desvio), alto / tramos, R(r, -desvio, desvio)); }
            return pts;
        }

        static Vector3 EnTallo(List<Vector3> pts, float t)
        {
            float f = t * (pts.Count - 1); int i = Mathf.Min(pts.Count - 2, (int)f);
            return Vector3.Lerp(pts[i], pts[i + 1], f - i);
        }

        static void Tomate(MallaBuilder mb, int etapa, System.Random r)
        {
            float[] altos = { 0.2f, 0.7f, 1.1f, 1.4f, 1.5f };
            float alto = altos[etapa];
            var tallo = TalloRecto(Vector3.zero, alto, r, 0.025f, 6);
            Tallo(mb, tallo, 0.008f + 0.004f * etapa, 0.004f);
            if (etapa >= 1) mb.Tubo(4, new List<Vector3> { new Vector3(0.06f, -0.2f, 0.02f), new Vector3(0.06f, 1.75f, 0.02f) }, new List<float> { 0.011f, 0.009f }, 5, 1f, true);
            int hojas = etapa == 0 ? 4 : 9 + etapa * 4;
            for (int i = 0; i < hojas; i++)
            {
                float t = Mathf.Lerp(0.12f, 0.98f, i / (float)Mathf.Max(1, hojas - 1));
                var b = EnTallo(tallo, t);
                var dir = Rot(Vector3.forward, i * 137.5f) + Vector3.up * R(r, 0.15f, 0.5f);
                float largo = (etapa == 0 ? 0.11f : 0.38f) * (1.15f - t * 0.4f);
                Hoja(mb, b, dir, largo, largo * 0.8f, R(r, 25, 50));
                if (etapa == 2 && i % 3 == 1) for (int k = 0; k < 3; k++) Flor(mb, b + Rot(Vector3.forward, i * 137.5f + 70f) * 0.07f + new Vector3(R(r, -0.03f, 0.03f), -0.02f * k, R(r, -0.03f, 0.03f)), 0.035f);
                if (etapa >= 3 && i % 3 == 1 && t < 0.85f)
                {
                    var racimo = b + Rot(Vector3.forward, i * 137.5f + 80f) * 0.08f + Vector3.down * 0.05f;
                    int n = 3 + r.Next(3);
                    for (int k = 0; k < n; k++)
                    {
                        bool maduro = etapa == 4 && (t < 0.7f || r.NextDouble() < 0.5);
                        float rr = R(r, 0.026f, 0.036f);
                        Esfera(mb, maduro ? 5 : 2, racimo + new Vector3(R(r, -0.05f, 0.05f), -k * 0.035f, R(r, -0.05f, 0.05f)), new Vector3(rr, rr * 0.88f, rr));
                    }
                }
            }
        }

        static void Pimiento(MallaBuilder mb, int etapa, System.Random r)
        {
            float[] altos = { 0.15f, 0.4f, 0.55f, 0.6f, 0.65f };
            float alto = altos[etapa];
            var tallo = TalloRecto(Vector3.zero, alto * 0.45f, r, 0.01f, 3);
            Tallo(mb, tallo, 0.01f, 0.007f);
            var cruz = tallo[tallo.Count - 1];
            int ramas = etapa == 0 ? 1 : 3;
            for (int k = 0; k < ramas; k++)
            {
                var dir = (Rot(Vector3.forward, k * 120f + R(r, -20, 20)) * 0.45f + Vector3.up).normalized;
                var rama = new List<Vector3> { cruz, cruz + dir * alto * 0.3f, cruz + dir * alto * 0.55f + Vector3.up * 0.03f };
                if (etapa > 0) Tallo(mb, rama, 0.006f, 0.003f);
                int hojas = etapa == 0 ? 5 : 6 + etapa;
                for (int i = 0; i < hojas; i++)
                {
                    var b = etapa == 0 ? EnTallo(tallo, 0.4f + i * 0.12f) : EnTallo(rama, R(r, 0.2f, 1f));
                    Hoja(mb, b, Rot(Vector3.forward, i * 137.5f + k * 40f) + Vector3.up * R(r, 0f, 0.4f), etapa == 0 ? 0.07f : 0.13f, 0.08f, R(r, 15, 40));
                }
                if (etapa == 2) for (int i = 0; i < 3; i++) Flor(mb, EnTallo(rama, R(r, 0.4f, 1f)) + Vector3.down * 0.02f, 0.03f);
                if (etapa >= 3)
                    for (int i = 0; i < 2; i++)
                    {
                        var p = EnTallo(rama, R(r, 0.5f, 1f)) + Rot(Vector3.forward, R(r, 0, 360)) * 0.04f + Vector3.down * 0.06f;
                        bool maduro = etapa == 4 && r.NextDouble() < 0.65;
                        Esfera(mb, maduro ? 5 : 2, p, new Vector3(0.032f, 0.06f, 0.032f));
                    }
            }
        }

        static void Judia(MallaBuilder mb, int etapa, System.Random r)
        {
            // cana de enrame desde la siembra
            mb.Tubo(4, new List<Vector3> { new Vector3(0, -0.25f, 0), new Vector3(0.02f, 2.2f, 0.01f) }, new List<float> { 0.013f, 0.01f }, 5, 1f, true);
            if (etapa == 0)
            {
                var t0 = TalloRecto(new Vector3(0.05f, 0, 0), 0.1f, r, 0.005f, 2); Tallo(mb, t0, 0.004f, 0.003f);
                Hoja(mb, t0[2], Vector3.right + Vector3.up * 0.3f, 0.06f, 0.06f, 20); Hoja(mb, t0[2], Vector3.left + Vector3.up * 0.3f, 0.06f, 0.06f, 20);
                return;
            }
            float[] altos = { 0.1f, 1.0f, 1.9f, 2.05f, 2.05f };
            float alto = altos[etapa];
            // guia enroscada a la cana
            var guia = new List<Vector3>();
            for (int i = 0; i <= 24; i++) { float t = i / 24f; guia.Add(new Vector3(Mathf.Cos(t * 14f) * 0.025f, t * alto, Mathf.Sin(t * 14f) * 0.025f)); }
            Tallo(mb, guia, 0.004f, 0.003f, 0, 4);
            int hojas = (int)(alto * 12);
            for (int i = 0; i < hojas; i++)
            {
                float t = (i + 0.5f) / hojas;
                var b = EnTallo(guia, t);
                Hoja(mb, b, Rot(Vector3.forward, i * 137.5f) + Vector3.down * 0.1f, 0.16f, 0.15f, R(r, 30, 60));
                if (etapa == 2 && i % 3 == 0) Flor(mb, b + Rot(Vector3.forward, i * 137.5f + 60f) * 0.06f, 0.025f);
                if (etapa >= 3 && i % 2 == 0 && t < 0.9f)
                {
                    var p = b + Rot(Vector3.forward, i * 137.5f + 70f) * 0.07f;
                    mb.Tubo(2, new List<Vector3> { p, p + new Vector3(0.01f, -0.08f, 0), p + new Vector3(0.025f, -0.15f, 0.01f) }, new List<float> { 0.006f, 0.007f, 0.004f }, 5, 1f, true);
                }
            }
        }

        static void Patata(MallaBuilder mb, int etapa, System.Random r)
        {
            float[] altos = { 0.1f, 0.4f, 0.55f, 0.55f, 0.42f };
            float alto = altos[etapa];
            int tallos = etapa == 0 ? 2 : 5;
            for (int k = 0; k < tallos; k++)
            {
                var dir = (Rot(Vector3.forward, k * 72f + R(r, -15, 15)) * (etapa == 4 ? 0.9f : 0.45f) + Vector3.up).normalized;
                var tallo = new List<Vector3> { Vector3.zero, dir * alto * 0.5f, dir * alto * 0.85f + Vector3.up * alto * 0.1f * (etapa == 4 ? -1f : 1f) };
                Tallo(mb, tallo, 0.007f, 0.004f);
                int hojas = etapa == 0 ? 2 : 5;
                for (int i = 0; i < hojas; i++)
                    Hoja(mb, EnTallo(tallo, 0.3f + i * 0.14f), Rot(Vector3.forward, i * 137.5f + k * 50f) + Vector3.up * 0.2f, etapa == 0 ? 0.08f : 0.24f, 0.18f, etapa == 4 ? 70f : R(r, 20, 45));
                if (etapa == 2) for (int i = 0; i < 3; i++) Flor(mb, tallo[2] + new Vector3(R(r, -0.04f, 0.04f), 0.03f + i * 0.015f, R(r, -0.04f, 0.04f)), 0.035f);
            }
            if (etapa == 4)
                for (int i = 0; i < 3; i++)
                    Esfera(mb, 5, Rot(Vector3.forward, i * 120f + 30f) * R(r, 0.12f, 0.22f) + Vector3.up * 0.01f, new Vector3(0.04f, 0.03f, 0.05f));
        }

        static void Lechuga(MallaBuilder mb, int etapa, System.Random r)
        {
            if (etapa == 4)
            {
                // espigada: tallo floral con hojas pequenas y flores amarillas
                var tallo = TalloRecto(Vector3.zero, 0.7f, r, 0.015f, 5); Tallo(mb, tallo, 0.012f, 0.004f);
                for (int i = 0; i < 10; i++) Hoja(mb, EnTallo(tallo, i / 10f), Rot(Vector3.forward, i * 137.5f) + Vector3.up * 0.6f, 0.12f, 0.08f, 20);
                for (int i = 0; i < 8; i++) Flor(mb, EnTallo(tallo, 0.85f + i * 0.02f) + Rot(Vector3.forward, i * 45f) * 0.05f, 0.025f);
            }
            float[] radios = { 0.07f, 0.14f, 0.16f, 0.18f, 0.12f };
            int[] n = { 5, 10, 14, 18, 8 };
            for (int i = 0; i < n[etapa]; i++)
            {
                float capa = i / (float)n[etapa];
                // las de fuera abiertas, las de dentro cerradas formando el cogollo
                float abrir = etapa >= 2 ? Mathf.Lerp(0.15f, 1.6f, 1f - capa) : 0.9f;
                var dir = Rot(Vector3.forward, i * 137.5f) * abrir + Vector3.up;
                Hoja(mb, Vector3.up * 0.01f, dir, radios[etapa] * Mathf.Lerp(1f, 0.75f, capa) * 1.4f, radios[etapa] * 1.1f, etapa >= 2 ? Mathf.Lerp(45f, -10f, capa) : 35f);
            }
        }

        static void Cebolla(MallaBuilder mb, int etapa, System.Random r)
        {
            float[] bulbo = { 0.008f, 0.012f, 0.025f, 0.042f, 0.05f };
            Esfera(mb, etapa >= 2 ? 5 : 0, Vector3.up * bulbo[etapa] * 0.6f, new Vector3(bulbo[etapa], bulbo[etapa] * 0.9f, bulbo[etapa]));
            int hojas = new[] { 3, 6, 8, 8, 6 }[etapa];
            float largo = new[] { 0.14f, 0.4f, 0.5f, 0.55f, 0.45f }[etapa];
            for (int i = 0; i < hojas; i++)
            {
                var dir = Rot(Vector3.forward, i * 137.5f) * (etapa == 4 ? 1.6f : 0.25f) + Vector3.up;
                Hoja(mb, Vector3.up * bulbo[etapa], dir, largo, 0.035f, etapa == 4 ? 60f : R(r, 5, 25));
            }
        }

        static void Calabaza(MallaBuilder mb, int etapa, System.Random r)
        {
            if (etapa == 0)
            {
                var t0 = TalloRecto(Vector3.zero, 0.08f, r, 0.005f, 2); Tallo(mb, t0, 0.006f, 0.005f);
                for (int i = 0; i < 4; i++) Hoja(mb, t0[2], Rot(Vector3.forward, i * 90f) + Vector3.up * 0.4f, i < 2 ? 0.06f : 0.1f, i < 2 ? 0.04f : 0.09f, 15);
                return;
            }
            float guia = new[] { 0f, 0.6f, 1.0f, 1.2f, 1.25f }[etapa];
            int guias = 3;
            for (int k = 0; k < guias; k++)
            {
                var dir = Rot(Vector3.forward, k * 120f + R(r, -25, 25));
                var pts = new List<Vector3>();
                for (int i = 0; i <= 6; i++) { float t = i / 6f; pts.Add(dir * guia * t + Rot(dir, 90) * Mathf.Sin(t * 5f) * 0.08f + Vector3.up * 0.03f); }
                Tallo(mb, pts, 0.012f, 0.007f);
                for (int i = 1; i <= 8; i++)
                {
                    var b = EnTallo(pts, i / 8.5f);
                    // hoja grande sobre un peciolo largo, como una sombrilla
                    var peciolo = b + Vector3.up * R(r, 0.18f, 0.32f) + Rot(dir, R(r, -60, 60)) * 0.05f;
                    mb.Tubo(0, new List<Vector3> { b, peciolo }, new List<float> { 0.006f, 0.005f }, 4, 1f, false);
                    Hoja(mb, peciolo, Rot(dir, R(r, -90, 90)) + Vector3.up * 0.15f, 0.4f, 0.4f, R(r, 15, 35));
                    if (etapa == 2 && i % 2 == 0) Flor(mb, b + Vector3.up * 0.12f, 0.1f);
                }
                if (etapa >= 3 && k < 2)
                {
                    float rr = etapa == 3 ? 0.12f : 0.17f;
                    Esfera(mb, etapa == 4 ? 5 : 2, EnTallo(pts, 0.55f) + Rot(dir, 90) * 0.15f + Vector3.up * rr * 0.75f, new Vector3(rr, rr * 0.78f, rr), 8, 12);
                }
            }
        }

        static void Berza(MallaBuilder mb, int etapa, System.Random r)
        {
            float alto = new[] { 0.06f, 0.22f, 0.3f, 0.32f, 0.32f }[etapa];
            var tallo = TalloRecto(Vector3.zero, alto, r, 0.01f, 3);
            Tallo(mb, tallo, 0.02f, 0.016f);
            int hojas = new[] { 4, 8, 12, 14, 14 }[etapa];
            float largo = new[] { 0.1f, 0.3f, 0.34f, 0.36f, 0.36f }[etapa];
            for (int i = 0; i < hojas; i++)
            {
                float capa = i / (float)hojas;
                var b = EnTallo(tallo, Mathf.Lerp(0.3f, 1f, capa));
                float abrir = etapa >= 2 ? Mathf.Lerp(1.4f, 0.3f, capa) : 1f;
                Hoja(mb, b, Rot(Vector3.forward, i * 137.5f) * abrir + Vector3.up * 0.7f, largo * Mathf.Lerp(1f, 0.7f, capa), largo * 0.8f, etapa >= 2 ? Mathf.Lerp(40f, -15f, capa) : 30f);
            }
        }

        // ---------------- Materiales y modelos ----------------
        static Material[] MaterialesCultivo(string id, Material tutor, Material cana)
        {
            string hoja; Color tallo, verde, maduro; string flor = "Flor_Blanca";
            switch (id)
            {
                case "Tomate": hoja = "Hoja_Compuesta"; tallo = new Color(0.36f, 0.5f, 0.22f); verde = new Color(0.4f, 0.6f, 0.2f); maduro = new Color(0.78f, 0.1f, 0.05f); flor = "Flor_Amarilla"; break;
                case "Pimiento": hoja = "Hoja_Simple"; tallo = new Color(0.32f, 0.48f, 0.2f); verde = new Color(0.16f, 0.42f, 0.1f); maduro = new Color(0.7f, 0.05f, 0.03f); break;
                case "Judia": hoja = "Hoja_Trifoliada"; tallo = new Color(0.4f, 0.55f, 0.25f); verde = new Color(0.42f, 0.62f, 0.2f); maduro = verde; break;
                case "Patata": hoja = "Hoja_Patata"; tallo = new Color(0.38f, 0.5f, 0.25f); verde = new Color(0.6f, 0.48f, 0.32f); maduro = new Color(0.64f, 0.52f, 0.36f); break;
                case "Lechuga": hoja = "Hoja_Lechuga"; tallo = new Color(0.6f, 0.75f, 0.4f); verde = tallo; maduro = tallo; flor = "Flor_Amarilla"; break;
                case "Cebolla": hoja = "Hoja_Cebolla"; tallo = new Color(0.86f, 0.82f, 0.68f); verde = tallo; maduro = new Color(0.8f, 0.6f, 0.36f); break;
                case "Calabaza": hoja = "Hoja_Calabaza"; tallo = new Color(0.4f, 0.55f, 0.28f); verde = new Color(0.3f, 0.45f, 0.2f); maduro = new Color(0.92f, 0.5f, 0.1f); flor = "Flor_Amarilla"; break;
                default: hoja = "Hoja_Berza"; tallo = new Color(0.55f, 0.65f, 0.55f); verde = tallo; maduro = tallo; break;
            }
            return new[]
            {
                Util.Mat($"Cultivo_{id}_Tallo", null, null, tallo, 0.2f),
                Util.Mat($"Cultivo_{id}_Hoja", Util.Tex($"Plantas/{hoja}.png", hojas: true, max: 512), null, Color.white, 0.3f, recorte: true, dobleCara: true),
                Util.Mat($"Cultivo_{id}_Fruto", null, null, verde, 0.55f),
                Util.Mat($"Cultivo_{id}_Flor", Util.Tex($"Plantas/{flor}.png", hojas: true, max: 256), null, Color.white, 0.2f, recorte: true, dobleCara: true),
                id == "Judia" ? cana : tutor,
                Util.Mat($"Cultivo_{id}_Maduro", null, null, maduro, 0.6f),
            };
        }

        static ModeloPlanta[] Modelos()
        {
            var tutor = Util.Mat("Cultivo_Tutor", Util.Tex("weathered_planks_diff_1k.jpg", max: 1024), null, new Color(0.7f, 0.62f, 0.52f), 0.1f);
            var cana = Util.Mat("Cultivo_Cana", null, null, new Color(0.78f, 0.7f, 0.5f), 0.35f);
            var lista = new List<ModeloPlanta>();
            foreach (var c in Gemelo.Cultivo.Todos)
            {
                var m = new ModeloPlanta { cultivo = c.id, etapas = new Mesh[5], materiales = MaterialesCultivo(c.id, tutor, cana) };
                for (int e = 0; e < 5; e++)
                {
                    string nombre = $"Planta_{c.id}_{e}";
                    m.etapas[e] = Util.Guardar(Planta(c.id, e).Construir(nombre), nombre);
                }
                lista.Add(m);
            }
            return lista.ToArray();
        }

        // ---------------- Bancal en la escena ----------------
        static float Suelo(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;

        /// <summary>Tierra labrada con caballones a lo largo del bancal, pegada al relieve.</summary>
        static Mesh Tierra(Terrain t, Bancal b, Vector3 centro, float marcoFila)
        {
            var mb = new MallaBuilder(1);
            float ancho = b.area.width + 0.5f, largo = b.area.height + 0.5f;
            int nx = Mathf.CeilToInt(ancho / 0.06f), nz = Mathf.CeilToInt(largo / 0.15f);
            for (int iz = 0; iz <= nz; iz++)
                for (int ix = 0; ix <= nx; ix++)
                {
                    float lx = -ancho / 2 + ix * ancho / nx, lz = -largo / 2 + iz * largo / nz;
                    float borde = Mathf.Min(Util.Smooth(0f, 0.3f, ancho / 2 - Mathf.Abs(lx)), Util.Smooth(0f, 0.3f, largo / 2 - Mathf.Abs(lz)));
                    // caballon en cada fila de plantas
                    float fase = (lx + ancho / 2) / Mathf.Max(0.25f, marcoFila) * Mathf.PI * 2f;
                    float alto = 0.02f + borde * (0.05f + 0.045f * Mathf.Cos(fase - Mathf.PI * ((ancho - 0.5f) / Mathf.Max(0.25f, marcoFila) % 1f)));
                    float y = Suelo(t, centro.x + lx, centro.z + lz) - centro.y + alto - (1f - borde) * 0.03f;
                    mb.Vert(new Vector3(lx, y, lz), Vector3.up, new Vector2(centro.x + lx, centro.z + lz) / 1.6f);
                }
            int fila = nx + 1;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iz * fila + ix, bb = a + 1, c = a + fila, d = c + 1;
                    mb.Tri(0, a, c, d); mb.Tri(0, a, d, bb);
                }
            var m = mb.Construir("Tierra_Bancal_" + b.numero);
            m.RecalculateNormals(); m.RecalculateTangents();
            return m;
        }

        static Material MaterialLluvia()
        {
            string ruta = $"{Util.Raiz}/Materiales/Lluvia.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, ruta); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.92f, 0.28f));
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem Lluvia(Transform padre, Bancal b, Material mat)
        {
            var go = new GameObject("Lluvia");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.up * 7f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 1f;
            main.startLifetime = 0.8f; main.startSpeed = 0f; main.startSize = 0.008f; main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(b.area.width + 0.6f, 0.1f, b.area.height + 0.6f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.3f); vel.y = new ParticleSystem.MinMaxCurve(-9f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var ren = go.GetComponent<ParticleSystemRenderer>();
            ren.renderMode = ParticleSystemRenderMode.Stretch; ren.velocityScale = 0.022f; ren.lengthScale = 1f;
            ren.sharedMaterial = mat; ren.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static void Estaca(MallaBuilder mb, Vector3 p, float alto)
        {
            mb.Caja(0, p + Vector3.up * (alto * 0.5f - 0.15f), new Vector3(0.022f, 0, 0), new Vector3(0, alto * 0.5f, 0), new Vector3(0, 0, 0.022f));
        }

        public static void Crear(Terrain t, Transform huerto, Transform camara, Juego.Paseante paseante)
        {
            var modelos = Modelos();
            var madera = Util.Mat("Madera_Tablones", Util.Tex("weathered_planks_diff_1k.jpg", max: 1024), Util.Tex("weathered_planks_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.9f, 0.86f, 0.8f), 0.15f);
            var tierra = Util.Mat("Tierra_Labrada", Util.Tex("dry_ground_rocks_diff_2k.jpg"), Util.Tex("dry_ground_rocks_nor_gl_2k.jpg", normal: true), new Color(0.74f, 0.64f, 0.55f), 0.05f);
            var lluviaMat = MaterialLluvia();

            var raiz = new GameObject("Gemelo_Digital");
            raiz.transform.SetParent(huerto, false);
            var sim = raiz.AddComponent<SimuladorHuerto>();
            sim.enabled = false;   // se activa al final, con todo listo
            sim.datosMeteo = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Util.Raiz}/Datos/Meteo_LaAlberca_1996_2025.csv");
            sim.modelos = modelos;
            var parcelas = new List<ParcelaCultivo>();
            foreach (var b in Bancales)
            {
                var c = Gemelo.Cultivo.Get(b.cultivo);
                var go = new GameObject($"Bancal_{b.numero}_{c.id}");
                go.transform.SetParent(raiz.transform, false);
                var cen = b.area.center;
                var centro = new Vector3(cen.x, Suelo(t, cen.x, cen.y), cen.y);
                go.transform.position = centro;

                var malla = Util.Guardar(Tierra(t, b, centro, c.marcoFila), "Tierra_Bancal_" + b.numero);
                var suelo = Util.Objeto("Tierra", go.transform, malla, tierra);
                suelo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                // estacas en las esquinas, cordel, banderin del color del bancal y cartel con el numero
                var mbE = new MallaBuilder(3);
                float hx = b.area.width / 2 + 0.15f, hz = b.area.height / 2 + 0.15f;
                var esquinas = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz) };
                for (int i = 0; i < 4; i++)
                {
                    var e = esquinas[i]; e.y = Suelo(t, centro.x + e.x, centro.z + e.z) - centro.y; esquinas[i] = e;
                    Estaca(mbE, e, 0.5f);
                }
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = esquinas[i] + Vector3.up * 0.22f, bb = esquinas[(i + 1) % 4] + Vector3.up * 0.22f;
                    mbE.Tubo(1, new List<Vector3> { a, (a + bb) * 0.5f + Vector3.down * 0.03f, bb }, new List<float> { 0.003f, 0.003f, 0.003f }, 3);
                }
                // banderin en la estaca del noroeste
                var mastil = esquinas[3] + new Vector3(0, 0, 0);
                mbE.Caja(0, mastil + Vector3.up * 0.55f, new Vector3(0.012f, 0, 0), new Vector3(0, 0.45f, 0), new Vector3(0, 0, 0.012f));
                mbE.Quad(2, mastil + new Vector3(0.012f, 0.98f, 0), mastil + new Vector3(0.2f, 0.93f, 0), mastil + new Vector3(0.2f, 0.82f, 0), mastil + new Vector3(0.012f, 0.86f, 0));
                mbE.Quad(2, mastil + new Vector3(0.012f, 0.86f, 0), mastil + new Vector3(0.2f, 0.82f, 0), mastil + new Vector3(0.2f, 0.93f, 0), mastil + new Vector3(0.012f, 0.98f, 0));
                var banderin = Util.Mat($"Banderin_{b.numero}", null, null, b.color, 0.2f, dobleCara: true);
                var cordel = Util.Mat("Cordel", null, null, new Color(0.85f, 0.8f, 0.68f), 0.2f);
                var estacas = Util.Objeto("Estacas", go.transform, Util.Guardar(mbE.Construir("Estacas_" + b.numero), "Estacas_" + b.numero), madera, cordel, banderin);
                // cartel: tabla en dos estacas delante del lado sur, con el numero pintado
                var mbC = new MallaBuilder(2);
                var pc = new Vector3(0f, Suelo(t, centro.x, centro.z - hz - 0.25f) - centro.y, -hz - 0.25f);
                Estaca(mbC, pc + new Vector3(-0.13f, 0, 0), 0.6f); Estaca(mbC, pc + new Vector3(0.13f, 0, 0), 0.6f);
                mbC.Caja(0, pc + new Vector3(0, 0.42f, -0.025f), new Vector3(0.18f, 0, 0), new Vector3(0, 0.12f, 0), new Vector3(0, 0, 0.012f));
                float zf = -0.038f;
                mbC.Quad(1, pc + new Vector3(0.11f, 0.31f, zf), pc + new Vector3(-0.11f, 0.31f, zf), pc + new Vector3(-0.11f, 0.53f, zf), pc + new Vector3(0.11f, 0.53f, zf), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
                var numero = Util.Mat($"Cartel_Numero_{b.numero}", Util.Tex($"Plantas/Numero_{b.numero}.png", hojas: true, max: 256), null, Color.white, 0.1f, recorte: true);
                Util.Objeto("Cartel", go.transform, Util.Guardar(mbC.Construir("Cartel_" + b.numero), "Cartel_" + b.numero), madera, numero);

                var p = go.AddComponent<ParcelaCultivo>();
                p.numero = b.numero; p.nombre = b.nombre; p.color = b.color; p.tam = new Vector2(b.area.width, b.area.height);
                p.cultivoId = c.id; p.clima = PerfilClima.Preset(b.clima, b.numero); p.terreno = t;
                p.suelo = suelo.GetComponent<MeshRenderer>();
                p.lluvia = Lluvia(go.transform, b, lluviaMat);
                parcelas.Add(p);
            }
            sim.parcelas = parcelas.ToArray();
            sim.enabled = true;

            // ---- Tableta (UI Toolkit) ----
            var panel = Panel();
            var tgo = new GameObject("Tableta");
            tgo.transform.SetParent(raiz.transform, false);
            var doc = tgo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var tab = tgo.AddComponent<Tableta>();
            tab.simulador = sim; tab.paseante = paseante;
            tab.estilo = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{Util.Raiz}/UI/Tableta.uss");
            // la interfaz necesita un EventSystem que lea el Input System
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log($"Huerto: {parcelas.Count} bancales de cultivo con gemelo digital");
        }

        static PanelSettings Panel()
        {
            string ruta = $"{Util.Raiz}/UI/Tableta_Panel.asset";
            var p = AssetDatabase.LoadAssetAtPath<PanelSettings>(ruta);
            if (p == null) { p = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(p, ruta); }
            p.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>($"{Util.Raiz}/UI/TemaRuntime.tss");
            p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p.referenceResolution = new Vector2Int(1920, 1080);
            p.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            p.match = 0.5f;
            p.sortingOrder = 10;
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
