using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Huerto
{
    /// <summary>Arbolado de los alrededores (parcelas vecinas y monte) como arboles de terreno instanciados.</summary>
    public static class Bosque
    {
        struct Variante { public string especie; public float alto; public int semilla; public bool lejano; }

        static readonly Variante[] Cercanos =
        {
            new Variante { especie = "Roble", alto = 13f, semilla = 101 },
            new Variante { especie = "Roble", alto = 10.5f, semilla = 102 },
            new Variante { especie = "Castano", alto = 10f, semilla = 103 },
            new Variante { especie = "Nogal", alto = 9f, semilla = 104 },
            new Variante { especie = "Cerezo", alto = 6f, semilla = 105 },
            new Variante { especie = "RobleSeco", alto = 11f, semilla = 106 },
        };

        static readonly Variante[] Lejanos =
        {
            new Variante { especie = "Roble", alto = 12f, semilla = 201, lejano = true },
            new Variante { especie = "Castano", alto = 11f, semilla = 202, lejano = true },
            new Variante { especie = "Roble", alto = 9f, semilla = 203, lejano = true },
        };

        static readonly Dictionary<string, GameObject> hechos = new Dictionary<string, GameObject>();

        static GameObject Prefab(Variante v, Dictionary<string, Material> cortezas, Dictionary<string, Material> hojas)
        {
            if (hechos.TryGetValue($"{v.especie}_{v.semilla}", out var ya) && ya != null) return ya;
            Util.Carpeta($"{Util.Raiz}/Generado/Prefabs");
            var esp = Arboles.Get(v.especie);
            if (v.lejano) { esp.hojasPorRama = Mathf.Max(2, esp.hojasPorRama / 3); esp.hoja *= 1.8f; esp.niveles = Mathf.Min(esp.niveles, 2); }
            string nombre = $"Bosque_{v.especie}_{v.semilla}";
            var mats = esp.hojas != null ? new[] { cortezas[esp.corteza], hojas[esp.hojas] } : new[] { cortezas[esp.corteza] };
            // tres niveles de detalle con la misma silueta: de cerca, a media distancia y de lejos
            var go = new GameObject(nombre);
            var lods = new LOD[3];
            float[] pantalla = { 0.3f, 0.08f, 0.005f };   // con lodBias 2: LOD0 hasta ~55 m, LOD1 hasta ~200 m (arbol de 10 m)
            for (int d = 0; d < 3; d++)
            {
                string nl = $"{nombre}_LOD{d}";
                var malla = Util.Guardar(Arboles.Generar(esp, v.alto, v.semilla, nl, d), nl);
                var hijo = new GameObject(nl);
                hijo.transform.SetParent(go.transform, false);
                hijo.AddComponent<MeshFilter>().sharedMesh = malla;
                var r = hijo.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                if (d == 2) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lods[d] = new LOD(pantalla[d], new Renderer[] { r });
            }
            var grupo = go.AddComponent<LODGroup>();
            grupo.SetLODs(lods);
            grupo.RecalculateBounds();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Util.Raiz}/Generado/Prefabs/{nombre}.prefab");
            Object.DestroyImmediate(go);
            hechos[$"{v.especie}_{v.semilla}"] = prefab;
            return prefab;
        }

        static TreePrototype[] Prototipos(Variante[] vars, Dictionary<string, Material> cortezas, Dictionary<string, Material> hojas)
        {
            var lista = new List<TreePrototype>();
            foreach (var v in vars) lista.Add(new TreePrototype { prefab = Prefab(v, cortezas, hojas) });
            return lista.ToArray();
        }

        static void Ajustes(Terrain t, float distancia)
        {
            t.treeDistance = distancia;
            t.treeBillboardDistance = distancia;   // sin billboards: en URP salen mal con mallas que no son SpeedTree
            t.treeCrossFadeLength = 0;
            t.treeMaximumFullLODCount = 100000;
        }

        /// <summary>
        /// Arboles lejanos: los que desde el huerto siempre se ven en su LOD2 pasan a Juego.ArbolesFundidos,
        /// que los funde en una malla por cuadro de 250 m al cargar la escena (solo se guarda la lista).
        /// </summary>
        class Fundidos
        {
            readonly List<Juego.ArbolesFundidos.Modelo> modelos = new List<Juego.ArbolesFundidos.Modelo>();
            readonly Dictionary<GameObject, int> indice = new Dictionary<GameObject, int>();
            readonly List<int> modelo = new List<int>();
            readonly List<Vector3> posicion = new List<Vector3>();
            readonly List<float> giro = new List<float>();
            readonly List<Vector2> escala = new List<Vector2>();
            public int Cuantos => posicion.Count;

            public List<TreeInstance> Separar(Terrain t, List<TreeInstance> l)
            {
                var td = t.terrainData; Vector3 o = t.transform.position, tam = td.size;
                var protos = td.treePrototypes;
                var cerca = new List<TreeInstance>();
                foreach (var ti in l)
                {
                    var pref = protos[ti.prototypeIndex].prefab;
                    float alto = pref.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.max.y * ti.heightScale;
                    var p = new Vector3(o.x + ti.position.x * tam.x, 0, o.z + ti.position.z * tam.z);
                    float d = Util.DistanciaPolilinea(new Vector2(p.x, p.z), Datos.Parcela, true) - 5f;
                    // con lodBias 2 el LOD2 entra cuando el arbol ocupa menos del 4 % de la pantalla: a unas 21 veces su altura
                    if (d < 22f * alto) { cerca.Add(ti); continue; }
                    if (!indice.TryGetValue(pref, out int id))
                    {
                        var lod2 = pref.transform.GetChild(2);
                        modelos.Add(new Juego.ArbolesFundidos.Modelo { malla = lod2.GetComponent<MeshFilter>().sharedMesh, materiales = lod2.GetComponent<MeshRenderer>().sharedMaterials });
                        indice[pref] = id = modelos.Count - 1;
                    }
                    p.y = t.SampleHeight(p) + o.y;
                    modelo.Add(id); posicion.Add(p); giro.Add(ti.rotation * Mathf.Rad2Deg); escala.Add(new Vector2(ti.widthScale, ti.heightScale));
                }
                return cerca;
            }

            public void Construir(Transform padre)
            {
                foreach (var guid in AssetDatabase.FindAssets("Lejanos_ t:Mesh", new[] { $"{Util.Raiz}/Generado" })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                var go = new GameObject("Arboles_Lejanos");
                go.transform.SetParent(padre, false);
                var f = go.AddComponent<Juego.ArbolesFundidos>();
                f.modelos = modelos.ToArray(); f.modelo = modelo.ToArray(); f.posicion = posicion.ToArray(); f.giro = giro.ToArray(); f.escala = escala.ToArray();
                f.Construir();
            }
        }

        public static void Poblar(Terrain huerto, Terrain pueblo, Terrain sierra, List<Datos.Poligono> parcelas, List<Datos.Poligono> edificios,
            Dictionary<string, Material> cortezas, Dictionary<string, Material> hojas, List<Datos.Arbol> vecinos, Visibilidad.Datos2 vis)
        {
            hechos.Clear();
            int quitados = 0;
            // solo se plantan los arboles que se ven desde el huerto (o cuya sombra se ve)
            var lejanos = new Fundidos();
            TreeInstance[] Visibles(Terrain t, List<TreeInstance> l)
            {
                if (vis == null) return l.ToArray();
                var r = l.FindAll(ti => vis.Arbol(t, ti));
                quitados += l.Count - r.Count;
                // los que desde el huerto siempre se ven en su LOD mas simple se funden en mallas por zonas
                return lejanos.Separar(t, r).ToArray();
            }
            var rnd = new System.Random(99);
            // Cajas de edificios para descartar
            var cajasEd = new List<Rect>();
            foreach (var e in edificios)
            {
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                foreach (var q in e.pts) { x0 = Mathf.Min(x0, q.x); z0 = Mathf.Min(z0, q.y); x1 = Mathf.Max(x1, q.x); z1 = Mathf.Max(z1, q.y); }
                cajasEd.Add(Rect.MinMaxRect(x0 - 2.5f, z0 - 2.5f, x1 + 2.5f, z1 + 2.5f));
            }
            System.Func<Vector2, bool> enEdificio = p => { foreach (var c in cajasEd) if (c.Contains(p)) return true; return false; };
            var cajasPar = new List<Rect>();
            foreach (var pa in parcelas)
            {
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                foreach (var q in pa.pts) { x0 = Mathf.Min(x0, q.x); z0 = Mathf.Min(z0, q.y); x1 = Mathf.Max(x1, q.x); z1 = Mathf.Max(z1, q.y); }
                cajasPar.Add(Rect.MinMaxRect(x0, z0, x1, z1));
            }

            // ---- Parcelas vecinas (terreno detallado) ----
            var dh = huerto.terrainData;
            dh.treePrototypes = Prototipos(Cercanos, cortezas, hojas);
            var inst = new List<TreeInstance>();
            foreach (var a in vecinos)
            {
                if (a.pos.x < 0.5f || a.pos.x > Datos.Tam - 0.5f || a.pos.y < 0.5f || a.pos.y > Datos.Tam - 0.5f) continue;
                int proto;
                switch (a.especie)
                {
                    case "Roble": proto = rnd.Next(0, 2); break;
                    case "Castano": case "Tuya": proto = 2; break;
                    case "Nogal": proto = 3; break;
                    case "RobleSeco": proto = 5; break;
                    default: proto = 4; break;
                }
                float s = Mathf.Clamp(a.alto / Cercanos[proto].alto, 0.35f, 1.8f);
                inst.Add(new TreeInstance
                {
                    position = new Vector3(a.pos.x / Datos.Tam, 0, a.pos.y / Datos.Tam), prototypeIndex = proto,
                    widthScale = s * Util.Rng(rnd, 0.9f, 1.1f), heightScale = s, rotation = Util.Rng(rnd, 0, 6.283f),
                    color = Color.white, lightmapColor = Color.white,
                });
            }
            var vh = Visibles(huerto, inst); dh.SetTreeInstances(vh, true);
            Ajustes(huerto, 600f);
            EditorUtility.SetDirty(dh);

            // ---- Pueblo y alrededores: arboles reales del LiDAR (posicion y altura) ----
            var dp = pueblo.terrainData;
            dp.treePrototypes = Prototipos(Cercanos, cortezas, hojas);
            var inst1 = new List<TreeInstance>();
            foreach (var l in System.IO.File.ReadAllLines($"{Util.Raiz}/Datos/Arboles_Pueblo_LiDAR.txt"))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var c = l.Split(';');
                float F(int i) => float.Parse(c[i], System.Globalization.CultureInfo.InvariantCulture);
                float x = F(0), z = F(1), h = F(2);
                float nx = (x - Terreno.PuebloOrigen.x) / Terreno.PuebloTam, nz = (z - Terreno.PuebloOrigen.y) / Terreno.PuebloTam;
                if (nx <= 0.002f || nx >= 0.998f || nz <= 0.002f || nz >= 0.998f) continue;
                // castanar y rebollar alrededor del pueblo; nogales y cerezos en los huertos
                double r = rnd.NextDouble();
                int proto = h < 6.5f ? (r < 0.5 ? 4 : r < 0.8 ? 2 : 3) : r < 0.45 ? 2 : r < 0.68 ? 0 : r < 0.88 ? 1 : r < 0.95 ? 3 : 4;
                float s = Mathf.Clamp(h / Cercanos[proto].alto, 0.3f, 2.2f);
                inst1.Add(new TreeInstance
                {
                    position = new Vector3(nx, 0, nz), prototypeIndex = proto,
                    widthScale = s * Util.Rng(rnd, 0.9f, 1.15f), heightScale = s, rotation = Util.Rng(rnd, 0, 6.283f),
                    color = Color.white, lightmapColor = Color.white,
                });
            }
            var vp = Visibles(pueblo, inst1); dp.SetTreeInstances(vp, true);
            Ajustes(pueblo, 1200f);
            EditorUtility.SetDirty(dp);

            // ---- Monte alrededor (terreno de la sierra) ----
            var ds = sierra.terrainData;
            var cerca = Prototipos(Cercanos, cortezas, hojas);
            var lejos = Prototipos(Lejanos, cortezas, hojas);
            var protos = new List<TreePrototype>(cerca); protos.AddRange(lejos);
            ds.treePrototypes = protos.ToArray();
            var inst2 = new List<TreeInstance>();
            Vector3 o = sierra.transform.position; Vector3 tam = ds.size;
            const float Radio = 1300f;
            for (float z = 96f - Radio; z < 96f + Radio; z += 11f)
                for (float x = 96f - Radio; x < 96f + Radio; x += 11f)
                {
                    var p = new Vector2(x + Util.Rng(rnd, -5f, 5f), z + Util.Rng(rnd, -5f, 5f));
                    float d = Vector2.Distance(p, new Vector2(96f, 96f));
                    if (d > Radio) continue;
                    if (Mathf.Abs(p.x - Terreno.PuebloCentro.x) < Terreno.PuebloTam / 2 + 1f && Mathf.Abs(p.y - Terreno.PuebloCentro.y) < Terreno.PuebloTam / 2 + 1f) continue;   // ya cubierto por el LiDAR
                    if (enEdificio(p)) continue;
                    float nx = (p.x - o.x) / tam.x, nz = (p.y - o.z) / tam.z;
                    if (ds.GetSteepness(nx, nz) > 34f) continue;
                    float altura = ds.GetInterpolatedHeight(nx, nz) + o.y + Datos.Base;
                    if (altura > 1550f) continue;
                    float n = Mathf.PerlinNoise(p.x * 0.006f + 3f, p.y * 0.006f + 17f);
                    float claro = Mathf.PerlinNoise(p.x * 0.02f + 8f, p.y * 0.02f + 1f);
                    float dens = Util.Smooth(0.28f, 0.55f, n) * Util.Smooth(0.25f, 0.45f, claro);
                    if (rnd.NextDouble() > dens) continue;
                    int proto = d < 450f ? rnd.Next(0, cerca.Length - 1) : cerca.Length + rnd.Next(0, lejos.Length);
                    float s = Util.Rng(rnd, 0.8f, 1.25f);
                    inst2.Add(new TreeInstance
                    {
                        position = new Vector3(nx, 0, nz), prototypeIndex = proto,
                        widthScale = s * Util.Rng(rnd, 0.9f, 1.15f), heightScale = s, rotation = Util.Rng(rnd, 0, 6.283f),
                        color = Color.white, lightmapColor = Color.white,
                    });
                }
            var vs = Visibles(sierra, inst2); ds.SetTreeInstances(vs, true);
            Ajustes(sierra, 2500f);
            EditorUtility.SetDirty(ds);
            if (vis != null) lejanos.Construir(sierra.transform.parent);
            Debug.Log($"Huerto: {vh.Length + vp.Length + vs.Length} arboles de terreno ({vh.Length} junto al huerto, {vp.Length} en el pueblo, {vs.Length} en el monte) y {lejanos.Cuantos} lejanos fundidos en mallas por cuadros" + (vis != null ? $" ({quitados} quitados por no verse desde el huerto)" : ""));
        }
    }
}
