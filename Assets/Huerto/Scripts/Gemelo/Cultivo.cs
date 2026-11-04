using System.Collections.Generic;

namespace Huerto.Gemelo
{
    /// <summary>Fase de desarrollo: empieza al alcanzar la fraccion 'inicio' de los grados-dia totales del cultivo.</summary>
    public class Fase
    {
        public string nombre; public float inicio; public int etapa;   // etapa visual: 0 plantula, 1 vegetativo, 2 flor, 3 fruto verde, 4 maduro
        public Fase(string n, float i, int e) { nombre = n; inicio = i; etapa = e; }
    }

    /// <summary>
    /// Parametros de un cultivo de huerto de montana. Los grados-dia de cada ciclo estan calibrados con 30 anos de datos
    /// diarios en el huerto (1996-2025): son la media entre la fecha habitual de siembra o trasplante en la sierra y la de cosecha.
    /// Coeficientes de cultivo (Kc), profundidad de raices y fraccion de agotamiento (p) de FAO-56.
    /// </summary>
    public class Cultivo
    {
        public string id, nombre, variedad;
        public float tBase, tSup;                 // grados-dia: temperatura base y tope
        public float gddTotal;                    // de la siembra/trasplante a la cosecha
        public float tHelada, tMuerte, tCalor;    // dano por helada, muerte por helada, estres por calor en flor
        public float kcIni, kcMed, kcFin, raizMax, p;
        public float rendimiento;                 // kg/m2 en un huerto familiar al aire libre
        public float marcoFila, marcoPlanta;      // m entre filas y entre plantas
        public int diaSiembra;                    // dia del ano habitual en La Alberca
        public string enfermedad;                 // null, "mildiu" u "oidio"
        public float sombraTolerada = 0.15f;
        public bool espiga;                       // sube a flor con calor (lechuga)
        public int diasEnCampo = 20;              // dias que aguanta lista sin perder cosecha
        public bool Sembrado => fases[0].nombre.StartsWith("Germ") || fases[0].nombre.StartsWith("Brot");   // semilla o tuberculo bajo tierra al principio
        public Fase[] fases;

        public int FaseDe(float desarrollo)
        {
            int f = 0;
            for (int i = 0; i < fases.Length; i++) if (desarrollo >= fases[i].inicio) f = i;
            return f;
        }

        public float Kc(float d)
        {
            if (d < 0.15f) return kcIni;
            if (d < 0.45f) return UnityEngine.Mathf.Lerp(kcIni, kcMed, (d - 0.15f) / 0.3f);
            if (d < 0.8f) return kcMed;
            return UnityEngine.Mathf.Lerp(kcMed, kcFin, (d - 0.8f) / 0.2f);
        }

        // ---------------- Catalogo ----------------
        public static readonly Cultivo[] Todos =
        {
            new Cultivo { id = "Tomate", nombre = "Tomate", variedad = "Tomate de huerta, variedad tradicional de la sierra", tBase = 10, tSup = 30, gddTotal = 790,
                tHelada = 0.5f, tMuerte = -1.5f, tCalor = 33, kcIni = 0.6f, kcMed = 1.15f, kcFin = 0.8f, raizMax = 1.0f, p = 0.4f, rendimiento = 6f,
                marcoFila = 1.0f, marcoPlanta = 0.5f, diaSiembra = 140, enfermedad = "mildiu",
                fases = new[] { new Fase("Trasplante y arraigo", 0, 0), new Fase("Crecimiento vegetativo", 0.08f, 1), new Fase("Floración", 0.35f, 2), new Fase("Cuajado y engorde", 0.55f, 3), new Fase("Maduración", 0.85f, 4), new Fase("Lista para cosechar", 1f, 4) } },
            new Cultivo { id = "Pimiento", nombre = "Pimiento", variedad = "Pimiento morron", tBase = 10, tSup = 30, gddTotal = 900,
                tHelada = 1f, tMuerte = -1f, tCalor = 33, kcIni = 0.6f, kcMed = 1.05f, kcFin = 0.9f, raizMax = 0.7f, p = 0.3f, rendimiento = 3f,
                marcoFila = 0.7f, marcoPlanta = 0.4f, diaSiembra = 145,
                fases = new[] { new Fase("Trasplante y arraigo", 0, 0), new Fase("Crecimiento vegetativo", 0.08f, 1), new Fase("Floración", 0.35f, 2), new Fase("Cuajado y engorde", 0.55f, 3), new Fase("Maduración", 0.85f, 4), new Fase("Lista para cosechar", 1f, 4) } },
            new Cultivo { id = "Judia", nombre = "Judía verde", variedad = "Judía de enrame, en cañas", tBase = 10, tSup = 30, gddTotal = 600,
                tHelada = 1f, tMuerte = -1f, tCalor = 32, kcIni = 0.5f, kcMed = 1.05f, kcFin = 0.9f, raizMax = 0.6f, p = 0.45f, rendimiento = 2f,
                marcoFila = 0.8f, marcoPlanta = 0.35f, diaSiembra = 135, sombraTolerada = 0.2f, diasEnCampo = 12,
                fases = new[] { new Fase("Germinación", 0, 0), new Fase("Trepando por las cañas", 0.15f, 1), new Fase("Floración", 0.55f, 2), new Fase("Formación de vainas", 0.75f, 3), new Fase("Lista para cosechar", 1f, 3) } },
            new Cultivo { id = "Patata", nombre = "Patata", variedad = "Patata de media estación", tBase = 7, tSup = 28, gddTotal = 1255,
                tHelada = -0.5f, tMuerte = -6f, tCalor = 32, kcIni = 0.5f, kcMed = 1.15f, kcFin = 0.75f, raizMax = 0.5f, p = 0.35f, rendimiento = 3.5f,
                marcoFila = 0.7f, marcoPlanta = 0.35f, diaSiembra = 100, enfermedad = "mildiu", diasEnCampo = 30,
                fases = new[] { new Fase("Brotación", 0, 0), new Fase("Crecimiento de las matas", 0.15f, 1), new Fase("Floración y tuberización", 0.45f, 2), new Fase("Engorde de tubérculos", 0.6f, 2), new Fase("Senescencia de la mata", 0.85f, 4), new Fase("Lista para cosechar", 1f, 4) } },
            new Cultivo { id = "Lechuga", nombre = "Lechuga", variedad = "Lechuga romana", tBase = 4, tSup = 26, gddTotal = 500,
                tHelada = -3f, tMuerte = -6f, tCalor = 27, kcIni = 0.7f, kcMed = 1.0f, kcFin = 0.95f, raizMax = 0.3f, p = 0.3f, rendimiento = 3f,
                marcoFila = 0.35f, marcoPlanta = 0.3f, diaSiembra = 105, sombraTolerada = 0.45f, espiga = true, diasEnCampo = 8,
                fases = new[] { new Fase("Trasplante y arraigo", 0, 0), new Fase("Roseta de hojas", 0.15f, 1), new Fase("Acogollado", 0.55f, 2), new Fase("Lista para cosechar", 1f, 3) } },
            new Cultivo { id = "Cebolla", nombre = "Cebolla", variedad = "Cebolla dulce de día largo", tBase = 4, tSup = 28, gddTotal = 1610,
                tHelada = -5f, tMuerte = -10f, tCalor = 35, kcIni = 0.7f, kcMed = 1.05f, kcFin = 0.75f, raizMax = 0.4f, p = 0.3f, rendimiento = 4f,
                marcoFila = 0.3f, marcoPlanta = 0.12f, diaSiembra = 84, enfermedad = "mildiu", diasEnCampo = 30,
                fases = new[] { new Fase("Trasplante y arraigo", 0, 0), new Fase("Desarrollo de hojas", 0.12f, 1), new Fase("Bulbificación", 0.5f, 2), new Fase("Engorde del bulbo", 0.7f, 3), new Fase("Maduración (cae el cuello)", 0.9f, 4), new Fase("Lista para cosechar", 1f, 4) } },
            new Cultivo { id = "Calabaza", nombre = "Calabaza", variedad = "Calabaza de invierno", tBase = 10, tSup = 32, gddTotal = 1160,
                tHelada = 1f, tMuerte = -1f, tCalor = 35, kcIni = 0.5f, kcMed = 1.0f, kcFin = 0.8f, raizMax = 1.0f, p = 0.35f, rendimiento = 3.5f,
                marcoFila = 1.5f, marcoPlanta = 1.2f, diaSiembra = 140, enfermedad = "oidio", diasEnCampo = 40,
                fases = new[] { new Fase("Germinación", 0, 0), new Fase("Crecimiento de guías", 0.1f, 1), new Fase("Floración", 0.4f, 2), new Fase("Cuajado y engorde", 0.55f, 3), new Fase("Maduración", 0.8f, 4), new Fase("Lista para cosechar", 1f, 4) } },
            new Cultivo { id = "Berza", nombre = "Berza", variedad = "Berza de invierno", tBase = 5, tSup = 26, gddTotal = 1375,
                tHelada = -8f, tMuerte = -14f, tCalor = 30, kcIni = 0.7f, kcMed = 1.05f, kcFin = 0.95f, raizMax = 0.6f, p = 0.45f, rendimiento = 3f,
                marcoFila = 0.6f, marcoPlanta = 0.5f, diaSiembra = 196, sombraTolerada = 0.3f, diasEnCampo = 40,
                fases = new[] { new Fase("Trasplante y arraigo", 0, 0), new Fase("Desarrollo de hojas", 0.12f, 1), new Fase("Formación del cogollo", 0.5f, 2), new Fase("Lista para cosechar", 1f, 3) } },
        };

        static Dictionary<string, Cultivo> porId;
        public static Cultivo Get(string id)
        {
            if (porId == null) { porId = new Dictionary<string, Cultivo>(); foreach (var c in Todos) porId[c.id] = c; }
            return porId.TryGetValue(id, out var r) ? r : Todos[0];
        }
    }
}
