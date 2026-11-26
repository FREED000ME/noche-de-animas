// Noche de Ánimas — armas por piezas. Cada mapa esconde las piezas de una o dos armas caseras
// (números 1-3 y 4-6 del mapa). Con las tres piezas, en la mesa de armado (W) se mantiene ACCIÓN
// para construirla. En la mesa también se compra munición con puntos.
// Las armas construidas se quedan para los mapas siguientes.
using System.Collections.Generic;
using Forja3D;
using UnityEngine;

namespace NocheDeAnimas
{
    public class NAArmado : MonoBehaviour
    {
        public static NAArmado Inst { get; private set; }
        public const int PrecioMunicion = 500;
        const string Clave = "na_armas";

        class Pieza { public string arma, nombre; public Vector3 pos; public GameObject go; public bool tengo; }

        readonly List<Pieza> piezas = new List<Pieza>();
        readonly List<string> armasDelMapa = new List<string>();
        Vector3 mesa; bool hayMesa;
        float avance;

        public string Indicacion { get; private set; } = "";
        public float Progreso { get; private set; } = -1f;

        static readonly Dictionary<string, string[]> NombresPiezas = new Dictionary<string, string[]>
        {
            { "hechiza", new[] { "CAÑÓN", "GATILLO", "CACHA" } },
            { "tubo", new[] { "TUBO GRUESO", "CULATA", "CINTA" } },
            { "ballesta", new[] { "VARILLA", "CUERDA", "TABLA" } },
            { "clavos", new[] { "LANZACLAVOS ROTO", "BATERÍA", "CINTA" } },
            { "cohetero", new[] { "TUBO DE CARTÓN", "COHETES", "MECHA" } },
        };

        void Awake() => Inst = this;
        void OnDestroy() { if (Inst == this) Inst = null; }

        // ── Persistencia ──
        public static List<string> Construidas()
        {
            var l = new List<string>();
            foreach (var s in PlayerPrefs.GetString(Clave, "").Split(',')) if (s.Length > 0 && !l.Contains(s)) l.Add(s);
            return l;
        }
        public static void Olvidar() { PlayerPrefs.DeleteKey(Clave); PlayerPrefs.Save(); }
        static void Guardar(string id)
        {
            var l = Construidas(); if (l.Contains(id)) return;
            l.Add(id); PlayerPrefs.SetString(Clave, string.Join(",", l)); PlayerPrefs.Save();
        }

        public void Iniciar(NivelConfig cfg)
        {
            var hechas = Construidas();
            var m = NAMapa.Todas('W');
            if (m.Count > 0) { mesa = m[0]; hayMesa = true; CrearMesa(mesa); }
            Preparar(cfg.armaA, "123", hechas);
            Preparar(cfg.armaB, "456", hechas);
        }

        void Preparar(string arma, string digitos, List<string> hechas)
        {
            if (string.IsNullOrEmpty(arma)) return;
            armasDelMapa.Add(arma);
            bool hecha = hechas.Contains(arma);
            var nombres = NombresPiezas.TryGetValue(arma, out var n) ? n : new[] { "PIEZA", "PIEZA", "PIEZA" };
            for (int i = 0; i < 3; i++)
            {
                var pos = NAMapa.Todas(digitos[i]);
                if (pos.Count == 0) continue;
                var p = new Pieza { arma = arma, nombre = nombres[i], pos = pos[0], tengo = hecha };
                if (!hecha) p.go = CrearPieza(p.pos, i);
                piezas.Add(p);
            }
        }

        public bool Completa(string arma)
        {
            bool alguna = false;
            foreach (var p in piezas) if (p.arma == arma) { alguna = true; if (!p.tengo) return false; }
            return alguna;
        }

        // Texto para el HUD: piezas que lleva de cada arma que falta construir
        public string ListaPiezas()
        {
            var sb = new System.Text.StringBuilder();
            var tir = NATirador.Instancia;
            foreach (var a in armasDelMapa)
            {
                if (tir && tir.Tiene(a)) continue;
                int tengo = 0, total = 0;
                foreach (var p in piezas) if (p.arma == a) { total++; if (p.tengo) tengo++; }
                if (total == 0) continue;
                var def = NADatos.Buscar(a);
                sb.Append(def != null ? def.nombre : a).Append("  ").Append(tengo).Append('/').Append(total);
                if (tengo == total) sb.Append("  → MESA DE ARMADO");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        void Update()
        {
            Indicacion = ""; Progreso = -1f;
            var j = NAJugador.Transform; var tir = NATirador.Instancia;
            if (!j || !tir || Forja3DGameManager.Bloqueado) { avance = 0f; return; }

            // Recoger piezas
            foreach (var p in piezas)
            {
                if (p.tengo || Plano(j.position, p.pos) > 1.7f) continue;
                p.tengo = true;
                if (p.go) Destroy(p.go);
                NASonido.Sonar("recoger", p.pos, 0.9f, 1.2f);
                var def = NADatos.Buscar(p.arma);
                Forja3DGameManager.Mensaje(Completa(p.arma)
                    ? $"¡TIENES TODO PARA {(def != null ? def.nombre : p.arma)}! LLÉVALO A LA MESA"
                    : $"PIEZA: {p.nombre}");
            }

            if (!hayMesa || Plano(j.position, mesa) > 3f) { avance = 0f; return; }

            // ¿Hay un arma lista para armar?
            string lista = null;
            foreach (var a in armasDelMapa) if (!tir.Tiene(a) && Completa(a)) { lista = a; break; }
            bool accion = Forja3DInput.Accion;
            if (lista != null)
            {
                var def = NADatos.Buscar(lista);
                Indicacion = $"Mantén ACCIÓN: armar {(def != null ? def.nombre : lista)}";
                if (accion) { avance += Time.deltaTime; Progreso = Mathf.Clamp01(avance / 2.5f); } else avance = 0f;
                if (avance >= 2.5f)
                {
                    avance = 0f;
                    Guardar(lista);
                    tir.AgregarArma(lista, true);
                    NASonido.Sonar("construir", mesa, 1f);
                    Forja3DFX.Chispas(mesa + Vector3.up * 1.1f, 25);
                    Forja3DGameManager.Mensaje($"¡ARMASTE {(def != null ? def.nombre : lista)}!");
                }
                return;
            }

            bool faltan = false;
            foreach (var a in armasDelMapa) if (!tir.Tiene(a)) faltan = true;
            if (tir.TieneArmasConMunicion)
            {
                Indicacion = NAPuntos.Puntos >= PrecioMunicion
                    ? $"Mantén ACCIÓN: llenar munición ({PrecioMunicion} puntos)"
                    : $"Munición: {PrecioMunicion} puntos (tienes {NAPuntos.Puntos})";
                if (faltan) Indicacion += "\nBusca las piezas que faltan por el mapa";
                if (accion && NAPuntos.Puntos >= PrecioMunicion) { avance += Time.deltaTime; Progreso = Mathf.Clamp01(avance / 1f); } else avance = 0f;
                if (avance >= 1f && NAPuntos.Gastar(PrecioMunicion))
                {
                    avance = 0f;
                    tir.LlenarTodo();
                    NASonido.Sonar("recarga", mesa, 1f);
                    Forja3DGameManager.Mensaje("MUNICIÓN LLENA");
                }
            }
            else Indicacion = faltan ? "Mesa de armado: busca las tres piezas de un arma" : "Mesa de armado";
        }

        static float Plano(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        // ── Piezas visuales ──
        GameObject CrearPieza(Vector3 pos, int i)
        {
            var g = new GameObject("PiezaArma");
            g.transform.position = pos + Vector3.up * 0.5f;
            var cuerpo = new GameObject("Cuerpo").transform; cuerpo.SetParent(g.transform, false);
            var gris = NAMat.Hex("#7a7f86"); var madera = NAMat.Hex("#8a5a32");
            switch (i)
            {
                case 0: NAMat.Pieza(PrimitiveType.Cylinder, cuerpo, Vector3.zero, new Vector3(0.1f, 0.3f, 0.1f), gris, new Vector3(90, 0, 0)); break;
                case 1: NAMat.Pieza(PrimitiveType.Cube, cuerpo, Vector3.zero, new Vector3(0.12f, 0.22f, 0.2f), gris); NAMat.Pieza(PrimitiveType.Cube, cuerpo, new Vector3(0, -0.1f, 0.05f), new Vector3(0.04f, 0.12f, 0.04f), NAMat.Hex("#333333")); break;
                default: NAMat.Pieza(PrimitiveType.Cube, cuerpo, Vector3.zero, new Vector3(0.12f, 0.18f, 0.4f), madera); break;
            }
            NAMat.Pieza(PrimitiveType.Cylinder, cuerpo, new Vector3(0, -0.42f, 0), new Vector3(0.7f, 0.01f, 0.7f), new Color(1f, 0.7f, 0.2f), default, 2.5f); // aro en el suelo
            var l = new GameObject("Brillo").AddComponent<Light>();
            l.transform.SetParent(g.transform, false); l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.3f); l.range = 3f; l.intensity = 1.8f;
            g.AddComponent<NAGirar>();
            return g;
        }

        void CrearMesa(Vector3 pos)
        {
            var g = new GameObject("MesaDeArmado"); g.transform.position = pos;
            var t = g.transform;
            var madera = NAMat.Hex("#6b4226");
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 0.9f, 0), new Vector3(2.0f, 0.1f, 1.0f), madera);
            for (int i = 0; i < 4; i++) NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0.45f, i < 2 ? -0.42f : 0.42f), new Vector3(0.1f, 0.9f, 0.1f), madera);
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(-0.5f, 1.0f, 0.1f), new Vector3(0.35f, 0.1f, 0.2f), NAMat.Hex("#555b60")); // tornillo de banco
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0.3f, 0.97f, -0.1f), new Vector3(0.5f, 0.04f, 0.08f), NAMat.Hex("#9aa0a6")); // segueta
            NAMat.Pieza(PrimitiveType.Cylinder, t, new Vector3(0.7f, 1.0f, 0.2f), new Vector3(0.18f, 0.06f, 0.18f), NAMat.Hex("#c0c0c0")); // cinta
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.9f, 0.45f), new Vector3(1.8f, 0.5f, 0.05f), NAMat.Hex("#2d2d2d")); // letrero
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0, 1.9f, 0.42f), new Vector3(1.5f, 0.12f, 0.02f), NAMat.Hex("#ff9f1c"), default, 2f);
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(-0.85f, 1.4f, 0.45f), new Vector3(0.06f, 0.9f, 0.06f), madera);
            NAMat.Pieza(PrimitiveType.Cube, t, new Vector3(0.85f, 1.4f, 0.45f), new Vector3(0.06f, 0.9f, 0.06f), madera);
            var l = new GameObject("Foco").AddComponent<Light>();
            l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(0, 2.4f, 0);
            l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.5f); l.range = 7f; l.intensity = 2f;
            var col = g.AddComponent<BoxCollider>(); col.size = new Vector3(2f, 1f, 1f); col.center = Vector3.up * 0.5f;
            var obs = g.AddComponent<UnityEngine.AI.NavMeshObstacle>(); obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box; obs.size = col.size; obs.center = col.center; obs.carving = true;
        }
    }
}
