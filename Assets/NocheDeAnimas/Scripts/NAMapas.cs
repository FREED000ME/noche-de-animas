// GENERADO por tools/mapas.py — no editar a mano.
namespace NocheDeAnimas
{
    public static class NAMapas
    {
        public const float Celda = 3f;
        public static string[] Mapa(string escena)
        {
            if (escena.StartsWith("01")) return new[] { "#########################", "#P..W....GGGGGG.....a...#", "#........G.c..G.........#", "#..F.....G..d.G..SS.SS..#", "#........GGQGGG..SS.SS..#", "#1...SS.SS..F...........#", "#....SS.SS......HHVHH...#", "#...............H.4.H...#", "#HHVHH....F.....D...V.F.#", "#H...H..SS.SS...HHDHH...#", "#H.2.D..SS.SS...........#", "#H...H.........HHDHH....#", "#HHVHH..F......H.5.V..6.#", "#.......3......V...H....#", "#....C.........HHHHH....#", "#...F...........C....F.M#", "#########################" };
            if (escena.StartsWith("02")) return new[] { "#########################", "#P.W.....T....##....a...#", "#........R....##........#", "###..T......#####...T...#", "###.........#####.......#", "#....HHVHH............R.#", "#.1..V...D..T....##.....#", "#....H.a.H.b.....##..4..#", "#..T.HHVHH.R............#", "####..........T...HHVHH.#", "####...2....##....D...H.#", "#...........##....H.5.V.#", "#..R...T.......a..HHHHH.#", "#.....3...c...........R.#", "#...##.........6........#", "#...##..T........T.....M#", "#########################" };
            if (escena.StartsWith("03")) return new[] { "#########################", "#P.W..HHHHHHH...........#", "#.....H.....D..a..####..#", "#..F..V..1..H.....####..#", "####..HHHVHHH...#######.#", "####............#######.#", "#.........b.............#", "#..HHVHH......HHVHH..F..#", "#..H...D...F..D...H.....#", "#..V...H......H.2.H.....#", "#..HHHHH......HHVHH.....#", "#.........c.......###d..#", "#.YY.YY.e.........###...#", "#.YY.YY...........###.3.#", "#..F....................#", "#.....................M.#", "#########################" };
            if (escena.StartsWith("04")) return new[] { "#########################", "#XXXXXXXXXXXXXXXXXXXX...#", "#XXXXXXXXXXXXXXXXXXXX.a.#", "#XXXXXXXXXXXXXXXXXXXX...#", "#.........M.............#", "#..F......d.......F.....#", "#.YY.YY.......Z...YY.YY.#", "#.......................#", "#.YY.YY...........YY.YY.#", "#b......................#", "#.YY.YY..F.....F..YY.YY.#", "#.......................#", "#####............HHVHHH.#", "#####..W.........D....H.#", "#................H..c.V.#", "#P.........C.....HHHHHH.#", "#########################" };
            return null;
        }

        // Nombre del mapa al que lleva la salida
        public static string Siguiente(string escena)
        {
            if (escena.StartsWith("01")) return "Día 2: La cañada";
            if (escena.StartsWith("02")) return "Noche 2: Los callejones";
            if (escena.StartsWith("03")) return "Noche de Ánimas: El atrio";
            if (escena.StartsWith("04")) return "El amanecer";
            return "El siguiente mapa";
        }
    }
}
