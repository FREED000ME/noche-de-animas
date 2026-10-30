// Forja3D — controlador en tercera persona con combate cuerpo a cuerpo.
// PC: WASD mover · Shift correr · Espacio saltar · Clic izq. atacar · Q cubrir · E esquivar · Clic der. + ratón cámara
// Android: joystick + botones ATACAR / CUBRIR / ESQUIVAR / SALTAR / CORRER y arrastrar a la derecha para la cámara.
//
// Combate:
//   · Combo de 3 golpes (el 3.º hace el doble de daño y empuja). Se orienta solo hacia el enemigo más cercano.
//   · CUBRIR mantenido bloquea los ataques con destello AMARILLO (gasta estamina).
//   · Pulsar CUBRIR justo antes del impacto (0,22 s) = PARRY: el enemigo queda aturdido y el siguiente golpe es letal.
//   · ESQUIVAR: voltereta con invulnerabilidad. Única defensa contra ataques con destello ROJO (imparables).
//   · CORRER: esprint, gasta estamina; se apaga al soltar el joystick.
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// EXPANSIÓN FUTURA: nuevas armas = otro valor de alcance/daño/velocidad (ScriptableObject por arma);
// en PC con mando se puede añadir "fijar objetivo" (lock-on) con el stick derecho pulsado.
namespace Forja3D
{
    [RequireComponent(typeof(CharacterController))]
    public class Forja3DPlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        public float velocidadTrote = 3.6f;
        public float velocidadCorrer = 6.6f;
        public float velocidadCubierto = 1.4f;
        public float giro = 12f;
        public float fuerzaSalto = 5f;
        public float gravedad = -20f;

        [Header("Combate")]
        public float alcanceAtaque = 1.35f; // espada; la hoz llega un 25 % más lejos
        public int danioAtaque = 1;
        public float ventanaParry = 0.22f;
        // Mejoras del árbol de virtudes (Forja3DProgreso)
        [HideInInspector] public int tercerGolpe = 2, multGracia = 3;
        [HideInInspector] public float invulnerableEsquiva = 0.42f;
        [HideInInspector] public bool robaVida;
        int golpesCerteros;

        [Header("Estamina")]
        public float estaminaMaxima = 100f;
        public float recuperacion = 32f;
        public float costoEsquiva = 24f, costoCorrer = 18f, costoBloqueo = 16f, costoAtaque = 6f;

        [Header("Cámara")]
        public bool crearCamaraAutomatica = true;
        public string nombreRecompensa = "";

        // ── Arma ganada (p. ej. la hoz de la Muerte): ya está en la mano, oculta, desde el editor ──
        Transform BuscarHijo(string n) { foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        public bool TieneRecompensa => BuscarHijo("ArmaRecompensa");
        public void CambiarARecompensa()
        {
            var nueva = BuscarHijo("ArmaRecompensa"); if (!nueva) return;
            var vieja = BuscarHijo("Arma"); if (vieja) Destroy(vieja.gameObject);
            nueva.gameObject.SetActive(true); nueva.name = "Arma";
            // La hoz desaparece de las manos del jefe caído: ahora es de Dante
            foreach (var e in FindObjectsByType<Forja3DEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (e && e.Muerto)
                    foreach (var t in e.GetComponentsInChildren<Transform>(true))
                        if (t.name == "ArmaJefe") { Forja3DFX.Chispas(t.position, 30); Destroy(t.gameObject); }
            alcanceAtaque *= 1.25f; // un arma de asta llega más lejos
            Forja3DProgreso.EscalarAlcanceBase(1.25f);
            // Con la hoz se cubre con el asta: el escudo se descarta
            var escudo = BuscarHijo("Escudo"); if (escudo) Destroy(escudo.gameObject);
            Forja3DFX.Chispas(nueva.position, 40);
            Forja3DAudio.Sonar("victoria", nueva.position, 0.6f);
            if (!string.IsNullOrEmpty(nombreRecompensa)) Forja3DGameManager.Mensaje(nombreRecompensa);
        }

        public float Estamina { get; private set; }
        public bool Cubriendo { get; private set; }
        public static Forja3DPlayerController Instancia { get; private set; }

        CharacterController cc;
        Animator anim;
        Transform visual, caderas;
        bool visualPropio => visual && visual != transform; // el Animator suele estar en el mismo objeto que el CharacterController

        Forja3DHealth vida;
        Transform cam;
        float vy, velAnim, ultimoMovimiento, ultimoGasto = -9f, inicioCubrir = -9f, proximoPaso, aturdidoHasta;
        bool atacando, esquivando, quiereOtroGolpe;
        int pasoCombo;
        float finCombo;
        Vector3 empuje;
        Vector3 ultimoSeguro; float fueraDesde = -1f, proximoChequeo;
        int capaTorso = -1; float pesoTorso, parryHasta;

        void Awake()
        {
            Instancia = this;
            cc = GetComponent<CharacterController>();
            anim = GetComponentInChildren<Animator>();
            visual = anim ? anim.transform : (transform.childCount > 0 ? transform.GetChild(0) : null);
            vida = GetComponent<Forja3DHealth>();
            if (anim) anim.applyRootMotion = false;
            if (anim && anim.isHuman) caderas = anim.GetBoneTransform(HumanBodyBones.Hips);
            Forja3DSinRaiz.Poner(anim);
            Estamina = estaminaMaxima;
            if (anim) { capaTorso = anim.GetLayerIndex("Torso"); if (capaTorso >= 0) anim.SetLayerWeight(capaTorso, 0f); }
        }

        void Start()
        {
            Forja3DProgreso.Aplicar(this);
            if (vida)
            {
                vida.Interceptar = Interceptar;
                vida.AlRecibirDanio += AlSerGolpeado;
                vida.AlMorir += () => Forja3DAudio.Sonar("muerte_dante", transform.position + Vector3.up);
            }
            cam = Camera.main ? Camera.main.transform : null;
            if (!crearCamaraAutomatica) return;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); cam = go.transform;
            }
            var seguir = cam.GetComponent<Forja3DCamera>();
            if (!seguir) seguir = cam.gameObject.AddComponent<Forja3DCamera>();
            if (!seguir.objetivo) seguir.objetivo = transform;
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        void Update()
        {
            bool muerto = vida && vida.Muerto;
            bool bloqueado = muerto || Forja3DGameManager.Bloqueado;
            bool aturdido = Time.time < aturdidoHasta;
            bool libre = !bloqueado && !aturdido;

            var entrada = bloqueado || aturdido ? Vector2.zero : Forja3DInput.Mover;
            Vector3 dir = new Vector3(entrada.x, 0, entrada.y);
            if (cam)
            {
                var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                dir = f * entrada.y + r * entrada.x;
            }
            if (entrada.sqrMagnitude > 0.01f) ultimoMovimiento = Time.time;
            else if (Time.time - ultimoMovimiento > 0.6f) Forja3DInput.CorrerActivo = false; // soltar el joystick apaga el esprint

            // ── Cubrirse ──
            bool cubrir = libre && !esquivando && Forja3DInput.Cubrir && Estamina > 1f;
            if (cubrir && !Cubriendo)
            {
                inicioCubrir = Time.time;
                Forja3DAudio.Sonar("cubrirse", transform.position + Vector3.up, 0.5f);
            }
            Cubriendo = cubrir;
            Forja3DAnim.Bool(anim, "Block", Cubriendo);
            // La capa del torso solo pesa mientras se cubre o hace parry: al soltar, los brazos vuelven a la animación normal
            if (capaTorso >= 0)
            {
                float objetivo = Cubriendo || Time.time < parryHasta ? 1f : 0f;
                pesoTorso = Mathf.MoveTowards(pesoTorso, objetivo, Time.deltaTime * (objetivo > pesoTorso ? 12f : 6f));
                anim.SetLayerWeight(capaTorso, pesoTorso);
            }
            Separarse();

            // ── Esquivar ──
            if (libre && !esquivando && cc.isGrounded && Forja3DInput.Esquivar && Estamina >= costoEsquiva * 0.5f)
                StartCoroutine(Esquivar(dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward));

            // ── Atacar (combo) ──
            if (libre && !esquivando && !Cubriendo && Forja3DInput.Atacar)
            {
                if (!atacando) StartCoroutine(Combo());
                else quiereOtroGolpe = true;
            }
            if (!atacando && Time.time > finCombo) pasoCombo = 0;

            // ── Movimiento ──
            bool corre = Forja3DInput.Correr && !Cubriendo && !atacando && entrada.sqrMagnitude > 0.04f && Estamina > 0f;
            float vel = entrada.magnitude * (Cubriendo ? velocidadCubierto : corre ? velocidadCorrer : velocidadTrote);
            if (atacando) vel *= 0.15f;
            if (corre) Gastar(costoCorrer * Time.deltaTime, false);
            if (corre && Estamina <= 0.5f) Forja3DInput.CorrerActivo = false;

            if (!esquivando)
            {
                Vector3 mirar = dir;
                if (Cubriendo || atacando) { var e = BuscarObjetivo(6f, 360f); if (e) { mirar = e.transform.position - transform.position; mirar.y = 0; } }
                if (mirar.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mirar), (atacando ? 18f : giro) * Time.deltaTime);
            }

            if (cc.isGrounded && vy < 0) vy = -2f;
            if (libre && !atacando && !esquivando && !Cubriendo && cc.isGrounded && Forja3DInput.Saltar) { vy = fuerzaSalto; Forja3DAnim.Trigger(anim, "Jump"); Forja3DAudio.Sonar("esquiva", transform.position, 0.4f); }
            vy += gravedad * Time.deltaTime;
            empuje = Vector3.ClampMagnitude(Vector3.MoveTowards(empuje, Vector3.zero, 14f * Time.deltaTime), 7f);
            empuje.y = 0f;
            if (!esquivando) cc.Move((dir.normalized * vel + empuje + Vector3.up * vy) * Time.deltaTime);

            // ── Estamina ──
            if (Time.time - ultimoGasto > 0.7f) Estamina = Mathf.Min(estaminaMaxima, Estamina + recuperacion * Time.deltaTime * (Cubriendo ? 0.35f : 1f));

            // ── Pasos ──
            if (cc.isGrounded && vel > 0.5f && !esquivando && Time.time > proximoPaso)
            {
                proximoPaso = Time.time + Mathf.Lerp(0.55f, 0.3f, vel / velocidadCorrer);
                Forja3DAudio.Sonar("paso", transform.position, Mathf.Lerp(0.25f, 0.55f, vel / velocidadCorrer), 0.15f);
            }

            velAnim = Mathf.Lerp(velAnim, esquivando ? 0f : vel, 10f * Time.deltaTime);
            Forja3DAnim.Float(anim, "Speed", velAnim);
            Forja3DAnim.Bool(anim, "Grounded", cc.isGrounded);

            VigilarLimites();
        }

        // Red de seguridad: recuerda el último punto pisable (sobre el NavMesh) y, si un golpe o un choque
        // deja a Dante fuera de la zona jugable (encima de un muro, fuera del mapa o cayendo), lo devuelve allí.
        // Ya no teletransporta solo por estar lejos del NavMesh (eso devolvía a Dante al punto donde empezó
        // la voltereta cuando terminaba junto a un árbol o un borde). Solo actúa si cae, si queda encima de un
        // muro o si sale del rectángulo jugable.
        void VigilarLimites()
        {
            if (Time.time < proximoChequeo) return;
            proximoChequeo = Time.time + 0.2f;
            if (esquivando) { fueraDesde = -1f; return; }
            bool enSuelo = NavMesh.SamplePosition(transform.position, out var hit, 1.5f, NavMesh.AllAreas);
            if (enSuelo && cc.isGrounded && Mathf.Abs(hit.position.y - transform.position.y) < 0.8f) ultimoSeguro = transform.position;
            if (zonaJugable.size.x < 1f && Time.timeSinceLevelLoad > 1f) CalcularZona();
            if (ultimoSeguro == Vector3.zero) { ultimoSeguro = transform.position; return; }
            var p = transform.position;
            bool cayendo = p.y < ultimoSeguro.y - 4f;
            bool fuera = zonaJugable.size.x > 1f && (p.x < zonaJugable.min.x || p.x > zonaJugable.max.x || p.z < zonaJugable.min.z || p.z > zonaJugable.max.z);
            bool encima = p.y > ultimoSeguro.y + 2.2f && cc.isGrounded;
            if (cayendo || fuera) { Reubicar(ultimoSeguro); return; }
            if (encima) { if (fueraDesde < 0f) fueraDesde = Time.time; if (Time.time - fueraDesde > 0.8f) Reubicar(ultimoSeguro); }
            else fueraDesde = -1f;
        }

        // Rectángulo jugable = la malla de navegación horneada (con 3 m de holgura).
        Bounds zonaJugable;
        void CalcularZona()
        {
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) return;
            var b = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (var v in tri.vertices) b.Encapsulate(v);
            b.Expand(new Vector3(6f, 100f, 6f));
            zonaJugable = b;
        }

        // Los enemigos no chocan físicamente con Dante (así ningún jefe lo empuja por encima de un muro);
        // en su lugar se separan con suavidad en horizontal.
        readonly System.Collections.Generic.HashSet<Collider> ignorados = new System.Collections.Generic.HashSet<Collider>();
        void Separarse()
        {
            foreach (var e in Forja3DEnemy.Activos)
            {
                if (!e) continue;
                foreach (var c in e.GetComponentsInChildren<Collider>())
                    if (ignorados.Add(c)) Physics.IgnoreCollision(cc, c, true);
                if (e.Muerto) continue;
                var d = transform.position - e.transform.position; d.y = 0;
                float minimo = cc.radius * transform.lossyScale.x + e.Radio;
                float dist = d.magnitude;
                if (dist < minimo && dist > 0.001f) cc.Move(d / dist * Mathf.Min(minimo - dist, 6f * Time.deltaTime));
            }
        }

        void Reubicar(Vector3 punto)
        {
            // Busca el suelo real bajo el punto (la malla de navegación puede quedar un poco por debajo del
            // terreno; si se coloca a Dante ahí, atraviesa el suelo y cae fuera del mapa)
            var mascara = ~0;
            if (Physics.Raycast(punto + Vector3.up * 4f, Vector3.down, out var suelo, 12f, mascara, QueryTriggerInteraction.Ignore) && suelo.collider != cc)
                punto = suelo.point;
            cc.enabled = false;
            transform.position = punto + Vector3.up * (cc.skinWidth + 0.15f);
            cc.enabled = true;
            vy = 0f; empuje = Vector3.zero; fueraDesde = -1f;
        }

        void Gastar(float cantidad, bool golpe = true)
        {
            Estamina = Mathf.Max(0f, Estamina - cantidad);
            ultimoGasto = Time.time;
        }

        // Enemigo más cercano delante (para orientar los golpes)
        Forja3DEnemy BuscarObjetivo(float radio, float angulo)
        {
            Forja3DEnemy mejor = null; float mejorD = radio;
            foreach (var e in Forja3DEnemy.Activos)
            {
                if (!e || e.Muerto) continue;
                var d = e.transform.position - transform.position; d.y = 0;
                float dist = d.magnitude;
                if (dist > mejorD) continue;
                if (angulo < 360f && Vector3.Angle(transform.forward, d) > angulo * 0.5f) continue;
                mejor = e; mejorD = dist;
            }
            return mejor;
        }

        // ── Combo de tres golpes ──
        IEnumerator Combo()
        {
            atacando = true;
            do
            {
                quiereOtroGolpe = false;
                pasoCombo = pasoCombo % 3 + 1;
                bool fuerte = pasoCombo == 3;
                float dur = fuerte ? 0.6f : 0.42f, impacto = fuerte ? 0.3f : 0.2f;
                Gastar(costoAtaque);

                // Cada golpe usa un clip distinto si existe (Skill1/Skill2 de la biblioteca de animaciones)
                // Tajo derecho → tajo izquierdo → tajo cargado (biblioteca de combate); si faltan, Skill1/Skill2
                string trig = pasoCombo == 2 ? Primero("Combo2", "Skill1") : pasoCombo == 3 ? Primero("Combo3", "Skill2") : "Attack";
                Forja3DAnim.Trigger(anim, trig);

                var obj = BuscarObjetivo(5f, 140f);
                if (obj)
                {
                    var d = obj.transform.position - transform.position; d.y = 0;
                    transform.rotation = Quaternion.LookRotation(d);
                    if (d.magnitude > alcanceAtaque * 0.8f) empuje = d.normalized * Mathf.Min(7f, (d.magnitude - alcanceAtaque * 0.6f) / impacto); // se lanza hacia el enemigo
                }
                else empuje = transform.forward * 2f;

                yield return new WaitForSeconds(impacto * 0.45f);
                Forja3DAudio.Sonar("espada_aire", transform.position + Vector3.up, fuerte ? 0.9f : 0.7f, 0.1f, fuerte ? 0.85f : 1f);
                yield return new WaitForSeconds(impacto * 0.55f);
                Golpear(fuerte);
                // Ventana para encadenar el siguiente golpe
                for (float t = impacto; t < dur; t += Time.deltaTime) yield return null;
                finCombo = Time.time + 0.5f;
            } while (quiereOtroGolpe && pasoCombo < 3 && !Forja3DGameManager.Bloqueado && !(vida && vida.Muerto) && Time.time >= aturdidoHasta);
            if (pasoCombo >= 3) { pasoCombo = 0; yield return new WaitForSeconds(0.15f); }
            atacando = false;
        }

        string Primero(params string[] triggers)
        {
            foreach (var t in triggers) if (Forja3DAnim.Tiene(anim, t, AnimatorControllerParameterType.Trigger)) return t;
            return "Attack";
        }

        void Golpear(bool fuerte)
        {
            var centro = transform.position + transform.forward * (alcanceAtaque * 0.7f) + Vector3.up * 1f;
            bool acerto = false;
            var golpeados = new System.Collections.Generic.HashSet<Forja3DHealth>();
            foreach (var c in Physics.OverlapSphere(centro, alcanceAtaque * (fuerte ? 1.25f : 1f), ~0, QueryTriggerInteraction.Ignore))
            {
                var h = c.GetComponentInParent<Forja3DHealth>();
                if (!h || h.esJugador || h.Muerto || !golpeados.Add(h)) continue;
                var e = h.GetComponent<Forja3DEnemy>();
                bool gracia = e && e.Aturdido;
                int danio = danioAtaque * (fuerte ? tercerGolpe : 1) * (gracia ? multGracia : 1);
                var dir = h.transform.position - transform.position; dir.y = 0; dir.Normalize();
                if (!h.Danio(danio, transform)) continue;
                acerto = true;
                var punto = h.transform.position + Vector3.up * 1.1f * h.transform.lossyScale.y - dir * 0.3f;
                Forja3DFX.Sangre(punto, dir + transform.right * (pasoCombo == 2 ? -0.8f : 0.8f), gracia ? 40 : fuerte ? 26 : 16);
                Forja3DAudio.Sonar(gracia ? "golpe_gracia" : "golpe_carne", punto, fuerte || gracia ? 1f : 0.85f);
                if (e) e.Empujar(dir * (gracia ? 6f : fuerte ? 4.5f : 1.8f), fuerte || gracia);
                if (gracia) { Forja3DGameManager.Mensaje("¡GOLPE DE GRACIA!"); Forja3DProgreso.Ganar(8, "gracia"); }
                if (robaVida && ++golpesCerteros % 8 == 0 && vida) vida.Curar(1);
            }
            if (acerto)
            {
                Forja3DFX.HitStop(fuerte ? 0.1f : 0.055f);
                Forja3DFX.Temblor(fuerte ? 0.22f : 0.1f, 0.15f);
                Estamina = Mathf.Min(estaminaMaxima, Estamina + 4f); // golpear recompensa
            }
        }

        // ── Voltereta ──
        // Con clip (biblioteca: Roll_Dodge) el modelo rueda de verdad; sin clip, se agacha y se desliza.
        // Recorre unos 4,5 m; invulnerable durante casi toda la voltereta.
        IEnumerator Esquivar(Vector3 dir)
        {
            esquivando = true;
            Gastar(costoEsquiva);
            if (vida) vida.InvulnerableHasta = Time.time + invulnerableEsquiva;
            Forja3DAudio.Sonar("esquiva", transform.position + Vector3.up * 0.5f, 0.8f);
            bool clip = Forja3DAnim.Tiene(anim, "Dodge", AnimatorControllerParameterType.Trigger);
            if (clip) Forja3DAnim.Trigger(anim, "Dodge");
            const float dur = 0.5f;
            var rotIni = transform.rotation; var rotFin = Quaternion.LookRotation(dir);
            var posVis = visual ? visual.localPosition : Vector3.zero;
            var rotVis = visual ? visual.localRotation : Quaternion.identity;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                transform.rotation = Quaternion.Slerp(rotIni, rotFin, Mathf.Clamp01(k * 8f)); // gira rápido pero sin salto brusco
                float v = Mathf.Lerp(13f, 4f, k * k);
                vy += gravedad * Time.deltaTime; if (cc.isGrounded && vy < 0) vy = -2f;
                cc.Move((dir * v + Vector3.up * vy) * Time.deltaTime);
                if (!clip && visualPropio)
                {
                    // Sin clip: agachado e inclinado hacia delante (sin girar el modelo entero)
                    float baja = Mathf.Sin(k * Mathf.PI);
                    visual.localPosition = posVis + Vector3.down * 0.45f * baja;
                    visual.localRotation = rotVis * Quaternion.Euler(35f * baja, 0, 0);
                }
                yield return null;
            }
            // (antes esto devolvía a Dante al punto de partida: "visual" es el propio jugador cuando el Animator está en él)
            if (!clip && visualPropio) { visual.localPosition = posVis; visual.localRotation = rotVis; }
            ultimoSeguro = Vector3.zero; proximoChequeo = 0f;
            esquivando = false;
        }

        // ── Bloqueo y parry (lo llama Forja3DHealth antes de aplicar el daño) ──
        int Interceptar(int danio, Transform fuente, bool imparable)
        {
            if (!Cubriendo || imparable || !fuente) return danio;
            var d = fuente.position - transform.position; d.y = 0;
            if (Vector3.Angle(transform.forward, d) > 110f) return danio; // golpe por la espalda
            var punto = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
            var enemigo = fuente.GetComponentInParent<Forja3DEnemy>();

            if (Time.time - inicioCubrir <= ventanaParry)
            {
                // ¡PARRY!
                if (enemigo) enemigo.Aturdir(1.8f);
                Forja3DAnim.Trigger(anim, "Parry");
                parryHasta = Time.time + 0.6f;
                Forja3DFX.Chispas(punto, 40, true);
                Forja3DFX.HitStop(0.16f, 0.02f);
                Forja3DFX.Temblor(0.3f, 0.2f);
                Forja3DAudio.Sonar("parry", punto, 1f, 0.04f);
                Estamina = Mathf.Min(estaminaMaxima, Estamina + 20f);
                Forja3DGameManager.Mensaje("¡PARRY!");
                Forja3DProgreso.Ganar(5, "parry");
                return 0;
            }

            Gastar(costoBloqueo * danio);
            if (Estamina <= 0f)
            {
                // Guardia rota: aturdido un momento
                aturdidoHasta = Time.time + 0.9f;
                Cubriendo = false;
                Forja3DAudio.Sonar("guardia_rota", punto, 1f);
                Forja3DFX.Chispas(punto, 25);
                Forja3DGameManager.Mensaje("GUARDIA ROTA");
                return Mathf.Max(1, danio / 2);
            }
            empuje = -transform.forward * 3f;
            Forja3DFX.Chispas(punto, 16);
            Forja3DFX.HitStop(0.05f);
            Forja3DAudio.Sonar("bloqueo", punto, 0.9f);
            return 0;
        }

        void AlSerGolpeado()
        {
            var p = transform.position + Vector3.up * 1.2f;
            var desde = vida.UltimoAtacante ? (transform.position - vida.UltimoAtacante.position) : -transform.forward;
            desde.y = 0;
            Forja3DFX.Sangre(p, desde, 22);
            Forja3DFX.Temblor(0.35f, 0.25f);
            Forja3DFX.HitStop(0.08f);
            Forja3DAudio.Sonar("dolor_dante", p, 0.9f);
            Forja3DAudio.Sonar("golpe_carne", p, 0.8f, 0.1f, 0.85f);
            empuje = desde.normalized * 4f;
            Forja3DGameManager.Herido();
        }
    }
}
