// Forja3D — entrada unificada: teclado/ratón en PC y controles táctiles en Android.
// En móvil el ratón simulado se ignora por completo: cada dedo tiene un solo papel
// (joystick, cámara o botón), así el joystick nunca mueve la cámara.
//
// Acciones de combate:
//   ATACAR  (toque)          → combo de 3 golpes
//   CUBRIR  (mantener)       → bloqueo; pulsarlo justo antes del golpe = PARRY
//   ESQUIVAR (toque)         → voltereta con invulnerabilidad
//   CORRER  (alternar)       → esprint (gasta estamina)
//   SALTAR  (toque)
using UnityEngine;

// ── EXPANSIÓN FUTURA ───────────────────────────────────────────────────────────
// PC / consolas: añadir soporte de mando (gamepad). Opción recomendada: instalar el paquete
//   com.unity.inputsystem (Input System) y poner "Active Input Handling = Both" en Player Settings.
//   Después, leer aquí Gamepad.current: leftStick → Mover, rightStick → Mirar, buttonSouth → Saltar,
//   buttonWest → Atacar, buttonEast → Esquivar, leftShoulder → Cubrir, leftStickButton → Correr.
//   Todo el juego lee la entrada SOLO a través de esta clase, así que basta con cambiar este archivo.
// iOS: funciona igual que Android (EsTactil es true en iPhone/iPad); no requiere cambios aquí.
// ──────────────────────────────────────────────────────────────────────────────
namespace Forja3D
{
    public static class Forja3DInput
    {
        public static Vector2 JoystickTactil;   // lo escribe Forja3DTouchControls
        public static Vector2 MiradaTactil;     // grados acumulados este frame
        public static bool CubrirTactil;        // botón CUBRIR mantenido
        public static bool CorrerActivo;        // botón CORRER (alterna)
        static int saltoFrame = -10, ataqueFrame = -10, esquivaFrame = -10;

        // PC con pantalla táctil también cuenta como táctil.
        public static bool EsTactil => Application.isMobilePlatform || Input.touchCount > 0;

        public static void PulsarSalto() => saltoFrame = Time.frameCount;
        public static void PulsarAtaque() => ataqueFrame = Time.frameCount;
        public static void PulsarEsquiva() => esquivaFrame = Time.frameCount;
        public static void AlternarCorrer() => CorrerActivo = !CorrerActivo;

        public static Vector2 Mover
        {
            get
            {
                var v = JoystickTactil;
                if (!EsTactil)
                {
                    var k = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    if (k.sqrMagnitude > v.sqrMagnitude) v = k;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool Correr => CorrerActivo || (!EsTactil && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
        public static bool Cubrir => CubrirTactil || (!EsTactil && (Input.GetMouseButton(1) && Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q)));

        public static bool Saltar => Pulso(ref saltoFrame) || (!EsTactil && Input.GetKeyDown(KeyCode.Space));
        public static bool Atacar => Pulso(ref ataqueFrame) || (!EsTactil && Input.GetMouseButtonDown(0));
        public static bool Esquivar => Pulso(ref esquivaFrame) || (!EsTactil && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftAlt)));

        public static Vector2 Mirar
        {
            get
            {
                var m = MiradaTactil; MiradaTactil = Vector2.zero;
                if (!EsTactil && Input.GetMouseButton(1) && !Input.GetKey(KeyCode.LeftControl)) m += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
                return m;
            }
        }

        // Un toque vale durante 2 frames (por si el Update del jugador corre antes que el de los controles)
        static bool Pulso(ref int frame)
        {
            if (Time.frameCount - frame > 1) return false;
            frame = -10; return true;
        }

        public static void Reiniciar() { JoystickTactil = MiradaTactil = Vector2.zero; CubrirTactil = CorrerActivo = false; }
    }
}
