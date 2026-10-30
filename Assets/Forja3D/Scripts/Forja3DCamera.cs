// Forja3D — cámara orbital en tercera persona (ratón con clic derecho o arrastre táctil).
using UnityEngine;

// EXPANSIÓN FUTURA (PC/consolas): con mando, la cámara recibe el stick derecho a través de
// Forja3DInput.Mirar; puede convenir una opción "invertir eje Y" y sensibilidad configurable en un menú.
namespace Forja3D
{
    public class Forja3DCamera : MonoBehaviour
    {
        public Transform objetivo;
        public float distancia = 5f;
        public float altura = 1.5f;
        public float sensibilidad = 1f;
        public Vector2 limitesPitch = new Vector2(-10f, 70f);
        float yaw, pitch = 18f;
        float tiembla, tiemblaHasta, tiemblaDur = 1f;
        Vector3 suave; bool iniciado;

        // Temblor de cámara (golpes fuertes, jefes)
        public void Temblar(float fuerza, float duracion)
        {
            if (fuerza < tiembla && Time.unscaledTime < tiemblaHasta) return;
            tiembla = fuerza; tiemblaDur = Mathf.Max(0.01f, duracion); tiemblaHasta = Time.unscaledTime + duracion;
        }

        void Start() { if (objetivo) yaw = objetivo.eulerAngles.y; }

        void LateUpdate()
        {
            if (!objetivo) return;
            var m = Forja3DInput.Mirar * sensibilidad;
            yaw += m.x;
            pitch = Mathf.Clamp(pitch - m.y, limitesPitch.x, limitesPitch.y);
            distancia = Mathf.Clamp(distancia - Input.mouseScrollDelta.y * 0.5f, 2f, 12f);

            var foco = objetivo.position + Vector3.up * altura;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var deseada = foco - rot * Vector3.forward * distancia;
            if (Physics.Linecast(foco, deseada, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(objetivo))
                deseada = hit.point + hit.normal * 0.2f;
            if (!iniciado) { suave = transform.position; iniciado = true; }
            var pos = suave = Vector3.Lerp(suave, deseada, 15f * Time.unscaledDeltaTime);
            if (Time.unscaledTime < tiemblaHasta)
            {
                float k = (tiemblaHasta - Time.unscaledTime) / tiemblaDur;
                transform.SetPositionAndRotation(pos + Random.insideUnitSphere * tiembla * k, rot);
            }
            else transform.SetPositionAndRotation(pos, rot);
        }
    }
}
