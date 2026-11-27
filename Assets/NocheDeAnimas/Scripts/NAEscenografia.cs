// Noche de Ánimas — convierte el mapa en un pueblo de la sierra: casas de adobe de colores, bodega,
// iglesia con campanario, puestos del mercado, tumbas con cempasúchil, faroles y papel picado.
// Se construye al cargar el nivel a partir de NAMapas (mismo mapa que el nivel de Forja3D). 0 créditos.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NocheDeAnimas
{
    // Rejilla del mapa → posiciones del mundo
    public static class NAMapa
    {
        public static string[] Filas;
        public static float C => NAMapas.Celda;

        public static void Cargar(string escena) { Filas = NAMapas.Mapa(escena); casas = null; }

        public static char En(int f, int col)
        {
            if (Filas == null || f < 0 || f >= Filas.Length || col < 0 || col >= Filas[f].Length) return '#';
            return Filas[f][col];
        }

        public static bool EsMuro(char ch) => ch == '#' || ch == 'X' || ch == 'B';

        public static Vector3 Pos(int f, int col)
        {
            var p = new Vector3(col * C, 0f, (Filas.Length - 1 - f) * C);
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        public static List<Vector3> Todas(char ch)
        {
            var l = new List<Vector3>();
            if (Filas == null) return l;
            for (int f = 0; f < Filas.Length; f++)
                for (int c = 0; c < Filas[f].Length; c++)
                    if (Filas[f][c] == ch) l.Add(Pos(f, c));
            return l;
        }

        public static bool Buscar(char ch, out int fila, out int col)
        {
            fila = col = -1;
            if (Filas == null) return false;
            for (int f = 0; f < Filas.Length; f++) { int c = Filas[f].IndexOf(ch); if (c >= 0) { fila = f; col = c; return true; } }
            return false;
        }

        // Dirección hacia el muro vecino (para colgar una cortina, un calendario o una puerta).
        // El largo del vector es la distancia del centro de la celda a la cara del muro.
        public static Vector3 HaciaMuro(int f, int col)
        {
            if (NACasas.EsLinea(En(f, col)))
                return (NACasas.LineaEnX(Filas, f, col) ? Vector3.forward : Vector3.right) * 0.05f;
            var dirs = new[] { (f - 1, col, Vector3.forward), (f + 1, col, Vector3.back), (f, col + 1, Vector3.right), (f, col - 1, Vector3.left) };
            foreach (var (a, b, d) in dirs) if (EsMuro(En(a, b))) return d * (C / 2f);
            foreach (var (a, b, d) in dirs) if (NACasas.EsPared(En(a, b))) return d * (C - NACasas.Grosor / 2f);
            return Vector3.forward * (C / 2f);
        }

        // Celda del mapa que corresponde a un punto del mundo
        public static void Celda(Vector3 p, out int f, out int col)
        {
            col = Mathf.RoundToInt(p.x / C);
            f = (Filas != null ? Filas.Length - 1 : 0) - Mathf.RoundToInt(p.z / C);
        }

        // Un punto del NavMesh donde puede aparecer un zombie: en el suelo (no encima de un muro
        // invisible ni de un puesto) y fuera de las celdas de muro
        public static bool SueloValido(Vector3 p)
        {
            if (Filas == null) return true;
            Celda(p, out int f, out int col);
            char ch = En(f, col);
            if (EsMuro(ch) || ch == 'S' || ch == 'Y' || NACasas.EsLinea(ch)) return false;
            var t = Terrain.activeTerrain;
            if (t && Mathf.Abs(p.y - (t.SampleHeight(p) + t.transform.position.y)) > 0.8f) return false;
            return true;
        }

        static List<NACasas.Casa> casas;
        public static List<NACasas.Casa> Casas => casas ??= NACasas.Casas(Filas);
        public static void Reiniciar() => casas = null;

        // Índice de la casa en la que está un punto (-1 si está en la calle)
        public static int CasaEn(Vector3 p)
        {
            Celda(p, out int f, out int col);
            var l = Casas;
            for (int i = 0; i < l.Count; i++) if (f >= l[i].f0 && f <= l[i].f1 && col >= l[i].c0 && col <= l[i].c1) return i;
            return -1;
        }

        // Celdas interiores de una casa (para que los muertos salgan de ahí)
        public static List<Vector3> Interior(int i)
        {
            var r = new List<Vector3>();
            var k = Casas[i];
            for (int f = k.f0 + 1; f < k.f1; f++)
                for (int col = k.c0 + 1; col < k.c1; col++)
                {
                    char ch = En(f, col);
                    if (ch == '.' || (ch >= '1' && ch <= '6')) r.Add(Pos(f, col));
                }
            return r;
        }
    }

    public static class NAEscenografia
    {
        static readonly string[] Adobe = { "#e8b87c", "#d9776b", "#7fb7a4", "#f0d27a", "#c96f53", "#9fb5d6", "#e79fb4", "#b5c98a" };
        static readonly string[] Papel = { "#e63946", "#f4a261", "#2a9d8f", "#e9c46a", "#9b5de5", "#00bbf9", "#f15bb5" };

        public static void Construir(string escena, bool noche)
        {
            var filas = NAMapa.Filas;
            if (filas == null) return;
            lucesFarol = 0;
            bool pueblo = !escena.StartsWith("02");
            var raiz = new GameObject("Pueblo").transform;
            var rnd = new System.Random(escena.GetHashCode());
            float c = NAMapa.C;

            // Las rocas que la plantilla pone en los muros sobran en el pueblo
            if (pueblo) { var rocas = GameObject.Find("RocasMuro"); if (rocas) Object.Destroy(rocas); }

            Luz(noche);

            for (int f = 0; f < filas.Length; f++)
            {
                for (int col = 0; col < filas[f].Length; col++)
                {
                    char ch = filas[f][col];
                    if (pueblo && NAMapa.EsMuro(ch))
                    {
                        // Tramos de 1 a 3 celdas del mismo tipo: cada tramo es una casa
                        int fin = col;
                        int largo = 1 + rnd.Next(3);
                        while (fin + 1 < filas[f].Length && filas[f][fin + 1] == ch && fin - col + 1 < largo) fin++;
                        Casa(raiz, f, col, fin, ch, rnd);
                        col = fin;
                        continue;
                    }
                    var p = NAMapa.Pos(f, col);
                    switch (ch)
                    {
                        case 'S': Puesto(raiz, p, rnd); break;
                        case 'Y': Tumba(raiz, p, rnd); break;
                        case 'F': Farol(raiz, p, noche); break;
                    }
                }
            }
            CasasHuecas(raiz, rnd);
            if (escena.StartsWith("04")) Campanario(raiz);
            if (pueblo) PapelPicado(raiz, rnd);
            StaticBatchingUtility.Combine(raiz.gameObject);
        }

        static void Luz(bool noche)
        {
            var sol = GameObject.Find("Sol");
            var l = sol ? sol.GetComponent<Light>() : null;
            if (noche)
            {
                if (l) { l.intensity = 0.22f; l.color = new Color(0.55f, 0.62f, 1f); }
                RenderSettings.ambientSkyColor = new Color(0.12f, 0.14f, 0.26f);
                RenderSettings.ambientEquatorColor = new Color(0.08f, 0.09f, 0.16f);
                RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.07f);
                RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.14f);
                RenderSettings.fogStartDistance = 6f; RenderSettings.fogEndDistance = 38f;
            }
            else if (l) { l.intensity = 1.15f; l.color = new Color(1f, 0.82f, 0.6f); l.transform.rotation = Quaternion.Euler(28, -40, 0); }
        }

        static void Casa(Transform raiz, int f, int col, int fin, char ch, System.Random rnd)
        {
            float c = NAMapa.C;
            var a = NAMapa.Pos(f, col); var b = NAMapa.Pos(f, fin);
            var centro = (a + b) / 2f;
            float ancho = (fin - col + 1) * c;
            float alto = ch == 'X' ? 9f : ch == 'B' ? 5f : 3.6f + (float)rnd.NextDouble() * 2.6f;
            Color color = ch == 'X' ? NAMat.Hex("#e9e2d0") : ch == 'B' ? NAMat.Hex("#7d8a96") : NAMat.Hex(Adobe[rnd.Next(Adobe.Length)]);
            var casa = new GameObject(ch == 'X' ? "Iglesia" : ch == 'B' ? "Bodega" : "Casa").transform;
            casa.SetParent(raiz); casa.position = new Vector3(centro.x, 0f, centro.z);
            NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(0, alto / 2f - 1f, 0), new Vector3(ancho, alto + 2f, c), color);
            // Zócalo oscuro
            NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(0, 0.0f, 0), new Vector3(ancho + 0.04f, 1.6f, c + 0.04f), color * 0.7f);
            if (ch == '#' && rnd.NextDouble() < 0.6)
                NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(0, alto + 0.12f, 0), new Vector3(ancho + 0.5f, 0.25f, c + 0.5f), NAMat.Hex("#8a3b2a")); // tejas
            else
                NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(0, alto + 0.08f, 0), new Vector3(ancho + 0.2f, 0.16f, c + 0.2f), color * 0.85f); // pretil

            // Ventanas y puertas hacia la calle
            for (int k = col; k <= fin; k++)
            {
                float x = (k - (col + fin) / 2f) * c;
                foreach (int lado in new[] { -1, 1 })
                {
                    char vecino = NAMapa.En(f - lado, k); // lado 1 = la fila de arriba (norte, +z)
                    if (NAMapa.EsMuro(vecino)) continue;
                    float z = lado * (c / 2f + 0.03f);
                    if (ch == 'X')
                    {
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 4.2f, z), new Vector3(0.9f, 2.2f, 0.06f), NAMat.Hex("#3b2a5a"), default, 0.6f); // vitral
                        continue;
                    }
                    // Estas casas no se abren: puertas y ventanas tapiadas con tablas cruzadas
                    float zt = z + lado * 0.05f;
                    var tabla = NAMat.Hex("#8a6a45");
                    if (rnd.NextDouble() < 0.35)
                    {
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 0.9f, z), new Vector3(1.1f, 2.1f, 0.06f), NAMat.Hex("#4a2e1e")); // puerta
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 0.9f, zt), new Vector3(0.16f, 2.3f, 0.05f), tabla, new Vector3(0, 0, 28));
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 0.9f, zt), new Vector3(0.16f, 2.3f, 0.05f), tabla, new Vector3(0, 0, -28));
                    }
                    else
                    {
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 1.8f, z), new Vector3(0.9f, 1.0f, 0.06f), NAMat.Hex("#1c1f26")); // ventana
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 1.95f, zt), new Vector3(1.1f, 0.16f, 0.05f), tabla, new Vector3(0, 0, 8));
                        NAMat.Pieza(PrimitiveType.Cube, casa, new Vector3(x, 1.6f, zt), new Vector3(1.1f, 0.16f, 0.05f), tabla, new Vector3(0, 0, -6));
                    }
                }
            }
        }

        // Casas por las que se puede entrar: paredes delgadas, marcos de puertas y ventanas, techo y piso
        static void CasasHuecas(Transform raiz, System.Random rnd)
        {
            var m = NAMapa.Filas; var casas = NAMapa.Casas;
            if (casas.Count == 0) return;
            var colores = new Color[casas.Count];
            for (int i = 0; i < casas.Count; i++) colores[i] = casas[i].bodega ? NAMat.Hex("#7d8a96") : NAMat.Hex(Adobe[rnd.Next(Adobe.Length)]);
            var t = Terrain.activeTerrain;
            float Suelo(Vector3 p) => t ? t.SampleHeight(new Vector3(p.x, 0, p.z)) + t.transform.position.y : 0f;

            foreach (var k in NACasas.Cajas(m, NAMapa.C))
            {
                int i = -1;
                for (int j = 0; j < casas.Count; j++) if (k.f >= casas[j].f0 && k.f <= casas[j].f1 && k.c >= casas[j].c0 && k.c <= casas[j].c1) { i = j; break; }
                var color = i >= 0 ? colores[i] : NAMat.Hex(Adobe[0]);
                var centro = k.b.center; float y0 = Suelo(centro);
                var p = new Vector3(centro.x, y0 + centro.y, centro.z);
                NAMat.Pieza(PrimitiveType.Cube, raiz, p, k.b.size, k.dintel ? color * 0.92f : color);
                if (!k.dintel && k.b.size.y > 2f)
                    NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(p.x, y0 + 0.25f, p.z), new Vector3(k.b.size.x + 0.05f, 1.0f, k.b.size.z + 0.05f), color * 0.68f); // zócalo
            }

            float g = NACasas.Grosor / 2f;
            for (int i = 0; i < casas.Count; i++)
            {
                var k = casas[i];
                var no = NAMapa.Pos(k.f0, k.c0); var se = NAMapa.Pos(k.f1, k.c1);
                float x0 = no.x - g, x1 = se.x + g, z1 = no.z + g, z0 = se.z - g;
                var centro = new Vector3((x0 + x1) / 2f, 0, (z0 + z1) / 2f);
                float y0 = Suelo(centro);
                // Techo de teja (o de lámina en la bodega) que sí da sombra
                var techo = NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(centro.x, y0 + NACasas.Alto + 0.1f, centro.z), new Vector3(x1 - x0 + 0.7f, 0.22f, z1 - z0 + 0.7f), k.bodega ? NAMat.Hex("#9aa3ab") : NAMat.Hex("#8a3b2a"));
                techo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(centro.x, y0 + NACasas.Alto + 0.25f, centro.z), new Vector3(x1 - x0 + 0.2f, 0.12f, z1 - z0 + 0.2f), colores[i] * 0.85f); // pretil
                // Piso de barro cocido
                NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(centro.x, y0 + 0.05f, centro.z), new Vector3(x1 - x0 - 0.5f, 0.08f, z1 - z0 - 0.5f), k.bodega ? NAMat.Hex("#5d6166") : NAMat.Hex("#9c5f3e"));
                // Algo de mobiliario: una mesa y una silla en una esquina libre
                if (!k.bodega && k.f1 - k.f0 >= 3 && k.c1 - k.c0 >= 3)
                {
                    var esquina = NAMapa.Pos(k.f0 + 1, k.c0 + 1);
                    if (NAMapa.En(k.f0 + 1, k.c0 + 1) == '.')
                    {
                        NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(esquina.x - 0.4f, y0 + 0.75f, esquina.z + 0.4f), new Vector3(1.2f, 0.08f, 0.8f), NAMat.Hex("#6b4226"));
                        NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(esquina.x - 0.4f, y0 + 0.37f, esquina.z + 0.4f), new Vector3(0.9f, 0.74f, 0.5f), NAMat.Hex("#5a3720"));
                    }
                }
            }
        }

        static void Obstaculo(GameObject g, Vector3 tam, Vector3 centro)
        {
            var col = g.AddComponent<BoxCollider>(); col.size = tam; col.center = centro;
            var obs = g.AddComponent<NavMeshObstacle>(); obs.shape = NavMeshObstacleShape.Box; obs.size = tam; obs.center = centro; obs.carving = true;
        }

        // Puesto del mercado: mesa, postes y toldo de color
        static void Puesto(Transform raiz, Vector3 p, System.Random rnd)
        {
            var g = new GameObject("Puesto"); g.transform.SetParent(raiz); g.transform.position = p;
            g.transform.rotation = Quaternion.Euler(0, rnd.Next(4) * 90f, 0);
            var t = g.transform;
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.9f, 1.2f), NAMat.Hex("#8a5a35"));
            foreach (var x in new[] { -1.05f, 1.05f }) foreach (var z in new[] { -0.55f, 0.55f })
                    NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(x, 1.2f, z), new Vector3(0.07f, 2.4f, 0.07f), NAMat.Hex("#5a3a22"));
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 2.45f, 0), new Vector3(2.5f, 0.06f, 1.6f), NAMat.Hex(Papel[rnd.Next(Papel.Length)]));
            // Frutas aparte (fuera del lote estático): revientan de un tiro
            var frutas = new GameObject("FrutasPuesto").transform;
            frutas.SetPositionAndRotation(g.transform.position, g.transform.rotation);
            string[] fruta = { "#f77f00", "#d62828", "#fcbf49", "#6a994e" };
            string[] pulpa = { "#ffb347", "#ff4d4d", "#ffd166", "#b5e48c" };
            for (int i = 0; i < 6; i++)
            {
                int k = rnd.Next(fruta.Length);
                NAFruta.Crear(frutas, new Vector3(-0.8f + i * 0.32f, 1.02f, (float)rnd.NextDouble() * 0.6f - 0.3f), 0.24f, NAMat.Hex(fruta[k]), NAMat.Hex(pulpa[k]));
            }
            if (rnd.NextDouble() < 0.6) NAFruta.Crear(frutas, new Vector3(0.7f, 1.12f, 0.25f), 0.45f, NAMat.Hex("#2d6a4f"), NAMat.Hex("#ff4d6d")); // sandía
            Obstaculo(g, new Vector3(2.2f, 1.6f, 1.2f), new Vector3(0, 0.8f, 0));
        }

        // Tumba con lápida y flores de cempasúchil
        static void Tumba(Transform raiz, Vector3 p, System.Random rnd)
        {
            var g = new GameObject("Tumba"); g.transform.SetParent(raiz); g.transform.position = p;
            var t = g.transform;
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.25f, 0), new Vector3(1.0f, 0.5f, 2.0f), NAMat.Hex("#8d8a84"));
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.9f, 0.9f), new Vector3(0.9f, 1.1f, 0.18f), NAMat.Hex("#a7a39b"));
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.25f, 0.9f), new Vector3(0.12f, 0.5f, 0.2f), NAMat.Hex("#6d6a64"));
            for (int i = 0; i < 5; i++)
                NAMat.Pieza(PrimitiveType.Sphere, t, new Vector3((float)rnd.NextDouble() * 0.8f - 0.4f, 0.56f, (float)rnd.NextDouble() * 1.4f - 0.7f), Vector3.one * 0.18f, NAMat.Hex("#ff8c00"), default, 0.4f);
            Obstaculo(g, new Vector3(1.0f, 1.2f, 2.0f), new Vector3(0, 0.6f, 0));
        }

        static int lucesFarol;
        static void Farol(Transform raiz, Vector3 p, bool noche)
        {
            var g = new GameObject("Farol"); g.transform.SetParent(raiz); g.transform.position = p;
            NAMat.Pieza(PrimitiveType.Cylinder, g.transform, new Vector3(0, 1.7f, 0), new Vector3(0.12f, 1.7f, 0.12f), NAMat.Hex("#26262a"));
            NAMat.Pieza(PrimitiveType.Cube, g.transform, new Vector3(0, 3.55f, 0), new Vector3(0.4f, 0.5f, 0.4f), NAMat.Hex("#ffcf7a"), default, noche ? 2.5f : 0.8f);
            if (lucesFarol < 6 || !noche)
            {
                var l = new GameObject("Luz").AddComponent<Light>();
                l.transform.SetParent(g.transform, false); l.transform.localPosition = new Vector3(0, 3.3f, 0);
                l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.range = noche ? 11f : 6f; l.intensity = noche ? 1.8f : 0.6f;
                lucesFarol++;
            }
            var col = g.AddComponent<CapsuleCollider>(); col.radius = 0.15f; col.height = 3.6f; col.center = Vector3.up * 1.8f;
        }

        // Campanario junto a la puerta de la iglesia (nivel 4)
        static void Campanario(Transform raiz)
        {
            if (!NAMapa.Buscar('M', out int f, out int col)) return;
            var p = NAMapa.Pos(f - 2, col + 3);
            var g = new GameObject("Campanario").transform; g.SetParent(raiz); g.position = new Vector3(p.x, 0, p.z);
            NAMat.Pieza(PrimitiveType.Cube, g, new Vector3(0, 8f, 0), new Vector3(3.2f, 18f, 3.2f), NAMat.Hex("#e9e2d0"));
            NAMat.Pieza(PrimitiveType.Cube, g, new Vector3(0, 15.2f, 0), new Vector3(3.4f, 2.4f, 3.4f), NAMat.Hex("#2a2420"));
            NAMat.Pieza(PrimitiveType.Cylinder, g, new Vector3(0, 15.1f, 0), new Vector3(1.1f, 0.6f, 1.1f), NAMat.Hex("#b08d3c"), default, 0.4f);
            NAMat.Pieza(PrimitiveType.Cube, g, new Vector3(0, 17.4f, 0), new Vector3(2.4f, 2.4f, 2.4f), NAMat.Hex("#c9543a"), new Vector3(45, 0, 45));
            NAMat.Pieza(PrimitiveType.Cube, g, new Vector3(0, 19.6f, 0), new Vector3(0.18f, 1.6f, 0.18f), NAMat.Hex("#d9c38a"));
            NAMat.Pieza(PrimitiveType.Cube, g, new Vector3(0, 19.9f, 0), new Vector3(0.9f, 0.18f, 0.18f), NAMat.Hex("#d9c38a"));
            // Puerta de la iglesia sobre la salida
            var puerta = NAMapa.Pos(f, col);
            NAMat.Pieza(PrimitiveType.Cube, raiz, new Vector3(puerta.x, puerta.y + 1.6f, puerta.z + NAMapa.C / 2f + 0.05f), new Vector3(2.2f, 3.4f, 0.1f), NAMat.Hex("#3a2416"));
        }

        // Tiras de papel picado entre casas, de lado a lado de las calles
        static void PapelPicado(Transform raiz, System.Random rnd)
        {
            var filas = NAMapa.Filas;
            int tiras = 0;
            for (int intento = 0; intento < 60 && tiras < 7; intento++)
            {
                int f = 1 + rnd.Next(filas.Length - 2);
                int col = rnd.Next(filas[f].Length);
                bool Tope(char ch) => NAMapa.EsMuro(ch) || NACasas.EsLinea(ch);
                if (Tope(filas[f][col]) || NAMapa.CasaEn(NAMapa.Pos(f, col)) >= 0) continue;
                int izq = col, der = col;
                while (izq > 0 && !Tope(filas[f][izq - 1])) izq--;
                while (der < filas[f].Length - 1 && !Tope(filas[f][der + 1])) der++;
                if (der - izq < 2 || der - izq > 12) continue;
                var a = NAMapa.Pos(f, izq) - Vector3.right * NAMapa.C / 2f;
                var b = NAMapa.Pos(f, der) + Vector3.right * NAMapa.C / 2f;
                float largo = Vector3.Distance(a, b);
                int n = Mathf.FloorToInt(largo / 0.55f);
                for (int i = 1; i < n; i++)
                {
                    float t = i / (float)n;
                    var q = Vector3.Lerp(a, b, t);
                    q.y = 4.3f - Mathf.Sin(t * Mathf.PI) * 0.6f + Mathf.Max(a.y, b.y);
                    NAMat.Pieza(PrimitiveType.Cube, raiz, q, new Vector3(0.36f, 0.32f, 0.01f), NAMat.Hex(Papel[rnd.Next(Papel.Length)]));
                }
                tiras++;
            }
        }
    }
}
