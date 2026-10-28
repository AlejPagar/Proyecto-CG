using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Huerto.Gemelo
{
    public struct DiaMeteo
    {
        public float tmax, tmin, lluvia, et0, rad, hr;
        public float TMedia => (tmax + tmin) * 0.5f;
    }

    /// <summary>
    /// Serie diaria real del huerto (Open-Meteo, reanalisis ERA5, 1996-2025) y sus normales,
    /// con las que tambien se generan anios sinteticos con la estadistica de esos 30 anios.
    /// </summary>
    public class BaseMeteo
    {
        public readonly Dictionary<int, DiaMeteo[]> anios = new Dictionary<int, DiaMeteo[]>();
        public int primerAnio = int.MaxValue, ultimoAnio = int.MinValue;
        // normales por dia del anio (media movil de 31 dias) y lluvia por mes (cadena de Markov humedo/seco)
        public readonly DiaMeteo[] normal = new DiaMeteo[366];
        public readonly float[] pHumedoTrasSeco = new float[12], pHumedoTrasHumedo = new float[12], lluviaDiaHumedo = new float[12];

        public static BaseMeteo Cargar(string csv)
        {
            var b = new BaseMeteo();
            var inv = CultureInfo.InvariantCulture;
            foreach (var linea in csv.Split('\n'))
            {
                if (linea.Length < 10 || linea[0] == '#' || !char.IsDigit(linea[0])) continue;
                var c = linea.Trim().Split(';');
                int anio = int.Parse(c[0].Substring(0, 4)), mes = int.Parse(c[0].Substring(5, 2)), dia = int.Parse(c[0].Substring(8, 2));
                int doy = new System.DateTime(anio, mes, dia).DayOfYear - 1;
                if (!b.anios.TryGetValue(anio, out var serie)) b.anios[anio] = serie = new DiaMeteo[366];
                serie[doy] = new DiaMeteo
                {
                    tmax = float.Parse(c[1], inv), tmin = float.Parse(c[2], inv), lluvia = float.Parse(c[3], inv),
                    et0 = float.Parse(c[4], inv), rad = float.Parse(c[5], inv), hr = float.Parse(c[6], inv),
                };
                b.primerAnio = Mathf.Min(b.primerAnio, anio); b.ultimoAnio = Mathf.Max(b.ultimoAnio, anio);
            }
            // los anios no bisiestos repiten el ultimo dia
            foreach (var kv in b.anios) if (!System.DateTime.IsLeapYear(kv.Key)) kv.Value[365] = kv.Value[364];
            b.Normales();
            return b;
        }

        void Normales()
        {
            var suma = new double[366, 5]; int n = anios.Count;
            int[] secos = new int[12], secoHumedo = new int[12], humedos = new int[12], humedoHumedo = new int[12]; double[] mmHumedo = new double[12];
            foreach (var kv in anios)
            {
                var s = kv.Value; bool previo = false;
                for (int d = 0; d < 366; d++)
                {
                    suma[d, 0] += s[d].tmax; suma[d, 1] += s[d].tmin; suma[d, 2] += s[d].et0; suma[d, 3] += s[d].rad; suma[d, 4] += s[d].hr;
                    int mes = new System.DateTime(2021, 1, 1).AddDays(Mathf.Min(d, 364)).Month - 1;
                    bool humedo = s[d].lluvia >= 1f;
                    if (previo) { humedos[mes]++; if (humedo) humedoHumedo[mes]++; } else { secos[mes]++; if (humedo) secoHumedo[mes]++; }
                    if (humedo) mmHumedo[mes] += s[d].lluvia;
                    previo = humedo;
                }
            }
            for (int d = 0; d < 366; d++)
            {
                var acc = new double[5]; int k = 0;
                for (int w = -15; w <= 15; w++) { int dd = (d + w + 366) % 366; for (int j = 0; j < 5; j++) acc[j] += suma[dd, j]; k++; }
                normal[d] = new DiaMeteo { tmax = (float)(acc[0] / k / n), tmin = (float)(acc[1] / k / n), et0 = (float)(acc[2] / k / n), rad = (float)(acc[3] / k / n), hr = (float)(acc[4] / k / n) };
            }
            for (int m = 0; m < 12; m++)
            {
                pHumedoTrasSeco[m] = secos[m] > 0 ? secoHumedo[m] / (float)secos[m] : 0.2f;
                pHumedoTrasHumedo[m] = humedos[m] > 0 ? humedoHumedo[m] / (float)humedos[m] : 0.5f;
                int diasHumedos = secoHumedo[m] + humedoHumedo[m];
                lluviaDiaHumedo[m] = diasHumedos > 0 ? (float)(mmHumedo[m] / diasHumedos) : 5f;
            }
        }
    }

    /// <summary>
    /// Climatologia propia de una parcela: un anio real (o uno sintetico) y modificadores encima:
    /// temperatura, lluvia, sombra de los arboles y episodios extremos (ola de calor, helada tardia, sequia de verano).
    /// </summary>
    [System.Serializable]
    public class PerfilClima
    {
        public string nombre = "Año típico (2021)";
        public int anio = 2021;              // 0 = sintetico con la estadistica de 1996-2025
        public int semilla = 1;
        public float deltaT;                // grados que se suman a todo el anio
        public float factorLluvia = 1f;
        [Range(0, 0.9f)] public float sombra;
        public bool olaCalor, heladaTardia, sequia;

        public static readonly string[] Presets =
        {
            "Año típico (2021)", "Año seco (2005)", "Año lluvioso (2025)", "Verano tórrido (2022)", "Helada tardía en mayo",
            "Ola de calor en julio", "Cambio climático (+2 °C)", "Sombra de los árboles", "Sintético (estadística 1996-2025)",
        };

        public static PerfilClima Preset(string nombre, int semilla = 1)
        {
            var p = new PerfilClima { nombre = nombre, semilla = semilla };
            switch (System.Array.IndexOf(Presets, nombre))
            {
                case 1: p.anio = 2005; break;
                case 2: p.anio = 2025; break;
                case 3: p.anio = 2022; break;
                case 4: p.heladaTardia = true; break;
                case 5: p.olaCalor = true; break;
                case 6: p.deltaT = 2f; p.factorLluvia = 0.85f; break;
                case 7: p.sombra = 0.5f; break;
                case 8: p.anio = 0; break;
            }
            return p;
        }

        public PerfilClima Copia() => (PerfilClima)MemberwiseClone();

        static float Hargreaves(float tmax, float tmin) => Mathf.Max(0.1f, ((tmax + tmin) * 0.5f + 17.8f) * Mathf.Sqrt(Mathf.Max(0.5f, tmax - tmin)));

        /// <summary>Los 366 dias del anio con todos los modificadores aplicados.</summary>
        public DiaMeteo[] Serie(BaseMeteo b)
        {
            DiaMeteo[] s;
            if (anio > 0 && b.anios.TryGetValue(anio, out var real)) s = (DiaMeteo[])real.Clone();
            else s = Sintetico(b);
            for (int d = 0; d < s.Length; d++)
            {
                var m = s[d];
                float h0 = Hargreaves(m.tmax, m.tmin);
                m.tmax += deltaT; m.tmin += deltaT;
                if (olaCalor && d >= 189 && d <= 202)
                {
                    float env = Mathf.Sin((d - 188) / 15f * Mathf.PI);
                    m.tmax += 7f * env; m.tmin += 4.5f * env; m.lluvia = 0f; m.hr = Mathf.Max(15f, m.hr - 15f * env);
                }
                if (heladaTardia && d >= 137 && d <= 141)
                {
                    float f = 1f - Mathf.Abs(d - 139) / 3f;
                    m.tmin = Mathf.Min(m.tmin, Mathf.Lerp(m.tmin, -4f, f)); m.tmax = Mathf.Min(m.tmax, Mathf.Lerp(m.tmax, 11f, f));
                }
                if (sequia && d >= 151 && d <= 243) { m.lluvia = 0f; m.hr = Mathf.Max(18f, m.hr - 12f); }
                m.lluvia *= factorLluvia;
                // bajo la copa de un arbol: menos sol, maximas mas bajas y menos evaporacion
                m.tmax -= 2.5f * sombra; m.rad *= 1f - sombra; m.hr = Mathf.Min(100f, m.hr + 6f * sombra);
                m.et0 *= Hargreaves(m.tmax, m.tmin) / h0 * (1f - 0.6f * sombra);
                s[d] = m;
            }
            return s;
        }

        DiaMeteo[] Sintetico(BaseMeteo b)
        {
            var r = new System.Random(semilla * 7907 + 3);
            float Normal() { double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble(); return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2)); }
            var s = new DiaMeteo[366];
            float anomalia = 0f; bool humedo = false;
            for (int d = 0; d < 366; d++)
            {
                var n = b.normal[d];
                int mes = new System.DateTime(2021, 1, 1).AddDays(Mathf.Min(d, 364)).Month - 1;
                humedo = r.NextDouble() < (humedo ? b.pHumedoTrasHumedo[mes] : b.pHumedoTrasSeco[mes]);
                anomalia = 0.7f * anomalia + 2.2f * Normal();
                var m = new DiaMeteo
                {
                    tmax = n.tmax + anomalia + (humedo ? -2.2f : 0.6f),
                    tmin = n.tmin + 0.6f * anomalia + 1.2f * Normal() + (humedo ? 0.8f : -0.2f),
                    lluvia = humedo ? -b.lluviaDiaHumedo[mes] * Mathf.Log(1f - (float)r.NextDouble() * 0.999f) : 0f,
                    rad = n.rad * (humedo ? 0.55f : 1.08f),
                    hr = Mathf.Clamp(n.hr + (humedo ? 14f : -4f) + 4f * Normal(), 12f, 100f),
                };
                m.tmin = Mathf.Min(m.tmin, m.tmax - 1f);
                m.et0 = n.et0 * (humedo ? 0.6f : 1.08f) * Hargreaves(m.tmax, m.tmin) / Hargreaves(n.tmax, n.tmin);
                s[d] = m;
            }
            return s;
        }
    }
}
