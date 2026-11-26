// Noche de Ánimas — zombies. No avisan: en cuanto aparecen saben dónde estás y van a por ti.
//   Caminante  · mordida cuerpo a cuerpo
//   Corredor   · rapidísimo; se lanza en embestida desde media distancia
//   Escupidor  · mantiene la distancia y escupe ácido en parábola
//   Compadre   · grandote y lento; golpe de área que empuja
//   Plañidera  · grita: te ralentiza y llama a más muertos
//   Cuetero    · lleva cohetes encendidos a la espalda; explota al alcanzarte o al morir
//   Sacristán  · jefe; toca la campana para llamar a la horda y golpea en área
// Los cuerpos se construyen con piezas simples (0 créditos de generación).
using System.Collections;
using System.Collections.Generic;
using Forja3D;
using UnityEngine;
using UnityEngine.AI;

namespace NocheDeAnimas
{
    public class NAZombie : MonoBehaviour
    {
        public static readonly List<NAZombie> Vivos = new List<NAZombie>();

        public TipoZombie tipo;
        public float velocidad = 2f, rango = 1.5f, enfriamiento = 1.3f;
        public int danio = 1;
        public bool esJefe;

        NavMeshAgent agente;
        Forja3DHealth vida;
        Transform jugador, cuerpo, brazoI, brazoD, piernaI, piernaD;
        Forja3DHealth vidaJugador;
        float proximoAtaque, proximaRuta, proximaEspecial, proximaVoz, fase, sacudida;
        bool accion, muerto, explotado, emergiendo;
        Vector3 posCuerpo;

        public bool Muerto => muerto;
        public Vector3 Pecho => transform.position + Vector3.up * (1.2f * transform.lossyScale.y);

        // ───────── Creación ─────────
        public static NAZombie Crear(TipoZombie tipo, Vector3 pos, bool emerger)
        {
            var g = new GameObject("Zombie_" + tipo);
            g.transform.position = pos;
            float escala = tipo == TipoZombie.Compadre ? 1.45f : tipo == TipoZombie.Sacristan ? 2.1f : tipo == TipoZombie.Corredor ? 0.95f : 1f;
            float ancho = tipo == TipoZombie.Compadre ? 1.5f : tipo == TipoZombie.Corredor ? 0.8f : 1f;

            var col = g.AddComponent<CapsuleCollider>();
            col.height = 1.9f * escala; col.radius = 0.38f * ancho * escala; col.center = Vector3.up * col.height / 2f;
            var ag = g.AddComponent<NavMeshAgent>();
            ag.height = col.height; ag.radius = col.radius; ag.acceleration = 16f; ag.angularSpeed = 600f;
            ag.avoidancePriority = Random.Range(30, 70);
            var h = g.AddComponent<Forja3DHealth>();
            h.invulnerable = 0f;
            var z = g.AddComponent<NAZombie>();
            z.tipo = tipo;
            int vidaMax;
            switch (tipo)
            {
                case TipoZombie.Corredor: vidaMax = 3; z.velocidad = 5.0f; z.rango = 1.4f; z.danio = 1; z.enfriamiento = 1.0f; break;
                case TipoZombie.Escupidor: vidaMax = 4; z.velocidad = 2.4f; z.rango = 1.4f; z.danio = 1; z.enfriamiento = 2.2f; break;
                case TipoZombie.Compadre: vidaMax = 20; z.velocidad = 1.7f; z.rango = 2.4f; z.danio = 2; z.enfriamiento = 2.4f; break;
                case TipoZombie.Planidera: vidaMax = 4; z.velocidad = 3.0f; z.rango = 1.4f; z.danio = 1; z.enfriamiento = 1.3f; break;
                case TipoZombie.Cuetero: vidaMax = 2; z.velocidad = 4.3f; z.rango = 2.0f; z.danio = 2; z.enfriamiento = 1f; break;
                case TipoZombie.Sacristan: vidaMax = 90; z.velocidad = 2.3f; z.rango = 3.0f; z.danio = 2; z.enfriamiento = 2.0f; z.esJefe = true; break;
                default: vidaMax = 4; z.velocidad = 2.1f; z.rango = 1.5f; z.danio = 1; z.enfriamiento = 1.3f; break;
            }
            h.vidaMaxima = vidaMax;
            h.Curar(9999); // Health ya hizo Awake con la vida por defecto: la llena hasta el máximo nuevo
            ag.speed = z.velocidad;
            ag.stoppingDistance = z.rango * 0.7f;
            z.ConstruirCuerpo(escala, ancho);
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) ag.Warp(hit.position);
            if (emerger) z.StartCoroutine(z.Emerger());
            return z;
        }

        void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            vida = GetComponent<Forja3DHealth>();
            vida.AlRecibirDanio += AlRecibir;
            vida.AlMorir += Morir;
        }

        void OnEnable() => Vivos.Add(this);
        void OnDisable() => Vivos.Remove(this);

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p) { jugador = p.transform; vidaJugador = p.GetComponent<Forja3DHealth>(); }
            fase = Random.value * 10f;
            proximaEspecial = Time.time + Random.Range(2f, 4f);
            proximaVoz = Time.time + Random.Range(2f, 9f);
            if (esJefe) Forja3DGameManager.RegistrarJefe(vida, "EL SACRISTÁN");
            if (tipo == TipoZombie.Cuetero) StartCoroutine(Mecha());
        }

        // ───────── Cuerpo con piezas ─────────
        void ConstruirCuerpo(float escala, float ancho)
        {
            Color piel = NAMat.Hex("#6f7f5a"), ropa = NAMat.Hex("#5a4632"), ojos = NAMat.Hex("#ff9a2e"), pantalon = NAMat.Hex("#3a3328");
            switch (tipo)
            {
                case TipoZombie.Corredor: piel = NAMat.Hex("#8a8f86"); ropa = NAMat.Hex("#2f3a4a"); ojos = NAMat.Hex("#ff3020"); break;
                case TipoZombie.Escupidor: piel = NAMat.Hex("#6a8a3a"); ropa = NAMat.Hex("#3a3a2a"); ojos = NAMat.Hex("#b8ff3a"); break;
                case TipoZombie.Compadre: piel = NAMat.Hex("#5b5f4c"); ropa = NAMat.Hex("#3b2a20"); ojos = NAMat.Hex("#ff6a00"); break;
                case TipoZombie.Planidera: piel = NAMat.Hex("#d8d4cc"); ropa = NAMat.Hex("#e8e4dc"); ojos = Color.white; break;
                case TipoZombie.Cuetero: ropa = NAMat.Hex("#7a2a1a"); ojos = NAMat.Hex("#ffcc33"); break;
                case TipoZombie.Sacristan: piel = NAMat.Hex("#c9c2b0"); ropa = NAMat.Hex("#151515"); ojos = NAMat.Hex("#ff2200"); pantalon = ropa; break;
            }
            cuerpo = new GameObject("Cuerpo").transform;
            cuerpo.SetParent(transform, false);
            cuerpo.localScale = new Vector3(ancho, 1f, ancho) * escala;
            var c = cuerpo;
            bool vestido = tipo == TipoZombie.Planidera || tipo == TipoZombie.Sacristan;

            // Torso y cabeza
            NAMat.Pieza(PrimitiveType.Capsule, c, new Vector3(0, 1.08f, 0), new Vector3(0.55f, 0.42f, 0.38f), ropa);
            float cabeza = tipo == TipoZombie.Escupidor ? 0.5f : 0.38f;
            NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(0, 1.66f, 0.03f), Vector3.one * cabeza, piel);
            NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(-0.08f, 1.7f, 0.15f + (cabeza - 0.38f) * 0.5f), Vector3.one * 0.075f, ojos, default, 2.5f);
            NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(0.08f, 1.7f, 0.15f + (cabeza - 0.38f) * 0.5f), Vector3.one * 0.075f, ojos, default, 2.5f);
            if (tipo == TipoZombie.Escupidor) NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(0, 1.56f, 0.24f), new Vector3(0.18f, 0.1f, 0.1f), NAMat.Hex("#9dff2e"), default, 2f);

            // Brazos estirados hacia delante (la pose clásica)
            brazoI = new GameObject("BrazoI").transform; brazoI.SetParent(c, false); brazoI.localPosition = new Vector3(-0.3f, 1.32f, 0.05f);
            brazoD = new GameObject("BrazoD").transform; brazoD.SetParent(c, false); brazoD.localPosition = new Vector3(0.3f, 1.32f, 0.05f);
            NAMat.Pieza(PrimitiveType.Cube, brazoI, new Vector3(0, 0, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), piel);
            NAMat.Pieza(PrimitiveType.Cube, brazoD, new Vector3(0, 0, 0.3f), new Vector3(0.12f, 0.12f, 0.6f), piel);

            if (vestido)
            {
                // Falda o sotana acampanada
                NAMat.Pieza(PrimitiveType.Cylinder, c, new Vector3(0, 0.42f, 0), new Vector3(0.62f, 0.42f, 0.62f), ropa);
                piernaI = piernaD = null;
            }
            else
            {
                piernaI = new GameObject("PiernaI").transform; piernaI.SetParent(c, false); piernaI.localPosition = new Vector3(-0.13f, 0.78f, 0);
                piernaD = new GameObject("PiernaD").transform; piernaD.SetParent(c, false); piernaD.localPosition = new Vector3(0.13f, 0.78f, 0);
                NAMat.Pieza(PrimitiveType.Cube, piernaI, new Vector3(0, -0.39f, 0), new Vector3(0.17f, 0.78f, 0.17f), pantalon);
                NAMat.Pieza(PrimitiveType.Cube, piernaD, new Vector3(0, -0.39f, 0), new Vector3(0.17f, 0.78f, 0.17f), pantalon);
            }

            switch (tipo)
            {
                case TipoZombie.Compadre:
                    NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(0, 0.95f, 0.12f), new Vector3(0.62f, 0.55f, 0.5f), ropa); // panza
                    break;
                case TipoZombie.Planidera:
                    NAMat.Pieza(PrimitiveType.Cube, c, new Vector3(0, 1.72f, -0.04f), new Vector3(0.44f, 0.5f, 0.44f), NAMat.Hex("#111111")); // velo negro
                    break;
                case TipoZombie.Cuetero:
                    // Atado de cohetes a la espalda
                    NAMat.Pieza(PrimitiveType.Cylinder, c, new Vector3(-0.12f, 1.25f, -0.28f), new Vector3(0.1f, 0.35f, 0.1f), NAMat.Hex("#d62828"));
                    NAMat.Pieza(PrimitiveType.Cylinder, c, new Vector3(0.0f, 1.3f, -0.3f), new Vector3(0.1f, 0.4f, 0.1f), NAMat.Hex("#f4c430"));
                    NAMat.Pieza(PrimitiveType.Cylinder, c, new Vector3(0.12f, 1.25f, -0.28f), new Vector3(0.1f, 0.35f, 0.1f), NAMat.Hex("#2b59c3"));
                    break;
                case TipoZombie.Sacristan:
                    // Campana de bronce en la mano derecha
                    NAMat.Pieza(PrimitiveType.Cylinder, brazoD, new Vector3(0, -0.18f, 0.62f), new Vector3(0.28f, 0.16f, 0.28f), NAMat.Hex("#b08d3c"), default, 0.3f);
                    NAMat.Pieza(PrimitiveType.Sphere, c, new Vector3(0, 1.86f, 0.0f), new Vector3(0.42f, 0.2f, 0.42f), ropa); // capucha
                    break;
            }
            posCuerpo = cuerpo.localPosition;
        }

        // Sale de la tierra (lejos del jugador: no es un aviso, es cómo llegan)
        IEnumerator Emerger()
        {
            emergiendo = true;
            var fin = posCuerpo; var ini = fin + Vector3.down * 1.9f;
            Forja3DFX.Polvo(transform.position, 0.9f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.55f)
            {
                if (cuerpo) cuerpo.localPosition = Vector3.Lerp(ini, fin, t);
                yield return null;
            }
            if (cuerpo) cuerpo.localPosition = fin;
            emergiendo = false;
        }

        // ───────── IA ─────────
        void Update()
        {
            if (muerto) return;
            if (!jugador) { var p = GameObject.FindWithTag("Player"); if (p) { jugador = p.transform; vidaJugador = p.GetComponent<Forja3DHealth>(); } return; }
            Animar();
            bool jugadorVivo = vidaJugador == null || !vidaJugador.Muerto;
            if (Forja3DGameManager.Bloqueado || !jugadorVivo || emergiendo || !agente.isOnNavMesh)
            {
                if (agente.isOnNavMesh) agente.isStopped = true;
                return;
            }
            Forja3DAudio.EnCombate();
            if (accion) return;
            agente.isStopped = false;

            var haciaJ = jugador.position - transform.position; haciaJ.y = 0;
            float d = haciaJ.magnitude;

            if (Time.time > proximaVoz)
            {
                proximaVoz = Time.time + Random.Range(5f, 11f);
                NASonido.Sonar("gemido", Pecho, 0.35f, tipo == TipoZombie.Compadre || esJefe ? 0.7f : tipo == TipoZombie.Planidera ? 1.5f : 1f);
            }

            switch (tipo)
            {
                case TipoZombie.Corredor:
                    if (d > 2.6f && d < 7f && Time.time > proximaEspecial && Visible()) { StartCoroutine(Embestida()); return; }
                    break;
                case TipoZombie.Escupidor:
                    if (d < 15f && d > 4.5f && Time.time > proximaEspecial && Visible()) { StartCoroutine(Escupir()); return; }
                    if (d < 6f && d > rango * 1.2f)
                    {
                        // Retrocede para mantener la distancia
                        var lejos = transform.position - haciaJ.normalized * 4f;
                        if (Time.time > proximaRuta && NavMesh.SamplePosition(lejos, out var h2, 3f, NavMesh.AllAreas)) { proximaRuta = Time.time + 0.3f; agente.SetDestination(h2.position); }
                        Mirar(haciaJ);
                        return;
                    }
                    if (d >= 6f && d <= 12f && Visible()) { agente.ResetPath(); Mirar(haciaJ); return; }
                    break;
                case TipoZombie.Planidera:
                    if (d < 16f && Time.time > proximaEspecial && Visible()) { StartCoroutine(Gritar()); return; }
                    break;
                case TipoZombie.Cuetero:
                    if (d <= rango) { Explotar(); return; }
                    break;
                case TipoZombie.Sacristan:
                    if (Time.time > proximaEspecial) { StartCoroutine(Campana()); return; }
                    break;
            }

            // Persecución
            if (Time.time > proximaRuta) { proximaRuta = Time.time + 0.2f; agente.SetDestination(jugador.position); }
            if (d <= rango && Time.time > proximoAtaque && tipo != TipoZombie.Cuetero) StartCoroutine(Golpe(haciaJ));
        }

        bool Visible()
        {
            var desde = Pecho; var hasta = jugador.position + Vector3.up * 1.2f;
            foreach (var hit in Physics.RaycastAll(desde, (hasta - desde).normalized, Vector3.Distance(desde, hasta), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform == jugador || hit.transform.IsChildOf(jugador)) continue;
                if (hit.collider.GetComponentInParent<NAZombie>()) continue;
                return false;
            }
            return true;
        }

        void Mirar(Vector3 dir)
        {
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }

        // Caminar: balanceo de brazos y piernas
        void Animar()
        {
            if (!cuerpo) return;
            float v = agente && agente.enabled ? agente.velocity.magnitude : 0f;
            fase += Time.deltaTime * (2.5f + v * 2.2f);
            float s = Mathf.Sin(fase) * Mathf.Clamp01(v / 2f);
            if (piernaI) piernaI.localRotation = Quaternion.Euler(s * 30f, 0, 0);
            if (piernaD) piernaD.localRotation = Quaternion.Euler(-s * 30f, 0, 0);
            if (!accion)
            {
                if (brazoI) brazoI.localRotation = Quaternion.Euler(-s * 8f + Mathf.Sin(fase * 0.5f) * 4f, 0, 0);
                if (brazoD) brazoD.localRotation = Quaternion.Euler(s * 8f - Mathf.Sin(fase * 0.5f) * 4f, 0, 0);
            }
            sacudida = Mathf.MoveTowards(sacudida, 0f, Time.deltaTime * 4f);
            if (!emergiendo) cuerpo.localPosition = posCuerpo + Vector3.up * Mathf.Abs(Mathf.Sin(fase)) * 0.05f * Mathf.Clamp01(v);
            cuerpo.localRotation = Quaternion.Euler(8f + sacudida * 25f, Mathf.Sin(fase * 0.5f) * 6f, Mathf.Sin(fase) * 4f * Mathf.Clamp01(v));
        }

        // ───────── Ataques ─────────
        IEnumerator Golpe(Vector3 haciaJ)
        {
            accion = true;
            agente.isStopped = true;
            transform.rotation = Quaternion.LookRotation(haciaJ);
            bool area = tipo == TipoZombie.Compadre || tipo == TipoZombie.Sacristan;
            // Brazos arriba y abajo de golpe (sin destellos de aviso)
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.2f) { Brazos(-60f * t); yield return null; }
            Brazos(35f);
            NASonido.Sonar(area ? "golpe" : "mordida", Pecho, 0.8f, area ? 0.8f : 1f);
            if (area)
            {
                Forja3DFX.Polvo(transform.position + transform.forward * 1f, rango);
                Forja3DFX.Temblor(0.3f, 0.25f);
                if (Distancia() <= rango * 1.25f) { Danar(danio); NAJugador.Empujar(jugador.position - transform.position, 9f); }
            }
            else if (Distancia() <= rango * 1.35f) Danar(danio);
            yield return new WaitForSeconds(0.3f);
            Brazos(0f);
            proximoAtaque = Time.time + enfriamiento * Random.Range(0.85f, 1.2f);
            accion = false;
        }

        IEnumerator Embestida()
        {
            accion = true;
            proximaEspecial = Time.time + 3.2f;
            var dir = jugador.position - transform.position; dir.y = 0; dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir);
            Brazos(-50f);
            bool golpeo = false;
            for (float t = 0; t < 0.4f; t += Time.deltaTime)
            {
                if (muerto) yield break;
                if (agente.isOnNavMesh) agente.Move(dir * 12f * Time.deltaTime);
                if (!golpeo && Distancia() < 1.3f) { golpeo = true; Danar(danio); NASonido.Sonar("mordida", Pecho, 0.9f); NAJugador.Empujar(dir, 6f); break; }
                yield return null;
            }
            Brazos(0f);
            yield return new WaitForSeconds(0.25f);
            proximoAtaque = Time.time + enfriamiento;
            accion = false;
        }

        IEnumerator Escupir()
        {
            accion = true;
            agente.ResetPath();
            proximaEspecial = Time.time + enfriamiento * Random.Range(0.9f, 1.3f);
            var dir = jugador.position - transform.position; dir.y = 0;
            for (float t = 0; t < 0.25f; t += Time.deltaTime) { Mirar(dir); yield return null; }
            NASonido.Sonar("escupitajo", Pecho, 0.9f);
            var boca = transform.position + Vector3.up * 1.55f * transform.lossyScale.y + transform.forward * 0.4f;
            NAAcido.Lanzar(boca, jugador.position + Vector3.up * 1f, danio, transform);
            yield return new WaitForSeconds(0.35f);
            accion = false;
        }

        IEnumerator Gritar()
        {
            accion = true;
            agente.isStopped = true;
            proximaEspecial = Time.time + 12f;
            Brazos(-100f);
            NASonido.Sonar("grito", Pecho, 1f);
            Forja3DFX.Almas(Pecho, 12);
            Forja3DFX.Temblor(0.15f, 0.8f);
            if (Distancia() < 16f) NAJugador.Ralentizar(2.5f);
            NAOleadas.Refuerzos(2, TipoZombie.Caminante);
            yield return new WaitForSeconds(1.1f);
            Brazos(0f);
            accion = false;
        }

        IEnumerator Campana()
        {
            accion = true;
            agente.isStopped = true;
            proximaEspecial = Time.time + 15f;
            for (int i = 0; i < 3; i++)
            {
                Brazos(-70f); yield return new WaitForSeconds(0.18f);
                Brazos(10f);
                NASonido.Sonar("campana", Pecho, 1f, 0.9f + i * 0.05f);
                Forja3DFX.Chispas(Pecho + transform.forward * 1.2f, 10);
                yield return new WaitForSeconds(0.35f);
            }
            Forja3DFX.Temblor(0.35f, 0.6f);
            Forja3DGameManager.Mensaje("¡LA CAMPANA LLAMA A LOS MUERTOS!");
            NAOleadas.Refuerzos(4, TipoZombie.Caminante);
            Brazos(0f);
            accion = false;
        }

        IEnumerator Mecha()
        {
            while (!muerto)
            {
                Forja3DFX.Chispas(transform.position + Vector3.up * 1.75f - transform.forward * 0.3f, 3);
                yield return new WaitForSeconds(0.15f);
            }
        }

        void Explotar()
        {
            if (explotado) return;
            explotado = true;
            NAExplosion.En(transform.position + Vector3.up, 3.4f, danio, 4, transform);
            if (!muerto) vida.Danio(9999, transform);
        }

        void Brazos(float x)
        {
            if (brazoI) brazoI.localRotation = Quaternion.Euler(x, 0, 0);
            if (brazoD) brazoD.localRotation = Quaternion.Euler(x, 0, 0);
        }

        float Distancia() { var d = jugador.position - transform.position; d.y = 0; return d.magnitude; }

        void Danar(int cantidad) { if (vidaJugador) vidaJugador.Danio(cantidad, transform, true); }

        // ───────── Daño y muerte ─────────
        public void Empujar(Vector3 dir, float fuerza)
        {
            if (muerto || !agente.isOnNavMesh) return;
            float peso = tipo == TipoZombie.Compadre ? 0.25f : esJefe ? 0.1f : 1f;
            dir.y = 0;
            agente.Move(dir.normalized * fuerza * 0.12f * peso);
        }

        void AlRecibir()
        {
            sacudida = 1f;
            NASonido.Sonar("impacto", Pecho, 0.6f);
        }

        void Morir()
        {
            if (muerto) return;
            muerto = true;
            Vivos.Remove(this);
            StopAllCoroutines();
            if (tipo == TipoZombie.Cuetero) Explotar();
            if (agente.isOnNavMesh) agente.ResetPath();
            agente.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Forja3DFX.Sangre(Pecho, Vector3.up, esJefe ? 70 : 26);
            NASonido.Sonar("gemido", Pecho, 0.6f, esJefe ? 0.5f : 0.8f);
            if (esJefe)
            {
                Forja3DFX.Temblor(0.6f, 1.2f);
                Forja3DGameManager.Mensaje("EL SACRISTÁN HA CAÍDO");
                Forja3DGameManager.JefeDerrotado();
            }
            NAOleadas.ZombieMuerto(this);
            NASuministro.TalVezSoltar(transform.position, esJefe ? 1f : tipo == TipoZombie.Compadre ? 0.8f : 0.3f);
            StartCoroutine(Caer());
        }

        IEnumerator Caer()
        {
            var ini = transform.rotation; var fin = ini * Quaternion.Euler(-82f, 0, 0);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f) { transform.rotation = Quaternion.Slerp(ini, fin, t * t); yield return null; }
            yield return new WaitForSeconds(2.5f);
            var p = transform.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.2f) { transform.position = p + Vector3.down * 1.5f * t; yield return null; }
            Destroy(gameObject);
        }
    }

    // ───────── Escupitajo de ácido (parábola) ─────────
    public class NAAcido : MonoBehaviour
    {
        Vector3 vel; int danio; Transform autor; float muere;
        static readonly Vector3 G = new Vector3(0, -9.81f, 0);

        public static void Lanzar(Vector3 desde, Vector3 hacia, int danio, Transform autor)
        {
            var g = NAMat.Pieza(PrimitiveType.Sphere, null, desde, Vector3.one * 0.28f, NAMat.Hex("#9dff2e"), default, 2.5f).gameObject;
            g.name = "Acido";
            var a = g.AddComponent<NAAcido>();
            float dist = Vector3.Distance(desde, hacia);
            float t = Mathf.Clamp(dist / 13f, 0.35f, 1.3f);
            a.vel = (hacia - desde) / t - 0.5f * G * t;
            a.danio = danio; a.autor = autor; a.muere = Time.time + 4f;
        }

        void Update()
        {
            if (Time.time > muere) { Destroy(gameObject); return; }
            var paso = vel * Time.deltaTime;
            vel += G * Time.deltaTime;
            var jugador = NAJugador.Transform;
            if (jugador && Vector3.Distance(transform.position, jugador.position + Vector3.up * 1f) < 0.9f)
            {
                var h = jugador.GetComponent<Forja3DHealth>();
                if (h) h.Danio(danio, autor, true);
                Salpicar(); return;
            }
            if (Physics.Raycast(transform.position, paso.normalized, out var hit, paso.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<NAZombie>())
            {
                if (hit.collider.CompareTag("Player")) { var h = hit.collider.GetComponentInParent<Forja3DHealth>(); if (h) h.Danio(danio, autor, true); }
                Salpicar(); return;
            }
            transform.position += paso;
        }

        void Salpicar()
        {
            Forja3DFX.Chispas(transform.position, 8);
            NASonido.Sonar("escupitajo", transform.position, 0.5f, 1.4f);
            Destroy(gameObject);
        }
    }
}
