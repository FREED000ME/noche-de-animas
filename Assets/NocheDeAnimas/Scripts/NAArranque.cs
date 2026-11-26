// Noche de Ánimas — arranque de cada nivel: convierte la escena de la plantilla en el shooter de oleadas.
//   · Los enemigos colocados en el mapa (E, K, J) pasan a ser zombies de Noche de Ánimas.
//   · El jugador recibe sus armas caseras, su vida y su sombrero.
//   · Se añaden el director de oleadas y el HUD de munición.
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

                var tirador = jugador.AddComponent<NATirador>();
                tirador.Configurar(cfg);
                var oleadas = gm.gameObject.AddComponent<NAOleadas>();
                oleadas.cfg = cfg;
                gm.gameObject.AddComponent<NAHUD>();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Forja3D] Noche de Ánimas: error al preparar el nivel: " + ex);
            }
        }
    }

    // HUD propio: arma, munición, recarga y oleada
    public class NAHUD : MonoBehaviour
    {
        Text textoArma, textoMunicion, textoOleada, textoRecarga;

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

            textoOleada = Forja3DGameManager.Texto(t, "", 40, TextAnchor.UpperCenter);
            textoOleada.rectTransform.offsetMax = new Vector2(0, -36);
            textoOleada.color = new Color(1f, 0.62f, 0.25f);

            textoRecarga = Forja3DGameManager.Texto(t, "", 40, TextAnchor.MiddleCenter);
            textoRecarga.rectTransform.offsetMin = new Vector2(0, -320);
            textoRecarga.rectTransform.offsetMax = new Vector2(0, -200);
        }

        void Update()
        {
            var tir = NATirador.Instancia;
            if (textoArma && tir && tir.Arma != null)
            {
                textoArma.text = tir.Arma.nombre;
                string reserva = tir.Infinita ? "∞" : tir.Reserva.ToString();
                string color = tir.Cargado == 0 ? "#ff5a4a" : tir.Cargado <= Mathf.Max(1, tir.Arma.cargador / 4) ? "#ffc04a" : "#ffffff";
                textoMunicion.text = $"<color={color}>{tir.Cargado}</color><size=44> / {reserva}</size>";
                if (tir.Recargando)
                {
                    int n = Mathf.RoundToInt(tir.ProgresoRecarga * 10f);
                    textoRecarga.text = "RECARGANDO  " + new string('|', n) + new string('.', 10 - n);
                }
                else textoRecarga.text = tir.Cargado == 0 && tir.Reserva == 0 && !tir.Infinita ? "SIN MUNICIÓN — CAMBIA DE ARMA" : "";
            }
            var o = NAOleadas.Inst;
            if (textoOleada && o)
            {
                if (o.Completado) textoOleada.text = "¡SOBREVIVISTE!  VUELVE AL REFUGIO";
                else if (o.EnPausa && o.Oleada > 0) textoOleada.text = $"OLEADA {o.Oleada + 1} EN {Mathf.CeilToInt(o.SegundosParaLaSiguiente)}…";
                else if (o.Oleada > 0) textoOleada.text = $"OLEADA {o.Oleada}/{o.Total}   ·   QUEDAN {o.Restantes}";
                else textoOleada.text = "";
            }
        }
    }
}
