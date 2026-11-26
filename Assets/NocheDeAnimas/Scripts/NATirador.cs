// Noche de Ánimas — armas del jugador en primera persona: se dispara hacia la mira (con una pequeña
// ayuda de puntería para el móvil), tiros a la cabeza al doble, munición por tipo, cargador y recarga.
// Se empieza con la resortera; las demás armas caseras se arman con piezas (NAArmado).
//   DISPARAR (mantener) · RECARGAR · ARMA (cambia) · ESQUIVAR · CORRER · ACCIÓN
//   PC: ratón mira · clic dispara · R recarga · Tab o 1-6 cambia de arma · F acción
using System.Collections;
using System.Collections.Generic;
using Forja3D;
using UnityEngine;

namespace NocheDeAnimas
{
    // Acceso al jugador desde zombies y efectos
    public static class NAJugador
    {
        public static Transform Transform => NATirador.Instancia ? NATirador.Instancia.transform : null;
        public static void Empujar(Vector3 dir, float fuerza) { if (NATirador.Instancia) NATirador.Instancia.Empujar(dir, fuerza); }
        public static void Ralentizar(float segundos) { if (NATirador.Instancia) NATirador.Instancia.Ralentizar(segundos); }
    }

    public class NATirador : MonoBehaviour
    {
        public static NATirador Instancia { get; private set; }

        readonly List<ArmaDef> armas = new List<ArmaDef>();
        readonly Dictionary<string, int> enCargador = new Dictionary<string, int>();
        readonly int[] reserva = new int[6];
        readonly List<GameObject> modelos = new List<GameObject>();
        int actual;
        float proximoDisparo, finRecarga, lentoHasta, baseTrote, baseCorrer, patada, balanceo, cambioHasta;
        bool recargando;
        Vector3 empuje;
        Coroutine recarga;

        CharacterController cc;
        Forja3DHealth vida;
        Forja3DPlayerController control;
        Transform vista, mano;
        Light fogonazo;

        public ArmaDef Arma => armas.Count > 0 ? armas[actual] : null;
        public int Cargado => Arma != null && enCargador.TryGetValue(Arma.id, out var n) ? n : 0;
        public int Reserva => Arma != null ? reserva[(int)Arma.municion] : 0;
        public bool Recargando => recargando;
        public float ProgresoRecarga => recargando && Arma != null ? 1f - Mathf.Clamp01((finRecarga - Time.time) / Arma.recarga) : 0f;
        public bool Infinita => Arma != null && Arma.municion == Municion.Canicas;
        public bool Tiene(string id) { foreach (var a in armas) if (a.id == id) return true; return false; }
        public bool TieneArmasConMunicion { get { foreach (var a in armas) if (a.municion != Municion.Canicas) return true; return false; } }

        void Awake()
        {
            Instancia = this;
            cc = GetComponent<CharacterController>();
            vida = GetComponent<Forja3DHealth>();
            control = GetComponent<Forja3DPlayerController>();
            if (control) { baseTrote = control.velocidadTrote; baseCorrer = control.velocidadCorrer; }
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; if (vista) Destroy(vista.gameObject); }

        // Empieza con la resortera y con las armas que ya armó en mapas anteriores
        public void Configurar(NivelConfig cfg)
        {
            var cam = Camera.main ? Camera.main.transform : transform;
            vista = new GameObject("VistaArma").transform;
            vista.SetParent(cam, false);
            mano = new GameObject("Mano").transform;
            mano.SetParent(vista, false);
            var fl = new GameObject("Fogonazo"); fl.transform.SetParent(mano, false); fl.transform.localPosition = new Vector3(0, 0.05f, 0.75f);
            fogonazo = fl.AddComponent<Light>(); fogonazo.type = LightType.Point; fogonazo.range = 7f; fogonazo.color = new Color(1f, 0.75f, 0.35f); fogonazo.intensity = 0f;

            AgregarArma("resortera", false);
            foreach (var id in NAArmado.Construidas()) AgregarArma(id, false);
            actual = armas.Count - 1; // la mejor que tenga
            MostrarModelo();
        }

        public void AgregarArma(string id, bool equipar)
        {
            if (Tiene(id)) { if (equipar) LlenarTodo(); return; }
            var a = NADatos.Buscar(id);
            if (a == null) return;
            armas.Add(a);
            enCargador[a.id] = a.cargador;
            if (a.municion != Municion.Canicas) reserva[(int)a.municion] = Mathf.Max(reserva[(int)a.municion], a.cargador * 3);
            var m = NAModelosArma.Crear(a.id, mano);
            m.SetActive(false);
            modelos.Add(m);
            if (equipar) Cambiar(armas.Count - 1);
        }

        // Munición máxima: cargadores llenos y reserva al tope
        public void LlenarTodo()
        {
            foreach (var a in armas)
            {
                enCargador[a.id] = a.cargador;
                if (a.municion != Municion.Canicas) reserva[(int)a.municion] = Mathf.Max(reserva[(int)a.municion], Maximo(a));
            }
        }

        static int Maximo(ArmaDef a) => a.cargador * 5 + NADatos.Caja(a.municion) * 2;

        void MostrarModelo()
        {
            for (int i = 0; i < modelos.Count; i++) modelos[i].SetActive(i == actual);
        }

        void Update()
        {
            if (empuje.sqrMagnitude > 0.01f && cc && cc.enabled)
            {
                cc.Move(empuje * Time.deltaTime);
                empuje = Vector3.MoveTowards(empuje, Vector3.zero, 22f * Time.deltaTime);
            }
            if (control && Time.time > lentoHasta && control.velocidadTrote < baseTrote) { control.velocidadTrote = baseTrote; control.velocidadCorrer = baseCorrer; }

            if (armas.Count == 0 || Forja3DGameManager.Bloqueado || (vida && vida.Muerto)) return;

            int directa = Forja3DInput.ArmaDirecta;
            if (directa >= 0 && directa < armas.Count && directa != actual) Cambiar(directa);
            else if (Forja3DInput.CambiarArma && armas.Count > 1) Cambiar((actual + 1) % armas.Count);

            if (Forja3DInput.Recargar) EmpezarRecarga();
            bool gatillo = Forja3DInput.Disparar && !Forja3DInput.Accion;
            if (!Forja3DInput.EsTactil && Cursor.lockState != CursorLockMode.Locked) gatillo = false; // el primer clic captura el ratón
            if (gatillo && Time.time >= proximoDisparo) IntentarDisparo();
        }

        // Arma en la mano: balanceo al caminar, patada al disparar y se baja al recargar o cambiar
        void LateUpdate()
        {
            if (!vista) return;
            float vel = NACamaraFP.Inst ? NACamaraFP.Inst.Velocidad : 0f;
            balanceo += Time.deltaTime * (vel > 0.5f ? 9f : 1.5f);
            float amp = vel > 0.5f ? 0.012f : 0.003f;
            patada = Mathf.MoveTowards(patada, 0f, Time.deltaTime * 1.2f);
            float baja = recargando ? Mathf.Sin(ProgresoRecarga * Mathf.PI) * 0.18f : 0f;
            if (Time.time < cambioHasta) baja += (cambioHasta - Time.time) / 0.3f * 0.25f;
            vista.localPosition = new Vector3(0.2f + Mathf.Cos(balanceo) * amp, -0.22f + Mathf.Abs(Mathf.Sin(balanceo)) * amp - baja, 0.38f - patada);
            vista.localRotation = Quaternion.Euler(-patada * 60f + baja * 120f, 0, recargando ? baja * 80f : 0f);
        }

        void Cambiar(int i)
        {
            if (recarga != null) { StopCoroutine(recarga); recarga = null; }
            recargando = false;
            actual = i;
            MostrarModelo();
            proximoDisparo = Time.time + 0.3f;
            cambioHasta = Time.time + 0.3f;
            NASonido.Sonar("recarga", transform.position + Vector3.up, 0.4f, 1.4f);
            Forja3DGameManager.Mensaje(Arma.nombre);
        }

        void IntentarDisparo()
        {
            if (recargando) return;
            var a = Arma;
            if (Cargado <= 0)
            {
                if (Reserva > 0 || Infinita) EmpezarRecarga();
                else
                {
                    proximoDisparo = Time.time + 0.4f;
                    NASonido.Sonar("vacio", transform.position + Vector3.up, 0.7f);
                    Forja3DGameManager.Mensaje("SIN " + NADatos.NombreMunicion(a.municion).ToUpperInvariant());
                }
                return;
            }
            proximoDisparo = Time.time + a.cadencia;
            enCargador[a.id] = Cargado - 1;

            var cam = Camera.main ? Camera.main.transform : transform;
            var origen = cam.position;
            var dir = Asistir(origen, cam.forward, a.alcance);
            var boca = mano.TransformPoint(new Vector3(0, 0.05f, 0.6f));

            NASonido.Sonar(a.sonido, boca, 0.9f);
            Forja3DAudio.EnCombate();
            StartCoroutine(Fogonazo(a));
            patada = Mathf.Clamp(0.03f + a.retroceso * 0.25f, 0f, 0.14f);
            if (NACamaraFP.Inst) NACamaraFP.Inst.Retroceso(a.retroceso);

            if (a.proyectil) NAProyectil.Disparar(a, origen + dir * 0.4f, Dispersar(dir, a.dispersion * 0.5f), transform);
            else for (int i = 0; i < a.perdigones; i++) Rayo(a, origen, boca, Dispersar(dir, a.dispersion * 0.6f));

            if (Cargado <= 0 && (Reserva > 0 || Infinita)) EmpezarRecarga();
        }

        // Ayuda de puntería para el móvil: si la mira está casi encima de un zombie, el tiro va a él.
        // Si ya apunta a un zombie (cabeza incluida), no toca nada.
        Vector3 Asistir(Vector3 origen, Vector3 dir, float alcance)
        {
            if (Physics.Raycast(origen, dir, out var h, alcance, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<NAZombie>()) return dir;
            NAZombie mejor = null; float mejorAng = 7f;
            foreach (var z in NAZombie.Vivos)
            {
                if (!z || z.Muerto) continue;
                var v = z.Pecho - origen;
                if (v.magnitude > alcance) continue;
                float ang = Vector3.Angle(dir, v);
                if (ang < mejorAng && LineaLibre(origen, z)) { mejorAng = ang; mejor = z; }
            }
            return mejor ? (mejor.Pecho - origen).normalized : dir;
        }

        static Vector3 Dispersar(Vector3 dir, float grados)
        {
            var q = Quaternion.Euler(Random.Range(-grados, grados) * 0.5f, Random.Range(-grados, grados) * 0.5f, 0);
            return (Quaternion.LookRotation(dir) * q * Vector3.forward).normalized;
        }

        void Rayo(ArmaDef a, Vector3 origen, Vector3 boca, Vector3 dir)
        {
            var hits = Physics.RaycastAll(origen, dir, a.alcance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 fin = origen + dir * a.alcance;
            int atravesados = 0;
            var golpeados = new HashSet<NAZombie>();
            foreach (var h in hits)
            {
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                var z = h.collider.GetComponentInParent<NAZombie>();
                if (z)
                {
                    if (z.Muerto || !golpeados.Add(z)) continue;
                    Impactar(z, a.danio, dir, h.point, z.EsCabeza(h.point));
                    if (++atravesados > a.atraviesa) { fin = h.point; break; }
                    continue;
                }
                fin = h.point;
                Forja3DFX.Chispas(h.point + h.normal * 0.05f, 5);
                break;
            }
            NATrazo.Dibujar(boca, fin, a.id == "resortera" ? new Color(0.8f, 0.8f, 0.85f) : new Color(1f, 0.85f, 0.45f));
        }

        // Daño a un zombie: cabeza ×2, INSTA-KILL mata de un golpe (menos al jefe); da puntos
        public static void Impactar(NAZombie z, int danio, Vector3 dir, Vector3 punto, bool cabeza = false)
        {
            var h = z.GetComponent<Forja3DHealth>();
            if (!h || h.Muerto) return;
            int d = cabeza ? danio * 2 : danio;
            if (NAPoderes.InstaKill && !z.esJefe) d = 99999;
            if (!h.Danio(d, NAJugador.Transform)) return;
            Forja3DFX.Sangre(punto, dir, 10 + Mathf.Min(d, 10) * 3);
            z.Empujar(dir, 1.5f + Mathf.Min(d, 8));
            if (h.Muerto)
            {
                NAPuntos.Muerte(cabeza);
                if (cabeza) { NASonido.Sonar("cabeza", punto, 0.9f); Forja3DFX.Sangre(punto, Vector3.up, 30); }
            }
            else NAPuntos.Acierto();
        }

        bool LineaLibre(Vector3 ojo, NAZombie z)
        {
            var hacia = z.Pecho;
            foreach (var h in Physics.RaycastAll(ojo, (hacia - ojo).normalized, Vector3.Distance(ojo, hacia), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<NAZombie>()) continue;
                return false;
            }
            return true;
        }

        IEnumerator Fogonazo(ArmaDef a)
        {
            if (!fogonazo) yield break;
            fogonazo.intensity = a.id == "resortera" || a.id == "ballesta" ? 0f : a.perdigones > 1 || a.explosiva ? 6f : 3.5f;
            if (fogonazo.intensity > 0f) Forja3DFX.Chispas(fogonazo.transform.position, a.perdigones > 1 ? 10 : 4);
            yield return new WaitForSeconds(0.05f);
            fogonazo.intensity = 0f;
        }

        void EmpezarRecarga()
        {
            var a = Arma;
            if (a == null || recargando || Cargado >= a.cargador) return;
            if (!Infinita && Reserva <= 0) return;
            recarga = StartCoroutine(Recargar(a));
        }

        IEnumerator Recargar(ArmaDef a)
        {
            recargando = true;
            finRecarga = Time.time + a.recarga;
            NASonido.Sonar("recarga", transform.position + Vector3.up, 0.7f);
            yield return new WaitForSeconds(a.recarga);
            int falta = a.cargador - Cargado;
            int toma = Infinita ? falta : Mathf.Min(falta, Reserva);
            enCargador[a.id] = Cargado + toma;
            if (!Infinita) reserva[(int)a.municion] -= toma;
            NASonido.Sonar("recarga", transform.position + Vector3.up, 0.5f, 1.3f);
            recargando = false;
            recarga = null;
        }

        // ── Suministros ──
        public bool TieneMunicion(Municion m) { foreach (var a in armas) if (a.municion == m) return true; return false; }
        public List<Municion> MunicionesUsadas()
        {
            var l = new List<Municion>();
            foreach (var a in armas) if (a.municion != Municion.Canicas && !l.Contains(a.municion)) l.Add(a.municion);
            return l;
        }
        public void DarMunicion(Municion m, int cantidad)
        {
            if (m == Municion.Canicas) return;
            int tope = 0;
            foreach (var a in armas) if (a.municion == m) tope = Mathf.Max(tope, Maximo(a));
            reserva[(int)m] = Mathf.Min(reserva[(int)m] + cantidad, Mathf.Max(tope, reserva[(int)m]));
        }

        public void Empujar(Vector3 dir, float fuerza) { dir.y = 0; if (dir.sqrMagnitude > 0.001f) empuje = dir.normalized * fuerza; }
        public void Ralentizar(float segundos)
        {
            if (!control) return;
            lentoHasta = Time.time + segundos;
            control.velocidadTrote = baseTrote * 0.5f; control.velocidadCorrer = baseCorrer * 0.5f;
            Forja3DGameManager.Mensaje("¡EL GRITO TE PARALIZA!");
        }
    }

    // ───────── Trazo de bala (línea breve) ─────────
    public class NATrazo : MonoBehaviour
    {
        public static void Dibujar(Vector3 a, Vector3 b, Color color)
        {
            var g = new GameObject("Trazo");
            var lr = g.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = 0.05f; lr.endWidth = 0.015f;
            lr.sharedMaterial = NAMat.De(color, 3f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(g, 0.05f);
        }
    }

    // ───────── Varillas de ballesta y cohetes ─────────
    public class NAProyectil : MonoBehaviour
    {
        ArmaDef arma; Vector3 dir; Transform autor; float recorrido; int atravesados;
        readonly HashSet<NAZombie> golpeados = new HashSet<NAZombie>();

        public static void Disparar(ArmaDef a, Vector3 desde, Vector3 dir, Transform autor)
        {
            var g = new GameObject(a.explosiva ? "Cohete" : "Varilla");
            g.transform.position = desde; g.transform.rotation = Quaternion.LookRotation(dir);
            if (a.explosiva)
            {
                NAMat.Pieza(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.12f, 0.25f, 0.12f), NAMat.Hex("#d62828"), new Vector3(90, 0, 0));
                NAMat.Pieza(PrimitiveType.Cylinder, g.transform, new Vector3(0, 0, 0.1f), new Vector3(0.125f, 0.06f, 0.125f), NAMat.Hex("#f4c430"), new Vector3(90, 0, 0));
                NAMat.Pieza(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, 0.28f), Vector3.one * 0.12f, NAMat.Hex("#2b59c3"));
            }
            else NAMat.Pieza(PrimitiveType.Cube, g.transform, Vector3.zero, new Vector3(0.04f, 0.04f, 0.75f), NAMat.Hex("#8a4b2a"));
            var p = g.AddComponent<NAProyectil>();
            p.arma = a; p.dir = dir; p.autor = autor;
        }

        void Update()
        {
            float paso = arma.velProyectil * Time.deltaTime;
            if (arma.explosiva) Forja3DFX.Chispas(transform.position - dir * 0.3f, 2);
            foreach (var h in Ordenar(Physics.RaycastAll(transform.position, dir, paso, ~0, QueryTriggerInteraction.Ignore)))
            {
                if (autor && (h.transform == autor || h.transform.IsChildOf(autor))) continue;
                var z = h.collider.GetComponentInParent<NAZombie>();
                if (z && (z.Muerto || golpeados.Contains(z))) continue;
                if (arma.explosiva) { Explotar(h.point); return; }
                if (z)
                {
                    golpeados.Add(z);
                    NATirador.Impactar(z, arma.danio, dir, h.point, z.EsCabeza(h.point));
                    if (++atravesados > arma.atraviesa) { Destroy(gameObject); return; }
                    continue;
                }
                // Clavada en el escenario
                Forja3DFX.Chispas(h.point, 6);
                NASonido.Sonar("impacto", h.point, 0.5f, 0.7f);
                transform.position = h.point - dir * 0.3f;
                enabled = false;
                Destroy(gameObject, 6f);
                return;
            }
            transform.position += dir * paso;
            recorrido += paso;
            if (recorrido > arma.alcance) { if (arma.explosiva) Explotar(transform.position); else Destroy(gameObject); }
        }

        static RaycastHit[] Ordenar(RaycastHit[] h) { System.Array.Sort(h, (x, y) => x.distance.CompareTo(y.distance)); return h; }

        void Explotar(Vector3 punto)
        {
            NAExplosion.En(punto, arma.radioExplosion, 1, arma.danio, autor);
            Destroy(gameObject);
        }
    }

    // ───────── Explosiones (cohetes del jugador y del Cuetero) ─────────
    public static class NAExplosion
    {
        public static void En(Vector3 centro, float radio, int danioJugador, int danioZombies, Transform autor)
        {
            NASonido.Sonar("explosion", centro, 1f);
            Forja3DFX.Polvo(centro, radio * 0.8f);
            Forja3DFX.Chispas(centro, 40, true);
            Forja3DFX.Temblor(0.5f, 0.45f);
            var luz = new GameObject("Explosion").AddComponent<Light>();
            luz.transform.position = centro + Vector3.up * 0.5f;
            luz.type = LightType.Point; luz.range = radio * 3f; luz.intensity = 8f; luz.color = new Color(1f, 0.55f, 0.2f);
            Object.Destroy(luz.gameObject, 0.18f);

            foreach (var z in new List<NAZombie>(NAZombie.Vivos))
            {
                if (!z || z.Muerto || z.transform == autor) continue;
                var d = z.transform.position - centro; d.y = 0;
                if (d.magnitude <= radio) NATirador.Impactar(z, danioZombies, d.normalized, z.Pecho);
            }
            var j = NAJugador.Transform;
            if (j && danioJugador > 0)
            {
                var d = j.position - centro; d.y = 0;
                if (d.magnitude <= radio * 0.75f)
                {
                    var h = j.GetComponent<Forja3DHealth>();
                    if (h) h.Danio(danioJugador, autor, true);
                    NAJugador.Empujar(d, 8f);
                }
            }
        }
    }

    // ───────── Suministros: cajas de munición y pan de muerto ─────────
    public class NASuministro : MonoBehaviour
    {
        bool esPan; Municion municion; float muere, fase;

        public static void TalVezSoltar(Vector3 pos, float probabilidad)
        {
            if (Random.value > probabilidad || !NATirador.Instancia) return;
            var g = new GameObject("Suministro");
            g.transform.position = pos + Vector3.up * 0.4f;
            var s = g.AddComponent<NASuministro>();
            var usadas = NATirador.Instancia.MunicionesUsadas();
            s.esPan = usadas.Count == 0 || Random.value < 0.22f;
            if (s.esPan)
            {
                NAMat.Pieza(PrimitiveType.Sphere, g.transform, Vector3.zero, new Vector3(0.5f, 0.3f, 0.5f), NAMat.Hex("#c98a3e"));
                NAMat.Pieza(PrimitiveType.Cube, g.transform, new Vector3(0, 0.14f, 0), new Vector3(0.45f, 0.06f, 0.08f), NAMat.Hex("#a8692a"));
                NAMat.Pieza(PrimitiveType.Cube, g.transform, new Vector3(0, 0.14f, 0), new Vector3(0.08f, 0.06f, 0.45f), NAMat.Hex("#a8692a"));
            }
            else
            {
                s.municion = usadas[Random.Range(0, usadas.Count)];
                NAMat.Pieza(PrimitiveType.Cube, g.transform, Vector3.zero, new Vector3(0.45f, 0.3f, 0.32f), NAMat.Hex("#5a6b3a"));
                NAMat.Pieza(PrimitiveType.Cube, g.transform, new Vector3(0, 0.16f, 0), new Vector3(0.47f, 0.04f, 0.1f), NAMat.Hex("#f4c430"), default, 1.2f);
            }
            s.muere = Time.time + 30f;
        }

        void Update()
        {
            fase += Time.deltaTime;
            transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
            if (Time.time > muere) { Destroy(gameObject); return; }
            var j = NAJugador.Transform;
            if (!j || Vector3.Distance(j.position + Vector3.up * 0.5f, transform.position) > 1.7f) return;
            if (esPan)
            {
                var h = j.GetComponent<Forja3DHealth>();
                if (h) h.Curar(2);
                Forja3DGameManager.Mensaje("PAN DE MUERTO  +2 VIDA");
            }
            else
            {
                int n = NADatos.Caja(municion);
                NATirador.Instancia.DarMunicion(municion, n);
                Forja3DGameManager.Mensaje($"+{n} {NADatos.NombreMunicion(municion).ToUpperInvariant()}");
            }
            NASonido.Sonar("recoger", transform.position, 0.8f);
            Destroy(gameObject);
        }
    }

    // Los suministros del mapa (las "monedas" del nivel) dan munición de todo y un poco de vida
    public class NASuministroMapa : MonoBehaviour
    {
        void OnTriggerEnter(Collider c)
        {
            if (!c.CompareTag("Player") || !NATirador.Instancia) return;
            foreach (var m in NATirador.Instancia.MunicionesUsadas()) NATirador.Instancia.DarMunicion(m, Mathf.Max(1, NADatos.Caja(m) / 2));
            var h = c.GetComponentInParent<Forja3DHealth>();
            if (h) h.Curar(1);
            NASonido.Sonar("recoger", transform.position, 0.8f);
            Forja3DGameManager.Mensaje("SUMINISTROS: MUNICIÓN Y +1 VIDA");
        }
    }
}
