// Noche de Ánimas — director de oleadas. Los muertos salen de la tierra por todo el mapa,
// lejos del jugador y sin avisar; cada oleada trae más y de más tipos. Al superar todas,
// se abre el camino de vuelta al refugio (la meta del nivel).
using System.Collections.Generic;
using Forja3D;
using UnityEngine;
using UnityEngine.AI;

namespace NocheDeAnimas
{
    public class NAOleadas : MonoBehaviour
    {
        public static NAOleadas Inst { get; private set; }
        public NivelConfig cfg = new NivelConfig();

        public int Oleada { get; private set; }
        public int Total => cfg.oleadas;
        public bool Completado { get; private set; }
        public bool EnPausa => !enCurso && !Completado;
        public int Matados { get; private set; }
        public int Restantes => porSalir + deOleada.Count;
        public float SegundosParaLaSiguiente => Mathf.Max(0f, pausaHasta - Time.time);

        int porSalir;
        bool enCurso;
        float proximaSalida, pausaHasta;
        readonly HashSet<NAZombie> deOleada = new HashSet<NAZombie>();

        // Malla de navegación para elegir puntos al azar por todo el mapa
        Vector3[] vertices; int[] indices; float[] acumulado; float areaTotal;

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        void Start()
        {
            Forja3DGameManager.CondicionExtra = () => Completado;
            Forja3DGameManager.MotivoExtra = "¡Sobrevive a todas las oleadas primero!";
            PrepararMalla();
            pausaHasta = Time.time + 4f;
        }

        void Update()
        {
            if (Completado) return;
            if (Forja3DGameManager.Bloqueado) { if (!enCurso) pausaHasta = Mathf.Max(pausaHasta, Time.time + 3f); return; }

            if (!enCurso)
            {
                if (Time.time < pausaHasta) return;
                if (Oleada >= cfg.oleadas) { Completar(); return; }
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
                if (Oleada >= cfg.oleadas) { Completar(); return; }
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
            Forja3DGameManager.Mensaje(Oleada == cfg.oleadas ? $"ÚLTIMA OLEADA" : $"OLEADA {Oleada} DE {cfg.oleadas}");
        }

        void Completar()
        {
            Completado = true;
            enCurso = false;
            Forja3DGameManager.Mensaje("¡SOBREVIVISTE!");
            Forja3DGameManager.ExtraVictoria = $"Muertos devueltos a su descanso: {Matados}";
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

        void Salir()
        {
            if (!Punto(11f, 999f, out var p)) return;
            var z = NAZombie.Crear(Elegir(), p, true);
            deOleada.Add(z);
            porSalir--;
        }

        // Refuerzos (grito de la Plañidera, campana del Sacristán): cerca, pero no encima
        public static void Refuerzos(int cuantos, TipoZombie tipo)
        {
            if (!Inst) return;
            for (int i = 0; i < cuantos; i++)
            {
                if (NAZombie.Vivos.Count >= Inst.cfg.maxVivos + 6) return;
                if (Inst.Punto(7f, 16f, out var p)) NAZombie.Crear(tipo, p, true);
            }
        }

        public static void ZombieMuerto(NAZombie z)
        {
            if (!Inst) return;
            Inst.Matados++;
            Inst.deOleada.Remove(z);
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
                float d = jugador ? Vector3.Distance(jugador.position, hit.position) : minimo;
                if (d >= minimo && d <= maximo) { punto = hit.position; return true; }
                if (d >= minimo * 0.6f && d > mejorD) { mejorD = d; mejor = hit.position; }
            }
            if (mejorD > 0f) { punto = mejor; return true; }
            return false;
        }
    }
}
