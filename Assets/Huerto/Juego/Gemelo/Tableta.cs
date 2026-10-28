using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Huerto.Juego;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Huerto.Gemelo
{
    /// <summary>
    /// Tableta del gemelo digital (tecla Tab): estado de cada bancal, su clima, el historial de la temporada
    /// y la comparacion entre bancales. Desde aqui se controla el reloj y se riega, cosecha o planta.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class Tableta : MonoBehaviour
    {
        public SimuladorHuerto simulador;
        public Paseante paseante;
        public StyleSheet estilo;

        static readonly CultureInfo es = new CultureInfo("es-ES");
        static readonly float[] Velocidades = { 0.1f, 0.5f, 3f, 15f };
        static readonly string[] NombresVelocidad = { "Lenta", "Normal", "Rápida", "Muy rápida" };

        VisualElement raiz, pantalla, lista, contenido;
        Label fecha, velocidadTxt, mensaje;
        readonly List<Button> pestanas = new List<Button>();
        readonly List<VisualElement> tarjetas = new List<VisualElement>();
        int seleccion, pestana;
        bool abierta, sucio = true;
        float proximoRefresco;

        ParcelaCultivo Sel => simulador.parcelas[Mathf.Clamp(seleccion, 0, simulador.parcelas.Length - 1)];

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            raiz = doc.rootVisualElement;
            raiz.Clear();
            if (estilo != null) raiz.styleSheets.Add(estilo);
            raiz.AddToClassList("raiz");
            Construir();
            AudioHuerto.ConectarInterfaz(raiz);
            Mostrar(false);
            if (simulador != null) simulador.DiaAvanzado += Marcar;
        }

        void OnDisable()
        {
            if (simulador != null) simulador.DiaAvanzado -= Marcar;
            EstadoJuego.TabletaAbierta = false;
        }

        void Marcar() => sucio = true;

        void Update()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            bool alternar = (k != null && k.tabKey.wasPressedThisFrame) || (g != null && g.selectButton.wasPressedThisFrame);
            if (abierta && k != null && k.escapeKey.wasPressedThisFrame) alternar = true;
            if (alternar) Mostrar(!abierta);
            if (abierta && sucio && Time.unscaledTime >= proximoRefresco) { Refrescar(); sucio = false; proximoRefresco = Time.unscaledTime + 0.1f; }
        }

        void Mostrar(bool si)
        {
            if (si != abierta) AudioHuerto.Sonar(si ? Efecto.TabletaAbrir : Efecto.TabletaCerrar, 0.8f);
            abierta = si;
            EstadoJuego.TabletaAbierta = si;
            raiz.style.display = si ? DisplayStyle.Flex : DisplayStyle.None;
            if (si)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
                // abre el bancal que tiene mas cerca el personaje
                if (paseante != null)
                {
                    float mejor = 6f;
                    for (int i = 0; i < simulador.parcelas.Length; i++)
                    {
                        float d = Vector3.Distance(paseante.transform.position, simulador.parcelas[i].transform.position);
                        if (d < mejor) { mejor = d; seleccion = i; }
                    }
                }
                sucio = true; Refrescar();
            }
        }

        // ---------------- Construccion ----------------
        static VisualElement Caja(string clase, VisualElement padre = null)
        {
            var v = new VisualElement(); v.AddToClassList(clase); padre?.Add(v); return v;
        }
        static Label Texto(string t, string clase, VisualElement padre)
        {
            var l = new Label(t); l.AddToClassList(clase); l.enableRichText = true; padre.Add(l); return l;
        }
        static Button Boton(string t, System.Action accion, VisualElement padre, string clase = "boton")
        {
            var b = new Button(accion) { text = t }; b.AddToClassList(clase); padre.Add(b); return b;
        }

        void Construir()
        {
            var fondo = Caja("fondo", raiz);
            var tableta = Caja("tableta", fondo);
            Caja("camara-frontal", tableta);
            pantalla = Caja("pantalla", tableta);

            var barra = Caja("barra", pantalla);
            var marca = Caja("marca", barra);
            Texto("Huerto de los abuelos", "marca-titulo", marca);
            Texto("Gemelo digital · Parcela 39, La Alberca", "marca-sub", marca);
            fecha = Texto("", "fecha", barra);
            var reloj = Caja("reloj", barra);
            Boton("Pausa", () => { simulador.pausado = !simulador.pausado; sucio = true; }, reloj);
            for (int i = 0; i < Velocidades.Length; i++)
            {
                int j = i;
                Boton(NombresVelocidad[i], () => { simulador.velocidad = Velocidades[j]; simulador.pausado = false; sucio = true; }, reloj);
            }
            Boton("+1 semana", () => { simulador.Saltar(7); sucio = true; }, reloj);
            Boton("+1 mes", () => { simulador.Saltar(30); sucio = true; }, reloj);
            Boton("Reiniciar", () => { simulador.Reiniciar(); sucio = true; }, reloj, "boton-aviso");
            velocidadTxt = Texto("", "velocidad", barra);

            var cuerpo = Caja("cuerpo", pantalla);
            lista = new ScrollView(ScrollViewMode.Vertical); lista.AddToClassList("lista"); cuerpo.Add(lista);
            var detalle = Caja("detalle", cuerpo);
            var tabs = Caja("pestanas", detalle);
            string[] nombres = { "Estado", "Clima", "Historial", "Comparar", "Sonido" };
            for (int i = 0; i < nombres.Length; i++)
            {
                int j = i;
                pestanas.Add(Boton(nombres[i], () => { pestana = j; sucio = true; Refrescar(); }, tabs, "pestana"));
            }
            contenido = new ScrollView(ScrollViewMode.Vertical); contenido.AddToClassList("contenido"); detalle.Add(contenido);

            var pie = Caja("pie", pantalla);
            Texto("Tab o Esc: guardar la tableta", "pie-ayuda", pie);
            mensaje = Texto("", "pie-mensaje", pie);
            Boton("Exportar CSV", Exportar, pie);

            for (int i = 0; i < simulador.parcelas.Length; i++)
            {
                int j = i;
                var t = Caja("tarjeta", lista);
                t.RegisterCallback<ClickEvent>(_ => { seleccion = j; sucio = true; Refrescar(); });
                tarjetas.Add(t);
            }
        }

        // ---------------- Refresco ----------------
        static string Pct(float f) => (f * 100f).ToString("0", es) + " %";
        static string Num(float f, string formato = "0.0") => f.ToString(formato, es);
        static Color Col(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        string EstadoCorto(ParcelaCultivo p)
        {
            var m = p.modelo;
            switch (m.situacion)
            {
                case Situacion.Preparada: return "Se planta el " + SimuladorHuerto.Fecha(m.diaSiembra);
                case Situacion.Cosechada: return $"Cosechado: {Num(m.cosechaKg)} kg";
                case Situacion.Muerta: return "Plantas muertas";
                default: return m.espigada ? "Espigada" : m.cultivo.fases[m.Fase].nombre;
            }
        }

        static void Barra(VisualElement padre, float valor, string clase)
        {
            var fondo = Caja("mini-barra", padre);
            var b = Caja(clase, fondo);
            b.style.width = Length.Percent(Mathf.Clamp01(valor) * 100f);
        }

        void Refrescar()
        {
            if (simulador == null || simulador.parcelas == null) return;
            int dia = simulador.Dia;
            fecha.text = $"{SimuladorHuerto.Fecha(dia)}  <size=70%>· día {dia - simulador.diaInicio + 1} de la temporada</size>";
            velocidadTxt.text = simulador.pausado ? "En pausa" : $"{Num(simulador.velocidad, "0.#")} días/s";

            for (int i = 0; i < tarjetas.Count; i++)
            {
                var p = simulador.parcelas[i]; var m = p.modelo; var t = tarjetas[i];
                t.Clear();
                t.EnableInClassList("tarjeta-sel", i == seleccion);
                var franja = Caja("tarjeta-color", t); franja.style.backgroundColor = p.color;
                var col = Caja("tarjeta-cuerpo", t);
                Texto($"<b>{p.numero}</b> · {m.cultivo.nombre}", "tarjeta-titulo", col);
                Texto(EstadoCorto(p), "tarjeta-sub", col);
                Texto(p.clima.nombre, "tarjeta-clima", col);
                Barra(col, m.desarrollo, "barra-desarrollo");
                Barra(col, m.salud / 100f, m.salud > 60 ? "barra-salud" : m.salud > 30 ? "barra-salud-media" : "barra-salud-baja");
                if (m.alertas.Count > 0 && m.Viva) Texto(m.alertas[m.alertas.Count - 1], "tarjeta-alerta", col);
            }
            for (int i = 0; i < pestanas.Count; i++) pestanas[i].EnableInClassList("pestana-sel", i == pestana);

            contenido.Clear();
            switch (pestana)
            {
                case 0: Estado(); break;
                case 1: Clima(); break;
                case 2: Historial(); break;
                case 3: Comparar(); break;
                default: Sonido(); break;
            }
        }

        void Dato(VisualElement padre, string titulo, string valor, string nota = null)
        {
            var d = Caja("dato", padre);
            Texto(titulo, "dato-titulo", d);
            Texto(valor, "dato-valor", d);
            if (nota != null) Texto(nota, "dato-nota", d);
        }

        void Estado()
        {
            var p = Sel; var m = p.modelo; var c = m.cultivo; int dia = simulador.Dia;
            var cab = Caja("cabecera", contenido);
            var punto = Caja("punto", cab); punto.style.backgroundColor = p.color;
            Texto($"Bancal {p.numero} · {c.nombre}", "titulo", cab);
            Texto($"{c.variedad}  ·  {Num(p.Area, "0.#")} m²  ·  clima: {p.clima.nombre}", "subtitulo", contenido);

            var datos = Caja("datos", contenido);
            Dato(datos, "Desarrollo", Pct(m.desarrollo), $"{Num(m.gdd, "0")} de {Num(c.gddTotal, "0")} °C·día");
            Dato(datos, "Vigor", m.desarrollo > 0.05f ? Pct(Mathf.Clamp01(m.vigor / m.desarrollo)) : "—", "crecimiento frente al ideal");
            Dato(datos, "Salud", Pct(m.salud / 100f), m.situacion == Situacion.Muerta ? "plantas muertas" : null);
            Dato(datos, "Humedad del suelo", Pct(m.Humedad), $"raíces a {Num(m.Raiz * 100f, "0")} cm");
            Dato(datos, m.situacion == Situacion.Cosechada ? "Cosecha" : "Cosecha estimada", m.situacion == Situacion.Preparada ? "—" : $"{Num(m.CosechaEstimada)} kg",
                m.cuajado < 0.99f ? $"cuajado {Pct(m.cuajado)}" : $"potencial {Num(c.rendimiento * p.Area, "0")} kg");
            int prev = p.CosechaPrevista();
            Dato(datos, "Cosecha prevista", m.situacion == Situacion.Cosechada ? SimuladorHuerto.Fecha(m.diaCosecha) : prev > 0 ? SimuladorHuerto.Fecha(prev) : "—",
                m.Viva ? $"{dia - m.diaSiembra} días desde la siembra" : null);

            // fases
            var fases = Caja("fases", contenido);
            int actual = m.Viva || m.situacion == Situacion.Muerta ? m.Fase : -1;
            for (int i = 0; i < c.fases.Length; i++)
            {
                var f = Caja("fase", fases);
                f.EnableInClassList("fase-hecha", actual > i || m.situacion == Situacion.Cosechada);
                f.EnableInClassList("fase-actual", actual == i && m.situacion != Situacion.Cosechada);
                Texto(c.fases[i].nombre, "fase-texto", f);
            }

            var dos = Caja("dos-columnas", contenido);
            var hoy = Caja("panel", dos);
            Texto("Hoy en este bancal", "panel-titulo", hoy);
            var h = p.Hoy;
            Texto($"Máxima {Num(h.tmax)} °C   ·   mínima {Num(h.tmin)} °C", "linea", hoy);
            Texto($"Lluvia {Num(h.lluvia)} mm   ·   evapotranspiración {Num(h.et0)} mm", "linea", hoy);
            Texto($"Humedad del aire {Num(h.hr, "0")} %   ·   radiación {Num(h.rad)} MJ/m²", "linea", hoy);
            Texto($"Temporada: lluvia {Num(m.lluviaTotal, "0")} mm · riego {Num(m.riegoTotal, "0")} mm · consumo {Num(m.etTotal, "0")} mm", "linea", hoy);

            var av = Caja("panel", dos);
            Texto("Avisos", "panel-titulo", av);
            int desde = Mathf.Max(0, p.avisos.Count - 7);
            if (p.avisos.Count == 0) Texto("Sin avisos.", "linea", av);
            for (int i = p.avisos.Count - 1; i >= desde; i--)
                Texto($"<b>{SimuladorHuerto.FechaCorta(p.avisos[i].Key)}</b>  {p.avisos[i].Value}", "linea", av);

            var acc = Caja("acciones", contenido);
            Boton("Regar 15 mm", () => { m.Regar(15f); AudioHuerto.Sonar(Efecto.Regar); Mensaje($"Riego de 15 mm en el bancal {p.numero}"); }, acc);
            Boton(m.Viva && m.desarrollo >= 0.5f ? "Cosechar ahora" : "Cosechar (aún no)", () =>
            {
                float kg = m.Cosechar(dia);
                if (kg > 0f) AudioHuerto.Sonar(Efecto.Cosechar);
                Mensaje(kg > 0f ? $"Cosechados {Num(kg)} kg de {c.nombre.ToLower()}" : "Todavía no hay nada que cosechar");
                p.ActualizarVisual(true); sucio = true;
            }, acc);
            var riego = new Toggle("Riego por goteo automático") { value = p.riegoAuto };
            riego.RegisterValueChangedCallback(e => { p.riegoAuto = e.newValue; sucio = true; }); riego.AddToClassList("interruptor"); acc.Add(riego);
            var cosecha = new Toggle("Cosecha automática") { value = p.cosechaAuto };
            cosecha.RegisterValueChangedCallback(e => { p.cosechaAuto = e.newValue; sucio = true; }); cosecha.AddToClassList("interruptor"); acc.Add(cosecha);

            var plantar = Caja("acciones", contenido);
            var opciones = new List<string>(); foreach (var cu in Cultivo.Todos) opciones.Add(cu.nombre);
            var elegir = new DropdownField("Plantar otro cultivo", opciones, System.Array.IndexOf(Cultivo.Todos, c));
            elegir.AddToClassList("desplegable"); plantar.Add(elegir);
            Boton("Plantar", () =>
            {
                var nuevo = Cultivo.Todos[Mathf.Max(0, elegir.index)];
                p.Resembrar(nuevo.id, dia);
                AudioHuerto.Sonar(Efecto.Plantar);
                Mensaje($"Bancal {p.numero}: {nuevo.nombre.ToLower()}" + (p.modelo.diaSiembra > dia ? $" (se planta el {SimuladorHuerto.Fecha(p.modelo.diaSiembra)})" : ""));
                sucio = true;
            }, plantar);
        }

        void Clima()
        {
            var p = Sel; var cl = p.clima;
            Texto($"Clima del bancal {p.numero}", "titulo", contenido);
            Texto("Cada bancal tiene su propia climatología: un año real del huerto (datos diarios 1996-2025) o uno sintético, con cambios encima. Los cambios cuentan desde hoy; lo ya ocurrido no se borra.", "subtitulo", contenido);

            var fila = Caja("acciones", contenido);
            var presets = new List<string>(PerfilClima.Presets);
            var preset = new DropdownField("Escenario", presets, Mathf.Max(0, presets.IndexOf(cl.nombre)));
            preset.AddToClassList("desplegable");
            preset.RegisterValueChangedCallback(e =>
            {
                var nuevo = PerfilClima.Preset(e.newValue, cl.semilla);
                p.clima = nuevo; p.ClimaCambiado(); sucio = true; Refrescar();
            });
            fila.Add(preset);
            var anios = new List<string> { "Sintético" };
            var b = SimuladorHuerto.Base;
            for (int a = b.primerAnio; a <= b.ultimoAnio; a++) anios.Add(a.ToString());
            var anio = new DropdownField("Año de datos", anios, cl.anio == 0 ? 0 : Mathf.Max(0, anios.IndexOf(cl.anio.ToString())));
            anio.AddToClassList("desplegable");
            anio.RegisterValueChangedCallback(e => { cl.anio = e.newValue == "Sintético" ? 0 : int.Parse(e.newValue); cl.nombre = "Personalizado"; p.ClimaCambiado(); sucio = true; Refrescar(); });
            fila.Add(anio);

            var ajustes = Caja("ajustes", contenido);
            Deslizador(ajustes, "Temperatura (°C)", -3f, 5f, cl.deltaT, v => cl.deltaT = v, p, "+0.0;-0.0;0");
            Deslizador(ajustes, "Lluvia (×)", 0f, 2f, cl.factorLluvia, v => cl.factorLluvia = v, p, "0.00");
            Deslizador(ajustes, "Sombra de árboles", 0f, 0.9f, cl.sombra, v => cl.sombra = v, p, "0%");
            var eventos = Caja("acciones", contenido);
            Interruptor(eventos, "Ola de calor (julio)", cl.olaCalor, v => cl.olaCalor = v, p);
            Interruptor(eventos, "Helada tardía (mayo)", cl.heladaTardia, v => cl.heladaTardia = v, p);
            Interruptor(eventos, "Sequía de verano", cl.sequia, v => cl.sequia = v, p);

            // la temporada entera de este bancal
            var g = new Grafica("Temporada en este bancal: máximas, mínimas y lluvia");
            g.AddToClassList("grafica-grande");
            var smax = g.Nueva("Máxima (°C)", Col("#d9534f"), -10f, 42f);
            var smin = g.Nueva("Mínima (°C)", Col("#3a7bd5"), -10f, 42f);
            var sll = g.Nueva("Lluvia (mm)", new Color(0.2f, 0.55f, 0.85f, 0.45f), 0f, 60f, true);
            int ini = simulador.diaInicio, fin = simulador.diaFin;
            float total = 0f; int heladas = 0, calor = 0;
            for (int d = ini; d <= fin; d++)
            {
                var m = p.DiaClima(d);
                smax.puntos.Add(new Vector2(d, m.tmax)); smin.puntos.Add(new Vector2(d, m.tmin)); sll.puntos.Add(new Vector2(d, m.lluvia));
                total += m.lluvia; if (m.tmin < 0f) heladas++; if (m.tmax >= 30f) calor++;
            }
            g.xMin = ini; g.xMax = fin; g.marcaX = simulador.Dia;
            g.Ejes("42 °C", "60 mm", SimuladorHuerto.FechaCorta(ini), SimuladorHuerto.FechaCorta(fin));
            contenido.Add(g);
            Texto($"De {SimuladorHuerto.Fecha(ini)} a {SimuladorHuerto.Fecha(fin)}: {Num(total, "0")} mm de lluvia, {heladas} días de helada y {calor} días de 30 °C o más.", "linea", contenido);
        }

        void Deslizador(VisualElement padre, string titulo, float min, float max, float valor, System.Action<float> poner, ParcelaCultivo p, string formato)
        {
            var fila = Caja("ajuste", padre);
            var s = new Slider(titulo, min, max) { value = valor };
            s.AddToClassList("deslizador");
            var v = Texto(valor.ToString(formato, es), "ajuste-valor", fila);
            s.RegisterValueChangedCallback(e => { poner(e.newValue); p.clima.nombre = "Personalizado"; p.ClimaCambiado(); v.text = e.newValue.ToString(formato, es); });
            s.RegisterCallback<PointerCaptureOutEvent>(_ => { sucio = true; Refrescar(); });
            fila.Insert(0, s);
        }

        void Interruptor(VisualElement padre, string titulo, bool valor, System.Action<bool> poner, ParcelaCultivo p)
        {
            var t = new Toggle(titulo) { value = valor };
            t.AddToClassList("interruptor");
            t.RegisterValueChangedCallback(e => { poner(e.newValue); p.clima.nombre = "Personalizado"; p.ClimaCambiado(); sucio = true; Refrescar(); });
            padre.Add(t);
        }

        void Historial()
        {
            var p = Sel; var h = p.modelo.historia;
            Texto($"Historial del bancal {p.numero} · {p.modelo.cultivo.nombre}", "titulo", contenido);
            if (h.Count == 0) { Texto("Todavía no ha pasado ningún día. Pon en marcha el reloj.", "subtitulo", contenido); return; }
            int ini = simulador.diaInicio, fin = Mathf.Max(simulador.Dia, ini + 10);

            var g1 = new Grafica("Tiempo: temperaturas, lluvia y riego");
            var a = g1.Nueva("Máxima", Col("#d9534f"), -10f, 42f); var bmin = g1.Nueva("Mínima", Col("#3a7bd5"), -10f, 42f);
            var ll = g1.Nueva("Lluvia", new Color(0.2f, 0.55f, 0.85f, 0.5f), 0f, 50f, true); var ri = g1.Nueva("Riego", new Color(0.2f, 0.7f, 0.45f, 0.6f), 0f, 50f, true);
            var g2 = new Grafica("Suelo y planta: humedad y salud");
            var hu = g2.Nueva("Humedad del suelo (%)", Col("#2a7fba"), 0f, 100f); var sa = g2.Nueva("Salud (%)", Col("#3c9d5d"), 0f, 100f);
            var g3 = new Grafica("Crecimiento: desarrollo y vigor");
            var de = g3.Nueva("Desarrollo (%)", Col("#8a6d3b"), 0f, 100f); var vi = g3.Nueva("Vigor (%)", Col("#5cb85c"), 0f, 100f);
            foreach (var r in h)
            {
                a.puntos.Add(new Vector2(r.dia, r.tmax)); bmin.puntos.Add(new Vector2(r.dia, r.tmin)); ll.puntos.Add(new Vector2(r.dia, r.lluvia)); ri.puntos.Add(new Vector2(r.dia, r.riego));
                hu.puntos.Add(new Vector2(r.dia, r.humedad * 100f)); sa.puntos.Add(new Vector2(r.dia, r.salud));
                de.puntos.Add(new Vector2(r.dia, r.desarrollo * 100f)); vi.puntos.Add(new Vector2(r.dia, r.vigor * 100f));
            }
            foreach (var g in new[] { g1, g2, g3 }) { g.xMin = ini; g.xMax = fin; contenido.Add(g); }
            g1.Ejes("42 °C", "50 mm", SimuladorHuerto.FechaCorta(ini), SimuladorHuerto.FechaCorta(fin));
            g2.Ejes("100 %", "", SimuladorHuerto.FechaCorta(ini), SimuladorHuerto.FechaCorta(fin));
            g3.Ejes("100 %", "", SimuladorHuerto.FechaCorta(ini), SimuladorHuerto.FechaCorta(fin));
        }

        void Comparar()
        {
            Texto("Comparación de los bancales", "titulo", contenido);
            var tabla = Caja("tabla", contenido);
            var cab = Caja("tabla-fila", tabla); cab.AddToClassList("tabla-cabecera");
            foreach (var t in new[] { "Bancal", "Cultivo", "Clima", "Estado", "Desarrollo", "Salud", "Humedad", "Cosecha (kg)" }) Texto(t, "tabla-celda", cab);
            float maxKg = 0.1f;
            foreach (var p in simulador.parcelas) maxKg = Mathf.Max(maxKg, p.modelo.CosechaEstimada, p.modelo.cultivo.rendimiento * p.Area);
            foreach (var p in simulador.parcelas)
            {
                var m = p.modelo;
                var f = Caja("tabla-fila", tabla);
                var c0 = Texto($"<color=#{ColorUtility.ToHtmlStringRGB(p.color)}><b>—</b></color> {p.numero}", "tabla-celda", f);
                Texto(m.cultivo.nombre, "tabla-celda", f);
                Texto(p.clima.nombre, "tabla-celda", f);
                Texto(EstadoCorto(p), "tabla-celda", f);
                Texto(Pct(m.desarrollo), "tabla-celda", f);
                Texto(Pct(m.salud / 100f), "tabla-celda", f);
                Texto(Pct(m.Humedad), "tabla-celda", f);
                var kg = Caja("tabla-celda", f);
                Texto(Num(m.CosechaEstimada) + (m.situacion == Situacion.Cosechada ? " (cosechado)" : ""), "", kg);
                Barra(kg, m.CosechaEstimada / maxKg, "barra-desarrollo");
            }
            int ini = simulador.diaInicio, fin = Mathf.Max(simulador.Dia, ini + 10);
            var g = new Grafica("Desarrollo de cada bancal (%)");
            var g2 = new Grafica("Salud de cada bancal (%)");
            foreach (var p in simulador.parcelas)
            {
                var s = g.Nueva(p.numero + " " + p.modelo.cultivo.nombre, p.color, 0f, 100f);
                var s2 = g2.Nueva(p.numero + " " + p.modelo.cultivo.nombre, p.color, 0f, 100f);
                foreach (var r in p.modelo.historia) { s.puntos.Add(new Vector2(r.dia, r.desarrollo * 100f)); s2.puntos.Add(new Vector2(r.dia, r.salud)); }
            }
            foreach (var gr in new[] { g, g2 }) { gr.xMin = ini; gr.xMax = fin; gr.Ejes("100 %", "", SimuladorHuerto.FechaCorta(ini), SimuladorHuerto.FechaCorta(fin)); contenido.Add(gr); }
        }

        void Mensaje(string t) { mensaje.text = t; sucio = true; }

        // ---------------- Exportar ----------------
        void Sonido()
        {
            var dos = Caja("dos-columnas", contenido);
            var vol = Caja("panel", dos);
            Texto("Volumen", "panel-titulo", vol);
            Volumen(vol, "Música", AudioHuerto.VolMusica, v => AudioHuerto.VolMusica = v);
            Volumen(vol, "Ambiente", AudioHuerto.VolAmbiente, v => AudioHuerto.VolAmbiente = v);
            Volumen(vol, "Efectos", AudioHuerto.VolEfectos, v => AudioHuerto.VolEfectos = v);
            var acc = Caja("acciones", vol);
            var silencio = new Toggle("Sin música (tecla M)") { value = AudioHuerto.MusicaSilenciada };
            silencio.RegisterValueChangedCallback(e => { AudioHuerto.MusicaSilenciada = e.newValue; AudioHuerto.GuardarVolumenes(); sucio = true; });
            silencio.AddToClassList("interruptor"); acc.Add(silencio);
            Boton("Siguiente tema", () => { AudioHuerto.Instancia?.SiguienteTema(); sucio = true; }, acc);

            var info = Caja("panel", dos);
            Texto("Qué suena", "panel-titulo", info);
            Texto($"<b>Tema:</b> {(AudioHuerto.Instancia != null ? AudioHuerto.Instancia.TemaActual : "—")}", "linea", info);
            Texto("Música compuesta y sintetizada para el proyecto: vals con guitarra y flauta, una tarde con celesta y una pieza de guitarra sola.", "linea", info);
            Texto("El ambiente sigue al gemelo: el cuco canta de abril a junio, las chicharras con el calor del verano y cada bancal suena a lluvia cuando llueve en su clima.", "linea", info);
            Texto("Las pisadas cambian con el suelo: hierba, tierra, hojarasca o piedra. De vez en cuando tocan las campanas del pueblo.", "linea", info);
        }

        void Volumen(VisualElement padre, string titulo, float valor, System.Action<float> poner)
        {
            var fila = Caja("ajuste", padre);
            var s = new Slider(titulo, 0f, 1f) { value = valor };
            s.AddToClassList("deslizador");
            var v = Texto(Pct(valor), "ajuste-valor", fila);
            s.RegisterValueChangedCallback(e => { poner(e.newValue); v.text = Pct(e.newValue); });
            s.RegisterCallback<PointerCaptureOutEvent>(_ => AudioHuerto.GuardarVolumenes());
            fila.Insert(0, s);
        }

        void Exportar()
        {
            var sb = new StringBuilder();
            sb.AppendLine("bancal;cultivo;clima;fecha;dia;tmax;tmin;lluvia_mm;riego_mm;et_mm;humedad_suelo;salud;desarrollo;vigor;fase;situacion");
            var inv = CultureInfo.InvariantCulture;
            foreach (var p in simulador.parcelas)
                foreach (var r in p.modelo.historia)
                    sb.AppendLine(string.Join(";", p.numero, p.modelo.cultivo.nombre, p.clima.nombre, new System.DateTime(2021, 1, 1).AddDays(r.dia).ToString("MM-dd"), r.dia,
                        r.tmax.ToString("0.0", inv), r.tmin.ToString("0.0", inv), r.lluvia.ToString("0.0", inv), r.riego.ToString("0.0", inv), r.et.ToString("0.00", inv),
                        r.humedad.ToString("0.000", inv), r.salud.ToString("0.0", inv), r.desarrollo.ToString("0.000", inv), r.vigor.ToString("0.000", inv),
                        r.fase >= 0 ? p.modelo.cultivo.fases[r.fase].nombre : "", r.situacion));
            string ruta = System.IO.Path.Combine(Application.persistentDataPath, $"Huerto_temporada_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
            Mensaje("Guardado en " + ruta);
            AudioHuerto.Sonar(Efecto.AvisoBueno, 0.6f);
            Debug.Log("Huerto: historial exportado a " + ruta);
        }
    }
}
