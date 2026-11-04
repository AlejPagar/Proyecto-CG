using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Huerto
{
    /// <summary>Objetos vistos en las fotos: deposito, malla, pilar, caseta, tablones, plumero, rocas y ramas.</summary>
    public static class Objetos
    {
        static float Suelo(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;

        /// <summary>Deposito IBC de 1000 L: palet, deposito blanco y jaula metalica (fotos 105655, 105726).</summary>
        public static void Deposito(Terrain t, Transform padre, Vector2 pos, float rumbo, Material blanco, Material metal, Material palet)
        {
            var mb = new MallaBuilder(3);
            Vector3 ex = Vector3.right * 0.6f, ez = Vector3.forward * 0.5f;
            mb.Caja(2, new Vector3(0, 0.075f, 0), ex, Vector3.up * 0.075f, ez);
            mb.Caja(0, new Vector3(0, 0.66f, 0), ex * 0.95f, Vector3.up * 0.5f, ez * 0.95f);
            var tapa = new List<Vector3> { new Vector3(0, 1.15f, 0), new Vector3(0, 1.22f, 0) };
            mb.Tubo(0, tapa, new List<float> { 0.11f, 0.11f }, 12, 1f, true);
            // jaula: tubos verticales y horizontales en las cuatro caras y arriba
            float r = 0.011f;
            for (int cara = 0; cara < 4; cara++)
            {
                Vector3 n = cara == 0 ? Vector3.right : cara == 1 ? Vector3.left : cara == 2 ? Vector3.forward : Vector3.back;
                float medio = cara < 2 ? 0.6f : 0.5f, ancho = cara < 2 ? 0.5f : 0.6f;
                Vector3 lado = Vector3.Cross(Vector3.up, n);
                for (int i = 0; i <= 6; i++)
                {
                    Vector3 b = n * (medio + 0.015f) + lado * Mathf.Lerp(-ancho, ancho, i / 6f);
                    mb.Tubo(1, new List<Vector3> { b + Vector3.up * 0.15f, b + Vector3.up * 1.17f }, new List<float> { r, r }, 5);
                }
                for (int j = 0; j <= 5; j++)
                {
                    float y = Mathf.Lerp(0.18f, 1.17f, j / 5f);
                    Vector3 c = n * (medio + 0.015f) + Vector3.up * y;
                    mb.Tubo(1, new List<Vector3> { c - lado * ancho, c + lado * ancho }, new List<float> { r, r }, 5);
                }
            }
            for (int i = 0; i <= 5; i++)
            {
                float x = Mathf.Lerp(-0.6f, 0.6f, i / 5f);
                mb.Tubo(1, new List<Vector3> { new Vector3(x, 1.18f, -0.5f), new Vector3(x, 1.18f, 0.5f) }, new List<float> { r, r }, 5);
            }
            var go = Util.Objeto("Deposito_IBC_1000L", padre, Util.Guardar(mb.Construir("Deposito_IBC"), "Deposito_IBC"), blanco, metal, palet);
            go.transform.position = new Vector3(pos.x, Suelo(t, pos.x, pos.y) - 0.02f, pos.y);
            go.transform.rotation = Quaternion.Euler(0, rumbo, 0);
            go.AddComponent<BoxCollider>().center = new Vector3(0, 0.6f, 0);
            go.GetComponent<BoxCollider>().size = new Vector3(1.2f, 1.2f, 1.0f);
        }

        /// <summary>Malla de ocultacion verde sobre postes a lo largo del lindero del camino (foto 105655).</summary>
        public static void Malla(Terrain t, Transform padre, Vector2[] linea, Material tela, Material poste)
        {
            var mb = new MallaBuilder(2);
            float altoMalla = 1.75f;
            for (int i = 0; i < linea.Length - 1; i++)
            {
                Vector2 a = linea[i], b = linea[i + 1];
                float L = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.CeilToInt(L / 2.5f));
                for (int k = 0; k < n; k++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, k / (float)n), p1 = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    float y0 = Suelo(t, p0.x, p0.y) + 0.55f, y1 = Suelo(t, p1.x, p1.y) + 0.55f;
                    var q0 = new Vector3(p0.x, y0, p0.y); var q1 = new Vector3(p1.x, y1, p1.y);
                    float combado = 0.06f;
                    Vector3 mid = (q0 + q1) * 0.5f + Vector3.Cross(q1 - q0, Vector3.up).normalized * combado;
                    float u0 = 0, u1 = Vector2.Distance(p0, p1) * 0.5f;
                    mb.Quad(0, q0, q0 + Vector3.up * altoMalla, mid + Vector3.up * altoMalla, mid, new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u1, 1), new Vector2(u1, 0));
                    mb.Quad(0, mid, mid + Vector3.up * altoMalla, q1 + Vector3.up * altoMalla, q1, new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u1 * 2, 1), new Vector2(u1 * 2, 0));
                    mb.Tubo(1, new List<Vector3> { q0 - Vector3.up * 0.3f, q0 + Vector3.up * (altoMalla + 0.05f) }, new List<float> { 0.025f, 0.025f }, 6, 1f, true);
                }
            }
            Util.Objeto("Malla_Ocultacion", padre, Util.Guardar(mb.Construir("Malla_Ocultacion"), "Malla_Ocultacion"), tela, poste);
        }

        /// <summary>Pilar de piedras apiladas (foto 105655).</summary>
        public static void Pilar(Terrain t, Transform padre, Vector2 pos, float alto, Material piedra, string nombre)
        {
            var rnd = new System.Random(Util.Hash(nombre));
            var mb = new MallaBuilder(1);
            float y = Suelo(t, pos.x, pos.y) - 0.2f;
            float tope = y + alto;
            while (y < tope)
            {
                float h = Util.Rng(rnd, 0.18f, 0.28f);
                for (int i = 0; i < 2; i++)
                {
                    float ang = Util.Rng(rnd, 0, 360);
                    var off = Quaternion.Euler(0, ang, 0) * Vector3.right * 0.08f;
                    mb.Piedra(0, new Vector3(pos.x, y + h * 0.5f, pos.y) + off * (i == 0 ? 1 : -1),
                        Quaternion.Euler(Util.Rng(rnd, -4, 4), ang, Util.Rng(rnd, -4, 4)), new Vector3(0.24f, h * 0.55f, 0.2f), rnd, 0.4f);
                }
                y += h;
            }
            Util.Objeto(nombre, padre, Util.Guardar(mb.Construir(nombre), nombre), piedra);
        }

        /// <summary>Tablones de madera tirados junto al lindero (fotos 105906, 105908).</summary>
        public static void Tablones(Terrain t, Transform padre, Material madera)
        {
            var mb = new MallaBuilder(1);
            var tabs = new[] { (new Vector2(99.0f, 112.6f), new Vector2(98.2f, 109.4f)), (new Vector2(99.25f, 112.4f), new Vector2(98.6f, 109.0f)), (new Vector2(98.7f, 111.2f), new Vector2(97.9f, 108.6f)) };
            foreach (var (a, b) in tabs)
            {
                Vector3 pa = new Vector3(a.x, Suelo(t, a.x, a.y) + 0.03f, a.y), pb = new Vector3(b.x, Suelo(t, b.x, b.y) + 0.03f, b.y);
                Vector3 eje = (pb - pa) * 0.5f;
                Vector3 lado = Vector3.Cross(eje.normalized, Vector3.up).normalized;
                mb.Caja(0, (pa + pb) * 0.5f, eje, Vector3.Cross(lado, eje.normalized).normalized * 0.02f, lado * 0.1f, 1f);
            }
            Util.Objeto("Tablones", padre, Util.Guardar(mb.Construir("Tablones"), "Tablones"), madera);
        }

        /// <summary>Caseta de aperos de piedra con tejado de teja y puerta (foto 105908, al fondo del pasillo).</summary>
        public static void Caseta(Terrain t, Transform padre, Vector2 pos, float rumbo, Material muro, Material teja, Material puerta)
        {
            var mb = new MallaBuilder(3);
            float ancho = 2.4f, fondo = 2.0f, alero = 2.1f;
            float y0 = Suelo(t, pos.x, pos.y);
            mb.Caja(0, new Vector3(0, alero * 0.5f - 0.3f, 0), Vector3.right * ancho * 0.5f, Vector3.up * (alero * 0.5f + 0.3f), Vector3.forward * fondo * 0.5f, 0.6f);
            // tejado a un agua
            float vuelo = 0.25f;
            Vector3 a = new Vector3(-ancho / 2 - vuelo, alero + 0.35f, fondo / 2 + vuelo), b = new Vector3(ancho / 2 + vuelo, alero + 0.35f, fondo / 2 + vuelo);
            Vector3 c = new Vector3(ancho / 2 + vuelo, alero - 0.05f, -fondo / 2 - vuelo), d = new Vector3(-ancho / 2 - vuelo, alero - 0.05f, -fondo / 2 - vuelo);
            mb.Quad(1, a, b, c, d, new Vector2(0, 0), new Vector2(ancho, 0), new Vector2(ancho, fondo), new Vector2(0, fondo));
            mb.Quad(1, d, c, b, a, new Vector2(0, 0), new Vector2(ancho, 0), new Vector2(ancho, fondo), new Vector2(0, fondo));
            mb.Caja(0, new Vector3(0, alero + 0.15f, fondo / 2 - 0.1f), Vector3.right * ancho * 0.5f, Vector3.up * 0.2f, Vector3.forward * 0.1f, 0.6f);
            // puerta en la cara -z
            float z = -fondo / 2 - 0.02f;
            mb.Quad(2, new Vector3(-0.45f, 0, z), new Vector3(-0.45f, 1.85f, z), new Vector3(0.45f, 1.85f, z), new Vector3(0.45f, 0, z));
            var go = Util.Objeto("Caseta_Aperos", padre, Util.Guardar(mb.Construir("Caseta_Aperos"), "Caseta_Aperos"), muro, teja, puerta);
            go.transform.position = new Vector3(pos.x, y0, pos.y);
            go.transform.rotation = Quaternion.Euler(0, rumbo, 0);
            go.AddComponent<BoxCollider>().center = new Vector3(0, 1f, 0);
            go.GetComponent<BoxCollider>().size = new Vector3(ancho, 2.2f, fondo);
        }

        /// <summary>Puerta verde de chapa/madera en el lindero oeste del pasillo (foto 102440).</summary>
        public static void PuertaVerde(Terrain t, Transform padre, Vector2 pos, float rumbo, Material puerta)
        {
            var mb = new MallaBuilder(1);
            mb.Caja(0, new Vector3(0, 0.85f, 0), Vector3.right * 0.5f, Vector3.up * 0.85f, Vector3.forward * 0.03f, 1f);
            var go = Util.Objeto("Puerta_Verde", padre, Util.Guardar(mb.Construir("Puerta_Verde"), "Puerta_Verde"), puerta);
            go.transform.position = new Vector3(pos.x, Suelo(t, pos.x, pos.y) - 0.02f, pos.y);
            go.transform.rotation = Quaternion.Euler(0, rumbo, 0);
        }

        /// <summary>Plumero (hierba de la Pampa): hojas arqueadas y espigas crema (fotos 102440, 105908).</summary>
        public static void Plumero(Terrain t, Transform padre, Vector2 pos, Material hojasMat, Material espigaMat)
        {
            var rnd = new System.Random(5);
            var mb = new MallaBuilder(2);
            for (int i = 0; i < 90; i++)
            {
                float az = Util.Rng(rnd, 0, Mathf.PI * 2), largo = Util.Rng(rnd, 1.0f, 1.7f), incl = Util.Rng(rnd, 0.25f, 0.9f);
                Vector3 h = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 lado = Vector3.Cross(h, Vector3.up) * 0.018f;
                Vector3 b = h * Util.Rng(rnd, 0, 0.3f);
                Vector3 prev = b;
                for (int s = 1; s <= 5; s++)
                {
                    float tt = s / 5f;
                    Vector3 p = b + (Vector3.up * (1 - incl * tt) + h * incl * tt * 1.3f).normalized * largo * tt;
                    p.y = b.y + largo * tt * (1f - incl * tt * tt * 0.9f);
                    float w0 = 1f - (s - 1) / 5f, w1 = 1f - tt;
                    var nrm = (h + Vector3.up).normalized;
                    int i0 = mb.Vert(prev - lado * w0, nrm, new Vector2(0, tt - 0.2f)), i1 = mb.Vert(prev + lado * w0, nrm, new Vector2(1, tt - 0.2f));
                    int i2 = mb.Vert(p + lado * w1, nrm, new Vector2(1, tt)), i3 = mb.Vert(p - lado * w1, nrm, new Vector2(0, tt));
                    mb.Tri(0, i0, i3, i2); mb.Tri(0, i0, i2, i1);
                    prev = p;
                }
            }
            for (int i = 0; i < 12; i++)
            {
                float az = Util.Rng(rnd, 0, Mathf.PI * 2);
                Vector3 h = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 b = h * Util.Rng(rnd, 0, 0.2f);
                Vector3 tope = b + (Vector3.up + h * Util.Rng(rnd, 0.05f, 0.25f)).normalized * Util.Rng(rnd, 2.0f, 2.6f);
                mb.Tubo(0, new List<Vector3> { b, tope - (tope - b).normalized * 0.7f }, new List<float> { 0.008f, 0.006f }, 4);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 lado = Quaternion.Euler(0, k * 90 + az * Mathf.Rad2Deg, 0) * Vector3.right * 0.16f;
                    Vector3 pb = tope - (tope - b).normalized * 0.75f;
                    mb.Quad(1, pb - lado, tope - lado, tope + lado, pb + lado, new Vector2(0, 0.3f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0.3f));
                }
            }
            var go = Util.Objeto("Plumero", padre, Util.Guardar(mb.Construir("Plumero"), "Plumero"), hojasMat, espigaMat);
            go.transform.position = new Vector3(pos.x, Suelo(t, pos.x, pos.y) - 0.05f, pos.y);
        }

        static GameObject Instanciar(string ruta, Transform padre, Material mat, Vector3 pos, Quaternion rot, float escala, string nombre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null) { Debug.LogWarning("Falta modelo " + ruta); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * escala;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Bolos de granito (fotos 105645, 105610) con modelos CC0 de Poly Haven.</summary>
        public static void Rocas(Terrain t, Transform padre, Material[] mats)
        {
            var rnd = new System.Random(31);
            string[] modelos = { "namaqualand_boulder_02", "namaqualand_boulder_04", "namaqualand_boulder_05" };
            var sitios = new (Vector2 p, float s)[]
            {
                (new Vector2(93.6f, 80.9f), 1.2f), (new Vector2(94.8f, 81.9f), 0.95f), (new Vector2(95.4f, 80.5f), 0.8f), (new Vector2(93.9f, 82.6f), 0.7f),
                (new Vector2(90.6f, 83.2f), 1.1f), (new Vector2(97.0f, 82.4f), 0.55f), (new Vector2(106.6f, 89.1f), 1.5f), (new Vector2(105.2f, 88.4f), 0.8f),
                (new Vector2(104.8f, 79.4f), 0.7f), (new Vector2(112.4f, 82.2f), 0.9f), (new Vector2(100.2f, 86.9f), 0.5f), (new Vector2(89.2f, 97.2f), 0.7f),
                (new Vector2(93.8f, 74.2f), 0.6f), (new Vector2(113.6f, 76.4f), 0.8f),
            };
            for (int i = 0; i < sitios.Length; i++)
            {
                var (p, s) = sitios[i];
                int m = i % modelos.Length;
                var pos = new Vector3(p.x, Suelo(t, p.x, p.y) - s * 0.15f, p.y);
                var go = Instanciar($"{Util.Raiz}/Modelos/Importados/{modelos[m]}/{modelos[m]}_1k.fbx", padre, mats[m], pos,
                    Quaternion.Euler(Util.Rng(rnd, -8, 8), Util.Rng(rnd, 0, 360), Util.Rng(rnd, -8, 8)), 1f, "Roca_Granito_" + i);
                if (go == null) continue;
                // 's' es la medida mayor deseada en metros
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float mayor = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                go.transform.localScale = Vector3.one * (1.35f * s / Mathf.Max(0.01f, mayor));
            }
        }

        /// <summary>Montones de ramas secas de la poda (foto 105610).</summary>
        public static void Ramas(Terrain t, Transform padre, Material mat)
        {
            var rnd = new System.Random(41);
            var montones = new[] { new Vector2(95.6f, 92.6f), new Vector2(99.2f, 95.4f), new Vector2(91.8f, 98.0f) };
            int n = 0;
            foreach (var c in montones)
                for (int k = 0; k < 5; k++)
                {
                    var p = c + new Vector2(Util.Rng(rnd, -0.8f, 0.8f), Util.Rng(rnd, -0.6f, 0.6f));
                    var pos = new Vector3(p.x, Suelo(t, p.x, p.y) + k * 0.08f, p.y);
                    Instanciar($"{Util.Raiz}/Modelos/Importados/dry_branches_medium_01/dry_branches_medium_01_1k.fbx", padre, mat, pos,
                        Quaternion.Euler(Util.Rng(rnd, -10, 10), Util.Rng(rnd, 0, 360), Util.Rng(rnd, -10, 10)), Util.Rng(rnd, 1.1f, 1.6f), "Ramas_Secas_" + n++);
                }
        }
    }
}
