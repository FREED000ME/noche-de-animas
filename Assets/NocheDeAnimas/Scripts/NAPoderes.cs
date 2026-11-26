// Noche de Ánimas — puntos y poderes que sueltan los zombies al morir:
//   MUNICIÓN MÁXIMA · INSTA-KILL (30 s) · DOBLE PUNTOS (30 s) · CALAVERA (todos los zombies caen)
// Los puntos se ganan acertando y matando, y se gastan en la mesa de armado (munición).
using System.Collections.Generic;
using Forja3D;
using UnityEngine;

namespace NocheDeAnimas
{
    public static class NAPuntos
    {
        public static int Puntos { get; private set; }
        public static int Matados { get; private set; }
        public static void Reiniciar() { Puntos = 500; Matados = 0; }
        static int Mult => NAPoderes.DoblePuntos ? 2 : 1;
        public static void Acierto() => Puntos += 10 * Mult;
        public static void Muerte(bool cabeza) { Matados++; Puntos += (cabeza ? 100 : 60) * Mult; }
        public static void Sumar(int n) => Puntos += n * Mult;
        public static bool Gastar(int n) { if (Puntos < n) return false; Puntos -= n; return true; }
    }

    public enum TipoPoder { MunicionMaxima, InstaKill, DoblePuntos, Calavera }

    public class NAPoderes : MonoBehaviour
    {
        public const float Duracion = 30f;
        static float instaHasta, dobleHasta, proximaCaida;
        public static bool InstaKill => Time.time < instaHasta;
        public static bool DoblePuntos => Time.time < dobleHasta;
        public static float RestaInsta => Mathf.Max(0f, instaHasta - Time.time);
        public static float RestaDoble => Mathf.Max(0f, dobleHasta - Time.time);
        static readonly List<NAPoderes> enSuelo = new List<NAPoderes>();

        TipoPoder tipo; float muere; Transform icono;

        public static void Reiniciar() { instaHasta = dobleHasta = 0f; proximaCaida = Time.time + 20f; enSuelo.Clear(); }

        public static void TalVezSoltar(Vector3 pos)
        {
            if (Time.time < proximaCaida || enSuelo.Count >= 2 || Random.value > 0.06f) return;
            proximaCaida = Time.time + 15f;
            Soltar((TipoPoder)Random.Range(0, 4), pos);
        }

        public static void Soltar(TipoPoder t, Vector3 pos)
        {
            var g = new GameObject("Poder_" + t);
            g.transform.position = pos + Vector3.up * 1.1f;
            var p = g.AddComponent<NAPoderes>();
            p.tipo = t; p.muere = Time.time + 25f;
            p.icono = new GameObject("Icono").transform; p.icono.SetParent(g.transform, false);
            Color brillo;
            switch (t)
            {
                case TipoPoder.MunicionMaxima:
                    brillo = NAMat.Hex("#4cff6a");
                    NAMat.Pieza(PrimitiveType.Cube, p.icono, Vector3.zero, new Vector3(0.6f, 0.4f, 0.4f), NAMat.Hex("#3d5a2a"));
                    for (int i = 0; i < 3; i++) NAMat.Pieza(PrimitiveType.Cylinder, p.icono, new Vector3(-0.18f + i * 0.18f, 0.32f, 0), new Vector3(0.1f, 0.12f, 0.1f), NAMat.Hex("#e0b040"), default, 1.5f);
                    break;
                case TipoPoder.InstaKill:
                    brillo = NAMat.Hex("#ff3b30");
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, Vector3.zero, new Vector3(0.55f, 0.6f, 0.55f), NAMat.Hex("#f2efe6"), default, 0.4f);
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, new Vector3(-0.12f, 0.05f, 0.22f), Vector3.one * 0.16f, NAMat.Hex("#ff3b30"), default, 3f);
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, new Vector3(0.12f, 0.05f, 0.22f), Vector3.one * 0.16f, NAMat.Hex("#ff3b30"), default, 3f);
                    break;
                case TipoPoder.DoblePuntos:
                    brillo = NAMat.Hex("#ffd23f");
                    NAMat.Pieza(PrimitiveType.Cylinder, p.icono, new Vector3(-0.17f, 0, 0), new Vector3(0.42f, 0.04f, 0.42f), NAMat.Hex("#ffd23f"), new Vector3(90, 0, 0), 1.5f);
                    NAMat.Pieza(PrimitiveType.Cylinder, p.icono, new Vector3(0.17f, 0.1f, -0.05f), new Vector3(0.42f, 0.04f, 0.42f), NAMat.Hex("#ffd23f"), new Vector3(90, 0, 0), 1.5f);
                    break;
                default:
                    brillo = NAMat.Hex("#ff8c00");
                    // Calavera de azúcar
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, Vector3.zero, new Vector3(0.6f, 0.62f, 0.55f), NAMat.Hex("#ffffff"), default, 0.3f);
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, new Vector3(-0.13f, 0.06f, 0.22f), Vector3.one * 0.18f, NAMat.Hex("#ff8c00"), default, 2f);
                    NAMat.Pieza(PrimitiveType.Sphere, p.icono, new Vector3(0.13f, 0.06f, 0.22f), Vector3.one * 0.18f, NAMat.Hex("#ff8c00"), default, 2f);
                    NAMat.Pieza(PrimitiveType.Cube, p.icono, new Vector3(0, -0.18f, 0.24f), new Vector3(0.3f, 0.05f, 0.04f), NAMat.Hex("#9b5de5"), default, 1.5f);
                    break;
            }
            var luz = g.AddComponent<Light>(); luz.type = LightType.Point; luz.color = brillo; luz.range = 5f; luz.intensity = 2.2f;
            enSuelo.Add(p);
        }

        void OnDestroy() => enSuelo.Remove(this);

        void Update()
        {
            if (!icono) return;
            icono.localRotation = Quaternion.Euler(0, Time.time * 120f, 0);
            icono.localPosition = Vector3.up * Mathf.Sin(Time.time * 3f) * 0.12f;
            float resta = muere - Time.time;
            if (resta < 6f) icono.gameObject.SetActive(Mathf.Repeat(Time.time * (resta < 2.5f ? 8f : 4f), 1f) > 0.35f); // parpadea antes de irse
            if (resta <= 0f) { Destroy(gameObject); return; }
            var j = NAJugador.Transform;
            if (j && Vector3.Distance(j.position + Vector3.up, transform.position) < 1.8f) Tomar();
        }

        void Tomar()
        {
            NASonido.Sonar("poder", null, 0.9f);
            Forja3DFX.Almas(transform.position, 20);
            switch (tipo)
            {
                case TipoPoder.MunicionMaxima:
                    if (NATirador.Instancia) NATirador.Instancia.LlenarTodo();
                    Forja3DGameManager.Mensaje("¡MUNICIÓN MÁXIMA!");
                    break;
                case TipoPoder.InstaKill:
                    instaHasta = Time.time + Duracion;
                    Forja3DGameManager.Mensaje("¡INSTA-KILL!");
                    break;
                case TipoPoder.DoblePuntos:
                    dobleHasta = Time.time + Duracion;
                    Forja3DGameManager.Mensaje("¡DOBLE PUNTOS!");
                    break;
                case TipoPoder.Calavera:
                    Forja3DGameManager.Mensaje("¡CALAVERA! LOS MUERTOS VUELVEN A SU TUMBA");
                    Forja3DFX.Temblor(0.4f, 0.6f);
                    NASonido.Sonar("explosion", null, 0.8f, 0.7f);
                    foreach (var z in new List<NAZombie>(NAZombie.Vivos))
                    {
                        if (!z || z.Muerto || z.esJefe) continue;
                        var h = z.GetComponent<Forja3DHealth>();
                        if (h) h.Danio(99999, null);
                    }
                    NAPuntos.Sumar(400);
                    break;
            }
            Destroy(gameObject);
        }
    }
}
