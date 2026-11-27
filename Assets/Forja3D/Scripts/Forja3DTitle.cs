// Forja3D — pantalla de título: COMENZAR (desde el nivel 1) y CONTINUAR (último nivel alcanzado).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Forja3D
{
    public class Forja3DTitle : MonoBehaviour
    {
        public string titulo = "JUEGO";
        public string subtitulo = "";
        public string cita = "";
        public string citaTraduccion = "";
        public Color colorFondo = new Color(0.05f, 0.02f, 0.02f);
        public Color colorTitulo = new Color(0.92f, 0.75f, 0.42f);

        public const string ClaveProgreso = "forja3d_nivel";
        // El juego puede describir cada nivel en el selector (nombre, récord…). Recibe el índice de escena.
        public static System.Func<int, string> InfoNivel;
        // Abrir directamente el selector de niveles al cargar el título (p. ej. desde el menú de pausa)
        public static bool AbrirSelector;
        GameObject selector;

        void Start()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = colorFondo; }
            if (!FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Titulo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;
            var raiz = canvasGo.transform;

            var t = Forja3DGameManager.Texto(raiz, titulo, 130, TextAnchor.MiddleCenter);
            t.color = colorTitulo; t.fontStyle = FontStyle.Bold;
            t.rectTransform.anchorMin = new Vector2(0, 0.6f); t.rectTransform.anchorMax = new Vector2(1, 0.85f);
            var s = Forja3DGameManager.Texto(raiz, subtitulo, 50, TextAnchor.MiddleCenter);
            s.fontStyle = FontStyle.Italic; s.color = new Color(1, 1, 1, 0.8f);
            s.rectTransform.anchorMin = new Vector2(0, 0.52f); s.rectTransform.anchorMax = new Vector2(1, 0.6f);
            if (!string.IsNullOrEmpty(cita))
            {
                var c = Forja3DGameManager.Texto(raiz, $"«{cita}»\n<size=28>{citaTraduccion}</size>", 34, TextAnchor.MiddleCenter);
                c.supportRichText = true; c.fontStyle = FontStyle.Italic; c.color = new Color(1, 1, 1, 0.55f);
                c.rectTransform.anchorMin = new Vector2(0.1f, 0.01f); c.rectTransform.anchorMax = new Vector2(0.9f, 0.11f);
            }

            int guardado = PlayerPrefs.GetInt(ClaveProgreso, 1);
            int total = SceneManager.sceneCountInBuildSettings;
            bool continuar = guardado > 1 && guardado < total;
            Boton(raiz, "COMENZAR", new Vector2(0.5f, 0.42f), () => { PlayerPrefs.SetInt(ClaveProgreso, 1); SceneManager.LoadScene(1); });
            if (continuar)
                Boton(raiz, "CONTINUAR", new Vector2(0.5f, 0.30f), () => SceneManager.LoadScene(guardado));
            if (total > 2) Boton(raiz, "ELEGIR NIVEL", new Vector2(0.5f, continuar ? 0.18f : 0.30f), () => selector.SetActive(true));
            CrearSelector(raiz, total);
            if (AbrirSelector) { AbrirSelector = false; selector.SetActive(true); }
        }

        // Lista de niveles (todos disponibles), con lo que el juego quiera contar de cada uno
        void CrearSelector(Transform raiz, int total)
        {
            selector = new GameObject("Selector", typeof(RectTransform), typeof(Image));
            selector.transform.SetParent(raiz, false);
            var r = selector.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            selector.GetComponent<Image>().color = new Color(colorFondo.r, colorFondo.g, colorFondo.b, 0.97f);
            var t = Forja3DGameManager.Texto(selector.transform, "ELIGE UN NIVEL", 70, TextAnchor.MiddleCenter);
            t.color = colorTitulo; t.fontStyle = FontStyle.Bold;
            t.rectTransform.anchorMin = new Vector2(0, 0.84f); t.rectTransform.anchorMax = new Vector2(1, 0.96f);
            int n = total - 1;
            for (int i = 1; i <= n; i++)
            {
                int indice = i;
                string nombre = System.IO.Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                string info = InfoNivel != null ? InfoNivel(i) : nombre;
                float y = 0.74f - (i - 1) * Mathf.Min(0.15f, 0.6f / Mathf.Max(1, n));
                var b = Boton(selector.transform, info, new Vector2(0.5f, y), () => SceneManager.LoadScene(indice));
                b.sizeDelta = new Vector2(1100, 115);
                var txt = b.GetComponentInChildren<Text>(); txt.fontSize = 36; txt.supportRichText = true;
            }
            Boton(selector.transform, "VOLVER", new Vector2(0.5f, 0.08f), () => selector.SetActive(false));
            selector.SetActive(false);
        }

        static RectTransform Boton(Transform padre, string texto, Vector2 ancla, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(texto, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = ancla; r.sizeDelta = new Vector2(520, 120);
            go.GetComponent<Image>().color = new Color(0.45f, 0.08f, 0.06f, 0.9f);
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0.92f, 0.75f, 0.42f, 0.7f); o.effectDistance = new Vector2(2, -2);
            go.GetComponent<Button>().onClick.AddListener(accion);
            Forja3DGameManager.Texto(go.transform, texto, 46, TextAnchor.MiddleCenter);
            return r;
        }
    }
}
