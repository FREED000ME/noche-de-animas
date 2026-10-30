// Forja3D — construye escenas jugables a partir de los niveles JSON de Assets/Forja3D/Niveles
// (los escribe el conector con la herramienta crear_nivel). Cada carácter del "mapa" es una celda.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Forja3D.EditorTools
{
    [Serializable] public class Simbolo { public string simbolo; public string tipo; public string modelo; public int vida = 2; public float escala = 1f; public string color; public string nombre; public bool seguir; public Linea[] dialogo; public string voz; public string aura; public bool fantasma; public bool clave; public bool desvanecer; public string arma; }
    [Serializable] public class JugadorDef { public string modelo; public int vida = 5; public string arma; }
    [Serializable]
    public class JuegoDef
    {
        public string titulo; public string subtitulo; public string cita; public string cita_traduccion;
        public string color_fondo = "#0d0505"; public string color_titulo = "#ebbf6b";
    }
    [Serializable] public class TerrenoDef { public string tipo = "pasto"; public float colinas = 0.5f; public int semilla = 1; }
    [Serializable]
    public class Nivel
    {
        public string titulo = "Nivel";
        public float celda = 2f;
        public string[] mapa;
        public Simbolo[] leyenda;
        public JugadorDef jugador = new JugadorDef();
        public TerrenoDef terreno = new TerrenoDef();
        public string objetivo = "meta";
        public string cielo = "#8ec9f0";
        public bool niebla;
        public float altura_muros = 3f;
        public string color_muros = "#8c8478";
        public string ambiente = "infierno";
        public Linea[] intro;
        public Linea[] salida;
        public string recompensa_arma;   // p. ej. "hoz": sustituye al arma al vencer a todos los jefes
        public string recompensa_nombre; // texto: "LA HOZ DE LA MUERTE"
        public Linea[] recompensa_dialogo;
        public string meta_modelo;      // "barca": la meta es la barca en la orilla (sin puerta)
        public string transicion;       // texto del pergamino al terminar el nivel: hacia dónde va Dante
        public string capitulo;         // encabezado del pergamino al empezar ("Canto I")
        public Linea[] narracion;       // texto del pergamino al empezar (si no, se usan las líneas del narrador de la intro)
    }

    public static class Forja3DLevelBuilder
    {
        public const string RutaNiveles = "Assets/Forja3D/Niveles";
        public const string RutaGenerado = "Assets/Forja3D/Generado";

        const string Demo = @"{""titulo"":""Nivel de demostración"",""objetivo"":""meta"",""mapa"":[
""##############"",
""#P....#......#"",
""#..C..#..E...#"",
""#.....#......#"",
""#..T......C..#"",
""#.....####...#"",
""#..J.....T...#"",
""#.........C.M#"",
""##############""]}";

        [MenuItem("Forja3D/Construir niveles")]
        public static void Menu()
        {
            var escenas = ConstruirTodos();
            EditorUtility.DisplayDialog("Forja3D", $"{escenas.Count} nivel(es) construidos en {RutaGenerado}/Escenas", "OK");
            if (escenas.Count > 0) EditorSceneManager.OpenScene(escenas[0]);
        }

        public static List<string> ConstruirTodos()
        {
            if (AssetDatabase.IsValidFolder(RutaGenerado)) AssetDatabase.DeleteAsset(RutaGenerado);
            Directory.CreateDirectory($"{RutaGenerado}/Escenas");
            Directory.CreateDirectory($"{RutaGenerado}/Datos");
            AssetDatabase.Refresh();

            var jsons = Directory.Exists(RutaNiveles)
                ? Directory.GetFiles(RutaNiveles, "*.json").Where(f => Path.GetFileName(f) != "juego.json").OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>();
            var escenas = new List<string> { ConstruirTitulo() };
            if (jsons.Count == 0)
            {
                Debug.Log("[Forja3D] No hay niveles: construyo el nivel de demostración.");
                escenas.Add(Construir(JsonUtility.FromJson<Nivel>(Demo), "00_demo"));
            }
            else foreach (var j in jsons)
                escenas.Add(Construir(JsonUtility.FromJson<Nivel>(File.ReadAllText(j)), Path.GetFileNameWithoutExtension(j)));
            return escenas;
        }

        // ---------- Pantalla de título (Assets/Forja3D/Niveles/juego.json) ----------
        static string ConstruirTitulo()
        {
            var ruta = $"{RutaNiveles}/juego.json";
            var j = File.Exists(ruta) ? JsonUtility.FromJson<JuegoDef>(File.ReadAllText(ruta)) : new JuegoDef();
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            var t = new GameObject("Titulo").AddComponent<Forja3DTitle>();
            t.titulo = string.IsNullOrEmpty(j.titulo) ? PlayerSettings.productName : j.titulo;
            t.subtitulo = j.subtitulo ?? ""; t.cita = j.cita ?? ""; t.citaTraduccion = j.cita_traduccion ?? "";
            t.colorFondo = Hex(j.color_fondo, t.colorFondo); t.colorTitulo = Hex(j.color_titulo, t.colorTitulo);
            string r = $"{RutaGenerado}/Escenas/00_titulo.unity";
            EditorSceneManager.SaveScene(escena, r);
            return r;
        }

        // ---------- Construcción ----------
        static readonly Dictionary<char, Simbolo> Base = new Dictionary<char, Simbolo>
        {
            ['#'] = new Simbolo { tipo = "muro" }, ['.'] = new Simbolo { tipo = "suelo" }, [' '] = new Simbolo { tipo = "suelo" },
            ['P'] = new Simbolo { tipo = "jugador" }, ['M'] = new Simbolo { tipo = "meta" }, ['E'] = new Simbolo { tipo = "enemigo" },
            ['J'] = new Simbolo { tipo = "jefe", vida = 15, escala = 2f },
            ['C'] = new Simbolo { tipo = "moneda" }, ['T'] = new Simbolo { tipo = "arbol" }, ['R'] = new Simbolo { tipo = "roca" },
            ['~'] = new Simbolo { tipo = "rio" },
        };

        static string Construir(Nivel n, string id)
        {
            if (n.mapa == null || n.mapa.Length == 0) throw new Exception($"Nivel {id}: falta el mapa.");
            var leyenda = new Dictionary<char, Simbolo>(Base);
            if (n.leyenda != null) foreach (var s in n.leyenda) if (!string.IsNullOrEmpty(s.simbolo)) leyenda[s.simbolo[0]] = s;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float c = Mathf.Max(1f, n.celda);
            int filas = n.mapa.Length, cols = n.mapa.Max(f => f.Length);
            Terrain terreno = null; // Pos() se apoya en la altura real del terreno en cuanto existe
            Vector3 Pos(int f, int col)
            {
                var p0 = new Vector3(col * c, 0, (filas - 1 - f) * c);
                if (terreno) p0.y = terreno.SampleHeight(p0) + terreno.transform.position.y;
                return p0;
            }

            // Ambiente
            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional; luz.shadows = LightShadows.Soft; luz.intensity = 1.1f;
            luz.transform.rotation = Quaternion.Euler(50, -35, 0);
            var cielo = Hex(n.cielo, new Color(0.56f, 0.79f, 0.94f));
            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky) { var m = new Material(sky); if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", cielo); Guardar(m, $"cielo_{id}"); RenderSettings.skybox = m; }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = cielo; RenderSettings.ambientEquatorColor = cielo * 0.7f; RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.fog = n.niebla; RenderSettings.fogColor = cielo; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15; RenderSettings.fogEndDistance = 60;

            // Efectos de combate y sonido
            new GameObject("Forja3DFX").AddComponent<Forja3DFX>().matParticula = MatParticula();
            var audio = new GameObject("Audio").AddComponent<Forja3DAudio>();
            audio.ambiente = "ambiente_" + (string.IsNullOrEmpty(n.ambiente) ? "infierno" : n.ambiente);

            // Celdas de muro: el relieve del terreno se levanta ahí (acantilados en vez de cubos)
            var esMuro = new bool[filas, cols];
            for (int f = 0; f < filas; f++) for (int col = 0; col < cols; col++) esMuro[f, col] = Tipo(n, leyenda, f, col) == "muro";

            var nivel = new GameObject("Nivel");
            terreno = CrearTerreno(n, id, filas, cols, c, esMuro, nivel.transform);
            var rnd = new System.Random((n.terreno?.semilla ?? 1) * 7919 + id.GetHashCode());
            var rocas = MallasRoca(id, 6, n.terreno?.semilla ?? 1);
            var matRoca = MatRoca(id, Hex(n.color_muros, new Color(0.55f, 0.52f, 0.47f)));

            // Muros: une celdas seguidas de cada fila en un solo bloque
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    if (Tipo(n, leyenda, f, col) != "muro") continue;
                    int fin = col;
                    while (fin + 1 < cols && Tipo(n, leyenda, f, fin + 1) == "muro") fin++;
                    // Colisión invisible (el aspecto lo dan el relieve del terreno y las rocas)
                    var muro = new GameObject("MuroColision");
                    muro.transform.SetParent(nivel.transform);
                    float altoCol = n.altura_muros + 8f;
                    var centro = (Pos(f, col) + Pos(f, fin)) / 2f; centro.y = 0;
                    muro.transform.position = centro + Vector3.up * (altoCol / 2f - 1f);
                    muro.AddComponent<BoxCollider>().size = new Vector3((fin - col + 1) * c, altoCol, c);
                    GameObjectUtility.SetStaticEditorFlags(muro, StaticEditorFlags.NavigationStatic);
                    col = fin;
                }

            // Ríos (Aqueronte): agua oscura y rojiza que no se puede cruzar a pie; solo en barca
            Material matRio = null;
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    if (Tipo(n, leyenda, f, col) != "rio") continue;
                    int fin = col;
                    while (fin + 1 < cols && Tipo(n, leyenda, f, fin + 1) == "rio") fin++;
                    var centro = (Pos(f, col) + Pos(f, fin)) / 2f;
                    float largoRio = (fin - col + 1) * c;
                    var bloqueo = new GameObject("RioColision"); bloqueo.transform.SetParent(nivel.transform);
                    bloqueo.transform.position = new Vector3(centro.x, 1.5f, centro.z);
                    bloqueo.AddComponent<BoxCollider>().size = new Vector3(largoRio, 4f, c);
                    GameObjectUtility.SetStaticEditorFlags(bloqueo, StaticEditorFlags.NavigationStatic);
                    if (!matRio)
                    {
                        matRio = new Material(Shader.Find("Standard"));
                        matRio.SetColor("_Color", new Color(0.09f, 0.015f, 0.015f));
                        matRio.SetFloat("_Glossiness", 0.93f); matRio.SetFloat("_Metallic", 0.2f);
                        matRio.EnableKeyword("_EMISSION"); matRio.SetColor("_EmissionColor", new Color(0.32f, 0.04f, 0.015f));
                        Guardar(matRio, $"rio_{id}");
                    }
                    var agua = GameObject.CreatePrimitive(PrimitiveType.Quad); agua.name = "Rio";
                    UnityEngine.Object.DestroyImmediate(agua.GetComponent<Collider>());
                    agua.transform.SetParent(nivel.transform);
                    agua.transform.position = new Vector3(centro.x, centro.y + 0.12f, centro.z);
                    agua.transform.rotation = Quaternion.Euler(90, 0, 0);
                    agua.transform.localScale = new Vector3(largoRio + c * 0.2f, c * 1.08f, 1f);
                    agua.GetComponent<MeshRenderer>().sharedMaterial = matRio;
                    // Vapor que sube del río
                    var vapor = new GameObject("Vapor"); vapor.transform.SetParent(agua.transform, false);
                    var ps = vapor.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main; main.startLifetime = 3f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2f); main.maxParticles = 60; main.scalingMode = ParticleSystemScalingMode.Shape;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.12f, 0.1f, 0.25f), new Color(0.2f, 0.06f, 0.06f, 0.35f));
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var em = ps.emission; em.rateOverTime = 4f * (fin - col + 1);
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Rectangle;
                    vapor.transform.localRotation = Quaternion.Euler(180, 0, 0); // el eje Z local queda hacia arriba: emite hacia el cielo
                    vapor.GetComponent<ParticleSystemRenderer>().sharedMaterial = MatParticula();
                    ps.Play();
                    col = fin;
                }

            // Rocas a lo largo de los muros: rompen la silueta del relieve
            var grupoRocas = new GameObject("RocasMuro").transform; grupoRocas.SetParent(nivel.transform);
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    if (!esMuro[f, col] || rnd.NextDouble() > 0.7) continue;
                    var p = Pos(f, col) + new Vector3((float)(rnd.NextDouble() - 0.5) * c * 0.6f, 0, (float)(rnd.NextDouble() - 0.5) * c * 0.6f);
                    p.y = terreno.SampleHeight(p) - 0.4f;
                    float tam = c * (0.55f + (float)rnd.NextDouble() * 0.7f);
                    Roca(rocas[rnd.Next(rocas.Length)], matRoca, p, new Vector3(tam, tam * (0.8f + (float)rnd.NextDouble() * 1.1f), tam), rnd, grupoRocas, false);
                }

            // Paisaje fuera de la zona de juego: árboles muertos y peñascos en las colinas
            Paisaje(n, id, filas, cols, c, terreno, rocas, matRoca, rnd, nivel.transform);

            // Decoración (obstáculos para el NavMesh)
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    var s = Sim(n, leyenda, f, col);
                    if (s == null || (s.tipo != "arbol" && s.tipo != "roca" && s.tipo != "decoracion")) continue;
                    var d = Decoracion(s, c, id, rocas, matRoca, rnd);
                    d.transform.SetParent(nivel.transform);
                    d.transform.position = Pos(f, col);
                    d.transform.rotation = Quaternion.Euler(0, (f * 73 + col * 37) % 360, 0);
                }

            // NavMesh antes de poner personajes y objetos dinámicos
            var surf = nivel.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.BuildNavMesh();
            if (surf.navMeshData) AssetDatabase.CreateAsset(surf.navMeshData, $"{RutaGenerado}/Datos/navmesh_{id}.asset");

            Vector3? inicio = null, meta = null;
            var dinamicos = new GameObject("Dinamicos").transform;
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    var s = Sim(n, leyenda, f, col);
                    if (s == null) continue;
                    var p = Pos(f, col);
                    switch (s.tipo)
                    {
                        case "jugador": inicio = p; Jugador(n, p, dinamicos); break;
                        case "enemigo": Enemigo(s, p, dinamicos, id, false); break;
                        case "jefe": Enemigo(s, p, dinamicos, id, true); break;
                        case "npc": Npc(s, p, dinamicos, id); break;
                        case "moneda": Moneda(p, dinamicos, id); break;
                        case "meta": meta = p; break;
                    }
                }
            if (inicio == null) throw new Exception($"Nivel {id}: falta la P del jugador.");
            if (meta != null) Meta(meta.Value, c, dinamicos, id, inicio.Value, rocas, matRoca, n.meta_modelo);
            if (meta == null) throw new Exception($"Nivel {id}: falta la M de la meta.");

            // Validación: debe existir camino del jugador a la meta
            var camino = new NavMeshPath();
            if (NavMesh.SamplePosition(inicio.Value, out var a, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(meta.Value, out var b, 2f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, camino);
                if (camino.status != NavMeshPathStatus.PathComplete)
                    throw new Exception($"Nivel {id}: no hay camino de P a M (estado {camino.status}). Revisa los muros.");
            }
            else throw new Exception($"Nivel {id}: el jugador o la meta quedaron fuera del NavMesh.");

            // Cámara y reglas
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 400; camGo.AddComponent<AudioListener>();
            camGo.transform.position = inicio.Value + new Vector3(0, 4, -5);
            camGo.AddComponent<Forja3DCamera>().objetivo = GameObject.FindWithTag("Player").transform;
            var gm = new GameObject("Forja3D").AddComponent<Forja3DGameManager>();
            gm.titulo = n.titulo;
            gm.intro = n.intro; gm.salida = n.salida;
            gm.objetivo = n.objetivo == "enemigos" ? Objetivo.Enemigos : n.objetivo == "monedas" ? Objetivo.Monedas : n.objetivo == "jefe" ? Objetivo.Jefe : n.objetivo == "dialogos" ? Objetivo.Dialogos : Objetivo.Meta;
            gm.recompensaDialogo = n.recompensa_dialogo;
            gm.capitulo = n.capitulo; gm.narracion = n.narracion; gm.transicion = n.transicion;

            string ruta = $"{RutaGenerado}/Escenas/{id}.unity";
            EditorSceneManager.SaveScene(escena, ruta);
            Debug.Log($"[Forja3D] Nivel construido: {ruta}");
            return ruta;
        }

        static Simbolo Sim(Nivel n, Dictionary<char, Simbolo> l, int f, int col)
        {
            var fila = n.mapa[f];
            char ch = col < fila.Length ? fila[col] : ' ';
            return l.TryGetValue(ch, out var s) ? s : null;
        }
        static string Tipo(Nivel n, Dictionary<char, Simbolo> l, int f, int col) => Sim(n, l, f, col)?.tipo;

        // ---------- Terreno: plano en la zona jugable, colinas alrededor ----------
        static Terrain CrearTerreno(Nivel n, string id, int filas, int cols, float c, bool[,] esMuro, Transform padre)
        {
            float margen = 30f, alturaMax = 24f;
            float ancho = cols * c + margen * 2, largo = filas * c + margen * 2;
            var td = new TerrainData { heightmapResolution = 513, alphamapResolution = 512 };
            td.size = new Vector3(ancho, alturaMax, largo);
            int res = td.heightmapResolution;
            var h = new float[res, res];
            var rnd = new System.Random(n.terreno?.semilla ?? 1);
            float ox = (float)rnd.NextDouble() * 1000, oz = (float)rnd.NextDouble() * 1000;
            float colinas = Mathf.Clamp01(n.terreno?.colinas ?? 0.5f);
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wx = x / (float)(res - 1) * ancho - margen - c / 2f;
                    float wz = z / (float)(res - 1) * largo - margen - c / 2f;
                    float dx = Mathf.Max(0, -wx, wx - cols * c), dz = Mathf.Max(0, -wz, wz - filas * c);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    float k = Mathf.Clamp01((d - 1f) / (margen * 0.6f));
                    float ruido = Mathf.PerlinNoise(ox + wx * 0.05f, oz + wz * 0.05f);
                    float detalle = Mathf.PerlinNoise(ox + wx * 0.23f, oz + wz * 0.23f);
                    // Colinas fuera de la zona de juego (más altas que antes: horizonte montañoso)
                    float alto = k * k * (0.25f + 0.75f * colinas) * (0.35f + 0.5f * ruido + 0.15f * detalle) * alturaMax;
                    // Suelo jugable con ondulación suave (no afecta a la navegación)
                    if (d <= 0) alto = (detalle - 0.5f) * 0.25f + 0.12f;
                    // Justo fuera del mapa, el borde de acantilado continúa y se funde con las colinas
                    else alto = Mathf.Max(alto, n.altura_muros * (1.1f + 0.5f * ruido) * (1f - Mathf.SmoothStep(0f, 1f, d / 14f)));
                    // Crestas rocosas sobre las celdas de muro
                    float prof = ProfundidadMuro(wx, wz, c, filas, cols, esMuro);
                    if (prof > 0f)
                    {
                        // El relieve sube solo DENTRO de las celdas de muro: los pasos quedan limpios
                        float caida = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(prof / (c * 0.4f)));
                        float cresta = n.altura_muros * (1.05f + 0.6f * Mathf.PerlinNoise(ox + wx * 0.31f, oz + wz * 0.31f)) + detalle * 0.8f;
                        alto = Mathf.Max(alto, cresta * caida);
                    }
                    h[z, x] = Mathf.Clamp01(alto / alturaMax);
                }
            td.SetHeights(0, 0, h);

            var (col1, col2) = (n.terreno?.tipo ?? "pasto") switch
            {
                "arena" => (new Color(0.62f, 0.52f, 0.36f), new Color(0.48f, 0.38f, 0.26f)),
                "nieve" => (new Color(0.86f, 0.89f, 0.93f), new Color(0.7f, 0.75f, 0.82f)),
                "piedra" => (new Color(0.33f, 0.3f, 0.28f), new Color(0.22f, 0.2f, 0.19f)),
                "lava" => (new Color(0.16f, 0.12f, 0.11f), new Color(0.38f, 0.12f, 0.05f)),
                _ => (new Color(0.2f, 0.26f, 0.15f), new Color(0.13f, 0.16f, 0.1f)),   // hierba seca y oscura
            };
            var colRoca = Hex(n.color_muros, new Color(0.4f, 0.37f, 0.34f));
            var capaSuelo = Capa(id, "suelo", TexturaRuido(col1, col2, 3, 1.4f, false), 7f);
            var capaSuelo2 = Capa(id, "suelo2", TexturaRuido(col2 * 0.9f, Color.Lerp(col2, new Color(0.25f, 0.2f, 0.16f), 0.6f), 11, 2.2f, false), 5f);
            var capaRoca = Capa(id, "roca", TexturaRuido(colRoca * 1.1f, colRoca * 0.55f, 23, 1.8f, true), 4f);
            td.terrainLayers = new[] { capaSuelo, capaSuelo2, capaRoca };

            // Pinta: roca en pendientes y crestas, manchas de tierra en el suelo
            int ar = td.alphamapResolution;
            var alfa = new float[ar, ar, 3];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float nx = x / (float)(ar - 1), nz = z / (float)(ar - 1);
                    float pend = td.GetSteepness(nx, nz);
                    float alt = td.GetInterpolatedHeight(nx, nz);
                    float roca = Mathf.Clamp01(Mathf.Max((pend - 22f) / 18f, (alt - 1.2f) / 1.5f));
                    float mancha = Mathf.Clamp01((Mathf.PerlinNoise(ox + nx * 18f, oz + nz * 18f) - 0.45f) * 3f);
                    alfa[z, x, 2] = roca;
                    alfa[z, x, 1] = (1 - roca) * mancha;
                    alfa[z, x, 0] = (1 - roca) * (1 - mancha);
                }
            td.SetAlphamaps(0, 0, alfa);
            AssetDatabase.CreateAsset(td, $"{RutaGenerado}/Datos/terreno_{id}.asset");

            var t = Terrain.CreateTerrainGameObject(td);
            t.name = "Terreno"; t.transform.SetParent(padre);
            t.transform.position = new Vector3(-margen - c / 2f, 0, -margen - c / 2f);
            var ter = t.GetComponent<Terrain>();
            ter.heightmapPixelError = 4; ter.basemapDistance = 60; ter.drawInstanced = true;
            GameObjectUtility.SetStaticEditorFlags(t, StaticEditorFlags.NavigationStatic);
            return ter;
        }

        // Profundidad (m) de un punto dentro de una celda de muro: distancia a la celda de suelo más cercana.
        // 0 si el punto está en suelo. Fuera del mapa cuenta como muro (las colinas lo rodean).
        static float ProfundidadMuro(float wx, float wz, float c, int filas, int cols, bool[,] esMuro)
        {
            bool Muro(int col, int fz) { int fila = filas - 1 - fz; return col < 0 || fila < 0 || col >= cols || fila >= filas || esMuro[fila, col]; }
            int cc = Mathf.RoundToInt(wx / c), fz0 = Mathf.RoundToInt(wz / c);
            if (cc < 0 || fz0 < 0 || cc >= cols || fz0 >= filas) return 0f; // fuera: lo gestionan las colinas
            if (!Muro(cc, fz0)) return 0f;
            float mejor = c;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (Muro(cc + dx, fz0 + dz)) continue;
                    float cx = (cc + dx) * c, cz = (fz0 + dz) * c;
                    float ex = Mathf.Max(0, Mathf.Abs(wx - cx) - c / 2f), ez = Mathf.Max(0, Mathf.Abs(wz - cz) - c / 2f);
                    mejor = Mathf.Min(mejor, Mathf.Sqrt(ex * ex + ez * ez));
                }
            return Mathf.Max(0.0001f, mejor);
        }

        static TerrainLayer Capa(string id, string nombre, Texture2D tex, float tile)
        {
            var ruta = $"{RutaGenerado}/Datos/{nombre}_{id}.png";
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
            var capa = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta), tileSize = new Vector2(tile, tile), smoothness = 0.05f };
            AssetDatabase.CreateAsset(capa, $"{RutaGenerado}/Datos/capa_{nombre}_{id}.terrainlayer");
            return capa;
        }

        // Textura de ruido fractal que se repite sin costuras (suelo, tierra, roca con grietas)
        static Texture2D TexturaRuido(Color a, Color b, int semilla, float contraste, bool grietas)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
            var px = new Color[N * N];
            float Ruido(float x, float y, float f)
            {
                // Ruido periódico: mezcla de 4 muestras para que el borde case con el lado opuesto
                float u = x / N, v = y / N;
                float n00 = Mathf.PerlinNoise(semilla + x * f, semilla + y * f), n10 = Mathf.PerlinNoise(semilla + (x - N) * f, semilla + y * f);
                float n01 = Mathf.PerlinNoise(semilla + x * f, semilla + (y - N) * f), n11 = Mathf.PerlinNoise(semilla + (x - N) * f, semilla + (y - N) * f);
                return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
            }
            var r = new System.Random(semilla);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = Ruido(x, y, 0.02f) * 0.5f + Ruido(x, y, 0.06f) * 0.3f + Ruido(x, y, 0.18f) * 0.2f;
                    v = Mathf.Clamp01((v - 0.5f) * contraste + 0.5f);
                    float grano = (float)r.NextDouble() * 0.12f - 0.06f;
                    var col = Color.Lerp(b, a, v) * (1f + grano);
                    if (grietas)
                    {
                        float g = Mathf.Abs(Ruido(x, y, 0.045f) - 0.5f);
                        if (g < 0.025f) col *= 0.45f + g * 12f;
                    }
                    col.a = 1; px[y * N + x] = col;
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ---------- Rocas procedurales (malla de icosfera deformada, facetada) ----------
        static Mesh[] MallasRoca(string id, int cuantas, int semilla)
        {
            var lista = new Mesh[cuantas];
            for (int i = 0; i < cuantas; i++)
            {
                var m = Icosfera(2);
                var v = m.vertices;
                float s0 = semilla * 13.1f + i * 71.7f;
                for (int k = 0; k < v.Length; k++)
                {
                    var p = v[k];
                    float n1 = Ruido3(p * 1.3f, s0), n2 = Ruido3(p * 3.2f, s0 + 50);
                    p *= 1f + (n1 - 0.5f) * 0.7f + (n2 - 0.5f) * 0.22f;
                    p.y = p.y < -0.25f ? -0.25f + (p.y + 0.25f) * 0.2f : p.y; // base plana
                    v[k] = new Vector3(p.x, p.y * 0.75f, p.z) * 0.5f;
                }
                m.vertices = v;
                Facetar(m);
                m.name = $"roca_{id}_{i}";
                AssetDatabase.CreateAsset(m, $"{RutaGenerado}/Datos/roca_{id}_{i}.asset");
                lista[i] = m;
            }
            return lista;
        }

        static float Ruido3(Vector3 p, float s) =>
            (Mathf.PerlinNoise(p.x + s, p.y + s * 0.7f) + Mathf.PerlinNoise(p.y + s * 1.3f, p.z + s) + Mathf.PerlinNoise(p.z + s * 0.4f, p.x + s * 1.9f)) / 3f;

        static Mesh Icosfera(int subdiv)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3> {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var tris = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                int Medio(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (cache.TryGetValue(k, out var i)) return i;
                    verts.Add(((verts[a] + verts[b]) / 2f).normalized);
                    return cache[k] = verts.Count - 1;
                }
                var nuevos = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Medio(a, b), bc = Medio(b, c), ca = Medio(c, a);
                    nuevos.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nuevos;
            }
            var m = new Mesh(); m.SetVertices(verts); m.SetTriangles(tris, 0);
            return m;
        }

        // Duplica vértices por triángulo (aspecto tallado) y proyecta UV según la cara
        static void Facetar(Mesh m)
        {
            var v = m.vertices; var t = m.triangles;
            var nv = new Vector3[t.Length]; var uv = new Vector2[t.Length]; var nt = new int[t.Length];
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                var an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                Vector2 P(Vector3 p) => an.y >= an.x && an.y >= an.z ? new Vector2(p.x, p.z) : an.x >= an.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                for (int k = 0; k < 3; k++) { nv[i + k] = v[t[i + k]]; uv[i + k] = P(v[t[i + k]]) * 1.5f; nt[i + k] = i + k; }
            }
            m.Clear(); m.vertices = nv; m.uv = uv; m.triangles = nt;
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        }

        static Material MatRoca(string id, Color color)
        {
            var ruta = $"{RutaGenerado}/Datos/tex_roca_{id}.png";
            File.WriteAllBytes(ruta, TexturaRuido(color * 1.15f, color * 0.5f, 31, 2f, true).EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
            var m = new Material(Shader.Find("Standard")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta) };
            m.SetFloat("_Glossiness", 0.08f);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, $"{RutaGenerado}/Datos/mat_roca_{id}.mat");
            return m;
        }

        static GameObject Roca(Mesh malla, Material mat, Vector3 pos, Vector3 escala, System.Random rnd, Transform padre, bool colision)
        {
            var g = new GameObject("Roca", typeof(MeshFilter), typeof(MeshRenderer));
            g.GetComponent<MeshFilter>().sharedMesh = malla;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            g.transform.SetParent(padre);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 20 - 10, (float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 20 - 10);
            g.transform.localScale = escala;
            if (colision) g.AddComponent<MeshCollider>().sharedMesh = malla;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }

        // Árbol muerto procedural (tronco y ramas retorcidas)
        static GameObject ArbolMuerto(System.Random rnd, Material mat, float altura)
        {
            var raiz = new GameObject("ArbolMuerto");
            void Rama(Transform padre, Vector3 desde, Vector3 dir, float largo, float grosor, int nivel)
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.DestroyImmediate(r.GetComponent<Collider>());
                r.GetComponent<Renderer>().sharedMaterial = mat;
                r.transform.SetParent(padre, false);
                r.transform.position = desde + dir * largo / 2f;
                r.transform.up = dir;
                r.transform.localScale = new Vector3(grosor, largo / 2f, grosor);
                if (nivel <= 0) return;
                var fin = desde + dir * largo;
                int hijos = nivel == 3 ? 3 : 2;
                for (int i = 0; i < hijos; i++)
                {
                    var giro = Quaternion.Euler((float)rnd.NextDouble() * 50 + 15, i * (360f / hijos) + (float)rnd.NextDouble() * 60, 0);
                    var nd = (giro * dir + Vector3.up * 0.15f).normalized;
                    var inicio = Vector3.Lerp(desde, fin, 0.65f + (float)rnd.NextDouble() * 0.35f);
                    Rama(raiz.transform, inicio, nd, largo * (0.55f + (float)rnd.NextDouble() * 0.15f), grosor * 0.55f, nivel - 1);
                }
            }
            var inclinado = Quaternion.Euler((float)rnd.NextDouble() * 10 - 5, 0, (float)rnd.NextDouble() * 10 - 5) * Vector3.up;
            Rama(raiz.transform, Vector3.zero, inclinado, altura * 0.5f, altura * 0.07f, 3);
            return raiz;
        }

        static void Paisaje(Nivel n, string id, int filas, int cols, float c, Terrain ter, Mesh[] rocas, Material matRoca, System.Random rnd, Transform padre)
        {
            var grupo = new GameObject("Paisaje").transform; grupo.SetParent(padre);
            var matTronco = Mat("tronco_muerto", new Color(0.12f, 0.1f, 0.09f));
            bool hayArbol = Forja3DImporter.RutaPrefab("arbol");
            int total = Mathf.Clamp((filas + cols) * 3, 40, 140);
            float ancho = cols * c, largo = filas * c;
            for (int i = 0; i < total; i++)
            {
                // Punto en un anillo de 3 a 26 m fuera de la zona de juego
                float lado = (float)rnd.NextDouble() * 4f, dist = 3f + (float)Math.Pow(rnd.NextDouble(), 1.6) * 23f;
                float t = (float)rnd.NextDouble();
                Vector3 p = lado < 1 ? new Vector3(t * ancho, 0, -dist) : lado < 2 ? new Vector3(t * ancho, 0, largo + dist)
                          : lado < 3 ? new Vector3(-dist, 0, t * largo) : new Vector3(ancho + dist, 0, t * largo);
                p -= new Vector3(c / 2f, 0, c / 2f);
                p.y = ter.SampleHeight(p) + ter.transform.position.y;
                if (rnd.NextDouble() < 0.45)
                {
                    GameObject a = hayArbol ? InstanciarModelo("arbol") : ArbolMuerto(rnd, matTronco, 3.5f + (float)rnd.NextDouble() * 3f);
                    foreach (var col in a.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);
                    if (hayArbol) { Enderezar(a); AjustarAltura(a, 6f + (float)rnd.NextDouble() * 4f); }
                    a.transform.SetParent(grupo); a.transform.position = p - Vector3.up * 0.1f;
                    a.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0);
                    GameObjectUtility.SetStaticEditorFlags(a, StaticEditorFlags.BatchingStatic);
                }
                else
                {
                    float tam = 1.5f + (float)rnd.NextDouble() * 4.5f;
                    Roca(rocas[rnd.Next(rocas.Length)], matRoca, p - Vector3.up * tam * 0.2f, new Vector3(tam, tam * (0.6f + (float)rnd.NextDouble()), tam), rnd, grupo, false);
                }
            }
        }

        static Material MatParticula()
        {
            var m = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (m) return m;
            var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, $"{RutaGenerado}/Datos/mat_particula.mat");
            return m;
        }

        // ---------- Entidades ----------
        static GameObject InstanciarModelo(string modelo)
        {
            var pf = Forja3DImporter.RutaPrefab(modelo);
            if (!pf) { if (!string.IsNullOrEmpty(modelo)) Debug.LogWarning($"[Forja3D] Modelo '{modelo}' no encontrado: uso una figura simple."); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            return g;
        }

        // Altura objetivo (m) de cada modelo conocido; el resto usa la del tipo. "escala" del nivel la multiplica.
        static readonly Dictionary<string, float> Alturas = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["dante"] = 1.8f, ["virgilio"] = 1.82f, ["francesca"] = 1.68f, ["paolo"] = 1.8f, ["condenado"] = 1.78f,
            ["muerte"] = 3.0f, ["caron"] = 5.4f, ["minos"] = 5.6f, ["homero"] = 1.78f, // Carón y Minos: titanes (≈7 m con EscalaJefe)
            ["pantera"] = 1.9f, ["leon"] = 2.4f, ["loba"] = 1.9f, // a la cruz; exageradas a propósito (la loba ×1,3 por jefa)
        };
        // Los jefes se exageran un 30 % para que impongan
        const float EscalaJefe = 1.3f;
        static float AlturaDe(string modelo, string tipo, bool fiera) =>
            (!string.IsNullOrEmpty(modelo) && Alturas.TryGetValue(modelo, out var h) ? h : fiera ? 1.1f : tipo == "jefe" ? 2.6f : 1.8f)
            * (tipo == "jefe" ? EscalaJefe : 1f);

        // Escala un modelo para que mida exactamente 'metros' de alto (los FBX de Meshy y Tripo llegan en unidades distintas)
        static void AjustarAltura(GameObject g, float metros)
        {
            float actual = AlturaActual(g);
            if (actual > 0.01f) g.transform.localScale *= metros / actual;
        }

        // Humanoides: de los pies a la coronilla según los huesos (los límites de la malla con piel a veces
        // vienen mal: capas, túnicas o vértices sueltos). Resto: límites de las mallas.
        static float AlturaActual(GameObject g)
        {
            var anim = g.GetComponentInChildren<Animator>();
            if (anim && anim.isHuman)
            {
                var cabeza = anim.GetBoneTransform(HumanBodyBones.Head);
                var pieI = anim.GetBoneTransform(HumanBodyBones.LeftFoot); var pieD = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                if (cabeza && pieI && pieD)
                {
                    float pies = Mathf.Min(pieI.position.y, pieD.position.y);
                    float h = (cabeza.position.y - pies) * 1.16f; // tobillo→suelo y cuello→coronilla
                    if (h > 0.01f) return h;
                }
            }
            return Forja3DImporter.Limites(g).size.y;
        }

        static GameObject Capsula(string nombre, Color color, string id)
        {
            var raiz = new GameObject(nombre);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(raiz.transform); cap.transform.localPosition = Vector3.up;
            cap.GetComponent<Renderer>().sharedMaterial = Mat(nombre.ToLowerInvariant() + "_" + id, color);
            var ojo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(ojo.GetComponent<Collider>());
            ojo.transform.SetParent(raiz.transform); ojo.transform.localPosition = new Vector3(0, 1.5f, 0.45f); ojo.transform.localScale = new Vector3(0.6f, 0.15f, 0.1f);
            ojo.GetComponent<Renderer>().sharedMaterial = Mat("ojos", UnityEngine.Color.black);
            return raiz;
        }

        static void Jugador(Nivel n, Vector3 p, Transform padre)
        {
            var g = InstanciarModelo(n.jugador?.modelo) ?? Capsula("Jugador", new Color(0.2f, 0.45f, 0.95f), "j");
            g.name = "Jugador"; g.tag = "Player"; g.transform.SetParent(padre);
            if (g.GetComponentInChildren<Animator>()) AjustarAltura(g, AlturaDe(n.jugador?.modelo, "jugador", false));
            g.transform.position = p + Vector3.up * 0.05f;
            var cc = g.GetComponent<CharacterController>();
            if (!cc) { cc = g.AddComponent<CharacterController>(); cc.height = 2f; cc.radius = 0.4f; cc.center = Vector3.up; }
            var h = g.AddComponent<Forja3DHealth>(); h.esJugador = true; h.vidaMaxima = Mathf.Max(1, n.jugador?.vida ?? 5);
            var control = g.AddComponent<Forja3DPlayerController>(); control.crearCamaraAutomatica = false;
            var armaIni = n.jugador?.arma ?? "";
            bool conAsta = armaIni.Contains("hoz") || armaIni.Contains("guadana");
            if (conAsta) control.alcanceAtaque *= 1.25f;
            Arma(g, !string.IsNullOrEmpty(n.jugador?.arma) ? n.jugador.arma : Forja3DImporter.RutaPrefab("espada") ? "espada" : null);
            // Arma que se gana al vencer al jefe del nivel (la hoz de la Muerte): queda preparada y oculta
            if (!string.IsNullOrEmpty(n.recompensa_arma) && Forja3DImporter.RutaPrefab(n.recompensa_arma))
            {
                var premio = Arma(g, n.recompensa_arma, "ArmaRecompensa");
                if (premio) { premio.SetActive(false); var pc = g.GetComponent<Forja3DPlayerController>(); pc.nombreRecompensa = n.recompensa_nombre ?? ""; }
            }
            if (!conAsta && Forja3DImporter.RutaPrefab("escudo")) Escudo(g, "escudo"); // con la hoz se cubre con el asta
        }

        // Coloca un arma generada (p. ej. "espada") en la mano derecha de un humanoide.
        // Detecta solo el eje largo y la empuñadura (el extremo cercano a la guarda, la parte más ancha),
        // y la orienta hacia delante del personaje en su pose de referencia (T-pose).
        static GameObject Arma(GameObject portador, string modelo, string nombre = "Arma")
        {
            if (string.IsNullOrEmpty(modelo)) return null;
            // Hoz/guadaña: arma de asta (≈1,9 m), la hoja es el extremo ANCHO y lejano; se empuña por el mango
            bool asta = modelo.Contains("hoz") || modelo.Contains("guadana") || modelo.Contains("lanza");
            var anim = portador.GetComponentInChildren<Animator>();
            bool humano = anim && anim.isHuman;
            var mano = humano ? anim.GetBoneTransform(HumanBodyBones.RightHand) : BuscarHueso(portador.transform, "R_Hand", "RightHand", "hand_r", "Hand_R", "mixamorig:RightHand");
            var antebrazo = humano ? anim.GetBoneTransform(HumanBodyBones.RightLowerArm) : BuscarHueso(portador.transform, "R_Forearm", "RightForeArm", "lowerarm_r", "Forearm_R");
            // Esqueletos de Tripo (genéricos): se coloca el arma en la pose de reposo del clip idle, no en la de
            // referencia de Blender (que deja los brazos en posiciones raras)
            if (!humano && anim && anim.runtimeAnimatorController)
            {
                var idle = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
                if (idle) idle.SampleAnimation(anim.gameObject, 0.1f);
            }
            var arma = InstanciarModelo(modelo);
            if (!mano || !arma) { if (arma) UnityEngine.Object.DestroyImmediate(arma); Debug.LogWarning($"[Forja3D] No se pudo poner el arma '{modelo}'."); return null; }
            foreach (var c in arma.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            arma.transform.position = Vector3.zero; arma.transform.rotation = Quaternion.identity; arma.transform.localScale = Vector3.one;

            // Vértices en el espacio del arma
            var pts = new List<Vector3>();
            foreach (var mf in arma.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh) foreach (var v in mf.sharedMesh.vertices) pts.Add(arma.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            foreach (var sk in arma.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (sk.sharedMesh) foreach (var v in sk.sharedMesh.vertices) pts.Add(arma.transform.InverseTransformPoint(sk.transform.TransformPoint(v)));
            if (pts.Count == 0) { UnityEngine.Object.DestroyImmediate(arma); return null; }
            var min = pts[0]; var max = pts[0];
            foreach (var q in pts) { min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
            var tam = max - min;
            int eje = tam.x >= tam.y && tam.x >= tam.z ? 0 : tam.y >= tam.z ? 1 : 2;
            float largo = Mathf.Max(tam[eje], 0.001f);

            // Perfil de anchura a lo largo del eje: la guarda es el tramo más ancho
            const int B = 20; var ancho = new float[B];
            var centro = (min + max) / 2f;
            foreach (var q in pts)
            {
                int i = Mathf.Clamp((int)((q[eje] - min[eje]) / largo * B), 0, B - 1);
                var d = q - centro; d[eje] = 0;
                ancho[i] = Mathf.Max(ancho[i], d.magnitude);
            }
            int guarda = 0; for (int i = 1; i < B; i++) if (ancho[i] > ancho[guarda]) guarda = i;
            bool empunaduraEnMin = guarda < B / 2;                // la empuñadura está en el extremo más cercano a la guarda
            float tGuarda = (guarda + 0.5f) / B;
            float tAgarre = empunaduraEnMin ? tGuarda * 0.5f : 1f - (1f - tGuarda) * 0.5f;
            if (asta) return Asta(portador, arma, pts, min, largo, eje, mano, antebrazo, humano, nombre);
            var agarre = centro; agarre[eje] = min[eje] + tAgarre * largo;
            var haciaPunta = Vector3.zero; haciaPunta[eje] = empunaduraEnMin ? 1f : -1f;

            // Escala: 1,1 m de largo para un personaje de 1,8 m
            float escPortador = portador.transform.lossyScale.y;
            float k = (asta ? 1.9f : 0.95f) * escPortador / largo;
            arma.transform.localScale = Vector3.one * k;
            // Orienta la hoja hacia delante y un poco hacia arriba
            // En T-pose la palma mira abajo y el pulgar hacia delante: la hoja sale en la dirección del pulgar
            var destino = asta ? (portador.transform.forward + portador.transform.up * 0.6f).normalized : portador.transform.forward;
            arma.transform.rotation = Quaternion.FromToRotation(haciaPunta, destino);
            // Puño: un poco más allá del hueso de la mano, en la dirección del brazo
            var dirBrazo = antebrazo ? (mano.position - antebrazo.position).normalized : portador.transform.right;
            var puno = mano.position + dirBrazo * 0.085f * escPortador - portador.transform.up * 0.03f * escPortador;
            arma.transform.position += puno - arma.transform.TransformPoint(agarre);
            arma.transform.SetParent(mano, true);
            arma.name = nombre;
            GameObjectUtility.SetStaticEditorFlags(arma, (StaticEditorFlags)0);
            return arma;
        }

        static Transform BuscarHueso(Transform raiz, params string[] nombres)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                foreach (var n in nombres) if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        // Arma de asta (hoz): el eje largo no basta, porque la hoja curva ensancha la caja. Se localiza el asta
        // (puntos centrales a lo largo del eje), el extremo de la hoja (el más disperso) y hacia qué lado sale
        // la hoja; luego se orienta con dos ejes: el asta en la dirección del puño y la hoja hacia fuera.
        static GameObject Asta(GameObject portador, GameObject arma, List<Vector3> pts, Vector3 min, float largo, int eje, Transform mano, Transform antebrazo, bool humano, string nombre)
        {
            const int B = 20;
            var suma = new Vector3[B]; var n = new int[B]; var disp = new float[B];
            int Bin(Vector3 q) => Mathf.Clamp((int)((q[eje] - min[eje]) / largo * B), 0, B - 1);
            foreach (var q in pts) { int i = Bin(q); suma[i] += q; n[i]++; }
            var cen = new Vector3[B]; for (int i = 0; i < B; i++) cen[i] = n[i] > 0 ? suma[i] / n[i] : Vector3.zero;
            foreach (var q in pts) { int i = Bin(q); var d = q - cen[i]; d[eje] = 0; disp[i] = Mathf.Max(disp[i], d.magnitude); }
            float Disp(int a, int b) { float m = 0; for (int i = a; i <= b; i++) m = Mathf.Max(m, disp[i]); return m; }
            bool hojaEnMax = Disp(B - 5, B - 1) >= Disp(0, 4);
            int Idx(int k) => hojaEnMax ? k : B - 1 - k;           // k = 0 en el extremo del mango
            Vector3 Cen(int k) { var c = cen[Idx(k)]; return n[Idx(k)] > 0 ? c : cen[Idx(Mathf.Min(k + 1, B - 1))]; }
            var punta = Cen(0);                                     // extremo del mango
            var alto = Cen(13);                                     // asta, por debajo de la hoja
            var dirAsta = (alto - punta).normalized;
            var agarre = punta + (alto - punta) * (0.3f / 0.65f);   // a un 30 % desde el extremo
            var hoja = Vector3.zero; int nh = 0;
            foreach (var q in pts) { int k = hojaEnMax ? Bin(q) : B - 1 - Bin(q); if (k >= 16) { hoja += q; nh++; } }
            hoja = nh > 0 ? hoja / nh : alto;
            var lado = Vector3.ProjectOnPlane(hoja - alto, dirAsta);
            if (lado.sqrMagnitude < 1e-6f) lado = Vector3.Cross(dirAsta, Vector3.right);

            // Tamaño: 1,9 m para Dante (1,8 m); para un jefe, casi su altura
            float altoPortador = Forja3DImporter.Limites(portador).size.y;
            float objetivo = humano ? 1.9f * portador.transform.lossyScale.y : altoPortador * 0.95f;
            arma.transform.localScale = Vector3.one * (objetivo / largo);
            // Humanoide en T-pose: el puño cierra a lo largo del pulgar (hacia delante) y el dorso mira arriba:
            // el asta sale hacia delante y la hoja hacia arriba. Jefe en pose idle: asta casi vertical, hoja al frente.
            var f = portador.transform.forward; var u = portador.transform.up;
            var astaW = humano ? f : (u + f * 0.25f).normalized;
            var hojaW = humano ? u : f;
            arma.transform.rotation = Quaternion.LookRotation(astaW, hojaW) * Quaternion.Inverse(Quaternion.LookRotation(dirAsta, lado));
            float esc = humano ? portador.transform.lossyScale.y : altoPortador / 1.8f;
            var dirBrazo = antebrazo ? (mano.position - antebrazo.position).normalized : portador.transform.right;
            var puno = mano.position + dirBrazo * 0.085f * esc - (humano ? u * 0.03f * esc : Vector3.zero);
            arma.transform.position += puno - arma.transform.TransformPoint(agarre);
            arma.transform.SetParent(mano, true);
            arma.name = nombre;
            GameObjectUtility.SetStaticEditorFlags(arma, (StaticEditorFlags)0);
            return arma;
        }

        // Escudo en el antebrazo izquierdo: la cara (eje más fino del modelo) mira hacia fuera del brazo
        static void Escudo(GameObject portador, string modelo)
        {
            var anim = portador.GetComponentInChildren<Animator>();
            var ante = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            var mano = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var esc = InstanciarModelo(modelo);
            if (!ante || !mano || !esc) { if (esc) UnityEngine.Object.DestroyImmediate(esc); return; }
            foreach (var c in esc.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            esc.transform.position = Vector3.zero; esc.transform.rotation = Quaternion.identity; esc.transform.localScale = Vector3.one;
            var b = Forja3DImporter.Limites(esc);
            int fino = b.size.x <= b.size.y && b.size.x <= b.size.z ? 0 : b.size.y <= b.size.z ? 1 : 2;
            var normal = Vector3.zero; normal[fino] = 1f;
            float diametro = Mathf.Max(b.size[(fino + 1) % 3], b.size[(fino + 2) % 3], 0.01f);
            float escP = portador.transform.lossyScale.y;
            esc.transform.localScale = Vector3.one * (0.75f * escP / diametro);
            // En T-pose el brazo izquierdo apunta a -X: la cara del escudo mira hacia delante del personaje
            esc.transform.rotation = Quaternion.FromToRotation(normal, portador.transform.forward);
            var centroAnte = Vector3.Lerp(ante.position, mano.position, 0.55f) + portador.transform.forward * 0.09f * escP;
            esc.transform.position += centroAnte - Forja3DImporter.Limites(esc).center;
            esc.transform.SetParent(ante, true);
            esc.name = "Escudo";
        }

        static void Enemigo(Simbolo s, Vector3 p, Transform padre, string id, bool jefe)
        {
            var g = InstanciarModelo(s.modelo) ?? Capsula(jefe ? "Jefe" : "Enemigo", Hex(s.color, jefe ? new Color(0.45f, 0.05f, 0.1f) : new Color(0.85f, 0.2f, 0.2f)), id);
            g.name = jefe ? "Jefe" : "Enemigo"; g.transform.SetParent(padre);
            float esc = Mathf.Max(0.2f, s.escala);
            string mod0 = (s.modelo ?? "").ToLowerInvariant();
            bool fiera0 = mod0.Contains("pantera") || mod0.Contains("leon") || mod0.Contains("lob") || mod0.Contains("perro") || mod0.Contains("cerbero");
            g.transform.position = p;
            if (g.GetComponentInChildren<Renderer>() && !string.IsNullOrEmpty(s.modelo) && Forja3DImporter.RutaPrefab(s.modelo))
                AjustarAltura(g, AlturaDe(s.modelo, jefe ? "jefe" : "enemigo", fiera0) * esc);
            else g.transform.localScale *= esc;
            var cc = g.GetComponent<CharacterController>();
            float alto = cc ? cc.height : 2f, radio = cc ? cc.radius : 0.4f;
            if (!cc)
            {
                // Sin CharacterController (jefes Tripo): la cápsula se ajusta al tamaño real del modelo
                var lim = Forja3DImporter.Limites(g); float k = Mathf.Max(0.0001f, g.transform.lossyScale.y);
                if (lim.size.y > 0.1f) { alto = lim.size.y / k; radio = Mathf.Clamp(Mathf.Min(lim.size.x, lim.size.z) / k * 0.3f, 0.3f, alto * 0.25f); }
            }
            if (cc) UnityEngine.Object.DestroyImmediate(cc);
            var col = g.AddComponent<CapsuleCollider>(); col.height = alto; col.radius = radio; col.center = Vector3.up * alto / 2f;
            var ag = g.AddComponent<NavMeshAgent>(); ag.height = alto; ag.radius = radio;
            var h = g.AddComponent<Forja3DHealth>(); h.vidaMaxima = Mathf.Max(1, s.vida);
            var e = g.AddComponent<Forja3DEnemy>();
            e.nombre = s.nombre ?? "";
            string m = (s.modelo ?? "").ToLowerInvariant();
            bool fiera = m.Contains("pantera") || m.Contains("leon") || m.Contains("lob") || m.Contains("perro") || m.Contains("cerbero");
            e.voz = !string.IsNullOrEmpty(s.voz) ? s.voz : fiera ? "fiera" : jefe ? "jefe" : "condenado";
            // Arma en la mano del enemigo (la Muerte lleva su hoz)
            if (!string.IsNullOrEmpty(s.arma) && Forja3DImporter.RutaPrefab(s.arma)) Arma(g, s.arma, "ArmaJefe");
            // Aura del pecado (campo "aura" del nivel, o el color de cada fiera por defecto)
            string auraHex = !string.IsNullOrEmpty(s.aura) ? s.aura : m.Contains("pantera") ? "#ff2a8a" : m.Contains("leon") ? "#ffb62e" : m.Contains("lob") ? "#9b3cff" : "";
            if (!string.IsNullOrEmpty(auraHex)) Aura(g, Hex(auraHex, Color.white), id, AlturaDe(s.modelo, jefe ? "jefe" : "enemigo", fiera) * esc);
            if (fiera) { e.velocidad = 3.6f; e.radioAtaque = Mathf.Max(1.5f, AlturaDe(s.modelo, "enemigo", true) * 1.1f) * esc; e.probImparable = 0.3f; }
            if (jefe)
            {
                e.esJefe = true; e.danio = 2; e.velocidad = fiera ? 3.2f : 2.4f; e.enfriamiento = 1.6f;
                // El alcance crece con el tamaño real del modelo (jefes de 2,5-3 m)
                float altoReal = AlturaDe(s.modelo, "jefe", fiera) * esc;
                e.radioDeteccion = 14f; e.radioAtaque = Mathf.Max(1.5f * esc, altoReal * (fiera ? 1.1f : 0.55f)); e.radioPatrulla = 2f; e.probImparable = 0.25f; // un cuadrúpedo es más largo que alto
            }
        }

        // Material translúcido (modo Fade del Standard) con un brillo frío: alma de un poeta
        static void Fantasma(GameObject g, string id, string modelo)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]) { name = $"fantasma_{modelo}_{i}" };
                    m.shader = Shader.Find("Standard");
                    m.SetFloat("_Mode", 2f);
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = 3000;
                    var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    m.SetColor("_Color", new Color(col.r * 0.9f, col.g * 0.95f, col.b, 0.5f));
                    m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.18f, 0.24f, 0.32f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    Guardar(m, $"fantasma_{id}_{modelo}_{r.GetInstanceID() & 0xffff}_{i}");
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var luz = new GameObject("Halo").AddComponent<Light>();
            luz.transform.SetParent(g.transform, false); luz.transform.localPosition = Vector3.up * 1.2f / Mathf.Max(0.0001f, g.transform.lossyScale.y);
            luz.type = LightType.Point; luz.color = new Color(0.6f, 0.75f, 1f); luz.range = 4f; luz.intensity = 0.8f; luz.shadows = LightShadows.None;
        }

        // Aura del pecado que encarna el enemigo: luz de color y chispas que suben del cuerpo
        static void Aura(GameObject g, Color color, string id, float alto)
        {
            var aura = new GameObject("Aura"); aura.transform.SetParent(g.transform, false);
            float k = Mathf.Max(0.0001f, g.transform.lossyScale.y);
            aura.transform.localPosition = Vector3.up * (alto * 0.45f / k);
            aura.transform.localScale = Vector3.one / k;
            var luz = aura.AddComponent<Light>();
            luz.type = LightType.Point; luz.color = color; luz.range = alto * 2.2f; luz.intensity = 2.2f; luz.shadows = LightShadows.None;
            var ps = aura.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 2f; main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f); main.startSize = new ParticleSystem.MinMaxCurve(0.08f * alto / 2f, 0.22f * alto / 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 120; main.gravityModifier = -0.15f;
            var em = ps.emission; em.rateOverTime = 28f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(alto * 0.45f, alto * 0.9f, alto * 0.45f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var r = aura.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = MatParticula(); r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        static void Npc(Simbolo s, Vector3 p, Transform padre, string id)
        {
            var g = InstanciarModelo(s.modelo) ?? Capsula(string.IsNullOrEmpty(s.nombre) ? "Personaje" : s.nombre, Hex(s.color, new Color(0.85f, 0.82f, 0.7f)), id);
            g.name = string.IsNullOrEmpty(s.nombre) ? "Personaje" : s.nombre;
            g.transform.SetParent(padre); g.transform.position = p;
            if (!string.IsNullOrEmpty(s.modelo) && Forja3DImporter.RutaPrefab(s.modelo)) AjustarAltura(g, AlturaDe(s.modelo, "npc", false) * Mathf.Max(0.2f, s.escala));
            else g.transform.localScale *= Mathf.Max(0.2f, s.escala);
            var cc = g.GetComponent<CharacterController>();
            float alto = cc ? cc.height : 2f, radio = cc ? cc.radius : 0.4f;
            if (cc) UnityEngine.Object.DestroyImmediate(cc);
            // Fantasma (Virgilio): sin colisión y semitransparente; se le puede atravesar
            if (s.fantasma) Fantasma(g, id, s.modelo);
            else { var col = g.AddComponent<CapsuleCollider>(); col.height = alto; col.radius = radio; col.center = Vector3.up * alto / 2f; }
            if (s.seguir) { var ag = g.AddComponent<NavMeshAgent>(); ag.height = alto; ag.radius = radio; ag.speed = 3.2f; ag.stoppingDistance = 2f; }
            var npc = g.AddComponent<Forja3DNPC>();
            npc.nombre = s.nombre ?? ""; npc.dialogo = s.dialogo; npc.seguir = s.seguir; npc.clave = s.clave; npc.desvanecer = s.desvanecer;
            if (s.clave || s.desvanecer) npc.distancia = 4f;
        }

        static void Moneda(Vector3 p, Transform padre, string id)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "Moneda"; g.transform.SetParent(padre);
            g.transform.position = p + Vector3.up; g.transform.rotation = Quaternion.Euler(90, 0, 0);
            g.transform.localScale = new Vector3(0.7f, 0.06f, 0.7f);
            var m = Mat("moneda", new Color(1f, 0.82f, 0.15f)); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0f));
            g.GetComponent<Renderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            var t = g.AddComponent<SphereCollider>(); t.isTrigger = true; t.radius = 1.2f;
            g.AddComponent<Forja3DCollectible>();
        }

        // Meta: la Puerta (modelo "puerta") con un resplandor; sin modelo, un arco de roca procedural
        static void Meta(Vector3 p, float c, Transform padre, string id, Vector3 mirarA, Mesh[] rocas, Material matRoca, string modeloMeta = null)
        {
            // Meta con otro modelo (p. ej. la barca de Carón en la orilla): sin puerta ni portal
            if (!string.IsNullOrEmpty(modeloMeta) && modeloMeta != "puerta" && Forja3DImporter.RutaPrefab(modeloMeta))
            {
                var gm = new GameObject("Meta"); gm.transform.SetParent(padre); gm.transform.position = p;
                var trig = gm.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.size = new Vector3(c * 1.2f, 3f, c * 1.2f); trig.center = Vector3.up * 1.5f;
                var mod = InstanciarModelo(modeloMeta);
                foreach (var cc in mod.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(cc);
                mod.transform.SetParent(gm.transform, false);
                var lb = Forja3DImporter.Limites(mod);
                float largoMod = Mathf.Max(lb.size.x, lb.size.z);
                mod.transform.localScale *= c * 2.2f / Mathf.Max(0.01f, largoMod);
                // A lo largo de la orilla (eje X del mapa) y medio metido en el agua (hacia el sur)
                lb = Forja3DImporter.Limites(mod);
                if (lb.size.z > lb.size.x) mod.transform.Rotate(0, 90, 0, Space.World);
                lb = Forja3DImporter.Limites(mod);
                mod.transform.position += new Vector3(p.x - lb.center.x, p.y - lb.min.y - 0.25f, p.z - c * 0.55f - lb.center.z);
                var farol = new GameObject("Farol").AddComponent<Light>();
                farol.transform.SetParent(gm.transform, false); farol.transform.localPosition = Vector3.up * 2.2f;
                farol.type = LightType.Point; farol.color = new Color(1f, 0.55f, 0.25f); farol.range = 8f; farol.intensity = 1.8f;
                gm.AddComponent<Forja3DGoal>();
                return;
            }
            var g = new GameObject("Meta"); g.transform.SetParent(padre); g.transform.position = p;
            var hacia = mirarA - p; hacia.y = 0;
            if (hacia.sqrMagnitude > 0.01f) g.transform.rotation = Quaternion.LookRotation(-hacia.normalized);
            var col = g.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(c * 0.9f, 3f, c * 0.9f); col.center = Vector3.up * 1.5f;

            var puerta = InstanciarModelo("puerta");
            if (puerta)
            {
                foreach (var cc in puerta.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(cc);
                puerta.transform.SetParent(g.transform, false);
                var b = Forja3DImporter.Limites(puerta);
                puerta.transform.localScale *= 5.5f / Mathf.Max(0.01f, b.size.y);
                b = Forja3DImporter.Limites(puerta);
                puerta.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
                b = Forja3DImporter.Limites(puerta);
                Portal(g, b, p, id);
            }
            else
            {
                var rnd = new System.Random(id.GetHashCode());
                for (int lado = -1; lado <= 1; lado += 2)
                    for (int k = 0; k < 3; k++)
                    {
                        var r = Roca(rocas[rnd.Next(rocas.Length)], matRoca, Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), rnd, g.transform, false);
                        r.transform.localPosition = new Vector3(lado * c * 0.7f, 0.6f + k * 1.4f, 0);
                    }
                var dintel = Roca(rocas[0], matRoca, Vector3.zero, new Vector3(c * 1.9f, 0.9f, 1.1f), rnd, g.transform, false);
                dintel.transform.localPosition = new Vector3(0, 4.4f, 0); dintel.transform.localRotation = Quaternion.identity;
            }
            // Resplandor rojizo dentro del arco: señala la salida
            var luz = new GameObject("Resplandor").AddComponent<Light>();
            luz.transform.SetParent(g.transform, false); luz.transform.localPosition = Vector3.up * 2f;
            luz.type = LightType.Point; luz.color = new Color(1f, 0.45f, 0.2f); luz.range = 9f; luz.intensity = 2.2f;
            g.AddComponent<Forja3DGoal>();
        }

        // Portal oscuro dentro del arco: un velo negro con borde carmesí y humo que gira hacia el centro
        static void Portal(GameObject meta, Bounds b, Vector3 suelo, string id)
        {
            bool anchoEnX = b.size.x >= b.size.z;
            float ancho = (anchoEnX ? b.size.x : b.size.z) * 0.46f, alto = b.size.y * 0.66f;
            var velo = GameObject.CreatePrimitive(PrimitiveType.Quad); velo.name = "Portal";
            UnityEngine.Object.DestroyImmediate(velo.GetComponent<Collider>());
            velo.transform.SetParent(meta.transform, true);
            velo.transform.position = new Vector3(b.center.x, suelo.y + alto * 0.5f + 0.05f, b.center.z);
            velo.transform.rotation = Quaternion.LookRotation(anchoEnX ? Vector3.forward : Vector3.right);
            velo.transform.localScale = new Vector3(ancho, alto, 1f);
            // Textura radial: negro absoluto en el centro, carmesí en el borde, transparente fuera
            const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "portal" };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f); float d = Mathf.Sqrt(dx * dx + dy * dy);
                    var c = Color.Lerp(new Color(0.01f, 0f, 0.02f), new Color(0.55f, 0.04f, 0.08f), Mathf.SmoothStep(0.55f, 0.95f, d));
                    c.a = 1f - Mathf.SmoothStep(0.88f, 1f, d);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, $"{RutaGenerado}/Datos/portal_{id}.asset");
            var mat = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = tex };
            Guardar(mat, $"portal_{id}");
            var r = velo.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Por detrás también (el quad es de una sola cara)
            var reverso = UnityEngine.Object.Instantiate(velo, velo.transform.parent); reverso.name = "PortalReverso";
            reverso.transform.rotation = velo.transform.rotation * Quaternion.Euler(0, 180, 0);
            // Humo que gira hacia dentro
            var humo = new GameObject("Remolino"); humo.transform.SetParent(velo.transform, false);
            var ps = humo.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = 2.2f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.02f, 0.05f, 0.7f), new Color(0.05f, 0f, 0.08f, 0.9f));
            main.maxParticles = 150; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.rateOverTime = 45f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f; sh.radiusThickness = 0.15f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.6f); vel.radial = new ParticleSystem.MinMaxCurve(-0.22f);
            vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            humo.GetComponent<ParticleSystemRenderer>().sharedMaterial = MatParticula();
            ps.Play();
        }

        // Si un modelo llega tumbado (su eje más largo no es el vertical), lo pone de pie
        static void Enderezar(GameObject g)
        {
            var b = Forja3DImporter.Limites(g);
            if (b.size.y >= b.size.x * 0.9f && b.size.y >= b.size.z * 0.9f) return;
            g.transform.rotation = (b.size.x > b.size.z ? Quaternion.Euler(0, 0, 90) : Quaternion.Euler(90, 0, 0)) * g.transform.rotation;
        }

        static GameObject Decoracion(Simbolo s, float c, string id, Mesh[] rocas, Material matRoca, System.Random rnd)
        {
            // T y R usan los modelos "arbol" y "roca" del proyecto si existen
            var modelo = !string.IsNullOrEmpty(s.modelo) ? s.modelo : s.tipo == "arbol" && Forja3DImporter.RutaPrefab("arbol") ? "arbol" : s.tipo == "roca" && Forja3DImporter.RutaPrefab("roca") ? "roca" : null;
            var g = InstanciarModelo(modelo);
            if (g)
            {
                // Ajusta el modelo generado al tamaño de la celda
                if (s.tipo == "arbol")
                {
                    Enderezar(g);
                    AjustarAltura(g, (5f + (float)rnd.NextDouble() * 2.5f) * Mathf.Max(0.2f, s.escala));
                    // Colisión solo en el tronco (las ramas no bloquean el paso)
                    foreach (var cc in g.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(cc);
                    var raiz = new GameObject("Arbol"); g.transform.SetParent(raiz.transform, true);
                    var bt = Forja3DImporter.Limites(g); g.transform.position -= new Vector3(bt.center.x, bt.min.y, bt.center.z);
                    var tronco = raiz.AddComponent<CapsuleCollider>(); tronco.radius = 0.4f; tronco.height = 3f; tronco.center = Vector3.up * 1.5f;
                    return raiz;
                }
                var b = Forja3DImporter.Limites(g);
                float lado = Mathf.Max(b.size.x, b.size.z, 0.01f);
                g.transform.localScale *= c * 0.9f / lado * Mathf.Max(0.2f, s.escala);
                var b2 = Forja3DImporter.Limites(g); g.transform.position -= new Vector3(b2.center.x, b2.min.y, b2.center.z);
                var envoltura = new GameObject(s.tipo == "roca" ? "Roca" : "Decoracion"); g.transform.SetParent(envoltura.transform, true);
                return envoltura;
            }
            g = new GameObject(s.tipo == "roca" ? "Roca" : "Arbol");
            if (s.tipo == "roca")
            {
                float tam = c * 0.85f;
                Roca(rocas[rnd.Next(rocas.Length)], matRoca, Vector3.up * -0.15f, new Vector3(tam, tam * 0.9f, tam), rnd, g.transform, true);
            }
            else
            {
                var a = ArbolMuerto(rnd, Mat("tronco_muerto", Hex(s.color, new Color(0.12f, 0.1f, 0.09f))), 4f + (float)rnd.NextDouble() * 2f);
                a.transform.SetParent(g.transform, false);
                var cap = g.AddComponent<CapsuleCollider>(); cap.radius = 0.35f; cap.height = 3f; cap.center = Vector3.up * 1.5f;
            }
            g.transform.localScale *= Mathf.Max(0.2f, s.escala);
            return g;
        }

        // ---------- Utilidades ----------
        static readonly Dictionary<string, Material> cacheMat = new Dictionary<string, Material>();
        static Material Mat(string nombre, Color color)
        {
            var ruta = $"{RutaGenerado}/Datos/mat_{nombre}.mat";
            if (cacheMat.TryGetValue(ruta, out var m) && m) return m;
            m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(m, ruta);
            return cacheMat[ruta] = m;
        }
        static void Guardar(UnityEngine.Object o, string nombre) => AssetDatabase.CreateAsset(o, $"{RutaGenerado}/Datos/{nombre}.mat");

        static Color Hex(string hex, Color def) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : def;
    }
}
#endif
