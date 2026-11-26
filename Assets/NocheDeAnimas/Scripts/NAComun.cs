// Noche de Ánimas — utilidades comunes: materiales, sonidos sintetizados, datos de armas y de cada nivel.
// Todo se crea por código (sin modelos ni audios generados): 0 créditos.
using System.Collections.Generic;
using UnityEngine;

namespace NocheDeAnimas
{
    // ───────────────────────── Materiales de color ─────────────────────────
    public static class NAMat
    {
        static Shader sombreador;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Material De(Color c, float emision = 0f)
        {
            string clave = ColorUtility.ToHtmlStringRGBA(c) + "_" + emision.ToString("0.00");
            if (cache.TryGetValue(clave, out var m) && m) return m;
            if (!sombreador) sombreador = Shader.Find("Standard");
            if (!sombreador)
            {
                var r = Object.FindFirstObjectByType<MeshRenderer>();
                if (r && r.sharedMaterial) sombreador = r.sharedMaterial.shader;
            }
            m = new Material(sombreador) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            if (emision > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emision);
            }
            cache[clave] = m;
            return m;
        }

        // Pieza simple (cubo, esfera, cilindro, cápsula) sin colisión
        public static Transform Pieza(PrimitiveType tipo, Transform padre, Vector3 pos, Vector3 escala, Color color, Vector3 rot = default, float emision = 0f)
        {
            var g = GameObject.CreatePrimitive(tipo);
            var col = g.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = escala;
            var rend = g.GetComponent<Renderer>();
            rend.sharedMaterial = De(color, emision);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    // ───────────────────────── Sonidos sintetizados ─────────────────────────
    public class NASonido : MonoBehaviour
    {
        static NASonido inst;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> fuentes = new List<AudioSource>();
        const int Frec = 22050;

        static NASonido Asegurar()
        {
            if (inst) return inst;
            var g = new GameObject("NASonido");
            DontDestroyOnLoad(g);
            inst = g.AddComponent<NASonido>();
            for (int i = 0; i < 16; i++)
            {
                var h = new GameObject("Fuente" + i); h.transform.SetParent(g.transform);
                var s = h.AddComponent<AudioSource>();
                s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 4f; s.maxDistance = 55f;
                inst.fuentes.Add(s);
            }
            return inst;
        }

        public static void Sonar(string nombre, Vector3? pos = null, float volumen = 1f, float tono = 1f)
        {
            var clip = Clip(nombre);
            if (!clip) return;
            var a = Asegurar();
            AudioSource s = null;
            foreach (var f in a.fuentes) if (!f.isPlaying) { s = f; break; }
            if (!s) s = a.fuentes[Random.Range(0, a.fuentes.Count)];
            if (pos.HasValue) s.transform.position = pos.Value;
            s.spatialBlend = pos.HasValue ? 0.7f : 0f;
            s.pitch = tono * Random.Range(0.94f, 1.06f);
            s.volume = Mathf.Clamp01(volumen);
            s.PlayOneShot(clip);
        }

        static AudioClip Clip(string n)
        {
            if (clips.TryGetValue(n, out var c) && c) return c;
            c = Crear(n);
            if (c) clips[n] = c;
            return c;
        }

        // Pequeño sintetizador: ruido, senos y envolventes
        static AudioClip Crear(string n)
        {
            float dur;
            System.Func<float, float> f;
            var rnd = new System.Random(n.GetHashCode());
            float Ruido() => (float)(rnd.NextDouble() * 2.0 - 1.0);
            float paso = 0f; // filtro paso bajo de un polo
            float Bajo(float x, float k) { paso += (x - paso) * k; return paso; }
            float Seno(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);

            switch (n)
            {
                case "tiro": dur = 0.25f; f = t => Bajo(Ruido(), 0.6f) * Mathf.Exp(-t * 28f) + Seno(t, 90f) * Mathf.Exp(-t * 22f) * 0.8f; break;
                case "escopeta": dur = 0.45f; f = t => Bajo(Ruido(), 0.35f) * Mathf.Exp(-t * 11f) * 1.2f + Seno(t, 60f) * Mathf.Exp(-t * 14f); break;
                case "resortera": dur = 0.16f; f = t => Seno(t, Mathf.Lerp(700f, 180f, t / 0.16f)) * Mathf.Exp(-t * 18f) + Ruido() * 0.15f * Mathf.Exp(-t * 40f); break;
                case "ballesta": dur = 0.22f; f = t => Seno(t, 170f) * Mathf.Exp(-t * 20f) + Bajo(Ruido(), 0.5f) * Mathf.Exp(-t * 35f); break;
                case "clavo": dur = 0.07f; f = t => Ruido() * Mathf.Exp(-t * 70f) * 0.8f + Seno(t, 1900f) * Mathf.Exp(-t * 60f) * 0.4f; break;
                case "cohete": dur = 0.6f; f = t => Bajo(Ruido(), Mathf.Lerp(0.05f, 0.5f, t / 0.6f)) * Mathf.Min(1f, t * 12f) * Mathf.Exp(-t * 3f) * 1.4f; break;
                case "explosion": dur = 1.4f; f = t => Bajo(Ruido(), 0.12f) * Mathf.Exp(-t * 3.5f) * 2.2f + Seno(t, 45f) * Mathf.Exp(-t * 4f); break;
                case "recarga": dur = 0.32f; f = t => (t < 0.05f ? Ruido() * Mathf.Exp(-t * 90f) : 0f) + (t > 0.22f && t < 0.28f ? Ruido() * Mathf.Exp(-(t - 0.22f) * 90f) : 0f); break;
                case "vacio": dur = 0.06f; f = t => Ruido() * Mathf.Exp(-t * 120f) * 0.6f; break;
                case "grito": dur = 1.3f; f = t => (Seno(t, 760f + 260f * Mathf.Sin(t * 30f)) * 0.6f + Ruido() * 0.25f) * Mathf.Min(1f, t * 8f) * Mathf.Exp(-t * 1.6f); break;
                case "campana": dur = 2.2f; f = t => (Seno(t, 196f) + Seno(t, 392f) * 0.6f + Seno(t, 587f) * 0.4f + Seno(t, 932f) * 0.25f) * Mathf.Exp(-t * 1.8f) * 0.5f; break;
                case "gemido": dur = 1.1f; f = t => (Seno(t, 115f + 18f * Mathf.Sin(t * 7f)) * 0.7f + Bajo(Ruido(), 0.1f) * 0.5f) * Mathf.Sin(Mathf.PI * t / 1.1f); break;
                case "mordida": dur = 0.2f; f = t => Bajo(Ruido(), 0.7f) * Mathf.Exp(-t * 25f) * (0.6f + 0.4f * Mathf.Sin(t * 400f)); break;
                case "escupitajo": dur = 0.3f; f = t => Bajo(Ruido(), 0.25f) * Mathf.Exp(-t * 12f) * (0.5f + 0.5f * Seno(t, 25f)); break;
                case "golpe": dur = 0.3f; f = t => Seno(t, 70f) * Mathf.Exp(-t * 14f) + Bajo(Ruido(), 0.3f) * Mathf.Exp(-t * 20f); break;
                case "recoger": dur = 0.22f; f = t => Seno(t, t < 0.1f ? 880f : 1320f) * Mathf.Exp(-(t % 0.11f) * 20f) * 0.5f; break;
                case "impacto": dur = 0.1f; f = t => Bajo(Ruido(), 0.4f) * Mathf.Exp(-t * 45f); break;
                default: return null;
            }
            int muestras = Mathf.CeilToInt(dur * Frec);
            var datos = new float[muestras];
            float max = 0.0001f;
            for (int i = 0; i < muestras; i++) { datos[i] = f(i / (float)Frec); max = Mathf.Max(max, Mathf.Abs(datos[i])); }
            for (int i = 0; i < muestras; i++) datos[i] = datos[i] / max * 0.9f;
            var clip = AudioClip.Create("na_" + n, muestras, 1, Frec, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }

    // ───────────────────────── Armas caseras ─────────────────────────
    public enum Municion { Canicas, Balas, Cartuchos, Varillas, Clavos, Cohetes }

    public class ArmaDef
    {
        public string id, nombre, sonido;
        public Municion municion;
        public int danio = 1, cargador = 6, perdigones = 1, atraviesa = 0;
        public float cadencia = 0.4f, recarga = 1.5f, alcance = 25f, dispersion = 1.5f, retroceso = 0.1f;
        public bool automatica, proyectil, explosiva;
        public float velProyectil = 40f, radioExplosion = 0f;
    }

    public static class NADatos
    {
        // Orden = orden en que se consiguen
        public static readonly ArmaDef[] Armas =
        {
            new ArmaDef { id = "resortera", nombre = "RESORTERA", sonido = "resortera", municion = Municion.Canicas, danio = 1, cargador = 8, cadencia = 0.45f, recarga = 1.2f, alcance = 22f, dispersion = 2f, retroceso = 0.03f },
            new ArmaDef { id = "hechiza", nombre = "PISTOLA HECHIZA", sonido = "tiro", municion = Municion.Balas, danio = 2, cargador = 6, cadencia = 0.32f, recarga = 1.6f, alcance = 28f, dispersion = 2.5f, retroceso = 0.12f },
            new ArmaDef { id = "tubo", nombre = "ESCOPETA DE TUBO", sonido = "escopeta", municion = Municion.Cartuchos, danio = 1, perdigones = 7, cargador = 2, cadencia = 0.75f, recarga = 2.2f, alcance = 13f, dispersion = 11f, retroceso = 0.35f },
            new ArmaDef { id = "ballesta", nombre = "BALLESTA DE VARILLA", sonido = "ballesta", municion = Municion.Varillas, danio = 6, atraviesa = 2, cargador = 1, cadencia = 0.3f, recarga = 1.3f, alcance = 40f, dispersion = 0.5f, retroceso = 0.1f, proyectil = true, velProyectil = 55f },
            new ArmaDef { id = "clavos", nombre = "LANZACLAVOS", sonido = "clavo", municion = Municion.Clavos, danio = 1, cargador = 30, cadencia = 0.09f, recarga = 2.4f, alcance = 18f, dispersion = 4.5f, retroceso = 0.04f, automatica = true },
            new ArmaDef { id = "cohetero", nombre = "COHETERO", sonido = "cohete", municion = Municion.Cohetes, danio = 9, cargador = 1, cadencia = 0.5f, recarga = 2.8f, alcance = 45f, dispersion = 1f, retroceso = 0.4f, proyectil = true, explosiva = true, velProyectil = 24f, radioExplosion = 4.2f },
        };

        public static string NombreMunicion(Municion m) => m switch
        {
            Municion.Canicas => "canicas", Municion.Balas => "balas", Municion.Cartuchos => "cartuchos",
            Municion.Varillas => "varillas", Municion.Clavos => "clavos", _ => "cohetes",
        };

        // Cuánta munición da una caja o un suministro, por tipo
        public static int Caja(Municion m) => m switch
        {
            Municion.Canicas => 16, Municion.Balas => 12, Municion.Cartuchos => 6,
            Municion.Varillas => 4, Municion.Clavos => 40, _ => 1,
        };
    }

    // ───────────────────────── Zombies ─────────────────────────
    public enum TipoZombie { Caminante, Corredor, Escupidor, Compadre, Planidera, Cuetero, Sacristan }

    // ───────────────────────── Configuración de cada nivel ─────────────────────────
    public class NivelConfig
    {
        public int armasDisponibles = 2;               // primeras N armas de NADatos.Armas
        public int[] reservaInicial = { 999, 24, 0, 0, 0, 0 }; // por tipo de munición (canicas = infinitas)
        public int vidaJugador = 10;
        public int oleadas = 3;
        public int zombiesBase = 6, zombiesPorOleada = 3;
        public float pausaEntreOleadas = 6f;
        public int maxVivos = 14;
        public TipoZombie tipoInicial = TipoZombie.Caminante;
        // Peso de cada tipo en las oleadas (índice = TipoZombie); la oleada n añade tipos a partir de su índice de entrada
        public float[] pesos = { 1f, 0f, 0f, 0f, 0f, 0f };
        public int[] entraEnOleada = { 1, 99, 99, 99, 99, 99 };
    }

    public static class NAConfig
    {
        public static NivelConfig Para(string escena)
        {
            var c = new NivelConfig();
            if (escena.StartsWith("01"))
            {
                c.armasDisponibles = 2; c.reservaInicial = new[] { 999, 24, 0, 0, 0, 0 };
                c.oleadas = 3; c.zombiesBase = 6; c.zombiesPorOleada = 3; c.maxVivos = 10;
                c.pesos = new[] { 1f, 0.5f, 0f, 0f, 0f, 0.25f }; c.entraEnOleada = new[] { 1, 2, 99, 99, 99, 3 };
            }
            else if (escena.StartsWith("02"))
            {
                c.armasDisponibles = 3; c.reservaInicial = new[] { 999, 30, 10, 0, 0, 0 };
                c.oleadas = 4; c.zombiesBase = 8; c.zombiesPorOleada = 3; c.maxVivos = 12;
                c.pesos = new[] { 1f, 0.6f, 0.4f, 0.2f, 0f, 0.3f }; c.entraEnOleada = new[] { 1, 1, 2, 4, 99, 3 };
            }
            else if (escena.StartsWith("03"))
            {
                c.armasDisponibles = 5; c.reservaInicial = new[] { 999, 36, 12, 8, 90, 0 };
                c.oleadas = 4; c.zombiesBase = 10; c.zombiesPorOleada = 4; c.maxVivos = 14;
                c.pesos = new[] { 1f, 0.7f, 0.4f, 0.25f, 0.25f, 0.35f }; c.entraEnOleada = new[] { 1, 1, 1, 3, 2, 2 };
            }
            else if (escena.StartsWith("04"))
            {
                c.armasDisponibles = 6; c.reservaInicial = new[] { 999, 40, 14, 10, 120, 4 };
                c.oleadas = 3; c.zombiesBase = 10; c.zombiesPorOleada = 4; c.maxVivos = 16;
                c.pesos = new[] { 1f, 0.7f, 0.5f, 0.3f, 0.3f, 0.4f }; c.entraEnOleada = new[] { 1, 1, 1, 2, 2, 1 };
            }
            else
            {
                // Niveles futuros: dificultad creciente con todas las armas
                c.armasDisponibles = 6; c.reservaInicial = new[] { 999, 40, 14, 10, 120, 4 };
                c.oleadas = 5; c.zombiesBase = 12; c.zombiesPorOleada = 4; c.maxVivos = 18;
                c.pesos = new[] { 1f, 0.8f, 0.5f, 0.35f, 0.3f, 0.4f }; c.entraEnOleada = new[] { 1, 1, 1, 2, 2, 1 };
            }
            return c;
        }
    }
}
