// Noche de Ánimas — armas caseras armadas con piezas simples (tubos, madera, cinta, varilla).
// Cada arma apunta hacia +Z local. Sin modelos generados: 0 créditos.
using UnityEngine;

namespace NocheDeAnimas
{
    public static class NAModelosArma
    {
        static readonly Color Madera = NAMat.Hex("#7a5230"), MaderaClara = NAMat.Hex("#a8774a"), Tubo = NAMat.Hex("#6d6f72"),
            Oxido = NAMat.Hex("#8a4b2a"), Cinta = NAMat.Hex("#222222"), CintaPlata = NAMat.Hex("#b9bcc0"), Hule = NAMat.Hex("#8c1c13");

        public static GameObject Crear(string id, Transform mano)
        {
            var raiz = new GameObject("Arma_" + id).transform;
            raiz.SetParent(mano, false);
            switch (id)
            {
                case "resortera": Resortera(raiz); break;
                case "hechiza": Hechiza(raiz); break;
                case "tubo": EscopetaTubo(raiz); break;
                case "ballesta": Ballesta(raiz); break;
                case "clavos": Lanzaclavos(raiz); break;
                case "cohetero": Cohetero(raiz); break;
            }
            return raiz.gameObject;
        }

        static Transform P(PrimitiveType t, Transform r, Vector3 pos, Vector3 esc, Color c, Vector3 rot = default) => NAMat.Pieza(t, r, pos, esc, c, rot);

        // Horqueta de madera con liga de hule
        static void Resortera(Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0, -0.08f, 0.15f), new Vector3(0.05f, 0.1f, 0.05f), Madera);
            P(PrimitiveType.Cylinder, r, new Vector3(-0.06f, 0.06f, 0.18f), new Vector3(0.04f, 0.08f, 0.04f), Madera, new Vector3(0, 0, 25));
            P(PrimitiveType.Cylinder, r, new Vector3(0.06f, 0.06f, 0.18f), new Vector3(0.04f, 0.08f, 0.04f), Madera, new Vector3(0, 0, -25));
            P(PrimitiveType.Cube, r, new Vector3(0, 0.12f, 0.1f), new Vector3(0.17f, 0.015f, 0.015f), Hule);
            P(PrimitiveType.Cube, r, new Vector3(0, 0.12f, 0.02f), new Vector3(0.05f, 0.03f, 0.04f), NAMat.Hex("#5b3b22")); // badana
        }

        // Pistola hechiza: tubo, cacha de madera y cinta
        static void Hechiza(Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.04f, 0.25f), new Vector3(0.05f, 0.2f, 0.05f), Tubo, new Vector3(90, 0, 0));
            P(PrimitiveType.Cube, r, new Vector3(0, -0.06f, 0.06f), new Vector3(0.06f, 0.18f, 0.09f), Madera, new Vector3(-15, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.04f, 0.15f), new Vector3(0.058f, 0.02f, 0.058f), Cinta, new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.04f, 0.3f), new Vector3(0.058f, 0.02f, 0.058f), Cinta, new Vector3(90, 0, 0));
            P(PrimitiveType.Cube, r, new Vector3(0, 0.0f, 0.12f), new Vector3(0.02f, 0.04f, 0.05f), Tubo); // gatillo
        }

        // Escopeta de tubo ("chimenea"): tubo grueso, culata de tabla y cinta plateada
        static void EscopetaTubo(Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.05f, 0.42f), new Vector3(0.08f, 0.38f, 0.08f), Tubo, new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.05f, 0.08f), new Vector3(0.1f, 0.08f, 0.1f), Oxido, new Vector3(90, 0, 0)); // cople
            P(PrimitiveType.Cube, r, new Vector3(0, -0.02f, -0.18f), new Vector3(0.07f, 0.12f, 0.4f), MaderaClara, new Vector3(8, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.05f, 0.3f), new Vector3(0.088f, 0.03f, 0.088f), CintaPlata, new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.05f, 0.55f), new Vector3(0.088f, 0.03f, 0.088f), CintaPlata, new Vector3(90, 0, 0));
        }

        // Ballesta de varilla: culata de madera, arco de varilla corrugada y cuerda
        static void Ballesta(Transform r)
        {
            P(PrimitiveType.Cube, r, new Vector3(0, 0, 0.18f), new Vector3(0.07f, 0.07f, 0.62f), Madera);
            P(PrimitiveType.Cube, r, new Vector3(-0.2f, 0.02f, 0.44f), new Vector3(0.42f, 0.03f, 0.03f), Oxido, new Vector3(0, 20, 0));
            P(PrimitiveType.Cube, r, new Vector3(0.2f, 0.02f, 0.44f), new Vector3(0.42f, 0.03f, 0.03f), Oxido, new Vector3(0, -20, 0));
            P(PrimitiveType.Cube, r, new Vector3(0, 0.03f, 0.3f), new Vector3(0.74f, 0.008f, 0.008f), NAMat.Hex("#d9d2c0")); // cuerda
            P(PrimitiveType.Cube, r, new Vector3(0, 0.05f, 0.35f), new Vector3(0.02f, 0.02f, 0.5f), Oxido); // varilla cargada
        }

        // Lanzaclavos: herramienta amarilla con batería de coche amarrada
        static void Lanzaclavos(Transform r)
        {
            P(PrimitiveType.Cube, r, new Vector3(0, 0.05f, 0.25f), new Vector3(0.1f, 0.14f, 0.42f), NAMat.Hex("#f2b705"));
            P(PrimitiveType.Cube, r, new Vector3(0, -0.08f, 0.1f), new Vector3(0.07f, 0.16f, 0.08f), Cinta);
            P(PrimitiveType.Cube, r, new Vector3(0, -0.02f, 0.4f), new Vector3(0.04f, 0.06f, 0.22f), Tubo); // cargador de clavos
            P(PrimitiveType.Cube, r, new Vector3(0, 0.18f, 0.12f), new Vector3(0.13f, 0.1f, 0.18f), NAMat.Hex("#1c1c1c")); // batería
            P(PrimitiveType.Cube, r, new Vector3(0.04f, 0.24f, 0.08f), new Vector3(0.025f, 0.02f, 0.025f), NAMat.Hex("#c0392b"));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.11f, 0.12f), new Vector3(0.14f, 0.015f, 0.14f), CintaPlata);
        }

        // Cohetero: tubo de cartón pintado con colores de papel picado
        static void Cohetero(Transform r)
        {
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.1f, 0.2f), new Vector3(0.16f, 0.45f, 0.16f), NAMat.Hex("#e85d75"), new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.1f, 0.0f), new Vector3(0.165f, 0.05f, 0.165f), NAMat.Hex("#f4c430"), new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.1f, 0.35f), new Vector3(0.165f, 0.05f, 0.165f), NAMat.Hex("#7b2cbf"), new Vector3(90, 0, 0));
            P(PrimitiveType.Cylinder, r, new Vector3(0, 0.1f, 0.6f), new Vector3(0.165f, 0.04f, 0.165f), NAMat.Hex("#2b9348"), new Vector3(90, 0, 0));
            P(PrimitiveType.Cube, r, new Vector3(0, -0.06f, 0.15f), new Vector3(0.05f, 0.14f, 0.07f), Madera);
        }
    }
}
