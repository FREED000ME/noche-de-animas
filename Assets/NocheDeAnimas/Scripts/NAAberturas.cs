// Noche de Ánimas — puertas y ventanas de las casas: se abren y se cierran con ACCIÓN,
// y se rompen a balazos, con explosiones o a golpes de los muertos.
// Cerradas bloquean el paso (y el NavMesh): los zombies que quedan encerrados las golpean hasta romperlas.
using System.Collections.Generic;
using Forja3D;
using UnityEngine;
using UnityEngine.AI;

namespace NocheDeAnimas
{
    public class NAAbertura : MonoBehaviour
    {
        public static readonly List<NAAbertura> Todas = new List<NAAbertura>();

        public bool ventana, abierta, rota;
        public int vida, vidaMax;
        readonly List<Transform> bisagras = new List<Transform>();
        readonly List<float> angulos = new List<float>();
        BoxCollider bloqueo; NavMeshObstacle obstaculo;
        float sacude, proximoGolpe, sentido = 1f;

        public bool Cerrada => !abierta && !rota;
        public Vector3 Centro => transform.position + Vector3.up * (ventana ? 1.6f : 1.3f);
        public string Nombre => ventana ? "la ventana" : "la puerta";

        public static NAAbertura Crear(Vector3 pos, bool enX, bool ventana, bool abierta, Color marco)
        {
            var g = new GameObject(ventana ? "Ventana" : "Puerta");
            g.transform.position = pos;
            g.transform.rotation = enX ? Quaternion.identity : Quaternion.Euler(0, 90, 0); // X local = a lo largo de la pared
            var a = g.AddComponent<NAAbertura>();
            a.ventana = ventana;
            a.vidaMax = a.vida = ventana ? 4 : 8;
            float ancho = NACasas.Hueco, y0 = ventana ? NACasas.Alfeizar : 0f, y1 = NACasas.AltoHueco;
            var madera = ventana ? NAMat.Hex("#3f6e8c") : NAMat.Hex("#5a3a22");
            if (ventana)
            {
                // Dos contraventanas de tablas, una a cada lado
                foreach (int lado in new[] { -1, 1 })
                {
                    var b = new GameObject("Bisagra").transform; b.SetParent(g.transform, false);
                    b.localPosition = new Vector3(lado * ancho / 2f, 0, 0);
                    float w = ancho / 2f;
                    NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(-lado * w / 2f, (y0 + y1) / 2f, 0), new Vector3(w - 0.04f, y1 - y0 - 0.04f, 0.07f), madera);
                    for (int i = 0; i < 3; i++)
                        NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(-lado * w / 2f, y0 + 0.3f + i * 0.75f, -0.04f), new Vector3(w - 0.1f, 0.08f, 0.03f), madera * 0.75f);
                    a.bisagras.Add(b); a.angulos.Add(lado * 105f);
                }
                // Reja de herrería por dentro (decorativa, sin colisión)
                for (int i = 0; i < 4; i++)
                    NAMat.Pieza(PrimitiveType.Cube, g.transform, new Vector3(-0.6f + i * 0.4f, (y0 + y1) / 2f, 0.12f), new Vector3(0.03f, y1 - y0, 0.03f), NAMat.Hex("#1e1e1e"));
            }
            else
            {
                var b = new GameObject("Bisagra").transform; b.SetParent(g.transform, false);
                b.localPosition = new Vector3(-ancho / 2f, 0, 0);
                NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(ancho / 2f, y1 / 2f, 0), new Vector3(ancho - 0.04f, y1 - 0.04f, 0.09f), madera);
                for (int i = 0; i < 2; i++)
                    NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(ancho / 2f, 0.6f + i * 1.4f, -0.05f), new Vector3(ancho - 0.2f, 0.5f, 0.03f), madera * 0.8f);
                NAMat.Pieza(PrimitiveType.Sphere, b, new Vector3(ancho - 0.22f, 1.05f, -0.08f), Vector3.one * 0.08f, NAMat.Hex("#b08d3c"));
                a.bisagras.Add(b); a.angulos.Add(-100f);
            }
            a.bloqueo = g.AddComponent<BoxCollider>();
            a.bloqueo.center = new Vector3(0, (y0 + y1) / 2f, 0); a.bloqueo.size = new Vector3(ancho, y1 - y0, 0.16f);
            a.obstaculo = g.AddComponent<NavMeshObstacle>();
            a.obstaculo.shape = NavMeshObstacleShape.Box; a.obstaculo.center = new Vector3(0, y1 / 2f, 0); a.obstaculo.size = new Vector3(ancho + 0.2f, y1, 0.5f);
            a.obstaculo.carving = true; a.obstaculo.carveOnlyStationary = false;
            a.abierta = abierta;
            a.Aplicar(true);
            Todas.Add(a);
            return a;
        }

        void OnDestroy() => Todas.Remove(this);

        void Aplicar(bool inmediato)
        {
            bool tapa = Cerrada;
            if (bloqueo) bloqueo.enabled = tapa;
            if (obstaculo) obstaculo.enabled = tapa;
            if (inmediato) for (int i = 0; i < bisagras.Count; i++) if (bisagras[i]) bisagras[i].localRotation = Quaternion.Euler(0, abierta ? angulos[i] * sentido : 0, 0);
        }

        public void Alternar(Vector3 desde)
        {
            if (rota) return;
            abierta = !abierta;
            if (abierta)
            {
                // Se abre hacia el lado contrario a quien la empuja
                var local = transform.InverseTransformPoint(desde);
                sentido = local.z < 0 ? 1f : -1f;
            }
            Aplicar(false);
            NASonido.Sonar("recarga", Centro, 0.6f, ventana ? 0.8f : 0.55f);
        }

        public void Danio(int n, Vector3 desde)
        {
            if (rota || abierta || n <= 0) return;
            vida -= n;
            sacude = 0.25f;
            NASonido.Sonar("golpe", Centro, 0.7f, ventana ? 1.2f : 0.9f);
            Forja3DFX.Chispas(Centro, 4);
            if (vida <= 0) Romper(desde);
        }

        void Romper(Vector3 desde)
        {
            rota = true;
            Aplicar(false);
            NASonido.Sonar("impacto", Centro, 1f, 0.6f);
            Forja3DFX.Polvo(Centro, 0.8f);
            var dir = transform.position - desde; dir.y = 0; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            var madera = ventana ? NAMat.Hex("#3f6e8c") : NAMat.Hex("#5a3a22");
            // Astillas en el suelo
            var restos = new GameObject("Astillas").transform; restos.position = transform.position;
            for (int i = 0; i < 6; i++)
            {
                var p = dir * Random.Range(0.3f, 1.4f) + transform.right * Random.Range(-0.8f, 0.8f) + Vector3.up * 0.05f;
                NAMat.Pieza(PrimitiveType.Cube, restos, p, new Vector3(Random.Range(0.12f, 0.25f), 0.05f, Random.Range(0.5f, 1.1f)), madera * Random.Range(0.7f, 1f), new Vector3(0, Random.Range(0f, 180f), 0));
            }
            foreach (var b in bisagras) if (b) Destroy(b.gameObject);
            bisagras.Clear(); angulos.Clear();
        }

        // Golpe de un zombie que está junto a la puerta o ventana cerrada
        public void Golpe(Vector3 desde)
        {
            if (!Cerrada || Time.time < proximoGolpe) return;
            proximoGolpe = Time.time + 1.1f;
            Danio(1, desde);
        }

        void Update()
        {
            for (int i = 0; i < bisagras.Count; i++)
            {
                if (!bisagras[i]) continue;
                float objetivo = abierta ? angulos[i] * sentido : 0f;
                if (sacude > 0f) objetivo += Mathf.Sin(Time.time * 60f) * 4f;
                bisagras[i].localRotation = Quaternion.Slerp(bisagras[i].localRotation, Quaternion.Euler(0, objetivo, 0), 10f * Time.deltaTime);
            }
            if (sacude > 0f) sacude -= Time.deltaTime;
        }
    }

    // Crea las puertas y ventanas del mapa y gestiona el botón ACCIÓN y los golpes de los zombies
    public class NAAberturas : MonoBehaviour
    {
        public static NAAberturas Inst { get; private set; }
        public string Indicacion { get; private set; } = "";
        bool accionAntes;

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        public void Iniciar(string escena)
        {
            NAAbertura.Todas.Clear();
            var m = NAMapa.Filas; if (m == null) return;
            var rnd = new System.Random(escena.GetHashCode() ^ 77);
            for (int f = 0; f < m.Length; f++)
                for (int c = 0; c < m[f].Length; c++)
                {
                    char ch = m[f][c];
                    if (ch != 'D' && ch != 'V') continue;
                    bool ventana = ch == 'V';
                    bool abierta = rnd.NextDouble() < (ventana ? 0.35 : 0.5);
                    NAAbertura.Crear(NAMapa.Pos(f, c), NACasas.LineaEnX(m, f, c), ventana, abierta, Color.white);
                }
        }

        public static NAAbertura MasCercana(Vector3 pos, float radio, bool soloCerradas)
        {
            NAAbertura mejor = null; float md = radio;
            foreach (var a in NAAbertura.Todas)
            {
                if (!a || a.rota || (soloCerradas && a.abierta)) continue;
                var d = a.transform.position - pos; d.y = 0;
                if (d.magnitude < md) { md = d.magnitude; mejor = a; }
            }
            return mejor;
        }

        // La puerta o ventana cerrada que un zombie debe romper para llegar hasta el jugador
        public static NAAbertura ParaRomper(Vector3 zombie, Vector3 jugador)
        {
            NAAbertura mejor = null; float mp = 30f;
            foreach (var a in NAAbertura.Todas)
            {
                if (!a || !a.Cerrada) continue;
                float d = Vector3.Distance(a.transform.position, zombie);
                if (d > 22f) continue;
                float p = d + Vector3.Distance(a.transform.position, jugador) * 0.5f;
                if (p < mp) { mp = p; mejor = a; }
            }
            return mejor;
        }

        public static void Explosion(Vector3 centro, float radio, int danio)
        {
            foreach (var a in new List<NAAbertura>(NAAbertura.Todas))
                if (a && Vector3.Distance(a.Centro, centro) <= radio + 0.6f) a.Danio(danio * 2, centro);
        }

        void Update()
        {
            Indicacion = "";
            bool accion = Forja3DInput.Accion;
            bool pulso = accion && !accionAntes;
            accionAntes = accion;

            // Golpes de los zombies a las puertas y ventanas cerradas
            foreach (var z in NAZombie.Vivos)
            {
                if (!z || z.Muerto) continue;
                var a = MasCercana(z.transform.position, 1.5f, true);
                if (a) { a.Golpe(z.transform.position); z.MirarHacia(a.transform.position); }
            }

            var j = NAJugador.Transform;
            if (!j || Forja3DGameManager.Bloqueado) return;
            // Las misiones y la mesa de armado usan ACCIÓN primero
            if ((NAMision.Inst && !string.IsNullOrEmpty(NAMision.Inst.Indicacion)) || (NAArmado.Inst && !string.IsNullOrEmpty(NAArmado.Inst.Indicacion))) return;
            var cerca = MasCercana(j.position, 2.3f, false);
            if (!cerca) return;
            Indicacion = $"ACCIÓN: {(cerca.abierta ? "cerrar" : "abrir")} {cerca.Nombre}";
            if (pulso) cerca.Alternar(j.position);
        }
    }

    // La salida del mapa: un portón con arco de cempasúchil, un letrero con el mapa que sigue
    // y una columna de luz que se ve desde lejos (roja con candado, dorada con la llave).
    public class NASalida : MonoBehaviour
    {
        public static NASalida Inst { get; private set; }
        Light luz; Transform hojaI, hojaD, columna; Renderer rColumna; GameObject candado; UnityEngine.UI.Text letrero;
        bool abierta;

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        public static void Construir(string escena)
        {
            var meta = Object.FindFirstObjectByType<Forja3DGoal>();
            if (!meta) return;
            var g = meta.gameObject;
            // Quita el arco de la plantilla (parecía la puerta de una casa más); deja el disparador
            foreach (Transform hijo in g.transform) Object.Destroy(hijo.gameObject);
            var s = g.AddComponent<NASalida>();
            s.Armar(g.transform, NAMapas.Siguiente(escena));
        }

        void Armar(Transform t, string siguiente)
        {
            float a = NAMapa.C * 0.45f;
            var piedra = NAMat.Hex("#b9a58a"); var oscuro = NAMat.Hex("#5a4a3a");
            foreach (int lado in new[] { -1, 1 })
            {
                NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(lado * a, 2.6f, 0), new Vector3(0.7f, 5.2f, 0.7f), piedra);
                NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(lado * a, 5.35f, 0), new Vector3(0.9f, 0.3f, 0.9f), oscuro);
                NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(lado * a, 5.75f, 0), Vector3.one * 0.5f, NAMat.Hex("#ff8c00"), default, 1.2f);
            }
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 5.0f, 0), new Vector3(a * 2f + 0.7f, 0.5f, 0.6f), piedra);
            // Arco de flores de cempasúchil
            for (int i = 0; i <= 14; i++)
            {
                float k = i / 14f; float x = Mathf.Lerp(-a, a, k);
                float y = 5.4f + Mathf.Sin(k * Mathf.PI) * 1.3f;
                NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(x, y, -0.1f), Vector3.one * 0.42f, NAMat.Hex(i % 3 == 0 ? "#ffb000" : "#ff7a00"), default, 0.9f);
            }
            // Portón de dos hojas de herrería
            hojaI = Hoja(t, -1, a); hojaD = Hoja(t, 1, a);
            // Cadena y candado
            candado = new GameObject("Candado");
            candado.transform.SetParent(t, false);
            NAMat.Pieza(PrimitiveType.Cube, candado.transform, new Vector3(0, 1.4f, -0.12f), new Vector3(0.9f, 0.08f, 0.08f), NAMat.Hex("#8a8f96"));
            NAMat.Pieza(PrimitiveType.Cube, candado.transform, new Vector3(0, 1.25f, -0.15f), new Vector3(0.3f, 0.35f, 0.12f), NAMat.Hex("#c9a227"));
            // Columna de luz: se ve desde cualquier parte del mapa
            columna = NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0, 22f, 0.8f), new Vector3(0.35f, 20f, 0.35f), NAMat.Hex("#ff2a1a"), default, 3f);
            rColumna = columna.GetComponent<Renderer>();
            luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(t, false); luz.transform.localPosition = new Vector3(0, 3.5f, -1f);
            luz.type = LightType.Point; luz.range = 12f; luz.intensity = 3f; luz.color = new Color(1f, 0.2f, 0.12f);
            // Letrero
            var lienzo = new GameObject("Letrero", typeof(RectTransform), typeof(Canvas));
            lienzo.transform.SetParent(t, false);
            lienzo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = lienzo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 220); rt.localScale = Vector3.one * 0.01f;
            rt.localPosition = new Vector3(0, 7.6f, -0.2f);
            var fondo = NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 7.6f, 0), new Vector3(6.2f, 2.3f, 0.1f), NAMat.Hex("#2a1a12"));
            fondo.name = "Tabla";
            letrero = Forja3DGameManager.Texto(lienzo.transform, "", 52, TextAnchor.MiddleCenter);
            letrero.supportRichText = true;
            letrero.horizontalOverflow = HorizontalWrapMode.Wrap;
            Actualizar(siguiente);
            this.siguiente = siguiente;
        }

        string siguiente;

        void Actualizar(string sig)
        {
            if (!letrero) return;
            string candadoTxt = abierta ? "<color=#7dff8a>ABIERTA</color>" : "<color=#ff6a5a>CERRADA: BUSCA LA LLAVE</color>";
            letrero.text = $"<b><size=70>SALIDA</size></b>\n→ {sig}\n<size=40>{candadoTxt}</size>";
        }

        static Transform Hoja(Transform t, int lado, float a)
        {
            var b = new GameObject("Hoja").transform; b.SetParent(t, false);
            b.localPosition = new Vector3(lado * (a - 0.35f), 0, 0);
            float w = a - 0.35f;
            var hierro = NAMat.Hex("#1f1f22");
            for (int i = 0; i < 6; i++)
                NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(-lado * (0.15f + i * (w - 0.2f) / 5f), 1.9f, 0), new Vector3(0.06f, 3.8f, 0.06f), hierro);
            foreach (var y in new[] { 0.3f, 1.9f, 3.7f })
                NAMat.Pieza(PrimitiveType.Cube, b, new Vector3(-lado * w / 2f, y, 0), new Vector3(w, 0.08f, 0.08f), hierro);
            return b;
        }

        void Update()
        {
            bool llave = NAMision.Inst && NAMision.Inst.LlaveObtenida;
            if (llave && !abierta)
            {
                abierta = true;
                if (candado) Destroy(candado);
                luz.color = new Color(1f, 0.8f, 0.3f);
                if (rColumna) rColumna.sharedMaterial = NAMat.De(NAMat.Hex("#ffd23f"), 3f);
                Actualizar(siguiente);
            }
            if (abierta)
            {
                if (hojaI) hojaI.localRotation = Quaternion.Slerp(hojaI.localRotation, Quaternion.Euler(0, 95, 0), 2f * Time.deltaTime);
                if (hojaD) hojaD.localRotation = Quaternion.Slerp(hojaD.localRotation, Quaternion.Euler(0, -95, 0), 2f * Time.deltaTime);
            }
            if (columna) columna.localScale = new Vector3(0.35f + Mathf.Sin(Time.time * 3f) * 0.05f, 20f, 0.35f + Mathf.Sin(Time.time * 3f) * 0.05f);
        }
    }
}
