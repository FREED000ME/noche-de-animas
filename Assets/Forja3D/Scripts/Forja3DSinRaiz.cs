using UnityEngine;

namespace Forja3D
{
    // Descarta el desplazamiento propio de las animaciones (root motion). Con "Apply Root Motion" apagado,
    // Unity sigue moviendo el CUERPO con el clip (una voltereta o una reacción al golpe lo alejan 1-3 m del
    // CharacterController y luego "vuelve" de golpe). Con OnAnimatorMove presente el movimiento queda a cargo
    // del script, y aquí simplemente no se aplica: la animación se reproduce en el sitio y quien mueve al
    // personaje es el CharacterController o el NavMeshAgent.
    // (Válido igual en Android, PC e iOS.)
    [DisallowMultipleComponent]
    public class Forja3DSinRaiz : MonoBehaviour
    {
        public static void Poner(Animator a)
        {
            if (a && !a.GetComponent<Forja3DSinRaiz>()) a.gameObject.AddComponent<Forja3DSinRaiz>();
        }

        void OnAnimatorMove() { }
    }
}
