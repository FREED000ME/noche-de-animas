// Forja3D — controles táctiles leyendo los dedos directamente (sin EventSystem).
// Cada dedo recibe un papel al tocar la pantalla y lo conserva hasta soltarlo:
//   · mitad izquierda  → joystick flotante (aparece donde pones el dedo)
//   · botones          → ATACAR / CUBRIR (mantener) / ESQUIVAR / SALTAR / CORRER (alterna)
//   · resto de la mitad derecha → girar la cámara
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ── EXPANSIÓN FUTURA ───────────────────────────────────────────────────────────
// iOS: en iPhone con notch/isla dinámica, colocar los botones dentro de Screen.safeArea
//   (convertir safeArea a coordenadas del canvas y desplazar 'raiz').
// PC: este componente se desactiva solo fuera de móvil (mostrarEnPC = false).
// Ajustes de jugabilidad: sensibilidadCamara, radioJoystick y las posiciones de Boton() en Start().
// ──────────────────────────────────────────────────────────────────────────────
namespace Forja3D
{
    public class Forja3DTouchControls : MonoBehaviour
    {
        public bool mostrarEnPC = false;
        [Tooltip("Grados de giro por cada pantalla completa arrastrada")]
        public float sensibilidadCamara = 220f;
        public float radioJoystick = 150f; // en unidades del canvas

        enum Papel { Joystick, Camara, Boton }
        readonly Dictionary<int, Papel> papeles = new Dictionary<int, Papel>();
        int dedoJoystick = -1;
        Vector2 origenJoystick;
        RectTransform raiz, fondo, palanca;
        class Boton
        {
            public RectTransform rect; public Image img; public Color color;
            public System.Action alPulsar, alSoltar; public System.Func<bool> encendido; public int dedos;
        }
        readonly List<Boton> botones = new List<Boton>();
        readonly Dictionary<int, Boton> botonDeDedo = new Dictionary<int, Boton>();
        Vector2 posReposo;
        Canvas canvas;

        public static void Crear(Transform canvas)
        {
            var go = new GameObject("ControlesTactiles", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            go.AddComponent<Forja3DTouchControls>();
        }

        void Start()
        {
            if (!mostrarEnPC && !Application.isMobilePlatform) { gameObject.SetActive(false); return; }
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;
            raiz = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            fondo = Panel("Joystick", transform, new Color(1, 1, 1, 0.15f), new Vector2(radioJoystick * 2, radioJoystick * 2));
            fondo.anchorMin = fondo.anchorMax = Vector2.zero;
            posReposo = new Vector2(260, 260);
            fondo.anchoredPosition = posReposo;
            palanca = Panel("Palanca", fondo, new Color(1, 1, 1, 0.45f), new Vector2(120, 120));
            palanca.anchorMin = palanca.anchorMax = new Vector2(0.5f, 0.5f);
            fondo.GetComponent<Image>().sprite = Circulo(); palanca.GetComponent<Image>().sprite = Circulo();

            Forja3DInput.Reiniciar();
            // Racimo de botones abajo a la derecha (unidades del canvas 1920x1080, ancla esquina inferior derecha)
            CrearBoton("ATACAR", new Vector2(-220, 230), 230, new Color(0.85f, 0.22f, 0.18f, 0.72f), Forja3DInput.PulsarAtaque);
            CrearBoton("CUBRIR", new Vector2(-500, 150), 175, new Color(0.85f, 0.7f, 0.25f, 0.65f),
                () => Forja3DInput.CubrirTactil = true, () => Forja3DInput.CubrirTactil = false);
            CrearBoton("ESQUIVAR", new Vector2(-230, 500), 170, new Color(0.3f, 0.75f, 0.55f, 0.65f), Forja3DInput.PulsarEsquiva);
            CrearBoton("SALTAR", new Vector2(-480, 400), 140, new Color(0.25f, 0.55f, 0.95f, 0.6f), Forja3DInput.PulsarSalto);
            CrearBoton("CORRER", new Vector2(-740, 120), 135, new Color(0.6f, 0.6f, 0.65f, 0.55f), Forja3DInput.AlternarCorrer, null, () => Forja3DInput.CorrerActivo);
        }

        void CrearBoton(string texto, Vector2 pos, float tam, Color color, System.Action alPulsar, System.Action alSoltar = null, System.Func<bool> encendido = null)
        {
            var r = Panel(texto, transform, color, new Vector2(tam, tam));
            r.anchorMin = r.anchorMax = new Vector2(1, 0); r.anchoredPosition = pos;
            var img = r.GetComponent<Image>(); img.sprite = Circulo();
            Forja3DGameManager.Texto(r, texto, Mathf.RoundToInt(Mathf.Clamp(tam * 0.16f, 22, 34)), TextAnchor.MiddleCenter);
            botones.Add(new Boton { rect = r, img = img, color = color, alPulsar = alPulsar, alSoltar = alSoltar, encendido = encendido });
        }

        // Sprite circular generado (botones redondos sin necesidad de imágenes)
        static Sprite circulo;
        static Sprite Circulo()
        {
            if (circulo) return circulo;
            const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                    float a = Mathf.Clamp01((1f - d) * 24f);
                    float borde = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 30f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * (0.75f + 0.25f * borde)));
                }
            tex.Apply();
            return circulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void LateUpdate()
        {
            foreach (var b in botones)
            {
                bool on = b.dedos > 0 || (b.encendido != null && b.encendido());
                b.img.color = on ? Color.Lerp(b.color, Color.white, 0.45f) : b.color;
            }
        }

        static RectTransform Panel(string nombre, Transform padre, Color color, Vector2 tam)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            var r = go.GetComponent<RectTransform>(); r.sizeDelta = tam;
            return r;
        }

        Vector2 ACanvas(Vector2 pantalla)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz, pantalla, null, out var p);
            return p - raiz.rect.min; // origen abajo a la izquierda
        }

        void Update()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began: Empezar(t); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: Mover(t); break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled: Terminar(t); break;
                }
            }
            // Seguridad: si se perdió algún dedo, suelta todo
            if (Input.touchCount == 0 && (papeles.Count > 0 || dedoJoystick >= 0)) { papeles.Clear(); SoltarJoystick(); SoltarBotones(); }
        }

        void Empezar(Touch t)
        {
            foreach (var b in botones)
                if (Dentro(b.rect, t.position))
                {
                    papeles[t.fingerId] = Papel.Boton;
                    botonDeDedo[t.fingerId] = b;
                    b.dedos++;
                    b.alPulsar?.Invoke();
                    return;
                }
            if (t.position.x < Screen.width * 0.45f && dedoJoystick < 0)
            {
                papeles[t.fingerId] = Papel.Joystick;
                dedoJoystick = t.fingerId;
                origenJoystick = ACanvas(t.position);
                fondo.anchoredPosition = origenJoystick;
                fondo.GetComponent<Image>().color = new Color(1, 1, 1, 0.25f);
                Mover(t);
                return;
            }
            papeles[t.fingerId] = Papel.Camara;
        }

        void Mover(Touch t)
        {
            if (!papeles.TryGetValue(t.fingerId, out var papel)) return;
            if (papel == Papel.Joystick)
            {
                var d = ACanvas(t.position) - origenJoystick;
                var v = Vector2.ClampMagnitude(d / radioJoystick, 1f);
                if (v.magnitude < 0.12f) v = Vector2.zero; // zona muerta
                Forja3DInput.JoystickTactil = v;
                palanca.anchoredPosition = v * radioJoystick;
            }
            else if (papel == Papel.Camara && t.phase == TouchPhase.Moved)
            {
                // Independiente de la resolución: arrastrar toda la altura de la pantalla = sensibilidadCamara grados
                Forja3DInput.MiradaTactil += t.deltaPosition / Mathf.Max(1, Screen.height) * sensibilidadCamara;
            }
        }

        void Terminar(Touch t)
        {
            if (botonDeDedo.TryGetValue(t.fingerId, out var b))
            {
                b.dedos = Mathf.Max(0, b.dedos - 1);
                if (b.dedos == 0) b.alSoltar?.Invoke();
                botonDeDedo.Remove(t.fingerId);
            }
            papeles.Remove(t.fingerId);
            if (t.fingerId == dedoJoystick) SoltarJoystick();
        }

        // Área de toque circular y un 15 % más grande que el dibujo (más fácil de acertar con el pulgar)
        static bool Dentro(RectTransform r, Vector2 pantalla)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, pantalla, null, out var p)) return false;
            return p.magnitude <= r.rect.width * 0.5f * 1.15f;
        }

        void SoltarBotones()
        {
            foreach (var b in botones) { if (b.dedos > 0) { b.dedos = 0; b.alSoltar?.Invoke(); } }
            botonDeDedo.Clear();
        }

        void SoltarJoystick()
        {
            dedoJoystick = -1;
            Forja3DInput.JoystickTactil = Vector2.zero;
            if (palanca) palanca.anchoredPosition = Vector2.zero;
            if (fondo) { fondo.anchoredPosition = posReposo; fondo.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f); }
        }

        void OnDisable() { Forja3DInput.JoystickTactil = Vector2.zero; Forja3DInput.CubrirTactil = false; }
    }
}
