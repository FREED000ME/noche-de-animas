// Noche de Ánimas — cámara en primera persona: los ojos del protagonista.
//   Móvil: arrastrar en la mitad derecha mira. PC: el ratón mira (clic para capturarlo, Esc para soltarlo).
//   Balanceo al caminar, retroceso al disparar, temblor de los golpes y linterna de noche.
using Forja3D;
using UnityEngine;

namespace NocheDeAnimas
{
    public class NACamaraFP : MonoBehaviour
    {
        public static NACamaraFP Inst { get; private set; }
        public Transform jugador;
        public float altura = 1.62f, sensibilidad = 1f;
        public bool linterna;

        float yaw, pitch, balanceo, retroceso, retrocesoLateral;
        Vector3 ultimaPos;
        Camera cam;
        Forja3DCamera orbital;

        public float Velocidad { get; private set; }

        void Awake() { Inst = this; }
        void OnDestroy() { if (Inst == this) Inst = null; if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }

        public void Iniciar(Transform j, bool noche)
        {
            jugador = j;
            cam = GetComponent<Camera>();
            if (cam) { cam.nearClipPlane = 0.05f; cam.fieldOfView = 70f; }
            orbital = GetComponent<Forja3DCamera>();
            if (orbital) orbital.enabled = false;
            yaw = j.eulerAngles.y; pitch = 0f;
            ultimaPos = j.position;
            // El cuerpo cápsula no se ve desde dentro
            foreach (var r in j.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (noche)
            {
                var l = new GameObject("Linterna").AddComponent<Light>();
                l.transform.SetParent(transform, false); l.transform.localPosition = new Vector3(0.15f, -0.1f, 0f);
                l.type = LightType.Spot; l.spotAngle = 55f; l.range = 22f; l.intensity = 2.2f; l.color = new Color(1f, 0.93f, 0.8f);
                l.shadows = LightShadows.None;
            }
        }

        public void Retroceso(float fuerza)
        {
            retroceso += fuerza * 6f;
            retrocesoLateral += Random.Range(-1f, 1f) * fuerza * 2f;
        }

        void Start()
        {
            // Por si la cámara orbital se añadió después (el Start del jugador)
            if (!orbital) orbital = GetComponent<Forja3DCamera>();
            if (orbital) orbital.enabled = false;
        }

        void Update()
        {
            if (orbital && orbital.enabled) orbital.enabled = false;
            if (Forja3DInput.EsTactil) return;
            // PC: capturar el ratón
            if (Forja3DGameManager.Bloqueado) { if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        void LateUpdate()
        {
            if (!jugador) return;
            var m = Forja3DInput.Mirar * sensibilidad; // también vacía la mirada táctil de este frame
            if (!Forja3DInput.EsTactil && Cursor.lockState == CursorLockMode.Locked && !Input.GetMouseButton(1))
                m += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2.2f;
            if (Forja3DGameManager.Bloqueado) m = Vector2.zero;

            yaw += m.x;
            pitch = Mathf.Clamp(pitch - m.y, -80f, 80f);

            // El cuerpo siempre mira hacia donde mira la cámara (los controles se mueven relativos a ella)
            jugador.rotation = Quaternion.Euler(0, yaw, 0);

            // Balanceo al caminar
            var d = jugador.position - ultimaPos; d.y = 0;
            Velocidad = Time.deltaTime > 0 ? d.magnitude / Time.deltaTime : 0f;
            ultimaPos = jugador.position;
            if (Velocidad > 0.5f) balanceo += Time.deltaTime * Mathf.Lerp(7f, 12f, Mathf.InverseLerp(3f, 6.6f, Velocidad));
            float amp = Mathf.Clamp01(Velocidad / 4f) * 0.045f;
            var bob = new Vector3(Mathf.Cos(balanceo) * amp, Mathf.Abs(Mathf.Sin(balanceo)) * amp * 1.4f, 0);

            retroceso = Mathf.MoveTowards(retroceso, 0f, Time.deltaTime * 18f);
            retrocesoLateral = Mathf.MoveTowards(retrocesoLateral, 0f, Time.deltaTime * 8f);

            var rot = Quaternion.Euler(pitch - retroceso, yaw + retrocesoLateral, 0);
            var pos = jugador.position + Vector3.up * altura + rot * bob;
            if (orbital) pos += orbital.Sacudida() * 0.6f;
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
