// Forja3D — personaje con diálogo (Virgilio, Francesca…): habla cuando Dante se acerca.
// Con "seguir" activado acompaña al jugador (como Virgilio a Dante).
using UnityEngine;
using UnityEngine.AI;

namespace Forja3D
{
    public class Forja3DNPC : MonoBehaviour
    {
        public string nombre = "";
        public Linea[] dialogo;
        public bool repetir = false;
        public bool seguir = false;
        public float distancia = 3f;
        public bool clave = false; // hay que escucharlo para abrir la meta (objetivo "dialogos")
        public bool desvanecer = false; // al terminar de hablar se vuelve sombra y se disuelve (Homero, Francesca, Paolo)

        Transform jugador;
        Animator anim;
        NavMeshAgent agente;
        bool hablado;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p) jugador = p.transform;
            anim = GetComponentInChildren<Animator>();
            if (anim) anim.applyRootMotion = false;
            Forja3DSinRaiz.Poner(anim);
            agente = GetComponent<NavMeshAgent>();
            if (clave) Forja3DGameManager.RegistrarClave();
        }
        bool contado;

        void Update()
        {
            if (!jugador) return;
            var d = jugador.position - transform.position; d.y = 0;
            float dist = d.magnitude;

            if (seguir && agente && agente.isOnNavMesh)
            {
                if (dist > 3.5f && !Forja3DDialogue.Activo) agente.SetDestination(jugador.position - d.normalized * 2.2f);
                else if (dist < 2.5f) agente.ResetPath();
                Forja3DAnim.Float(anim, "Speed", agente.velocity.magnitude);
            }

            // Mira a Dante cuando está cerca
            if (dist < distancia * 2f && d.sqrMagnitude > 0.01f && (!agente || agente.velocity.sqrMagnitude < 0.1f))
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 4f * Time.deltaTime);

            if (!hablado && dist <= distancia && !Forja3DDialogue.Activo && !Forja3DPergamino.Activo && !Forja3DGameManager.Terminado && dialogo != null && dialogo.Length > 0)
            {
                hablado = !repetir;
                Forja3DAnim.Trigger(anim, "Alert");
                Forja3DDialogue.Mostrar(dialogo, () =>
                {
                    if (clave && !contado) { contado = true; Forja3DGameManager.ClaveOida(); }
                    if (desvanecer) StartCoroutine(Disolverse());
                });
            }
            if (repetir && dist > distancia * 1.5f) hablado = false;
        }

        // Se vuelve translúcido, sube un poco y se disuelve en polvo de luz
        System.Collections.IEnumerator Disolverse()
        {
            yield return new WaitForSeconds(0.4f);
            if (agente) agente.enabled = false;
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)
                {
                    m.SetFloat("_Mode", 2f);
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = 3000;
                    m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.25f, 0.3f, 0.4f));
                    mats.Add(m);
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var ini = transform.position; var luz = gameObject.AddComponent<Light>(); luz.type = LightType.Point; luz.color = new Color(0.7f, 0.8f, 1f); luz.range = 5f;
            Forja3DAudio.Sonar("esquiva", transform.position + Vector3.up, 0.4f, 0f, 0.6f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 2.8f)
            {
                foreach (var m in mats) { var c = m.color; c.a = Mathf.Lerp(0.85f, 0f, t); m.color = c; }
                transform.position = ini + Vector3.up * (0.8f * t * t);
                luz.intensity = Mathf.Sin(t * Mathf.PI) * 1.5f;
                if (Random.value < 0.6f) Forja3DFX.Almas(transform.position + Vector3.up * Random.Range(0.3f, 1.7f), 2);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
