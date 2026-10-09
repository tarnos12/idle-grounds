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

        // ---------------------------------------------------------------- 9-slice insets (ART-SPEC §5), key -> {L,R,T,B}
        static readonly Dictionary<string, int[]> NineSlice = BuildNineSlice();

        static Dictionary<string, int[]> BuildNineSlice()
        {
            var d = new Dictionary<string, int[]>();
            void Add(int l, int r, int t, int b, params string[] keys) { foreach (var k in keys) d[k] = new[] { l, r, t, b }; }
            Add(20, 20, 18, 18, "ui_panel_scroll");
            Add(16, 16, 16, 16, "ui_panel_jade");
            Add(8, 8, 8, 8, "ui_panel_dark", "ui_tooltip_frame", "ui_handchip_frame",
                "ui_pill_green", "ui_pill_gold", "ui_pill_red", "ui_pill_purple",
                "ui_btn_normal", "ui_btn_hover", "ui_btn_pressed", "ui_btn_disabled",
                "ui_btn_primary_normal", "ui_btn_primary_hover", "ui_btn_primary_pressed", "ui_btn_primary_disabled",
                "ui_btn_toggle_on", "ui_btn_toggle_danger");
            Add(12, 12, 10, 0, "ui_bottombar_bg");
            Add(20, 20, 20, 20, "ui_questpanel_frame");
            Add(10, 10, 10, 10, "ui_card_normal", "ui_card_target", "ui_card_affordable", "ui_card_dim");
            Add(3, 3, 3, 3, "ui_progress_track", "ui_progress_fill_gold", "ui_progress_fill_green", "ui_progress_fill_red", "ui_scrollbar");
            Add(6, 6, 6, 6, "ui_badge_count");
            Add(12, 12, 12, 12, "ui_unlockbtn_frame");
            return d;
        }

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
                if (cat == "ui" && NineSlice.TryGetValue(ParseName(bn, out _, out _, out _), out var ins))
                    s.spriteBorder = new Vector4(ins[0], ins[3], ins[1], ins[2]);   // Unity border = (L,B,R,T); table = (L,R,T,B)
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

            // (the Dragon is skipped when the sleeping-dragon strip exists: it is wired below and would otherwise flip-flop)
            foreach (var a in db.buildings)
                if (a && !(a.def.key == "dragon" && map.ContainsKey("dragon_sleeping")) && Set(map, "bld_" + a.def.key, used, s => { frameMap.TryGetValue("bld_" + a.def.key, out var bf); if (a.icon == s && a.hasRealArt && SameFrames(a.frames, bf)) return false; a.icon = s; a.frames = bf; a.hasRealArt = true; EditorUtility.SetDirty(a); return true; })) wired++;

            // the Dragon building has no bld_ art: its body is the sleeping-dragon strip (awake art = last stage asset)
            var dragonB = db.FindBuilding("dragon");
            if (dragonB && map.TryGetValue("dragon_sleeping", out var dragonSpr))
            {
                frameMap.TryGetValue("dragon_sleeping", out var df);
                if (dragonB.icon != dragonSpr || !dragonB.hasRealArt || !SameFrames(dragonB.frames, df))
                { dragonB.icon = dragonSpr; dragonB.frames = df; dragonB.hasRealArt = true; EditorUtility.SetDirty(dragonB); wired++; }
            }

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
                if (existing != null && existing.sprite == kv.Value && SameFrames(existing.frames, fr)) { used.Add(kv.Key); continue; }
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
                    // "<key>_flow": a full-sprite animation loop that replaces the static art's frames (fix_spring_flow)
                    if (frameMap.TryGetValue(rk + "_flow", out var flow) && flow != null) { fr = flow; used.Add(rk + "_flow"); }
                    // "<key>_sparkle": an animated overlay drawn above the sprite (fix_spirittree_sparkle)
                    Sprite[] ov = null;
                    if (map.TryGetValue(rk + "_sparkle", out var ov0)) { ov = frameMap.TryGetValue(rk + "_sparkle", out var ovf) && ovf != null ? ovf : new[] { ov0 }; used.Add(rk + "_sparkle"); }
                    if (e.sprite != s || !SameFrames(e.frames, fr) || !SameFrames(e.overlay, ov) || !e.realArt)
                    { e.sprite = s; e.frames = fr; e.overlay = ov; e.realArt = true; dirty = true; wired++; }
                    used.Add(rk);
                }
                // "<key>_2x2": size variant for big spawner nodes (node_ore_2x2), stored as its own entry
                for (int i = 0, n0 = r.sprites.Count; i < n0; i++)
                {
                    string bk = r.sprites[i].key + "_2x2";
                    if (!map.TryGetValue(bk, out var bs)) continue;
                    frameMap.TryGetValue(bk, out var bfr);
                    var be = r.sprites.Find(x => x.key == bk);
                    if (be == null) { be = new SpriteEntry { key = bk }; r.sprites.Add(be); }
                    if (be.sprite != bs || !SameFrames(be.frames, bfr) || !be.realArt) { be.sprite = bs; be.frames = bfr; be.realArt = true; dirty = true; wired++; }
                    used.Add(bk);
                }
                if (Set(map, "ui_area_" + r.def.key, used, s => { if (r.icon == s) return false; r.icon = s; dirty = true; return true; })) wired++;
                if (Set(map, "ui_action_" + r.def.key, used, s => { if (r.actionIcon == s) return false; r.actionIcon = s; dirty = true; return true; })) wired++;
                if (dirty) EditorUtility.SetDirty(r);
            }
            for (int i = 0; i < db.dragonStages.Count; i++)
            {
                var a = db.dragonStages[i]; if (!a) continue;
                string dk = i == 0 ? "dragon_sleeping" : "dragon_awake";
                if (Set(map, dk, used, s => { frameMap.TryGetValue(dk, out var sf); if (a.icon == s && a.hasRealArt && SameFrames(a.frames, sf)) return false; a.icon = s; a.frames = sf; a.hasRealArt = true; EditorUtility.SetDirty(a); return true; })) wired++;
            }
            foreach (var a in db.upgrades) if (a && Set(map, "upg_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.perks) if (a && Set(map, "perk_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.vows) if (a && Set(map, "ui_vow_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            foreach (var a in db.quests) if (a && Set(map, "ui_quest_" + a.def.id, used, s => { if (a.icon == s) return false; a.icon = s; EditorUtility.SetDirty(a); return true; })) wired++;
            if (Set(map, "ui_alert", used, s => { if (db.missingSprite == s) return false; db.missingSprite = s; EditorUtility.SetDirty(db); return true; })) wired++;

            // fx_*: generic FX registry wiring by key
            foreach (var kv in map)
            {
                if (!kv.Key.StartsWith("fx_") && kv.Key != "deco_tree") continue;
                frameMap.TryGetValue(kv.Key, out var fr);
                var e = db.fx.Find(x => x.key == kv.Key);
                if (e == null) { e = new SpriteEntry { key = kv.Key }; db.fx.Add(e); }
                if (e.sprite != kv.Value || !e.realArt || !SameFrames(e.frames, fr))
                { e.sprite = kv.Value; e.frames = fr; e.realArt = true; EditorUtility.SetDirty(db); wired++; }
                used.Add(kv.Key);
            }
            // ui_*: generic UI chrome registry (panels, buttons, fuel rack, icons...) not consumed by a typed target above
            foreach (var kv in map)
            {
                string k = kv.Key;
                if (!k.StartsWith("ui_") || used.Contains(k)) continue;
                if (k.StartsWith("ui_area_") || k.StartsWith("ui_action_") || k.StartsWith("ui_vow_") || k.StartsWith("ui_quest_") || k == "ui_alert") continue;
                frameMap.TryGetValue(k, out var fr);
                var e = db.ui.Find(x => x.key == k);
                if (e == null) { e = new SpriteEntry { key = k }; db.ui.Add(e); }
                if (e.sprite != kv.Value || !e.realArt || !SameFrames(e.frames, fr))
                { e.sprite = kv.Value; e.frames = fr; e.realArt = true; EditorUtility.SetDirty(db); wired++; }
                used.Add(k);
            }
            // enemy_<kind>_<anim>: per-enemy animation set (idle/move/hit/die)
            foreach (var kv in map)
            {
                if (!Regex.IsMatch(kv.Key, "^enemy_[a-z0-9_]+_(idle|move|hit|die)$")) continue;
                frameMap.TryGetValue(kv.Key, out var fr);
                var e = db.enemyAnims.Find(x => x.key == kv.Key);
                if (e == null) { e = new SpriteEntry { key = kv.Key }; db.enemyAnims.Add(e); }
                if (e.sprite != kv.Value || !e.realArt || !SameFrames(e.frames, fr))
                { e.sprite = kv.Value; e.frames = fr; e.realArt = true; EditorUtility.SetDirty(db); wired++; }
                used.Add(kv.Key);
            }
            // consumed directly by IslandArtBuilder's Rule Tiles
            foreach (var biome in new[] { "center", "farm", "mine", "fishing", "volcano", "grove", "celestial" })
                used.Add("island_" + biome + "_ground_fill");
            // consumed directly from Art/Incoming by IslandDressingBuilder (ADR 0005 island dressing)
            foreach (var biome in new[] { "center", "farm", "mine", "fishing", "volcano", "grove", "celestial" })
                used.Add("island_" + biome + "_scatter");
            foreach (var k in new[] { "island_center_path", "island_center_plaza", "island_fishing_water", "island_volcano_lavacrack",
                "island_celestial_twinkle", "zone_wood_patch", "zone_stone_patch", "zone_clay_patch", "zone_sand_patch", "zone_water_patch" })
                used.Add(k);
            used.Add("bld_dragon");     // compatibility key, superseded by dragon_sleeping
            used.Add("sky_moon");  // stored; the 3/4 top-down sky (IslandsBuilder.BuildSky) no longer shows a moon
            // consumed directly from Art/Incoming by IslandsBuilder.BuildSky / IslandCoastBuilder.BuildUnderside
            used.Add("sky_cloudpuff");
            used.Add("sky_sunbeam");
            used.Add("island_underside_vines");
            used.Add("island_underside_lavadrip");

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

        static bool SameFrames(Sprite[] a, Sprite[] b)
        {
            if (a == null || a.Length == 0 || b == null || b.Length == 0)     // Unity serializes null arrays as empty
                return (a == null || a.Length == 0) && (b == null || b.Length == 0);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
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
            return "enemy_" + GameDatabase.EnemyKind(name);
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
