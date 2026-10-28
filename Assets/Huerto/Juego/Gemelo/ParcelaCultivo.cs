using System.Collections.Generic;
using UnityEngine;

namespace Huerto.Gemelo
{
    /// <summary>Mallas de un cultivo para cada etapa visual (plantula, vegetativo, flor, fruto verde, maduro) y sus materiales.</summary>
    [System.Serializable]
    public class ModeloPlanta
    {
        public string cultivo;
        public Mesh[] etapas;
        public Material[] materiales;   // 0 tallo, 1 hoja, 2 fruto verde, 3 flor, 4 tutor, 5 fruto maduro
    }

    /// <summary>
    /// Bancal de cultivo del gemelo digital: su cultivo, su propia climatologia y su modelo de crecimiento.
    /// Las plantas se dibujan en una sola malla que se rehace cuando cambian de etapa o de tamano;
    /// el suelo se oscurece con la humedad y llueve encima solo cuando llueve en la serie de esta parcela.
    /// </summary>
    [ExecuteAlways]
    public class ParcelaCultivo : MonoBehaviour
    {
        public int numero;
        public string nombre;
        public Color color = Color.white;
        public Vector2 tam = new Vector2(3f, 4f);      // ancho (x local) y largo (z local), en metros
        public string cultivoId = "Tomate";
        public PerfilClima clima = new PerfilClima();
        public bool riegoAuto = true, cosechaAuto = true;
        public Terrain terreno;
        public MeshRenderer suelo;
        public ParticleSystem lluvia;

        [System.NonSerialized] public readonly ModeloCultivo modelo = new ModeloCultivo();
        /// <summary>Avisos de la temporada (dia, texto), sin repetir el mismo aviso en dias seguidos.</summary>
        [System.NonSerialized] public readonly List<KeyValuePair<int, string>> avisos = new List<KeyValuePair<int, string>>();
        DiaMeteo[] serie;
        int diaHoy;

        public Cultivo Cultivo => Cultivo.Get(cultivoId);
        public float Area => tam.x * tam.y;
        public DiaMeteo Hoy => Serie()[Mathf.Clamp(diaHoy, 0, 365)];
        public DiaMeteo DiaClima(int dia) => Serie()[Mathf.Clamp(dia, 0, 365)];

        // ---- plantas ----
        struct Planta { public Vector3 pos; public float giro, escala; }
        readonly List<Planta> plantas = new List<Planta>();
        GameObject plantasGo; Mesh mallaPlantas;
        int etapaHecha = -2; float tamHecho = -1f; string cultivoHecho;
        MaterialPropertyBlock mpb;
        float ultimaVez = -10f;

        /// <summary>Hay que llamarlo al cambiar cualquier dato del clima de la parcela.</summary>
        public void ClimaCambiado() => serie = null;

        DiaMeteo[] Serie()
        {
            if (serie == null) serie = clima.Serie(SimuladorHuerto.Base);
            return serie;
        }

        public void Reiniciar(int diaInicio)
        {
            int siembra = Mathf.Max(Cultivo.diaSiembra, diaInicio);
            modelo.Reiniciar(Cultivo, siembra, Area);
            modelo.riegoAuto = riegoAuto; modelo.cosechaAuto = cosechaAuto;
            diaHoy = diaInicio; serie = null; avisos.Clear();
            Colocar();
        }

        /// <summary>Planta otro cultivo hoy mismo (o en su fecha habitual si aun no ha llegado).</summary>
        public void Resembrar(string id, int hoy)
        {
            cultivoId = id;
            int siembra = Cultivo.diaSiembra >= hoy ? Cultivo.diaSiembra : hoy;
            modelo.Reiniciar(Cultivo, siembra, Area);
            modelo.riegoAuto = riegoAuto; modelo.cosechaAuto = cosechaAuto;
            avisos.Add(new KeyValuePair<int, string>(hoy, siembra > hoy ? $"Se plantará {Cultivo.nombre.ToLower()} el {SimuladorHuerto.Fecha(siembra)}" : $"Plantado: {Cultivo.nombre.ToLower()}"));
            Colocar();
            ActualizarVisual(true);
        }

        public void Avanzar(int dia)
        {
            diaHoy = dia;
            modelo.sombraHoy = clima.sombra;
            modelo.riegoAuto = riegoAuto; modelo.cosechaAuto = cosechaAuto;
            modelo.Paso(dia, Serie()[Mathf.Clamp(dia, 0, 365)]);
            foreach (var a in modelo.alertas)
            {
                int i = avisos.FindLastIndex(x => x.Value == a);
                if (i >= 0 && avisos[i].Key >= dia - 1 && i >= avisos.Count - 6) avisos[i] = new KeyValuePair<int, string>(dia, a);
                else avisos.Add(new KeyValuePair<int, string>(dia, a));
            }
        }

        /// <summary>Fecha prevista de cosecha con los grados-dia normales de La Alberca a partir de hoy (mas el desfase de temperatura del perfil).</summary>
        public int CosechaPrevista()
        {
            var m = modelo; var b = SimuladorHuerto.Base;
            if (b == null || m.cultivo == null || !(m.Viva || m.situacion == Situacion.Preparada)) return -1;
            float falta = (1f - m.desarrollo) * m.cultivo.gddTotal;
            int d = Mathf.Max(diaHoy, m.diaSiembra);
            while (falta > 0f && d < 365)
            {
                d++;
                var n = b.normal[d];
                float tmax = Mathf.Min(n.tmax + clima.deltaT - 2.5f * clima.sombra, m.cultivo.tSup), tmin = Mathf.Max(n.tmin + clima.deltaT, m.cultivo.tBase);
                falta -= Mathf.Max(0f, (tmax + Mathf.Min(tmin, tmax)) * 0.5f - m.cultivo.tBase);
            }
            return falta > 0f ? -1 : d;
        }

        // Marco de plantacion: filas a lo largo del bancal
        void Colocar()
        {
            plantas.Clear();
            var c = Cultivo;
            var r = new System.Random(numero * 977 + c.id.GetHashCode());
            int filas = Mathf.Max(1, Mathf.FloorToInt((tam.x - 0.15f) / c.marcoFila + 0.01f));
            int porFila = Mathf.Max(1, Mathf.FloorToInt((tam.y - 0.15f) / c.marcoPlanta + 0.01f));
            for (int f = 0; f < filas; f++)
                for (int k = 0; k < porFila; k++)
                {
                    float x = (f - (filas - 1) * 0.5f) * c.marcoFila + (float)(r.NextDouble() - 0.5) * 0.04f;
                    float z = (k - (porFila - 1) * 0.5f) * c.marcoPlanta + (float)(r.NextDouble() - 0.5) * 0.04f;
                    // alguna marra (planta que no prendio)
                    if (r.NextDouble() < 0.04) continue;
                    var mundo = transform.TransformPoint(new Vector3(x, 0, z));
                    if (terreno != null) mundo.y = terreno.SampleHeight(mundo) + terreno.transform.position.y + 0.03f;
                    plantas.Add(new Planta { pos = transform.InverseTransformPoint(mundo), giro = (float)r.NextDouble() * 360f, escala = 0.85f + (float)r.NextDouble() * 0.3f });
                }
            etapaHecha = -2;
        }

        void OnDisable()
        {
            if (plantasGo != null) DestroyImmediate(plantasGo);
            if (mallaPlantas != null) DestroyImmediate(mallaPlantas);
        }

        public void ActualizarVisual(bool forzar = false)
        {
            var m = modelo;
            if (m.cultivo == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            // suelo: mas oscuro cuanto mas humedo
            if (suelo != null)
            {
                mpb.Clear();
                mpb.SetColor("_BaseColor", Color.Lerp(new Color(0.95f, 0.92f, 0.88f), new Color(0.52f, 0.47f, 0.43f), Mathf.Pow(m.Humedad, 1.4f)));
                suelo.SetPropertyBlock(mpb);
            }

            // lluvia local
            if (lluvia != null && Application.isPlaying)
            {
                var em = lluvia.emission;
                bool llueve = Hoy.lluvia > 0.5f && SimuladorHuerto.Instancia != null && SimuladorHuerto.Instancia.velocidad <= 3f && !SimuladorHuerto.Instancia.pausado;
                em.rateOverTime = llueve ? Mathf.Clamp(120f + Hoy.lluvia * 30f, 120f, 1200f) : 0f;
                if (llueve && !lluvia.isPlaying) lluvia.Play();
            }

            int etapa = m.Etapa;
            float rel = m.desarrollo > 0.05f ? Mathf.Clamp01(m.vigor / m.desarrollo) : 1f;
            float tamano = etapa < 0 ? 0f : (0.3f + 0.7f * Mathf.Sqrt(Mathf.Clamp01(m.desarrollo / 0.75f))) * (0.75f + 0.25f * rel);
            tamano = Mathf.Round(tamano * 25f) / 25f;
            bool cambia = etapa != etapaHecha || Mathf.Abs(tamano - tamHecho) > 0.001f || cultivoHecho != m.cultivo.id;
            if (cambia && (forzar || !Application.isPlaying || Time.unscaledTime - ultimaVez > 0.15f))
            {
                Construir(etapa, tamano);
                ultimaVez = Time.unscaledTime;
            }

            // color de las hojas: amarillean con la falta de salud y al secarse la mata
            if (plantasGo != null)
            {
                var r = plantasGo.GetComponent<MeshRenderer>();
                var mats = r.sharedMaterials;
                bool senescente = etapa == 4 && (m.cultivo.id == "Patata" || m.cultivo.id == "Cebolla");
                for (int i = 0; i < 2 && i < mats.Length; i++)
                {
                    Color b = mats[i].GetColor("_BaseColor");
                    Color amarillo = new Color(0.78f, 0.66f, 0.3f), seco = new Color(0.45f, 0.33f, 0.2f);
                    Color col = Color.Lerp(b, amarillo, (1f - m.salud / 100f) * 0.85f + (senescente ? 0.6f : 0f));
                    if (m.situacion == Situacion.Muerta) col = seco;
                    mpb.Clear(); mpb.SetColor("_BaseColor", col);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        void Construir(int etapa, float tamano)
        {
            etapaHecha = etapa; tamHecho = tamano; cultivoHecho = modelo.cultivo.id;
            var mod = SimuladorHuerto.Instancia != null ? SimuladorHuerto.Instancia.Modelo(modelo.cultivo.id) : null;
            if (etapa < 0 || mod == null || etapa >= mod.etapas.Length || mod.etapas[etapa] == null || plantas.Count == 0)
            {
                if (plantasGo != null) plantasGo.SetActive(false);
                return;
            }
            if (plantasGo == null)
            {
                plantasGo = new GameObject("Plantas") { hideFlags = HideFlags.HideAndDontSave };
                plantasGo.transform.SetParent(transform, false);
                plantasGo.AddComponent<MeshFilter>();
                plantasGo.AddComponent<MeshRenderer>();
                mallaPlantas = new Mesh { name = "Plantas_" + nombre, hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                plantasGo.GetComponent<MeshFilter>().sharedMesh = mallaPlantas;
            }
            plantasGo.SetActive(true);
            var fuente = mod.etapas[etapa];
            var fv = fuente.vertices; var fn = fuente.normals; var ft = fuente.tangents; var fu = fuente.uv;
            int sub = fuente.subMeshCount;
            var ftri = new int[sub][];
            for (int s = 0; s < sub; s++) ftri[s] = fuente.GetTriangles(s);
            var v = new List<Vector3>(fv.Length * plantas.Count); var n = new List<Vector3>(v.Capacity); var t = new List<Vector4>(v.Capacity); var u = new List<Vector2>(v.Capacity);
            var tris = new List<int>[sub];
            for (int s = 0; s < sub; s++) tris[s] = new List<int>(ftri[s].Length * plantas.Count);
            foreach (var p in plantas)
            {
                var mat = Matrix4x4.TRS(p.pos, Quaternion.Euler(0, p.giro, 0), Vector3.one * (tamano * p.escala));
                int b = v.Count;
                for (int i = 0; i < fv.Length; i++)
                {
                    v.Add(mat.MultiplyPoint3x4(fv[i])); n.Add(mat.MultiplyVector(fn[i]).normalized); u.Add(fu[i]);
                    Vector3 tg = mat.MultiplyVector(ft[i]).normalized; t.Add(new Vector4(tg.x, tg.y, tg.z, ft[i].w));
                }
                for (int s = 0; s < sub; s++) foreach (int i in ftri[s]) tris[s].Add(b + i);
            }
            mallaPlantas.Clear();
            mallaPlantas.SetVertices(v); mallaPlantas.SetNormals(n); mallaPlantas.SetTangents(t); mallaPlantas.SetUVs(0, u);
            mallaPlantas.subMeshCount = sub;
            for (int s = 0; s < sub; s++) mallaPlantas.SetTriangles(tris[s], s);
            mallaPlantas.RecalculateBounds();
            var r = plantasGo.GetComponent<MeshRenderer>();
            var mats = new Material[sub];
            for (int s = 0; s < sub; s++) mats[s] = mod.materiales[Mathf.Min(s, mod.materiales.Length - 1)];
            r.sharedMaterials = mats;
        }
    }
}
