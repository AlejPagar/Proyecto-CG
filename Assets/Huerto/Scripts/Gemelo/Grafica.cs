using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Huerto.Gemelo
{
    /// <summary>Grafica sencilla para la tableta: lineas y barras sobre un eje de dias, con escala propia por serie.</summary>
    public class Grafica : VisualElement
    {
        public class Serie
        {
            public string nombre; public Color color; public bool barras; public float min, max; public float grosor = 2f;
            public readonly List<Vector2> puntos = new List<Vector2>();   // (dia, valor)
        }

        public readonly List<Serie> series = new List<Serie>();
        public float xMin, xMax = 1f;
        public float marcaX = float.NaN;    // linea vertical (hoy)
        readonly Label titulo, leyenda, ejeIzq, ejeDer, ejeX0, ejeX1;

        public Grafica(string tituloTexto)
        {
            AddToClassList("grafica");
            generateVisualContent += Dibujar;
            titulo = new Label(tituloTexto); titulo.AddToClassList("grafica-titulo"); Add(titulo);
            leyenda = new Label(); leyenda.AddToClassList("grafica-leyenda"); leyenda.enableRichText = true; Add(leyenda);
            ejeIzq = Etiqueta("grafica-eje-izq"); ejeDer = Etiqueta("grafica-eje-der"); ejeX0 = Etiqueta("grafica-eje-x0"); ejeX1 = Etiqueta("grafica-eje-x1");
        }

        Label Etiqueta(string clase) { var l = new Label(); l.AddToClassList(clase); l.enableRichText = true; Add(l); return l; }

        public Serie Nueva(string nombre, Color color, float min, float max, bool barras = false)
        {
            var s = new Serie { nombre = nombre, color = color, min = min, max = max, barras = barras };
            series.Add(s);
            return s;
        }

        public void Limpiar() { series.Clear(); marcaX = float.NaN; }

        public void Ejes(string izq, string der, string x0, string x1)
        {
            ejeIzq.text = izq; ejeDer.text = der; ejeX0.text = x0; ejeX1.text = x1;
            var sb = new System.Text.StringBuilder();
            foreach (var s in series) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(s.color)}><b>—</b></color> {s.nombre}   ");
            leyenda.text = sb.ToString();
            MarkDirtyRepaint();
        }

        const float MargenIzq = 44f, MargenDer = 44f, MargenArriba = 30f, MargenAbajo = 22f;

        void Dibujar(MeshGenerationContext mgc)
        {
            var r = contentRect;
            float x0 = MargenIzq, x1 = r.width - MargenDer, y0 = MargenArriba, y1 = r.height - MargenAbajo;
            if (x1 - x0 < 10f || y1 - y0 < 10f) return;
            var p = mgc.painter2D;
            // rejilla
            p.lineWidth = 1f; p.strokeColor = new Color(0f, 0f, 0f, 0.08f);
            for (int i = 0; i <= 4; i++)
            {
                float y = Mathf.Lerp(y0, y1, i / 4f);
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
            }
            float X(float d) => Mathf.Lerp(x0, x1, Mathf.InverseLerp(xMin, xMax, d));
            float anchoBarra = Mathf.Max(1f, (x1 - x0) / Mathf.Max(1f, xMax - xMin) * 0.8f);
            foreach (var s in series)
            {
                if (s.puntos.Count == 0) continue;
                float Y(float v) => Mathf.Lerp(y1, y0, Mathf.InverseLerp(s.min, s.max, v));
                if (s.barras)
                {
                    p.fillColor = s.color;
                    foreach (var q in s.puntos)
                    {
                        if (q.y <= s.min) continue;
                        float xx = X(q.x), yy = Y(Mathf.Min(q.y, s.max));
                        p.BeginPath();
                        p.MoveTo(new Vector2(xx - anchoBarra * 0.5f, y1)); p.LineTo(new Vector2(xx + anchoBarra * 0.5f, y1));
                        p.LineTo(new Vector2(xx + anchoBarra * 0.5f, yy)); p.LineTo(new Vector2(xx - anchoBarra * 0.5f, yy));
                        p.ClosePath(); p.Fill();
                    }
                }
                else
                {
                    p.strokeColor = s.color; p.lineWidth = s.grosor; p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    for (int i = 0; i < s.puntos.Count; i++)
                    {
                        var q = new Vector2(X(s.puntos[i].x), Y(Mathf.Clamp(s.puntos[i].y, s.min, s.max)));
                        if (i == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.Stroke();
                }
            }
            if (!float.IsNaN(marcaX) && marcaX >= xMin && marcaX <= xMax)
            {
                p.strokeColor = new Color(0.85f, 0.25f, 0.2f, 0.8f); p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(new Vector2(X(marcaX), y0)); p.LineTo(new Vector2(X(marcaX), y1)); p.Stroke();
            }
            // marco
            p.strokeColor = new Color(0f, 0f, 0f, 0.25f); p.lineWidth = 1f;
            p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x0, y1)); p.LineTo(new Vector2(x1, y1)); p.Stroke();
        }
    }
}
