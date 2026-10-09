using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Player builds (menu "Idle Grounds/Build/..."): Windows x64 → Builds/Windows/IdleGrounds.exe,
    /// WebGL → Builds/WebGL (gzip + decompression fallback so it runs from any static host).
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
