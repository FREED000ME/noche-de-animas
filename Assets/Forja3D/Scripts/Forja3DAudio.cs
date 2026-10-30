// Forja3D — sonido: efectos 3D con variaciones, ambiente en bucle y música de combate que entra
// cuando un enemigo te ataca y se apaga al quedar a salvo.
// Los clips viven en Assets/Forja3D/Resources/Forja3DAudio/. Variantes: nombre_1, nombre_2, ...
// (se elige una al azar y se varía el tono para que nunca suene igual dos veces).
using System.Collections.Generic;
using UnityEngine;

// EXPANSIÓN FUTURA: para un juego más grande conviene un AudioMixer con grupos (Música/Efectos/Voces)
// y un menú de volumen; en PC/consola, audio envolvente (spatialBlend 1 + reverberación por zonas).
namespace Forja3D
{
    public class Forja3DAudio : MonoBehaviour
    {
        public string ambiente = "ambiente_infierno";
        public string musicaCombate = "musica_combate";
        [Range(0, 1)] public float volumenAmbiente = 0.55f, volumenMusica = 0.5f, volumenEfectos = 1f;

        static Forja3DAudio inst;
        static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource srcAmbiente, srcMusica;
        float combateHasta;

        public static Forja3DAudio Asegurar()
        {
            if (inst) return inst;
            return new GameObject("Audio").AddComponent<Forja3DAudio>();
        }

        void Awake()
        {
            if (inst && inst != this) { Destroy(this); return; }
            inst = this;
            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("Sonido" + i);            // cada fuente en su propio objeto: posición 3D propia
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0.75f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3f; s.maxDistance = 40f; s.dopplerLevel = 0;
                pool.Add(s);
            }
            srcAmbiente = Bucle(); srcMusica = Bucle();
        }

        AudioSource Bucle()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        void Start()
        {
            var a = Clip(ambiente); if (a) { srcAmbiente.clip = a; srcAmbiente.Play(); }
            var m = Clip(musicaCombate); if (m) { srcMusica.clip = m; srcMusica.Play(); }
        }

        void OnDestroy() { if (inst == this) inst = null; }

        void Update()
        {
            bool combate = Time.time < combateHasta && !Forja3DGameManager.Terminado;
            srcAmbiente.volume = Mathf.MoveTowards(srcAmbiente.volume, volumenAmbiente * (combate ? 0.5f : 1f), Time.unscaledDeltaTime * 0.4f);
            srcMusica.volume = Mathf.MoveTowards(srcMusica.volume, combate ? volumenMusica : 0f, Time.unscaledDeltaTime * (combate ? 0.8f : 0.25f));
        }

        // Un enemigo en pelea llama a esto cada frame: mantiene la música de combate
        public static void EnCombate() { if (inst) inst.combateHasta = Time.time + 4f; }

        static AudioClip[] Variantes(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            if (cache.TryGetValue(nombre, out var v)) return v;
            var lista = new List<AudioClip>();
            var c = Resources.Load<AudioClip>("Forja3DAudio/" + nombre);
            if (c) lista.Add(c);
            for (int i = 1; i <= 8; i++) { var ci = Resources.Load<AudioClip>($"Forja3DAudio/{nombre}_{i}"); if (ci) lista.Add(ci); else if (i > 1) break; }
            return cache[nombre] = lista.ToArray();
        }

        public static AudioClip Clip(string nombre)
        {
            var v = Variantes(nombre);
            return v != null && v.Length > 0 ? v[Random.Range(0, v.Length)] : null;
        }

        // Reproduce un efecto en una posición (null = sin posición, sonido de interfaz)
        public static void Sonar(string nombre, Vector3? pos = null, float volumen = 1f, float variacionTono = 0.08f, float tono = 1f)
        {
            var clip = Clip(nombre);
            if (!clip) return;
            var a = Asegurar();
            AudioSource s = null;
            foreach (var p in a.pool) if (!p.isPlaying) { s = p; break; }
            if (!s) s = a.pool[Random.Range(0, a.pool.Count)];
            if (pos.HasValue) s.transform.position = pos.Value;
            s.spatialBlend = pos.HasValue ? 0.75f : 0f;
            s.pitch = tono * (1f + Random.Range(-variacionTono, variacionTono));
            s.volume = volumen * a.volumenEfectos;
            s.PlayOneShot(clip);
        }
    }
}
