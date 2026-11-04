using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Huerto
{
    /// <summary>Genera la escena Assets/Scenes/Huerto.unity completa. Menu: Huerto > Generar escena completa.</summary>
    public static class Escena
    {
        public const string RutaEscena = "Assets/Escenas/Huerto.unity";

        static Texture2D TexModelo(string modelo, string archivo, bool normal)
        {
            string p = $"{Util.Raiz}/Modelos/Importados/{modelo}/textures/{archivo}";
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti != null && normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        [MenuItem("Huerto/Generar escena completa")]
        public static void Generar()
        {
            try { GenerarInterno(); }
            catch (System.Exception ex) { Debug.LogException(ex); throw; }
        }

        static void GenerarInterno()
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- Materiales ----------
            var texRoca = Util.Tex("rock_boulder_dry_diff_2k.jpg"); var norRoca = Util.Tex("rock_boulder_dry_nor_gl_2k.jpg", normal: true);
            var piedraMuro = Util.Mat("Granito_Muro", texRoca, norRoca, new Color(0.74f, 0.73f, 0.7f), 0.12f, repeticion: 0.6f);
            var piedras = new[]
            {
                piedraMuro,
                Util.Mat("Granito_Muro_Ocre", texRoca, norRoca, new Color(0.72f, 0.66f, 0.58f), 0.12f, repeticion: 0.6f),
                Util.Mat("Granito_Muro_Liquen", Util.Tex("mossy_rock_diff_2k.jpg"), Util.Tex("mossy_rock_nor_gl_2k.jpg", normal: true), new Color(1.0f, 1.0f, 0.95f), 0.1f, repeticion: 0.6f),
                Util.Mat("Granito_Muro_Oscuro", texRoca, norRoca, new Color(0.56f, 0.55f, 0.53f), 0.12f, repeticion: 0.6f),
            };
            var hiedra = Util.Mat("Hiedra", Util.Tex("Hojas_Hiedra.png", hojas: true, max: 1024), null, Color.white, 0.35f, recorte: true, dobleCara: true);
            var fondoHiedra = Util.Mat("Hiedra_Fondo", null, null, new Color(0.1f, 0.18f, 0.07f), 0.2f);
            var cortezas = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["Nogal"] = Util.Mat("Corteza_Nogal", Util.Tex("jolcham_oak_bark_01_diff_1k.jpg", max: 1024), Util.Tex("jolcham_oak_bark_01_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.85f, 0.84f, 0.82f), 0.1f),
                ["Castano"] = Util.Mat("Corteza_Castano", Util.Tex("bark_brown_02_diff_1k.jpg", max: 1024), Util.Tex("bark_brown_02_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.8f, 0.74f, 0.68f), 0.1f),
                ["Cerezo"] = Util.Mat("Corteza_Cerezo", Util.Tex("tree_bark_03_diff_1k.jpg", max: 1024), Util.Tex("tree_bark_03_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.9f, 0.9f, 0.86f), 0.1f),
                ["Roble"] = Util.Mat("Corteza_Roble", Util.Tex("bark_brown_02_diff_1k.jpg", max: 1024), Util.Tex("bark_brown_02_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.62f, 0.6f, 0.56f), 0.08f),
                ["Seco"] = Util.Mat("Corteza_Seca", Util.Tex("jolcham_oak_bark_01_diff_1k.jpg", max: 1024), Util.Tex("jolcham_oak_bark_01_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.78f, 0.77f, 0.75f), 0.08f),
            };
            var hojas = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var h in new[] { "Nogal", "Castano", "Cerezo", "Roble", "Arbusto" })
                hojas[h] = Util.Mat("Hojas_" + h, Util.Tex($"Hojas_{h}.png", hojas: true, max: 1024), null, new Color(0.88f, 0.9f, 0.84f), 0.25f, recorte: true, dobleCara: true);
            var blanco = Util.Mat("Deposito_Blanco", null, null, new Color(0.9f, 0.9f, 0.86f), 0.45f);
            var metal = Util.Mat("Metal_Galvanizado", null, null, new Color(0.62f, 0.64f, 0.66f), 0.55f, metal: 0.85f);
            var palet = Util.Mat("Plastico_Negro", null, null, new Color(0.07f, 0.07f, 0.07f), 0.3f);
            var tela = Util.Mat("Malla_Verde", null, null, new Color(0.12f, 0.25f, 0.16f), 0.05f, dobleCara: true);
            var poste = Util.Mat("Poste_Verde", null, null, new Color(0.14f, 0.28f, 0.17f), 0.4f, metal: 0.5f);
            var madera = Util.Mat("Madera_Tablones", Util.Tex("weathered_planks_diff_1k.jpg", max: 1024), Util.Tex("weathered_planks_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.9f, 0.86f, 0.8f), 0.15f);
            var muroCasa = Util.Mat("Piedra_Casa", Util.Tex("old_stone_wall_diff_2k.jpg"), Util.Tex("old_stone_wall_nor_gl_2k.jpg", normal: true), new Color(0.82f, 0.8f, 0.76f), 0.1f, repeticion: 0.45f);
            var teja = Util.Mat("Teja", Util.Tex("clay_roof_tiles_diff_2k.jpg"), Util.Tex("clay_roof_tiles_nor_gl_2k.jpg", normal: true), new Color(0.88f, 0.78f, 0.72f), 0.15f, repeticion: 0.45f);
            var puerta = Util.Mat("Puerta_Verde", Util.Tex("green_rough_planks_diff_1k.jpg", max: 1024), Util.Tex("green_rough_planks_nor_gl_1k.jpg", normal: true, max: 1024), Color.white, 0.2f);
            var plumeroHojas = Util.Mat("Plumero_Hojas", null, null, new Color(0.5f, 0.55f, 0.34f), 0.3f, dobleCara: true);
            var plumeroEspiga = Util.Mat("Plumero_Espiga", Util.Tex("Plumero_Espiga.png", hojas: true, max: 1024), null, Color.white, 0.1f, recorte: true, dobleCara: true);
            string[] rocas = { "namaqualand_boulder_02", "namaqualand_boulder_04", "namaqualand_boulder_05" };
            var matRocas = new Material[rocas.Length];
            for (int i = 0; i < rocas.Length; i++)
                matRocas[i] = Util.Mat("Roca_" + i, TexModelo(rocas[i], rocas[i] + "_diff_1k.jpg", false), TexModelo(rocas[i], rocas[i] + "_nor_gl_1k.exr", true), new Color(0.88f, 0.86f, 0.82f), 0.12f);
            var matRamas = Util.Mat("Ramas_Secas", TexModelo("dry_branches_medium_01", "dry_branches_medium_01_diff_1k.jpg", false), TexModelo("dry_branches_medium_01", "dry_branches_medium_01_nor_gl_1k.exr", true), new Color(0.85f, 0.75f, 0.65f), 0.1f);

            // ---------- Jerarquia ----------
            var entorno = new GameObject("Entorno").transform;
            var huertoGo = new GameObject("Huerto_Parcela39").transform;
            var murosGo = new GameObject("Muros_Piedra_Seca").transform; murosGo.SetParent(huertoGo);
            var arbolesGo = new GameObject("Arboles").transform;
            var objetosGo = new GameObject("Objetos").transform; objetosGo.SetParent(huertoGo);
            var puebloGo = new GameObject("Pueblo_La_Alberca").transform;

            // ---------- Terreno ----------
            var parcelas = Datos.Leer("Parcelas_Catastro.txt");
            var edificios = Datos.Leer("Edificios_Catastro.txt");
            var sierra = Terreno.CrearEntorno(entorno);
            var pueblo = Terreno.CrearPueblo(entorno, sierra);
            Terreno.PintarPueblo(pueblo);
            var terreno = Terreno.CrearHuerto(entorno, sierra, parcelas);
            Terreno.Pintar(terreno, parcelas);
            Terreno.Hierba(terreno, parcelas);
            Cultivos.PrepararSuelo(terreno);

            // ---------- Muros, hiedra y objetos ----------
            foreach (var m in Datos.Muros) Muros.Crear(m, terreno, murosGo, piedras, hiedra);
            Muros.Cordon(new[] { new Vector2(95.1f, 116.4f), new Vector2(93.9f, 114.6f), new Vector2(92.3f, 112.6f), new Vector2(90.7f, 110.9f) }, 1.0f, terreno, objetosGo, fondoHiedra, hiedra, "Cordon_Hiedra");
            Objetos.Deposito(terreno, objetosGo, new Vector2(104.6f, 70.0f), 8f, blanco, metal, palet);
            Objetos.Malla(terreno, objetosGo, new[] { new Vector2(93.3f, 70.0f), new Vector2(103.53f, 68.8f), new Vector2(113.4f, 67.2f) }, tela, poste);
            Objetos.Pilar(terreno, objetosGo, new Vector2(93.2f, 70.4f), 2.3f, piedraMuro, "Pilar_Piedra_1");
            Objetos.Pilar(terreno, objetosGo, new Vector2(103.6f, 69.1f), 2.1f, piedraMuro, "Pilar_Piedra_2");
            Objetos.Tablones(terreno, objetosGo, madera);
            Objetos.Caseta(terreno, objetosGo, new Vector2(97.6f, 101.0f), 180f, muroCasa, teja, puerta);
            Objetos.PuertaVerde(terreno, objetosGo, new Vector2(87.7f, 111.0f), 80f, puerta);
            Objetos.Plumero(terreno, objetosGo, new Vector2(92.4f, 108.2f), plumeroHojas, plumeroEspiga);
            Objetos.Rocas(terreno, objetosGo, matRocas);
            Objetos.Ramas(terreno, objetosGo, matRamas);
            Terceros.Atrezo(terreno, objetosGo);
            // rocas y pilares solidos para el personaje
            foreach (Transform o in objetosGo)
                if (o.name.StartsWith("Roca_") || o.name.StartsWith("Pilar_"))
                    foreach (var mf in o.GetComponentsInChildren<MeshFilter>())
                        if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

            // ---------- Arboles ----------
            // Arboles reales del LiDAR: unicos (malla propia) cerca del huerto, instanciados en el resto
            foreach (var guid in AssetDatabase.FindAssets("Arbol_ t:Mesh", new[] { $"{Util.Raiz}/Modelos/Generados" })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            Datos.Recargar();
            var resto = new System.Collections.Generic.List<Datos.Arbol>();
            int nUnicos = 0;
            foreach (var a in Datos.Arboles)
            {
                bool cerca = Util.DentroPoligono(a.pos, Datos.Parcela) || Util.DistanciaPolilinea(a.pos, Datos.Parcela, true) < 22f;
                if (!cerca) { resto.Add(a); continue; }
                var esp = Arboles.Get(a.especie);
                string nombre = $"Arbol_{nUnicos++:000}_{a.especie}";
                // castanos, robles y nogales: modelos realistas de la Asset Store con la altura y la copa del LiDAR
                var realista = Terceros.PrefabArbol(a.especie, a.semilla, a.alto);
                if (realista != null)
                {
                    var pos = new Vector3(a.pos.x, terreno.SampleHeight(new Vector3(a.pos.x, 0, a.pos.y)) - 0.08f, a.pos.y);
                    var arbol = Terceros.Arbol(realista, nombre, arbolesGo, pos, (a.semilla * 47) % 360, a.alto, a.copa);
                    var fuste = arbol.AddComponent<CapsuleCollider>();
                    fuste.radius = Mathf.Max(0.08f, a.alto * 0.022f) / arbol.transform.localScale.x;
                    fuste.height = 2.5f / arbol.transform.localScale.y; fuste.center = new Vector3(0, fuste.height * 0.5f, 0);
                    continue;
                }
                var malla = Util.Guardar(Arboles.Generar(esp, a.alto, a.semilla, nombre), nombre);
                var mats = esp.hojas != null ? new[] { cortezas[esp.corteza], hojas[esp.hojas] } : new[] { cortezas[esp.corteza] };
                var go = Util.Objeto(nombre, arbolesGo, malla, mats);
                go.transform.position = new Vector3(a.pos.x, terreno.SampleHeight(new Vector3(a.pos.x, 0, a.pos.y)) - 0.08f, a.pos.y);
                go.transform.rotation = Quaternion.Euler(0, (a.semilla * 47) % 360, 0);
                // tronco solido para el personaje
                var tronco = go.AddComponent<CapsuleCollider>();
                float rTronco = Mathf.Max(0.07f, a.alto * esp.radio * (esp.tallos > 1 ? 0.7f : 1f));
                tronco.radius = rTronco; tronco.height = Mathf.Max(rTronco * 2f, a.alto * esp.fuste + rTronco); tronco.center = new Vector3(0, tronco.height * 0.5f, 0);
            }

            // Cuenca visual: lo que no se ve desde el huerto no se genera (Huerto > Calcular cuenca visual y regenerar)
            var vis = Visibilidad.Leer();
            Bosque.Poblar(terreno, pueblo, sierra, parcelas, edificios, cortezas, hojas, resto, vis);

            // ---------- Pueblo ----------
            System.Func<Vector2, float> suelo = p =>
                p.x > 0.5f && p.x < Datos.Tam - 0.5f && p.y > 0.5f && p.y < Datos.Tam - 0.5f
                    ? terreno.SampleHeight(new Vector3(p.x, 0, p.y)) + terreno.transform.position.y
                    : Terreno.AlturaPueblo(p.x, p.y);
            Casas.Crear(edificios, suelo, puebloGo, Casas.Materiales(), vis != null ? (System.Func<string, bool>)(id => vis.edificios.Contains(id)) : null);

            // ---------- Luz y cielo: 2 de agosto, 10:56, La Alberca (40,49 N) ----------
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.transform.rotation = Quaternion.Euler(39f, 100f + 180f, 0f);
            sol.color = new Color(1f, 0.96f, 0.9f);
            sol.intensity = 2.6f;
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.85f;
            RenderSettings.sun = sol;
            // Cielo despejado de verano: HDRI real (Poly Haven, CC0) girado para que su sol coincida con el sol de las fotos
            const string rutaHdri = "Assets/Huerto/Texturas/Cielo/kloofendal_43d_clear_puresky_4k.hdr";
            var tiHdri = AssetImporter.GetAtPath(rutaHdri) as TextureImporter;
            if (tiHdri != null && (tiHdri.maxTextureSize != 4096 || tiHdri.wrapModeV != TextureWrapMode.Clamp))
            {
                tiHdri.maxTextureSize = 4096; tiHdri.wrapModeU = TextureWrapMode.Repeat; tiHdri.wrapModeV = TextureWrapMode.Clamp;
                tiHdri.textureCompression = TextureImporterCompression.CompressedHQ; tiHdri.SaveAndReimport();
            }
            string rutaCielo = $"{Util.Raiz}/Materiales/Cielo_HDRI.mat";
            var cielo = AssetDatabase.LoadAssetAtPath<Material>(rutaCielo);
            if (cielo == null) { cielo = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(cielo, rutaCielo); }
            cielo.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaHdri));
            cielo.SetFloat("_Mapping", 1f); cielo.SetFloat("_ImageType", 0f);
            cielo.SetFloat("_Exposure", Datos.CieloExposicion);
            cielo.SetFloat("_Rotation", Datos.CieloGiro);
            cielo.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(cielo);
            RenderSettings.skybox = cielo;
            // luz ambiente en degradado: cielo azul arriba y rebote calido del suelo seco (el HDRI solo da el fondo y los reflejos)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f) * 1.25f;
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.64f, 0.58f) * 1.25f;
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.42f, 0.32f) * 1.25f;
            RenderSettings.reflectionIntensity = 0.5f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.00028f;
            RenderSettings.fogColor = new Color(0.7f, 0.78f, 0.88f);
            DynamicGI.UpdateEnvironment();

            // ---------- Postproceso ----------
            string rutaPerfil = $"{Util.Raiz}/Materiales/PostProceso_Huerto.asset";
            AssetDatabase.DeleteAsset(rutaPerfil);
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, rutaPerfil);
            var tm = perfil.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var ca = perfil.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0f); ca.contrast.Override(12f); ca.saturation.Override(10f);
            var bl = perfil.Add<Bloom>(true);
            bl.intensity.Override(0.35f); bl.threshold.Override(1.1f);
            var vg = perfil.Add<Vignette>(true);
            vg.intensity.Override(0.15f);
            // los componentes tienen que guardarse dentro del asset del perfil
            foreach (var c in perfil.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, perfil); }
            EditorUtility.SetDirty(perfil);
            var vol = new GameObject("Postproceso").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = perfil;

            // ---------- Vistas de las fotos y camara ----------
            var vistas = new GameObject("Vistas_Fotos").transform;
            foreach (var v in Datos.Vistas)
            {
                var go = new GameObject("Foto_" + v.foto).transform;
                go.SetParent(vistas);
                go.position = new Vector3(v.pos.x, terreno.SampleHeight(new Vector3(v.pos.x, 0, v.pos.y)) + v.alturaOjos, v.pos.y);
                go.rotation = Quaternion.Euler(v.inclinacion, v.rumbo, 0);
            }
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.08f; cam.farClipPlane = 9000f;
            camGo.AddComponent<AudioListener>();
            var datosCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            datosCam.renderPostProcessing = true;
            datosCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var primera = vistas.GetChild(0);
            camGo.transform.SetPositionAndRotation(primera.position, primera.rotation);

            // ---------- Personaje jugable (empieza en la entrada desde la casa, donde se hizo la primera foto) ----------
            var jugador = Personaje.Crear(terreno, Datos.Vistas[0].pos, Datos.Vistas[0].rumbo).GetComponent<Juego.Paseante>();
            Personaje.Limites(terreno, huertoGo);
            jugador.camara = camGo.transform;
            camGo.AddComponent<Juego.CamaraPaseante>().objetivo = jugador;

            // ---------- Gemelo digital: bancales de cultivo, simulador y tableta ----------
            Cultivos.Crear(terreno, huertoGo, camGo.transform, jugador);

            // ---------- Musica, ambiente y efectos ----------
            Sonido.Crear(jugador, Object.FindFirstObjectByType<Gemelo.SimuladorHuerto>(), arbolesGo, pueblo);

            // Sombras hasta 300 m: mas alla las casas y los arboles se ven ya pequenos y con bruma
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null && urp.shadowDistance != 300f) { urp.shadowDistance = 300f; EditorUtility.SetDirty(urp); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), RutaEscena);
            Debug.Log($"Huerto: escena generada en {RutaEscena} ({reloj.Elapsed.TotalSeconds:0.0} s)");
        }
    }
}
