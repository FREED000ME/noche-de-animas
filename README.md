# noche-de-animas
Noche de Ánimas: supervivencia y horror para Android. De día los muertos duermen; de noche te buscan.

## Noche de Ánimas: shooter de oleadas

Código propio en `Assets/NocheDeAnimas/Scripts` (todo hecho con piezas y sonidos sintetizados, 0 créditos):

- **Armas caseras** (`NATirador`, `NAModelosArma`): resortera, pistola hechiza, escopeta de tubo, ballesta de varilla, lanzaclavos y cohetero. Munición por tipo, cargador y recarga.
- **Zombies** (`NAZombie`): caminante, corredor, escupidor, compadre, plañidera, cuetero y el Sacristán (jefe). No avisan: solo atacan.
- **Oleadas** (`NAOleadas`): salen de la tierra por todo el mapa; al superarlas se abre la meta.
- **Configuración por nivel** (`NAComun` → `NAConfig`): armas, munición inicial, número de oleadas y tipos de zombie.
- Controles táctiles: DISPARAR (mantener) · RECARGAR · ARMA · ESQUIVAR · CORRER. PC: clic izq. · R · Tab o 1-6.
