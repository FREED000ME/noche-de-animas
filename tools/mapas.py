# Noche de Ánimas — fuente única de los mapas.
# Genera los niveles de Forja3D (Assets/Forja3D/Niveles/*.json) y una copia para el juego
# (Assets/NocheDeAnimas/Scripts/NAMapas.cs), que usan la escenografía y las misiones.
#
# Símbolos:
#   #  casa (muro)            X  iglesia (muro)          P jugador   M salida (portón/puerta)
#   W  mesa de armado         S  puesto del mercado      Y tumba     F farol     C suministros
#   T  árbol   R  roca        1-6 piezas de armas        a-f puntos de la misión   Z aparición del jefe
#   B  bodega con cortina de metal (muro)
# Uso: python3 tools/mapas.py
import json, os
from collections import deque

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUROS = set("#XB")
OBSTACULOS = set("SY")

NIVELES = [
  dict(id="01_01-mercado", titulo="Día 1: El mercado", ambiente="selva", niebla=False, cielo="#f2b27a",
       terreno=dict(tipo="arena", colinas=0.7, semilla=2), altura_muros=0.3, color_muros="#a0785a",
       capitulo="Día 1",
       narracion=["San Ánimas del Cerro, 2 de noviembre de 1994. Los muertos no volvieron a sus tumbas.",
                  "El portón del mercado está cerrado con candado. La llave la guardaba don Chema en la bodega.",
                  "Junta piezas para armar algo mejor que una resortera. Los muertos no avisan: solo atacan."],
       transicion="Con la llave del portón saliste del mercado. Abajo espera la cañada.",
       mapa=[
"#########################",
"#P..W....######....a....#",
"#........######.........#",
"#..F.....bBBBBB...SS.SS.#",
"###...........F...SS.SS.#",
"#1....SS.SS.............#",
"#.....SS.SS....####..4..#",
"#..............####.....#",
"#####.....F.....c##..F..#",
"#####...SS.SS.......SS..#",
"#..2....SS.SS....5..SS..#",
"#...........###.........#",
"#..F...d....###....F..6.#",
"#.......3...###.........#",
"#....C..........C.......#",
"#..................F...M#",
"#########################"]),
  dict(id="02_02-canada", titulo="Día 2: La cañada", ambiente="selva", niebla=False, cielo="#e9a46a",
       terreno=dict(tipo="piedra", colinas=0.9, semilla=7), altura_muros=3, color_muros="#7d6a58",
       capitulo="Día 2",
       narracion=["La cañada se inundó. La llave de la mina quedó en el fondo del pozo.",
                  "Junta gasolina, llena el generador de la bomba y aguanta mientras saca el agua."],
       transicion="El sol se esconde detrás del cerro. Esta noche toca cruzar los callejones.",
       mapa=[
"#########################",
"#P.W.....T....##....a...#",
"#........R....##........#",
"###..T......#####...T...#",
"###.....a...#####.......#",
"#....##..............R..#",
"#.1..##....T....##......#",
"#........b......##..4...#",
"#..T.......R............#",
"####..........T....##...#",
"####...2....##.....##.5.#",
"#...........##..........#",
"#..R...T.......a....T...#",
"#.....3...c.........R...#",
"#...##.........6....##..#",
"#...##..T...........##.M#",
"#########################"]),
  dict(id="03_03-primera-noche", titulo="Noche 2: Los callejones", ambiente="tormenta", niebla=True, cielo="#1d2440",
       terreno=dict(tipo="piedra", colinas=0.6, semilla=11), altura_muros=0.3, color_muros="#5a5560",
       capitulo="Noche 2",
       narracion=["De noche los callejones se llenan de niebla y de gritos.",
                  "Enciende los faroles, busca la cruz de plata en el panteón y abre la casa del cura: él tiene la llave de la sacristía."],
       transicion="Desde la capilla se oye una campana. Alguien la está tocando en el atrio.",
       mapa=[
"#########################",
"#P.W..#######...........#",
"#.....#######..a..####..#",
"#..F..............####..#",
"####...####.....#######.#",
"####...####..1..#######.#",
"#.........b.............#",
"#..####.......####...F..#",
"#..####...F...####......#",
"#.........####.....#..2.#",
"#.YY.YY...####..d###....#",
"#.........####..####..F.#",
"#.YY.YY.e...............#",
"#.......c....###...3....#",
"#..F........####........#",
"#...........####......M.#",
"#########################"]),
  dict(id="04_04-atrio", titulo="Noche de Ánimas: El atrio", ambiente="infierno", niebla=True, cielo="#2a1830",
       terreno=dict(tipo="piedra", colinas=0.8, semilla=31), altura_muros=0.3, color_muros="#6a5a50",
       capitulo="Noche de Ánimas",
       narracion=["El Sacristán toca la campana y los muertos acuden a su llamado.",
                  "Junta la ofrenda (el pan, la vela y la foto), ponla en el altar y aguanta. Cuando salga, acaba con él: lleva la llave del campanario."],
       transicion="La campana calla. Por primera vez en siete noches, San Ánimas duerme en silencio.",
       mapa=[
"#########################",
"#XXXXXXXXXXXXXXXXXXXX...#",
"#XXXXXXXXXXXXXXXXXXXX.a.#",
"#XXXXXXXXXXXXXXXXXXXX...#",
"#.........M.............#",
"#..F......d.......F.....#",
"#.YY.YY.......Z...YY.YY.#",
"#.......................#",
"#.YY.YY...........YY.YY.#",
"#b......................#",
"#.YY.YY..F.....F..YY.YY.#",
"#.......................#",
"#####.................c.#",
"#####..W...........######",
"#....................####",
"#P.........C.........####",
"#########################"]),
]

def validar(n):
    m = n["mapa"]; ancho = len(m[0])
    for i, f in enumerate(m):
        assert len(f) == ancho, f'{n["id"]}: fila {i} mide {len(f)} (debe medir {ancho})'
    todo = "".join(m)
    assert todo.count("P") == 1 and todo.count("M") == 1, f'{n["id"]}: debe haber 1 P y 1 M'
    pos = {(r, c): ch for r, f in enumerate(m) for c, ch in enumerate(f)}
    inicio = next(k for k, v in pos.items() if v == "P")
    vistos = {inicio}; q = deque([inicio])
    while q:
        r, c = q.popleft()
        for dr, dc in ((1,0),(-1,0),(0,1),(0,-1)):
            k = (r+dr, c+dc)
            if k in pos and k not in vistos and pos[k] not in MUROS and pos[k] not in OBSTACULOS:
                vistos.add(k); q.append(k)
    for k, v in pos.items():
        if v not in MUROS and v not in OBSTACULOS and v not in ".":
            assert k in vistos, f'{n["id"]}: "{v}" en {k} no es alcanzable'
    # Los puntos de interacción junto a muros (cortina, puerta) deben tener un muro al lado
    return True

def generar():
    os.makedirs(f"{RAIZ}/Assets/Forja3D/Niveles", exist_ok=True)
    for n in NIVELES:
        validar(n)
        d = dict(titulo=n["titulo"], celda=3, mapa=n["mapa"], objetivo="meta", ambiente=n["ambiente"],
                 niebla=n["niebla"], cielo=n["cielo"], terreno=n["terreno"], altura_muros=n["altura_muros"],
                 color_muros=n["color_muros"], jugador=dict(vida=10),
                 leyenda=[dict(simbolo="X", tipo="muro"), dict(simbolo="B", tipo="muro")],
                 capitulo=n["capitulo"], narracion=[dict(quien="", texto=t) for t in n["narracion"]],
                 transicion=n["transicion"])
        with open(f'{RAIZ}/Assets/Forja3D/Niveles/{n["id"]}.json', "w", encoding="utf-8") as f:
            json.dump(d, f, ensure_ascii=False, indent=2)
    lineas = ["// GENERADO por tools/mapas.py — no editar a mano.", "namespace NocheDeAnimas", "{",
              "    public static class NAMapas", "    {", "        public const float Celda = 3f;",
              "        public static string[] Mapa(string escena)", "        {"]
    for n in NIVELES:
        filas = ", ".join(json.dumps(f) for f in n["mapa"])
        lineas.append(f'            if (escena.StartsWith("{n["id"][:2]}")) return new[] {{ {filas} }};')
    lineas += ["            return null;", "        }", "    }", "}", ""]
    with open(f"{RAIZ}/Assets/NocheDeAnimas/Scripts/NAMapas.cs", "w", encoding="utf-8") as f:
        f.write("\n".join(lineas))
    print("Mapas generados:", ", ".join(n["id"] for n in NIVELES))

if __name__ == "__main__":
    generar()
