using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Huerto
{
    /// <summary>Terreno del huerto (relieve real + bancales) y entorno lejano de la sierra.</summary>
    public static class Terreno
    {
        const int Res = 1025;          // 192 m / 1024 = 0,1875 m entre muestras
        const int ResEntorno = 513;
        const float TamEntorno = 10000f;
        const float AltoTerreno = 50f;

        static Material MaterialTerreno()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            return rp != null ? rp.defaultTerrainMaterial : null;
        }

        static TerrainLayer Capa(string nombre, string tex, string nor, float tam, Vector4 tinte, float suave = 0f)
        {
            Util.Carpeta($"{Util.Raiz}/TerrainLayers");
            string ruta = $"{Util.Raiz}/TerrainLayers/{nombre}.terrainlayer";
            var capa = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ruta);
            if (capa == null) { capa = new TerrainLayer(); AssetDatabase.CreateAsset(capa, ruta); }
            capa.diffuseTexture = Util.Tex(tex);
            capa.normalMapTexture = nor != null ? Util.Tex(nor, normal: true) : null;
            capa.tileSize = new Vector2(tam, tam);
            capa.diffuseRemapMax = tinte;
            capa.smoothness = suave;
            EditorUtility.SetDirty(capa);
            return capa;
        }

        static TerrainData NuevoData(string nombre, int res, Vector3 tam)
        {
            Util.Carpeta($"{Util.Raiz}/Terreno");
            string ruta = $"{Util.Raiz}/Terreno/{nombre}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ruta);
            if (data == null) { data = new TerrainData(); AssetDatabase.CreateAsset(data, ruta); }
            data.heightmapResolution = res;
            data.size = tam;
            return data;
        }

        // ---------------- Sierra (MDT25 del IGN, 10 x 10 km) ----------------
        public static Terrain CrearEntorno(Transform padre)
        {
            var alt = Datos.LeerAlturas("Alturas_MDT25_Entorno.bytes");
            float min = float.MaxValue, max = float.MinValue;
            foreach (var a in alt) { min = Mathf.Min(min, a); max = Mathf.Max(max, a); }
            min = Mathf.Floor(min) - 5f; max = Mathf.Ceil(max) + 5f;
            var data = NuevoData("Entorno_Sierra", ResEntorno, new Vector3(TamEntorno, max - min, TamEntorno));
            var h = new float[ResEntorno, ResEntorno];
            float paso = TamEntorno / (ResEntorno - 1);
            float ox = 96f - TamEntorno / 2, oz = 96f - TamEntorno / 2;
            for (int z = 0; z < ResEntorno; z++)
                for (int x = 0; x < ResEntorno; x++)
                {
                    float a = alt[z * ResEntorno + x];
                    // Bajo el terreno del pueblo se hunde 3 m para que no asome a traves del terreno detallado
                    float cheb = Mathf.Max(Mathf.Abs(ox + x * paso - PuebloCentro.x), Mathf.Abs(oz + z * paso - PuebloCentro.y));
                    a -= 3f * (1f - Util.Smooth(PuebloTam / 2 - 45f, PuebloTam / 2 - 14f, cheb));
                    h[z, x] = (a - min) / (max - min);
                }
            data.SetHeights(0, 0, h);
            data.alphamapResolution = 512;
            data.baseMapResolution = 512;
            data.terrainLayers = new[]
            {
                Capa("Lejos_Bosque", "sparse_grass_diff_2k.jpg", null, 60f, new Vector4(0.26f, 0.34f, 0.2f, 1f)),
                Capa("Lejos_Prado", "withered_grass_diff_2k.jpg", null, 40f, new Vector4(0.72f, 0.7f, 0.52f, 1f)),
                Capa("Lejos_Roca", "mossy_rock_diff_2k.jpg", null, 50f, new Vector4(0.9f, 0.9f, 0.88f, 1f)),
            };
            int ar = data.alphamapResolution;
            var mapa = new float[ar, ar, 3];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float nx = (x + 0.5f) / ar, nz = (z + 0.5f) / ar;
                    float pend = data.GetSteepness(nx, nz);
                    float altura = data.GetInterpolatedHeight(nx, nz) + min;
                    float wx = nx * TamEntorno, wz = nz * TamEntorno;
                    float ruido = Mathf.PerlinNoise(wx * 0.0021f + 7.1f, wz * 0.0021f + 3.3f);
                    float roca = Util.Smooth(26f, 40f, pend + (ruido - 0.5f) * 10f) + Util.Smooth(1450f, 1650f, altura) * 0.8f;
                    float distPueblo = new Vector2(wx - TamEntorno / 2, wz - TamEntorno / 2).magnitude;
                    float prado = Util.Smooth(0.62f, 0.8f, ruido) * 0.7f + (1f - Util.Smooth(150f, 500f, distPueblo)) * 0.5f;
                    roca = Mathf.Clamp01(roca);
                    prado = Mathf.Clamp01(prado) * (1 - roca);
                    mapa[z, x, 0] = Mathf.Max(0, 1 - roca - prado);
                    mapa[z, x, 1] = prado;
                    mapa[z, x, 2] = roca;
                }
            data.SetAlphamaps(0, 0, mapa);
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Sierra_MDT25";
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(ox, min - Datos.Base, oz);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = MaterialTerreno();
            t.heightmapPixelError = 3;
            t.basemapDistance = 20000;
            t.drawInstanced = true;
            return t;
        }

        // ---------------- Pueblo (LiDAR PNOA 2025 a 1 m, 1024 x 1024 m) ----------------
        public static readonly Vector2 PuebloOrigen = new Vector2(-450f, -300f);
        public const float PuebloTam = 1024f;
        public static Vector2 PuebloCentro => PuebloOrigen + Vector2.one * PuebloTam / 2;
        const int ResPueblo = 1025;
        static float[] altPueblo;

        /// <summary>Altura local (y) del suelo LiDAR del pueblo, interpolada.</summary>
        public static float AlturaPueblo(float wx, float wz)
        {
            if (altPueblo == null) altPueblo = Datos.LeerAlturas("Alturas_Pueblo_LiDAR.bytes");
            float fx = Mathf.Clamp(wx - PuebloOrigen.x, 0, ResPueblo - 1.001f), fz = Mathf.Clamp(wz - PuebloOrigen.y, 0, ResPueblo - 1.001f);
            int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
            var d = altPueblo;
            return Mathf.Lerp(Mathf.Lerp(d[z0 * ResPueblo + x0], d[z0 * ResPueblo + x0 + 1], tx), Mathf.Lerp(d[(z0 + 1) * ResPueblo + x0], d[(z0 + 1) * ResPueblo + x0 + 1], tx), tz) - Datos.Base;
        }

        public static Terrain CrearPueblo(Transform padre, Terrain sierra)
        {
            altPueblo = Datos.LeerAlturas("Alturas_Pueblo_LiDAR.bytes");
            float min = float.MaxValue, max = float.MinValue;
            foreach (var a in altPueblo) { min = Mathf.Min(min, a); max = Mathf.Max(max, a); }
            min = Mathf.Floor(min - Datos.Base) - 5f; max = Mathf.Ceil(max - Datos.Base) + 5f;
            var data = NuevoData("Terreno_Pueblo", ResPueblo, new Vector3(PuebloTam, max - min, PuebloTam));
            var h = new float[ResPueblo, ResPueblo];
            for (int z = 0; z < ResPueblo; z++)
                for (int x = 0; x < ResPueblo; x++)
                {
                    float wx = PuebloOrigen.x + x, wz = PuebloOrigen.y + z;
                    float a = altPueblo[z * ResPueblo + x] - Datos.Base;
                    // bajo el huerto se hunde: alli manda el terreno detallado
                    float chebH = Mathf.Max(Mathf.Abs(wx - 96f), Mathf.Abs(wz - 96f));
                    a -= 1.5f * (1f - Util.Smooth(88f, 94f, chebH));
                    // borde: se funde con la sierra
                    float cheb = Mathf.Max(Mathf.Abs(wx - PuebloCentro.x), Mathf.Abs(wz - PuebloCentro.y));
                    float w = Util.Smooth(PuebloTam / 2 - 40f, PuebloTam / 2 - 0.5f, cheb);
                    if (w > 0f && sierra != null)
                    {
                        // altura de la sierra sin hundir (el hundimiento ya es 0 en el borde)
                        float hs = sierra.SampleHeight(new Vector3(wx, 0, wz)) + sierra.transform.position.y + 3f * (1f - Util.Smooth(PuebloTam / 2 - 45f, PuebloTam / 2 - 14f, cheb)) + 0.12f;
                        a = Mathf.Lerp(a, hs, w);
                    }
                    h[z, x] = (a - min) / (max - min);
                }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terreno_Pueblo_LiDAR";
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(PuebloOrigen.x, min, PuebloOrigen.y);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = MaterialTerreno();
            t.heightmapPixelError = 2;
            t.basemapDistance = 400;
            t.drawInstanced = true;
            return t;
        }

        /// <summary>Pinta el pueblo con el mapa de superficies del LiDAR: empedrado en las calles, prado, tierra y hojarasca.</summary>
        public static void PintarPueblo(Terrain t)
        {
            var data = t.terrainData;
            data.alphamapResolution = 1024;
            data.baseMapResolution = 1024;
            data.terrainLayers = new[]
            {
                Capa("Hierba_Seca", "withered_grass_diff_2k.jpg", "withered_grass_nor_gl_2k.jpg", 3f, new Vector4(0.95f, 0.92f, 0.85f, 1f)),
                Capa("Tierra", "dry_ground_rocks_diff_2k.jpg", "dry_ground_rocks_nor_gl_2k.jpg", 3.5f, new Vector4(0.86f, 0.86f, 0.84f, 1f)),
                Capa("Hojarasca", "dry_decay_leaves_diff_2k.jpg", "dry_decay_leaves_nor_gl_2k.jpg", 2.5f, new Vector4(0.82f, 0.8f, 0.7f, 1f)),
                Capa("Hierba_Verde", "sparse_grass_diff_2k.jpg", "sparse_grass_nor_gl_2k.jpg", 2.5f, new Vector4(1f, 1f, 1f, 1f)),
                Capa("Empedrado", "Casas/cobblestone_floor_01_diff_2k.jpg", "Casas/cobblestone_floor_01_nor_gl_2k.jpg", 2.2f, new Vector4(0.95f, 0.93f, 0.9f, 1f)),
            };
            var cls = File.ReadAllBytes($"{Util.Raiz}/Datos/Superficies_Pueblo.bytes");
            const int N = 1024;
            // clase -> capa: 0 seca, 1 verde, 2 tierra, 3 hojarasca, 4 empedrado, 5 bajo edificio (tierra)
            int[] capa = { 0, 3, 1, 2, 4, 1 };
            var mapa = new float[N, N, 5];
            for (int z = 0; z < N; z++)
                for (int x = 0; x < N; x++)
                {
                    // suavizado 3x3 para transiciones blandas
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int zz = Mathf.Clamp(z + dz, 0, N - 1), xx = Mathf.Clamp(x + dx, 0, N - 1);
                            mapa[z, x, capa[cls[zz * N + xx]]] += dz == 0 && dx == 0 ? 0.36f : 0.08f;
                        }
                }
            data.SetAlphamaps(0, 0, mapa);
            EditorUtility.SetDirty(data);
        }

        // ---------------- Huerto (MDT05 + bancales) ----------------
        public static Terrain CrearHuerto(Transform padre, Terrain entorno, List<Datos.Poligono> parcelas)
        {
            // Relieve real del LiDAR PNOA 2025 (suelo clasificado, rejilla de 0,1875 m)
            var dem = Datos.LeerAlturas("Alturas_LiDAR_2025.bytes");
            var data = NuevoData("Terreno_Huerto", Res, new Vector3(Datos.Tam, AltoTerreno, Datos.Tam));
            float paso = Datos.Tam / (Res - 1);
            System.Func<float, float, float> Lidar = (wx, wz) =>
            {
                float fx = Mathf.Clamp(wx / paso, 0, Res - 1.001f), fz = Mathf.Clamp(wz / paso, 0, Res - 1.001f);
                int x0 = (int)fx, z0 = (int)fz; float tx = fx - x0, tz = fz - z0;
                return Mathf.Lerp(Mathf.Lerp(dem[z0 * Res + x0], dem[z0 * Res + x0 + 1], tx), Mathf.Lerp(dem[(z0 + 1) * Res + x0], dem[(z0 + 1) * Res + x0 + 1], tx), tz);
            };
            var h = new float[Res, Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                {
                    float wx = x * paso, wz = z * paso;
                    float a = dem[z * Res + x];
                    // Bajo los muros de bancal el LiDAR da un talud de ~1 m: se convierte en un escalon neto
                    if (wx > 84f && wx < 118f && wz > 66f && wz < 104f)
                        foreach (var m in Datos.Muros)
                        {
                            if (!m.bancal) continue;
                            var p = new Vector2(wx, wz);
                            for (int i = 0; i < m.puntos.Length - 1; i++)
                            {
                                Vector2 A = m.puntos[i], B = m.puntos[i + 1], ab = B - A;
                                float u = Mathf.Clamp01(Vector2.Dot(p - A, ab) / ab.sqrMagnitude);
                                Vector2 q = A + ab * u; float dist = Vector2.Distance(p, q);
                                if (dist > 1.1f) continue;
                                Vector2 nrm = new Vector2(-ab.y, ab.x).normalized;
                                float lado = Mathf.Sign(Vector2.Dot(p - q, nrm));
                                Vector2 s = q + nrm * lado * 1.3f;
                                a = Lidar(s.x, s.y);
                            }
                        }
                    // Borde: se funde con el terreno del pueblo para que no haya escalon
                    float cheb = Mathf.Max(Mathf.Abs(wx - 96f), Mathf.Abs(wz - 96f));
                    float w = Util.Smooth(80f, 95.9f, cheb);
                    if (w > 0f) a = Mathf.Lerp(a, AlturaPueblo(wx, wz) + Datos.Base + 0.08f, w);
                    h[z, x] = (a - Datos.Base) / AltoTerreno;
                }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terreno_Huerto";
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = MaterialTerreno();
            t.heightmapPixelError = 2;
            t.basemapDistance = 300;
            t.drawInstanced = true;
            t.detailObjectDistance = 70;
            t.detailObjectDensity = 1f;
            return t;
        }

        /// <summary>Pinta hierba seca, tierra de caminos, hojarasca bajo los arboles y hierba verde en la sombra.</summary>
        public static void Pintar(Terrain t, List<Datos.Poligono> parcelas)
        {
            var data = t.terrainData;
            data.alphamapResolution = 1024;
            data.baseMapResolution = 1024;
            data.terrainLayers = new[]
            {
                Capa("Hierba_Seca", "withered_grass_diff_2k.jpg", "withered_grass_nor_gl_2k.jpg", 3f, new Vector4(0.95f, 0.92f, 0.85f, 1f)),
                Capa("Tierra", "dry_ground_rocks_diff_2k.jpg", "dry_ground_rocks_nor_gl_2k.jpg", 3.5f, new Vector4(0.86f, 0.86f, 0.84f, 1f)),
                Capa("Hojarasca", "dry_decay_leaves_diff_2k.jpg", "dry_decay_leaves_nor_gl_2k.jpg", 2.5f, new Vector4(0.82f, 0.8f, 0.7f, 1f)),
                Capa("Hierba_Verde", "sparse_grass_diff_2k.jpg", "sparse_grass_nor_gl_2k.jpg", 2.5f, new Vector4(1f, 1f, 1f, 1f)),
            };
            // Cajas envolventes de las parcelas para descartar rapido
            var cajas = new List<Rect>();
            foreach (var p in parcelas)
            {
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                foreach (var q in p.pts) { x0 = Mathf.Min(x0, q.x); z0 = Mathf.Min(z0, q.y); x1 = Mathf.Max(x1, q.x); z1 = Mathf.Max(z1, q.y); }
                cajas.Add(Rect.MinMaxRect(x0, z0, x1, z1));
            }
            var arboles = Datos.Arboles.FindAll(a => a.pos.x > -10f && a.pos.x < 202f && a.pos.y > -10f && a.pos.y < 202f);
            var senda = new[] { new Vector2(95.3f, 120.5f), new Vector2(93.2f, 114f), new Vector2(91.6f, 107.5f), new Vector2(92.4f, 102f), new Vector2(94f, 99.8f) };
            int ar = data.alphamapResolution;
            var mapa = new float[ar, ar, 4];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float wx = (x + 0.5f) / ar * Datos.Tam, wz = (z + 0.5f) / ar * Datos.Tam;
                    var p = new Vector2(wx, wz);
                    bool enParcela = false;
                    for (int i = 0; i < parcelas.Count && !enParcela; i++)
                        if (cajas[i].Contains(p) && Util.DentroPoligono(p, parcelas[i].pts)) enParcela = true;
                    float n1 = Mathf.PerlinNoise(wx * 0.13f + 4.2f, wz * 0.13f + 9.1f);
                    float n2 = Mathf.PerlinNoise(wx * 0.6f + 1.7f, wz * 0.6f + 5.3f);
                    float tierra = enParcela ? 0f : 1f;
                    float hojas = 0f;
                    foreach (var a in arboles)
                    {
                        if (Mathf.Abs(p.x - a.pos.x) > 9f || Mathf.Abs(p.y - a.pos.y) > 9f) continue;
                        float r = a.alto * (a.especie == "Joven" ? 0.25f : 0.42f);
                        float d = Vector2.Distance(p, a.pos);
                        hojas = Mathf.Max(hojas, (1f - Util.Smooth(r * 0.55f, r * 1.1f, d + (n2 - 0.5f) * 1.5f)) * (a.especie == "RobleSeco" ? 0.15f : 0.4f));
                    }
                    float verde = Util.Smooth(0.55f, 0.8f, n1) * 0.5f;
                    bool dentro39 = wx > 84f && wx < 118f && wz > 65f && wz < 127f && Util.DentroPoligono(p, Datos.Parcela);
                    if (dentro39)
                    {
                        if (Datos.EnPasillo(wx, wz)) verde += 0.35f;
                        float ds = Util.DistanciaPolilinea(p, senda, false);
                        tierra = Mathf.Max(tierra, (1f - Util.Smooth(0.35f, 0.9f, ds + (n2 - 0.5f) * 0.5f)) * 0.75f);
                    }
                    else if (enParcela) verde *= 0.6f;
                    hojas *= 1f - tierra * 0.7f;
                    verde = Mathf.Clamp01(verde) * (1f - hojas) * (1f - tierra);
                    float seca = Mathf.Max(0f, 1f - tierra - hojas - verde);
                    float suma = seca + tierra + hojas + verde;
                    mapa[z, x, 0] = seca / suma; mapa[z, x, 1] = tierra / suma; mapa[z, x, 2] = hojas / suma; mapa[z, x, 3] = verde / suma;
                }
            data.SetAlphamaps(0, 0, mapa);
            EditorUtility.SetDirty(data);
        }

        /// <summary>Matas de hierba seca: cortas en lo segado, altas junto a muros y fuera de la parcela.</summary>
        public static void Hierba(Terrain t, List<Datos.Poligono> parcelas)
        {
            var data = t.terrainData;
            data.SetDetailResolution(1024, 32);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            var seca = Util.Tex("Hierba_Seca.png", hojas: true, max: 512);
            var verde = Util.Tex("Hierba_Verde.png", hojas: true, max: 512);
            data.detailPrototypes = new[]
            {
                new DetailPrototype { prototypeTexture = seca, renderMode = DetailRenderMode.Grass, minWidth = 0.18f, maxWidth = 0.32f, minHeight = 0.07f, maxHeight = 0.16f, noiseSpread = 0.3f, healthyColor = new Color(0.88f, 0.86f, 0.8f), dryColor = new Color(0.8f, 0.76f, 0.68f), useInstancing = false },
                new DetailPrototype { prototypeTexture = seca, renderMode = DetailRenderMode.Grass, minWidth = 0.3f, maxWidth = 0.5f, minHeight = 0.3f, maxHeight = 0.65f, noiseSpread = 0.4f, healthyColor = new Color(0.86f, 0.84f, 0.78f), dryColor = new Color(0.78f, 0.73f, 0.64f), useInstancing = false },
                new DetailPrototype { prototypeTexture = verde, renderMode = DetailRenderMode.Grass, minWidth = 0.25f, maxWidth = 0.4f, minHeight = 0.15f, maxHeight = 0.4f, noiseSpread = 0.3f, healthyColor = new Color(0.95f, 0.95f, 0.9f), dryColor = new Color(0.9f, 0.88f, 0.75f), useInstancing = false },
            };
            data.wavingGrassTint = new Color(0.95f, 0.95f, 0.92f);
            data.wavingGrassStrength = 0.15f;
            data.wavingGrassAmount = 0.15f;
            int dr = data.detailResolution;
            var corta = new int[dr, dr]; var alta = new int[dr, dr]; var verdeL = new int[dr, dr];
            var rnd = new System.Random(77);
            var mapa = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            int ar = data.alphamapWidth;
            for (int z = 0; z < dr; z++)
                for (int x = 0; x < dr; x++)
                {
                    float wx = (x + 0.5f) / dr * Datos.Tam, wz = (z + 0.5f) / dr * Datos.Tam;
                    int ax = Mathf.Min(ar - 1, (int)((x + 0.5f) / dr * ar)), az = Mathf.Min(ar - 1, (int)((z + 0.5f) / dr * ar));
                    float wSeca = mapa[az, ax, 0], wTierra = mapa[az, ax, 1], wVerde = mapa[az, ax, 3];
                    var p = new Vector2(wx, wz);
                    bool dentro39 = wx > 84f && wx < 118f && wz > 65f && wz < 127f && Util.DentroPoligono(p, Datos.Parcela);
                    float cercaMuro = 99f;
                    if (wx > 82f && wx < 122f && wz > 62f && wz < 126f)
                        foreach (var m in Datos.Muros) cercaMuro = Mathf.Min(cercaMuro, Util.DistanciaPolilinea(p, m.puntos, false));
                    float pc = dentro39 ? 0.05f : 0.18f;
                    float pa = dentro39 ? 0.01f : 0.06f;
                    if (cercaMuro < 1.2f) { pa += 0.25f * (1f - cercaMuro / 1.2f); }
                    double r = rnd.NextDouble();
                    if (r < pc * wSeca) corta[z, x] = 1;
                    if (rnd.NextDouble() < pa * (wSeca + wVerde * 0.5f) * (1f - wTierra)) alta[z, x] = 1;
                    if (rnd.NextDouble() < 0.05f * wVerde) verdeL[z, x] = 1;
                }
            data.SetDetailLayer(0, 0, 0, corta);
            data.SetDetailLayer(0, 0, 1, alta);
            data.SetDetailLayer(0, 0, 2, verdeL);
            EditorUtility.SetDirty(data);
        }
    }
}
