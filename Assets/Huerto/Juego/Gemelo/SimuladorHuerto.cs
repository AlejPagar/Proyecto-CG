using UnityEngine;

namespace Huerto.Gemelo
{
    /// <summary>
    /// Reloj de la temporada del gemelo digital: avanza los dias y hace dar un paso a cada bancal con su propio clima.
    /// En el editor (sin jugar) simula hasta 'diaVistaPrevia' para que la escena se vea con las plantas a media temporada.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public class SimuladorHuerto : MonoBehaviour
    {
        public static SimuladorHuerto Instancia;

        public TextAsset datosMeteo;
        public ParcelaCultivo[] parcelas;
        public ModeloPlanta[] modelos;

        [Header("Temporada (dia del ano, 0 = 1 de enero)")]
        public int diaInicio = 75;          // 16 de marzo
        public int diaFin = 333;            // 30 de noviembre
        public int diaVistaPrevia = 200;    // 20 de julio: lo que se ve en el editor
        [Header("Velocidad (dias simulados por segundo)")]
        public float velocidad = 0.5f;
        public bool pausado;

        public int Dia { get; private set; }
        public event System.Action DiaAvanzado;
        /// <summary>Cada aviso nuevo de un bancal (helada, lista para cosechar...), para el sonido.</summary>
        public event System.Action<ParcelaCultivo, string> Alerta;
        float acumulado;

        static BaseMeteo baseMeteo;
        public static BaseMeteo Base
        {
            get
            {
                if (baseMeteo == null && Instancia != null && Instancia.datosMeteo != null) baseMeteo = BaseMeteo.Cargar(Instancia.datosMeteo.text);
                return baseMeteo;
            }
        }

        static readonly System.Globalization.CultureInfo es = new System.Globalization.CultureInfo("es-ES");
        public static string Fecha(int dia) => new System.DateTime(2021, 1, 1).AddDays(Mathf.Clamp(dia, 0, 364)).ToString("d 'de' MMMM", es);
        public static string FechaCorta(int dia) => new System.DateTime(2021, 1, 1).AddDays(Mathf.Clamp(dia, 0, 364)).ToString("d MMM", es);

        public ModeloPlanta Modelo(string id)
        {
            if (modelos != null) foreach (var m in modelos) if (m.cultivo == id) return m;
            return null;
        }

        void OnEnable()
        {
            Instancia = this;
            if (parcelas == null || parcelas.Length == 0) return;   // recien anadido, aun sin bancales
            Reiniciar();
            if (!Application.isPlaying) Saltar(diaVistaPrevia - diaInicio);
        }

        void Update()
        {
            if (!Application.isPlaying || pausado || Dia >= diaFin || parcelas == null) return;
            acumulado += Time.deltaTime * velocidad;
            int pasos = 0;
            while (acumulado >= 1f && pasos < 40 && Dia < diaFin) { acumulado -= 1f; Paso(); pasos++; }
            if (pasos > 0) foreach (var p in parcelas) p.ActualizarVisual();
            if (Dia >= diaFin) pausado = true;
        }

        void Paso()
        {
            Dia++;
            foreach (var p in parcelas)
            {
                p.Avanzar(Dia);
                if (Alerta != null) foreach (var a in p.modelo.alertas) Alerta(p, a);
            }
            DiaAvanzado?.Invoke();
        }

        /// <summary>Avanza n dias de golpe (sin esperar al reloj).</summary>
        public void Saltar(int n)
        {
            if (parcelas == null) return;
            for (int i = 0; i < n && Dia < diaFin; i++) Paso();
            foreach (var p in parcelas) p.ActualizarVisual(true);
        }

        public void Reiniciar()
        {
            Dia = diaInicio; acumulado = 0f;
            if (parcelas == null) return;
            foreach (var p in parcelas) if (p != null) p.Reiniciar(diaInicio);
            foreach (var p in parcelas) if (p != null) p.ActualizarVisual(true);
            DiaAvanzado?.Invoke();
        }
    }
}
