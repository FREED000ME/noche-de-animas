// Forja3D — pergamino: la narración entre niveles se lee como en el libro donde se escribió.
// Un pliego de papel envejecido (generado por código, con bordes quemados) se desenrolla, el texto aparece
// como tinta que se seca letra a letra y se toca para seguir. También aloja el árbol de virtudes.
// Fuente: IM Fell English (SIL Open Font License, ver Resources/Fuentes/OFL_IMFellEnglish.txt).
// EXPANSIÓN FUTURA: en PC el toque equivale al clic o a la tecla Espacio/Enter (ya soportado por el botón).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Forja3D
{
    public class Forja3DPergamino : MonoBehaviour
    {
        public static bool Activo { get; private set; }

        static Font fuente, cursiva;
        public static Font Fuente(bool italica = false)
        {
            if (!fuente) fuente = Resources.Load<Font>("Fuentes/IMFellEnglish");
            if (!cursiva) cursiva = Resources.Load<Font>("Fuentes/IMFellEnglish_Italic");
            var f = italica ? cursiva : fuente;
            return f ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static readonly Color Tinta = new Color(0.2f, 0.12f, 0.06f), TintaRoja = new Color(0.45f, 0.1f, 0.05f);

        // Muestra un pliego. extra: texto pequeño bajo el cuerpo (XP); virtudes: añade el enlace al árbol.
        public static void Mostrar(string encabezado, string cuerpo, string pie, Action alCerrar, string extra = null, bool virtudes = false)
        {
            var go = new GameObject("Pergamino");
            go.AddComponent<Forja3DPergamino>().Abrir(encabezado, cuerpo, pie, alCerrar, extra, virtudes);
        }

        Action alCerrar;
        Text cuerpoTxt, pieTxt; string cuerpoCompleto; bool escribiendo, cerrando;
        RectTransform hoja; float listoDesde;
        Transform raizCanvas;

        void Abrir(string encabezado, string cuerpo, string pie, Action cerrar, string extra, bool virtudes)
        {
            Activo = true; alCerrar = cerrar;
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false); raizCanvas = canvasGo.transform;
            var c = canvasGo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 90;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;

            // Fondo oscuro que recibe el toque
            var fondo = Img(canvasGo.transform, "Fondo", new Color(0.02f, 0.01f, 0.01f, 0.92f));
            Estirar(fondo.rectTransform, Vector2.zero, Vector2.one);
            var b = fondo.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(Tocar);

            // La hoja
            var papel = Img(canvasGo.transform, "Hoja", Color.white); papel.raycastTarget = false;
            papel.sprite = SpritePapel(); papel.type = Image.Type.Simple;
            hoja = papel.rectTransform; Estirar(hoja, new Vector2(0.14f, 0.05f), new Vector2(0.86f, 0.95f));
            var sombra = papel.gameObject.AddComponent<Shadow>(); sombra.effectColor = new Color(0, 0, 0, 0.6f); sombra.effectDistance = new Vector2(10, -10);

            var titulo = Txt(hoja, encabezado ?? "", 76, TextAnchor.UpperCenter, TintaRoja, false);
            titulo.verticalOverflow = VerticalWrapMode.Overflow; titulo.lineSpacing = 0.9f;
            Estirar(titulo.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f));
            var orla = Txt(hoja, "~ · ~", 40, TextAnchor.UpperCenter, TintaRoja * 0.9f, false);
            Estirar(orla.rectTransform, new Vector2(0.3f, 0.72f), new Vector2(0.7f, 0.79f));

            cuerpoCompleto = cuerpo ?? "";
            cuerpoTxt = Txt(hoja, "", 42, TextAnchor.UpperLeft, Tinta, false);
            cuerpoTxt.lineSpacing = 1.12f; cuerpoTxt.horizontalOverflow = HorizontalWrapMode.Wrap; cuerpoTxt.verticalOverflow = VerticalWrapMode.Overflow;
            Estirar(cuerpoTxt.rectTransform, new Vector2(0.11f, extra != null ? 0.27f : 0.16f), new Vector2(0.89f, 0.71f));

            if (!string.IsNullOrEmpty(extra))
            {
                var ex = Txt(hoja, extra, 34, TextAnchor.MiddleCenter, TintaRoja, true);
                Estirar(ex.rectTransform, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.27f));
            }
            if (virtudes)
            {
                var v = Txt(hoja, Forja3DProgreso.Disponibles > 0 ? $"† Virtudes ({Forja3DProgreso.Disponibles} por asignar)" : "† Virtudes", 38, TextAnchor.MiddleLeft, TintaRoja, false);
                Estirar(v.rectTransform, new Vector2(0.1f, 0.05f), new Vector2(0.55f, 0.13f));
                v.raycastTarget = true;
                var bv = v.gameObject.AddComponent<Button>(); bv.transition = Selectable.Transition.None;
                bv.onClick.AddListener(() => { if (!escribiendo) Forja3DVirtudes.Abrir(raizCanvas, () => v.text = Forja3DProgreso.Disponibles > 0 ? $"† Virtudes ({Forja3DProgreso.Disponibles} por asignar)" : "† Virtudes"); });
            }
            pieTxt = Txt(hoja, pie ?? "— toca para continuar —", 32, virtudes ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, Tinta * 1.6f, true);
            Estirar(pieTxt.rectTransform, new Vector2(virtudes ? 0.5f : 0.1f, 0.05f), new Vector2(0.9f, 0.13f));
            pieTxt.color = new Color(pieTxt.color.r, pieTxt.color.g, pieTxt.color.b, 0f);

            Forja3DAudio.Sonar("esquiva", null, 0.35f, 0.05f, 0.55f); // roce de papel
            StartCoroutine(Desenrollar());
        }

        IEnumerator Desenrollar()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.55f)
            {
                float k = 1f - Mathf.Pow(1f - t, 3f);
                hoja.localScale = new Vector3(1f, Mathf.Lerp(0.04f, 1f, k), 1f);
                yield return null;
            }
            hoja.localScale = Vector3.one;
            escribiendo = true;
            // Tinta letra a letra (≈55 letras por segundo)
            float acumulado = 0f; int n = 0;
            while (n < cuerpoCompleto.Length && escribiendo)
            {
                acumulado += Time.unscaledDeltaTime * 55f;
                int nuevo = Mathf.Min(cuerpoCompleto.Length, Mathf.FloorToInt(acumulado));
                if (nuevo != n) { n = nuevo; cuerpoTxt.text = cuerpoCompleto.Substring(0, n) + "<color=#00000000>" + cuerpoCompleto.Substring(n) + "</color>"; }
                yield return null;
            }
            cuerpoTxt.text = cuerpoCompleto; escribiendo = false; listoDesde = Time.unscaledTime;
        }

        void Update()
        {
            if (!pieTxt || escribiendo || cerrando) return;
            var c = pieTxt.color; c.a = 0.55f + 0.35f * Mathf.Sin((Time.unscaledTime - listoDesde) * 2.5f); pieTxt.color = c;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Tocar();
        }

        void Tocar()
        {
            if (cerrando || Forja3DVirtudes.Abierto) return;
            if (escribiendo) { escribiendo = false; return; }       // primer toque: termina de escribir
            if (Time.unscaledTime - listoDesde < 0.25f) return;
            StartCoroutine(Cerrar());
        }

        IEnumerator Cerrar()
        {
            cerrando = true;
            var grupo = hoja.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { grupo.alpha = 1f - t; hoja.localScale = new Vector3(1f, 1f - t * 0.3f, 1f); yield return null; }
            Activo = false;
            var cb = alCerrar; Destroy(gameObject); cb?.Invoke();
        }

        // ---------- Papel envejecido por código ----------
        static Sprite papelCache;
        static Sprite SpritePapel()
        {
            if (papelCache) return papelCache;
            const int w = 512, h = 640;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var baseC = new Color(0.87f, 0.79f, 0.6f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    // Fibras y manchas
                    float fibra = Mathf.PerlinNoise(x * 0.9f, y * 0.04f) * 0.06f;
                    float mancha = Mathf.PerlinNoise(u * 5f + 3f, v * 5f + 7f);
                    float grano = Mathf.PerlinNoise(x * 0.35f + 11f, y * 0.35f + 5f) * 0.05f;
                    var c = baseC * (0.92f + fibra + grano) - new Color(0.12f, 0.12f, 0.1f, 0f) * Mathf.SmoothStep(0.55f, 0.85f, mancha);
                    // Oscurece hacia los bordes y los quema con un contorno irregular
                    float dx = Mathf.Min(u, 1f - u), dy = Mathf.Min(v, 1f - v);
                    float d = Mathf.Min(dx * 1.25f, dy);
                    float ruidoBorde = Mathf.PerlinNoise(u * 14f, v * 14f) * 0.05f + Mathf.PerlinNoise(u * 40f + 9f, v * 40f) * 0.015f;
                    float borde = d - ruidoBorde;
                    c = Color.Lerp(c, new Color(0.55f, 0.38f, 0.2f), Mathf.Clamp01(1f - borde / 0.12f) * 0.75f);
                    if (borde < 0.022f) c = Color.Lerp(new Color(0.12f, 0.06f, 0.03f), c, Mathf.Clamp01((borde - 0.008f) / 0.014f));
                    c.a = borde < 0.008f ? 0f : 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px); t.Apply();
            return papelCache = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        // ---------- Ayudas de UI ----------
        public static Image Img(Transform padre, string n, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(padre, false);
            var i = go.GetComponent<Image>(); i.color = c; return i;
        }
        public static Text Txt(Transform padre, string s, int tam, TextAnchor anc, Color c, bool italica)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text)); go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>(); t.font = Fuente(italica); t.text = s; t.fontSize = tam; t.alignment = anc; t.color = c;
            t.raycastTarget = false; t.supportRichText = true;
            return t;
        }
        public static void Estirar(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }
    }

    // Árbol de virtudes: cuatro columnas (virtudes cardinales) con tres grados cada una
    public class Forja3DVirtudes : MonoBehaviour
    {
        public static bool Abierto { get; private set; }
        Action alCerrar; Text puntos; readonly Text[,] nodos = new Text[4, 3];

        public static void Abrir(Transform canvas, Action cerrar)
        {
            var go = new GameObject("Virtudes", typeof(RectTransform)); go.transform.SetParent(canvas, false);
            Forja3DPergamino.Estirar(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            go.AddComponent<Forja3DVirtudes>().Construir(cerrar);
        }

        void Construir(Action cerrar)
        {
            Abierto = true; alCerrar = cerrar;
            var fondo = Forja3DPergamino.Img(transform, "Fondo", new Color(0.05f, 0.03f, 0.02f, 0.94f));
            Forja3DPergamino.Estirar(fondo.rectTransform, Vector2.zero, Vector2.one);
            var titulo = Forja3DPergamino.Txt(transform, "Las virtudes cardinales", 64, TextAnchor.MiddleCenter, new Color(0.93f, 0.8f, 0.55f), false);
            Forja3DPergamino.Estirar(titulo.rectTransform, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.97f));
            puntos = Forja3DPergamino.Txt(transform, "", 36, TextAnchor.MiddleCenter, new Color(0.85f, 0.75f, 0.6f), true);
            Forja3DPergamino.Estirar(puntos.rectTransform, new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.87f));
            for (int r = 0; r < 4; r++)
            {
                float x0 = 0.04f + r * 0.24f, x1 = x0 + 0.22f;
                var nom = Forja3DPergamino.Txt(transform, Forja3DProgreso.Ramas[r], 46, TextAnchor.MiddleCenter, new Color(0.95f, 0.82f, 0.5f), false);
                Forja3DPergamino.Estirar(nom.rectTransform, new Vector2(x0, 0.71f), new Vector2(x1, 0.79f));
                var lema = Forja3DPergamino.Txt(transform, Forja3DProgreso.Lemas[r], 28, TextAnchor.MiddleCenter, new Color(0.75f, 0.68f, 0.58f), true);
                Forja3DPergamino.Estirar(lema.rectTransform, new Vector2(x0, 0.66f), new Vector2(x1, 0.72f));
                for (int g = 0; g < 3; g++)
                {
                    float y1 = 0.64f - g * 0.17f, y0 = y1 - 0.15f;
                    var caja = Forja3DPergamino.Img(transform, "Nodo", Color.white);
                    caja.sprite = null; Forja3DPergamino.Estirar(caja.rectTransform, new Vector2(x0, y0), new Vector2(x1, y1));
                    int rama = r, grado = g;
                    var b = caja.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() => { if (Forja3DProgreso.Grado(rama) == grado && Forja3DProgreso.Comprar(rama)) { Forja3DAudio.Sonar("victoria", null, 0.4f, 0f, 1.4f); Refrescar(); } });
                    var t = Forja3DPergamino.Txt(caja.transform, "", 28, TextAnchor.MiddleCenter, Color.white, false);
                    Forja3DPergamino.Estirar(t.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f));
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    nodos[r, g] = t;
                }
            }
            var volver = Forja3DPergamino.Txt(transform, "— volver —", 40, TextAnchor.MiddleCenter, new Color(0.9f, 0.8f, 0.6f), true);
            Forja3DPergamino.Estirar(volver.rectTransform, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.11f));
            volver.raycastTarget = true;
            var bv = volver.gameObject.AddComponent<Button>(); bv.transition = Selectable.Transition.None;
            bv.onClick.AddListener(() => { Abierto = false; alCerrar?.Invoke(); Destroy(gameObject); });
            Refrescar();
        }

        void Refrescar()
        {
            int xp = Forja3DProgreso.XP, falta = Forja3DProgreso.XPPorPunto - xp % Forja3DProgreso.XPPorPunto;
            puntos.text = $"Experiencia: {xp}   ·   Puntos por asignar: {Forja3DProgreso.Disponibles}   ·   Siguiente punto en {falta} XP";
            for (int r = 0; r < 4; r++)
                for (int g = 0; g < 3; g++)
                {
                    int grado = Forja3DProgreso.Grado(r);
                    bool tiene = g < grado, siguiente = g == grado, puede = siguiente && Forja3DProgreso.Disponibles > 0;
                    var caja = nodos[r, g].transform.parent.GetComponent<Image>();
                    caja.color = tiene ? new Color(0.55f, 0.36f, 0.12f, 0.95f) : puede ? new Color(0.32f, 0.2f, 0.08f, 0.95f) : new Color(0.14f, 0.1f, 0.08f, 0.9f);
                    nodos[r, g].color = tiene ? new Color(1f, 0.93f, 0.75f) : puede ? new Color(0.95f, 0.82f, 0.55f) : new Color(0.55f, 0.5f, 0.45f);
                    nodos[r, g].text = (tiene ? "† " : puede ? "+ " : "· ") + Forja3DProgreso.Grados[r, g] + (puede ? "\n<i>(toca para aprender)</i>" : "");
                }
        }
    }
}
