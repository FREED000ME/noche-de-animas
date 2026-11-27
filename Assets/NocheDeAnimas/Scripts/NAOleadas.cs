// Noche de Ánimas — director de oleadas INFINITAS. Los muertos salen de la tierra por todo el mapa,
// lejos del jugador y sin avisar; cada oleada trae más, de más tipos y con más vida.
// No se acaban nunca: la única salida es completar la misión del mapa y llegar con la llave.
using System.Collections.Generic;
using Forja3D;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NocheDeAnimas
{
    public class NAOleadas : MonoBehaviour
    {
        public static NAOleadas Inst { get; private set; }
        public NivelConfig cfg = new NivelConfig();

        public int Oleada { get; private set; }
        public bool EnPausa => !enCurso;
        public float MultVida => 1f + 0.1f * Mathf.Max(0, Oleada - 1);
        public int Matados { get; private set; }
        public int Restantes => porSalir + deOleada.Count;
        public float SegundosParaLaSiguiente => Mathf.Max(0f, pausaHasta - Time.time);

        int porSalir;
        bool recordAvisado;
        bool enCurso;
        float proximaSalida, pausaHasta;
        readonly HashSet<NAZombie> deOleada = new HashSet<NAZombie>();

        // Malla de navegación para elegir puntos al azar por todo el mapa
        Vector3[] vertices; int[] indices; float[] acumulado; float areaTotal;

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        void Start()
        {
            PrepararMalla();
            pausaHasta = Time.time + 4f;
        }

        void Update()
        {
            if (Forja3DGameManager.Bloqueado) { if (!enCurso) pausaHasta = Mathf.Max(pausaHasta, Time.time + 3f); return; }

            if (!enCurso)
            {
                if (Time.time < pausaHasta) return;
                NuevaOleada();
                return;
            }

            if (porSalir > 0 && Time.time >= proximaSalida && NAZombie.Vivos.Count < cfg.maxVivos)
            {
                proximaSalida = Time.time + Random.Range(0.35f, 1.0f);
                Salir();
            }

            if (porSalir == 0 && deOleada.Count == 0)
            {
                enCurso = false;
                pausaHasta = Time.time + cfg.pausaEntreOleadas;
                Forja3DGameManager.Mensaje($"OLEADA {Oleada} SUPERADA");
            }
        }

        void NuevaOleada()
        {
            Oleada++;
            enCurso = true;
            porSalir = cfg.zombiesBase + (Oleada - 1) * cfg.zombiesPorOleada;
            proximaSalida = Time.time + 0.5f;
            NASonido.Sonar("campana", null, 0.7f, 0.6f);
            bool antes = NARecord.De(SceneManager.GetActiveScene().name) > 0;
            if (NARecord.Registrar(SceneManager.GetActiveScene().name, Oleada) && antes && !recordAvisado)
            { recordAvisado = true; Forja3DGameManager.Mensaje($"OLEADA {Oleada}: ¡NUEVO RÉCORD!"); }
            else Forja3DGameManager.Mensaje($"OLEADA {Oleada}");
        }

        TipoZombie Elegir()
        {
            float suma = 0f;
            for (int i = 0; i < cfg.pesos.Length; i++) if (cfg.entraEnOleada[i] <= Oleada) suma += cfg.pesos[i];
            if (suma <= 0f) return TipoZombie.Caminante;
            float r = Random.value * suma;
            for (int i = 0; i < cfg.pesos.Length; i++)
            {
                if (cfg.entraEnOleada[i] > Oleada) continue;
                r -= cfg.pesos[i];
                if (r <= 0f) return (TipoZombie)i;
            }
            return TipoZombie.Caminante;
        }

        // Dos de cada tres salen de dentro de una casa (y se abren paso por puertas y ventanas);
        // los demás salen de la tierra
        void Salir()
        {
            var tipo = Elegir();
            Vector3 p = default;
            bool deCasa = Random.value < 0.65f && DesdeCasa(out p);
            if (!deCasa && !Punto(11f, 999f, out p)) return;
            var z = NAZombie.Crear(tipo, p, !deCasa, MultVida);
            deOleada.Add(z);
            porSalir--;
        }

        bool DesdeCasa(out Vector3 p)
        {
            p = default;
            var casas = NAMapa.Casas;
            if (casas.Count == 0) return false;
            var j = NAJugador.Transform;
            int dentro = j ? NAMapa.CasaEn(j.position) : -1;
            for (int intento = 0; intento < 8; intento++)
            {
                int i = Random.Range(0, casas.Count);
                if (i == dentro || casas[i].bodega) continue;
                var celdas = NAMapa.Interior(i);
                if (celdas.Count == 0) continue;
                var c = celdas[Random.Range(0, celdas.Count)];
                if (j && Vector3.Distance(j.position, c) < 10f) continue;
                c += new Vector3(Random.Range(-0.7f, 0.7f), 0, Random.Range(-0.7f, 0.7f));
                if (!NavMesh.SamplePosition(c, out var hit, 1.2f, NavMesh.AllAreas) || !NAMapa.SueloValido(hit.position)) continue;
                p = hit.position;
                return true;
            }
            return false;
        }

        // Refuerzos (grito de la Plañidera, campana del Sacristán): cerca, pero no encima
        public static void Refuerzos(int cuantos, TipoZombie tipo)
        {
            if (!Inst) return;
            for (int i = 0; i < cuantos; i++)
            {
                if (NAZombie.Vivos.Count >= Inst.cfg.maxVivos + 6) return;
                if (Inst.Punto(7f, 16f, out var p)) NAZombie.Crear(tipo, p, true, Inst.MultVida);
            }
        }

        public static void ZombieMuerto(NAZombie z)
        {
            if (!Inst) return;
            Inst.Matados++;
            Inst.deOleada.Remove(z);
            Forja3DGameManager.ExtraVictoria = $"Oleadas resistidas: {Inst.Oleada}  ·  Muertos devueltos a su descanso: {Inst.Matados}  ·  Puntos: {NAPuntos.Puntos}";
        }

        // ── Puntos al azar sobre la malla de navegación ──
        void PrepararMalla()
        {
            var tri = NavMesh.CalculateTriangulation();
            vertices = tri.vertices; indices = tri.indices;
            if (indices == null || indices.Length < 3) return;
            acumulado = new float[indices.Length / 3];
            areaTotal = 0f;
            for (int i = 0; i < acumulado.Length; i++)
            {
                var a = vertices[indices[i * 3]]; var b = vertices[indices[i * 3 + 1]]; var c = vertices[indices[i * 3 + 2]];
                areaTotal += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                acumulado[i] = areaTotal;
            }
        }

        bool Punto(float minimo, float maximo, out Vector3 punto)
        {
            punto = Vector3.zero;
            if (acumulado == null || acumulado.Length == 0) PrepararMalla();
            if (acumulado == null || acumulado.Length == 0) return false;
            var jugador = NAJugador.Transform;
            Vector3 mejor = Vector3.zero; float mejorD = -1f;
            for (int intento = 0; intento < 30; intento++)
            {
                float r = Random.value * areaTotal;
                int i = System.Array.BinarySearch(acumulado, r);
                if (i < 0) i = ~i;
                i = Mathf.Clamp(i, 0, acumulado.Length - 1);
                var a = vertices[indices[i * 3]]; var b = vertices[indices[i * 3 + 1]]; var c = vertices[indices[i * 3 + 2]];
                float u = Random.value, v = Random.value;
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                var p = a + (b - a) * u + (c - a) * v;
                if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                if (!NAMapa.SueloValido(hit.position)) continue; // nada de zombies encima de los muros
                float d = jugador ? Vector3.Distance(jugador.position, hit.position) : minimo;
                if (d >= minimo && d <= maximo) { punto = hit.position; return true; }
                if (d >= minimo * 0.6f && d > mejorD) { mejorD = d; mejor = hit.position; }
            }
            if (mejorD > 0f) { punto = mejor; return true; }
            return false;
        }
    }
}
