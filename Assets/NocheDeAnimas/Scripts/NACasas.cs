// Noche de Ánimas — casas huecas: paredes delgadas con puertas y ventanas por donde se entra y se sale.
//   H pared de adobe · G pared de la bodega · D puerta · V ventana · Q cortina de la bodega
// La misma geometría la usa el constructor de niveles (para hornear las paredes en el NavMesh)
// y el juego (para dibujarlas). Así las paredes que se ven son exactamente las que chocan.
using System.Collections.Generic;
using UnityEngine;

namespace NocheDeAnimas
{
    public static class NACasas
    {
        public const float Grosor = 0.4f, Alto = 3.3f, Hueco = 1.7f, AltoHueco = 2.7f, Alfeizar = 0.5f;

        public static bool EsPared(char ch) => ch == 'H' || ch == 'G';
        public static bool EsHueco(char ch) => ch == 'D' || ch == 'V' || ch == 'Q';
        public static bool EsLinea(char ch) => EsPared(ch) || EsHueco(ch);

        public struct Caja { public Bounds b; public int f, c; public bool dintel; }
        public struct Casa { public int f0, c0, f1, c1; public bool bodega; }

        static char En(string[] m, int f, int c) => f < 0 || f >= m.Length || c < 0 || c >= m[f].Length ? ' ' : m[f][c];
        static Vector3 P(string[] m, int f, int c, float C) => new Vector3(c * C, 0f, (m.Length - 1 - f) * C);

        // ¿La línea de pared en esta celda corre a lo largo de X (este-oeste)?
        public static bool LineaEnX(string[] m, int f, int c)
        {
            bool x = EsLinea(En(m, f, c - 1)) || EsLinea(En(m, f, c + 1));
            bool z = EsLinea(En(m, f - 1, c)) || EsLinea(En(m, f + 1, c));
            return x || !z;
        }

        // Cajas de colisión (centro con y relativa al suelo). Las usa el constructor de niveles.
        public static Bounds[] Colisiones(string[] mapa, float celda)
        {
            var l = Cajas(mapa, celda);
            var r = new Bounds[l.Count];
            for (int i = 0; i < l.Count; i++) r[i] = l[i].b;
            return r;
        }

        public static List<Caja> Cajas(string[] m, float C)
        {
            var l = new List<Caja>();
            if (m == null) return l;
            float g = Grosor / 2f, altoY = Alto / 2f - 0.25f, altoT = Alto + 0.5f;
            int filas = m.Length;

            // Tramos este-oeste de pared
            for (int f = 0; f < filas; f++)
            {
                int cols = m[f].Length;
                for (int c = 0; c < cols; c++)
                {
                    if (!EsPared(m[f][c])) continue;
                    int fin = c;
                    while (fin + 1 < cols && EsPared(m[f][fin + 1])) fin++;
                    bool izq = EsLinea(En(m, f, c - 1)), der = EsLinea(En(m, f, fin + 1));
                    if (fin > c || izq || der)
                    {
                        float x0 = P(m, f, c, C).x + (izq ? -C / 2f : -g), x1 = P(m, f, fin, C).x + (der ? C / 2f : g);
                        var z = P(m, f, c, C).z;
                        l.Add(new Caja { f = f, c = c, b = new Bounds(new Vector3((x0 + x1) / 2f, altoY, z), new Vector3(x1 - x0, altoT, Grosor)) });
                    }
                    c = fin;
                }
            }
            // Tramos norte-sur de pared
            int maxCols = 0; foreach (var fila in m) maxCols = Mathf.Max(maxCols, fila.Length);
            for (int c = 0; c < maxCols; c++)
            {
                for (int f = 0; f < filas; f++)
                {
                    if (!EsPared(En(m, f, c))) continue;
                    int fin = f;
                    while (EsPared(En(m, fin + 1, c))) fin++;
                    bool arriba = EsLinea(En(m, f - 1, c)), abajo = EsLinea(En(m, fin + 1, c));
                    bool sola = fin == f && !arriba && !abajo;
                    bool tieneX = EsLinea(En(m, f, c - 1)) || EsLinea(En(m, f, c + 1));
                    if (!sola || !tieneX)
                    {
                        // fila menor = más al norte (z mayor)
                        float zN = P(m, f, c, C).z + (arriba ? C / 2f : g), zS = P(m, fin, c, C).z - (abajo ? C / 2f : g);
                        var x = P(m, f, c, C).x;
                        l.Add(new Caja { f = f, c = c, b = new Bounds(new Vector3(x, altoY, (zN + zS) / 2f), new Vector3(Grosor, altoT, zN - zS)) });
                    }
                    f = fin;
                }
            }
            // Marcos de puertas y ventanas: relleno a los lados, dintel arriba y alféizar en las ventanas
            for (int f = 0; f < filas; f++)
                for (int c = 0; c < m[f].Length; c++)
                {
                    char ch = m[f][c];
                    if (!EsHueco(ch)) continue;
                    var p = P(m, f, c, C);
                    bool enX = LineaEnX(m, f, c);
                    Vector3 eje = enX ? Vector3.right : Vector3.forward;
                    float relleno = (C - Hueco) / 2f;
                    foreach (int lado in new[] { -1, 1 })
                    {
                        var centro = p + eje * lado * (Hueco / 2f + relleno / 2f) + Vector3.up * altoY;
                        l.Add(new Caja { f = f, c = c, b = new Bounds(centro, Tam(enX, relleno, altoT)) });
                    }
                    float dAlto = Alto - AltoHueco;
                    l.Add(new Caja { f = f, c = c, dintel = true, b = new Bounds(p + Vector3.up * (AltoHueco + dAlto / 2f), Tam(enX, Hueco, dAlto)) });
                    if (ch == 'V')
                        l.Add(new Caja { f = f, c = c, b = new Bounds(p + Vector3.up * ((Alfeizar - 0.3f) / 2f), Tam(enX, Hueco, Alfeizar + 0.3f)) });
                }
            return l;
        }

        static Vector3 Tam(bool enX, float largo, float alto) => enX ? new Vector3(largo, alto, Grosor) : new Vector3(Grosor, alto, largo);

        // Casas: grupos de celdas de pared conectadas → rectángulo de la casa
        public static List<Casa> Casas(string[] m)
        {
            var l = new List<Casa>();
            if (m == null) return l;
            var visto = new HashSet<(int, int)>();
            for (int f = 0; f < m.Length; f++)
                for (int c = 0; c < m[f].Length; c++)
                {
                    if (!EsLinea(m[f][c]) || visto.Contains((f, c))) continue;
                    var casa = new Casa { f0 = f, c0 = c, f1 = f, c1 = c };
                    var cola = new Queue<(int, int)>(); cola.Enqueue((f, c)); visto.Add((f, c));
                    while (cola.Count > 0)
                    {
                        var (a, b) = cola.Dequeue();
                        casa.f0 = Mathf.Min(casa.f0, a); casa.f1 = Mathf.Max(casa.f1, a);
                        casa.c0 = Mathf.Min(casa.c0, b); casa.c1 = Mathf.Max(casa.c1, b);
                        if (En(m, a, b) == 'G' || En(m, a, b) == 'Q') casa.bodega = true;
                        foreach (var (da, db) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            var k = (a + da, b + db);
                            if (EsLinea(En(m, k.Item1, k.Item2)) && visto.Add(k)) cola.Enqueue(k);
                        }
                    }
                    if (casa.f1 - casa.f0 >= 2 && casa.c1 - casa.c0 >= 2) l.Add(casa);
                }
            return l;
        }
    }
}
