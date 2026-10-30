// Forja3D — compilación automática para Android (la llama GitHub Actions / GameCI).
// También: menú Forja3D → Compilar APK (local).
using System;
using System.IO;
using System.Linq;
using Forja3D.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ── EXPANSIÓN FUTURA ───────────────────────────────────────────────────────────
// Hoy solo se compila Android (APK para probar, AAB para Google Play).
// Para añadir plataformas, copiar BuildAndroid() en métodos nuevos que reutilicen
// PrepararTodo() + ConstruirTodos() y cambien solo el destino:
//   · PC Windows: BuildTarget.StandaloneWindows64, salida "build/StandaloneWindows64/Juego.exe".
//       En GitHub Actions: targetPlatform: StandaloneWindows64 (Mono funciona en ubuntu-latest).
//   · PC Linux:   BuildTarget.StandaloneLinux64.
//   · Mac:        BuildTarget.StandaloneOSX (para IL2CPP o firma, runner macos-latest).
//   · iOS:        BuildTarget.iOS genera un proyecto de Xcode; compilarlo y firmarlo exige
//       runner macos-latest, cuenta de Apple Developer y certificados (p. ej. con fastlane).
//       Ajustes: PlayerSettings.iOS.targetOSVersionString, applicationIdentifier para NamedBuildTarget.iOS.
//   · Web:        BuildTarget.WebGL (útil para pruebas en navegador).
//   · Consolas (Switch/Xbox/PlayStation): requieren ser desarrollador aprobado y módulos de Unity
//       bajo NDA; no se pueden compilar en GitHub Actions públicos.
// ──────────────────────────────────────────────────────────────────────────────
public static class Forja3DBuild
{
    const string RutaApp = "ProjectSettings/Forja3DApp.json";
    [Serializable] class DatosApp { public string nombre; public string version; public int codigo = 1; }
    // Identidad fija del juego (Assets/Forja3D/App): el id de paquete NO puede cambiar después de publicar en Google Play
    const string RutaIdentidad = "Assets/Forja3D/App/identidad.json", RutaIcono = "Assets/Forja3D/App/icono.png";
    [Serializable] class Identidad { public string paquete; public string empresa; }

    static string Arg(string nombre, string porDefecto)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, nombre);
        return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : porDefecto;
    }

    [MenuItem("Forja3D/Compilar APK")]
    static void Menu() => BuildAndroid();

    public static void BuildAndroid()
    {
        try
        {
            bool aab = Arg("-formato", "apk") == "aab";
            string ext = aab ? ".aab" : ".apk";
            string salida = Arg("-customBuildPath", "build/Android/juego" + ext);
            if (salida.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || salida.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
                salida = Path.ChangeExtension(salida, ext);
            else salida = Path.Combine(salida, "juego" + ext);
            // Nombre/versión: los escribe el workflow en ProjectSettings/Forja3DApp.json
            var app = File.Exists(RutaApp) ? JsonUtility.FromJson<DatosApp>(File.ReadAllText(RutaApp)) : new DatosApp();
            string nombreApp = string.IsNullOrWhiteSpace(app.nombre) ? "Juego Forja3D" : app.nombre;
            string version = string.IsNullOrWhiteSpace(app.version) ? "0.1.0" : app.version;
            int codigo = Mathf.Max(1, app.codigo);

            Debug.Log("[Forja3D] 1/3 Preparando modelos…");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Forja3DImporter.PrepararTodo();

            Debug.Log("[Forja3D] 2/3 Construyendo niveles…");
            var escenas = Forja3DLevelBuilder.ConstruirTodos();
            EditorBuildSettings.scenes = escenas.Select(e => new EditorBuildSettingsScene(e, true)).ToArray();

            Debug.Log("[Forja3D] 3/3 Compilando APK…");
            var t = NamedBuildTarget.Android;
            PlayerSettings.productName = nombreApp;
            var id = File.Exists(RutaIdentidad) ? JsonUtility.FromJson<Identidad>(File.ReadAllText(RutaIdentidad)) : new Identidad();
            PlayerSettings.companyName = string.IsNullOrWhiteSpace(id.empresa) ? "Forja3D" : id.empresa;
            var slug = new string(nombreApp.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(slug) || char.IsDigit(slug[0])) slug = "juego" + slug;
            PlayerSettings.SetApplicationIdentifier(t, string.IsNullOrWhiteSpace(id.paquete) ? "com.forja3d." + slug : id.paquete.Trim());
            // Icono de la app (1024x1024). Futuro iOS/PC: el mismo icono sirve con NamedBuildTarget.iOS / Standalone.
            var icono = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaIcono);
            if (icono != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icono }, IconKind.Any);
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.01f, 0.01f);
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = codigo;
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // Google Play exige apuntar a Android 16 (API 36) desde el 31-ago-2026
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            // Texturas comprimidas en ASTC (menos peso y memoria en móvil)
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = aab;
            if (aab)
            {
                // Firma con la clave de subida (el workflow decodifica el keystore desde los secretos de GitHub)
                string ks = Path.GetFullPath(Arg("-keystorePath", "android.keystore"));
                if (!File.Exists(ks)) throw new Exception("Falta el keystore para firmar el AAB. Agrega los secretos ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME y ANDROID_KEYALIAS_PASS.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = Arg("-keystorePass", "");
                PlayerSettings.Android.keyaliasName = Arg("-keyaliasName", "");
                PlayerSettings.Android.keyaliasPass = Arg("-keyaliasPass", "");
            }
            else PlayerSettings.Android.useCustomKeystore = false;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(salida)));
            var reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = escenas.ToArray(),
                locationPathName = salida,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var r = reporte.summary;
            Debug.Log($"[Forja3D] Formato: {(aab ? "AAB (Google Play)" : "APK")} · API destino 36");
            Debug.Log($"[Forja3D] Resultado: {r.result} · {r.totalSize / (1024f * 1024f):0.0} MB · {r.totalTime} · errores {r.totalErrors}");
            if (r.result != BuildResult.Succeeded)
            {
                // Vuelca los errores reales de la compilación con la etiqueta [Forja3D] (el conector los lee del log)
                foreach (var paso in reporte.steps)
                    foreach (var msg in paso.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.Log($"[Forja3D] ERROR en '{paso.name}': {msg.content.Replace('\n', ' ').Substring(0, Mathf.Min(400, msg.content.Length))}");
                Salir(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Forja3D] ERROR: " + e);
            Salir(1);
        }
    }

    static void Salir(int codigo)
    {
        if (Application.isBatchMode) EditorApplication.Exit(codigo);
        else if (codigo != 0) EditorUtility.DisplayDialog("Forja3D", "La compilación falló. Revisa la consola.", "OK");
    }
}
