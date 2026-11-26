// Noche de Ánimas — arranque de cada nivel: convierte la escena de la plantilla en el shooter de oleadas.
//   · Los enemigos colocados en el mapa (E, K, J) pasan a ser zombies de Noche de Ánimas.
//   · El pueblo se levanta sobre el mapa (casas de adobe, puestos, tumbas, faroles, iglesia).
//   · Cámara en primera persona; el jugador empieza con la resortera y las armas que ya armó.
//   · Oleadas infinitas, mesa de armado, misión por pasos para conseguir la llave, puntos y poderes.
// Se ejecuta solo al cargar cualquier escena que tenga un nivel (Forja3DGameManager).
using Forja3D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NocheDeAnimas
{
    public static class NAArranque
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Registrar()
        {
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
        }

        // sceneLoaded llega después de los Awake de la escena y antes de sus Start
        static void AlCargar(Scene escena, LoadSceneMode modo)
        {
            var gm = Object.FindFirstObjectByType<Forja3DGameManager>();
            var jugador = GameObject.FindWithTag("Player");
            if (!gm || !jugador) return;
            try
            {
                var cfg = NAConfig.Para(escena.name);
                NAMapa.Cargar(escena.name);
                if (escena.name.StartsWith("01")) NAArmado.Olvidar(); // partida nueva: se empieza solo con la resortera
                NAPuntos.Reiniciar();
                NAPoderes.Reiniciar();
                try { NAEscenografia.Construir(escena.name, cfg.noche); }
                catch (System.Exception ex) { Debug.LogError("[Forja3D] Noche de Ánimas: escenografía: " + ex); }

                var vida = jugador.GetComponent<Forja3DHealth>();
                if (vida) { vida.vidaMaxima = cfg.vidaJugador; vida.Curar(9999); }

                // Enemigos del mapa → zombies (antes de su Start: no se registran como enemigos de la plantilla)
                foreach (var e in Object.FindObjectsByType<Forja3DEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var pos = e.transform.position;
                    bool jefe = e.esJefe || e.name == "Jefe";
                    float escala = e.transform.localScale.x;
                    e.gameObject.SetActive(false);
                    Object.Destroy(e.gameObject);
                    var tipo = jefe ? TipoZombie.Sacristan : escala >= 1.4f ? TipoZombie.Compadre : cfg.tipoInicial;
                    NAZombie.Crear(tipo, pos, false);
                }

                // Suministros del mapa (las "monedas")
                foreach (var c in Object.FindObjectsByType<Forja3DCollectible>(FindObjectsSortMode.None))
                    if (!c.GetComponent<NASuministroMapa>()) c.gameObject.AddComponent<NASuministroMapa>();

                // Primera persona
                var cam = Camera.main;
                if (cam)
                {
                    var fp = cam.gameObject.AddComponent<NACamaraFP>();
                    fp.Iniciar(jugador.transform, cfg.noche);
                }

                var tirador = jugador.AddComponent<NATirador>();
                tirador.Configurar(cfg);
                var oleadas = gm.gameObject.AddComponent<NAOleadas>();
                oleadas.cfg = cfg;
                gm.gameObject.AddComponent<NAArmado>().Iniciar(cfg);
                gm.gameObject.AddComponent<NAMision>().Iniciar(NAMisiones.Para(escena.name));
                gm.gameObject.AddComponent<NAHUD>();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Forja3D] Noche de Ánimas: error al preparar el nivel: " + ex);
            }
        }
    }


    // HUD propio: mira, arma y munición, puntos, poderes activos, oleada, objetivo, piezas y avisos de ACCIÓN
    public class NAHUD : MonoBehaviour
    {
        Text textoArma, textoMunicion, textoOleada, textoRecarga, textoPuntos, textoPoderes, textoObjetivo, textoPiezas, textoAccion, mira;

        void Start()
        {
            var canvas = GameObject.Find("HUD");
            if (!canvas) return;
            var t = canvas.transform;

            textoArma = Forja3DGameManager.Texto(t, "", 34, TextAnchor.UpperRight);
            textoArma.rectTransform.offsetMax = new Vector2(-56, -40);
            textoArma.color = new Color(1f, 0.86f, 0.55f);

            textoMunicion = Forja3DGameManager.Texto(t, "", 72, TextAnchor.UpperRight);
            textoMunicion.rectTransform.offsetMax = new Vector2(-56, -80);
            textoMunicion.supportRichText = true;

            textoPuntos = Forja3DGameManager.Texto(t, "", 46, TextAnchor.UpperLeft);
            textoPuntos.rectTransform.offsetMin = new Vector2(56, 0);
            textoPuntos.rectTransform.offsetMax = new Vector2(0, -118);
            textoPuntos.color = new Color(1f, 0.82f, 0.25f);

            textoPoderes = Forja3DGameManager.Texto(t, "", 34, TextAnchor.UpperLeft);
            textoPoderes.rectTransform.offsetMin = new Vector2(56, 0);
            textoPoderes.rectTransform.offsetMax = new Vector2(0, -172);
            textoPoderes.supportRichText = true;

            textoOleada = Forja3DGameManager.Texto(t, "", 44, TextAnchor.UpperCenter);
            textoOleada.rectTransform.offsetMax = new Vector2(0, -30);
            textoOleada.color = new Color(0.85f, 0.12f, 0.08f);

            textoObjetivo = Forja3DGameManager.Texto(t, "", 32, TextAnchor.UpperCenter);
            textoObjetivo.rectTransform.offsetMax = new Vector2(0, -84);
            textoObjetivo.color = new Color(1f, 0.93f, 0.78f);

            textoPiezas = Forja3DGameManager.Texto(t, "", 28, TextAnchor.UpperRight);
            textoPiezas.rectTransform.offsetMax = new Vector2(-56, -170);
            textoPiezas.color = new Color(1f, 0.72f, 0.3f);

            textoRecarga = Forja3DGameManager.Texto(t, "", 38, TextAnchor.MiddleCenter);
            textoRecarga.rectTransform.offsetMin = new Vector2(0, -300);
            textoRecarga.rectTransform.offsetMax = new Vector2(0, -190);

            textoAccion = Forja3DGameManager.Texto(t, "", 36, TextAnchor.MiddleCenter);
            textoAccion.rectTransform.offsetMin = new Vector2(0, -190);
            textoAccion.rectTransform.offsetMax = new Vector2(0, -60);
            textoAccion.color = new Color(1f, 0.75f, 0.35f);

            mira = Forja3DGameManager.Texto(t, "+", 54, TextAnchor.MiddleCenter);
            mira.color = new Color(1f, 1f, 1f, 0.85f);
        }

        static string Barra(float p) { int n = Mathf.RoundToInt(Mathf.Clamp01(p) * 12f); return "[" + new string('|', n) + new string('.', 12 - n) + "]"; }

        void Update()
        {
            if (!textoArma) return;
            var tir = NATirador.Instancia;
            if (tir && tir.Arma != null)
            {
                textoArma.text = tir.Arma.nombre;
                string reserva = tir.Infinita ? "∞" : tir.Reserva.ToString();
                string color = tir.Cargado == 0 ? "#ff5a4a" : tir.Cargado <= Mathf.Max(1, tir.Arma.cargador / 4) ? "#ffc04a" : "#ffffff";
                textoMunicion.text = $"<color={color}>{tir.Cargado}</color><size=44> / {reserva}</size>";
                if (tir.Recargando) textoRecarga.text = "RECARGANDO  " + Barra(tir.ProgresoRecarga);
                else textoRecarga.text = tir.Cargado == 0 && tir.Reserva == 0 && !tir.Infinita ? "SIN MUNICIÓN — CAMBIA DE ARMA" : "";
            }

            textoPuntos.text = $"{NAPuntos.Puntos} PTS";
            string pod = "";
            if (NAPoderes.InstaKill) pod += $"<color=#ff3b30>INSTA-KILL {Mathf.CeilToInt(NAPoderes.RestaInsta)}</color>\n";
            if (NAPoderes.DoblePuntos) pod += $"<color=#ffd23f>DOBLE PUNTOS {Mathf.CeilToInt(NAPoderes.RestaDoble)}</color>\n";
            textoPoderes.text = pod;
            mira.color = NAPoderes.InstaKill ? new Color(1f, 0.25f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);

            var o = NAOleadas.Inst;
            if (o)
            {
                if (o.EnPausa && o.Oleada > 0) textoOleada.text = $"OLEADA {o.Oleada + 1} EN {Mathf.CeilToInt(o.SegundosParaLaSiguiente)}";
                else textoOleada.text = o.Oleada > 0 ? $"OLEADA {o.Oleada}" : "";
            }

            var mis = NAMision.Inst;
            var arm = NAArmado.Inst;
            textoObjetivo.text = mis ? "» " + mis.Objetivo : "";
            textoPiezas.text = arm ? arm.ListaPiezas() : "";

            string ind = ""; float prog = -1f;
            if (mis && !string.IsNullOrEmpty(mis.Indicacion)) { ind = mis.Indicacion; prog = mis.Progreso; }
            else if (arm && !string.IsNullOrEmpty(arm.Indicacion)) { ind = arm.Indicacion; prog = arm.Progreso; }
            if (prog >= 0f) ind += "\n" + Barra(prog);
            textoAccion.text = ind;
        }
    }
}
