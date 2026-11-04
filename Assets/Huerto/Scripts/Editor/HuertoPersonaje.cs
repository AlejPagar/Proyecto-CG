using System.Collections.Generic;
using System.Linq;
using Huerto.Juego;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Huerto
{
    /// <summary>
    /// Personaje jugable a escala real: hombre de 1,73 m (1,80 con el sombrero de paja), con camisa de lino remangada,
    /// pantalon de faena, cinturon y botas de cuero. Esqueleto de transforms (cadera, torso, cabeza, brazos y piernas)
    /// que anima Juego.Paseante, y muros invisibles en el lindero de la parcela 39 para no salir del huerto.
    /// </summary>
    public static class Personaje
    {
        enum M { Camisa, Pantalon, Cuero, Piel, Pelo, Paja, Cinta, Ojos, Suela }

        static Material[] Materiales()
        {
            return new[]
            {
                Util.Mat("Personaje_Camisa", Util.Tex("Personaje/rough_linen_diff_1k.jpg", max: 1024), Util.Tex("Personaje/rough_linen_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.92f, 0.95f, 1f), 0.15f, repeticion: 3f),
                Util.Mat("Personaje_Pantalon", Util.Tex("Personaje/denim_fabric_05_diff_1k.jpg", max: 1024), Util.Tex("Personaje/denim_fabric_05_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.78f, 0.7f, 0.6f), 0.12f, repeticion: 3f),
                Util.Mat("Personaje_Cuero", Util.Tex("Personaje/brown_leather_albedo_1k.jpg", max: 1024), Util.Tex("Personaje/brown_leather_nor_gl_1k.jpg", normal: true, max: 1024), new Color(0.55f, 0.45f, 0.38f), 0.35f, repeticion: 2f),
                Util.Mat("Personaje_Piel", null, null, new Color(0.8f, 0.6f, 0.48f), 0.32f),
                Util.Mat("Personaje_Pelo", null, null, new Color(0.6f, 0.58f, 0.56f), 0.2f),
                Util.Mat("Personaje_Paja", Util.Tex("Personaje/hessian_230_diff_1k.jpg", max: 1024), Util.Tex("Personaje/hessian_230_nor_gl_1k.jpg", normal: true, max: 1024), new Color(1.25f, 1.12f, 0.8f), 0.1f, repeticion: 4f),
                Util.Mat("Personaje_Cinta", null, null, new Color(0.1f, 0.09f, 0.08f), 0.25f),
                Util.Mat("Personaje_Ojos", null, null, new Color(0.04f, 0.035f, 0.03f), 0.8f),
                Util.Mat("Personaje_Suela", null, null, new Color(0.07f, 0.06f, 0.05f), 0.2f),
            };
        }

        // ---------------- Geometria ----------------
        /// <summary>Superficie de anillos elipticos de arriba abajo (ri = semiejes x, z). Caras hacia fuera.</summary>
        static void Anillos(MallaBuilder mb, M mat, IList<Vector3> c, IList<Vector2> ri, int lados = 14, bool tapaArriba = false, bool tapaAbajo = false)
        {
            int s = (int)mat, n = c.Count, fila = lados + 1, b0 = mb.v.Count;
            var P = new Vector3[n, fila];
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= lados; j++)
                {
                    float a = j % lados / (float)lados * Mathf.PI * 2f;
                    P[i, j] = c[i] + new Vector3(ri[i].x * Mathf.Cos(a), 0, ri[i].y * Mathf.Sin(a));
                }
            float vAcum = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) vAcum += Vector3.Distance(c[i], c[i - 1]) + Mathf.Abs(ri[i].x - ri[i - 1].x);
                float perim = Mathf.PI * (ri[i].x + ri[i].y);
                for (int j = 0; j <= lados; j++)
                {
                    Vector3 alrededor = P[i, (j + 1) % lados] - P[i, (j + lados - 1) % lados];
                    Vector3 alo = P[Mathf.Min(n - 1, i + 1), j] - P[Mathf.Max(0, i - 1), j];
                    Vector3 nr = Vector3.Cross(alrededor, alo);
                    if (nr.sqrMagnitude < 1e-12f) nr = i == 0 ? Vector3.up : Vector3.down;
                    mb.Vert(P[i, j], nr.normalized, new Vector2(j / (float)lados * perim, vAcum));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < lados; j++)
                {
                    int a = b0 + i * fila + j, b = a + 1, cc = a + fila, d = cc + 1;
                    mb.Tri(s, a, b, cc); mb.Tri(s, b, d, cc);
                }
            if (tapaArriba)
            {
                int cen = mb.Vert(c[0], Vector3.up, Vector2.zero);
                for (int j = 0; j < lados; j++) mb.Tri(s, cen, b0 + j + 1, b0 + j);
            }
            if (tapaAbajo)
            {
                int cen = mb.Vert(c[n - 1], Vector3.down, Vector2.zero), ul = b0 + (n - 1) * fila;
                for (int j = 0; j < lados; j++) mb.Tri(s, cen, ul + j, ul + j + 1);
            }
        }

        /// <summary>Tubo vertical de arriba abajo: lista de (y, radio x, radio z).</summary>
        static void Perfil(MallaBuilder mb, M mat, Vector3 o, float[,] yr, int lados = 14, bool tapaArriba = false, bool tapaAbajo = false, float zDesvio = 0f)
        {
            var c = new List<Vector3>(); var r = new List<Vector2>();
            for (int i = 0; i < yr.GetLength(0); i++)
            {
                c.Add(o + new Vector3(0, yr[i, 0], yr.GetLength(1) > 3 ? yr[i, 3] : 0f));
                r.Add(new Vector2(yr[i, 1], yr[i, 2]));
            }
            Anillos(mb, mat, c, r, lados, tapaArriba, tapaAbajo);
        }

        static void Elipsoide(MallaBuilder mb, M mat, Vector3 centro, Vector3 radios, int anillos = 10, int lados = 14)
        {
            var c = new List<Vector3>(); var r = new List<Vector2>();
            for (int i = 0; i <= anillos; i++)
            {
                float t = Mathf.Lerp(0.02f, Mathf.PI - 0.02f, i / (float)anillos);
                c.Add(centro + Vector3.up * radios.y * Mathf.Cos(t));
                r.Add(new Vector2(radios.x, radios.z) * Mathf.Sin(t));
            }
            Anillos(mb, mat, c, r, lados, true, true);
        }

        static Transform Hueso(string nombre, Transform padre, Vector3 pos)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            t.localPosition = pos;
            return t;
        }

        static void Parte(string nombre, Transform hueso, Material[] mats, System.Action<MallaBuilder> construir)
        {
            var mb = new MallaBuilder(mats.Length);
            construir(mb);
            var usados = new List<int>();
            for (int i = 0; i < mb.sub.Count; i++) if (mb.sub[i].Count > 0) usados.Add(i);
            var m = new Mesh { name = nombre };
            m.SetVertices(mb.v); m.SetNormals(mb.n); m.SetUVs(0, mb.uv);
            m.subMeshCount = usados.Count;
            var lm = new Material[usados.Count];
            for (int k = 0; k < usados.Count; k++) { m.SetTriangles(mb.sub[usados[k]], k); lm[k] = mats[usados[k]]; }
            m.RecalculateBounds(); m.RecalculateTangents();
            var malla = Util.Guardar(m, "Personaje_" + nombre);
            var go = new GameObject(nombre);
            go.transform.SetParent(hueso, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            go.AddComponent<MeshRenderer>().sharedMaterials = lm;
        }

        // ---------------- Personaje ----------------
        public static GameObject Crear(Terrain t, Vector2 pos, float rumbo)
        {
            var agricultor = Terceros.PrefabAgricultor();
            if (agricultor != null) return CrearAgricultor(t, pos, rumbo, agricultor);
            var mats = Materiales();
            var raiz = new GameObject("Personaje");
            raiz.layer = 2;   // Ignore Raycast: ni tapa la camara ni cuenta en los rayos del juego
            raiz.transform.position = new Vector3(pos.x, t.SampleHeight(new Vector3(pos.x, 0, pos.y)) + t.transform.position.y + 0.05f, pos.y);
            var cuerpo = Hueso("Cuerpo", raiz.transform, Vector3.zero);
            cuerpo.rotation = Quaternion.Euler(0, rumbo, 0);

            // Cadera (articulacion de las piernas a 0,93 m del suelo)
            var cadera = Hueso("Cadera", cuerpo, new Vector3(0, 0.95f, 0));
            Parte("Pelvis", cadera, mats, mb =>
            {
                Perfil(mb, M.Pantalon, Vector3.zero, new float[,] { { 0.07f, 0.158f, 0.104f }, { 0f, 0.168f, 0.11f }, { -0.07f, 0.162f, 0.105f }, { -0.12f, 0.14f, 0.092f } }, 16, true, true);
                Perfil(mb, M.Cuero, Vector3.zero, new float[,] { { 0.078f, 0.163f, 0.109f }, { 0.04f, 0.168f, 0.112f } }, 16);
                // hebilla
                mb.Caja((int)M.Ojos, new Vector3(0, 0.059f, 0.113f), new Vector3(0.022f, 0, 0), new Vector3(0, 0.018f, 0), new Vector3(0, 0, 0.004f));
            });

            // Torso con la camisa metida en el pantalon
            var torso = Hueso("Torso", cadera, new Vector3(0, 0.06f, 0));
            Parte("Camisa", torso, mats, mb =>
            {
                Perfil(mb, M.Camisa, Vector3.zero, new float[,]
                {
                    { 0.50f, 0.05f, 0.045f, 0f }, { 0.48f, 0.11f, 0.075f, 0f }, { 0.45f, 0.172f, 0.102f, 0f }, { 0.38f, 0.188f, 0.114f, 0.005f },
                    { 0.26f, 0.174f, 0.116f, 0.01f }, { 0.13f, 0.165f, 0.117f, 0.016f }, { 0.0f, 0.157f, 0.104f, 0.006f }, { -0.04f, 0.154f, 0.1f, 0f },
                }, 18);
                // cuello de la camisa y del hombre
                Perfil(mb, M.Camisa, Vector3.zero, new float[,] { { 0.52f, 0.058f, 0.058f }, { 0.47f, 0.07f, 0.066f } }, 14);
                Perfil(mb, M.Piel, Vector3.zero, new float[,] { { 0.6f, 0.047f, 0.05f }, { 0.45f, 0.053f, 0.054f } }, 12);
                // botones y bolsillo
                for (int i = 0; i < 5; i++) mb.Caja((int)M.Pelo, new Vector3(0, 0.44f - i * 0.1f, 0.112f + (i > 0 && i < 4 ? 0.006f : 0f)), new Vector3(0.006f, 0, 0), new Vector3(0, 0.006f, 0), new Vector3(0, 0, 0.003f));
            });

            // Cabeza
            var cabeza = Hueso("Cabeza", torso, new Vector3(0, 0.51f, 0));
            Parte("Cara", cabeza, mats, mb =>
            {
                Elipsoide(mb, M.Piel, new Vector3(0, 0.1f, 0.012f), new Vector3(0.077f, 0.112f, 0.096f), 12, 18);
                Elipsoide(mb, M.Piel, new Vector3(0, 0.088f, 0.106f), new Vector3(0.014f, 0.026f, 0.018f), 6, 10);              // nariz
                foreach (float lx in new[] { -1f, 1f })
                {
                    Elipsoide(mb, M.Piel, new Vector3(0.077f * lx, 0.095f, 0.0f), new Vector3(0.012f, 0.03f, 0.02f), 6, 8);       // orejas
                    Elipsoide(mb, M.Ojos, new Vector3(0.031f * lx, 0.118f, 0.091f), new Vector3(0.009f, 0.007f, 0.006f), 5, 8);   // ojos
                    mb.Caja((int)M.Pelo, new Vector3(0.032f * lx, 0.136f, 0.094f), new Vector3(0.017f, 0, 0), new Vector3(0, 0.004f, 0), new Vector3(0, 0, 0.004f)); // cejas
                }
                // pelo canoso por detras y en las sienes
                Elipsoide(mb, M.Pelo, new Vector3(0, 0.125f, -0.016f), new Vector3(0.081f, 0.095f, 0.092f), 10, 18);
            });
            Parte("Sombrero", cabeza, mats, mb =>
            {
                // copa, cinta y ala de paja (cara de arriba y de abajo)
                Perfil(mb, M.Paja, Vector3.zero, new float[,] { { 0.285f, 0.062f, 0.07f }, { 0.27f, 0.084f, 0.094f }, { 0.215f, 0.093f, 0.104f }, { 0.175f, 0.094f, 0.106f } }, 18, true);
                Perfil(mb, M.Cinta, Vector3.zero, new float[,] { { 0.215f, 0.0955f, 0.1065f }, { 0.185f, 0.0965f, 0.1085f } }, 18);
                Perfil(mb, M.Paja, Vector3.zero, new float[,] { { 0.178f, 0.094f, 0.106f }, { 0.168f, 0.17f, 0.18f }, { 0.152f, 0.205f, 0.215f } }, 22);
                Perfil(mb, M.Paja, Vector3.zero, new float[,] { { 0.146f, 0.205f, 0.215f }, { 0.162f, 0.17f, 0.18f }, { 0.172f, 0.094f, 0.106f } }, 22);
            });

            // Brazos: camisa remangada hasta el codo
            Transform[] hombros = new Transform[2], codos = new Transform[2];
            for (int l = 0; l < 2; l++)
            {
                float lx = l == 0 ? -1f : 1f; string lado = l == 0 ? "I" : "D";
                var hombro = Hueso("Hombro" + lado, torso, new Vector3(0.198f * lx, 0.415f, 0));
                Parte("Brazo" + lado, hombro, mats, mb =>
                    Perfil(mb, M.Camisa, Vector3.zero, new float[,] { { 0.05f, 0.02f, 0.02f }, { 0.035f, 0.048f, 0.05f }, { 0f, 0.058f, 0.06f }, { -0.15f, 0.052f, 0.054f }, { -0.29f, 0.046f, 0.048f } }, 12, true));
                var codo = Hueso("Codo" + lado, hombro, new Vector3(0, -0.29f, 0));
                Parte("Antebrazo" + lado, codo, mats, mb =>
                {
                    Perfil(mb, M.Camisa, Vector3.zero, new float[,] { { 0.035f, 0.05f, 0.052f }, { -0.035f, 0.052f, 0.054f } }, 12);   // manga vuelta
                    Perfil(mb, M.Piel, Vector3.zero, new float[,] { { 0.01f, 0.04f, 0.041f }, { -0.12f, 0.038f, 0.036f }, { -0.24f, 0.03f, 0.027f }, { -0.255f, 0.028f, 0.026f } }, 10);
                    Elipsoide(mb, M.Piel, new Vector3(0, -0.325f, 0.006f), new Vector3(0.022f, 0.078f, 0.044f), 8, 10);             // mano
                    Elipsoide(mb, M.Piel, new Vector3(-0.004f * lx, -0.3f, 0.04f), new Vector3(0.012f, 0.035f, 0.012f), 5, 8);    // pulgar
                });
                hombros[l] = hombro; codos[l] = codo;
            }

            // Piernas y botas
            Transform[] muslos = new Transform[2], rodillas = new Transform[2], tobillos = new Transform[2];
            for (int l = 0; l < 2; l++)
            {
                float lx = l == 0 ? -1f : 1f; string lado = l == 0 ? "I" : "D";
                var muslo = Hueso("Muslo" + lado, cadera, new Vector3(0.093f * lx, -0.02f, 0));
                Parte("Muslo" + lado, muslo, mats, mb =>
                    Perfil(mb, M.Pantalon, Vector3.zero, new float[,] { { 0.07f, 0.07f, 0.075f }, { 0f, 0.088f, 0.09f }, { -0.2f, 0.076f, 0.079f }, { -0.42f, 0.059f, 0.063f }, { -0.45f, 0.057f, 0.061f } }, 14, true));
                var rodilla = Hueso("Rodilla" + lado, muslo, new Vector3(0, -0.43f, 0));
                Parte("Pierna" + lado, rodilla, mats, mb =>
                    Perfil(mb, M.Pantalon, Vector3.zero, new float[,] { { 0.035f, 0.059f, 0.063f, 0f }, { -0.12f, 0.057f, 0.061f, -0.006f }, { -0.3f, 0.049f, 0.051f, 0f }, { -0.4f, 0.055f, 0.058f, 0f }, { -0.425f, 0.056f, 0.059f, 0f } }, 14));
                var tobillo = Hueso("Tobillo" + lado, rodilla, new Vector3(0, -0.44f, 0));
                Parte("Bota" + lado, tobillo, mats, mb =>
                {
                    Perfil(mb, M.Cuero, Vector3.zero, new float[,] { { 0.07f, 0.047f, 0.05f }, { -0.02f, 0.05f, 0.053f } }, 12);
                    Elipsoide(mb, M.Cuero, new Vector3(0, -0.012f, 0.045f), new Vector3(0.05f, 0.042f, 0.128f), 8, 14);
                    mb.Caja((int)M.Suela, new Vector3(0, -0.051f, 0.045f), new Vector3(0.051f, 0, 0), new Vector3(0, 0.009f, 0), new Vector3(0, 0, 0.128f));
                });
                muslos[l] = muslo; rodillas[l] = rodilla; tobillos[l] = tobillo;
            }

            // tableta del gemelo digital: solo se ve con la tableta abierta, sujeta delante del pecho y mirando a la cara
            var tableta = Tableta(torso, new Vector3(0, 0.27f, 0.3f), mats[(int)M.Suela]);

            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;

            var p = Controlador(raiz);
            p.cuerpo = cuerpo; p.cadera = cadera; p.torso = torso; p.cabeza = cabeza;
            p.hombroI = hombros[0]; p.hombroD = hombros[1]; p.codoI = codos[0]; p.codoD = codos[1];
            p.musloI = muslos[0]; p.musloD = muslos[1]; p.rodillaI = rodillas[0]; p.rodillaD = rodillas[1];
            p.tobilloI = tobillos[0]; p.tobilloD = tobillos[1];
            p.tabletaMano = tableta.gameObject;
            return raiz;
        }

        static Paseante Controlador(GameObject raiz)
        {
            var cc = raiz.AddComponent<CharacterController>();
            cc.height = 1.76f; cc.radius = 0.24f; cc.center = new Vector3(0, 0.88f + 0.01f, 0);
            cc.stepOffset = 0.3f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            return raiz.AddComponent<Paseante>();
        }

        /// <summary>Tableta del gemelo digital (borde y pantalla emisiva), oculta hasta que se abre.</summary>
        static Transform Tableta(Transform padre, Vector3 pos, Material borde)
        {
            var tableta = Hueso("TabletaMano", padre, pos);
            var pantallaMat = Util.Mat("Personaje_Pantalla", null, null, new Color(0.85f, 0.9f, 0.82f), 0.85f, dobleCara: true);
            pantallaMat.EnableKeyword("_EMISSION"); pantallaMat.SetColor("_EmissionColor", new Color(0.55f, 0.62f, 0.5f));
            var mb = new MallaBuilder(2);
            Vector3 n = new Vector3(0, 0.8f, -0.6f).normalized, ex = Vector3.right * 0.125f, ey = Vector3.Cross(n, Vector3.right).normalized * 0.088f;
            if (ey.y < 0) ey = -ey;
            mb.Caja(0, Vector3.zero, ex, ey, n * 0.006f);
            Vector3 c = n * 0.0065f, a1 = ex * 0.9f, a2 = ey * 0.86f;
            mb.Quad(1, c - a1 - a2, c - a1 + a2, c + a1 + a2, c + a1 - a2);
            var go = new GameObject("Tableta");
            go.transform.SetParent(tableta, false);
            go.AddComponent<MeshFilter>().sharedMesh = Util.Guardar(mb.Construir("Tableta"), "Personaje_Tableta");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { borde, pantallaMat };
            tableta.gameObject.SetActive(false);
            return tableta;
        }

        /// <summary>
        /// Personaje con el agricultor de la Asset Store (peto, camisa y gorra), escalado a 1,75 m y animado con
        /// Juego.AnimacionAgricultor. El Paseante sigue moviendolo y contando los pasos; los huesos procedurales se quedan vacios.
        /// </summary>
        static GameObject CrearAgricultor(Terrain t, Vector2 pos, float rumbo, GameObject prefab)
        {
            var raiz = new GameObject("Personaje");
            raiz.layer = 2;   // Ignore Raycast: ni tapa la camara ni cuenta en los rayos del juego
            raiz.transform.position = new Vector3(pos.x, t.SampleHeight(new Vector3(pos.x, 0, pos.y)) + t.transform.position.y + 0.05f, pos.y);
            var cuerpo = Hueso("Cuerpo", raiz.transform, Vector3.zero);
            cuerpo.rotation = Quaternion.Euler(0, rumbo, 0);

            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cuerpo);
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelo.name = "Agricultor";
            modelo.transform.localPosition = Vector3.zero;
            modelo.transform.localRotation = Quaternion.identity;
            modelo.transform.localScale = Vector3.one * (1.75f / 1.83f);
            foreach (var col in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var tr in modelo.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;

            var p = Controlador(raiz);
            var animador = modelo.GetComponentInChildren<Animator>();
            animador.runtimeAnimatorController = Terceros.ControladorAgricultor(p.velAndar, p.velCorrer);
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var cabeza = modelo.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Handyman Head");
            var tableta = Tableta(cuerpo, new Vector3(0, 1.13f, 0.34f), Util.Mat("Personaje_Suela", null, null, new Color(0.07f, 0.06f, 0.05f), 0.2f));
            p.cuerpo = cuerpo;
            p.cabeza = cabeza != null ? cabeza : cuerpo;
            p.tabletaMano = tableta.gameObject;
            p.rendCabeza = modelo.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Head1") || r.name.StartsWith("cap") || r.name.StartsWith("haircut") || r.name.StartsWith("helmet")).ToArray();

            var anim = animador.gameObject.AddComponent<AnimacionAgricultor>();
            anim.paseante = p;
            anim.tableta = tableta;
            anim.duracionTrepar = Terceros.DuracionTrepar();
            return raiz;
        }

        /// <summary>Muros invisibles sobre el lindero de la parcela 39 (capa Ignore Raycast: no frenan la camara).</summary>
        public static void Limites(Terrain t, Transform padre)
        {
            var raiz = new GameObject("Limites_Parcela39").transform;
            raiz.SetParent(padre, false);
            var poli = Datos.Parcela;
            for (int i = 0; i < poli.Length; i++)
            {
                Vector2 a = poli[i], b = poli[(i + 1) % poli.Length], d = b - a;
                float largo = d.magnitude, y0 = float.MaxValue, y1 = float.MinValue;
                for (int k = 0; k <= 8; k++)
                {
                    var q = Vector2.Lerp(a, b, k / 8f);
                    float y = t.SampleHeight(new Vector3(q.x, 0, q.y)) + t.transform.position.y;
                    y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
                var go = new GameObject($"Limite_{i:00}") { layer = 2 };
                go.transform.SetParent(raiz, false);
                Vector2 m = (a + b) * 0.5f;
                go.transform.SetPositionAndRotation(new Vector3(m.x, (y0 - 1f + y1 + 4f) * 0.5f, m.y), Quaternion.LookRotation(new Vector3(d.x, 0, d.y)));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(0.2f, y1 + 4f - (y0 - 1f), largo + 0.4f);
                go.AddComponent<LimiteHuerto>();
            }
        }
    }
}
