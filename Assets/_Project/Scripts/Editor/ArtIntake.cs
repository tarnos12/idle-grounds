using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IdleGrounds.Game.Data;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Art intake (docs/art/ART-SPEC.md). `node tools/art-intake/pull.js` drops delivered art in Assets/_Project/Art/Incoming/&lt;category&gt;/;
    /// this class (1) applies the spec import rules to everything there (AssetPostprocessor) and (2) wires it into the data assets
    /// via the menu "Idle Grounds/Art/Integrate Incoming Art".
    /// Island / sky / bridge art REPLACES the placeholder by copying the delivered bytes over the placeholder file of the same name
    /// (Art/Islands|Sky|Bridges) - the placeholder's .meta (GUID + slicing) is kept, so RuleTiles/materials/prefabs that reference it
    /// pick the new art up without any re-wiring. IslandArtBuilder only writes missing files, so it never overwrites delivered art.
    /// </summary>
    public class ArtIntake : AssetPostprocessor
    {
        public const string IncomingDir = "Assets/_Project/Art/Incoming/";
        const string DataRoot = "Assets/_Project/Data/";
        static readonly string[] PlaceholderDirs =
            { "Assets/_Project/Art/Islands/", "Assets/_Project/Art/Sky/", "Assets/_Project/Art/Bridges/" };

        // ---------------------------------------------------------------- naming
        static readonly Regex StripRx = new Regex(@"_(\d+)x(\d+)_(\d+)f$", RegexOptions.Compiled);
        static readonly Regex SizeOnlyRx = new Regex(@"_(\d+)x(\d+)$", RegexOptions.Compiled);

        /// <summary>"fox_idle_32x32_4f" -> key "fox_idle", frames 4, frame size 32x32. Static art -> frames 1.</summary>
        public static string ParseName(string baseName, out int frames, out int w, out int h)
        {
            frames = 1; w = h = 0;
            var m = StripRx.Match(baseName);
            if (m.Success) { w = int.Parse(m.Groups[1].Value); h = int.Parse(m.Groups[2].Value); frames = int.Parse(m.Groups[3].Value); return baseName.Substring(0, m.Index); }
            m = SizeOnlyRx.Match(baseName);
            if (m.Success) { w = int.Parse(m.Groups[1].Value); h = int.Parse(m.Groups[2].Value); return baseName.Substring(0, m.Index); }
            return baseName;
        }

        static string CategoryOf(string path)
        {
            string rest = path.Substring(IncomingDir.Length);
            int i = rest.IndexOf('/');
            return i < 0 ? "" : rest.Substring(0, i);
        }

        static bool IsIncomingArt(string path) =>
            path.StartsWith(IncomingDir, StringComparison.OrdinalIgnoreCase) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            CategoryOf(path).Length > 0 && !CategoryOf(path).StartsWith("_");

        static bool BottomPivot(string category, string baseName) =>
            category == "buildings" || category == "nodes" || category == "fixtures" || category == "creatures";

        static bool WantsRepeat(string category, string baseName) =>
            category == "sky" || baseName.Contains("_ground_") || baseName.Contains("_cliff_") || baseName.Contains("_tiles") ||
            baseName.Contains("_veil_") || baseName.Contains("_trail") || baseName.Contains("_water");

        // ---------------------------------------------------------------- import rules
        void OnPreprocessTexture()
        {
            if (!IsIncomingArt(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string cat = CategoryOf(assetPath);
            string bn = Path.GetFileNameWithoutExtension(assetPath);
            ParseName(bn, out int frames, out int fw, out int fh);
            bool bottom = BottomPivot(cat, bn);

            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 32;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.wrapMode = WantsRepeat(cat, bn) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 8192;

            if (frames > 1 && fw > 0 && fh > 0 && TryReadPngSize(assetPath, out int tw, out int th))
            {
                ti.spriteImportMode = SpriteImportMode.Multiple;
                int cols = Mathf.Max(1, tw / fw), rows = Mathf.Max(1, th / fh);
                var list = new List<SpriteMetaData>();
                int n = 0;
                for (int r = 0; r < rows && n < frames; r++)
                    for (int c = 0; c < cols && n < frames; c++, n++)
                        list.Add(new SpriteMetaData
                        {
                            name = bn + "_" + n,
                            rect = new Rect(c * fw, th - (r + 1) * fh, fw, fh),
                            alignment = (int)(bottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center),
                            pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f),
                        });
                if (n < frames) Debug.LogWarning($"ArtIntake: {assetPath}: {frames} frames declared but the {tw}x{th} canvas only fits {n} of {fw}x{fh}.");
#pragma warning disable CS0618
                ti.spritesheet = list.ToArray();
#pragma warning restore CS0618
            }
            else
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteAlignment = (int)(bottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                s.spritePivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                ti.SetTextureSettings(s);
            }
        }

        static bool TryReadPngSize(string assetPath, out int w, out int h)
        {
            w = h = 0;
            try
            {
                using (var fs = File.OpenRead(assetPath))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) < 24 || b[1] != 'P') return false;
                    w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return w > 0 && h > 0;
                }
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- sprite lookup (shared with DataImporter)
        /// <summary>key -> sprite (frame 0 for strips) for every delivered file. Keys are the stripped names ("node_bush", "enemy_fox_idle", "dragon_sleeping"); "_1x1" nodes are also registered under the bare key.</summary>
        public static Dictionary<string, Sprite> LoadIncomingSprites() => LoadIncoming(out _);

        public static Dictionary<string, Sprite> LoadIncoming(out Dictionary<string, Sprite[]> frameMap)
        {
            var map = new Dictionary<string, Sprite>();
            frameMap = new Dictionary<string, Sprite[]>();
            if (!Directory.Exists(IncomingDir)) return map;
            foreach (var file in Directory.GetFiles(IncomingDir, "*.png", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!IsIncomingArt(path)) continue;
                var sprites = new List<Sprite>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) sprites.Add(sp);
                if (sprites.Count == 0) continue;
                sprites.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
                string key = ParseName(Path.GetFileNameWithoutExtension(path), out _, out _, out _);
                Register(map, frameMap, key, sprites);
                if (key.EndsWith("_1x1")) Register(map, frameMap, key.Substring(0, key.Length - 4), sprites);
            }
            return map;
        }

        static void Register(Dictionary<string, Sprite> map, Dictionary<string, Sprite[]> fm, string key, List<Sprite> s)
        { map[key] = s[0]; fm[key] = s.Count > 1 ? s.ToArray() : null; }

        static int FrameIndex(string spriteName)
        {
            int i = spriteName.LastIndexOf('_');
            return i >= 0 && int.TryParse(spriteName.Substring(i + 1), out int n) ? n : 0;
        }

        /// <summary>Exact key, else "key_idle" (entities: enemy_fox -> enemy_fox_idle).</summary>
        public static Sprite Find(Dictionary<string, Sprite> map, string key)
        {
            if (map == null) return null;
            if (map.TryGetValue(key, out var s)) return s;
            return map.TryGetValue(key + "_idle", out s) ? s : null;
        }

        static string Resolved(Dictionary<string, Sprite> map, string key) => map.ContainsKey(key) ? key : key + "_idle";

        // ---------------------------------------------------------------- menu: wire delivered art
        [MenuItem("Idle Grounds/Art/Integrate Incoming Art")]
        public static void Integrate()
        {
            AssetDatabase.Refresh();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DataRoot + "GameDatabase.asset");
            if (db == null) { Debug.LogError("ArtIntake: GameDatabase.asset missing - run Idle Grounds/Data/Import From JSON first."); return; }
            var map = LoadIncoming(out var frameMap);
            int wired = 0, replaced = 0, warnings = 0;
            var used = new HashSet<string>();

            // 1) placeholder replacement (islands / sky / bridges), by identical file name
            var placeholders = new Dictionary<string, string>();
            foreach (var dir in PlaceholderDirs)
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                        placeholders[Path.GetFileName(f)] = f.Replace('\\', '/');
            if (Directory.Exists(IncomingDir))
                foreach (var file in Directory.GetFiles(IncomingDir, "*.png", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (!IsIncomingArt(path)) continue;
                    if (!placeholders.TryGetValue(Path.GetFileName(path), out var target)) continue;
                    if (!FilesEqual(path, target))
                    {
                        File.Copy(path, target, true);
                        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                        replaced++; Debug.Log("ArtIntake: replaced placeholder " + target);
                    }
                    used.Add(ParseName(Path.GetFileNameWithoutExtension(path), out _, out _, out _));
                }

            // 2) data assets
            foreach (var a in db.items)
                if (a && Set(map, "item_" + a.def.key, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;

            foreach (var a in db.buildings)
                if (a && Set(map, "bld_" + a.def.key, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;

            // building variants: longest building key that prefixes "bld_<key>_<variant>"
            foreach (var kv in map)
            {
                if (!kv.Key.StartsWith("bld_")) continue;
                BuildingAsset best = null;
                foreach (var b in db.buildings)
                    if (b && kv.Key.StartsWith("bld_" + b.def.key + "_") && (best == null || b.def.key.Length > best.def.key.Length)) best = b;
                if (best == null) continue;
                string variant = kv.Key.Substring(("bld_" + best.def.key + "_").Length);
                frameMap.TryGetValue(kv.Key, out var fr);
                var existing = best.variants.Find(v => v.key == variant);
                if (existing != null && existing.sprite == kv.Value) { used.Add(kv.Key); continue; }
                best.variants.RemoveAll(v => v.key == variant);
                best.variants.Add(new SpriteEntry { key = variant, sprite = kv.Value, frames = fr });
                EditorUtility.SetDirty(best); used.Add(kv.Key); wired++;
            }

            foreach (var r in db.regions)
            {
                if (!r) continue;
                bool dirty = false;
                foreach (var e in r.sprites)
                {
                    string lookup = e.key;
                    if (e.key == "enemy" && r.def.enemies.enabled) lookup = EnemyKey(r.def.enemies.name);
                    else if (e.key == "enemy_bait" && r.def.enemies.baitSpawn.enabled) lookup = EnemyKey(r.def.enemies.baitSpawn.name);
                    var s = Find(map, lookup);
                    if (s == null) continue;
                    string rk = Resolved(map, lookup);
                    frameMap.TryGetValue(rk, out var fr);
                    if (e.sprite != s || e.frames != fr) { e.sprite = s; e.frames = fr; dirty = true; wired++; }
                    used.Add(rk);
                }
                if (Set(map, "ui_area_" + r.def.key, used, s => { if (r.icon == s) return false; r.icon = s; dirty = true; return true; })) wired++;
                if (Set(map, "ui_action_" + r.def.key, used, s => { if (r.actionIcon == s) return false; r.actionIcon = s; dirty = true; return true; })) wired++;
                if (dirty) EditorUtility.SetDirty(r);
            }
            for (int i = 0; i < db.dragonStages.Count; i++)
            {
                var a = db.dragonStages[i]; if (!a) continue;
                if (Set(map, i == 0 ? "dragon_sleeping" : "dragon_awake", used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            }
            foreach (var a in db.upgrades) if (a && Set(map, "upg_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.perks) if (a && Set(map, "perk_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.vows) if (a && Set(map, "ui_vow_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.quests) if (a && Set(map, "ui_quest_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            if (Set(map, "ui_alert", used, s => { if (db.missingSprite == s) return false; db.missingSprite = s; EditorUtility.SetDirty(db); return true; })) wired++;

            // 3) leftovers: kept in Art/Incoming, no consumer yet -> warning (never an error)
            foreach (var key in map.Keys)
            {
                if (used.Contains(key) || used.Contains(key + "_idle")) continue;
                if (key.EndsWith("_1x1") && used.Contains(key.Substring(0, key.Length - 4))) continue;
                if (map.ContainsKey(key + "_1x1") && used.Contains(key)) continue;
                warnings++;
                Debug.LogWarning($"ArtIntake: '{key}' has no wiring target yet (kept in Art/Incoming; unknown or not-yet-consumed key).");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"ArtIntake: {wired} assignment(s) changed, {replaced} placeholder file(s) replaced, {warnings} unwired key(s) of {map.Count} delivered.");
        }

        static bool Set(Dictionary<string, Sprite> map, string key, HashSet<string> used, Func<Sprite, bool> assign)
        {
            var s = Find(map, key);
            if (s == null) return false;
            used.Add(Resolved(map, key));
            return assign(s);
        }

        static string EnemyKey(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("fox")) return "enemy_fox";
            if (n.Contains("boar")) return "enemy_boar";
            return "enemy_" + n;
        }

        static bool FilesEqual(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            var x = File.ReadAllBytes(a); var y = File.ReadAllBytes(b);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
