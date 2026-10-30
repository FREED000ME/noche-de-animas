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
                c.rectTransform.anchorMin = new Vector2(0.1f, 0.05f); c.rectTransform.anchorMax = new Vector2(0.9f, 0.18f);
            }

            int guardado = PlayerPrefs.GetInt(ClaveProgreso, 1);
            int total = SceneManager.sceneCountInBuildSettings;
            Boton(raiz, "COMENZAR", new Vector2(0.5f, 0.38f), () => { PlayerPrefs.SetInt(ClaveProgreso, 1); SceneManager.LoadScene(1); });
            if (guardado > 1 && guardado < total)
                Boton(raiz, "CONTINUAR", new Vector2(0.5f, 0.25f), () => SceneManager.LoadScene(guardado));
        }

        static void Boton(Transform padre, string texto, Vector2 ancla, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(texto, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = ancla; r.sizeDelta = new Vector2(520, 120);
            go.GetComponent<Image>().color = new Color(0.45f, 0.08f, 0.06f, 0.9f);
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0.92f, 0.75f, 0.42f, 0.7f); o.effectDistance = new Vector2(2, -2);
            go.GetComponent<Button>().onClick.AddListener(accion);
            Forja3DGameManager.Texto(go.transform, texto, 46, TextAnchor.MiddleCenter);
        }
    }
}
