// Forja3D — experiencia (XP) y árbol de virtudes de Dante.
// La XP se gana venciendo almas y jefes, con parries y golpes de gracia; cada 120 XP da un punto de virtud.
// Los puntos se gastan en las cuatro virtudes cardinales (3 grados cada una). Se guarda con PlayerPrefs.
// EXPANSIÓN FUTURA: en PC/iOS el mismo guardado funciona; para nube (Google Play Games / iCloud)
// bastaría con serializar este estado en JSON y subirlo.
using System;
using UnityEngine;

namespace Forja3D
{
    public static class Forja3DProgreso
    {
        public const int XPPorPunto = 120;
        public static readonly string[] Ramas = { "Fortaleza", "Templanza", "Prudencia", "Justicia" };
        public static readonly string[] Lemas = { "Resistir el dolor", "Dominar el aliento", "Leer el golpe", "Dar a cada uno lo suyo" };
        public static readonly string[,] Grados =
        {
            { "+1 de vida máxima", "+1 de vida máxima", "El tercer golpe del combo hace el triple de daño" },
            { "+20 de estamina máxima", "La estamina se recupera un 25 % más rápido", "Correr gasta un 30 % menos" },
            { "Ventana de parry más amplia", "Bloquear gasta un 30 % menos", "Esquivar gasta un 25 % menos y te protege más tiempo" },
            { "+10 % de alcance", "El golpe de gracia hace el cuádruple de daño", "Cada 8 golpes certeros recuperas 1 de vida" },
        };

        public static event Action<int, string> AlGanar; // (cantidad, motivo) para el aviso flotante

        public static int XP { get => PlayerPrefs.GetInt("f3d_xp", 0); private set => PlayerPrefs.SetInt("f3d_xp", value); }
        public static int Gastados { get => PlayerPrefs.GetInt("f3d_gastados", 0); private set => PlayerPrefs.SetInt("f3d_gastados", value); }
        public static int Disponibles => Mathf.Max(0, XP / XPPorPunto - Gastados);
        public static int Grado(int rama) => PlayerPrefs.GetInt("f3d_virtud_" + rama, 0);
        public static int XPEnEsteNivel { get; private set; }
        public static void NuevoNivel() => XPEnEsteNivel = 0;

        public static void Ganar(int cantidad, string motivo = "")
        {
            if (cantidad <= 0) return;
            int antes = Disponibles;
            XP += cantidad; XPEnEsteNivel += cantidad;
            PlayerPrefs.Save();
            AlGanar?.Invoke(cantidad, motivo);
            if (Disponibles > antes) Forja3DGameManager.Mensaje("¡NUEVO PUNTO DE VIRTUD!");
        }

        public static bool PuedeComprar(int rama) => rama >= 0 && rama < 4 && Grado(rama) < 3 && Disponibles > 0;
        public static bool Comprar(int rama)
        {
            if (!PuedeComprar(rama)) return false;
            PlayerPrefs.SetInt("f3d_virtud_" + rama, Grado(rama) + 1);
            Gastados += 1;
            PlayerPrefs.Save();
            var p = UnityEngine.Object.FindFirstObjectByType<Forja3DPlayerController>();
            if (p) Aplicar(p, true);
            return true;
        }

        // Valores base del jugador (se guardan la primera vez para poder recalcular al comprar a mitad de nivel)
        static float baseAlcance = -1, baseEstamina, baseRecuperacion, baseCorrer, baseBloqueo, baseEsquiva, baseParry;
        static int baseVida;

        public static void EscalarAlcanceBase(float k) { if (baseAlcance > 0) baseAlcance *= k; }

        public static void Aplicar(Forja3DPlayerController p, bool soloMejora = false)
        {
            var vida = p.GetComponent<Forja3DHealth>();
            if (baseAlcance < 0 || !soloMejora)
            {
                baseAlcance = p.alcanceAtaque; baseEstamina = p.estaminaMaxima; baseRecuperacion = p.recuperacion;
                baseCorrer = p.costoCorrer; baseBloqueo = p.costoBloqueo; baseEsquiva = p.costoEsquiva; baseParry = p.ventanaParry;
                baseVida = vida ? vida.vidaMaxima : 0;
            }
            int f = Grado(0), t = Grado(1), pr = Grado(2), j = Grado(3);
            if (vida) { int extra = Mathf.Min(f, 2); int antes = vida.vidaMaxima; vida.vidaMaxima = baseVida + extra; if (vida.vidaMaxima > antes) vida.Curar(vida.vidaMaxima - antes); }
            p.tercerGolpe = f >= 3 ? 3 : 2;
            p.estaminaMaxima = baseEstamina + (t >= 1 ? 20f : 0f);
            p.recuperacion = baseRecuperacion * (t >= 2 ? 1.25f : 1f);
            p.costoCorrer = baseCorrer * (t >= 3 ? 0.7f : 1f);
            p.ventanaParry = baseParry + (pr >= 1 ? 0.07f : 0f);
            p.costoBloqueo = baseBloqueo * (pr >= 2 ? 0.7f : 1f);
            p.costoEsquiva = baseEsquiva * (pr >= 3 ? 0.75f : 1f);
            p.invulnerableEsquiva = pr >= 3 ? 0.5f : 0.42f;
            p.alcanceAtaque = baseAlcance * (j >= 1 ? 1.1f : 1f);
            p.multGracia = j >= 2 ? 4 : 3;
            p.robaVida = j >= 3;
        }
    }
}
