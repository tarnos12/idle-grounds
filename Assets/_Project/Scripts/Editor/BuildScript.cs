using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Player builds (menu "Idle Grounds/Build/..."): Windows x64 → Builds/Windows/IdleGrounds.exe,
    /// WebGL → Builds/WebGL (gzip + decompression fallback so it runs from any static host),
    /// Android → Builds/Android/IdleGrounds.apk (landscape phones; IL2CPP ARM64, debug-signed).
    /// Scenes come from the Build Settings list. The product name is kept as-is: it decides the save
    /// folder (persistentDataPath), so changing it would orphan existing saves.
    /// </summary>
    public static class BuildScript
    {
        const string Root = "Builds";

        [MenuItem("Idle Grounds/Build/Windows")]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(Root, "Windows", "IdleGrounds.exe"));

        [MenuItem("Idle Grounds/Build/WebGL")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;     // works on hosts without Content-Encoding headers
            PlayerSettings.WebGL.dataCaching = true;
            Run(BuildTarget.WebGL, BuildTargetGroup.WebGL, Path.Combine(Root, "WebGL"));
        }

        public const string AndroidPackageId = "com.tarnos.idlegrounds";
        const string IconPath = "Assets/_Project/Art/AppIcon/app_icon_512.png";

        [MenuItem("Idle Grounds/Build/Android")]
        public static void BuildAndroid()
        {
            ApplyAndroidSettings();
            EditorUserBuildSettings.buildAppBundle = false;                  // an .apk the owner can sideload
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.development = false;
            Run(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(Root, "Android", "IdleGrounds.apk"));
        }

        /// <summary>
        /// Phone settings (idempotent): landscape only, package id, IL2CPP ARM64, Vulkan + GLES3, min API 25 (Unity 6.3's floor),
        /// target = highest installed SDK, debug keystore, app icon. productName / companyName are NOT touched (they decide
        /// the Windows save folder; on Android the save lives under the package id).
        /// </summary>
        [MenuItem("Idle Grounds/Build/Apply Android Settings")]
        public static void ApplyAndroidSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;      // the highest installed platform
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.renderOutsideSafeArea = true;           // draw behind the notch; MobileLayer insets the HUD
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.useCustomKeystore = false;              // Unity's debug keystore signs the APK
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.maxAspectRatio = 3f;                    // 21:9 and taller foldables still fill the screen
            var icon = EnsureIcon();
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            Debug.Log("BuildScript: Android settings applied (" + AndroidPackageId + ", IL2CPP ARM64, Vulkan + GLES3, landscape L/R)");
        }

        /// <summary>The app icon: the Spirit Tree on the HUD's dark teal, 512x512 pixel art (generated once, then a normal asset).</summary>
        static Texture2D EnsureIcon()
        {
            if (!File.Exists(IconPath))
            {
                string src = "Assets/_Project/Art/Incoming/fixtures/fix_spirittree.png";
                if (!File.Exists(src)) return null;
                var tree = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tree.LoadImage(File.ReadAllBytes(src));
                int s = 512, k = 2;                                       // nearest-neighbour 2x
                var px = new Color32[s * s];
                var top = new Color32(0x2f, 0x5d, 0x62, 255); var bot = new Color32(0x14, 0x24, 0x2c, 255);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++) px[y * s + x] = Color32.Lerp(bot, top, y / (float)(s - 1));
                int tw = tree.width * k, th = tree.height * k;
                int ox = (s - tw) / 2, oy = (s - th) / 2 + 6;
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                    {
                        var c = tree.GetPixel(x / k, y / k);
                        if (c.a < 0.5f) continue;
                        int X = ox + x, Y = oy + y;
                        if (X >= 0 && X < s && Y >= 0 && Y < s) px[Y * s + X] = c;
                    }
                var icon = new Texture2D(s, s, TextureFormat.RGBA32, false);
                icon.SetPixels32(px); icon.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, icon.EncodeToPNG());
                Object.DestroyImmediate(icon); Object.DestroyImmediate(tree);
                AssetDatabase.ImportAsset(IconPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
                if (imp != null) { imp.textureType = TextureImporterType.Default; imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None; imp.isReadable = false; imp.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        [MenuItem("Idle Grounds/Build/Windows + WebGL")]
        public static void BuildAll() { BuildWindows(); BuildWebGL(); }

        static void Run(BuildTarget target, BuildTargetGroup group, string path)
        {
            var scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            var s0 = report.summary;
            string msg = $"BuildScript: {target} {s0.result} → {path} ({s0.totalSize / (1024f * 1024f):0.0} MB, {s0.totalTime.TotalSeconds:0}s, {s0.totalErrors} error(s), {s0.totalWarnings} warning(s))";
            if (s0.result == BuildResult.Succeeded) Debug.Log(msg); else Debug.LogError(msg);
        }
    }
}
