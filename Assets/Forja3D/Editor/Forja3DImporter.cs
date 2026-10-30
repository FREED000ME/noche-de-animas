// Forja3D — importador de modelos para Unity 6.
// Prepara los FBX de Assets/Forja3D/Modelos/<nombre>/ (los sube el conector o este menú):
// rig Humanoid, nombres y bucles de clips, Animator Controller con blend tree y un prefab.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Networking;

namespace Forja3D.EditorTools
{
    [Serializable] public class Archivo { public string nombre; public string url; public string tipo; public string[] clips; }
    [Serializable] public class Manifiesto { public int forja3d; public bool personaje; public string esqueleto; public string rig_type; public bool biblioteca; public Archivo[] archivos; }

    public class Forja3DImporter : EditorWindow
    {
        public const string Raiz = "Assets/Forja3D/Modelos";
        string nombre = "MiPersonaje";
        string json = "";
        Vector2 scroll;

        public static string Limpio(string s) =>
            string.Concat((s ?? "modelo").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));

        public static Manifiesto LeerManifiesto(string carpeta)
        {
            var ruta = Path.Combine(carpeta, "forja3d.json");
            return File.Exists(ruta) ? JsonUtility.FromJson<Manifiesto>(File.ReadAllText(ruta)) : null;
        }

        // ---------- Ventana manual ----------
        [MenuItem("Forja3D/Importar modelo")]
        static void Abrir() => GetWindow<Forja3DImporter>("Forja3D").minSize = new Vector2(420, 320);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Importar desde el conector Forja3D", EditorStyles.boldLabel);
            nombre = EditorGUILayout.TextField("Nombre", nombre);
            EditorGUILayout.LabelField("Manifiesto (la línea JSON que te dio Claude):");
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            json = EditorGUILayout.TextArea(json, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(nombre)))
                if (GUILayout.Button("Descargar e importar", GUILayout.Height(32))) Importar(nombre.Trim(), json.Trim());
        }

        public static void Importar(string nombre, string json)
        {
            Manifiesto m;
            try { m = JsonUtility.FromJson<Manifiesto>(json); }
            catch (Exception e) { EditorUtility.DisplayDialog("Forja3D", "Manifiesto inválido: " + e.Message, "OK"); return; }
            if (m?.archivos == null || m.archivos.Length == 0) { EditorUtility.DisplayDialog("Forja3D", "El manifiesto no tiene archivos.", "OK"); return; }

            string carpeta = $"{Raiz}/{Limpio(nombre)}";
            Directory.CreateDirectory(carpeta);
            File.WriteAllText($"{carpeta}/forja3d.json", json); // antes de importar: el postprocesador lo lee
            try
            {
                for (int i = 0; i < m.archivos.Length; i++)
                {
                    var a = m.archivos[i];
                    if (string.IsNullOrEmpty(a.url)) continue;
                    EditorUtility.DisplayProgressBar("Forja3D", $"Descargando {a.nombre}…", (float)i / m.archivos.Length);
                    using var req = UnityWebRequest.Get(a.url);
                    var op = req.SendWebRequest();
                    while (!op.isDone) System.Threading.Thread.Sleep(50);
                    if (req.result != UnityWebRequest.Result.Success)
                        throw new Exception($"{a.nombre}: {req.error} (¿caducó el enlace? Pide a Claude que use guardar_asset)");
                    File.WriteAllBytes($"{carpeta}/{Limpio(a.nombre)}.fbx", req.downloadHandler.data);
                }
            }
            catch (Exception e) { EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Forja3D", e.Message, "OK"); return; }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = PrepararCarpeta(carpeta, true);
            if (prefab) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
            Debug.Log($"[Forja3D] {nombre} importado en {carpeta}");
        }

        // ---------- Preparación automática (la usa también la compilación en la nube) ----------
        public static void PrepararTodo()
        {
            if (!Directory.Exists(Raiz)) return;
            foreach (var dir in Directory.GetDirectories(Raiz))
            {
                var carpeta = dir.Replace('\\', '/');
                try { PrepararCarpeta(carpeta, false); }
                catch (Exception e) { Debug.LogError($"[Forja3D] No se pudo preparar {carpeta}: {e}"); }
            }
        }

        public static bool EsTripo(Manifiesto m) => m != null && string.Equals(m.esqueleto, "tripo", StringComparison.OrdinalIgnoreCase);

        // Carpetas cuyo manifiesto dice "biblioteca": sus clips se comparten con todos los humanoides
        public static List<string> CarpetasBiblioteca()
        {
            var res = new List<string>();
            if (!Directory.Exists(Raiz)) return res;
            foreach (var d in Directory.GetDirectories(Raiz))
            {
                var m = LeerManifiesto(d);
                if (m != null && m.biblioteca && !EsTripo(m)) res.Add(d.Replace('\\', '/'));
            }
            return res;
        }

        public static GameObject RutaPrefab(string modelo)
        {
            if (string.IsNullOrEmpty(modelo) || !Directory.Exists(Raiz)) return null;
            var dir = Directory.GetDirectories(Raiz).FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), Limpio(modelo), StringComparison.OrdinalIgnoreCase));
            if (dir == null) return null;
            var p = Directory.GetFiles(dir, "*.prefab").FirstOrDefault();
            return p == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'));
        }

        public static GameObject PrepararCarpeta(string carpeta, bool forzar)
        {
            var m = LeerManifiesto(carpeta);
            if (m?.archivos == null) return null;
            string nombre = Path.GetFileName(carpeta);
            string rutaPrefab = $"{carpeta}/{nombre}.prefab";
            if (!forzar && File.Exists(rutaPrefab)) return AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);

            var archivos = m.archivos.Where(a => a != null)
                .Select(a => (a, ruta: $"{carpeta}/{Limpio(a.nombre)}.fbx"))
                .Where(x => File.Exists(x.ruta)).ToList();
            var modelo = archivos.FirstOrDefault(x => x.a.tipo == "modelo").ruta ?? archivos.FirstOrDefault().ruta;
            var go = modelo != null ? AssetDatabase.LoadAssetAtPath<GameObject>(modelo) : null;
            if (!go) { Debug.LogWarning($"[Forja3D] {carpeta}: no hay FBX del modelo."); return null; }

            AnimatorController ctrl = m.personaje ? CrearAnimator(carpeta, archivos, !EsTripo(m)) : null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
            // Texturas incrustadas en el FBX (Meshy/Tripo): se extraen y se arma el material a mano
            var mat = MaterialEmbebido(carpeta, modelo);
            if (mat) foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            var b = Limites(inst);
            if (m.personaje)
            {
                var anim = inst.GetComponent<Animator>();
                if (!anim) anim = inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (EsTripo(m) && !inst.GetComponent<Forja3DEstabilizador>()) inst.AddComponent<Forja3DEstabilizador>();
                var cc = inst.GetComponent<CharacterController>();
                if (!cc) cc = inst.AddComponent<CharacterController>();
                float h = Mathf.Max(b.size.y, 0.5f);
                cc.height = h; cc.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.3f, 0.15f, h / 2f);
                cc.center = new Vector3(0, h / 2f, 0);
            }
            else if (!inst.GetComponent<Collider>())
            {
                var box = inst.AddComponent<BoxCollider>();
                box.center = inst.transform.InverseTransformPoint(b.center); box.size = b.size;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, rutaPrefab);
            DestroyImmediate(inst);
            return prefab;
        }

        // Busca imágenes JPG/PNG guardadas dentro del FBX binario (propiedad 'Content', tipo R + longitud),
        // las clasifica (color / mapa normal / grises) y crea un material Standard con color + relieve.
        static Material MaterialEmbebido(string carpeta, string fbx)
        {
            string dir = $"{carpeta}/Texturas";
            string rutaMat = $"{dir}/{Path.GetFileNameWithoutExtension(fbx)}.mat";
            var previo = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (previo) return previo;
            byte[] d;
            try { d = File.ReadAllBytes(fbx); } catch { return null; }
            var imagenes = new List<(byte[] datos, string ext)>();
            void Buscar(byte[] firma, string ext)
            {
                for (int i = 5; i < d.Length - firma.Length; i++)
                {
                    bool ok = true;
                    for (int k = 0; k < firma.Length; k++) if (d[i + k] != firma[k]) { ok = false; break; }
                    if (!ok || d[i - 5] != (byte)'R') continue;
                    int n = BitConverter.ToInt32(d, i - 4);
                    if (n <= 0 || i + n > d.Length) continue;
                    var b = new byte[n]; Buffer.BlockCopy(d, i, b, 0, n);
                    imagenes.Add((b, ext)); i += n - 1;
                }
            }
            Buscar(new byte[] { 0xFF, 0xD8, 0xFF }, "jpg");
            Buscar(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png");
            if (imagenes.Count == 0) return null;

            byte[] albedo = null, normal = null; string extA = "jpg", extN = "jpg";
            foreach (var (datos, ext) in imagenes)
            {
                var t = new Texture2D(2, 2);
                if (!t.LoadImage(datos)) { UnityEngine.Object.DestroyImmediate(t); continue; }
                float r = 0, g = 0, bl = 0, mn = 1, mx = 0; int c = 0;
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        var p = t.GetPixel(x * t.width / 16, y * t.height / 16);
                        r += p.r; g += p.g; bl += p.b; c++;
                        mn = Mathf.Min(mn, Mathf.Abs(p.r - p.g) + Mathf.Abs(p.g - p.b)); mx = Mathf.Max(mx, Mathf.Abs(p.r - p.g) + Mathf.Abs(p.g - p.b));
                    }
                r /= c; g /= c; bl /= c;
                UnityEngine.Object.DestroyImmediate(t);
                bool esNormal = bl > 0.8f && Mathf.Abs(r - 0.5f) < 0.15f && Mathf.Abs(g - 0.5f) < 0.15f;
                bool gris = mx < 0.04f;
                if (esNormal) { if (normal == null) { normal = datos; extN = ext; } }
                else if (!gris && albedo == null) { albedo = datos; extA = ext; }
            }
            if (albedo == null) foreach (var (datos, ext) in imagenes) if (datos != normal) { albedo = datos; extA = ext; break; }
            if (albedo == null) return null;

            Directory.CreateDirectory(dir);
            string rutaA = $"{dir}/color.{extA}", rutaN = $"{dir}/normal.{extN}";
            File.WriteAllBytes(rutaA, albedo);
            if (normal != null) File.WriteAllBytes(rutaN, normal);
            AssetDatabase.ImportAsset(rutaA, ImportAssetOptions.ForceSynchronousImport);
            if (normal != null) AssetDatabase.ImportAsset(rutaN, ImportAssetOptions.ForceSynchronousImport);
            // Móvil: 1024 px bastan (2048 llenaban la memoria)
            if (AssetImporter.GetAtPath(rutaA) is TextureImporter ia) { ia.maxTextureSize = 1024; ia.sRGBTexture = true; ia.SaveAndReimport(); }
            if (normal != null && AssetImporter.GetAtPath(rutaN) is TextureImporter inn) { inn.textureType = TextureImporterType.NormalMap; inn.maxTextureSize = 1024; inn.SaveAndReimport(); }

            var mat = new Material(Shader.Find("Standard")) { name = Path.GetFileName(carpeta) };
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaA);
            mat.color = Color.white;
            if (normal != null) { mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN)); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
            mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Glossiness", 0.2f);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, rutaMat);
            Debug.Log($"[Forja3D] Material con textura para {Path.GetFileName(carpeta)} ({imagenes.Count} imágenes en el FBX{(normal != null ? ", con relieve" : "")})");
            return mat;
        }

        public static Bounds Limites(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // Animator: locomoción (idle/caminar/correr por "Speed") + triggers de acciones + muerte.
        static AnimatorController CrearAnimator(string carpeta, List<(Archivo a, string ruta)> archivos, bool humanoide)
        {
            var clips = new Dictionary<string, AnimationClip>();
            void Cargar(string ruta, bool sobrescribir)
            {
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__")) continue;
                    var k = c.name.ToLowerInvariant();
                    if (sobrescribir || !clips.ContainsKey(k)) clips[k] = c;
                }
            }
            foreach (var (_, ruta) in archivos) Cargar(ruta, true);
            // Humanoides: completa con los clips de la biblioteca (retargeting de Mecanim, sin gastar créditos)
            if (humanoide)
                foreach (var bib in CarpetasBiblioteca())
                    if (!string.Equals(bib, carpeta, StringComparison.OrdinalIgnoreCase))
                        foreach (var f in Directory.GetFiles(bib, "*.fbx")) Cargar(f.Replace('\\', '/'), false);

            AnimationClip Buscar(params string[] claves) =>
                claves.Select(k => clips.TryGetValue(k, out var exacto) ? exacto
                    : clips.Where(kv => kv.Key.Contains(k)).OrderBy(kv => kv.Key.Length).Select(kv => kv.Value).FirstOrDefault())
                    .FirstOrDefault(c => c != null);

            string rutaCtrl = $"{carpeta}/{Path.GetFileName(carpeta)}_Animator.controller";
            AssetDatabase.DeleteAsset(rutaCtrl);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(rutaCtrl);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            var loco = ctrl.CreateBlendTreeInController("Locomocion", out var arbol);
            arbol.blendParameter = "Speed";
            arbol.useAutomaticThresholds = false;
            var idle = Buscar("idle");
            var caminarParaIdle = idle ? null : Buscar("caminar", "walk");
            if (idle) arbol.AddChild(idle, 0f);
            else if (caminarParaIdle)
            {
                // Sin idle (p. ej. cuadrúpedos de Tripo): caminar casi detenido hace de reposo
                arbol.AddChild(caminarParaIdle, 0f);
                var hijos = arbol.children; hijos[hijos.Length - 1].timeScale = 0.15f; arbol.children = hijos;
            }
            var caminar = Buscar("caminar", "walk"); if (caminar) arbol.AddChild(caminar, 2f);
            var correr = Buscar("correr_rapido", "correr", "run"); if (correr) arbol.AddChild(correr, 5.5f);
            sm.defaultState = loco;

            // Biblioteca de combate (ids de Meshy): 158 voltereta, 138 bloqueo, 147 parry con espada, 178 reacción al golpe,
            // 189 caer de espaldas, 219 tajo derecho, 97 tajo izquierdo, 242 tajo cargado, 187 derribado
            var acciones = new (string trigger, string[] claves)[] {
                ("Jump", new[]{"saltar","jump"}), ("Attack", new[]{"accion_219","ataque","attack"}), ("Hit", new[]{"accion_178","golpeado","hit"}),
                ("Dodge", new[]{"accion_158"}), ("Combo2", new[]{"accion_97"}), ("Combo3", new[]{"accion_242"}), ("KnockDown", new[]{"accion_187"}),
                ("Skill1", new[]{"habilidad_1"}), ("Skill2", new[]{"habilidad_2"}), ("Skill3", new[]{"habilidad_3"}),
                ("Dance", new[]{"bailar"}), ("Alert", new[]{"alerta"}),
            };
            foreach (var (trigger, claves) in acciones)
            {
                var clip = Buscar(claves); if (!clip) continue;
                ctrl.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var st = sm.AddState(trigger); st.motion = clip;
                var ida = sm.AddAnyStateTransition(st);
                ida.AddCondition(AnimatorConditionMode.If, 0, trigger);
                ida.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                ida.duration = 0.1f; ida.canTransitionToSelf = false;
                var vuelta = st.AddTransition(loco); vuelta.hasExitTime = true; vuelta.duration = 0.15f;
                // Reacciones y esquivas cortas: vuelven antes a la locomoción para que el control sea ágil
                vuelta.exitTime = trigger == "Hit" ? 0.6f : trigger == "Dodge" ? 0.8f : trigger.StartsWith("Combo") || trigger == "Attack" ? 0.75f : 0.9f;
                // Ajusta la velocidad del clip a la duración del movimiento en el juego (así el golpe coincide con la animación)
                var duracion = trigger switch { "Attack" => 0.5f, "Combo2" => 0.5f, "Combo3" => 0.7f, "Dodge" => 0.55f, "Hit" => 0.45f, "KnockDown" => 1.4f, _ => 0f };
                if (duracion > 0f && clip.length > 0.05f) st.speed = Mathf.Clamp(clip.length / duracion, 0.6f, 3f);
            }
            if (humanoide) CapaTorso(ctrl, carpeta, Buscar("accion_147", "accion_138"), Buscar("accion_147"));
            var muerte = Buscar("accion_189", "muerte", "dead");
            if (muerte)
            {
                var st = sm.AddState("Muerte"); st.motion = muerte;
                var t = sm.AddAnyStateTransition(st);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dead"); t.duration = 0.1f; t.canTransitionToSelf = false;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // Capa de torso (solo humanoides): cubrirse y parry con los brazos mientras las piernas siguen caminando
        static void CapaTorso(AnimatorController ctrl, string carpeta, AnimationClip bloqueo, AnimationClip parry)
        {
            if (!bloqueo && !parry) return;
            var mascara = new AvatarMask { name = "Torso" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var parte = (AvatarMaskBodyPart)i;
                bool piernas = parte == AvatarMaskBodyPart.LeftLeg || parte == AvatarMaskBodyPart.RightLeg || parte == AvatarMaskBodyPart.Root
                            || parte == AvatarMaskBodyPart.LeftFootIK || parte == AvatarMaskBodyPart.RightFootIK;
                mascara.SetHumanoidBodyPartActive(parte, !piernas);
            }
            AssetDatabase.CreateAsset(mascara, $"{carpeta}/{Path.GetFileName(carpeta)}_Torso.mask");

            ctrl.AddParameter("Block", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Parry", AnimatorControllerParameterType.Trigger);
            // Pose de guardia: el clip de bloqueo se congela en este punto (0-1)
            var ps = ctrl.parameters.ToList();
            ps.Add(new AnimatorControllerParameter { name = "PoseGuardia", type = AnimatorControllerParameterType.Float, defaultFloat = 0.3f });
            ctrl.parameters = ps.ToArray();

            ctrl.AddLayer("Torso");
            var capas = ctrl.layers;
            var capa = capas[capas.Length - 1];
            capa.avatarMask = mascara; capa.defaultWeight = 1f; capa.blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = capas;
            var sm = ctrl.layers[capas.Length - 1].stateMachine;
            var nada = sm.AddState("Nada"); sm.defaultState = nada; nada.writeDefaultValues = false;
            if (bloqueo)
            {
                var cubrir = sm.AddState("Cubrir"); cubrir.motion = bloqueo;
                cubrir.timeParameterActive = true; cubrir.timeParameter = "PoseGuardia";
                var a = nada.AddTransition(cubrir); a.AddCondition(AnimatorConditionMode.If, 0, "Block"); a.duration = 0.08f; a.hasExitTime = false;
                var b = cubrir.AddTransition(nada); b.AddCondition(AnimatorConditionMode.IfNot, 0, "Block"); b.duration = 0.15f; b.hasExitTime = false;
                b.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            if (parry)
            {
                var st = sm.AddState("Parry"); st.motion = parry; st.speed = 1.4f;
                var ida = sm.AddAnyStateTransition(st); ida.AddCondition(AnimatorConditionMode.If, 0, "Parry"); ida.duration = 0.05f; ida.canTransitionToSelf = false;
                var vuelta = st.AddTransition(nada); vuelta.hasExitTime = true; vuelta.exitTime = 0.8f; vuelta.duration = 0.15f;
            }
        }

        // ---------- Escena rápida para probar un personaje en el editor ----------
        [MenuItem("Forja3D/Crear escena de prueba")]
        static void EscenaPrueba()
        {
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo"; suelo.transform.localScale = new Vector3(10, 1, 10);
            if (!UnityEngine.Object.FindObjectOfType<Light>())
            {
                var luz = new GameObject("Sol").AddComponent<Light>();
                luz.type = LightType.Directional; luz.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            var prefab = AssetDatabase.FindAssets("t:Prefab", new[] { Raiz }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (prefab == null) { Debug.Log("[Forja3D] Importa un personaje primero."); return; }
            var p = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            p.tag = "Player";
            if (!p.GetComponent<CharacterController>()) p.AddComponent<CharacterController>();
            if (!p.GetComponent<Forja3DHealth>()) p.AddComponent<Forja3DHealth>().esJugador = true;
            if (!p.GetComponent<Forja3DPlayerController>()) p.AddComponent<Forja3DPlayerController>();
            new GameObject("Forja3D").AddComponent<Forja3DGameManager>();
            Selection.activeObject = p;
        }
    }

    // Configura automáticamente los FBX de Assets/Forja3D/Modelos
    public class Forja3DPostprocessor : AssetPostprocessor
    {
        static readonly string[] EnBucle = { "idle", "caminar", "correr", "walk", "run", "bailar", "combate" };

        // Súbelo cada vez que cambie la forma de importar: Unity solo reimporta los modelos ya importados
        // (la carpeta Library se guarda en caché entre compilaciones) si cambia esta versión.
        public override uint GetVersion() => 9;

        bool EsNuestro => assetPath.StartsWith(Forja3DImporter.Raiz) && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        // Materiales de Meshy/Tripo: textura de color + normal; sin mapa metálico (con poca luz se veían negros/planos)
        void OnPostprocessMaterial(Material m)
        {
            if (!EsNuestro || m == null) return;
            if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
            m.DisableKeyword("_METALLICGLOSSMAP");
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.22f);
            if (m.HasProperty("_GlossMapScale")) m.SetFloat("_GlossMapScale", 0.22f);
            if (m.HasProperty("_Color")) { var c = m.color; m.color = new Color(1, 1, 1, c.a <= 0.01f ? 1f : c.a); }
            if (m.HasProperty("_MainTex") && m.mainTexture == null && m.HasProperty("_BaseMap")) m.mainTexture = m.GetTexture("_BaseMap");
            if (m.HasProperty("_Mode")) { m.SetFloat("_Mode", 0); m.SetOverrideTag("RenderType", ""); m.renderQueue = -1; m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.DisableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 1); if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 0); if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1); }
            m.enableInstancing = true;
        }

        void OnPreprocessModel()
        {
            if (!EsNuestro) return;
            var mi = (ModelImporter)assetImporter;
            var m = Forja3DImporter.LeerManifiesto(Path.GetDirectoryName(assetPath));
            if (m != null && !m.personaje)
            {
                mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
                mi.isReadable = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                return;
            }
            mi.animationType = Forja3DImporter.EsTripo(m) ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
            // (bakeAxisConversion NO: con él los jefes de Tripo quedaban mirando hacia atrás; de pie los mantiene
            // Forja3DEstabilizador)
            mi.bakeAxisConversion = false;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!EsNuestro) return;
            var mi = (ModelImporter)assetImporter;
            var m = Forja3DImporter.LeerManifiesto(Path.GetDirectoryName(assetPath));
            if (m == null || !m.personaje) return;
            var archivo = Path.GetFileNameWithoutExtension(assetPath);
            var a = m.archivos?.FirstOrDefault(x => x != null && Forja3DImporter.Limpio(x.nombre) == archivo);
            if (a == null) return;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            // Tripo exporta los clips por nombre inglés y en orden alfabético (fall, hurt, idle, slash, walk):
            // se renombran por su nombre real, nunca por posición. Ojo: el "hurt" de Tripo es el jefe tirado en el suelo
            // todo el clip (la cabeza queda a ~45 % de su altura): no se usa como reacción al golpe; en su lugar
            // el jefe se sacude (Forja3DEnemy.Sacudida) sin perder la guardia.
            var tripo = new Dictionary<string, string> { ["idle"] = "idle", ["walk"] = "caminar", ["run"] = "correr", ["slash"] = "ataque",
                ["attack"] = "ataque", ["hurt"] = "tumbado", ["hit"] = "golpeado", ["fall"] = "muerte", ["die"] = "muerte", ["death"] = "muerte", ["jump"] = "saltar" };
            string NombreTripo(string n)
            {
                var corto = n.Contains("|") ? n.Substring(n.LastIndexOf('|') + 1) : n;
                corto = corto.ToLowerInvariant().Trim();
                return tripo.TryGetValue(corto, out var es) ? es : null;
            }
            string[] nombres = null;
            if (Forja3DImporter.EsTripo(m) && clips.Any(c => NombreTripo(c.name) != null))
                nombres = clips.Select(c => NombreTripo(c.name) ?? c.name).ToArray();
            else nombres = a.clips != null && a.clips.Length == clips.Length ? a.clips : clips.Length == 1 ? new[] { a.nombre } : null;
            if (nombres == null && a.clips != null && a.clips.Length > 0 && !Forja3DImporter.EsTripo(m))
            {
                // Tripo nombra los clips en lote NlaTrack, NlaTrack.001… en el orden pedido
                var nla = clips.Select((c, idx) => (c, idx)).Where(x => x.c.name.Contains("NlaTrack")).ToList();
                if (nla.Count == a.clips.Length) { nombres = clips.Select(c => c.name).ToArray(); for (int k = 0; k < nla.Count; k++) nombres[nla[k].idx] = a.clips[k]; }
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (nombres != null) clips[i].name = nombres[i];
                var n = clips[i].name.ToLowerInvariant();
                clips[i].loopTime = EnBucle.Any(k => n.Contains(k));
                // Rotación y altura horneadas en la pose; el avance XZ NO (se extrae y se descarta): el movimiento
                // lo da el CharacterController y el cuerpo no "vuelve al punto inicial" al acabar el clip
                clips[i].lockRootRotation = clips[i].lockRootHeightY = !Forja3DImporter.EsTripo(m);
                clips[i].lockRootPositionXZ = false;
                clips[i].keepOriginalOrientation = clips[i].keepOriginalPositionY = clips[i].keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
#endif
