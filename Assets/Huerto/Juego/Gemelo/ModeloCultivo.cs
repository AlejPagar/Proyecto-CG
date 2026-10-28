using System.Collections.Generic;
using UnityEngine;

namespace Huerto.Gemelo
{
    public enum Situacion { Preparada, Creciendo, Lista, Cosechada, Muerta }

    [System.Serializable]
    public struct Registro
    {
        public int dia;
        public float tmax, tmin, lluvia, riego, et, humedad, salud, desarrollo, vigor;
        public int fase;
        public Situacion situacion;
    }

    /// <summary>
    /// Modelo diario de un bancal de cultivo:
    ///  - desarrollo por grados-dia (temperatura media acotada entre la base y el tope del cultivo),
    ///  - balance de agua del suelo en la zona de raices (FAO-56: Kc, agua util, coeficiente de estres Ks),
    ///  - vigor (biomasa relativa) que crece con el desarrollo si hay agua, luz y salud,
    ///  - salud que baja con heladas, calor, sed, encharcamiento y enfermedades (mildiu, oidio) y se recupera despacio.
    /// Suelo de jabre (arena granitica con algo de limo): unos 120 mm de agua util por metro de profundidad.
    /// </summary>
    public class ModeloCultivo
    {
        const float AguaUtilPorMetro = 120f;

        public Cultivo cultivo;
        public float area;
        public bool riegoAuto = true, cosechaAuto = true;
        public int diaSiembra;

        public Situacion situacion;
        public float gdd, desarrollo, vigor, salud = 100f, cuajado = 1f, perdida, agotamiento;
        public float riegoTotal, lluviaTotal, etTotal, cosechaKg;
        public int diaCosecha = -1, diasLista, diasEncharcado, diasCalor;
        public float riesgoEnfermedad;
        public bool espigada;
        public readonly List<string> alertas = new List<string>();
        public readonly List<Registro> historia = new List<Registro>();
        float riegoPendiente;

        public float Raiz => situacion == Situacion.Creciendo || situacion == Situacion.Lista ? Mathf.Lerp(0.15f, cultivo.raizMax, Mathf.Clamp01(desarrollo / 0.5f)) : 0.3f;
        public float AguaUtil => AguaUtilPorMetro * Raiz;
        public float Humedad => Mathf.Clamp01(1f - agotamiento / AguaUtil);
        public int Fase => cultivo.FaseDe(desarrollo);
        public bool Viva => situacion == Situacion.Creciendo || situacion == Situacion.Lista;

        /// <summary>Etapa visual de las plantas (-1 = no hay plantas).</summary>
        public int Etapa
        {
            get
            {
                if (situacion == Situacion.Preparada || situacion == Situacion.Cosechada) return -1;
                if (espigada) return 4;
                return cultivo.fases[Fase].etapa;
            }
        }

        public float CosechaEstimada
        {
            get
            {
                if (situacion == Situacion.Cosechada) return cosechaKg;
                if (situacion == Situacion.Muerta) return 0f;
                float relativo = desarrollo > 0.05f ? Mathf.Clamp01(vigor / desarrollo) : 1f;
                return cultivo.rendimiento * area * relativo * cuajado * Mathf.Sqrt(salud / 100f) * (1f - perdida) * (espigada ? 0.4f : 1f);
            }
        }

        public void Reiniciar(Cultivo c, int siembra, float areaM2)
        {
            cultivo = c; diaSiembra = siembra; area = areaM2;
            situacion = Situacion.Preparada;
            gdd = desarrollo = vigor = perdida = riegoTotal = lluviaTotal = etTotal = cosechaKg = riesgoEnfermedad = riegoPendiente = 0f;
            salud = 100f; cuajado = 1f; agotamiento = AguaUtil * 0.3f;
            diaCosecha = -1; diasLista = diasEncharcado = diasCalor = 0; espigada = false;
            alertas.Clear(); historia.Clear();
        }

        public void Regar(float mm) => riegoPendiente += mm;

        public float Cosechar(int dia)
        {
            if (!Viva || desarrollo < 0.5f) return 0f;
            cosechaKg = CosechaEstimada * Mathf.Clamp01((desarrollo - 0.5f) / 0.5f);
            situacion = Situacion.Cosechada; diaCosecha = dia;
            return cosechaKg;
        }

        public void Paso(int dia, DiaMeteo m)
        {
            alertas.Clear();
            var c = cultivo;
            if (situacion == Situacion.Preparada && dia >= diaSiembra) { situacion = Situacion.Creciendo; alertas.Add(c.fases[0].nombre.StartsWith("Germ") || c.fases[0].nombre.StartsWith("Brot") ? $"Sembrado: {c.nombre.ToLower()}" : $"Trasplantado: {c.nombre.ToLower()}"); }

            // ---- desarrollo (grados-dia) ----
            float dGdd = 0f;
            if (Viva && desarrollo < 1f)
            {
                float tmax = Mathf.Min(m.tmax, c.tSup), tmin = Mathf.Max(m.tmin, c.tBase);
                dGdd = Mathf.Max(0f, (tmax + Mathf.Min(tmin, tmax)) * 0.5f - c.tBase);
                gdd += dGdd;
                desarrollo = Mathf.Min(1f, gdd / c.gddTotal);
            }

            // ---- agua del suelo ----
            float taw = AguaUtil, raw = c.p * taw;
            float lluviaEf = m.lluvia > 40f ? 40f + (m.lluvia - 40f) * 0.5f : m.lluvia;   // escorrentia en los aguaceros
            float riego = riegoPendiente; riegoPendiente = 0f;
            if (riegoAuto && Viva && agotamiento > 0.5f * raw) riego += agotamiento;     // goteo: devuelve el suelo a capacidad de campo
            float kc = Viva ? c.Kc(desarrollo) : 0.3f;                                   // suelo desnudo: solo evaporacion
            float ks = agotamiento <= raw ? 1f : Mathf.Clamp01((taw - agotamiento) / ((1f - c.p) * taw));
            float et = kc * m.et0 * (Viva ? ks : Mathf.Clamp01(1f - agotamiento / taw));
            float balance = agotamiento - lluviaEf - riego + et;
            float exceso = Mathf.Max(0f, -balance);
            agotamiento = Mathf.Clamp(balance, 0f, taw);
            riegoTotal += riego; lluviaTotal += m.lluvia; etTotal += et;

            if (Viva)
            {
                float danoHoy = 0f;
                // helada
                // las semillas y los tuberculos aun sin brotar estan protegidos bajo tierra
                bool bajoTierra = c.Sembrado && desarrollo < c.fases[1].inicio * 0.6f;
                if (m.tmin < c.tHelada && !bajoTierra)
                {
                    if (m.tmin <= c.tMuerte) { danoHoy = 100f; alertas.Add($"Helada letal: {m.tmin:0.0} °C"); }
                    else { danoHoy += (c.tHelada - m.tmin) * 20f; alertas.Add($"Helada: {m.tmin:0.0} °C"); }
                }
                // calor en floracion y cuajado: se caen las flores
                int etapa = c.fases[Fase].etapa;
                if (m.tmax > c.tCalor && (etapa == 2 || etapa == 3) && !c.espiga)
                {
                    cuajado = Mathf.Max(0.35f, cuajado * (1f - 0.03f * (m.tmax - c.tCalor)));
                    alertas.Add($"Calor en floración: {m.tmax:0} °C, caen flores");
                }
                if (m.tmax > c.tCalor + 5f) { danoHoy += 1.5f; alertas.Add("Calor extremo"); }
                // lechuga: con calor se espiga (sube a flor) y amarga
                if (c.espiga && desarrollo > 0.4f)
                {
                    diasCalor = m.tmax > c.tCalor ? diasCalor + 1 : Mathf.Max(0, diasCalor - 1);
                    if ((diasCalor >= 4 || diasLista > c.diasEnCampo) && !espigada) { espigada = true; alertas.Add("Se ha espigado (subida a flor)"); }
                }
                // sed
                if (ks < 0.5f) { danoHoy += (0.5f - ks) * 6f; alertas.Add("Estrés hídrico"); }
                else if (ks < 0.85f) alertas.Add("Falta de agua");
                // encharcamiento
                diasEncharcado = exceso > 15f ? diasEncharcado + 1 : Mathf.Max(0, diasEncharcado - 1);
                if (diasEncharcado >= 3) { danoHoy += c.id == "Patata" || c.id == "Cebolla" ? 4f : 2f; alertas.Add("Suelo encharcado"); }
                // enfermedades por hongos
                if (c.enfermedad == "mildiu")
                {
                    bool favorable = m.TMedia > 10f && m.TMedia < 25f && (m.lluvia >= 2f || m.hr >= 85f);
                    riesgoEnfermedad = favorable ? riesgoEnfermedad + 1f : Mathf.Max(0f, riesgoEnfermedad - 1f);
                    if (riesgoEnfermedad >= 3f) { danoHoy += 3f; alertas.Add("Infección de mildiu"); }
                    else if (riesgoEnfermedad >= 2f) alertas.Add("Riesgo de mildiu");
                }
                else if (c.enfermedad == "oidio")
                {
                    bool favorable = m.TMedia > 18f && m.TMedia < 28f && m.hr > 50f && m.hr < 85f && m.lluvia < 1f;
                    riesgoEnfermedad = favorable ? riesgoEnfermedad + 0.5f : Mathf.Max(0f, riesgoEnfermedad - 0.5f);
                    if (riesgoEnfermedad >= 5f) { danoHoy += 1.5f; alertas.Add("Oídio en las hojas"); }
                    else if (riesgoEnfermedad >= 3.5f) alertas.Add("Riesgo de oídio");
                }

                salud = danoHoy > 0f ? salud - danoHoy : Mathf.Min(100f, salud + 0.5f);
                if (salud <= 0f) { salud = 0f; situacion = Situacion.Muerta; alertas.Add("Las plantas han muerto"); }
            }
            if (Viva)
            {

                // vigor: crece con el desarrollo si hay agua, luz y salud
                float luz = 1f - 0.8f * Mathf.Max(0f, sombraHoy - c.sombraTolerada);
                vigor = Mathf.Min(desarrollo, vigor + dGdd / c.gddTotal * Mathf.Pow(ks, 0.8f) * luz * Mathf.Sqrt(salud / 100f));

                if (situacion != Situacion.Muerta && desarrollo >= 1f)
                {
                    if (situacion == Situacion.Creciendo) { situacion = Situacion.Lista; alertas.Add("Lista para cosechar"); }
                    diasLista++;
                    if (diasLista > c.diasEnCampo) { perdida = Mathf.Min(0.8f, perdida + 0.015f); alertas.Add("Se pasa: se pierde cosecha"); }
                    if (cosechaAuto && diasLista >= 3) { Cosechar(dia); alertas.Add($"Cosechados {cosechaKg:0.0} kg"); }
                }
            }

            historia.Add(new Registro
            {
                dia = dia, tmax = m.tmax, tmin = m.tmin, lluvia = m.lluvia, riego = riego, et = et, humedad = Humedad,
                salud = salud, desarrollo = desarrollo, vigor = vigor, fase = Viva || situacion == Situacion.Muerta ? Fase : -1, situacion = situacion,
            });
        }

        /// <summary>Fraccion de sombra del dia (la pone la parcela antes de cada paso).</summary>
        public float sombraHoy;
    }
}
