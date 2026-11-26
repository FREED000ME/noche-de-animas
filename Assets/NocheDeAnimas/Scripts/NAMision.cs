// Noche de Ánimas — misión de cada mapa: una cadena de pasos para conseguir la llave de la salida.
//   Recoger  · ir hasta el objeto (manivela, garrafas, cruz, ofrenda…)
//   Usar     · mantener ACCIÓN junto a algo durante unos segundos (cortina, caja fuerte, faroles…)
//   Defender · aguantar dentro de una zona mientras llegan más muertos
//   MatarJefe· aparece el jefe y hay que acabar con él
// Las oleadas no paran nunca: solo se sale del mapa con la llave.
using System.Collections.Generic;
using Forja3D;
using UnityEngine;

namespace NocheDeAnimas
{
    public enum TipoPaso { Recoger, Usar, Defender, MatarJefe }

    public class Paso
    {
        public TipoPaso tipo;
        public string letras = "", texto = "", accion = "", alCompletar = "";
        public string visual = "";   // objeto en cada letra (separados por comas si cambian)
        public string fijo = "";     // objeto que está desde el principio aunque el paso llegue después
        public bool aparece;         // el objeto aparece al empezar el paso
        public float segundos = 3f;
        public int refuerzos;
        public bool daLlave;
    }

    public static class NAMisiones
    {
        public static List<Paso> Para(string escena)
        {
            var l = new List<Paso>();
            if (escena.StartsWith("01"))
            {
                l.Add(new Paso { tipo = TipoPaso.Recoger, letras = "a", visual = "manivela", texto = "Busca la manivela de la bodega", alCompletar = "TIENES LA MANIVELA" });
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "b", visual = "cortina", segundos = 6f, refuerzos = 6, texto = "Sube la cortina de la bodega", accion = "subir la cortina", alCompletar = "DENTRO HAY UNA NOTA: LA COMBINACIÓN ESTÁ EN EL CALENDARIO" });
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "c", visual = "calendario", segundos = 1.5f, texto = "Lee el calendario de la carnicería", accion = "leer el calendario", alCompletar = "COMBINACIÓN: 2 - 11 - 94" });
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "d", visual = "cajafuerte", segundos = 5f, refuerzos = 8, texto = "Abre la caja fuerte de don Chema", accion = "girar la combinación", daLlave = true, alCompletar = "LLAVE DEL PORTÓN" });
            }
            else if (escena.StartsWith("02"))
            {
                l.Add(new Paso { tipo = TipoPaso.Recoger, letras = "a", visual = "garrafa", texto = "Junta las garrafas de gasolina", alCompletar = "TIENES TODA LA GASOLINA" });
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "b", visual = "generador", segundos = 4f, refuerzos = 4, texto = "Llena el generador de la bomba de agua", accion = "echar la gasolina", alCompletar = "EL GENERADOR ARRANCA… ¡HACE MUCHO RUIDO!" });
                l.Add(new Paso { tipo = TipoPaso.Defender, letras = "b", segundos = 30f, refuerzos = 12, texto = "Aguanta junto al generador mientras saca el agua", alCompletar = "EL POZO SE VACIÓ" });
                l.Add(new Paso { tipo = TipoPaso.Recoger, letras = "c", visual = "llave", fijo = "pozo", aparece = true, texto = "Saca la llave de la mina del fondo del pozo", daLlave = true, alCompletar = "LLAVE DE LA MINA" });
            }
            else if (escena.StartsWith("03"))
            {
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "abc", visual = "farolapagado", segundos = 2f, refuerzos = 2, texto = "Enciende los faroles del callejón", accion = "encender el farol", alCompletar = "LOS FAROLES ALUMBRAN EL CAMINO AL PANTEÓN" });
                l.Add(new Paso { tipo = TipoPaso.Recoger, letras = "e", visual = "cruz", texto = "Busca la cruz de plata en el panteón", alCompletar = "TIENES LA CRUZ DE PLATA" });
                l.Add(new Paso { tipo = TipoPaso.Usar, letras = "d", visual = "puerta", segundos = 6f, refuerzos = 8, texto = "Abre la casa del cura con la cruz", accion = "abrir la puerta", daLlave = true, alCompletar = "LLAVE DE LA SACRISTÍA" });
            }
            else
            {
                l.Add(new Paso { tipo = TipoPaso.Recoger, letras = "abc", visual = "pan,vela,foto", texto = "Junta la ofrenda: pan, vela y foto", alCompletar = "LA OFRENDA ESTÁ COMPLETA" });
                l.Add(new Paso { tipo = TipoPaso.Defender, letras = "d", fijo = "altar", segundos = 35f, refuerzos = 14, texto = "Pon la ofrenda en el altar y aguanta", alCompletar = "LA CAMPANA DEJA DE SONAR… ALGO SE ACERCA" });
                l.Add(new Paso { tipo = TipoPaso.MatarJefe, letras = "Z", texto = "Acaba con el Sacristán", daLlave = true, alCompletar = "LLAVE DEL CAMPANARIO" });
            }
            return l;
        }
    }

    public class NAMision : MonoBehaviour
    {
        public static NAMision Inst { get; private set; }

        class Meta { public Vector3 pos; public Vector3 haciaMuro; public GameObject visual, marcador; public string tipo; public bool hecho, reforzado; }

        List<Paso> pasos = new List<Paso>();
        readonly List<Meta> metas = new List<Meta>();
        readonly Dictionary<string, GameObject> fijos = new Dictionary<string, GameObject>();
        int indice;
        float avance, proximoRefuerzo;
        NAZombie jefe;
        Light candado;

        public bool LlaveObtenida { get; private set; }
        public string Indicacion { get; private set; } = "";
        public float Progreso { get; private set; } = -1f;
        public Paso Actual => indice < pasos.Count ? pasos[indice] : null;

        public string Objetivo
        {
            get
            {
                if (LlaveObtenida) return "Ve a la salida: ya tienes la llave";
                var p = Actual; if (p == null) return "";
                int hechos = 0; foreach (var m in metas) if (m.hecho) hechos++;
                return metas.Count > 1 ? $"{p.texto} ({hechos}/{metas.Count})" : p.texto;
            }
        }

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        public void Iniciar(List<Paso> lista)
        {
            pasos = lista;
            Forja3DGameManager.CondicionExtra = () => LlaveObtenida;
            Forja3DGameManager.MotivoExtra = "La salida está cerrada: consigue la llave";
            // Objetos que están en el mapa desde el principio
            foreach (var p in pasos)
            {
                for (int i = 0; i < p.letras.Length; i++)
                    foreach (var pos in Posiciones(p.letras[i]))
                    {
                        if (!string.IsNullOrEmpty(p.fijo) && !fijos.ContainsKey(p.fijo + p.letras[i])) fijos[p.fijo + p.letras[i]] = NAObjetos.Crear(p.fijo, pos.Item1, pos.Item2);
                        string v = Visual(p, i);
                        if (!p.aparece && !string.IsNullOrEmpty(v) && p.tipo != TipoPaso.Defender && !fijos.ContainsKey(v + pos.Item1))
                            fijos[v + pos.Item1] = NAObjetos.Crear(v, pos.Item1, pos.Item2);
                    }
            }
            // Candado de la salida: rojo hasta tener la llave
            var salida = NAMapa.Todas('M');
            if (salida.Count > 0)
            {
                candado = new GameObject("Candado").AddComponent<Light>();
                candado.transform.position = salida[0] + Vector3.up * 3.2f;
                candado.type = LightType.Point; candado.range = 7f; candado.intensity = 2.5f; candado.color = new Color(1f, 0.15f, 0.1f);
            }
            Activar();
        }

        static string Visual(Paso p, int i)
        {
            if (string.IsNullOrEmpty(p.visual)) return "";
            var partes = p.visual.Split(',');
            return partes[Mathf.Min(i, partes.Length - 1)];
        }

        // Posición de cada letra en el mapa y hacia dónde está el muro más cercano
        static List<(Vector3, Vector3)> Posiciones(char letra)
        {
            var l = new List<(Vector3, Vector3)>();
            var filas = NAMapa.Filas; if (filas == null) return l;
            for (int f = 0; f < filas.Length; f++)
                for (int c = 0; c < filas[f].Length; c++)
                    if (filas[f][c] == letra) l.Add((NAMapa.Pos(f, c), NAMapa.HaciaMuro(f, c)));
            return l;
        }

        void Activar()
        {
            foreach (var m in metas) if (m.marcador) Destroy(m.marcador);
            metas.Clear();
            avance = 0f;
            var p = Actual;
            if (p == null) return;
            for (int i = 0; i < p.letras.Length; i++)
                foreach (var pos in Posiciones(p.letras[i]))
                {
                    var m = new Meta { pos = pos.Item1, haciaMuro = pos.Item2, tipo = Visual(p, i) };
                    if (!string.IsNullOrEmpty(m.tipo)) fijos.TryGetValue(m.tipo + m.pos, out m.visual);
                    if (p.aparece && !string.IsNullOrEmpty(m.tipo)) m.visual = NAObjetos.Crear(m.tipo, m.pos, m.haciaMuro);
                    if (p.tipo != TipoPaso.MatarJefe) m.marcador = NAObjetos.Marcador(m.pos);
                    metas.Add(m);
                }
            if (p.tipo == TipoPaso.MatarJefe && metas.Count > 0)
            {
                jefe = NAZombie.Crear(TipoZombie.Sacristan, metas[0].pos, true);
                Forja3DFX.Temblor(0.5f, 1.2f);
                NASonido.Sonar("campana", metas[0].pos, 1f, 0.8f);
            }
            Forja3DGameManager.Mensaje(p.texto.ToUpperInvariant());
        }

        void Update()
        {
            Indicacion = ""; Progreso = -1f;
            var p = Actual;
            var j = NAJugador.Transform;
            if (p == null || !j || Forja3DGameManager.Bloqueado || LlaveObtenida) return;
            bool accion = Forja3DInput.Accion;

            switch (p.tipo)
            {
                case TipoPaso.Recoger:
                    foreach (var m in metas)
                    {
                        if (m.hecho || Distancia(j, m.pos) > 1.9f) continue;
                        m.hecho = true;
                        if (m.visual) Destroy(m.visual);
                        if (m.marcador) Destroy(m.marcador);
                        NASonido.Sonar("recoger", m.pos, 0.9f);
                        Forja3DGameManager.Mensaje(Objetivo.ToUpperInvariant());
                    }
                    break;

                case TipoPaso.Usar:
                    foreach (var m in metas)
                    {
                        if (m.hecho || Distancia(j, m.pos) > 2.8f) continue;
                        Indicacion = $"Mantén ACCIÓN: {p.accion}";
                        if (!accion) break;
                        if (!m.reforzado && p.refuerzos > 0) { m.reforzado = true; NAOleadas.Refuerzos(p.refuerzos, TipoZombie.Caminante); }
                        avance += Time.deltaTime;
                        Progreso = Mathf.Clamp01(avance / p.segundos);
                        if (avance >= p.segundos)
                        {
                            m.hecho = true; avance = 0f;
                            if (m.marcador) Destroy(m.marcador);
                            NAObjetos.Activar(m.visual, m.tipo);
                            NASonido.Sonar("construir", m.pos, 0.8f);
                        }
                        break;
                    }
                    break;

                case TipoPaso.Defender:
                    if (metas.Count == 0) break;
                    var d = metas[0];
                    if (Distancia(j, d.pos) <= 6.5f)
                    {
                        avance += Time.deltaTime;
                        Progreso = Mathf.Clamp01(avance / p.segundos);
                        Indicacion = $"¡Aguanta aquí! {Mathf.CeilToInt(p.segundos - avance)} s";
                        float cada = p.segundos / Mathf.Max(1, p.refuerzos);
                        if (Time.time >= proximoRefuerzo)
                        {
                            proximoRefuerzo = Time.time + cada;
                            NAOleadas.Refuerzos(1, Random.value < 0.3f ? TipoZombie.Corredor : TipoZombie.Caminante);
                        }
                        if (avance >= p.segundos) { d.hecho = true; if (d.marcador) Destroy(d.marcador); }
                    }
                    else Indicacion = "¡Vuelve a la zona! El tiempo se detiene";
                    break;

                case TipoPaso.MatarJefe:
                    if (jefe && jefe.Muerto) foreach (var m in metas) m.hecho = true;
                    if (!jefe) foreach (var m in metas) m.hecho = true;
                    break;
            }

            bool todo = metas.Count > 0;
            foreach (var m in metas) if (!m.hecho) { todo = false; break; }
            if (todo) Completar(p);
        }

        void Completar(Paso p)
        {
            if (!string.IsNullOrEmpty(p.alCompletar)) Forja3DGameManager.Mensaje(p.alCompletar);
            NAPuntos.Sumar(250);
            indice++;
            if (p.daLlave || indice >= pasos.Count)
            {
                LlaveObtenida = true;
                foreach (var m in metas) if (m.marcador) Destroy(m.marcador);
                metas.Clear();
                NASonido.Sonar("llave", null, 1f);
                if (candado) candado.color = new Color(0.2f, 1f, 0.35f);
                var salida = NAMapa.Todas('M');
                if (salida.Count > 0) NAObjetos.Marcador(salida[0]);
                return;
            }
            Activar();
        }

        static float Distancia(Transform j, Vector3 p) { var d = j.position - p; d.y = 0; return d.magnitude; }
    }

    // ───────── Objetos de las misiones (piezas simples) ─────────
    public static class NAObjetos
    {
        public static GameObject Crear(string tipo, Vector3 pos, Vector3 haciaMuro)
        {
            var g = new GameObject("Obj_" + tipo);
            g.transform.position = pos;
            if (haciaMuro.sqrMagnitude > 0.01f) g.transform.rotation = Quaternion.LookRotation(haciaMuro);
            var t = g.transform;
            float pared = NAMapa.C / 2f - 0.15f; // distancia del centro de la celda al muro
            switch (tipo)
            {
                case "manivela":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.15f, 0), new Vector3(0.06f, 0.06f, 0.7f), NAMat.Hex("#8a8f96"));
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.15f, 0.35f), new Vector3(0.3f, 0.06f, 0.06f), NAMat.Hex("#8a8f96"));
                    Brillo(g, new Color(1f, 0.6f, 0.2f));
                    break;
                case "cortina":
                    var cortina = new GameObject("Cortina").transform; cortina.SetParent(t, false); cortina.localPosition = new Vector3(0, 0, pared);
                    for (int i = 0; i < 9; i++) NAMat.Pieza(PrimitiveType.Cube, cortina, new Vector3(0, 0.2f + i * 0.32f, 0), new Vector3(2.6f, 0.28f, 0.08f), NAMat.Hex(i % 2 == 0 ? "#9aa3ab" : "#7d858c"));
                    break;
                case "calendario":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.6f, pared), new Vector3(0.7f, 0.9f, 0.04f), NAMat.Hex("#f4efe1"));
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.92f, pared - 0.01f), new Vector3(0.7f, 0.22f, 0.04f), NAMat.Hex("#d62828"));
                    Brillo(g, new Color(1f, 0.9f, 0.7f));
                    break;
                case "cajafuerte":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.55f, 0), new Vector3(0.9f, 1.1f, 0.8f), NAMat.Hex("#2d3436"));
                    var puertaC = NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.55f, -0.41f), new Vector3(0.78f, 0.95f, 0.04f), NAMat.Hex("#3d4648"));
                    puertaC.name = "Puerta";
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0.15f, 0.6f, -0.44f), new Vector3(0.16f, 0.02f, 0.16f), NAMat.Hex("#c0c0c0"), new Vector3(90, 0, 0));
                    break;
                case "garrafa":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.55f, 0.22f), NAMat.Hex("#d62828"));
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0.1f, 0.62f, 0), new Vector3(0.08f, 0.06f, 0.08f), NAMat.Hex("#222222"));
                    Brillo(g, new Color(1f, 0.4f, 0.3f));
                    break;
                case "generador":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(1.3f, 0.9f, 0.8f), NAMat.Hex("#f2b705"));
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(-0.3f, 1.0f, 0), new Vector3(0.35f, 0.15f, 0.35f), NAMat.Hex("#333333"));
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0.45f, 1.1f, 0.2f), new Vector3(0.08f, 0.3f, 0.08f), NAMat.Hex("#555555"));
                    Solido(g, new Vector3(1.3f, 1.1f, 0.8f));
                    break;
                case "pozo":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(a) * 0.9f, 0.4f, Mathf.Sin(a) * 0.9f), new Vector3(0.55f, 0.8f, 0.3f), NAMat.Hex("#8d8a84"), new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                    }
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0, 0.1f, 0), new Vector3(1.5f, 0.05f, 1.5f), NAMat.Hex("#1b2a33"));
                    break;
                case "llave":
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0, 0.9f, 0), new Vector3(0.25f, 0.03f, 0.25f), NAMat.Hex("#ffd23f"), new Vector3(90, 0, 0), 1.5f);
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.6f, 0), new Vector3(0.05f, 0.45f, 0.05f), NAMat.Hex("#ffd23f"), default, 1.5f);
                    Brillo(g, new Color(1f, 0.85f, 0.3f));
                    break;
                case "farolapagado":
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0, 1.7f, 0), new Vector3(0.12f, 1.7f, 0.12f), NAMat.Hex("#26262a"));
                    var lampara = NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 3.55f, 0), new Vector3(0.4f, 0.5f, 0.4f), NAMat.Hex("#3a3328"));
                    lampara.name = "Lampara";
                    break;
                case "cruz":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.8f, 0), new Vector3(0.08f, 0.6f, 0.05f), NAMat.Hex("#dfe6e9"), default, 1f);
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.92f, 0), new Vector3(0.34f, 0.08f, 0.05f), NAMat.Hex("#dfe6e9"), default, 1f);
                    Brillo(g, new Color(0.8f, 0.9f, 1f));
                    break;
                case "puerta":
                    var bisagra = new GameObject("Bisagra").transform; bisagra.SetParent(t, false); bisagra.localPosition = new Vector3(-0.6f, 0, pared);
                    NAMat.Pieza(PrimitiveType.Cube, bisagra, new Vector3(0.6f, 1.1f, 0), new Vector3(1.2f, 2.2f, 0.1f), NAMat.Hex("#5a3a22"));
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 2.4f, pared), new Vector3(1.5f, 0.2f, 0.15f), NAMat.Hex("#3a2416"));
                    break;
                case "pan":
                    NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 0.3f, 0.5f), NAMat.Hex("#c98a3e"));
                    NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(0, 0.42f, 0), Vector3.one * 0.14f, NAMat.Hex("#a8692a"));
                    Brillo(g, new Color(1f, 0.7f, 0.3f));
                    break;
                case "vela":
                    NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.3f, 0.12f), NAMat.Hex("#f4efe1"));
                    NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(0, 0.66f, 0), new Vector3(0.07f, 0.12f, 0.07f), NAMat.Hex("#ffb347"), default, 3f);
                    Brillo(g, new Color(1f, 0.7f, 0.3f));
                    break;
                case "foto":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.5f, 0.05f), NAMat.Hex("#6b4226"), new Vector3(-15, 0, 0));
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.31f, -0.03f), new Vector3(0.3f, 0.38f, 0.02f), NAMat.Hex("#d8cfb8"), new Vector3(-15, 0, 0));
                    Brillo(g, new Color(1f, 0.9f, 0.7f));
                    break;
                case "altar":
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1.0f, 1.1f), NAMat.Hex("#7b2cbf"));
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.2f, 0.3f), new Vector3(1.8f, 0.4f, 0.5f), NAMat.Hex("#9b5de5"));
                    for (int i = 0; i < 9; i++) NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(-1f + i * 0.25f, 1.05f, -0.35f), Vector3.one * 0.2f, NAMat.Hex("#ff8c00"), default, 0.6f);
                    for (int i = 0; i < 4; i++) NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3(-0.75f + i * 0.5f, 1.55f, 0.3f), new Vector3(0.06f, 0.1f, 0.06f), NAMat.Hex("#ffb347"), default, 3f);
                    var la = new GameObject("Luz").AddComponent<Light>(); la.transform.SetParent(t, false); la.transform.localPosition = Vector3.up * 2f;
                    la.type = LightType.Point; la.color = new Color(1f, 0.55f, 0.25f); la.range = 9f; la.intensity = 2f;
                    Solido(g, new Vector3(2.2f, 1.4f, 1.1f));
                    break;
            }
            return g;
        }

        static void Brillo(GameObject g, Color c)
        {
            var l = new GameObject("Brillo").AddComponent<Light>();
            l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 0.8f;
            l.type = LightType.Point; l.color = c; l.range = 3.5f; l.intensity = 1.6f;
        }

        static void Solido(GameObject g, Vector3 tam)
        {
            var col = g.AddComponent<BoxCollider>(); col.size = tam; col.center = Vector3.up * tam.y / 2f;
            var obs = g.AddComponent<UnityEngine.AI.NavMeshObstacle>(); obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obs.size = tam; obs.center = Vector3.up * tam.y / 2f; obs.carving = true;
        }

        // Al completar un "Usar": la cortina sube, la caja fuerte se abre, el farol se enciende…
        public static void Activar(GameObject g, string tipo)
        {
            if (!g) return;
            switch (tipo)
            {
                case "cortina": { var c = g.transform.Find("Cortina"); if (c) c.localPosition += Vector3.up * 2.7f; break; }
                case "cajafuerte": { var p = g.transform.Find("Puerta"); if (p) { p.localPosition += new Vector3(-0.35f, 0, -0.35f); p.localRotation = Quaternion.Euler(0, 80, 0); } break; }
                case "puerta": { var b = g.transform.Find("Bisagra"); if (b) b.localRotation = Quaternion.Euler(0, -95, 0); break; }
                case "farolapagado":
                    {
                        var lamp = g.transform.Find("Lampara");
                        if (lamp) lamp.GetComponent<Renderer>().sharedMaterial = NAMat.De(NAMat.Hex("#ffcf7a"), 2.5f);
                        var l = new GameObject("Luz").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 3.3f;
                        l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.range = 12f; l.intensity = 2f;
                        break;
                    }
                case "generador":
                    {
                        var l = new GameObject("Luz").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 1.6f;
                        l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.4f); l.range = 8f; l.intensity = 2f;
                        Forja3DFX.Polvo(g.transform.position, 1.2f);
                        break;
                    }
            }
        }

        // Rombo flotante que señala el siguiente objetivo
        public static GameObject Marcador(Vector3 pos)
        {
            var g = new GameObject("Marcador");
            g.transform.position = pos + Vector3.up * 2.8f;
            NAMat.Pieza(PrimitiveType.Cube, g.transform, Vector3.zero, Vector3.one * 0.32f, NAMat.Hex("#ff9f1c"), new Vector3(45, 0, 45), 3f);
            g.AddComponent<NAGirar>();
            return g;
        }
    }

    public class NAGirar : MonoBehaviour
    {
        Vector3 inicio;
        void Start() => inicio = transform.position;
        void Update()
        {
            transform.rotation = Quaternion.Euler(0, Time.time * 90f, 0);
            transform.position = inicio + Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.2f;
        }
    }
}
