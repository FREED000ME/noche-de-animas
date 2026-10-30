// Forja3D — IA de enemigos con tácticas de grupo.
//
//   Patrulla → (te ve) Alerta: ruge y avisa a los cercanos → Acecho: te rodea en círculo, sin lanzarse todos a la vez
//   → toma un "turno de ataque" (máx. 2 enemigos atacando a la vez) → Preparando: DESTELLO de aviso
//        · amarillo = golpe normal: se puede bloquear y hacer PARRY
//        · rojo     = golpe imparable: hay que ESQUIVAR (los jefes marcan además un anillo en el suelo)
//   → Atacando → Recuperando (momento para castigarle) → vuelve a acechar.
//   Parry → Aturdido: el siguiente golpe del jugador es un golpe de gracia (x3).
//   Con poca vida, los enemigos comunes a veces se retiran y vuelven. Los jefes enfurecen al 50 %.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// EXPANSIÓN FUTURA: tipos de enemigo por datos (arquero, kamikaze, invocador) cambiando solo
// los parámetros de abajo o heredando de esta clase y sobrescribiendo ElegirAtaque().
namespace Forja3D
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Forja3DHealth))]
    public class Forja3DEnemy : MonoBehaviour
    {
        public float radioDeteccion = 9f;
        public float radioAtaque = 1.6f;
        public float radioPatrulla = 4f;
        public float enfriamiento = 1.4f;
        public int danio = 1;
        public float velocidad = 2.8f;
        public bool esJefe;
        [Range(0, 1)] public float probImparable = 0.2f;
        public string voz = "condenado";   // clips voz_<voz>, muerte_<voz>
        public string nombre = "";

        public static readonly List<Forja3DEnemy> Activos = new List<Forja3DEnemy>();
        static readonly List<Forja3DEnemy> conTurno = new List<Forja3DEnemy>();
        const int MaxAtacantes = 2;

        enum Estado { Patrulla, Alerta, Acecho, Acercarse, Preparando, Atacando, Recuperando, Aturdido, Retirada }
        Estado estado = Estado.Patrulla;

        NavMeshAgent agente;
        Animator anim;
        Forja3DHealth vida;
        Transform jugador;
        Forja3DHealth vidaJugador;
        Collider col;
        Vector3 origen, empuje;
        float tEstado, proximaPatrulla, proximoAtaque, proximaRuta, proximaVoz, sentidoGiro = 1f, aturdidoHasta;
        bool imparable, furioso, superArmadura;
        int golpesEnCadena;

        public bool Muerto => vida && vida.Muerto;
        // Radio horizontal real (para separarse del jugador sin colisión física)
        public float Radio => col is CapsuleCollider cap ? cap.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.5f;
        public bool Aturdido => estado == Estado.Aturdido && Time.time < aturdidoHasta;

        void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            vida = GetComponent<Forja3DHealth>();
            col = GetComponent<Collider>();
            agente.speed = velocidad;
            agente.acceleration = 14f;
            agente.angularSpeed = 540f;
            agente.stoppingDistance = 0.2f;
            agente.avoidancePriority = Random.Range(30, 70);
            vida.invulnerable = Mathf.Min(vida.invulnerable, 0.12f); // los combos del jugador deben conectar
            if (anim) anim.applyRootMotion = false;
            Forja3DSinRaiz.Poner(anim);
            vida.AlMorir += Morir;
            vida.AlRecibirDanio += AlRecibirGolpe;
        }

        void OnEnable() => Activos.Add(this);
        void OnDisable() { Activos.Remove(this); SoltarTurno(); }

        void Start()
        {
            origen = transform.position;
            var p = GameObject.FindWithTag("Player");
            if (p) { jugador = p.transform; vidaJugador = p.GetComponent<Forja3DHealth>(); }
            if (esJefe) { Forja3DGameManager.RegistrarJefe(vida, string.IsNullOrEmpty(nombre) ? "JEFE" : nombre); superArmadura = true; voz = voz == "condenado" ? "jefe" : voz; }
            else Forja3DGameManager.RegistrarEnemigo();
            sentidoGiro = Random.value < 0.5f ? -1f : 1f;
            proximaVoz = Time.time + Random.Range(2f, 8f);
        }

        Vector3 Cabeza => col ? new Vector3(transform.position.x, col.bounds.max.y + 0.2f, transform.position.z) : transform.position + Vector3.up * 2.1f;
        float Escala => transform.lossyScale.y;

        void Cambiar(Estado e) { estado = e; tEstado = Time.time; }
        float EnEstado => Time.time - tEstado;

        void Update()
        {
            if (vida.Muerto) return;
            Forja3DAnim.Float(anim, "Speed", agente.enabled ? agente.velocity.magnitude : 0f);
            if (!agente.isOnNavMesh) return;

            // Empujón (golpes del jugador)
            if (empuje.sqrMagnitude > 0.01f)
            {
                agente.Move(empuje * Time.deltaTime);
                empuje = Vector3.MoveTowards(empuje, Vector3.zero, 18f * Time.deltaTime);
            }

            bool jugadorVivo = jugador && (!vidaJugador || !vidaJugador.Muerto);
            if (Forja3DGameManager.Bloqueado || !jugadorVivo)
            {
                if (estado != Estado.Patrulla) { SoltarTurno(); agente.ResetPath(); Cambiar(Estado.Patrulla); }
                return;
            }

            var haciaJ = jugador.position - transform.position; haciaJ.y = 0;
            float d = haciaJ.magnitude;
            if (estado != Estado.Patrulla) Forja3DAudio.EnCombate();

            switch (estado)
            {
                case Estado.Patrulla:
                    agente.updateRotation = true;
                    agente.speed = velocidad * 0.5f;
                    if (d <= radioDeteccion) { Alertar(); break; }
                    if (Time.time > proximaPatrulla)
                    {
                        proximaPatrulla = Time.time + Random.Range(2f, 5f);
                        var r = origen + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * radioPatrulla;
                        if (NavMesh.SamplePosition(r, out var hit, 2f, NavMesh.AllAreas)) agente.SetDestination(hit.position);
                    }
                    break;

                case Estado.Alerta:
                    Mirar(haciaJ, 8f);
                    if (EnEstado > (esJefe ? 1.2f : 0.6f)) Cambiar(Estado.Acecho);
                    break;

                case Estado.Acecho:
                    Acechar(haciaJ, d);
                    break;

                case Estado.Acercarse:
                    agente.updateRotation = true;
                    agente.speed = velocidad * (furioso ? 1.6f : 1.35f);
                    if (Time.time > proximaRuta) { proximaRuta = Time.time + 0.15f; agente.SetDestination(jugador.position); }
                    if (d <= radioAtaque * 1.05f) { agente.ResetPath(); Preparar(); }
                    else if (EnEstado > 3.5f) { SoltarTurno(); Cambiar(Estado.Acecho); } // no llega: vuelve a rodear
                    break;

                case Estado.Preparando:
                    Mirar(haciaJ, imparable ? 3f : 6f); // sigue girando un poco: no basta con quedarse quieto
                    if (EnEstado >= TiempoAviso()) StartCoroutine(Atacar());
                    break;

                case Estado.Recuperando:
                    Mirar(haciaJ, 3f);
                    if (EnEstado > (esJefe ? 0.9f : 0.75f))
                    {
                        if (!esJefe && vida.Vida <= vida.vidaMaxima * 0.3f && Random.value < 0.4f) Retirarse();
                        else Cambiar(Estado.Acecho);
                    }
                    break;

                case Estado.Aturdido:
                    if (Time.time >= aturdidoHasta) { if (anim) anim.speed = 1f; Cambiar(Estado.Recuperando); }
                    else if (Random.value < 6f * Time.deltaTime) Forja3DFX.Chispas(Cabeza, 2);
                    break;

                case Estado.Retirada:
                    agente.updateRotation = false;
                    Mirar(haciaJ, 6f);
                    if (Time.time > proximaRuta)
                    {
                        proximaRuta = Time.time + 0.3f;
                        var lejos = transform.position - haciaJ.normalized * 4f;
                        if (NavMesh.SamplePosition(lejos, out var h2, 3f, NavMesh.AllAreas)) agente.SetDestination(h2.position);
                    }
                    if (EnEstado > 2.2f) Cambiar(Estado.Acecho);
                    break;
            }

            // Voces ocasionales en combate
            if (estado != Estado.Patrulla && Time.time > proximaVoz)
            {
                proximaVoz = Time.time + Random.Range(4f, 9f);
                Forja3DAudio.Sonar("voz_" + voz, Cabeza, esJefe ? 1f : 0.55f, 0.12f);
            }
        }

        // ── Acecho: rodear al jugador a media distancia y esperar turno ──
        void Acechar(Vector3 haciaJ, float d)
        {
            agente.updateRotation = false;
            agente.speed = velocidad * 0.75f;
            Mirar(haciaJ, 8f);
            float radio = radioAtaque + (esJefe ? 2.2f : 1.8f);
            if (Time.time > proximaRuta)
            {
                proximaRuta = Time.time + 0.25f;
                if (Random.value < 0.04f) sentidoGiro = -sentidoGiro; // cambia de lado a veces
                var desdeJ = -haciaJ.normalized;
                var orbita = Quaternion.Euler(0, sentidoGiro * 35f, 0) * desdeJ * radio;
                var destino = jugador.position + orbita;
                if (NavMesh.SamplePosition(destino, out var hit, 2.5f, NavMesh.AllAreas)) agente.SetDestination(hit.position);
                else sentidoGiro = -sentidoGiro; // pared: gira al otro lado
            }
            if (Time.time > proximoAtaque && PedirTurno())
            {
                golpesEnCadena = 0;
                if (d <= radioAtaque * 1.05f) Preparar(); else Cambiar(Estado.Acercarse);
            }
        }

        bool PedirTurno()
        {
            if (esJefe || conTurno.Contains(this)) return true;
            conTurno.RemoveAll(e => !e || e.Muerto);
            if (conTurno.Count >= MaxAtacantes) return false;
            conTurno.Add(this); return true;
        }
        void SoltarTurno() => conTurno.Remove(this);

        void Alertar()
        {
            if (estado != Estado.Patrulla) return;
            agente.ResetPath();
            Cambiar(Estado.Alerta);
            Forja3DAnim.Trigger(anim, "Alert");
            Forja3DAudio.Sonar("voz_" + voz, Cabeza, esJefe ? 1f : 0.7f, 0.1f);
            if (esJefe) Forja3DFX.Temblor(0.2f, 0.6f);
            proximoAtaque = Time.time + Random.Range(0.8f, 2f);
            // La manada se avisa entre sí
            foreach (var e in Activos)
                if (e != this && e.estado == Estado.Patrulla && Vector3.Distance(e.transform.position, transform.position) < 9f) e.Alertar();
        }

        float TiempoAviso() => (esJefe ? 0.8f : 0.55f) * (imparable ? 1.3f : 1f) * (furioso ? 0.75f : 1f) * (golpesEnCadena > 0 ? 0.6f : 1f);

        void Preparar()
        {
            agente.ResetPath();
            agente.updateRotation = false;
            imparable = Random.value < (esJefe ? probImparable + 0.15f : probImparable);
            Cambiar(Estado.Preparando);
            Forja3DFX.Aviso(Cabeza, imparable ? Forja3DFX.Rojo : Forja3DFX.Amarillo, esJefe ? 2f : 1.3f);
            Forja3DAudio.Sonar(imparable ? "aviso_imparable" : "aviso", Cabeza, 0.8f, 0.03f);
            if (esJefe && imparable) Forja3DFX.Anillo(transform.position, RadioArea, TiempoAviso() + 0.15f);
        }

        float RadioArea => radioAtaque * 1.8f;

        IEnumerator Atacar()
        {
            Cambiar(Estado.Atacando);
            bool area = esJefe && imparable;
            if (Forja3DAnim.Tiene(anim, "Attack", AnimatorControllerParameterType.Trigger))
            {
                string t = esJefe && furioso && Forja3DAnim.Tiene(anim, "Skill1", AnimatorControllerParameterType.Trigger) && Random.value < 0.5f ? "Skill1" : "Attack";
                Forja3DAnim.Trigger(anim, t);
                StartCoroutine(Arremeter(0.25f, area ? 0f : 1.1f));
            }
            else StartCoroutine(Embestida());
            Forja3DAudio.Sonar(esJefe ? "golpe_pesado_aire" : "garra_aire", transform.position + Vector3.up, 0.8f);
            yield return new WaitForSeconds(0.28f);
            if (estado != Estado.Atacando || vida.Muerto) yield break;

            if (area)
            {
                Forja3DFX.Polvo(transform.position, RadioArea);
                Forja3DFX.Temblor(0.5f, 0.4f);
                Forja3DAudio.Sonar("impacto_suelo", transform.position, 1f);
                if (jugador && Vector3.Distance(jugador.position, transform.position) <= RadioArea && vidaJugador) vidaJugador.Danio(danio + 1, transform, true);
            }
            else if (jugador)
            {
                var a = jugador.position - transform.position; a.y = 0;
                if (a.magnitude <= radioAtaque * 1.35f && Vector3.Angle(transform.forward, a) < 75f && vidaJugador)
                    vidaJugador.Danio(danio, transform, imparable);
            }

            // Cadena de golpes: jefes furiosos y a veces los comunes
            golpesEnCadena++;
            bool otro = golpesEnCadena < (esJefe ? (furioso ? 3 : 2) : 2) && Random.value < (esJefe ? 0.55f : 0.25f);
            yield return new WaitForSeconds(0.25f);
            if (estado != Estado.Atacando || vida.Muerto) yield break;
            if (otro && jugador && Vector3.Distance(jugador.position, transform.position) < radioAtaque * 1.6f) { Preparar(); yield break; }

            proximoAtaque = Time.time + enfriamiento * Random.Range(0.8f, 1.4f) * (furioso ? 0.65f : 1f);
            SoltarTurno();
            Cambiar(Estado.Recuperando);
        }

        // Avance corto durante el golpe (con clip de ataque)
        IEnumerator Arremeter(float dur, float distancia)
        {
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (jugador && Vector3.Distance(jugador.position, transform.position) < Radio + 0.6f) break; // no se mete encima de Dante
                if (agente.isOnNavMesh) agente.Move(transform.forward * (distancia / dur) * Time.deltaTime);
                yield return null;
            }
        }

        void Mirar(Vector3 dir, float vel)
        {
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), vel * Time.deltaTime);
        }

        void Retirarse()
        {
            SoltarTurno();
            Cambiar(Estado.Retirada);
        }

        // ── Reacciones ──
        public void Aturdir(float segundos)
        {
            if (vida.Muerto) return;
            StopAllCoroutines();
            SoltarTurno();
            agente.ResetPath();
            aturdidoHasta = Time.time + segundos;
            Cambiar(Estado.Aturdido);
            Forja3DAnim.Trigger(anim, "Hit");
            if (anim) anim.speed = 0.6f;
            empuje = -transform.forward * 3f;
            StartCoroutine(Tambaleo(segundos));
        }

        public void Empujar(Vector3 v, bool fuerte)
        {
            empuje = superArmadura ? v * 0.3f : v;
            // Golpe fuerte a un enemigo común: lo derriba si tiene el clip
            if (fuerte && !superArmadura && !vida.Muerto && Forja3DAnim.Tiene(anim, "KnockDown", AnimatorControllerParameterType.Trigger) && Random.value < 0.5f)
            {
                StopAllCoroutines(); SoltarTurno();
                Forja3DAnim.Trigger(anim, "KnockDown");
                aturdidoHasta = Time.time + 1.4f; Cambiar(Estado.Aturdido);
                return;
            }
            // Los golpes fuertes interrumpen el ataque de los enemigos comunes
            if (fuerte && !superArmadura && (estado == Estado.Preparando || estado == Estado.Acercarse || estado == Estado.Acecho))
            {
                StopAllCoroutines();
                SoltarTurno();
                Cambiar(Estado.Recuperando);
            }
        }

        void AlRecibirGolpe()
        {
            if (estado == Estado.Patrulla) Alertar();
            Forja3DAudio.Sonar("voz_" + voz, Cabeza, esJefe ? 0.8f : 0.6f, 0.15f, 1.15f);
            if (!Forja3DAnim.Tiene(anim, "Hit", AnimatorControllerParameterType.Trigger)) StartCoroutine(Sacudida());
            if (esJefe && !furioso && vida.Vida <= vida.vidaMaxima / 2)
            {
                furioso = true;
                agente.speed = velocidad *= 1.25f;
                Forja3DFX.Temblor(0.45f, 0.8f);
                Forja3DAudio.Sonar("voz_" + voz, Cabeza, 1f, 0f, 0.8f);
                Forja3DGameManager.Mensaje($"¡{(string.IsNullOrEmpty(nombre) ? "EL JEFE" : nombre.ToUpperInvariant())} ENFURECE!");
            }
        }

        IEnumerator Tambaleo(float dur)
        {
            var v = anim ? anim.transform : transform.childCount > 0 ? transform.GetChild(0) : null;
            if (!v) yield break;
            var ini = v.localRotation;
            for (float t = 0; t < dur && !vida.Muerto; t += Time.deltaTime)
            {
                v.localRotation = ini * Quaternion.Euler(Mathf.Sin(t * 9f) * 6f * (1 - t / dur), 0, Mathf.Sin(t * 5f) * 10f * (1 - t / dur));
                yield return null;
            }
            v.localRotation = ini;
        }

        // Animaciones procedurales para criaturas que solo traen "caminar"
        IEnumerator Embestida()
        {
            var ini = transform.position;
            var dir = transform.forward * Mathf.Min(1.6f, radioAtaque * 0.7f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                if (t < 0.5f && jugador && Vector3.Distance(jugador.position, transform.position) < Radio + 0.5f) continue;
                if (agente.isOnNavMesh) agente.Move((ini + dir * Mathf.Sin(t * Mathf.PI) - transform.position));
                yield return null;
            }
        }

        IEnumerator Sacudida()
        {
            var esc = transform.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f)
            {
                transform.localScale = esc * (1f + 0.08f * Mathf.Sin(t * Mathf.PI * 3f));
                yield return null;
            }
            transform.localScale = esc;
        }

        // Apoya el cadáver en el suelo: mide el vértice más bajo de la malla ya deformada (los límites del
        // renderer son una caja y lo dejaban flotando)
        void Apoyar()
        {
            float bajo = float.MaxValue;
            var m = new Mesh();
            foreach (var sk in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                sk.BakeMesh(m, true);
                foreach (var v in m.vertices) bajo = Mathf.Min(bajo, sk.transform.TransformPoint(v).y);
            }
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && mf.GetComponent<Renderer>() && mf.GetComponent<Renderer>().enabled && !mf.GetComponentInParent<ParticleSystem>())
                    foreach (var v in mf.sharedMesh.vertices) bajo = Mathf.Min(bajo, mf.transform.TransformPoint(v).y);
            Destroy(m);
            if (bajo == float.MaxValue) return;
            float suelo = transform.position.y;
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) suelo = hit.point.y;
            transform.position += Vector3.up * (suelo - bajo + 0.02f);
        }

        IEnumerator Caida()
        {
            // Las fieras caen de costado; los bípedos, de espaldas
            bool cuadrupedo = voz == "fiera";
            var rot = transform.rotation; var fin = rot * (cuadrupedo ? Quaternion.Euler(0, 0, 88f) : Quaternion.Euler(-80f, 0, 0));
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.6f)
            {
                transform.rotation = Quaternion.Slerp(rot, fin, t * t);
                yield return null;
            }
            transform.rotation = fin;
            Apoyar();
            Forja3DAudio.Sonar("caida_cuerpo", transform.position, 0.7f);
        }

        IEnumerator CamaraLenta()
        {
            Time.timeScale = 0.25f;
            yield return new WaitForSecondsRealtime(1.3f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.5f) { Time.timeScale = Mathf.Lerp(0.25f, 1f, t); yield return null; }
            Time.timeScale = 1f;
        }

        // El cadáver se hunde en el suelo y desaparece (sin "pop")
        IEnumerator Hundirse(float espera)
        {
            yield return new WaitForSeconds(espera);
            var ini = transform.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.5f) { transform.position = ini + Vector3.down * 1.6f * t; yield return null; }
            Destroy(gameObject);
        }

        void Morir()
        {
            Forja3DProgreso.Ganar(esJefe ? 150 : voz == "fiera" ? 30 : 10, esJefe ? "jefe" : "alma");
            StopAllCoroutines();
            SoltarTurno();
            Activos.Remove(this);
            if (anim) anim.speed = 1f;
            if (agente.isOnNavMesh) agente.ResetPath();
            agente.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var aura = transform.Find("Aura");
            if (aura) { foreach (var ps in aura.GetComponentsInChildren<ParticleSystem>()) ps.Stop(); var l = aura.GetComponent<Light>(); if (l) l.enabled = false; }
            Forja3DAudio.Sonar("muerte_" + voz, Cabeza, 1f, 0.1f);
            Forja3DFX.Sangre(transform.position + Vector3.up, Vector3.up, esJefe ? 60 : 30);
            // El parámetro "Dead" existe siempre; lo que importa es si hay un estado con clip de muerte
            // (las fieras de Tripo solo traen "caminar": sin esto seguían andando en el sitio al morir)
            bool clipMuerte = anim && anim.HasState(0, Animator.StringToHash("Muerte"));
            if (!clipMuerte) { if (anim) anim.enabled = false; StartCoroutine(Caida()); }
            if (esJefe)
            {
                // Derrota del jefe: cámara lenta, temblor y el cuerpo queda en el suelo
                StartCoroutine(CamaraLenta());
                Forja3DFX.Temblor(0.6f, 1.2f);
                Forja3DFX.Polvo(transform.position, radioAtaque * 1.5f);
                Forja3DAudio.Sonar("impacto_suelo", transform.position, 1f, 0f, 0.7f);
                Forja3DGameManager.Mensaje($"{(string.IsNullOrEmpty(nombre) ? "EL JEFE" : nombre.ToUpperInvariant())} HA CAÍDO");
                Forja3DGameManager.JefeDerrotado();
            }
            else { Forja3DGameManager.EnemigoDerrotado(); StartCoroutine(Hundirse(4f)); }
        }
    }
}
