// Forja3D — diálogos y narración: caja inferior con nombre, texto letra a letra y "toca para continuar".
// Mientras hay un diálogo abierto, el jugador y los enemigos se detienen (Forja3DDialogue.Activo).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Forja3D
{
    [Serializable] public class Linea { public string quien; public string texto; }

    public class Forja3DDialogue : MonoBehaviour
    {
        public static bool Activo { get; private set; }
        static Forja3DDialogue inst;

        GameObject panel;
        Text nombre, texto, pista;
        Queue<Linea> cola;
        Action alTerminar;
        bool escribiendo;
        string completo;
        float bloqueoHasta;

        public static void Mostrar(IList<Linea> lineas, Action alTerminar = null)
        {
            if (lineas == null || lineas.Count == 0) { alTerminar?.Invoke(); return; }
            if (!inst) Crear();
            inst.Abrir(lineas, alTerminar);
        }

        static void Crear()
        {
            var canvasGo = new GameObject("Dialogos", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 50;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;
            inst = canvasGo.AddComponent<Forja3DDialogue>();
            inst.Construir(canvasGo.transform);
        }

        void Construir(Transform raiz)
        {
            // Capa que recibe el toque en toda la pantalla
            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
            panel.transform.SetParent(raiz, false);
            var rp = panel.GetComponent<RectTransform>(); rp.anchorMin = Vector2.zero; rp.anchorMax = Vector2.one; rp.offsetMin = rp.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.25f);
            panel.GetComponent<Button>().transition = Selectable.Transition.None;
            panel.GetComponent<Button>().onClick.AddListener(Avanzar);

            var caja = new GameObject("Caja", typeof(RectTransform), typeof(Image));
            caja.transform.SetParent(panel.transform, false);
            var rc = caja.GetComponent<RectTransform>(); rc.anchorMin = new Vector2(0.08f, 0.04f); rc.anchorMax = new Vector2(0.92f, 0.34f); rc.offsetMin = rc.offsetMax = Vector2.zero;
            var img = caja.GetComponent<Image>(); img.color = new Color(0.05f, 0.03f, 0.03f, 0.88f); img.raycastTarget = false;
            var borde = caja.AddComponent<Outline>(); borde.effectColor = new Color(0.75f, 0.55f, 0.25f, 0.8f); borde.effectDistance = new Vector2(3, -3);

            nombre = Forja3DGameManager.Texto(caja.transform, "", 40, TextAnchor.UpperLeft);
            nombre.color = new Color(0.95f, 0.78f, 0.42f); nombre.fontStyle = FontStyle.Bold;
            nombre.rectTransform.offsetMin = new Vector2(40, 20); nombre.rectTransform.offsetMax = new Vector2(-40, -20);
            texto = Forja3DGameManager.Texto(caja.transform, "", 38, TextAnchor.UpperLeft);
            texto.rectTransform.offsetMin = new Vector2(40, 30); texto.rectTransform.offsetMax = new Vector2(-40, -75);
            texto.horizontalOverflow = HorizontalWrapMode.Wrap; texto.verticalOverflow = VerticalWrapMode.Overflow;
            pista = Forja3DGameManager.Texto(caja.transform, "▼", 30, TextAnchor.LowerRight);
            pista.rectTransform.offsetMin = new Vector2(40, 15); pista.rectTransform.offsetMax = new Vector2(-30, -15);
            pista.color = new Color(1, 1, 1, 0.6f);
            // Letra de libro antiguo (la misma del pergamino); el nombre en mayúsculas pequeñas
            nombre.font = Forja3DPergamino.Fuente(); nombre.fontStyle = FontStyle.Normal; nombre.fontSize = 44;
            texto.font = Forja3DPergamino.Fuente(); texto.fontSize = 40;
            panel.SetActive(false);
        }

        void Abrir(IList<Linea> lineas, Action fin)
        {
            cola = new Queue<Linea>(lineas);
            alTerminar = fin;
            Activo = true;
            panel.SetActive(true);
            bloqueoHasta = Time.unscaledTime + 0.25f;
            Siguiente();
        }

        void Avanzar()
        {
            if (Time.unscaledTime < bloqueoHasta) return;
            if (escribiendo) { StopAllCoroutines(); texto.text = completo; escribiendo = false; return; }
            Siguiente();
        }

        void Siguiente()
        {
            if (cola.Count == 0)
            {
                panel.SetActive(false); Activo = false;
                var f = alTerminar; alTerminar = null; f?.Invoke();
                return;
            }
            var l = cola.Dequeue();
            bool narrador = string.IsNullOrEmpty(l.quien);
            nombre.text = narrador ? "" : l.quien.ToUpperInvariant();
            texto.fontStyle = narrador ? FontStyle.Italic : FontStyle.Normal;
            texto.rectTransform.offsetMax = new Vector2(-40, narrador ? -30 : -75);
            completo = l.texto ?? "";
            StopAllCoroutines(); StartCoroutine(Escribir());
        }

        IEnumerator Escribir()
        {
            escribiendo = true; pista.enabled = false; texto.text = "";
            for (int i = 1; i <= completo.Length; i++)
            {
                texto.text = completo.Substring(0, i);
                if (i % 3 == 0 && !char.IsWhiteSpace(completo[i - 1])) Forja3DAudio.Sonar("dialogo", null, 0.5f, 0.1f);
                yield return new WaitForSecondsRealtime(0.022f);
            }
            escribiendo = false; pista.enabled = true;
        }

        void Update()
        {
            // Teclado en PC (EXPANSIÓN FUTURA: botón del mando)
            if (Activo && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) Avanzar();
        }

        void OnDestroy() { if (inst == this) { inst = null; Activo = false; } }
    }
}
