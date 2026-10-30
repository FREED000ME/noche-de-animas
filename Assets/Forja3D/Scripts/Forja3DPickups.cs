// Forja3D — monedas y meta del nivel.
using UnityEngine;

namespace Forja3D
{
    public class Forja3DCollectible : MonoBehaviour
    {
        void Start() => Forja3DGameManager.RegistrarMoneda();
        void Update() => transform.Rotate(0, 120f * Time.deltaTime, 0, Space.World);
        void OnTriggerEnter(Collider c)
        {
            if (!c.CompareTag("Player")) return;
            Forja3DGameManager.MonedaRecogida();
            Destroy(gameObject);
        }
    }

    public class Forja3DGoal : MonoBehaviour
    {
        // La puerta no gira: es la salida del nivel
        void OnTriggerEnter(Collider c) { if (c.CompareTag("Player")) Forja3DGameManager.MetaAlcanzada(); }
    }
}
