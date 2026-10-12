using System;
using System.IO;
using IdleGrounds.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Headless entry points for the BACKGROUND COPY tools (<c>tools/bg/*</c>, see CLAUDE.md "Background copy").
    /// They are called with <c>Unity.exe -batchmode -projectPath &lt;bg worktree&gt; -executeMethod IdleGrounds.Editor.Batch.X</c>
    /// and always end the editor with <see cref="EditorApplication.Exit"/> (0 = ok, non-zero = failure).
    /// Nothing here is meant to run in the owner's interactive editor (every method refuses outside batch mode).
    ///
    ///  - <see cref="Compile"/>      : exits 0 when the project compiled (the batch run only reaches here if it did).
    ///  - <see cref="RunMenu"/>      : <c>-menu "Idle Grounds/..."</c> [<c>-scene path</c> | <c>-noScene</c>] → ExecuteMenuItem, save scenes + assets.
    ///  - <see cref="Screenshots"/>  : <c>-shotSpec spec.json -shotOut dir</c> → PNGs rendered from the game camera
    ///                                 (mode "play": real Play mode boot via GameRunner; mode "edit": scene as authored).
    /// </summary>
    [InitializeOnLoad]
    public static class Batch
    {
        const string DefaultScene = "Assets/_Project/Scenes/Game.unity";
        const string Tag = "[Batch] ";

        // ------------------------------------------------------------------ helpers

        public static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        public static bool Flag(string name)
        {
            foreach (var s in Environment.GetCommandLineArgs())
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Exit(int code, string msg)
        {
            if (code == 0) Debug.Log(Tag + "RESULT OK: " + msg);
            else Debug.LogError(Tag + "RESULT FAIL(" + code + "): " + msg);
            EditorApplication.Exit(code);
        }

        static bool RequireBatch()
        {
            if (Application.isBatchMode) return true;
            Debug.LogError(Tag + "batch entry points only run headless (tools/bg/*), not in an interactive editor.");
            return false;
        }

        // ------------------------------------------------------------------ compile

        /// <summary>Reaching this method means every assembly compiled (otherwise -executeMethod fails to resolve).</summary>
        public static void Compile()
        {
            if (!RequireBatch()) return;
            if (EditorUtility.scriptCompilationFailed) Exit(1, "script compilation failed");
            else Exit(0, "compiled");
        }

        // ------------------------------------------------------------------ menu

        /// <summary>Runs one editor menu item by path, then saves dirty scenes and assets.</summary>
        public static void RunMenu()
        {
            if (!RequireBatch()) return;
            string path = Arg("-menu");
            if (string.IsNullOrEmpty(path)) { Exit(64, "missing -menu \"<menu path>\""); return; }

            if (!Flag("-noScene"))
            {
                string scene = Arg("-scene") ?? DefaultScene;
                try { EditorSceneManager.OpenScene(scene, OpenSceneMode.Single); Debug.Log(Tag + "opened " + scene); }
                catch (Exception e) { Debug.LogException(e); Exit(1, "could not open scene " + scene); return; }
            }

            int errors = 0, echoed = 0;
            // echo what the menu item logs as "[Batch] | ..." (first line only) so tools/bg/menu.sh can stream it
            void Count(string c, string s, LogType t)
            {
                if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++;
                if (c == null || c.StartsWith(Tag) || echoed >= 300) return;
                int nl = c.IndexOf('\n');
                string line = (t == LogType.Log ? "" : t.ToString().ToUpperInvariant() + ": ") + (nl >= 0 ? c.Substring(0, nl) : c);
                echoed++;
                Console.WriteLine(Tag + "| " + line);   // stdout goes to the -logFile without re-entering this callback
            }
            Application.logMessageReceived += Count;
            bool ok;
            try
            {
                Debug.Log(Tag + "menu: " + path);
                ok = EditorApplication.ExecuteMenuItem(path);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            finally { Application.logMessageReceived -= Count; }

            if (!ok) Exit(1, "menu item missing or failed: " + path);
            else if (errors > 0) Exit(2, "menu item ran but logged " + errors + " error(s): " + path);
            else Exit(0, "menu item ran: " + path);
        }

        // ------------------------------------------------------------------ screenshots

        [Serializable]
        public class ShotSpec
        {
            public string scene = DefaultScene;
            /// <summary>"play" (boot the game like Play mode; needs IDLEGROUNDS_SAVE_DIR) or "edit" (scene as authored).</summary>
            public string mode = "play";
            public int width = 1920, height = 1080;
            /// <summary>Play mode: frames to let the game boot before the first shot.</summary>
            public int warmupFrames = 60;
            /// <summary>Play mode: also draw screen-space UI (canvases switched to Screen Space - Camera).</summary>
            public bool ui;
            /// <summary>Play mode: save file to boot from (copied into IDLEGROUNDS_SAVE_DIR). Empty = fresh run.</summary>
            public string save = "";
            public int timeoutSeconds = 300;
            /// <summary>Hide every locked Island's fog veil while shooting (art review of locked Islands).</summary>
            public bool hideVeils;
            public Shot[] shots = new Shot[0];
        }

        [Serializable]
        public class Shot
        {
            public string name = "shot";
            /// <summary>Island key (center, farm, mine, ...). Empty = absolute x/y world position.</summary>
            public string island = "";
            /// <summary>Fit the whole Island (incl. coast + underside) in view; size is then ignored.</summary>
            public bool fit;
            /// <summary>Offset in world units (cells) from the Island centre, or the absolute position when island is empty.</summary>
            public float x, y;
            /// <summary>Orthographic half-height in cells (0 = 12).</summary>
            public float size;
        }

        const string KActive = "IG.Batch.Shots.Active", KSpec = "IG.Batch.Shots.Spec", KOut = "IG.Batch.Shots.Out",
                     KStart = "IG.Batch.Shots.Start", KPhase = "IG.Batch.Shots.Phase";

        public static void Screenshots()
        {
            if (!RequireBatch()) return;
            string specPath = Arg("-shotSpec"), outDir = Arg("-shotOut");
            if (string.IsNullOrEmpty(specPath) || !File.Exists(specPath)) { Exit(64, "missing/unknown -shotSpec <spec.json>: " + specPath); return; }
            if (string.IsNullOrEmpty(outDir)) { Exit(64, "missing -shotOut <dir>"); return; }
            Directory.CreateDirectory(outDir);
            string json = File.ReadAllText(specPath);
            ShotSpec spec;
            try { spec = JsonUtility.FromJson<ShotSpec>(json); }
            catch (Exception e) { Debug.LogException(e); Exit(65, "bad spec JSON"); return; }
            if (spec.shots == null || spec.shots.Length == 0) { Exit(65, "spec has no shots"); return; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Exit(66, "no graphics device: run without -nographics"); return; }

            try { EditorSceneManager.OpenScene(spec.scene, OpenSceneMode.Single); }
            catch (Exception e) { Debug.LogException(e); Exit(1, "could not open scene " + spec.scene); return; }

            if (spec.mode == "edit")
            {
                int n = 0;
                try { foreach (var s in spec.shots) { if (CaptureShot(spec, s, outDir, false)) n++; } }
                catch (Exception e) { Debug.LogException(e); }
                if (n == spec.shots.Length) Exit(0, n + " screenshot(s) -> " + outDir);
                else Exit(1, (spec.shots.Length - n) + " of " + spec.shots.Length + " screenshot(s) failed");
                return;
            }

            // play mode: never touch the owner's real save
            string saveDir = Environment.GetEnvironmentVariable("IDLEGROUNDS_SAVE_DIR");
            if (string.IsNullOrEmpty(saveDir)) { Exit(67, "refusing Play mode without IDLEGROUNDS_SAVE_DIR (it would use the owner's save)"); return; }
            Directory.CreateDirectory(saveDir);
            string savePath = Path.Combine(saveDir, SaveService.FileName);
            foreach (var f in new[] { savePath, savePath + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            if (!string.IsNullOrEmpty(spec.save))
            {
                if (!File.Exists(spec.save)) { Exit(65, "spec.save not found: " + spec.save); return; }
                File.Copy(spec.save, savePath, true);
                Debug.Log(Tag + "booting from save " + spec.save);
            }
            else Debug.Log(Tag + "booting a fresh run (no save)");

            SessionState.SetBool(KActive, true);
            SessionState.SetString(KSpec, json);
            SessionState.SetString(KOut, outDir);
            SessionState.SetString(KStart, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetInt(KPhase, 0);
            EditorApplication.update += PlayTick;
            Debug.Log(Tag + "entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        // survives the domain reload of entering Play mode
        static Batch()
        {
            if (Application.isBatchMode && SessionState.GetBool(KActive, false))
                EditorApplication.update += PlayTick;
        }

        static int playFrames, shotIndex, settle, captured;
        static ShotSpec playSpec;

        static void PlayTick()
        {
            if (!SessionState.GetBool(KActive, false)) { EditorApplication.update -= PlayTick; return; }
            if (playSpec == null) playSpec = JsonUtility.FromJson<ShotSpec>(SessionState.GetString(KSpec, "{}"));
            long.TryParse(SessionState.GetString(KStart, "0"), out long t0);
            double elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - t0).TotalSeconds;
            if (elapsed > Math.Max(30, playSpec.timeoutSeconds)) { Finish(3, "timed out after " + (int)elapsed + " s (shots: " + captured + ")"); return; }

            int phase = SessionState.GetInt(KPhase, 0);
            if (phase == 2) { if (!EditorApplication.isPlaying) Finish(-1, null); return; }    // waiting for Play mode to stop
            if (!EditorApplication.isPlaying) return;                                           // still entering

            if (phase == 0) { SessionState.SetInt(KPhase, 1); playFrames = 0; shotIndex = 0; settle = -1; captured = 0; }
            if (++playFrames < Math.Max(1, playSpec.warmupFrames)) return;

            try
            {
                if (GameRunner.Instance == null || GameRunner.Instance.Sim == null) { Finish(1, "GameRunner did not boot (see errors above)"); return; }
                var shots = playSpec.shots;
                if (shotIndex >= shots.Length) { Finish(captured == shots.Length ? 0 : 1, captured + "/" + shots.Length + " screenshot(s) -> " + SessionState.GetString(KOut, "")); return; }
                if (settle < 0)
                {
                    // place the camera and point it at an off-screen target the normal player loop renders into
                    // (UI canvases included), then let a few frames run so LateUpdate followers (parallax sky) catch up
                    var cc = UnityEngine.Object.FindFirstObjectByType<CameraController>();
                    if (cc != null) cc.enabled = false;
                    if (!Place(playSpec, shots[shotIndex])) { shotIndex++; return; }
                    BeginLoopCapture(playSpec);
                    settle = 4;
                    return;
                }
                if (--settle > 0) return;
                string outDir = SessionState.GetString(KOut, "");
                bool ok = loopRenders > 0
                    ? SaveRT(loopRT, Path.Combine(outDir, Sanitize(shots[shotIndex].name) + ".png"), shots[shotIndex].name, "player loop")
                    : CaptureShot(playSpec, shots[shotIndex], outDir, true);   // fallback: explicit render request
                if (ok) captured++;
                shotIndex++; settle = -1;
                if (shotIndex >= shots.Length) EndLoopCapture();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1, "exception while capturing"); }
        }

        // ---- play mode: capture what the player loop renders into the camera's target texture

        static RenderTexture loopRT;
        static int loopRenders;
        static Canvas[] switchedCanvases;

        static void CountRender(ScriptableRenderContext ctx, Camera c) { if (c == GameCamera()) loopRenders++; }

        static void BeginLoopCapture(ShotSpec spec)
        {
            var cam = GameCamera();
            int w = Mathf.Max(16, spec.width), h = Mathf.Max(16, spec.height);
            if (loopRT == null || loopRT.width != w || loopRT.height != h)
            {
                if (loopRT != null) { loopRT.Release(); UnityEngine.Object.DestroyImmediate(loopRT); }
                loopRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
                loopRT.Create();
            }
            cam.targetTexture = loopRT;
            cam.aspect = w / (float)h;
            if (spec.hideVeils)
                foreach (var isl in UnityEngine.Object.FindObjectsByType<IdleGrounds.Game.Island>(FindObjectsSortMode.None))
                    if (isl.veil != null) isl.veil.SetActive(false);
            if (spec.ui)
            {
                // re-attach every shot: a camera-space canvas only refits to the camera when it is (re)assigned
                if (switchedCanvases != null) foreach (var c in switchedCanvases) if (c != null) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                switchedCanvases = CanvasesToCamera(cam);
            }
            RenderPipelineManager.endCameraRendering -= CountRender;
            RenderPipelineManager.endCameraRendering += CountRender;
            loopRenders = 0;
        }

        static void EndLoopCapture()
        {
            RenderPipelineManager.endCameraRendering -= CountRender;
            var cam = GameCamera();
            if (cam != null && cam.targetTexture == loopRT) cam.targetTexture = null;
        }

        static bool SaveRT(RenderTexture rt, string file, string name, string how)
        {
            var prev = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                var cam = GameCamera();
                Debug.Log(Tag + "shot " + name + " -> " + file + "  (" + how + ", centre " + cam.transform.position.x.ToString("0.#") + "/" + cam.transform.position.y.ToString("0.#") + ", size " + cam.orthographicSize.ToString("0.##") + ")");
                return true;
            }
            catch (Exception e) { Debug.LogException(e); return false; }
            finally { RenderTexture.active = prev; }
        }

        static int finishCode;
        static string finishMsg;

        static void Finish(int code, string msg)
        {
            if (code >= 0)
            {
                finishCode = code; finishMsg = msg;
                SessionState.SetString("IG.Batch.Shots.Code", code.ToString());
                SessionState.SetString("IG.Batch.Shots.Msg", msg ?? "");
            }
            else
            {
                int.TryParse(SessionState.GetString("IG.Batch.Shots.Code", "1"), out finishCode);
                finishMsg = SessionState.GetString("IG.Batch.Shots.Msg", "");
            }
            if (EditorApplication.isPlaying) { SessionState.SetInt(KPhase, 2); EditorApplication.ExitPlaymode(); return; }
            SessionState.SetBool(KActive, false);
            EditorApplication.update -= PlayTick;
            Exit(finishCode, finishMsg);
        }

        static Camera GameCamera()
        {
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            return cam;
        }

        static Island FindIsland(string key)
        {
            foreach (var r in UnityEngine.Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.islandKey == key) return r;
            return null;
        }

        static bool Place(ShotSpec spec, Shot s)
        {
            var cam = GameCamera();
            if (cam == null) { Debug.LogError(Tag + "no camera in scene"); return false; }
            float aspect = spec.width / (float)Mathf.Max(1, spec.height);
            Vector2 c = new Vector2(s.x, s.y);
            float size = s.size > 0 ? s.size : 12f;
            if (!string.IsNullOrEmpty(s.island))
            {
                var isl = FindIsland(s.island);
                if (isl == null) { Debug.LogError(Tag + "shot '" + s.name + "': no Island '" + s.island + "'"); return false; }
                if (s.fit)
                {
                    var r = isl.VisualRect;
                    c = r.center + new Vector2(s.x, s.y);
                    size = Mathf.Max(r.height * 0.5f, r.width * 0.5f / aspect) * 1.02f;
                }
                else c = isl.Centre + new Vector2(s.x, s.y);
            }
            var p = cam.transform.position;
            cam.transform.position = new Vector3(c.x, c.y, p.z);
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.aspect = aspect;
            return true;
        }

        static bool CaptureShot(ShotSpec spec, Shot s, string outDir, bool alreadyPlaced)
        {
            if (!alreadyPlaced && !Place(spec, s)) return false;
            var cam = GameCamera();
            int w = Mathf.Max(16, spec.width), h = Mathf.Max(16, spec.height);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Canvas[] switched = null;
            try
            {
                cam.targetTexture = rt;
                cam.aspect = w / (float)h;
                if (spec.ui && Application.isPlaying) switched = CanvasesToCamera(cam);

                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                return SaveRT(rt, Path.Combine(outDir, Sanitize(s.name) + ".png"), s.name, "render request");
            }
            catch (Exception e) { Debug.LogException(e); return false; }
            finally
            {
                if (switched != null) foreach (var c in switched) if (c != null) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static Canvas[] CanvasesToCamera(Camera cam)
        {
            var list = new System.Collections.Generic.List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.5f;
                    // the 2D renderer sorts by sorting layer/order, not depth: put the HUD above every world layer
                    var layers = SortingLayer.layers;
                    if (layers.Length > 0) c.sortingLayerID = layers[layers.Length - 1].id;
                    c.sortingOrder = 32000;
                    list.Add(c);
                }
            Canvas.ForceUpdateCanvases();
            foreach (var c in list)
            {
                var rt = (RectTransform)c.transform;
                Debug.Log(Tag + "ui: canvas '" + c.name + "' -> camera space (" + rt.rect.width.ToString("0") + "x" + rt.rect.height.ToString("0") + ", scale " + c.scaleFactor.ToString("0.##") + ", layer " + LayerMask.LayerToName(c.gameObject.layer) + ", cam mask has it: " + ((cam.cullingMask & (1 << c.gameObject.layer)) != 0) + ")");
            }
            return list.ToArray();
        }

        static string Sanitize(string n)
        {
            if (string.IsNullOrEmpty(n)) return "shot";
            foreach (char ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_');
            return n;
        }
    }
}
