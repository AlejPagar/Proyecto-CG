using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Huerto
{
    /// <summary>
    /// Cuenca visual del huerto: lanza rayos desde los puntos donde puede estar la camara del jugador
    /// (dentro de la parcela y hasta 5 m fuera de ella, a la altura de los ojos y de la camara en tercera persona)
    /// hacia cada casa, cada arbol y el suelo del pueblo. Lo que ningun rayo alcanza, ni su sombra, no se genera.
    /// Tapan la vista el terreno, las casas y los muros; los arboles no se cuentan como obstaculo (lado seguro).
    /// </summary>
    public static class Visibilidad
    {
        public const string Archivo = "Visibilidad_Huerto.txt";
        static string Ruta => $"{Util.Raiz}/Datos/{Archivo}";

        // Alturas de la camara sobre el suelo: ojos (primera persona) y camara orbital (tercera persona, inclinacion maxima 45 grados)
        static readonly float[] AlturasCamara = { 1.6f, 3.0f, 4.6f };
        const float PasoPuntos = 2.5f, MargenFuera = 5f;

        // ---------------- Resultado guardado ----------------
        public class Datos2
        {
            public HashSet<string> edificios = new HashSet<string>();
            public HashSet<long> arboles = new HashSet<long>();
            public Rect sueloPueblo;
            public bool Arbol(Terrain t, TreeInstance ti) => arboles.Contains(Clave(t, ti));
        }

        /// <summary>Identificador de un arbol de terreno por su posicion (decimetros).</summary>
        public static long Clave(Terrain t, TreeInstance ti)
        {
            Vector3 o = t.transform.position, tam = t.terrainData.size;
            return ((long)Mathf.RoundToInt((o.x + ti.position.x * tam.x) * 10f) << 32) ^ (uint)Mathf.RoundToInt((o.z + ti.position.z * tam.z) * 10f);
        }

        /// <summary>Lee la cuenca visual guardada, o null si no existe (entonces se genera todo).</summary>
        public static Datos2 Leer()
        {
            if (!File.Exists(Ruta)) return null;
            var d = new Datos2();
            foreach (var l in File.ReadAllLines(Ruta))
            {
                if (l.Length < 2 || l[0] == '#') continue;
                var c = l.Split(';');
                float F(int i) => float.Parse(c[i], CultureInfo.InvariantCulture);
                switch (c[0])
                {
                    case "E": d.edificios.Add(c[1]); break;
                    case "A": d.arboles.Add(long.Parse(c[1], CultureInfo.InvariantCulture)); break;
                    case "R": d.sueloPueblo = Rect.MinMaxRect(F(1), F(2), F(3), F(4)); break;
                }
            }
            return d;
        }

        // ---------------- Calculo ----------------
        [MenuItem("Huerto/Calcular cuenca visual y regenerar")]
        public static void CalcularYRegenerar()
        {
            // la cuenca visual se mide sobre la escena completa, sin recortar
            if (File.Exists(Ruta)) AssetDatabase.DeleteAsset(Ruta);
            Escena.Generar();
            Calcular();
            AssetDatabase.ImportAsset(Ruta);
            Escena.Generar();
        }

        struct Objetivo { public Vector3 p; public int dueno; }

        public static void Calcular()
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            Physics.SyncTransforms();
            var terrenos = Object.FindObjectsByType<Terrain>();
            Terrain huerto = null;
            foreach (var t in terrenos) if (t.name == "Terreno_Huerto") huerto = t;
            var sol = RenderSettings.sun.transform.forward;

            // ---- Puntos de camara ----
            var camaras = new List<Vector3>();
            for (float x = 80f; x < 125f; x += PasoPuntos)
                for (float z = 60f; z < 132f; z += PasoPuntos)
                {
                    var p = new Vector2(x, z);
                    bool dentro = Util.DentroPoligono(p, Datos.Parcela);
                    if (!dentro && Util.DistanciaPolilinea(p, Datos.Parcela, true) > MargenFuera) continue;
                    float y = huerto.SampleHeight(new Vector3(x, 0, z)) + huerto.transform.position.y;
                    foreach (var a in AlturasCamara) camaras.Add(new Vector3(x, y + a, z));
                }

            // ---- Objetivos: cada uno pertenece a un "dueno" (casa, arbol o celda de suelo) ----
            var objetivos = new List<Objetivo>();
            var duenos = new List<string>();
            int Nuevo(string id) { duenos.Add(id); return duenos.Count - 1; }
            // un punto y la sombra que proyecta en el suelo
            void Punto(Vector3 p, int d, bool sombra)
            {
                objetivos.Add(new Objetivo { p = p, dueno = d });
                if (sombra && Physics.Raycast(p + sol * 0.5f, sol, out var hs, 400f))
                    objetivos.Add(new Objetivo { p = hs.point - sol * 0.15f, dueno = d });
            }

            // Casas: tejado (medido con un rayo hacia abajo sobre la malla ya construida) y fachadas
            var edificios = Datos.Leer("Edificios_Catastro.txt");
            foreach (var e in edificios)
            {
                if (e.pts.Length < 3) continue;
                int d = Nuevo("E;" + e.id);
                Vector2 c = Vector2.zero; foreach (var q in e.pts) c += q; c /= e.pts.Length;
                float suelo = Terreno.AlturaPueblo(c.x, c.y);
                if (Physics.Raycast(new Vector3(c.x, 2000f, c.y), Vector3.down, out var hc, 5000f)) Punto(hc.point + Vector3.up * 0.3f, d, true);
                int n = e.pts.Length;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = e.pts[i], b = e.pts[(i + 1) % n];
                    Vector2 m = (a + b) * 0.5f;
                    foreach (var q in new[] { a, m })
                    {
                        Vector2 haciaC = (c - q).normalized;
                        Vector2 dentro = q + haciaC * 0.4f, fuera = q - haciaC * 0.7f;
                        if (!Physics.Raycast(new Vector3(dentro.x, 2000f, dentro.y), Vector3.down, out var ht, 5000f)) continue;
                        Punto(ht.point + Vector3.up * 0.3f, d, true);
                        float yf = Terreno.AlturaPueblo(fuera.x, fuera.y);
                        Punto(new Vector3(fuera.x, yf + 1.8f, fuera.y), d, false);
                        if (ht.point.y - yf > 4.5f) Punto(new Vector3(fuera.x, ht.point.y - 1.2f, fuera.y), d, false);
                    }
                }
            }

            // Arboles de los terrenos: copa (arriba y en medio) y su sombra
            foreach (var t in terrenos)
            {
                var td = t.terrainData; var o = t.transform.position; var tam = td.size;
                var altos = new float[td.treePrototypes.Length];
                for (int i = 0; i < altos.Length; i++) altos[i] = td.treePrototypes[i].prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.max.y;
                foreach (var ti in td.treeInstances)
                {
                    float x = o.x + ti.position.x * tam.x, z = o.z + ti.position.z * tam.z;
                    float y = t.SampleHeight(new Vector3(x, 0, z)) + o.y;
                    float h = altos[ti.prototypeIndex] * ti.heightScale;
                    int d = Nuevo("A;" + Clave(t, ti));
                    Punto(new Vector3(x, y + h * 0.95f, z), d, true);
                    Punto(new Vector3(x, y + h * 0.6f, z), d, false);
                }
            }

            // Suelo del pueblo cada 4 m
            const float PasoSuelo = 4f;
            int celdas = Mathf.RoundToInt(Terreno.PuebloTam / PasoSuelo);
            int primeraCelda = duenos.Count;
            for (int iz = 0; iz < celdas; iz++)
                for (int ix = 0; ix < celdas; ix++)
                {
                    float x = Terreno.PuebloOrigen.x + (ix + 0.5f) * PasoSuelo, z = Terreno.PuebloOrigen.y + (iz + 0.5f) * PasoSuelo;
                    int d = Nuevo(null);
                    objetivos.Add(new Objetivo { p = new Vector3(x, Terreno.AlturaPueblo(x, z) + 0.4f, z), dueno = d });
                }

            // ---- Rayos (en lotes, con RaycastCommand en todos los nucleos) ----
            var visto = new bool[duenos.Count];
            var pendientes = new List<int>(); for (int i = 0; i < objetivos.Count; i++) pendientes.Add(i);
            var qp = new QueryParameters(Physics.DefaultRaycastLayers, false, QueryTriggerInteraction.Ignore, true);
            long rayos = 0;
            foreach (var cam in camaras)
            {
                pendientes.RemoveAll(i => visto[objetivos[i].dueno]);
                if (pendientes.Count == 0) break;
                int n = pendientes.Count;
                var cmds = new NativeArray<RaycastCommand>(n, Allocator.TempJob);
                var res = new NativeArray<RaycastHit>(n, Allocator.TempJob);
                for (int k = 0; k < n; k++)
                {
                    Vector3 dv = objetivos[pendientes[k]].p - cam;
                    float dist = dv.magnitude;
                    cmds[k] = new RaycastCommand(cam, dv / dist, qp, Mathf.Max(0.01f, dist - 0.3f));
                }
                RaycastCommand.ScheduleBatch(cmds, res, 512, 1).Complete();
                for (int k = 0; k < n; k++)
                    if (res[k].normal == Vector3.zero) visto[objetivos[pendientes[k]].dueno] = true;
                rayos += n;
                cmds.Dispose(); res.Dispose();
            }

            // ---- Guardar ----
            var sb = new StringBuilder();
            sb.AppendLine($"# Cuenca visual desde el huerto ({camaras.Count} puntos de camara, {rayos} rayos). Generado por Huerto/Calcular cuenca visual.");
            int nE = 0, nA = 0, tE = 0, tA = 0;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue; int nS = 0;
            for (int i = 0; i < duenos.Count; i++)
            {
                if (i >= primeraCelda)
                {
                    if (!visto[i]) continue;
                    int ix = (i - primeraCelda) % celdas, iz = (i - primeraCelda) / celdas;
                    float x = Terreno.PuebloOrigen.x + ix * PasoSuelo, z = Terreno.PuebloOrigen.y + iz * PasoSuelo;
                    x0 = Mathf.Min(x0, x); z0 = Mathf.Min(z0, z); x1 = Mathf.Max(x1, x + PasoSuelo); z1 = Mathf.Max(z1, z + PasoSuelo); nS++;
                    continue;
                }
                bool esE = duenos[i][0] == 'E';
                if (esE) tE++; else tA++;
                if (!visto[i]) continue;
                if (esE) nE++; else nA++;
                sb.AppendLine(duenos[i]);
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "R;{0};{1};{2};{3}", x0, z0, x1, z1));
            File.WriteAllText(Ruta, sb.ToString());
            Debug.Log($"Huerto: cuenca visual calculada en {reloj.Elapsed.TotalSeconds:0.0} s: {camaras.Count} puntos de camara, {rayos / 1e6:0.0} M rayos; " +
                      $"se ven {nE}/{tE} edificios, {nA}/{tA} arboles y {nS}/{celdas * celdas} celdas de suelo del pueblo (x {x0}..{x1}, z {z0}..{z1})");
        }
    }
}
