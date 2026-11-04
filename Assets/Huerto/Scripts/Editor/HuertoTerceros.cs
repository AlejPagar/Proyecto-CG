using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Huerto
{
    /// <summary>
    /// Modelos gratuitos de la Asset Store (Assets/Terceros, extraidos con Herramientas/Paquetes/unitypackage.py):
    /// el agricultor animado que hace de personaje, castanos y fresnos realistas para los arboles del huerto
    /// y el atrezo de aperos junto a la caseta y los bancales. Si falta algun paquete se usa lo procedural.
    /// </summary>
    public static class Terceros
    {
        public const string Carpeta = "Assets/Terceros";
        const string Agricultor = Carpeta + "/Agricultor_Bubba";
        const string RutaAnim = Util.Raiz + "/Animacion";
        const string RutaControlador = RutaAnim + "/Agricultor.controller";
        static readonly (string fbx, string clip, string nombre)[] Clips =
        {
            ("Handyman_idle_1_animation", "Handyman_idle1_anim", "Agricultor_Reposo"),
            ("Handyman_walk_animation", "Handyman_walk_anim", "Agricultor_Andar"),
            ("Handyman_run_animation", "Handyman_run_anim", "Agricultor_Correr"),
        };

        static GameObject Prefab(string ruta) => AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

        // ---------------- Agricultor ----------------
        /// <summary>Version del agricultor con peto, camisa y gorra (la que mas pega en un huerto).</summary>
        public static GameObject PrefabAgricultor() => Prefab($"{Agricultor}/Prefabs/Handyman_ver_2.prefab");

        /// <summary>
        /// Saca las animaciones de los FBX del paquete a .anim propios y borra esos FBX (cada uno lleva el modelo entero, 36 MB).
        /// Solo hace falta una vez, despues de extraer el paquete.
        /// </summary>
        [MenuItem("Huerto/Terceros/Extraer animaciones del agricultor")]
        public static void ExtraerAnimaciones()
        {
            Util.Carpeta(RutaAnim);
            foreach (var (fbx, clip, nombre) in Clips)
            {
                string destino = $"{RutaAnim}/{nombre}.anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(destino) != null) continue;
                var origen = AssetDatabase.LoadAllAssetsAtPath($"{Agricultor}/Animations/{fbx}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == clip);
                if (origen == null) { Debug.LogWarning($"Huerto: no encuentro la animacion {clip}"); continue; }
                var copia = Object.Instantiate(origen);
                copia.name = nombre;
                var ajustes = AnimationUtility.GetAnimationClipSettings(origen);
                ajustes.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copia, ajustes);
                AssetDatabase.CreateAsset(copia, destino);
            }
            AssetDatabase.SaveAssets();
            if (Clips.All(c => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{RutaAnim}/{c.nombre}.anim") != null))
            {
                AssetDatabase.DeleteAsset($"{Agricultor}/Animations");
                Debug.Log("Huerto: animaciones del agricultor extraidas a " + RutaAnim);
            }
        }

        /// <summary>
        /// Animacion humanoide de Mixamo (Assets/Terceros/Mixamo). Se queda en el sitio: lo que mueve al personaje es el Paseante.
        /// </summary>
        static AnimationClip ClipMixamo(string archivo, bool bucle)
        {
            string ruta = $"{Carpeta}/Mixamo/{archivo}.fbx";
            var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (imp == null) return null;
            if (imp.animationType != ModelImporterAnimationType.Human || imp.clipAnimations.Length == 0)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
                var defs = imp.defaultClipAnimations;
                foreach (var d in defs)
                {
                    d.name = archivo;
                    d.loopTime = bucle;
                    d.lockRootRotation = true;
                    d.lockRootHeightY = bucle;
                    d.lockRootPositionXZ = bucle;
                }
                imp.clipAnimations = defs;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        }

        /// <summary>Duracion de la animacion de encaramarse (0 si no esta el paquete de Mixamo).</summary>
        public static float DuracionTrepar()
        {
            var c = ClipMixamo("Freehang Climb", false);
            return c != null ? c.length : 0f;
        }

        /// <summary>Controlador con una mezcla por velocidad: reposo, andar (1,35 m/s) y correr (3,4 m/s), los estados de Mixamo y el IK para la tableta.</summary>
        public static RuntimeAnimatorController ControladorAgricultor(float andar, float correr)
        {
            var clips = Clips.Select(c => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{RutaAnim}/{c.nombre}.anim")).ToArray();
            if (clips.Any(c => c == null)) { ExtraerAnimaciones(); clips = Clips.Select(c => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{RutaAnim}/{c.nombre}.anim")).ToArray(); }
            if (clips.Any(c => c == null)) return null;
            AssetDatabase.DeleteAsset(RutaControlador);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
            ctrl.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
            ctrl.CreateBlendTreeInController("Locomocion", out var arbol, 0);
            arbol.blendParameter = "Velocidad";
            arbol.useAutomaticThresholds = false;
            arbol.AddChild(clips[0], 0f);
            arbol.AddChild(clips[1], andar);
            arbol.AddChild(clips[2], correr);
            var maquina = ctrl.layers[0].stateMachine;
            var tableta = ClipMixamo("Standing Using Touchscreen Tablet", true);
            if (tableta != null) maquina.AddState("Tableta").motion = tableta;
            var trepar = ClipMixamo("Freehang Climb", false);
            if (trepar != null) maquina.AddState("Trepar").motion = trepar;
            var capas = ctrl.layers;
            capas[0].iKPass = true;
            ctrl.layers = capas;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ---------------- Arboles ----------------
        static readonly Dictionary<string, (float alto, float ancho)> medidas = new Dictionary<string, (float, float)>();

        /// <summary>
        /// Prefab realista para una especie del huerto (castano: castano; roble, nogal y arbol joven: fresno) o null.
        /// Los arboles altos usan las variantes con la copa mas alta (empieza al 30-45 % de la altura) para poder pasar por debajo.
        /// </summary>
        public static GameObject PrefabArbol(string especie, int semilla, float alto)
        {
            string ruta = null;
            int k = Mathf.Abs(semilla);
            bool alto6 = alto > 6f;
            if (especie == "Castano") ruta = $"Chestnut/Chestnut {(alto > 7f ? 8 : alto6 ? new[] { 8, 4 }[k % 2] : k % 8 + 1)}";
            else if (especie == "Roble" || especie == "Nogal") ruta = $"Ash/Ash {(alto6 ? new[] { 1, 5, 6, 7 }[k % 4] : k % 8 + 1)}";
            else if (especie == "Joven") ruta = $"Ash/Ash {new[] { 2, 3, 4 }[k % 3]}";
            return ruta == null ? null : Prefab($"{Carpeta}/Arboles_EuropeanForests/Prefabs/URP/{ruta}.prefab");
        }

        /// <summary>
        /// Los castanos del paquete son de Indias (hoja palmeada y flor en racimo); en La Alberca es el castano comun.
        /// Copia los materiales de hojas con texturas propias de Castanea sativa (Herramientas/Texturas/castano.py):
        /// ramita y ramillete de hojas aserradas y los amentos de junio en lugar de la flor.
        /// </summary>
        static readonly Dictionary<Material, Material> castanoComun = new Dictionary<Material, Material>();

        static Material HojaCastanoComun(Material original)
        {
            if (original == null || !original.shader.name.Contains("Leaves")) return original;
            if (castanoComun.TryGetValue(original, out var hecho) && hecho != null) return hecho;
            var sh = original.shader;
            string prop = null;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
                if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture && sh.GetPropertyDescription(i) == "Leave Texture") prop = sh.GetPropertyName(i);
            var tex = prop != null ? original.GetTexture(prop) : null;
            if (tex == null) return original;
            string nombre = tex.name == "Leaf_3" ? "Castano_Amento" : tex.name == "Leaf_2" ? "Castano_Hoja_2" : "Castano_Hoja_1";
            string ruta = $"{Util.Raiz}/Materiales/Arbol_{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) { m = new Material(original); AssetDatabase.CreateAsset(m, ruta); }
            else m.CopyPropertiesFromMaterial(original);
            m.SetTexture(prop, Util.Tex($"Plantas/{nombre}.png", hojas: true, max: 1024));
            EditorUtility.SetDirty(m);
            return castanoComun[original] = m;
        }

        static (float alto, float ancho) Medir(GameObject prefab)
        {
            string ruta = AssetDatabase.GetAssetPath(prefab);
            if (medidas.TryGetValue(ruta, out var m)) return m;
            var go = Object.Instantiate(prefab);
            var rs = go.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.Contains("LOD0")).ToArray();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(go);
            return medidas[ruta] = (b.size.y, (b.size.x + b.size.z) * 0.5f);
        }

        /// <summary>Arbol realista con la altura y la copa del LiDAR; sin la lluvia de hojas del paquete.</summary>
        public static GameObject Arbol(GameObject prefab, string nombre, Transform padre, Vector3 pos, float rumbo, float alto, float copa)
        {
            var (altoP, anchoP) = Medir(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = nombre;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject != go) { Object.DestroyImmediate(ps.gameObject); continue; }
                var pr = ps.GetComponent<ParticleSystemRenderer>();
                Object.DestroyImmediate(ps);
                if (pr != null) Object.DestroyImmediate(pr);
            }
            if (AssetDatabase.GetAssetPath(prefab).Contains("/Chestnut/"))
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Select(HojaCastanoComun).ToArray();
            float s = alto / altoP;
            // la copa del LiDAR ensancha o estrecha un poco el arbol (radio medido en el MDS)
            float sh = copa > 0.5f ? Mathf.Clamp(2f * copa / (anchoP * s), 0.8f, 1.25f) : 1f;
            go.transform.localScale = new Vector3(s * sh, s, s * sh);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rumbo, 0));
            return go;
        }

        // ---------------- Atrezo ----------------
        static float Suelo(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;

        /// <summary>
        /// Coloca un prefab: rumbo e inclinacion (grados, sobre el eje X y Z del mundo tras el rumbo) y escala.
        /// Con 'altura' &lt; 0 se apoya en el suelo por su punto mas bajo; si no, el pivote queda a esa altura sobre el suelo.
        /// </summary>
        static GameObject Poner(Terrain t, Transform padre, string ruta, string nombre, Vector2 pos, float rumbo, float incX = 0f, float incZ = 0f, float escala = 1f, float altura = -1f)
        {
            var pf = Prefab($"{Carpeta}/{ruta}.prefab");
            if (pf == null) { Debug.LogWarning("Huerto: falta " + ruta); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, padre);
            go.name = nombre;
            go.transform.rotation = Quaternion.Euler(0, rumbo, 0) * Quaternion.Euler(incX, 0, incZ) * pf.transform.rotation;
            go.transform.localScale = pf.transform.localScale * escala;
            float suelo = Suelo(t, pos.x, pos.y);
            go.transform.position = new Vector3(pos.x, suelo + Mathf.Max(0f, altura), pos.y);
            if (altura < 0f)
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    go.transform.position += Vector3.up * (suelo + 0.005f - b.min.y);
                }
            }
            return go;
        }

        /// <summary>Aperos apoyados en la caseta, lena, escalera, tocon con hacha y cestas y herramientas junto a los bancales.</summary>
        public static void Atrezo(Terrain t, Transform padre)
        {
            if (!AssetDatabase.IsValidFolder($"{Carpeta}/Herramientas_Donopardo")) return;
            var raiz = new GameObject("Aperos_y_Atrezo").transform;
            raiz.SetParent(padre, false);
            const string D = "Herramientas_Donopardo/Prefabs/", G = "Herramientas_GBAndrewGB/", C = "Cestas_union3d/Prefabs/", N = "Aldea_NikolayFedorov/OldVillage/VilagePropsPack/Prefab/";

            // Caseta (97,6; 101) girada 180 grados: la fachada con la puerta mira al norte, en z = 102. Herramientas apoyadas a los lados de la puerta.
            const float inclina = -11f;   // la punta hacia la pared (sur)
            Poner(t, raiz, D + "Rake_001", "Rastrillo", new Vector2(96.62f, 102.33f), 0f, inclina);
            Poner(t, raiz, D + "Pitchfork_002", "Horca", new Vector2(96.9f, 102.33f), 8f, inclina);
            Poner(t, raiz, D + "Shovel_002", "Pala", new Vector2(98.32f, 102.33f), -6f, inclina);
            Poner(t, raiz, D + "Broom_001", "Escoba", new Vector2(98.62f, 102.33f), 90f, inclina);
            Poner(t, raiz, D + "Scythe_001", "Guadana", new Vector2(96.2f, 102.3f), 180f, inclina);
            Poner(t, raiz, G + "Watering Can/WateringCanPrefab", "Regadera", new Vector2(99.25f, 102.45f), 200f, escala: 0.082f);
            Poner(t, raiz, C + "light/Basket_01_light", "Cesta_Caseta", new Vector2(95.75f, 102.55f), 0f, escala: 0.3f);

            // Escalera apoyada en la pared este (x = 98,8) y lena apilada contra la oeste (x = 96,4)
            Poner(t, raiz, N + "Ladder/ladder_01", "Escalera", new Vector2(99.5f, 100.55f), 0f, 0f, 13.5f);
            var lena = new GameObject("Lena").transform; lena.SetParent(raiz, false);
            for (int fila = 0; fila < 3; fila++)
                for (int i = 0; i < 4 - fila; i++)
                {
                    float z = 100.15f + i * 0.3f + fila * 0.15f;
                    Poner(t, lena, N + (((i + fila) & 1) == 0 ? "Drova/Drova01" : "Drova/Drova02"), $"Lena_{fila}_{i}", new Vector2(96.12f, z), 90f + (i * 37 % 11) - 5f, altura: 0.11f + fila * 0.2f);
                }
            var tocon = Poner(t, raiz, N + "Trunks/Trunk01", "Tocon", new Vector2(95.2f, 103.3f), 25f);
            if (tocon != null)
            {
                var b = tocon.GetComponentInChildren<Renderer>().bounds;
                var hacha = Poner(t, raiz, D + "Axe_002", "Hacha", new Vector2(95.2f, 103.3f), 35f, altura: 0f);
                if (hacha != null) hacha.transform.position = new Vector3(95.12f, b.max.y + 0.015f, 103.28f);
            }

            // Cubo oxidado bajo el grifo del deposito de agua (104,6; 70) y un palet suelto al lado
            var cubo = Poner(t, raiz, N + "bucket01", "Cubo", new Vector2(103.85f, 69.35f), 0f);
            var oxido = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Aldea_NikolayFedorov/PBR_Tiled/MaterialsPBR/Rust.mat");
            if (cubo != null && oxido != null) foreach (var r in cubo.GetComponentsInChildren<Renderer>()) r.sharedMaterial = oxido;
            Poner(t, raiz, N + "Pallets/Pallet_01", "Palet", new Vector2(106.3f, 70.7f), 12f);

            // Junto a los bancales: cestas para la cosecha, una azada en el pasillo, la hoz y otra pala
            Poner(t, raiz, C + "dark/Basket_02_dark", "Cesta_Tomates", new Vector2(102.5f, 76.2f), 0f, escala: 0.28f);
            Poner(t, raiz, C + "light/Basket_02_light", "Cesta_Lechugas", new Vector2(99.75f, 97.0f), 0f, escala: 0.26f);
            Poner(t, raiz, G + "Hoe/HoePrefab", "Azada", new Vector2(100.6f, 79.12f), 90f, escala: 0.43f);
            Poner(t, raiz, G + "Sickle/SicklePrefab", "Hoz", new Vector2(96.95f, 90.2f), 30f, escala: 0.11f);
            Poner(t, raiz, D + "Shovel_003", "Pala_Bancal", new Vector2(90.15f, 87.65f), 100f, 90f);
            Debug.Log($"Huerto: {raiz.GetComponentsInChildren<Renderer>().Length} piezas de atrezo de la Asset Store");
        }
    }
}
