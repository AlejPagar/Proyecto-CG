using UnityEngine;
using UnityEngine.InputSystem;

namespace Huerto.Juego
{
    /// <summary>
    /// Camara en tercera persona que orbita detras del personaje (o en sus ojos con V).
    /// Se acerca sola si un muro, una casa o el terreno se interponen. Limites: distancia 4,5 m e inclinacion 40 grados,
    /// los mismos que usa el calculo de la cuenca visual para decidir que parte del pueblo se genera.
    /// </summary>
    public class CamaraPaseante : MonoBehaviour
    {
        public Paseante objetivo;
        public float alturaPivote = 1.55f;
        public float distancia = 3.2f, distanciaMin = 1.2f, distanciaMax = 4.5f;
        public float inclinacionMin = -35f, inclinacionMax = 40f;
        public float sensibilidad = 0.12f;

        float rumbo, inclinacion = 12f, distActual;
        Vector3 pivote;
        Renderer[] cabeza;

        void Start()
        {
            rumbo = objetivo.cuerpo.eulerAngles.y;
            pivote = objetivo.transform.position + Vector3.up * alturaPivote;
            distActual = distancia;
            // en primera persona la cabeza y el sombrero solo proyectan sombra
            cabeza = objetivo.rendCabeza != null && objetivo.rendCabeza.Length > 0 ? objetivo.rendCabeza : objetivo.cabeza.GetComponentsInChildren<Renderer>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var raton = Mouse.current; var k = Keyboard.current; var g = Gamepad.current;
            bool tableta = EstadoJuego.TabletaAbierta;
            if (raton != null && raton.leftButton.wasPressedThisFrame && !tableta) Cursor.lockState = CursorLockMode.Locked;
            if (k != null && k.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            bool capturado = Cursor.lockState == CursorLockMode.Locked;
            if (raton != null && !tableta && (capturado || raton.rightButton.isPressed))
            {
                Vector2 d = raton.delta.ReadValue() * sensibilidad;
                rumbo += d.x; inclinacion -= d.y;
            }
            if (g != null && !tableta)
            {
                Vector2 d = g.rightStick.ReadValue() * 140f * dt;
                rumbo += d.x; inclinacion -= d.y;
            }
            if (k != null && k.vKey.wasPressedThisFrame)
            {
                objetivo.PrimeraPersona = !objetivo.PrimeraPersona;
                foreach (var r in cabeza) r.shadowCastingMode = objetivo.PrimeraPersona ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (raton != null && !tableta)
            {
                // cada muesca de la rueda acerca o aleja 35 cm (el valor bruto depende de la plataforma)
                float rueda = raton.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f) distancia = Mathf.Clamp(distancia - Mathf.Sign(rueda) * 0.35f, distanciaMin, distanciaMax);
            }

            bool pp = objetivo.PrimeraPersona;
            inclinacion = Mathf.Clamp(inclinacion, pp ? -75f : inclinacionMin, pp ? 75f : inclinacionMax);
            var rot = Quaternion.Euler(inclinacion, rumbo, 0);

            if (pp)
            {
                // ojos: delante de la cabeza para no ver el interior
                Vector3 ojos = objetivo.cabeza.position + Vector3.up * 0.11f + objetivo.cuerpo.forward * 0.09f;
                transform.SetPositionAndRotation(ojos, rot);
                return;
            }

            Vector3 deseado = objetivo.transform.position + Vector3.up * alturaPivote;
            pivote = Vector3.Lerp(pivote, deseado, 1f - Mathf.Exp(-12f * dt));
            float dist = distancia;
            // la camara no atraviesa muros, casas ni terreno (el personaje y los limites invisibles estan en Ignore Raycast)
            if (Physics.SphereCast(pivote, 0.2f, -(rot * Vector3.forward), out var hit, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.3f, hit.distance - 0.05f);
            distActual = dist < distActual ? dist : Mathf.Lerp(distActual, dist, 1f - Mathf.Exp(-4f * dt));
            transform.SetPositionAndRotation(pivote - rot * Vector3.forward * distActual, rot);
        }
    }
}
