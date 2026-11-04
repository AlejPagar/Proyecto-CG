using System.Collections;
using System.Collections.Generic;
using Huerto.Gemelo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Huerto.Juego
{
    public enum Efecto { Clic, Pestana, Interruptor, TabletaAbrir, TabletaCerrar, Regar, Cosechar, Plantar, AvisoBueno, AvisoMalo, Salto, Trepar }

    /// <summary>Sonidos de un tipo de suelo (hierba, tierra, hojarasca, piedra).</summary>
    [System.Serializable]
    public class SonidosSuelo
    {
        public string nombre;
        public AudioClip[] pasos, aterrizajes;
    }

    /// <summary>
    /// Musica y sonido del huerto. Todo el audio esta sintetizado en Herramientas/Audio (sin grabaciones de terceros).
    /// El ambiente sigue al gemelo digital: el clima del bancal mas cercano y la epoca de la temporada deciden
    /// si cantan el cuco o las chicharras, y cada bancal suena a lluvia solo cuando llueve en su serie.
    /// Las pisadas cambian con la capa del terreno que hay bajo los pies.
    /// </summary>
    public class AudioHuerto : MonoBehaviour
    {
        public static AudioHuerto Instancia;
        public static float VolMusica = 0.55f, VolAmbiente = 0.8f, VolEfectos = 0.8f;
        public static bool MusicaSilenciada;
        public static readonly string[] NombresEfecto = { "Clic", "Pestana", "Interruptor", "Tableta_abrir", "Tableta_cerrar", "Regar", "Cosechar", "Plantar", "Aviso_bueno", "Aviso_malo", "Salto", "Trepar" };

        [Header("Musica")]
        public AudioClip[] musica;
        public Vector2 silencioEntreTemas = new Vector2(30f, 75f);

        [Header("Ambiente")]
        public AudioClip viento;
        public AudioClip pajaros, chicharras, grillos, lluvia;
        public AudioClip[] aves;
        public AudioClip campanaGrave, campanaAguda;
        public Vector3 posIglesia;
        public Transform arboles;

        [Header("Personaje y gemelo")]
        public Paseante paseante;
        public SimuladorHuerto simulador;
        public SonidosSuelo[] suelos;
        public AudioClip[] efectos;

        AudioSource fuenteMusica, fuenteUI, fuentePasos;
        AudioSource vientoSrc, pajarosSrc, chicharrasSrc, grillosSrc;
        readonly List<AudioSource> lluvias = new List<AudioSource>();
        readonly List<AudioSource> puntuales = new List<AudioSource>();
        readonly List<Vector3> copas = new List<Vector3>();
        int tema = -1; float volTema, proximoTema, proximaAve, proximaCampana, proximoAviso, proximoTick;
        bool sonandoTema;
        readonly Dictionary<TerrainData, int[]> capas = new Dictionary<TerrainData, int[]>();

        public string TemaActual => sonandoTema && tema >= 0 ? NombreTema(musica[tema]) : "—";
        static string NombreTema(AudioClip c) => c.name.Replace("Musica_", "").Replace('_', ' ');

        // ---------------- Arranque ----------------
        void Awake()
        {
            Instancia = this;
            VolMusica = PlayerPrefs.GetFloat("Huerto.VolMusica", VolMusica);
            VolAmbiente = PlayerPrefs.GetFloat("Huerto.VolAmbiente", VolAmbiente);
            VolEfectos = PlayerPrefs.GetFloat("Huerto.VolEfectos", VolEfectos);
            MusicaSilenciada = PlayerPrefs.GetInt("Huerto.MusicaSilenciada", 0) == 1;

            fuenteMusica = Fuente("Musica", null, false);
            fuenteUI = Fuente("Interfaz", null, false);
            vientoSrc = Fuente("Viento", viento, true); pajarosSrc = Fuente("Pajaros", pajaros, true);
            chicharrasSrc = Fuente("Chicharras", chicharras, true); grillosSrc = Fuente("Grillos", grillos, true);
            for (int i = 0; i < 6; i++)
            {
                var s = Fuente("Puntual_" + i, null, false);
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 6f; s.maxDistance = 120f; s.dopplerLevel = 0f;
                puntuales.Add(s);
            }
            if (paseante != null)
            {
                fuentePasos = paseante.gameObject.AddComponent<AudioSource>();
                fuentePasos.playOnAwake = false; fuentePasos.spatialBlend = 0.8f; fuentePasos.minDistance = 3f; fuentePasos.maxDistance = 30f; fuentePasos.dopplerLevel = 0f;
            }
            if (arboles != null)
                foreach (Transform a in arboles) copas.Add(a.position);
        }

        AudioSource Fuente(string nombre, AudioClip clip, bool bucle)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.clip = clip; s.loop = bucle; s.volume = 0f; s.spatialBlend = 0f; s.dopplerLevel = 0f;
            if (bucle && clip != null) { s.Play(); s.timeSamples = Random.Range(0, clip.samples); }
            return s;
        }

        void Start()
        {
            if (paseante != null)
            {
                paseante.Pisada += Pisada; paseante.Aterrizaje += Aterrizaje;
                paseante.Salto += () => SonarEn(Efecto.Salto, fuentePasos, 0.5f);
                paseante.Trepada += () => SonarEn(Efecto.Trepar, fuentePasos, 0.8f);
            }
            if (simulador != null)
            {
                simulador.Alerta += Alerta;
                // lluvia en cada bancal, en 3D sobre la tierra
                foreach (var p in simulador.parcelas)
                {
                    var go = new GameObject("Sonido_Lluvia");
                    go.transform.SetParent(p.transform, false);
                    go.transform.localPosition = Vector3.up * 1.2f;
                    var s = go.AddComponent<AudioSource>();
                    s.clip = lluvia; s.loop = true; s.volume = 0f; s.spatialBlend = 1f; s.dopplerLevel = 0f;
                    s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 3f; s.maxDistance = 45f;
                    if (lluvia != null) { s.Play(); s.timeSamples = Random.Range(0, lluvia.samples); }
                    lluvias.Add(s);
                }
            }
            proximoTema = Time.unscaledTime + 3f;
            proximaAve = Time.time + 2f;
            proximaCampana = Time.time + Random.Range(40f, 90f);
        }

        void OnDestroy()
        {
            if (paseante != null) { paseante.Pisada -= Pisada; paseante.Aterrizaje -= Aterrizaje; }
            if (simulador != null) simulador.Alerta -= Alerta;
            if (Instancia == this) Instancia = null;
        }

        public static void GuardarVolumenes()
        {
            PlayerPrefs.SetFloat("Huerto.VolMusica", VolMusica);
            PlayerPrefs.SetFloat("Huerto.VolAmbiente", VolAmbiente);
            PlayerPrefs.SetFloat("Huerto.VolEfectos", VolEfectos);
            PlayerPrefs.SetInt("Huerto.MusicaSilenciada", MusicaSilenciada ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ---------------- Bucle ----------------
        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.mKey.wasPressedThisFrame) { MusicaSilenciada = !MusicaSilenciada; GuardarVolumenes(); }
            Musica();
            Ambiente();
            Aves();
            Campanas();
        }

        void Musica()
        {
            if (musica == null || musica.Length == 0) return;
            float dt = Time.unscaledDeltaTime;
            if (!sonandoTema && Time.unscaledTime >= proximoTema) SiguienteTema();
            if (sonandoTema && !fuenteMusica.isPlaying)
            {
                sonandoTema = false;
                proximoTema = Time.unscaledTime + Random.Range(silencioEntreTemas.x, silencioEntreTemas.y);
            }
            float objetivo = MusicaSilenciada ? 0f : VolMusica * 0.7f;
            volTema = Mathf.MoveTowards(volTema, objetivo, dt / 2.5f);
            fuenteMusica.volume = volTema;
        }

        /// <summary>Pasa al siguiente tema (el primero es siempre «El huerto de los abuelos»).</summary>
        public void SiguienteTema()
        {
            if (musica == null || musica.Length == 0) return;
            tema = tema < 0 ? 0 : (tema + 1 + (musica.Length > 2 ? Random.Range(0, musica.Length - 1) : 0)) % musica.Length;
            fuenteMusica.clip = musica[tema];
            fuenteMusica.Play();
            volTema = 0f; sonandoTema = true;
        }

        ParcelaCultivo Cercana()
        {
            if (simulador == null || simulador.parcelas == null) return null;
            var oyente = Camera.main != null ? Camera.main.transform.position : transform.position;
            ParcelaCultivo mejor = null; float dmin = float.MaxValue;
            foreach (var p in simulador.parcelas)
            {
                float d = (p.transform.position - oyente).sqrMagnitude;
                if (d < dmin) { dmin = d; mejor = p; }
            }
            return mejor;
        }

        // Actividad de las aves segun la epoca: mucha en primavera, menos con el calor y en otono
        static float Estacion(int dia, float ini, float pico, float fin, float bajada)
            => Mathf.Clamp01(Mathf.InverseLerp(ini, pico, dia)) * (1f - Mathf.Clamp01(Mathf.InverseLerp(fin, fin + bajada, dia)));

        float actividadAves = 1f;

        void Ambiente()
        {
            float dt = Time.unscaledDeltaTime;
            int dia = simulador != null ? simulador.Dia : 140;
            var p = Cercana();
            var hoy = p != null ? p.Hoy : new DiaMeteo { tmax = 24f, tmin = 10f };
            bool llueve = hoy.lluvia > 0.5f;

            actividadAves = (0.35f + 0.65f * Estacion(dia, 60, 100, 170, 60)) * (llueve ? 0.25f : 1f) * (hoy.tmax > 33f ? 0.5f : 1f);
            float racha = Mathf.PerlinNoise(Time.time * 0.05f, 0.3f);
            Fundir(vientoSrc, (0.3f + 0.35f * racha + (llueve ? 0.2f : 0f)) * VolAmbiente, dt);
            Fundir(pajarosSrc, 0.45f * actividadAves * VolAmbiente, dt);
            float calor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 31f, hoy.tmax));
            Fundir(chicharrasSrc, 0.5f * Estacion(dia, 165, 180, 235, 20) * calor * (llueve ? 0f : 1f) * VolAmbiente, dt);
            Fundir(grillosSrc, 0.22f * Estacion(dia, 135, 160, 260, 30) * Mathf.InverseLerp(6f, 16f, hoy.tmin) * (llueve ? 0.3f : 1f) * VolAmbiente, dt);

            // lluvia: suena donde caen las gotas del bancal
            if (simulador != null)
                for (int i = 0; i < lluvias.Count && i < simulador.parcelas.Length; i++)
                {
                    var ps = simulador.parcelas[i].lluvia;
                    float tasa = ps != null && ps.isPlaying ? ps.emission.rateOverTime.constant : 0f;
                    Fundir(lluvias[i], (tasa > 1f ? Mathf.Clamp01(0.45f + tasa / 1500f) : 0f) * VolAmbiente, dt, 1.2f);
                }
        }

        static void Fundir(AudioSource s, float objetivo, float dt, float tiempo = 3f)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, objetivo, dt / tiempo);
        }

        // ---------------- Aves y campanas (sonidos en 3D) ----------------
        AudioSource Libre()
        {
            foreach (var s in puntuales) if (!s.isPlaying) return s;
            return null;
        }

        float PesoAve(string nombre, int dia)
        {
            if (nombre.Contains("Cuco")) return Estacion(dia, 85, 100, 175, 20) * 0.6f;      // abril a junio
            if (nombre.Contains("Abubilla")) return Estacion(dia, 70, 90, 220, 30) * 0.5f;
            if (nombre.Contains("Torcaz")) return 0.6f;
            if (nombre.Contains("Mirlo")) return 0.5f + 0.5f * Estacion(dia, 40, 70, 180, 40);
            return 1f;
        }

        void Aves()
        {
            if (aves == null || aves.Length == 0 || Time.time < proximaAve) return;
            proximaAve = Time.time + Random.Range(3f, 9f) / Mathf.Max(0.15f, actividadAves);
            var s = Libre(); if (s == null) return;
            int dia = simulador != null ? simulador.Dia : 140;
            float total = 0f; foreach (var a in aves) total += PesoAve(a.name, dia);
            float r = Random.value * total; AudioClip clip = aves[0];
            foreach (var a in aves) { r -= PesoAve(a.name, dia); if (r <= 0f) { clip = a; break; } }

            var oyente = Camera.main != null ? Camera.main.transform.position : transform.position;
            Vector3 pos = oyente + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(15f, 35f) + Vector3.up * 5f;
            for (int i = 0; i < 12 && copas.Count > 0; i++)
            {
                var c = copas[Random.Range(0, copas.Count)];
                float d = Vector3.Distance(new Vector3(c.x, 0, c.z), new Vector3(oyente.x, 0, oyente.z));
                if (d > 6f && d < 55f) { pos = c + Vector3.up * Random.Range(3f, 7f); break; }
            }
            bool lejana = clip.name.Contains("Cuco") || clip.name.Contains("Torcaz") || clip.name.Contains("Abubilla");
            s.transform.position = pos;
            s.minDistance = lejana ? 25f : 6f; s.maxDistance = lejana ? 300f : 120f;
            s.pitch = Random.Range(0.95f, 1.05f);
            s.clip = clip; s.volume = Random.Range(0.55f, 1f) * VolAmbiente;
            s.Play();
        }

        void Campanas()
        {
            if (campanaGrave == null || Time.time < proximaCampana) return;
            proximaCampana = Time.time + Random.Range(150f, 300f);
            StartCoroutine(Toque(Random.Range(3, 6)));
        }

        IEnumerator Toque(int golpes)
        {
            for (int i = 0; i < golpes; i++)
            {
                var s = Libre();
                if (s != null)
                {
                    s.transform.position = posIglesia;
                    s.minDistance = 70f; s.maxDistance = 2000f; s.pitch = 1f;
                    s.clip = i == golpes - 1 && campanaAguda != null ? campanaAguda : campanaGrave;
                    s.volume = 0.8f * VolAmbiente;
                    s.Play();
                }
                yield return new WaitForSeconds(2.4f);
            }
        }

        // ---------------- Personaje ----------------
        SonidosSuelo SueloBajo(Vector3 p)
        {
            if (suelos == null || suelos.Length == 0) return null;
            if (!Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var h, 1.2f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return suelos[0];
            if (!(h.collider is TerrainCollider tc)) return suelos[suelos.Length - 1];   // muros, rocas y pilares: piedra
            var td = tc.terrainData;
            if (!capas.TryGetValue(td, out var mapa))
            {
                mapa = new int[td.terrainLayers.Length];
                for (int i = 0; i < mapa.Length; i++)
                {
                    string n = td.terrainLayers[i] != null ? td.terrainLayers[i].name : "";
                    mapa[i] = n.Contains("Tierra") ? 1 : n.Contains("Hojarasca") ? 2 : n.Contains("Empedrado") || n.Contains("Roca") ? 3 : 0;
                }
                capas[td] = mapa;
            }
            Vector3 l = h.point - tc.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt(l.x / td.size.x * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(l.z / td.size.z * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
            var a = td.GetAlphamaps(x, z, 1, 1);
            int mejor = 0;
            for (int i = 1; i < mapa.Length; i++) if (a[0, 0, i] > a[0, 0, mejor]) mejor = i;
            return suelos[Mathf.Clamp(mapa.Length > 0 ? mapa[mejor] : 0, 0, suelos.Length - 1)];
        }

        void Pisada(float rapidez)
        {
            var s = SueloBajo(paseante.transform.position);
            if (s == null || s.pasos == null || s.pasos.Length == 0 || fuentePasos == null) return;
            fuentePasos.pitch = Random.Range(0.92f, 1.08f);
            fuentePasos.PlayOneShot(s.pasos[Random.Range(0, s.pasos.Length)], (0.35f + 0.3f * Mathf.Clamp01(rapidez / paseante.velCorrer)) * VolEfectos);
        }

        void Aterrizaje(float caida)
        {
            var s = SueloBajo(paseante.transform.position);
            if (s == null || s.aterrizajes == null || s.aterrizajes.Length == 0 || fuentePasos == null) return;
            fuentePasos.pitch = Random.Range(0.95f, 1.05f);
            fuentePasos.PlayOneShot(s.aterrizajes[Random.Range(0, s.aterrizajes.Length)], Mathf.Clamp01(0.4f + caida / 10f) * VolEfectos);
        }

        void SonarEn(Efecto e, AudioSource s, float vol)
        {
            int i = (int)e;
            if (s == null || efectos == null || i >= efectos.Length || efectos[i] == null) return;
            s.pitch = 1f;
            s.PlayOneShot(efectos[i], vol * VolEfectos);
        }

        // ---------------- Gemelo digital ----------------
        void Alerta(ParcelaCultivo p, string texto)
        {
            bool bueno = texto.StartsWith("Lista para cosechar") || texto.StartsWith("Cosechados");
            bool malo = texto.StartsWith("Helada") || texto.StartsWith("Las plantas han muerto") || texto.StartsWith("Infección") || texto.StartsWith("Oídio");
            if (!(bueno || malo) || Time.unscaledTime < proximoAviso) return;
            proximoAviso = Time.unscaledTime + 1.5f;
            Sonar(malo ? Efecto.AvisoMalo : Efecto.AvisoBueno, 0.6f);
        }

        /// <summary>Sonido de interfaz o de tarea (sin posicion).</summary>
        public static void Sonar(Efecto e, float vol = 1f, float tono = 1f)
        {
            var a = Instancia;
            if (a == null) return;
            a.fuenteUI.pitch = tono;
            a.SonarEn(e, a.fuenteUI, vol);
        }

        /// <summary>Clics de la tableta: botones, pestanas, tarjetas, interruptores, desplegables y deslizadores.</summary>
        public static void ConectarInterfaz(VisualElement raiz)
        {
            raiz.RegisterCallback<ClickEvent>(e =>
            {
                for (var v = e.target as VisualElement; v != null; v = v.parent)
                {
                    if (v is Button b) { Sonar(b.ClassListContains("pestana") ? Efecto.Pestana : Efecto.Clic, 0.7f); return; }
                    if (v.ClassListContains("tarjeta")) { Sonar(Efecto.Clic, 0.6f, 0.9f); return; }
                }
            }, TrickleDown.TrickleDown);
            raiz.RegisterCallback<ChangeEvent<bool>>(e => { if (e.target is Toggle) Sonar(Efecto.Interruptor, 0.7f); }, TrickleDown.TrickleDown);
            raiz.RegisterCallback<ChangeEvent<string>>(e => { if (e.target is DropdownField) Sonar(Efecto.Clic, 0.7f); }, TrickleDown.TrickleDown);
            raiz.RegisterCallback<ChangeEvent<float>>(e =>
            {
                var a = Instancia;
                if (a == null || !(e.target is Slider s) || Time.unscaledTime < a.proximoTick) return;
                a.proximoTick = Time.unscaledTime + 0.07f;
                Sonar(Efecto.Clic, 0.35f, 0.8f + 0.5f * Mathf.InverseLerp(s.lowValue, s.highValue, e.newValue));
            }, TrickleDown.TrickleDown);
        }
    }
}
