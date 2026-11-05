using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Huerto
{
    /// <summary>
    /// Disposicion del huerto (La Alberca) reconstruida a partir de las fotos
    /// del 2 de agosto (de la casa ladera arriba) y de la cartografia de Catastro.
    /// Coordenadas locales en metros: x = este, z = norte. El origen UTM no se publica.
    /// La altura de la escena es la altitud real menos 1040 m.
    /// </summary>
    public static class Datos
    {
        public const float Base = 1040f;
        // HDRI del cielo: el sol del HDRI esta en acimut 126 grados; se gira para llevarlo a 100 (sol del 2 de agosto, 10:56)
        public static float CieloGiro = 26f;
        public static float CieloExposicion = 1f;
        public const float Tam = 192f;

        // Contorno de la parcela
        public static readonly Vector2[] Parcela =
        {
            new Vector2(115.38f, 69.6f), new Vector2(113.54f, 66.94f), new Vector2(103.53f, 68.54f), new Vector2(93.12f, 69.75f),
            new Vector2(92.77f, 70.75f), new Vector2(91.17f, 76.29f), new Vector2(89.48f, 84.07f), new Vector2(87.73f, 95.33f),
            new Vector2(86.89f, 102.83f), new Vector2(86.51f, 106.2f), new Vector2(86.73f, 107.4f), new Vector2(87.96f, 114.33f),
            new Vector2(91.17f, 114.38f), new Vector2(91.72f, 117.42f), new Vector2(92.56f, 121.17f), new Vector2(98.1f, 120.38f),
            new Vector2(98.47f, 122.13f), new Vector2(99.08f, 125.03f), new Vector2(102.11f, 123.89f), new Vector2(100.61f, 119.96f),
            new Vector2(100.06f, 118.04f), new Vector2(99.86f, 111.39f), new Vector2(99.6f, 102.73f), new Vector2(99.54f, 100.74f),
            new Vector2(100.1f, 98.21f), new Vector2(106.09f, 89.5f), new Vector2(111.03f, 84.53f), new Vector2(115.82f, 78.92f),
            new Vector2(113.85f, 73.78f), new Vector2(116.36f, 71.02f),
        };

        // ---------------- Bancales medidos con el LiDAR PNOA 2025 ----------------
        // El relieve sale directamente de la nube LiDAR (Alturas_LiDAR_2025.bytes); los escalones detectados son:
        //  Muro0 z~100-101 (0,7 m) y Muro1 z~94-96 (1,1 m): cierran el pasillo (fotos 105548, 105557)
        //  Muro2 diagonal z 84->89 (1,2 m): fondo del bancal medio (fotos 105610, 105628)
        //  Muro3 z~72-73 (1,5 m): bancal alto junto al camino, con el deposito (fotos 105655, 105726)
        public static bool EnPasillo(float x, float z) => z >= 101.5f;

        // ---------------- Muros de piedra seca ----------------
        public class Muro
        {
            public string nombre; public Vector2[] puntos; public float hiedra; public float minAlto = 0.7f; public float grosor = 0.55f;
            public Vector2 hiedraZona = new Vector2(0f, 1f); // tramo (0..1) cubierto de hiedra
            public bool bancal = true;                      // muro de contencion entre bancales: afila el escalon del terreno
        }

        public static readonly Muro[] Muros =
        {
            new Muro { nombre = "Muro0_Pasillo", puntos = new[] { new Vector2(86.9f, 99.5f), new Vector2(93.0f, 100.3f), new Vector2(99.7f, 101.5f) }, hiedra = 0.5f, hiedraZona = new Vector2(0f, 0.45f) },
            new Muro { nombre = "Muro1_FinPasillo", puntos = new[] { new Vector2(87.9f, 94.0f), new Vector2(95.5f, 95.3f), new Vector2(101.9f, 95.7f) }, hiedra = 0.9f, hiedraZona = new Vector2(0.45f, 1f) },
            new Muro { nombre = "Muro2_Diagonal", puntos = new[] { new Vector2(89.6f, 83.6f), new Vector2(97.0f, 86.6f), new Vector2(105.9f, 89.6f) }, hiedra = 0.25f, hiedraZona = new Vector2(0.55f, 1f) },
            new Muro { nombre = "Muro3_BancalAlto", puntos = new[] { new Vector2(92.0f, 73.6f), new Vector2(99.0f, 73.0f), new Vector2(106.0f, 71.7f), new Vector2(115.0f, 72.4f) }, hiedra = 0.6f, hiedraZona = new Vector2(0f, 0.35f) },
            new Muro { nombre = "Lindero_Oeste", bancal = false, puntos = new[] { new Vector2(93.12f, 69.75f), new Vector2(92.77f, 70.75f), new Vector2(91.17f, 76.29f), new Vector2(89.48f, 84.07f), new Vector2(87.73f, 95.33f), new Vector2(86.89f, 102.83f), new Vector2(86.51f, 106.2f), new Vector2(86.73f, 107.4f), new Vector2(87.96f, 114.33f) }, hiedra = 0.7f, hiedraZona = new Vector2(0.3f, 0.75f) },
            new Muro { nombre = "Lindero_Este", bancal = false, puntos = new[] { new Vector2(100.61f, 119.96f), new Vector2(100.06f, 118.04f), new Vector2(99.86f, 111.39f), new Vector2(99.6f, 102.73f), new Vector2(99.54f, 100.74f), new Vector2(100.1f, 98.21f), new Vector2(106.09f, 89.5f), new Vector2(111.03f, 84.53f), new Vector2(115.82f, 78.92f), new Vector2(113.85f, 73.78f), new Vector2(116.36f, 71.02f), new Vector2(115.38f, 69.6f) }, hiedra = 1f, hiedraZona = new Vector2(0.3f, 0.6f) },
            new Muro { nombre = "Lindero_Camino", bancal = false, puntos = new[] { new Vector2(115.38f, 69.6f), new Vector2(113.54f, 66.94f), new Vector2(103.53f, 68.54f), new Vector2(93.12f, 69.75f) }, hiedra = 0.5f, hiedraZona = new Vector2(0.6f, 1f), minAlto = 0.6f },
        };

        // ---------------- Arboles: posicion y altura reales del LiDAR; especie segun las fotos ----------------
        public struct Arbol { public string especie; public Vector2 pos; public float alto; public float copa; public int semilla; }

        // Especie de los arboles del huerto y su entorno (el LiDAR da posicion y altura, las fotos la especie)
        static readonly (Vector2 pos, string especie)[] Especies =
        {
            (new Vector2(91.0f, 109.0f), "Nogal"),     // nogal del pasillo (102440, 105906)
            (new Vector2(99.0f, 108.5f), "Castano"),   // castano grande del lindero este (hojas a la izquierda en 102440)
            (new Vector2(88.0f, 114.0f), "Tuya"),      // arbol columnar junto a la casa vecina (102440)
            (new Vector2(97.0f, 120.5f), "Castano"),   // junto a la casa
            (new Vector2(93.5f, 96.5f), "Castano"),    // hojas grandes a la derecha en 105628
            (new Vector2(98.0f, 88.5f), "Joven"),      // arbolito del bancal medio (105610)
            (new Vector2(96.5f, 77.5f), "Cerezo"),     // cerezo del prado alto (105645, 105655)
            (new Vector2(109.0f, 76.0f), "Joven"),
            (new Vector2(111.0f, 84.0f), "Joven"),
            (new Vector2(106.0f, 70.5f), "Cerezo"),    // junto al deposito (105655)
            (new Vector2(89.5f, 72.5f), "Castano"),
            (new Vector2(86.0f, 91.0f), "Castano"),
            (new Vector2(86.5f, 84.5f), "Roble"),
        };

        // Arbolitos que el LiDAR no distingue (menos de 2,5 m) pero salen en las fotos 105610 y 105721
        static readonly Arbol[] Extra =
        {
            new Arbol { especie = "Joven", pos = new Vector2(101.0f, 93.6f), alto = 2.6f, copa = 1f },
            new Arbol { especie = "Joven", pos = new Vector2(95.0f, 80.0f), alto = 2.4f, copa = 1f },
            new Arbol { especie = "Joven", pos = new Vector2(103.4f, 84.0f), alto = 2.8f, copa = 1f },
        };

        static readonly Vector2[] RoblesSecos = { new Vector2(122.5f, 85.5f), new Vector2(128.5f, 88f) };

        static string EspeciePorAltura(float h, int semilla)
        {
            var r = new System.Random(semilla);
            double q = r.NextDouble();
            if (h < 4.5f) return q < 0.5 ? "Joven" : "Cerezo";
            if (h < 8f) return q < 0.35 ? "Cerezo" : q < 0.65 ? "Nogal" : "Castano";
            return q < 0.6 ? "Roble" : q < 0.85 ? "Castano" : "Nogal";
        }

        static List<Arbol> cacheArboles;
        public static List<Arbol> Arboles
        {
            get
            {
                if (cacheArboles != null) return cacheArboles;
                var lista = new List<Arbol>();
                int n = 0;
                foreach (var linea in File.ReadAllLines($"{Util.Raiz}/Datos/Arboles_LiDAR_2025.txt"))
                {
                    if (string.IsNullOrWhiteSpace(linea)) continue;
                    var c = linea.Split(';');
                    var a = new Arbol
                    {
                        pos = new Vector2(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture)),
                        alto = float.Parse(c[2], CultureInfo.InvariantCulture), copa = float.Parse(c[3], CultureInfo.InvariantCulture), semilla = 1000 + n++,
                    };
                    a.especie = EspeciePorAltura(a.alto, a.semilla);
                    foreach (var (pos, esp) in Especies) if (Vector2.Distance(pos, a.pos) < 2.5f) a.especie = esp;
                    foreach (var s in RoblesSecos) if (Vector2.Distance(s, a.pos) < 6f && a.alto > 7f) a.especie = "RobleSeco";
                    lista.Add(a);
                }
                for (int i = 0; i < Extra.Length; i++) { var e = Extra[i]; e.semilla = 50 + i; lista.Add(e); }
                cacheArboles = lista;
                return lista;
            }
        }

        public static void Recargar() => cacheArboles = null;

        // ---------------- Vistas de las fotos (posicion de los ojos, rumbo, inclinacion) ----------------
        public struct Vista { public string foto; public Vector2 pos; public float alturaOjos, rumbo, inclinacion; }

        public static readonly Vista[] Vistas =
        {
            new Vista { foto = "IMG20260802102440", pos = new Vector2(95.4f, 117.6f), alturaOjos = 1.6f, rumbo = 192f, inclinacion = 12f },
            new Vista { foto = "IMG20260802105548", pos = new Vector2(93.5f, 98.0f), alturaOjos = 1.6f, rumbo = 262f, inclinacion = -8f },
            new Vista { foto = "IMG20260802105557", pos = new Vector2(96.2f, 99.0f), alturaOjos = 1.5f, rumbo = 185f, inclinacion = 4f },
            new Vista { foto = "IMG20260802105610", pos = new Vector2(99.0f, 94.0f), alturaOjos = 1.6f, rumbo = 195f, inclinacion = 6f },
            new Vista { foto = "IMG20260802105628", pos = new Vector2(96.6f, 93.0f), alturaOjos = 1.6f, rumbo = 182f, inclinacion = 8f },
            new Vista { foto = "IMG20260802105645", pos = new Vector2(92.0f, 81.4f), alturaOjos = 0.9f, rumbo = 96f, inclinacion = -8f },
            new Vista { foto = "IMG20260802105655", pos = new Vector2(92.0f, 78.5f), alturaOjos = 1.4f, rumbo = 110f, inclinacion = -10f },
            new Vista { foto = "IMG20260802105721", pos = new Vector2(101.5f, 81.5f), alturaOjos = 1.6f, rumbo = 330f, inclinacion = 6f },
            new Vista { foto = "IMG20260802105726", pos = new Vector2(110.5f, 75.0f), alturaOjos = 1.6f, rumbo = 262f, inclinacion = 10f },
            new Vista { foto = "IMG20260802105906", pos = new Vector2(96.4f, 114.0f), alturaOjos = 1.4f, rumbo = 200f, inclinacion = 8f },
        };

        // ---------------- Datos de Catastro ----------------
        public class Poligono { public string id; public int valor; public Vector2[] pts; }

        public static List<Poligono> Leer(string archivo)
        {
            var lista = new List<Poligono>();
            foreach (var linea in File.ReadAllLines($"{Util.Raiz}/Datos/{archivo}"))
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;
                var c = linea.Split(';');
                var pts = new List<Vector2>();
                foreach (var par in c[2].Split(' '))
                {
                    var xy = par.Split(',');
                    pts.Add(new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture)));
                }
                lista.Add(new Poligono { id = c[0], valor = int.Parse(c[1]), pts = pts.ToArray() });
            }
            return lista;
        }

        public static float[] LeerAlturas(string archivo)
        {
            var b = File.ReadAllBytes($"{Util.Raiz}/Datos/{archivo}");
            var f = new float[b.Length / 4];
            System.Buffer.BlockCopy(b, 0, f, 0, b.Length);
            return f;
        }
    }
}
