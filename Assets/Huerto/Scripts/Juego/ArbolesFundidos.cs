using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Huerto.Juego
{
    /// <summary>
    /// Arboles lejanos del pueblo y del monte. Desde el huerto siempre se ven en su LOD mas simple, asi que en vez de
    /// miles de arboles de terreno (que la CPU recorre uno a uno cada fotograma) se funden en una malla por cuadro de 250 m.
    /// Solo se guarda la lista de arboles; las mallas se montan al cargar la escena y no se escriben a disco.
    /// </summary>
    [ExecuteAlways]
    public class ArbolesFundidos : MonoBehaviour
    {
        [System.Serializable]
        public struct Modelo { public Mesh malla; public Material[] materiales; }

        public float cuadro = 250f;
        public Modelo[] modelos;
        public int[] modelo;
        public Vector3[] posicion;
        public float[] giro;          // grados
        public Vector2[] escala;      // ancho, alto

        readonly List<GameObject> hechos = new List<GameObject>();
        readonly List<Mesh> mallas = new List<Mesh>();

        class Lote
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector4> t = new List<Vector4>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly Dictionary<Material, List<int>> tris = new Dictionary<Material, List<int>>();
        }

        void OnEnable() => Construir();
        void OnDisable() => Limpiar();

        void Limpiar()
        {
            foreach (var g in hechos) if (g != null) DestroyImmediate(g);
            foreach (var m in mallas) if (m != null) DestroyImmediate(m);
            hechos.Clear(); mallas.Clear();
        }

        public void Construir()
        {
            Limpiar();
            if (modelos == null || posicion == null) return;
            // datos de cada modelo leidos una sola vez
            var mv = new Vector3[modelos.Length][]; var mn = new Vector3[modelos.Length][]; var mt = new Vector4[modelos.Length][];
            var mu = new Vector2[modelos.Length][]; var mi = new int[modelos.Length][][];
            for (int i = 0; i < modelos.Length; i++)
            {
                var m = modelos[i].malla;
                if (m == null) continue;
                mv[i] = m.vertices; mn[i] = m.normals; mt[i] = m.tangents; mu[i] = m.uv;
                mi[i] = new int[m.subMeshCount][];
                for (int s = 0; s < m.subMeshCount; s++) mi[i][s] = m.GetTriangles(s);
            }
            var lotes = new Dictionary<Vector2Int, Lote>();
            for (int k = 0; k < posicion.Length; k++)
            {
                int id = modelo[k];
                if (mv[id] == null) continue;
                Vector3 p = posicion[k];
                var clave = new Vector2Int(Mathf.FloorToInt(p.x / cuadro), Mathf.FloorToInt(p.z / cuadro));
                if (!lotes.TryGetValue(clave, out var l)) lotes[clave] = l = new Lote();
                var mat = Matrix4x4.TRS(p, Quaternion.Euler(0, giro[k], 0), new Vector3(escala[k].x, escala[k].y, escala[k].x));
                var matN = mat.inverse.transpose;
                int b = l.v.Count;
                var vv = mv[id]; var nn = mn[id]; var tt = mt[id]; var uu = mu[id];
                for (int i = 0; i < vv.Length; i++)
                {
                    l.v.Add(mat.MultiplyPoint3x4(vv[i]));
                    l.n.Add(matN.MultiplyVector(nn[i]).normalized);
                    Vector3 tg = mat.MultiplyVector(tt[i]).normalized;
                    l.t.Add(new Vector4(tg.x, tg.y, tg.z, tt[i].w));
                    l.uv.Add(uu[i]);
                }
                var mats = modelos[id].materiales;
                for (int s = 0; s < mi[id].Length; s++)
                {
                    if (!l.tris.TryGetValue(mats[s], out var lt)) l.tris[mats[s]] = lt = new List<int>();
                    foreach (int i in mi[id][s]) lt.Add(b + i);
                }
            }
            foreach (var kv in lotes)
            {
                var l = kv.Value;
                var malla = new Mesh { name = $"Lejanos_{kv.Key.x}_{kv.Key.y}", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                malla.SetVertices(l.v); malla.SetNormals(l.n); malla.SetTangents(l.t); malla.SetUVs(0, l.uv);
                var mats = new List<Material>(l.tris.Keys);
                malla.subMeshCount = mats.Count;
                for (int s = 0; s < mats.Count; s++) malla.SetTriangles(l.tris[mats[s]], s);
                malla.RecalculateBounds();
                malla.UploadMeshData(true);
                var go = new GameObject(malla.name) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malla;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats.ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
                hechos.Add(go); mallas.Add(malla);
            }
        }
    }
}
