using UnityEngine;

namespace Huerto.Juego
{
    /// <summary>
    /// Anima al agricultor (modelo humanoide de la Asset Store): mezcla reposo, andar y correr segun la rapidez del Paseante,
    /// con la cadencia ajustada para que cada pisada caiga cuando suena el paso. Con la tableta abierta usa la animacion de
    /// Mixamo, pone las manos en sus bordes por IK y mira la pantalla; al encaramarse a un muro usa la de escalar de Mixamo,
    /// ajustada a lo que dura la subida, con las manos en el borde.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimacionAgricultor : MonoBehaviour
    {
        public Paseante paseante;
        public Transform tableta;
        [Header("Duracion de un ciclo (dos pasos) de cada animacion, s")]
        public float cicloAndar = 1.17f;
        public float cicloCorrer = 0.67f;
        [Tooltip("Duracion de la animacion de encaramarse; 0 si no hay animacion")]
        public float duracionTrepar;

        static readonly int Velocidad = Animator.StringToHash("Velocidad");
        Animator animador;
        float pesoTableta, pesoMuro;
        static readonly int Locomocion = Animator.StringToHash("Locomocion"), EstadoTableta = Animator.StringToHash("Tableta"), EstadoTrepar = Animator.StringToHash("Trepar");
        int estado;

        void Awake()
        {
            animador = GetComponent<Animator>();
            animador.applyRootMotion = false;
        }

        void Update()
        {
            if (paseante == null) return;
            float dt = Time.deltaTime, v = paseante.Rapidez;
            animador.SetFloat(Velocidad, v, 0.08f, dt);
            // dos pasos por ciclo de animacion; el Paseante da un paso cada Zancada(v) metros
            float w = Mathf.InverseLerp(paseante.velAndar, paseante.velCorrer, v);
            float ciclo = Mathf.Lerp(cicloAndar, cicloCorrer, w);
            animador.speed = v < 0.25f ? 1f : Mathf.Clamp(v * ciclo / (2f * paseante.Zancada(v)), 0.6f, 1.8f);

            bool conTableta = EstadoJuego.TabletaAbierta && tableta != null && tableta.gameObject.activeInHierarchy;
            pesoTableta = Mathf.MoveTowards(pesoTableta, conTableta ? 1f : 0f, dt * 4f);
            // con animacion de escalar, las manos las lleva ella; el IK al borde solo hace falta sin ella
            pesoMuro = Mathf.MoveTowards(pesoMuro, paseante.Trepando && paseante.TrepT < 0.55f && duracionTrepar <= 0f ? 1f : 0f, dt * 6f);

            int quiere = paseante.Trepando && animador.HasState(0, EstadoTrepar) ? EstadoTrepar
                : conTableta && animador.HasState(0, EstadoTableta) ? EstadoTableta : Locomocion;
            if (quiere != estado)
            {
                if (estado != 0 || quiere != Locomocion) animador.CrossFadeInFixedTime(quiere, quiere == EstadoTrepar ? 0.1f : 0.3f, 0, 0f);
                estado = quiere;
            }
            if (estado == EstadoTableta) animador.speed = 1f;
            else if (estado == EstadoTrepar && duracionTrepar > 0f) animador.speed = duracionTrepar / paseante.TrepDur;
        }

        void OnAnimatorIK(int capa)
        {
            float peso = Mathf.Max(pesoTableta, pesoMuro);
            animador.SetIKPositionWeight(AvatarIKGoal.LeftHand, peso);
            animador.SetIKPositionWeight(AvatarIKGoal.RightHand, peso);
            animador.SetLookAtWeight(pesoTableta, 0.15f, 0.8f, 0f, 0.6f);
            if (peso <= 0f) return;

            Vector3 izq, der;
            if (pesoTableta >= pesoMuro)
            {
                // sujeta la tableta por los bordes laterales, un poco por debajo del centro
                Vector3 lado = tableta.right * 0.115f, abajo = -tableta.up * 0.02f;
                izq = tableta.position - lado + abajo; der = tableta.position + lado + abajo;
                animador.SetLookAtPosition(tableta.position);
            }
            else
            {
                // manos en el borde del muro, a la anchura de los hombros
                Vector3 lado = paseante.cuerpo.right * 0.22f, borde = paseante.Agarre + Vector3.up * 0.04f;
                izq = borde - lado; der = borde + lado;
            }
            animador.SetIKPosition(AvatarIKGoal.LeftHand, izq);
            animador.SetIKPosition(AvatarIKGoal.RightHand, der);
        }
    }
}
