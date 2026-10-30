// Forja3D — efectos de combate: sangre, chispas, destellos de aviso, anillos de peligro,
// "hit-stop" (congelar el tiempo una fracción de segundo al golpear) y temblor de cámara.
// El constructor de niveles crea un objeto Forja3DFX en cada escena con el material de partículas.
using System.Collections;
using UnityEngine;

namespace Forja3D
{
    public class Forja3DFX : MonoBehaviour
    {
        public Material matParticula;
        static Forja3DFX inst;
        ParticleSystem sangre, chispas, destello;
        static float hitStopHasta;

        public static readonly Color Amarillo = new Color(1f, 0.85f, 0.35f);  // ataque que se puede bloquear / hacer parry
        public static readonly Color Rojo = new Color(1f, 0.15f, 0.1f);       // ataque imparable: ¡esquiva!

        void Awake()
        {
            inst = this;
            sangre = Sistema("Sangre", 1.6f, 0.9f);
            chispas = Sistema("Chispas", 0.4f, 0.35f);
            destello = Sistema("Destello", 0f, 0.35f);
        }

        void OnDestroy() { if (inst == this) inst = null; Time.timeScale = 1f; }

        ParticleSystem Sistema(string nombre, float gravedad, float vida)
        {
            var go = new GameObject(nombre); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 600;
            main.startLifetime = vida; main.gravityModifier = gravedad;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var tam = ps.sizeOverLifetime; tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (matParticula) r.sharedMaterial = matParticula;
            ps.Play();
            return ps;
        }

        static void Emitir(ParticleSystem ps, Vector3 pos, Vector3 vel, float tam, Color color, float vida = -1)
        {
            var e = new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = tam, startColor = color, applyShapeToPosition = false };
            if (vida > 0) e.startLifetime = vida;
            ps.Emit(e, 1);
        }

        // Salpicadura de sangre en dirección del golpe
        public static void Sangre(Vector3 pos, Vector3 dir, int cantidad = 18)
        {
            if (!inst) return;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.up;
            for (int i = 0; i < cantidad; i++)
            {
                var v = (dir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f) * Random.Range(1.5f, 5f);
                Emitir(inst.sangre, pos + Random.insideUnitSphere * 0.15f, v, Random.Range(0.06f, 0.2f),
                    Color.Lerp(new Color(0.35f, 0f, 0f), new Color(0.65f, 0.03f, 0.02f), Random.value));
            }
            for (int i = 0; i < 4; i++) // "niebla" de sangre
                Emitir(inst.sangre, pos, Random.insideUnitSphere * 0.6f, Random.Range(0.4f, 0.7f), new Color(0.45f, 0.02f, 0.02f, 0.5f), 0.4f);
        }

        // Chispas de metal (bloqueo y parry)
        public static void Chispas(Vector3 pos, int cantidad = 14, bool parry = false)
        {
            if (!inst) return;
            for (int i = 0; i < cantidad; i++)
                Emitir(inst.chispas, pos, Random.insideUnitSphere * Random.Range(3f, parry ? 9f : 6f), Random.Range(0.03f, 0.08f),
                    Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.7f), Random.value));
            Emitir(inst.destello, pos, Vector3.zero, parry ? 2.2f : 0.9f, parry ? new Color(1f, 0.95f, 0.75f) : new Color(1f, 0.7f, 0.3f, 0.8f), 0.15f);
        }

        // Polvo de luz pálido que sube (un alma que se desvanece)
        public static void Almas(Vector3 pos, int cantidad = 3)
        {
            if (!inst) return;
            for (int i = 0; i < cantidad; i++)
                Emitir(inst.chispas, pos + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.3f,
                    Random.Range(0.04f, 0.1f), Color.Lerp(new Color(0.7f, 0.8f, 1f, 0.9f), Color.white, Random.value), 1.2f);
        }

        // Destello de aviso antes de un ataque (amarillo = bloqueable, rojo = imparable)
        public static void Aviso(Vector3 pos, Color color, float tam = 1.3f)
        {
            if (!inst) return;
            Emitir(inst.destello, pos, Vector3.zero, tam, color, 0.35f);
            Emitir(inst.destello, pos, Vector3.zero, tam * 0.4f, Color.white, 0.25f);
        }

        // Anillo en el suelo que marca el área de un golpe imparable
        public static void Anillo(Vector3 centro, float radio, float duracion)
        {
            if (!inst) return;
            int n = Mathf.Clamp(Mathf.RoundToInt(radio * 14), 20, 90);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Emitir(inst.destello, centro + new Vector3(Mathf.Cos(a) * radio, 0.1f, Mathf.Sin(a) * radio), Vector3.zero, 0.35f, new Color(1f, 0.1f, 0.05f, 0.9f), duracion);
            }
        }

        // Polvo/fragmentos al golpear el suelo
        public static void Polvo(Vector3 centro, float radio)
        {
            if (!inst) return;
            for (int i = 0; i < 30; i++)
            {
                var d = Random.insideUnitCircle * radio;
                Emitir(inst.chispas, centro + new Vector3(d.x, 0.2f, d.y), new Vector3(d.x, Random.Range(2f, 5f), d.y), Random.Range(0.15f, 0.35f), new Color(0.35f, 0.3f, 0.27f, 0.8f), 0.8f);
            }
        }

        // Congela el tiempo un instante: da "peso" a cada golpe
        public static void HitStop(float segundos = 0.06f, float escala = 0.05f)
        {
            if (!inst || Forja3DGameManager.Terminado) return;
            hitStopHasta = Mathf.Max(hitStopHasta, Time.unscaledTime + segundos);
            Time.timeScale = escala;
            inst.StopCoroutine(nameof(Reanudar));
            inst.StartCoroutine(nameof(Reanudar));
        }

        IEnumerator Reanudar()
        {
            while (Time.unscaledTime < hitStopHasta) yield return null;
            Time.timeScale = 1f;
        }

        public static void Temblor(float fuerza = 0.25f, float duracion = 0.2f)
        {
            var c = Camera.main ? Camera.main.GetComponent<Forja3DCamera>() : null;
            if (c) c.Temblar(fuerza, duracion);
        }
    }
}
