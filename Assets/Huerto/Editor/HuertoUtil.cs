using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Huerto
{
    /// <summary>Construye mallas con varios submeshes (un material por submesh).</summary>
    public class MallaBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<List<int>> sub = new List<List<int>>();

        public MallaBuilder(int submeshes)
        {
            for (int i = 0; i < submeshes; i++) sub.Add(new List<int>());
        }

        public int Vert(Vector3 p, Vector3 normal, Vector2 t)
        {
            v.Add(p); n.Add(normal); uv.Add(t);
            return v.Count - 1;
        }

        public void Tri(int s, int a, int b, int c)
        {
            sub[s].Add(a); sub[s].Add(b); sub[s].Add(c);
        }

        /// <summary>Quad a-b-c-d en sentido horario visto desde delante.</summary>
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var nn = Vector3.Cross(b - a, c - a).normalized;
            int i0 = Vert(a, nn, ua), i1 = Vert(b, nn, ub), i2 = Vert(c, nn, uc), i3 = Vert(d, nn, ud);
            Tri(s, i0, i1, i2); Tri(s, i0, i2, i3);
        }

        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Quad(s, a, b, c, d, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }

        /// <summary>Tubo a lo largo de una polilinea con radio variable (ramas, postes).</summary>
        public void Tubo(int s, IList<Vector3> pts, IList<float> radios, int lados, float vEscala = 1f, bool tapa = false)
        {
            if (pts.Count < 2) return;
            Vector3 t0 = (pts[1] - pts[0]).normalized;
            Vector3 normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float vAcum = 0f;
            int baseIdx = v.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 tan = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                tan.Normalize();
                normal = Vector3.ProjectOnPlane(normal, tan).normalized;
                Vector3 bin = Vector3.Cross(tan, normal);
                if (i > 0) vAcum += Vector3.Distance(pts[i], pts[i - 1]);
                float r = radios[i];
                for (int k = 0; k <= lados; k++)
                {
                    float a = k / (float)lados * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    Vert(pts[i] + dir * r, dir, new Vector2(k / (float)lados, vAcum * vEscala / Mathf.Max(0.05f, radios[0] * 6.2832f)));
                }
            }
            int fila = lados + 1;
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < lados; k++)
                {
                    int a = baseIdx + i * fila + k, b = a + 1, c = a + fila, d = c + 1;
                    Tri(s, a, b, c); Tri(s, b, d, c);
                }
            if (tapa)
            {
                Vector3 tan = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
                int centro = Vert(pts[pts.Count - 1], tan, new Vector2(0.5f, 0.5f));
                int ultimo = baseIdx + (pts.Count - 1) * fila;
                for (int k = 0; k < lados; k++) Tri(s, ultimo + k, ultimo + k + 1, centro);
            }
        }

        /// <summary>Caja orientada (centro, ejes y medias medidas) con UV en metros.</summary>
        public void Caja(int s, Vector3 c, Vector3 ex, Vector3 ey, Vector3 ez, float uvEscala = 1f)
        {
            Vector3[] ejes = { ex, ey, ez };
            for (int i = 0; i < 3; i++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Vector3 nrm = ejes[i] * sg;
                    Vector3 u = ejes[(i + 1) % 3], w = ejes[(i + 2) % 3];
                    if (sg > 0) { var tmp = u; u = w; w = tmp; }
                    Vector3 o = c + nrm;
                    float lu = u.magnitude * 2 * uvEscala, lw = w.magnitude * 2 * uvEscala;
                    Quad(s, o - u - w, o - u + w, o + u + w, o + u - w,
                        new Vector2(0, 0), new Vector2(0, lw), new Vector2(lu, lw), new Vector2(lu, 0));
                }
        }

        /// <summary>Piedra: cubo subdividido, redondeado y deformado con ruido.</summary>
        public void Piedra(int s, Vector3 centro, Quaternion rot, Vector3 medias, System.Random rnd, float redondeo = 0.2f)
        {
            const int div = 2;
            float semilla = (float)rnd.NextDouble() * 100f;
            var cara = new Vector3[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nrm in cara)
            {
                Vector3 u = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(nrm, u);
                int b = v.Count;
                for (int i = 0; i <= div; i++)
                    for (int j = 0; j <= div; j++)
                    {
                        Vector3 p = nrm + u * (i / (float)div * 2 - 1) + w * (j / (float)div * 2 - 1);
                        Vector3 esf = p.normalized * 1.15f;
                        p = Vector3.Lerp(p, esf, redondeo);
                        float ruido = Mathf.PerlinNoise(p.x * 1.7f + semilla, p.z * 1.7f + p.y * 0.9f + semilla) - 0.5f;
                        p += p.normalized * ruido * 0.3f;
                        // aristas y bordes interiores un poco desconchados
                        if (i > 0 && i < div && j > 0 && j < div) p += nrm * Util.Rng(rnd, -0.08f, 0.05f);
                        Vector3 pl = Vector3.Scale(p, medias);
                        Vector3 pw = centro + rot * pl;
                        Vector3 nSuave = (rot * Vector3.Scale(p, new Vector3(1 / medias.x, 1 / medias.y, 1 / medias.z))).normalized;
                        // normal casi plana por cara: piedra labrada a golpes, no almohadillada
                        Vector3 nw = Vector3.Lerp(rot * nrm, nSuave, 0.3f).normalized;
                        Vector2 t = Mathf.Abs(nw.y) > 0.6f ? new Vector2(pw.x, pw.z) : Mathf.Abs(nw.x) > Mathf.Abs(nw.z) ? new Vector2(pw.z, pw.y) : new Vector2(pw.x, pw.y);
                        Vert(pw, nw, t * 0.9f);
                    }
                for (int i = 0; i < div; i++)
                    for (int j = 0; j < div; j++)
                    {
                        int a = b + i * (div + 1) + j, c = a + div + 1;
                        Tri(s, a, c + 1, a + 1); Tri(s, a, c, c + 1);
                    }
            }
        }

        public Mesh Construir(string nombre)
        {
            var m = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Count;
            for (int i = 0; i < sub.Count; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    public static class Util
    {
        public const string Raiz = "Assets/Huerto";

        public static void Carpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            int i = ruta.LastIndexOf('/');
            Carpeta(ruta.Substring(0, i));
            AssetDatabase.CreateFolder(ruta.Substring(0, i), ruta.Substring(i + 1));
        }

        /// <summary>Carga una textura de Assets/Huerto/Texturas ajustando su importacion.</summary>
        public static Texture2D Tex(string archivo, bool normal = false, bool hojas = false, int max = 2048)
        {
            string p = $"{Raiz}/Texturas/{archivo}";
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { Debug.LogWarning("Falta textura " + p); return null; }
            bool cambio = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; cambio = true; }
            if (hojas && !ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f;
                ti.wrapMode = TextureWrapMode.Clamp; cambio = true;
            }
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; cambio = true; }
            if (ti.anisoLevel < 4) { ti.anisoLevel = 4; cambio = true; }
            if (cambio) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        /// <summary>Material URP/Lit guardado en Assets/Huerto/Materiales.</summary>
        public static Material Mat(string nombre, Texture2D albedo, Texture2D normal, Color color, float suavidad,
            bool recorte = false, bool dobleCara = false, float repeticion = 1f, float metal = 0f)
        {
            Carpeta($"{Raiz}/Materiales");
            string ruta = $"{Raiz}/Materiales/{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            m.SetTextureScale("_BaseMap", Vector2.one * repeticion);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", suavidad);
            m.SetFloat("_Metallic", metal);
            if (recorte)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (dobleCara) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Guarda la malla como asset conservando el GUID si ya existia.</summary>
        public static Mesh Guardar(Mesh m, string nombre)
        {
            Carpeta($"{Raiz}/Generado");
            string ruta = $"{Raiz}/Generado/{nombre}.asset";
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente == null) { AssetDatabase.CreateAsset(m, ruta); return m; }
            // copia explicita: CopySerialized deja mal los inicios de indice de cada submesh
            existente.Clear();
            existente.indexFormat = m.indexFormat;
            existente.SetVertices(m.vertices);
            existente.SetNormals(m.normals);
            existente.SetTangents(m.tangents);
            existente.SetUVs(0, m.uv);
            if (m.colors.Length == m.vertexCount) existente.SetColors(m.colors);
            existente.subMeshCount = m.subMeshCount;
            for (int i = 0; i < m.subMeshCount; i++) existente.SetTriangles(m.GetTriangles(i), i, false);
            existente.RecalculateBounds();
            existente.name = nombre;
            EditorUtility.SetDirty(existente);
            return existente;
        }

        public static GameObject Objeto(string nombre, Transform padre, Mesh malla, params Material[] mats)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        public static bool DentroPoligono(Vector2 p, IList<Vector2> poly)
        {
            bool dentro = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    dentro = !dentro;
            }
            return dentro;
        }

        public static float DistanciaPolilinea(Vector2 p, IList<Vector2> poly, bool cerrada)
        {
            float best = float.MaxValue;
            int n = cerrada ? poly.Count : poly.Count - 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        public static float Rng(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>Hash estable (no depende del proceso) para sembrar aleatorios.</summary>
        public static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }
    }
}
