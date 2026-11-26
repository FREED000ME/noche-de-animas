# noche-de-animas
Noche de Ánimas: supervivencia y horror para Android. De día los muertos duermen; de noche te buscan.

## Noche de Ánimas: shooter de oleadas en primera persona (v0.3)

Código propio en `Assets/NocheDeAnimas/Scripts` (todo hecho con piezas y sonidos sintetizados, 0 créditos):

- **Primera persona** (`NACamaraFP`): mira con el dedo (mitad derecha) o con el ratón; balanceo, retroceso y linterna de noche.
- **Armas caseras** (`NATirador`, `NAModelosArma`): se empieza con la resortera. Tiros a la cabeza ×2, ayuda de puntería ligera.
- **Armas por piezas** (`NAArmado`): cada mapa esconde 3 piezas por arma (números 1-3 y 4-6 del mapa). En la mesa de armado (W) se mantiene ACCIÓN para armarla; ahí también se compra munición (500 pts). Las armas se conservan entre mapas.
- **Puntos y poderes** (`NAPoderes`): +10 por acierto, +60 por muerte, +100 a la cabeza. Los zombies sueltan MUNICIÓN MÁXIMA, INSTA-KILL (30 s), DOBLE PUNTOS (30 s) y CALAVERA (caen todos).
- **Oleadas infinitas** (`NAOleadas`): cada una trae más zombies, más tipos y +10 % de vida.
- **Misión por pasos** (`NAMision`): la salida (M) está cerrada hasta conseguir la llave (letras a-e y Z del mapa):
  1. Mercado: manivela → cortina de la bodega → calendario (combinación) → caja fuerte.
  2. Cañada: 3 garrafas → generador → aguantar 30 s → llave del fondo del pozo.
  3. Callejones: encender 3 faroles → cruz de plata → puerta del cura.
  4. Atrio: pan, vela y foto → aguantar en el altar 35 s → matar al Sacristán.
- **Pueblo** (`NAEscenografia`, `tools/mapas.py`): casas de adobe, puestos, tumbas, faroles, papel picado e iglesia con campanario, levantados sobre el mapa de texto. `python3 tools/mapas.py` regenera los JSON de nivel y `NAMapas.cs`.
- Controles táctiles: DISPARAR (mantener) · RECARGAR · ARMA · ESQUIVAR · CORRER · ACCIÓN (mantener). PC: ratón · clic · R · Tab o 1-6 · F.
