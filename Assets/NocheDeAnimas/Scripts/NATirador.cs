// Noche de Ánimas — armas del jugador: disparo con asistencia de puntería (pensado para el móvil),
// munición por tipo, cargador y recarga. Las armas son caseras y se arman con piezas simples.
//   DISPARAR (mantener) · RECARGAR · ARMA (cambia) · ESQUIVAR · CORRER
//   PC: clic izquierdo dispara · R recarga · Tab o 1-6 cambia de arma
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
        float proximoDisparo, apuntarHasta, finRecarga, lentoHasta, baseTrote, baseCorrer;
        bool recargando;
        Vector3 apuntarDir, empuje;
        Coroutine recarga;

        CharacterController cc;
        Forja3DHealth vida;
        Forja3DPlayerController control;
        Transform mano;
        Light fogonazo;

        public ArmaDef Arma => armas.Count > 0 ? armas[actual] : null;
        public int Cargado => Arma != null && enCargador.TryGetValue(Arma.id, out var n) ? n : 0;
        public int Reserva => Arma != null ? reserva[(int)Arma.municion] : 0;
        public bool Recargando => recargando;
        public float ProgresoRecarga => recargando && Arma != null ? 1f - Mathf.Clamp01((finRecarga - Time.time) / Arma.recarga) : 0f;
        public bool Infinita => Arma != null && Arma.municion == Municion.Canicas;

        void Awake()
        {
            Instancia = this;
            cc = GetComponent<CharacterController>();
            vida = GetComponent<Forja3DHealth>();
            control = GetComponent<Forja3DPlayerController>();
            if (control) { baseTrote = control.velocidadTrote; baseCorrer = control.velocidadCorrer; }
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        public void Configurar(NivelConfig cfg)
        {
            armas.Clear();
            for (int i = 0; i < Mathf.Min(cfg.armasDisponibles, NADatos.Armas.Length); i++) armas.Add(NADatos.Armas[i]);
            for (int i = 0; i < reserva.Length; i++) reserva[i] = i < cfg.reservaInicial.Length ? cfg.reservaInicial[i] : 0;
            foreach (var a in armas)
            {
                int r = reserva[(int)a.municion];
                int carga = a.municion == Municion.Canicas ? a.cargador : Mathf.Min(a.cargador, r);
                enCargador[a.id] = carga;
                if (a.municion != Municion.Canicas) reserva[(int)a.municion] -= carga;
            }
            mano = new GameObject("ManoArma").transform;
            mano.SetParent(transform, false);
            mano.localPosition = new Vector3(0.34f, 1.18f, 0.32f);
            foreach (var a in armas) { var m = NAModelosArma.Crear(a.id, mano); m.SetActive(false); modelos.Add(m); }
            // Empieza con la mejor arma que tenga munición
            actual = 0;
            for (int i = armas.Count - 1; i >= 0; i--) if (enCargador[armas[i].id] > 0 && armas[i].municion != Municion.Canicas) { actual = i; break; }
            MostrarModelo();
            Sombrero();

            var fl = new GameObject("Fogonazo"); fl.transform.SetParent(mano, false); fl.transform.localPosition = new Vector3(0, 0.05f, 0.7f);
            fogonazo = fl.AddComponent<Light>(); fogonazo.type = LightType.Point; fogonazo.range = 7f; fogonazo.color = new Color(1f, 0.75f, 0.35f); fogonazo.intensity = 0f;
        }

        // Identidad del protagonista sin generar modelos: sombrero de palma
        void Sombrero()
        {
            var ala = NAMat.Pieza(PrimitiveType.Cylinder, transform, new Vector3(0, 1.98f, 0), new Vector3(0.95f, 0.02f, 0.95f), NAMat.Hex("#c9a45c"));
            NAMat.Pieza(PrimitiveType.Cylinder, transform, new Vector3(0, 2.1f, 0), new Vector3(0.45f, 0.12f, 0.45f), NAMat.Hex("#b8914a"));
            NAMat.Pieza(PrimitiveType.Cylinder, transform, new Vector3(0, 2.03f, 0), new Vector3(0.47f, 0.025f, 0.47f), NAMat.Hex("#7a2a1a"));
            ala.name = "Sombrero";
        }

        void MostrarModelo()
        {
            for (int i = 0; i < modelos.Count; i++) modelos[i].SetActive(i == actual);
        }

        void Update()
        {
            // Empujones (golpes de área, embestidas)
            if (empuje.sqrMagnitude > 0.01f && cc && cc.enabled)
            {
                cc.Move(empuje * Time.deltaTime);
                empuje = Vector3.MoveTowards(empuje, Vector3.zero, 22f * Time.deltaTime);
            }
            if (control && Time.time > lentoHasta && control.velocidadTrote < baseTrote) { control.velocidadTrote = baseTrote; control.velocidadCorrer = baseCorrer; }

            if (armas.Count == 0 || Forja3DGameManager.Bloqueado || (vida && vida.Muerto)) return;

            int directa = Forja3DInput.ArmaDirecta;
            if (directa >= 0 && directa < armas.Count && directa != actual) Cambiar(directa);
            else if (Forja3DInput.CambiarArma) Cambiar((actual + 1) % armas.Count);

            if (Forja3DInput.Recargar) EmpezarRecarga();
            if (Forja3DInput.Disparar && Time.time >= proximoDisparo) IntentarDisparo();
        }

        void LateUpdate()
        {
            // Mientras dispara, mira hacia el objetivo (después del movimiento del controlador)
            if (Time.time < apuntarHasta && apuntarDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(apuntarDir), 25f * Time.deltaTime);
        }

        void Cambiar(int i)
        {
            if (recarga != null) { StopCoroutine(recarga); recarga = null; }
            recargando = false;
            actual = i;
            MostrarModelo();
            proximoDisparo = Time.time + 0.25f;
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

            var boca = mano.TransformPoint(new Vector3(0, 0.05f, 0.75f));
            var origen = transform.position + Vector3.up * 1.25f;
            var objetivo = BuscarObjetivo(a.alcance);
            Vector3 dir;
            if (objetivo) dir = (objetivo.Pecho - origen).normalized;
            else
            {
                var cam = Camera.main ? Camera.main.transform.forward : transform.forward;
                dir = Vector3.ProjectOnPlane(cam, Vector3.up).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            }
            apuntarDir = new Vector3(dir.x, 0, dir.z); apuntarHasta = Time.time + 0.45f;
            transform.rotation = Quaternion.LookRotation(apuntarDir); // gira al instante: el tiro sale hacia donde mira

            NASonido.Sonar(a.sonido, boca, 0.9f);
            Forja3DAudio.EnCombate();
            StartCoroutine(Fogonazo(a));
            Forja3DFX.Temblor(a.retroceso * 0.35f, 0.08f);

            if (a.proyectil) NAProyectil.Disparar(a, boca, Dispersar(dir, a.dispersion), transform);
            else for (int i = 0; i < a.perdigones; i++) Rayo(a, origen, boca, Dispersar(dir, a.dispersion));

            if (Cargado <= 0 && (Reserva > 0 || Infinita)) EmpezarRecarga();
        }

        static Vector3 Dispersar(Vector3 dir, float grados)
        {
            var q = Quaternion.Euler(Random.Range(-grados, grados) * 0.5f, Random.Range(-grados, grados), 0);
            return (Quaternion.LookRotation(dir) * q * Vector3.forward).normalized;
        }

        // Disparo instantáneo: impacta al primer zombie (o atraviesa varios) y se detiene en el escenario
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
                    Impactar(z, a.danio, dir, h.point);
                    if (++atravesados > a.atraviesa) { fin = h.point; break; }
                    continue;
                }
                fin = h.point;
                Forja3DFX.Chispas(h.point + h.normal * 0.05f, 5);
                break;
            }
            NATrazo.Dibujar(boca, fin, a.id == "resortera" ? new Color(0.8f, 0.8f, 0.85f) : new Color(1f, 0.85f, 0.45f));
        }

        public static void Impactar(NAZombie z, int danio, Vector3 dir, Vector3 punto)
        {
            var h = z.GetComponent<Forja3DHealth>();
            if (!h || h.Muerto) return;
            if (!h.Danio(danio, NAJugador.Transform)) return;
            Forja3DFX.Sangre(punto, dir, 10 + danio * 3);
            z.Empujar(dir, 1.5f + danio);
        }

        // Asistencia de puntería: el zombie visible más alineado con la cámara (o con el jugador)
        NAZombie BuscarObjetivo(float alcance)
        {
            var cam = Camera.main ? Camera.main.transform.forward : transform.forward;
            var refs = new[] { Vector3.ProjectOnPlane(cam, Vector3.up).normalized, transform.forward };
            var ojo = transform.position + Vector3.up * 1.25f;
            NAZombie mejor = null; float mejorPuntos = float.MaxValue;
            foreach (var z in NAZombie.Vivos)
            {
                if (!z || z.Muerto) continue;
                var v = z.Pecho - ojo; float d = v.magnitude;
                if (d > alcance) continue;
                var plano = new Vector3(v.x, 0, v.z);
                float ang = Mathf.Min(Vector3.Angle(refs[0], plano), Vector3.Angle(refs[1], plano));
                if (ang > 65f && d > 3f) continue;
                if (!LineaLibre(ojo, z)) continue;
                float puntos = ang * 0.06f + d * 0.05f;
                if (puntos < mejorPuntos) { mejorPuntos = puntos; mejor = z; }
            }
            return mejor;
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
            reserva[(int)m] += cantidad;
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
                    NATirador.Impactar(z, arma.danio, dir, h.point);
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
