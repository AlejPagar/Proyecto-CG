using UnityEngine;

namespace Huerto
{
    /// <summary>Muros de piedra seca de granito, piedra a piedra, y la hiedra que los cubre.</summary>
    public static class Muros
    {
        static float Suelo(Terrain t, Vector2 p) => t.SampleHeight(new Vector3(p.x, 0, p.y)) + t.transform.position.y;

        public static void Crear(Datos.Muro m, Terrain t, Transform padre, Material[] piedras, Material hiedra)
        {
            var rnd = new System.Random(Util.Hash(m.nombre));
            var mb = new MallaBuilder(piedras.Length);
            var mh = new MallaBuilder(1);
            float largoTotal = 0f;
            for (int i = 0; i < m.puntos.Length - 1; i++) largoTotal += Vector2.Distance(m.puntos[i], m.puntos[i + 1]);
            float recorrido = 0f;
            for (int i = 0; i < m.puntos.Length - 1; i++)
            {
                Vector2 a = m.puntos[i], b = m.puntos[i + 1];
                float L = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / L;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector3 dir3 = new Vector3(dir.x, 0, dir.y), perp3 = new Vector3(perp.x, 0, perp.y);
                Quaternion rotBase = Quaternion.LookRotation(Vector3.Cross(dir3, Vector3.up), Vector3.up);

                // Alto del muro en cada punto: del lado bajo (enterrado) al lado alto (+ coronacion)
                System.Func<float, Vector2> limites = u =>
                {
                    Vector2 p = a + dir * u;
                    float h1 = Suelo(t, p + perp * 0.75f), h2 = Suelo(t, p - perp * 0.75f);
                    float dif = Mathf.Abs(h1 - h2);
                    return new Vector2(Mathf.Min(h1, h2) - 0.3f, Mathf.Max(h1, h2) + Mathf.Max(0.12f, m.minAlto - dif));
                };

                // Hiladas
                var alturasHilada = new float[14];
                float acum = 0f;
                for (int k = 0; k < alturasHilada.Length; k++) { alturasHilada[k] = acum; acum += k == 0 ? Util.Rng(rnd, 0.28f, 0.38f) : Util.Rng(rnd, 0.13f, 0.26f); }
                for (int k = 0; k < alturasHilada.Length - 1; k++)
                {
                    float hk = alturasHilada[k + 1] - alturasHilada[k];
                    float u = -Util.Rng(rnd, 0f, 0.3f);
                    while (u < L)
                    {
                        float largo = k == 0 ? Util.Rng(rnd, 0.35f, 0.7f) : Util.Rng(rnd, 0.2f, 0.5f);
                        float uc = Mathf.Clamp(u + largo * 0.5f, 0f, L);
                        u += largo;
                        var lim = limites(uc);
                        float yb = lim.x + alturasHilada[k];
                        if (yb > lim.y - 0.08f) continue;
                        float alto = Mathf.Min(hk, lim.y - yb);
                        if (alto < 0.1f) continue;
                        float fondo = m.grosor * Util.Rng(rnd, 0.82f, 1.0f);
                        Vector2 c2 = a + dir * uc + perp * Util.Rng(rnd, -0.07f, 0.07f);
                        var c = new Vector3(c2.x, yb + alto * 0.5f, c2.y);
                        var rot = rotBase * Quaternion.Euler(Util.Rng(rnd, -7f, 7f), Util.Rng(rnd, -8f, 8f), Util.Rng(rnd, -9f, 9f));
                        float esc = Util.Rng(rnd, 0.9f, 1.12f);
                        int tono = rnd.Next(piedras.Length);
                        mb.Piedra(tono, c, rot, new Vector3(largo * 0.56f * esc, alto * 0.6f * esc, fondo * 0.5f), rnd, Util.Rng(rnd, 0.05f, 0.2f));
                    }
                }

                // Hiedra en el tramo indicado
                for (float u = 0.15f; u < L; u += 0.24f)
                {
                    float frac = (recorrido + u) / largoTotal;
                    if (frac < m.hiedraZona.x || frac > m.hiedraZona.y) continue;
                    var lim = limites(u);
                    Vector2 p2 = a + dir * u;
                    float mancha = Mathf.PerlinNoise(p2.x * 0.5f + 3f, p2.y * 0.5f + 8f);
                    for (float y = lim.x + 0.25f; y < lim.y + 0.1f; y += 0.22f)
                        for (int lado = -1; lado <= 1; lado += 2)
                        {
                            if (rnd.NextDouble() > m.hiedra * Util.Smooth(0.25f, 0.55f, mancha)) continue;
                            Vector3 nrm = perp3 * lado;
                            Vector3 basePos = new Vector3(p2.x, y, p2.y) + nrm * (m.grosor * 0.5f + Util.Rng(rnd, 0.02f, 0.12f)) + dir3 * Util.Rng(rnd, -0.1f, 0.1f);
                            Vector3 arriba = (Vector3.down * Util.Rng(rnd, 0.4f, 1f) + dir3 * Util.Rng(rnd, -0.8f, 0.8f) + nrm * 0.25f).normalized;
                            Hoja(mh, basePos, arriba, nrm, Util.Rng(rnd, 0.4f, 0.65f));
                        }
                    if (rnd.NextDouble() < m.hiedra * Util.Smooth(0.2f, 0.5f, mancha))
                    {
                        Vector3 top = new Vector3(p2.x, lim.y + 0.04f, p2.y);
                        Vector3 arriba = (dir3 * Util.Rng(rnd, -1f, 1f) + perp3 * Util.Rng(rnd, -1f, 1f)).normalized;
                        Hoja(mh, top - arriba * 0.25f, arriba, Vector3.up, Util.Rng(rnd, 0.5f, 0.75f));
                    }
                }
                recorrido += L;
            }
            var mallaMuro = Util.Guardar(mb.Construir(m.nombre), m.nombre);
            var go = Util.Objeto(m.nombre, padre, mallaMuro, piedras);
            go.AddComponent<MeshCollider>().sharedMesh = mallaMuro;
            if (mh.v.Count > 0)
                Util.Objeto(m.nombre + "_Hiedra", go.transform, Util.Guardar(mh.Construir(m.nombre + "_Hiedra"), m.nombre + "_Hiedra"), hiedra);
        }

        /// <summary>Tarjeta de hojas: la base de la ramita en 'basePos' y la punta hacia 'arriba'.</summary>
        public static void Hoja(MallaBuilder mb, Vector3 basePos, Vector3 arriba, Vector3 normal, float tam)
        {
            Vector3 ancho = Vector3.Cross(arriba, normal).normalized * tam * 0.5f;
            if (ancho.sqrMagnitude < 1e-6f) ancho = Vector3.Cross(arriba, Vector3.right).normalized * tam * 0.5f;
            Vector3 alto = arriba * tam;
            int i0 = mb.Vert(basePos - ancho, normal, new Vector2(0, 0));
            int i1 = mb.Vert(basePos - ancho + alto, normal, new Vector2(0, 1));
            int i2 = mb.Vert(basePos + ancho + alto, normal, new Vector2(1, 1));
            int i3 = mb.Vert(basePos + ancho, normal, new Vector2(1, 0));
            mb.Tri(0, i0, i1, i2); mb.Tri(0, i0, i2, i3);
        }

        /// <summary>Cordon de hiedra (un muro viejo tapado) del pasillo, foto 102440.</summary>
        public static void Cordon(Vector2[] puntos, float ancho, Terrain t, Transform padre, Material fondo, Material hiedra, string nombre)
        {
            var rnd = new System.Random(Util.Hash(nombre));
            var mb = new MallaBuilder(1);
            var mh = new MallaBuilder(1);
            var pts = new System.Collections.Generic.List<Vector3>();
            var rad = new System.Collections.Generic.List<float>();
            float total = 0f;
            for (int i = 0; i < puntos.Length - 1; i++) total += Vector2.Distance(puntos[i], puntos[i + 1]);
            float s = 0f;
            for (int i = 0; i < puntos.Length - 1; i++)
            {
                float L = Vector2.Distance(puntos[i], puntos[i + 1]);
                for (float u = 0; u < L; u += 0.3f)
                {
                    var p = Vector2.Lerp(puntos[i], puntos[i + 1], u / L);
                    float f = (s + u) / total;
                    float r = ancho * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Clamp(f * 1.08f - 0.04f, 0.02f, 0.98f));
                    pts.Add(new Vector3(p.x, Suelo(t, p) - 0.1f, p.y)); rad.Add(Mathf.Max(0.08f, r));
                }
                s += L;
            }
            mb.Tubo(0, pts, rad, 10);
            for (int i = 0; i < pts.Count; i++)
                for (int k = 0; k < 14; k++)
                {
                    float ang = Util.Rng(rnd, 0.1f, Mathf.PI - 0.1f);
                    Vector3 tan = i < pts.Count - 1 ? (pts[i + 1] - pts[i]).normalized : (pts[i] - pts[i - 1]).normalized;
                    Vector3 lado = Vector3.Cross(tan, Vector3.up).normalized;
                    Vector3 nrm = (lado * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)).normalized;
                    Vector3 bp = pts[i] + nrm * rad[i] * Util.Rng(rnd, 0.85f, 1.05f) + tan * Util.Rng(rnd, -0.15f, 0.15f);
                    Vector3 arriba = (nrm * 0.5f + tan * Util.Rng(rnd, -1f, 1f) + Vector3.Cross(nrm, tan) * Util.Rng(rnd, -1f, 1f)).normalized;
                    Hoja(mh, bp, arriba, nrm, Util.Rng(rnd, 0.45f, 0.7f));
                }
            var go = Util.Objeto(nombre, padre, Util.Guardar(mb.Construir(nombre), nombre), fondo);
            Util.Objeto(nombre + "_Hiedra", go.transform, Util.Guardar(mh.Construir(nombre + "_Hiedra"), nombre + "_Hiedra"), hiedra);
        }
    }
}
