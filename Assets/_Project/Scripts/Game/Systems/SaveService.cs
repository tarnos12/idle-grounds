using System;
using System.IO;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGrounds.Game
{
    /// <summary>
    /// JSON save file (ADR 0001 "Saves", engine-systems §1.4): <c>persistentDataPath/idle-grounds-save.json</c>,
    /// written atomically (temp file + replace). <see cref="LoadOrNull"/> runs from GameRunner.Awake before the
    /// simulation exists: a corrupt / newer-schema file is renamed to <c>.bak-&lt;timestamp&gt;</c> and the run
    /// starts fresh. Cadence: autosave every 5 s, on pause / quit, after an
    /// ascension and when the welcome modal closes (callers use <see cref="Save"/>). Reset deletes the file,
    /// blocks saving until the scene reloads (JS saveDisabled), then reloads the scene.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class SaveService : MonoBehaviour
    {
        public const string FileName = "idle-grounds-save.json";
        public const float AutosaveSeconds = 5f;

        [SerializeField] GameRunner runner;

        public static SaveService Instance { get; private set; }
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Set by Reset: nothing is written again until the scene reloads.</summary>
        public bool SavingDisabled { get; private set; }
        public int SaveCount { get; private set; }
        public string LastError { get; private set; }
        /// <summary>Why the last load fell back to a fresh run (null = loaded / no file).</summary>
        public static string LastLoadNote { get; private set; }

        float autosaveAt;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GameRunner.Instance;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ load

        /// <summary>
        /// The saved state, or null for a fresh run (no file, or a bad file that was backed up).
        /// Never throws.
        /// </summary>
        public static GameState LoadOrNull(GameConfig cfg, double nowMs)
        {
            LastLoadNote = null;
            string path = SavePath;
            try
            {
                if (!File.Exists(path))
                {
                    // a crash between "write temp" and "replace" leaves only the temp file
                    string tmp = path + ".tmp";
                    if (!File.Exists(tmp)) return null;
                    File.Move(tmp, path);
                }
                string json = File.ReadAllText(path);
                var s = Simulation.LoadJson(json, cfg, nowMs, out string reason);
                if (s != null) return s;
                Backup(path, reason);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                try { if (File.Exists(path)) Backup(path, e.Message); } catch { /* keep booting */ }
                return null;
            }
        }

        static void Backup(string path, string reason)
        {
            string bak = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(path, bak);
            LastLoadNote = reason;
            Debug.LogWarning("[SaveService] Save could not be loaded (" + reason + ") — backed up to " + bak + ", starting fresh.");
        }

        // ------------------------------------------------------------------ save

        /// <summary>Write the save now (atomic). False when disabled or failed.</summary>
        public bool Save()
        {
            if (SavingDisabled || runner == null || runner.Sim == null) return false;
            try
            {
                WriteAtomic(SavePath, runner.Sim.SaveJson());
                SaveCount++;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogException(e);
                return false;
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void IG_SyncFs();   // Plugins/WebGL/IdbSync.jslib
#endif

        public static void WriteAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL's virtual filesystem (IndexedDB-backed) has no File.Replace: every save after the first
            // would throw. Writes there are already all-or-nothing per sync, so write the file directly.
            File.WriteAllText(path, text);
            IG_SyncFs();    // flush the in-memory FS to IndexedDB now (otherwise the save is lost on reload)
            return;
#else
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
#endif
        }

        /// <summary>Reset button: delete the save, stop saving, reload the scene (fresh run).</summary>
        public void ResetAll()
        {
            SavingDisabled = true;
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(SavePath + ".tmp")) File.Delete(SavePath + ".tmp");
#if UNITY_WEBGL && !UNITY_EDITOR
                IG_SyncFs();    // make the delete stick in IndexedDB too
#endif
            }
            catch (Exception e) { Debug.LogException(e); }
            ReloadScene();
        }

        public static void ReloadScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) { SceneManager.LoadScene(scene.buildIndex); return; }
#if UNITY_EDITOR
            // not in the build list (editor Play mode): load by asset path
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(0);
#endif
        }

        // ------------------------------------------------------------------ cadence

        void Update()
        {
            if (runner == null || runner.Sim == null) { autosaveAt = Time.unscaledTime + AutosaveSeconds; return; }
            if (Time.unscaledTime >= autosaveAt)
            {
                autosaveAt = Time.unscaledTime + AutosaveSeconds;
                Save();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();
    }
}
