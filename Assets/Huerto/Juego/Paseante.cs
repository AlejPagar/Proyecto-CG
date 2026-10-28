using UnityEngine;
using UnityEngine.InputSystem;

namespace Huerto.Juego
{
    /// <summary>
    /// Personaje que recorre el huerto a escala real (1,72 m): anda, corre, salta y se encarama a los muros de los bancales.
    /// La animacion es procedural: el ciclo de paso avanza con la distancia recorrida, asi los pies no patinan.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Paseante : MonoBehaviour
    {
        [Header("Movimiento (velocidades reales, m/s)")]
        public float velAndar = 1.35f;
        public float velCorrer = 3.4f;
        public float alturaSalto = 0.4f;
        public float alturaMaxTrepar = 1.6f;
        public float gravedad = 9.81f;

        [Header("Esqueleto")]
        public Transform cuerpo;
        public Transform cadera, torso, cabeza;
        public Transform hombroI, hombroD, codoI, codoD;
        public Transform musloI, musloD, rodillaI, rodillaD, tobilloI, tobilloD;

        [Header("Camara")]
        public Transform camara;

        [Header("Tableta en la mano (se ve con la tableta abierta)")]
        public GameObject tabletaMano;

        CharacterController cc;
        Vector3 velocidad;          // horizontal
        float velVertical, fase, empuje, tiempo;
        Vector3 caderaBase;
        bool trepando; Vector3 trepIni, trepFin; float trepT, trepDur;
        float mostrarAyuda = 14f;

        /// <summary>Cada vez que un pie toca el suelo (con la rapidez en m/s), al saltar, al caer (velocidad de caida) y al encaramarse.</summary>
        public event System.Action<float> Pisada, Aterrizaje;
        public event System.Action Salto, Trepada;
        int ultimoPaso;
        bool estabaEnSuelo = true;

        public bool PrimeraPersona { get; set; }
        public float Rapidez => new Vector3(velocidad.x, 0, velocidad.z).magnitude;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            caderaBase = cadera.localPosition;
        }

        Vector2 LeerMovimiento(out bool correr, out bool saltar)
        {
            Vector2 m = Vector2.zero; correr = false; saltar = false;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) m.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) m.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) m.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) m.x -= 1;
                correr = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                saltar = k.spaceKey.wasPressedThisFrame;
                if (k.hKey.wasPressedThisFrame || k.f1Key.wasPressedThisFrame) mostrarAyuda = mostrarAyuda > 0 ? 0 : 9999f;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                m += g.leftStick.ReadValue();
                correr |= g.leftStickButton.isPressed || g.rightTrigger.ReadValue() > 0.5f;
                saltar |= g.buttonSouth.wasPressedThisFrame;
            }
            return Vector2.ClampMagnitude(m, 1f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            tiempo += dt; mostrarAyuda -= dt;
            if (trepando) { Trepar(dt); Animar(dt); return; }

            var entrada = LeerMovimiento(out bool correr, out bool saltar);
            if (EstadoJuego.TabletaAbierta) { entrada = Vector2.zero; correr = saltar = false; }
            if (tabletaMano != null && tabletaMano.activeSelf != EstadoJuego.TabletaAbierta) tabletaMano.SetActive(EstadoJuego.TabletaAbierta);
            // direccion relativa a la camara
            Vector3 adelante = camara != null ? Vector3.ProjectOnPlane(camara.forward, Vector3.up).normalized : transform.forward;
            if (adelante.sqrMagnitude < 0.01f) adelante = cuerpo.forward;
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
            Vector3 deseo = (adelante * entrada.y + derecha * entrada.x);
            float objetivo = deseo.magnitude * (correr ? velCorrer : velAndar);
            Vector3 velObjetivo = deseo.sqrMagnitude > 0.001f ? deseo.normalized * objetivo : Vector3.zero;
            float acel = cc.isGrounded ? (velObjetivo.magnitude > velocidad.magnitude ? 5f : 8f) : 1.5f;
            velocidad = Vector3.MoveTowards(velocidad, velObjetivo, acel * dt);

            // girar el cuerpo hacia donde anda (en primera persona, hacia donde mira la camara)
            Vector3 mirar = PrimeraPersona ? adelante : velObjetivo;
            if (mirar.sqrMagnitude > 0.001f)
                cuerpo.rotation = Quaternion.RotateTowards(cuerpo.rotation, Quaternion.LookRotation(mirar), 420f * dt);

            if (cc.isGrounded)
            {
                if (!estabaEnSuelo && velVertical < -3.5f) Aterrizaje?.Invoke(-velVertical);
                velVertical = -2f;
                if (saltar) { velVertical = Mathf.Sqrt(2f * gravedad * alturaSalto); Salto?.Invoke(); }
            }
            else velVertical -= gravedad * dt;

            estabaEnSuelo = cc.isGrounded;
            var flags = cc.Move((velocidad + Vector3.up * velVertical) * dt);
            // contra un muro: si se sigue empujando un momento, se encarama
            if ((flags & CollisionFlags.Sides) != 0 && entrada.magnitude > 0.5f && cc.isGrounded) empuje += dt; else empuje = 0f;
            if (empuje > 0.25f) { empuje = 0f; IntentarTrepar(velObjetivo.sqrMagnitude > 0.01f ? velObjetivo.normalized : cuerpo.forward); }
            Animar(dt);
        }

        // ---------------- Encaramarse a los muros ----------------
        void IntentarTrepar(Vector3 dir)
        {
            Vector3 p = transform.position;
            if (!Physics.Raycast(p + Vector3.up * 0.35f, dir, out var muro, cc.radius + 0.5f)) return;
            Vector3 sonda = muro.point + dir * (cc.radius + 0.15f) + Vector3.up * (alturaMaxTrepar + 0.4f);
            if (!Physics.Raycast(sonda, Vector3.down, out var arriba, alturaMaxTrepar + 0.4f)) return;
            float alto = arriba.point.y - p.y;
            if (alto <= cc.stepOffset || alto > alturaMaxTrepar || arriba.normal.y < 0.7f) return;
            // que quepa arriba y que no se salga del huerto (los limites invisibles estan en la capa Ignore Raycast)
            Vector3 fin = arriba.point + Vector3.up * 0.02f;
            if (Ocupado(fin)) return;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * (alto + 0.3f), dir, Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(fin.x, 0, fin.z)) + 0.3f, 1 << 2, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponent<LimiteHuerto>() != null) return;
            trepando = true; trepIni = p; trepFin = fin; trepT = 0f; trepDur = 0.45f + alto * 0.45f;
            cc.enabled = false; velocidad = Vector3.zero; velVertical = 0f;
            Trepada?.Invoke();
            cuerpo.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
        }

        bool Ocupado(Vector3 fin)
        {
            foreach (var c in Physics.OverlapCapsule(fin + Vector3.up * (cc.radius + 0.05f), fin + Vector3.up * (cc.height - cc.radius), cc.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                if (c != cc) return true;
            return false;
        }

        void Trepar(float dt)
        {
            trepT += dt / trepDur;
            float t = Mathf.Clamp01(trepT);
            // primero sube (manos en el borde) y luego pasa el cuerpo por encima
            float sube = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.65f));
            float avanza = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.35f) / 0.65f));
            Vector3 p = new Vector3(Mathf.Lerp(trepIni.x, trepFin.x, avanza), Mathf.Lerp(trepIni.y, trepFin.y + 0.05f, sube), Mathf.Lerp(trepIni.z, trepFin.z, avanza));
            transform.position = p;
            if (t >= 1f) { trepando = false; cc.enabled = true; }
        }

        // ---------------- Animacion procedural ----------------
        static float Suave(float k, float dt) => 1f - Mathf.Exp(-k * dt);

        void Animar(float dt)
        {
            float v = trepando ? 0f : Rapidez;
            bool suelo = trepando || cc.isGrounded;
            float andar = Mathf.Clamp01(v / velAndar), correr = Mathf.Clamp01((v - velAndar) / (velCorrer - velAndar));
            // un paso (medio ciclo) cada 0,35 + 0,25 * v metros: 0,69 m andando, 1,2 m corriendo
            float zancada = 0.35f + 0.25f * v;
            fase += v * dt / zancada * Mathf.PI;
            float s = Mathf.Sin(fase), c = Mathf.Cos(fase);
            // el pie apoya con la pierna mas adelantada (|sen| = 1)
            int paso = Mathf.FloorToInt((fase - Mathf.PI * 0.5f) / Mathf.PI);
            if (paso != ultimoPaso) { ultimoPaso = paso; if (suelo && !trepando && v > 0.25f) Pisada?.Invoke(v); }

            float muslo = 24f * andar + 22f * correr, rodilla = 30f * andar + 50f * correr, brazo = 16f * andar + 34f * correr, codo = 12f + 70f * correr;
            float inclina = 3f * andar + 9f * correr;
            float respira = Mathf.Sin(tiempo * 1.6f) * 1.2f * (1f - andar);

            Quaternion mI = Quaternion.Euler(-muslo * s, 0, 0), mD = Quaternion.Euler(muslo * s, 0, 0);
            Quaternion rI = Quaternion.Euler(4f + rodilla * Mathf.Max(0, c), 0, 0), rD = Quaternion.Euler(4f + rodilla * Mathf.Max(0, -c), 0, 0);
            Quaternion hI = Quaternion.Euler(brazo * s, 0, -5f), hD = Quaternion.Euler(-brazo * s, 0, 5f);
            Quaternion cI = Quaternion.Euler(-codo, 0, 0), cD = Quaternion.Euler(-codo, 0, 0);
            Quaternion tor = Quaternion.Euler(inclina + respira, -6f * s * andar, 0), cab = Quaternion.Euler(-inclina * 0.6f - respira * 0.5f, 3f * s * andar, 0);
            Vector3 cadPos = caderaBase + Vector3.up * ((0.022f * Mathf.Cos(2f * fase) - 0.01f) * andar + 0.03f * correr * Mathf.Abs(c) - 0.04f * correr);
            Quaternion cad = Quaternion.Euler(0, 5f * s * andar, 2f * c * andar);

            if (!suelo)
            {
                // en el aire: piernas recogidas
                mI = Quaternion.Euler(-35f, 0, 0); mD = Quaternion.Euler(-15f, 0, 0);
                rI = Quaternion.Euler(60f, 0, 0); rD = Quaternion.Euler(40f, 0, 0);
                hI = Quaternion.Euler(-30f, 0, -25f); hD = Quaternion.Euler(-30f, 0, 25f);
            }
            if (EstadoJuego.TabletaAbierta && suelo)
            {
                // sujeta la tableta con las dos manos delante del pecho y la mira
                hI = Quaternion.Euler(-44f, 0, 6f); hD = Quaternion.Euler(-44f, 0, -6f);
                cI = Quaternion.Euler(-62f, 30f, 0); cD = Quaternion.Euler(-62f, -30f, 0);
                cab = Quaternion.Euler(24f, 0, 0); tor = Quaternion.Euler(4f + respira, 0, 0);
            }
            if (trepando)
            {
                // manos al borde del muro y una rodilla arriba
                float t = Mathf.Clamp01(trepT);
                float brazos = Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI);
                hI = hD = Quaternion.Euler(-150f * brazos, 0, 0);
                cI = cD = Quaternion.Euler(-30f * brazos, 0, 0);
                mI = Quaternion.Euler(-80f * Mathf.Sin(t * Mathf.PI), 0, 0); rI = Quaternion.Euler(100f * Mathf.Sin(t * Mathf.PI), 0, 0);
                mD = Quaternion.Euler(-30f * Mathf.Sin(Mathf.Clamp01(t * 1.3f - 0.3f) * Mathf.PI), 0, 0); rD = Quaternion.Euler(70f * Mathf.Sin(Mathf.Clamp01(t * 1.3f - 0.3f) * Mathf.PI), 0, 0);
                tor = Quaternion.Euler(25f * brazos, 0, 0);
            }

            float k = Suave(trepando ? 25f : 14f, dt);
            musloI.localRotation = Quaternion.Slerp(musloI.localRotation, mI, k);
            musloD.localRotation = Quaternion.Slerp(musloD.localRotation, mD, k);
            rodillaI.localRotation = Quaternion.Slerp(rodillaI.localRotation, rI, k);
            rodillaD.localRotation = Quaternion.Slerp(rodillaD.localRotation, rD, k);
            hombroI.localRotation = Quaternion.Slerp(hombroI.localRotation, hI, k);
            hombroD.localRotation = Quaternion.Slerp(hombroD.localRotation, hD, k);
            codoI.localRotation = Quaternion.Slerp(codoI.localRotation, cI, k);
            codoD.localRotation = Quaternion.Slerp(codoD.localRotation, cD, k);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, tor, k);
            cabeza.localRotation = Quaternion.Slerp(cabeza.localRotation, cab, k);
            cadera.localRotation = Quaternion.Slerp(cadera.localRotation, cad, k);
            cadera.localPosition = Vector3.Lerp(cadera.localPosition, cadPos, k);
            // el pie acompana al suelo: punta arriba al adelantar la pierna, talon arriba al despegar
            tobilloI.localRotation = Quaternion.Slerp(tobilloI.localRotation, Quaternion.Euler(-10f * andar * s, 0, 0), k);
            tobilloD.localRotation = Quaternion.Slerp(tobilloD.localRotation, Quaternion.Euler(10f * andar * s, 0, 0), k);
        }

        void OnGUI()
        {
            if (mostrarAyuda <= 0f || EstadoJuego.TabletaAbierta) return;
            var estilo = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, padding = new RectOffset(12, 12, 10, 10) };
            estilo.normal.textColor = Color.white;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(mostrarAyuda));
            GUI.Box(new Rect(16, 16, 340, 190),
                "WASD / flechas: andar\nShift: correr    Espacio: saltar\nEmpujar contra un muro: encaramarse\nRaton: mirar (clic para capturar, Esc suelta)\nRueda: acercar / alejar    V: primera persona\nTab: tableta del gemelo digital\nM: quitar o poner la musica\nH: mostrar u ocultar esta ayuda", estilo);
        }
    }
}
