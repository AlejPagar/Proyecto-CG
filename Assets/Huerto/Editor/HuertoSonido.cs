using System.Collections.Generic;
using System.IO;
using System.Linq;
using Huerto.Gemelo;
using Huerto.Juego;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Huerto
{
    /// <summary>
    /// Ajustes de importacion del audio del huerto: la musica se lee en streaming, los bucles de ambiente
    /// se quedan comprimidos en memoria y los efectos cortos se descomprimen al cargar (en mono, para 3D).
    /// </summary>
    public class ImportarAudioHuerto : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Sonido.Carpeta + "/")) return;
            var imp = (AudioImporter)assetImporter;
            bool musica = assetPath.Contains("/Musica/");
            bool bucle = Path.GetFileName(assetPath).StartsWith("Amb_");
            var s = imp.defaultSampleSettings;
            s.loadType = musica ? AudioClipLoadType.Streaming : bucle ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = musica ? 0.6f : 0.5f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !musica && !bucle;
            imp.loadInBackground = musica;
        }
    }

    /// <summary>Monta el objeto «Sonido» de la escena con todos los clips (generados por Herramientas/Audio).</summary>
    public static class Sonido
    {
        public const string Carpeta = Util.Raiz + "/Audio";

        static AudioClip[] Clips(string sub, string prefijo) =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { $"{Carpeta}/{sub}" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.name.StartsWith(prefijo)).OrderBy(c => c.name).ToArray();

        static AudioClip Clip(string sub, string nombre) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Carpeta}/{sub}/{nombre}.ogg");

        public static AudioHuerto Crear(Paseante paseante, SimuladorHuerto sim, Transform arboles, Terrain pueblo)
        {
            var viejo = GameObject.Find("Sonido");
            if (viejo != null) Object.DestroyImmediate(viejo);
            var go = new GameObject("Sonido");
            var a = go.AddComponent<AudioHuerto>();
            // primero el tema del huerto, luego los demas
            a.musica = Clips("Musica", "Musica_").OrderBy(c => c.name.Contains("huerto") ? 0 : 1).ThenBy(c => c.name).ToArray();
            a.viento = Clip("Ambiente", "Amb_Viento"); a.pajaros = Clip("Ambiente", "Amb_Pajaros");
            a.chicharras = Clip("Ambiente", "Amb_Chicharras"); a.grillos = Clip("Ambiente", "Amb_Grillos"); a.lluvia = Clip("Ambiente", "Amb_Lluvia");
            a.aves = Clips("Ambiente", "Ave_");
            a.campanaGrave = Clip("Ambiente", "Campana_grave"); a.campanaAguda = Clip("Ambiente", "Campana_aguda");
            // campanario en el centro del casco antiguo (plaza e iglesia), unos 25 m sobre el suelo
            var casco = new Vector3(250f, 0f, 250f);
            casco.y = (pueblo != null ? pueblo.SampleHeight(casco) + pueblo.transform.position.y : 0f) + 25f;
            a.posIglesia = casco;
            a.arboles = arboles; a.paseante = paseante; a.simulador = sim;
            a.suelos = new[] { "hierba", "tierra", "hojarasca", "piedra" }
                .Select(n => new SonidosSuelo { nombre = n, pasos = Clips("Efectos", $"Pisada_{n}_"), aterrizajes = Clips("Efectos", $"Aterrizaje_{n}_") }).ToArray();
            a.efectos = AudioHuerto.NombresEfecto.Select(n => Clip("Efectos", n)).ToArray();

            var faltan = new List<string>();
            if (a.musica.Length == 0) faltan.Add("musica");
            if (a.aves.Length == 0) faltan.Add("aves");
            for (int i = 0; i < a.efectos.Length; i++) if (a.efectos[i] == null) faltan.Add(AudioHuerto.NombresEfecto[i]);
            if (faltan.Count > 0) Debug.LogWarning("Huerto: faltan clips de audio: " + string.Join(", ", faltan));
            Debug.Log($"Huerto: sonido con {a.musica.Length} temas, {a.aves.Length} cantos de aves y {a.suelos.Sum(s => s.pasos.Length)} pisadas");
            return a;
        }

        [MenuItem("Huerto/Añadir sonido a la escena")]
        public static void Menu()
        {
            var paseante = Object.FindFirstObjectByType<Paseante>();
            var sim = Object.FindFirstObjectByType<SimuladorHuerto>();
            var arboles = GameObject.Find("Arboles");
            var pueblo = GameObject.Find("Terreno_Pueblo_LiDAR");
            Crear(paseante, sim, arboles != null ? arboles.transform : null, pueblo != null ? pueblo.GetComponent<Terrain>() : null);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
