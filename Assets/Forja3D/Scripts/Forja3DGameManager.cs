// Forja3D — reglas del nivel, HUD, victoria/derrota y paso al siguiente nivel.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Forja3D
{
    public enum Objetivo { Meta, Enemigos, Monedas, Jefe, Dialogos }

    public class Forja3DGameManager : MonoBehaviour
    {
        public string titulo = "Nivel";
        public Objetivo objetivo = Objetivo.Meta;
        public Linea[] intro;   // narración/diálogo al empezar el nivel
        public Linea[] salida;  // al completar el nivel, antes de pasar al siguiente
        public Linea[] recompensaDialogo; // al vencer al último jefe, si el nivel da un arma nueva
        public string capitulo;     // encabezado del pergamino inicial ("Canto I")
        public Linea[] narracion;   // pergamino inicial (si falta: las líneas del narrador de la intro)
        [TextArea] public string transicion; // pergamino final: hacia dónde va Dante

        public static bool Terminado { get; private set; }
        // Bloquea al jugador y a los enemigos (fin del nivel o diálogo abierto)
        public static bool Bloqueado => Terminado || Forja3DDialogue.Activo || Forja3DPergamino.Activo;
        static Forja3DGameManager inst;
        static int enemigos, derrotados, monedas, recogidas, jefes, jefesDerrotados, claves, clavesOidas;
        static System.Collections.Generic.List<Forja3DHealth> vidasJefe = new System.Collections.Generic.List<Forja3DHealth>();
        static System.Collections.Generic.List<string> nombresJefe = new System.Collections.Generic.List<string>();

        Forja3DHealth jugador;
        Text hud;
        GameObject panelFinal;
        Text textoFinal, textoBoton;
        float mensajeHasta;
        Text textoAviso, textoJefe;
        Image barraVida, barraVidaRetardo, barraEstamina, barraJefe, barraJefeRetardo, vineta;
        GameObject marcoJefe;
        float vinetaAlfa, avisoHasta;
        Forja3DPlayerController controlJugador;

        public static void RegistrarEnemigo() => enemigos++;
        public static void EnemigoDerrotado() { derrotados++; inst?.Revisar(); }
        public static void RegistrarJefe(Forja3DHealth h, string nombre = "JEFE") { jefes++; vidasJefe.Add(h); nombresJefe.Add(nombre); }
        // Mensaje grande en el centro (PARRY, GOLPE DE GRACIA...)
        public static void Mensaje(string m) { if (inst && inst.textoAviso) { inst.textoAviso.text = m; inst.avisoHasta = Time.unscaledTime + 1.1f; } }
        // Destello rojo en los bordes al recibir daño
        public static void Herido() { if (inst) inst.vinetaAlfa = 0.55f; }
        public static void JefeDerrotado()
        {
            jefesDerrotados++; inst?.Aviso("¡Jefe derrotado!"); inst?.Revisar();
            // Último jefe caído: si el nivel da un arma (la hoz de la Muerte), Dante la toma
            if (inst && jefesDerrotados >= jefes && inst.controlJugador && inst.controlJugador.TieneRecompensa)
                inst.StartCoroutine(inst.Recompensa());
        }
        System.Collections.IEnumerator Recompensa()
        {
            yield return new WaitForSecondsRealtime(2.2f);
            if (Terminado) yield break;
            controlJugador.CambiarARecompensa();
            Forja3DDialogue.Mostrar(recompensaDialogo);
        }
        // Personajes "clave" (Francesca, Paolo…): hay que escucharlos para abrir la meta
        public static void RegistrarClave() => claves++;
        public static void ClaveOida() { clavesOidas++; inst?.Revisar(); }
        public static void RegistrarMoneda() => monedas++;
        public static void MonedaRecogida() { recogidas++; inst?.Revisar(); }
        public static void MetaAlcanzada()
        {
            if (inst == null || Terminado) return;
            if (inst.objetivo == Objetivo.Meta || inst.Cumplido()) inst.Ganar();
            else inst.Aviso(inst.objetivo == Objetivo.Enemigos ? "¡Derrota a todos los enemigos primero!" : inst.objetivo == Objetivo.Jefe ? "¡Derrota al jefe primero!" : inst.objetivo == Objetivo.Dialogos ? "La puerta no se abre… escucha antes a las almas de este círculo" : "¡Recoge todas las monedas primero!");
        }

        void Awake()
        {
            // EXPANSIÓN FUTURA: en PC conviene un menú de pausa (Escape), opciones de gráficos y resolución;
            // en iOS/Android, pausar al salir de la app (OnApplicationPause). Hoy el juego no tiene menú.
            inst = this; Terminado = false;
            enemigos = derrotados = monedas = recogidas = jefes = jefesDerrotados = claves = clavesOidas = 0; vidasJefe.Clear(); nombresJefe.Clear();
            Time.timeScale = 1f;
            Application.targetFrameRate = 60; // PC: dejar -1 y usar QualitySettings.vSyncCount = 1
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            CrearUI();
        }

        void Start()
        {
            Forja3DAudio.Asegurar(); // después de todos los Awake: no duplica el objeto de audio del nivel
            var p = GameObject.FindWithTag("Player");
            if (p) { jugador = p.GetComponent<Forja3DHealth>(); controlJugador = p.GetComponent<Forja3DPlayerController>(); if (jugador) jugador.AlMorir += Perder; }
            PlayerPrefs.SetInt(Forja3DTitle.ClaveProgreso, Mathf.Max(1, SceneManager.GetActiveScene().buildIndex));
            Forja3DProgreso.NuevoNivel();
            Forja3DProgreso.AlGanar += XPGanada;
            // Narración en el pergamino; los personajes hablan después en la caja de diálogo
            var narr = new System.Collections.Generic.List<string>(); var resto = new System.Collections.Generic.List<Linea>();
            if (narracion != null && narracion.Length > 0) { foreach (var l in narracion) narr.Add(l.texto); if (intro != null) resto.AddRange(intro); }
            else if (intro != null) foreach (var l in intro) { if (string.IsNullOrEmpty(l.quien)) narr.Add(l.texto.Replace(" / ", "\n")); else resto.Add(l); }
            System.Action seguir = () => Forja3DDialogue.Mostrar(resto, () => Aviso(titulo + ObjetivoTexto()));
            if (narr.Count > 0) Forja3DPergamino.Mostrar(string.IsNullOrEmpty(capitulo) ? titulo : capitulo + "\n<size=46>" + titulo + "</size>", string.Join("\n\n", narr), "— toca para comenzar —", seguir);
            else seguir();
        }

        void XPGanada(int cantidad, string motivo)
        {
            if (!textoXP) return;
            xpFlotante = $"+{cantidad} XP"; xpFlotanteHasta = Time.unscaledTime + 1.2f;
        }
        Text textoXP; string xpFlotante; float xpFlotanteHasta;

        void OnDestroy() { if (inst == this) inst = null; Forja3DProgreso.AlGanar -= XPGanada; }

        string ObjetivoTexto() => objetivo switch
        {
            Objetivo.Enemigos => "\nObjetivo: derrota a todos los enemigos y llega a la meta",
            Objetivo.Monedas => "\nObjetivo: recoge todas las monedas y llega a la meta",
            Objetivo.Jefe => "\nObjetivo: derrota al jefe y llega a la meta",
            Objetivo.Dialogos => "\nObjetivo: escucha a las almas y cruza la puerta",
            _ => "\nObjetivo: llega a la meta verde",
        };

        bool Cumplido() => objetivo switch
        {
            Objetivo.Enemigos => derrotados >= enemigos,
            Objetivo.Monedas => recogidas >= monedas,
            Objetivo.Jefe => jefesDerrotados >= jefes,
            Objetivo.Dialogos => clavesOidas >= claves,
            _ => true,
        };

        void Revisar() { if (Cumplido() && objetivo != Objetivo.Meta) Aviso("¡Objetivo cumplido! Ve a la meta"); }

        void Update()
        {
            if (!hud) return;
            var t = "";
            if (enemigos > 0) t += $"ENEMIGOS {derrotados}/{enemigos}    ";
            if (monedas > 0) t += $"MONEDAS {recogidas}/{monedas}";
            if (Time.time < mensajeHasta) t += "\n" + mensaje;
            hud.text = t;

            // Barras: vida (con "daño retardado" blanco), estamina y jefe
            float dt = Time.unscaledDeltaTime;
            if (jugador) Llenar(barraVida, barraVidaRetardo, jugador.Vida / (float)Mathf.Max(1, jugador.vidaMaxima), dt);
            if (controlJugador) barraEstamina.fillAmount = Mathf.MoveTowards(barraEstamina.fillAmount, controlJugador.Estamina / controlJugador.estaminaMaxima, dt * 3f);
            int iJefe = -1;
            for (int i = 0; i < vidasJefe.Count; i++) if (vidasJefe[i] && !vidasJefe[i].Muerto && Vector3.Distance(vidasJefe[i].transform.position, jugador ? jugador.transform.position : Vector3.zero) < 22f) { iJefe = i; break; }
            marcoJefe.SetActive(iJefe >= 0);
            if (iJefe >= 0)
            {
                textoJefe.text = nombresJefe[iJefe].ToUpperInvariant();
                Llenar(barraJefe, barraJefeRetardo, vidasJefe[iJefe].Vida / (float)Mathf.Max(1, vidasJefe[iJefe].vidaMaxima), dt);
            }
            vinetaAlfa = Mathf.MoveTowards(vinetaAlfa, jugador && jugador.Vida <= 1 && !jugador.Muerto ? 0.25f + 0.1f * Mathf.Sin(Time.time * 4f) : 0f, dt * 0.8f);
            vineta.color = new Color(0.6f, 0f, 0f, vinetaAlfa);
            var a = textoAviso.color; a.a = Mathf.Clamp01((avisoHasta - Time.unscaledTime) * 3f); textoAviso.color = a;
            if (textoXP)
            {
                int d = Forja3DProgreso.Disponibles;
                textoXP.text = $"XP {Forja3DProgreso.XP}" + (d > 0 ? $"   ·   {d} punto{(d > 1 ? "s" : "")} de virtud" : "") + (Time.unscaledTime < xpFlotanteHasta ? $"   <color=#ffd27a>{xpFlotante}</color>" : "");
            }
        }

        static void Llenar(Image barra, Image retardo, float valor, float dt)
        {
            barra.fillAmount = Mathf.MoveTowards(barra.fillAmount, valor, dt * 4f);
            retardo.fillAmount = retardo.fillAmount < barra.fillAmount ? barra.fillAmount : Mathf.MoveTowards(retardo.fillAmount, barra.fillAmount, dt * 0.6f);
        }

        string mensaje;
        void Aviso(string m) { mensaje = m; mensajeHasta = Time.time + 4f; }

        void Ganar()
        {
            Terminado = true;
            Forja3DDialogue.Mostrar(salida, PanelVictoria);
        }

        // Fin del nivel: un pergamino cuenta hacia dónde sigue el viaje (en vez de un botón "siguiente")
        void PanelVictoria()
        {
            bool hayMas = SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings;
            if (hayMas) PlayerPrefs.SetInt(Forja3DTitle.ClaveProgreso, SceneManager.GetActiveScene().buildIndex + 1);
            Forja3DProgreso.Ganar(50, "nivel");
            PlayerPrefs.Save();
            Forja3DAudio.Sonar("victoria", null, 0.6f, 0f);
            string texto = !string.IsNullOrEmpty(transicion) ? transicion : hayMas ? "Y el camino siguió, hacia abajo, siempre hacia abajo." : "Aquí termina esta parte del viaje.";
            string extra = $"Experiencia ganada en este canto: {Forja3DProgreso.XPEnEsteNivel}";
            Forja3DPergamino.Mostrar(hayMas ? "Y siguieron su camino" : "Fin de esta parte del viaje", texto,
                hayMas ? "— toca para seguir el viaje —" : "— toca para volver al inicio —",
                () => SceneManager.LoadScene(hayMas ? SceneManager.GetActiveScene().buildIndex + 1 : 0), extra, true);
        }

        void Perder()
        {
            Terminado = true;
            Forja3DAudio.Sonar("derrota", null, 0.8f, 0f);
            StartCoroutine(PergaminoDerrota());
        }
        System.Collections.IEnumerator PergaminoDerrota()
        {
            yield return new WaitForSecondsRealtime(1.6f);
            Forja3DPergamino.Mostrar("Has caído", "Y caí como cae un cuerpo muerto.\n\nPero aún no es tu hora, Dante: el camino te espera donde lo dejaste.",
                "— toca para levantarte —", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex), null, true);
        }

        void MostrarFinal(string titulo, string boton, UnityEngine.Events.UnityAction accion)
        {
            panelFinal.SetActive(true);
            textoFinal.text = titulo; textoBoton.text = boton;
            var b = panelFinal.GetComponentInChildren<Button>();
            b.onClick.RemoveAllListeners(); b.onClick.AddListener(accion);
        }

        // ---------- UI por código ----------
        void CrearUI()
        {
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = canvasGo.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; esc.referenceResolution = new Vector2(1920, 1080); esc.matchWidthOrHeight = 0.5f;

            // Viñeta roja (daño)
            vineta = new GameObject("Vineta", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            vineta.transform.SetParent(canvasGo.transform, false); vineta.raycastTarget = false;
            var rv = vineta.rectTransform; rv.anchorMin = Vector2.zero; rv.anchorMax = Vector2.one; rv.offsetMin = rv.offsetMax = Vector2.zero;
            vineta.sprite = SpriteVineta(); vineta.color = new Color(0.6f, 0, 0, 0);

            hud = Texto(canvasGo.transform, "", 34, TextAnchor.UpperLeft);
            var rh = hud.rectTransform; rh.offsetMin = new Vector2(48, 40); rh.offsetMax = new Vector2(-40, -165);

            // Barras del jugador (arriba a la izquierda)
            (barraVida, barraVidaRetardo) = Barra(canvasGo.transform, new Vector2(0, 1), new Vector2(48, -48), new Vector2(560, 30), new Color(0.75f, 0.08f, 0.06f));
            (barraEstamina, _) = Barra(canvasGo.transform, new Vector2(0, 1), new Vector2(48, -90), new Vector2(440, 16), new Color(0.85f, 0.72f, 0.3f));
            textoXP = Texto(canvasGo.transform, "", 30, TextAnchor.UpperLeft); textoXP.supportRichText = true;
            textoXP.rectTransform.offsetMin = new Vector2(50, 0); textoXP.rectTransform.offsetMax = new Vector2(-40, -118);
            textoXP.color = new Color(0.92f, 0.85f, 0.7f);
            // Barra del jefe (abajo al centro)
            marcoJefe = new GameObject("Jefe", typeof(RectTransform)); marcoJefe.transform.SetParent(canvasGo.transform, false);
            var rj = marcoJefe.GetComponent<RectTransform>(); rj.anchorMin = rj.anchorMax = new Vector2(0.5f, 0); rj.sizeDelta = new Vector2(1000, 90); rj.anchoredPosition = new Vector2(0, 60);
            (barraJefe, barraJefeRetardo) = Barra(marcoJefe.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(1000, 24), new Color(0.55f, 0.05f, 0.08f));
            textoJefe = Texto(marcoJefe.transform, "", 34, TextAnchor.UpperLeft);
            textoJefe.color = new Color(0.93f, 0.86f, 0.7f);
            marcoJefe.SetActive(false);
            // Mensaje de combate
            textoAviso = Texto(canvasGo.transform, "", 76, TextAnchor.MiddleCenter);
            textoAviso.rectTransform.offsetMin = new Vector2(0, 300); textoAviso.color = new Color(1f, 0.88f, 0.55f, 0);

            Forja3DTouchControls.Crear(canvasGo.transform);

            panelFinal = new GameObject("PanelFinal", typeof(RectTransform), typeof(Image));
            panelFinal.transform.SetParent(canvasGo.transform, false);
            var rp = panelFinal.GetComponent<RectTransform>(); rp.anchorMin = Vector2.zero; rp.anchorMax = Vector2.one; rp.offsetMin = rp.offsetMax = Vector2.zero;
            panelFinal.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            textoFinal = Texto(panelFinal.transform, "", 90, TextAnchor.MiddleCenter);
            textoFinal.rectTransform.offsetMin = new Vector2(0, 150);

            var bGo = new GameObject("Boton", typeof(RectTransform), typeof(Image), typeof(Button));
            bGo.transform.SetParent(panelFinal.transform, false);
            var rb = bGo.GetComponent<RectTransform>(); rb.anchorMin = rb.anchorMax = new Vector2(0.5f, 0.35f); rb.sizeDelta = new Vector2(520, 140);
            bGo.GetComponent<Image>().color = new Color(0.2f, 0.75f, 0.35f);
            textoBoton = Texto(bGo.transform, "", 50, TextAnchor.MiddleCenter);
            panelFinal.SetActive(false);
        }

        // Barra con fondo, capa de "daño retardado" y relleno
        static (Image relleno, Image retardo) Barra(Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam, Color color)
        {
            Image Capa(string n, Transform p, Color c, bool fill)
            {
                var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
                var r = go.GetComponent<RectTransform>();
                if (p == padre) { r.anchorMin = r.anchorMax = ancla; r.pivot = ancla; r.anchoredPosition = pos; r.sizeDelta = tam; }
                else { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3); }
                var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
                if (fill) { img.sprite = Blanco(); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillAmount = 1f; }
                return img;
            }
            var fondo = Capa("Barra", padre, new Color(0, 0, 0, 0.6f), false);
            var ret = Capa("Retardo", fondo.transform, new Color(1f, 0.95f, 0.85f, 0.8f), true);
            var rel = Capa("Relleno", fondo.transform, color, true);
            return (rel, ret);
        }

        static Sprite blanco;
        static Sprite Blanco()
        {
            if (blanco) return blanco;
            var t = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; t.SetPixels(px); t.Apply();
            return blanco = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        static Sprite SpriteVineta()
        {
            const int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, (d - 0.55f) / 0.6f)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        public static Text Texto(Transform padre, string contenido, int tam, TextAnchor anc)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = contenido; t.fontSize = tam; t.alignment = anc; t.color = Color.white; t.raycastTarget = false;
            var sombra = go.AddComponent<Shadow>(); sombra.effectDistance = new Vector2(2, -2);
            return t;
        }
    }
}
