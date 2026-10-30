// Forja3D — mantiene de pie los modelos de Tripo/Blender.
// En los FBX de Blender el nodo raíz del esqueleto ("Armature") guarda la corrección de ejes; algunas
// animaciones lo sobrescriben y el modelo se tumba. Aquí se restaura su postura de reposo tras el Animator:
// el resto de huesos (cadera, piernas, brazos) siguen animándose con normalidad, también la caída al morir.
using System.Collections.Generic;
using UnityEngine;

namespace Forja3D
{
    [DefaultExecutionOrder(1000)]
    public class Forja3DEstabilizador : MonoBehaviour
    {
        readonly List<(Transform t, Vector3 pos, Quaternion rot, Vector3 esc)> nodos = new List<(Transform, Vector3, Quaternion, Vector3)>();

        void Awake()
        {
            // Hijos directos que no son mallas: el/los nodo(s) raíz del esqueleto
            foreach (Transform hijo in transform)
                if (!hijo.GetComponent<Renderer>() && hijo.childCount > 0)
                    nodos.Add((hijo, hijo.localPosition, hijo.localRotation, hijo.localScale));
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.Equals("Hip", System.StringComparison.OrdinalIgnoreCase)) { cadera = t; break; }
        }

        // Los clips de Tripo para bípedos avanzan la cadera ~1,3 m por ciclo de caminar y luego la devuelven de
        // golpe (el jefe parecía andar hacia atrás a tirones). Se fija la cadera en horizontal sobre el origen:
        // la animación queda en el sitio y quien desplaza al jefe es el NavMeshAgent.
        Transform cadera;

        void LateUpdate()
        {
            foreach (var n in nodos)
            {
                if (!n.t) continue;
                n.t.localPosition = n.pos; n.t.localRotation = n.rot; n.t.localScale = n.esc;
            }
            if (cadera)
            {
                var local = transform.InverseTransformPoint(cadera.position);
                cadera.position = transform.TransformPoint(new Vector3(0f, local.y, 0f));
            }
        }
    }
}
