// Noche de Ánimas — salpicaduras (zombies que revientan, frutas del mercado), récord de oleadas
// y menú de pausa (continuar, reiniciar, elegir nivel, menú principal).
using System.Collections.Generic;
using Forja3D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NocheDeAnimas
{
    // ───────── Salpicaduras: trozos que saltan y una mancha en el suelo ─────────
    public static class NASplash
    {
        static int manchas;

        public static void En(Vector3 pos, Color color, int trozos, float fuerza, float tamMancha)
        {
            for (int i = 0; i < trozos; i++)
            {
                var g = new GameObject("Trozo");
                g.transform.position = pos + Random.insideUnitSphere * 0.15f;
                float t = Random.Range(0.06f, 0.16f);
                NAMat.Pieza(PrimitiveType.Cube, g.transform, Vector3.zero, Vector3.one * t, color * Random.Range(0.75f, 1.1f), new Vector3(Random.Range(0, 90f), Random.Range(0, 90f), 0));
                var tr = g.AddComponent<NATrozo>();
                var v = Random.insideUnitSphere; v.y = Mathf.Abs(v.y) + 0.6f;
                tr.vel = v.normalized * fuerza * Random.Range(0.6f, 1.2f);
            }
            if (tamMancha > 0f) Mancha(pos, color * 0.7f, tamMancha);
        }

        public static void Mancha(Vector3 pos, Color color, float tam)
        {
            if (manchas > 40) return; // tope para el móvil
            float suelo = Suelo(pos);
            var g = new GameObject("Mancha");
            g.transform.position = new Vector3(pos.x, suelo + 0.03f + Random.Range(0f, 0.01f), pos.z);
            for (int i = 0; i < 4; i++)
            {
                var off = i == 0 ? Vector3.zero : new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * tam * 0.6f;
                float s = i == 0 ? tam : tam * Random.Range(0.25f, 0.5f);
                NAMat.Pieza(PrimitiveType.Cylinder, g.transform, off, new Vector3(s, 0.005f, s * Random.Range(0.7f, 1.1f)), color);
            }
            g.AddComponent<NAMancha>();
            manchas++;
        }

        public static void Quitada() => manchas = Mathf.Max(0, manchas - 1);
        public static void Reiniciar() => manchas = 0;

        public static float Suelo(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            return t ? t.SampleHeight(p) + t.transform.position.y : 0f;
        }
    }

    public class NATrozo : MonoBehaviour
    {
        public Vector3 vel; float vida = 1.6f; bool quieto;
        void Update()
        {
            vida -= Time.deltaTime;
            if (vida <= 0f) { Destroy(gameObject); return; }
            if (vida < 0.4f) transform.localScale = Vector3.one * (vida / 0.4f);
            if (quieto) return;
            vel += Vector3.down * 14f * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            transform.Rotate(vel * 40f * Time.deltaTime, Space.World);
            float s = NASplash.Suelo(transform.position) + 0.05f;
            if (transform.position.y < s) { transform.position = new Vector3(transform.position.x, s, transform.position.z); quieto = true; }
        }
    }

    public class NAMancha : MonoBehaviour
    {
        float nace;
        void Start() => nace = Time.time;
        void Update()
        {
            float edad = Time.time - nace;
            if (edad > 14f) transform.localScale = Vector3.one * Mathf.Clamp01(1f - (edad - 14f) / 2f);
            if (edad > 16f) Destroy(gameObject);
        }
        void OnDestroy() => NASplash.Quitada();
    }

    // ───────── Frutas del mercado: revientan de un tiro ─────────
    public class NAFruta : MonoBehaviour
    {
        public static readonly List<NAFruta> Todas = new List<NAFruta>();
        public Color color, pulpa;
        bool rota;

        public static NAFruta Crear(Transform padre, Vector3 pos, float tam, Color color, Color pulpa)
        {
            var g = new GameObject("Fruta"); g.transform.SetParent(padre, false); g.transform.localPosition = pos;
            NAMat.Pieza(PrimitiveType.Sphere, g.transform, Vector3.zero, Vector3.one * tam, color);
            var col = g.AddComponent<SphereCollider>(); col.radius = tam * 0.6f;
            var f = g.AddComponent<NAFruta>(); f.color = color; f.pulpa = pulpa;
            Todas.Add(f);
            return f;
        }

        void OnDestroy() => Todas.Remove(this);

        public void Reventar()
        {
            if (rota) return;
            rota = true;
            NASonido.Sonar("escupitajo", transform.position, 0.8f, Random.Range(1.1f, 1.5f));
            NASplash.En(transform.position, pulpa, 9, 3.5f, 0f);
            NASplash.En(transform.position, color, 4, 2.5f, 0.55f);
            NAPuntos.Sumar(5);
            Destroy(gameObject);
        }

        public static void Explosion(Vector3 centro, float radio)
        {
            foreach (var f in new List<NAFruta>(Todas)) if (f && Vector3.Distance(f.transform.position, centro) <= radio) f.Reventar();
        }
    }

    // ───────── Récord de oleadas por nivel ─────────
    public static class NARecord
    {
        static string Clave(string escena) => "na_record_" + (escena.Length >= 2 ? escena.Substring(0, 2) : escena);
        public static int De(string escena) => PlayerPrefs.GetInt(Clave(escena), 0);
        public static bool Registrar(string escena, int oleada)
        {
            if (oleada <= De(escena)) return false;
            PlayerPrefs.SetInt(Clave(escena), oleada); PlayerPrefs.Save();
            return true;
        }
    }

    // ───────── Pausa ─────────
    public class NAPausa : MonoBehaviour
    {
        public static bool Activa { get; private set; }
        GameObject panel, controles;
        Text textoRecord;

        void Awake() => Activa = false;
        void OnDestroy() { if (Activa) { Activa = false; Time.timeScale = 1f; AudioListener.pause = false; } }

        void Start()
        {
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvasGo = new GameObject("Pausa", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 80;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;
            var raiz = canvasGo.transform;

            // Botón de pausa, arriba a la derecha (junto al texto de la oleada)
            var bp = Boton(raiz, "II", new Vector2(0.5f, 1f), new Vector2(430, -62), new Vector2(96, 96), Pausar);
            bp.GetComponentInChildren<Text>().fontSize = 44;

            panel = new GameObject("Menu", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(raiz, false);
            var r = panel.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.05f, 0.02f, 0.04f, 0.88f);
            var titulo = Forja3DGameManager.Texto(panel.transform, "PAUSA", 90, TextAnchor.MiddleCenter);
            titulo.color = new Color(1f, 0.62f, 0.2f); titulo.fontStyle = FontStyle.Bold;
            titulo.rectTransform.anchorMin = new Vector2(0, 0.8f); titulo.rectTransform.anchorMax = new Vector2(1, 0.95f);
            textoRecord = Forja3DGameManager.Texto(panel.transform, "", 40, TextAnchor.MiddleCenter);
            textoRecord.supportRichText = true;
            textoRecord.rectTransform.anchorMin = new Vector2(0, 0.68f); textoRecord.rectTransform.anchorMax = new Vector2(1, 0.8f);

            Boton(panel.transform, "CONTINUAR", new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(620, 110), Continuar);
            Boton(panel.transform, "REINICIAR NIVEL", new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(620, 110), () => Cargar(SceneManager.GetActiveScene().buildIndex, false));
            Boton(panel.transform, "ELEGIR NIVEL", new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(620, 110), () => Cargar(0, true));
            Boton(panel.transform, "MENÚ PRINCIPAL", new Vector2(0.5f, 0.19f), Vector2.zero, new Vector2(620, 110), () => Cargar(0, false));
            panel.SetActive(false);
        }

        static RectTransform Boton(Transform padre, string texto, Vector2 ancla, Vector2 pos, Vector2 tam, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(texto, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = ancla; r.anchoredPosition = pos; r.sizeDelta = tam;
            go.GetComponent<Image>().color = new Color(0.45f, 0.08f, 0.06f, 0.85f);
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(1f, 0.62f, 0.2f, 0.8f); o.effectDistance = new Vector2(2, -2);
            go.GetComponent<Button>().onClick.AddListener(accion);
            Forja3DGameManager.Texto(go.transform, texto, 44, TextAnchor.MiddleCenter);
            return r;
        }

        void Update()
        {
            if (!Forja3DInput.EsTactil && Input.GetKeyDown(KeyCode.Escape)) { if (Activa) Continuar(); else Pausar(); }
        }

        public void Pausar()
        {
            if (Activa || Forja3DGameManager.Terminado || !panel) return;
            Activa = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (!controles) controles = GameObject.Find("ControlesTactiles");
            if (controles) controles.SetActive(false);
            Forja3DInput.Reiniciar();
            var esc = SceneManager.GetActiveScene().name;
            var o = NAOleadas.Inst;
            textoRecord.text = $"{NAMapas.Titulo(esc)}\n<color=#ffb347>Oleada {(o ? o.Oleada : 0)}  ·  Récord: {NARecord.De(esc)} oleadas</color>";
            panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Continuar()
        {
            if (!Activa) return;
            Activa = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (controles) controles.SetActive(true);
            panel.SetActive(false);
        }

        void Cargar(int indice, bool selector)
        {
            Activa = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Forja3DTitle.AbrirSelector = selector;
            SceneManager.LoadScene(indice);
        }
    }
}
