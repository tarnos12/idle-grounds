using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>Creates/updates the ScriptableObject data assets from game-data.json (idempotent, keeps GUIDs).</summary>
    public static class DataImporter
    {
        const string Root = "Assets/_Project/Data/";
        const string JsonPath = Root + "Source/game-data.json";
        const string AtlasPath = "Assets/_Project/Art/Sprites/Emoji/emoji_atlas.png";
        const string ItemSpriteDir = "Assets/_Project/Art/Sprites/Items/";
        const string OldIcons = "old-game/assets/icons/";

        static Dictionary<string, Sprite> atlas;
        static Dictionary<string, Sprite> incoming;   // delivered art (Art/Incoming) - PREFERRED over old icons / emoji

        [MenuItem("Idle Grounds/Data/Import From JSON")]
        public static void Import()
        {
            var cfg = GameConfigJson.Load(File.ReadAllText(JsonPath));
            LoadAtlas();
            incoming = ArtIntake.LoadIncomingSprites();
            var itemSprites = ImportItemIcons();

            AssetDatabase.StartAssetEditing();
            try { Run(cfg, itemSprites); }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ArtIntake.Integrate();   // re-applies strip frames + building variants that the rebuild above cleared
            Debug.Log("DataImporter: imported " + cfg.items.Count + " items, " + cfg.regions.Count + " regions, " +
                      cfg.buildings.Count + " buildings, " + cfg.upgradeTree.Count + " upgrades, " + cfg.perks.Count +
                      " perks, " + cfg.vows.Count + " vows, " + cfg.quests.Count + " quests, " + cfg.dragonStages.Count + " dragon stages.");
        }

        static void Run(GameConfig cfg, Dictionary<string, Sprite> itemSprites)
        {
            var db = Get<GameDatabase>(Root + "GameDatabase.asset");
            db.missingSprite = Atlas("ui_alert") ?? Atlas("item_wood");

            db.items.Clear();
            foreach (var d in cfg.items)
            {
                var a = Get<ItemAsset>(Root + "Items/Item_" + d.key + ".asset");
                a.def = d;
                a.icon = ArtIntake.Find(incoming, "item_" + d.key) ?? (itemSprites.TryGetValue(d.key, out var s) ? s : Atlas("item_" + d.key));
                Dirty(a); db.items.Add(a);
            }
            db.buildings.Clear();
            foreach (var d in cfg.buildings)
            {
                var a = Get<BuildingAsset>(Root + "Buildings/Building_" + d.key + ".asset");
                // TODO(ADR 0003): spirit_bridge has no atlas emoji yet — the Wisp Lantern icon stands in
                a.def = d; a.icon = Atlas("bld_" + d.key) ?? (d.key == "spirit_bridge" ? Atlas("bld_wisp_lantern") : null);
                a.hasRealArt = false;   // ArtIntake re-flags delivered art
                Dirty(a); db.buildings.Add(a);
            }
            db.regions.Clear();
            foreach (var d in cfg.regions)
            {
                var a = Get<RegionAsset>(Root + "Regions/Region_" + d.key + ".asset");
                a.def = d;
                a.icon = Atlas("ui_area_" + d.key);
                a.actionIcon = Atlas("ui_action_" + d.key);
                a.groundColor = GroundColor(d.key);
                a.sprites = new List<SpriteEntry>();
                foreach (var sp in d.spawners) AddSprite(a, "node_" + sp.kind);
                foreach (var fx in d.fixtures) AddSprite(a, "fix_" + fx.kind);
                if (d.enemies.enabled)
                {
                    AddSprite(a, "enemy", Atlas(EnemyKey(d.enemies.name)));
                    if (d.enemies.baitSpawn.enabled) AddSprite(a, "enemy_bait", Atlas(EnemyKey(d.enemies.baitSpawn.name)));
                }
                Dirty(a); db.regions.Add(a);
            }
            db.upgrades.Clear();
            foreach (var d in cfg.upgradeTree)
            {
                var a = Get<UpgradeNodeAsset>(Root + "Upgrades/Upgrade_" + d.id + ".asset");
                a.def = d; a.icon = Atlas("upg_" + d.id);
                Dirty(a); db.upgrades.Add(a);
            }
            db.perks.Clear();
            foreach (var d in cfg.perks)
            {
                var a = Get<PerkAsset>(Root + "Perks/Perk_" + d.id + ".asset");
                a.def = d; a.icon = Atlas("perk_" + d.id);
                Dirty(a); db.perks.Add(a);
            }
            db.vows.Clear();
            foreach (var d in cfg.vows)
            {
                var a = Get<VowAsset>(Root + "Vows/Vow_" + d.id + ".asset");
                a.def = d; a.icon = Atlas("ui_vow_" + d.id);
                Dirty(a); db.vows.Add(a);
            }
            db.quests.Clear();
            foreach (var d in cfg.quests)
            {
                var a = Get<QuestAsset>(Root + "Quests/Quest_" + d.id + ".asset");
                a.def = d; a.icon = Atlas("ui_quest_" + d.id) ?? QuestFallbackIcon(db, d);
                Dirty(a); db.quests.Add(a);
            }
            db.dragonStages.Clear();
            for (int i = 0; i < cfg.dragonStages.Count; i++)
            {
                var a = Get<DragonStageAsset>(Root + "DragonStages/DragonStage_" + i + ".asset");
                a.def = cfg.dragonStages[i];
                a.icon = Atlas(i == 0 ? "dragon_sleeping" : "dragon_awake");
                Dirty(a); db.dragonStages.Add(a);
            }

            db.balance = cfg.balance;
            db.grid = cfg.grid;
            db.test = cfg.test;
            db.zones = cfg.zones;
            db.dragonBuffs = cfg.dragonBuffs;
            db.vitality = cfg.vitality;
            db.reveal = cfg.reveal;
            db.gateOfferings = cfg.gateOfferings;
            Dirty(db);
        }

        static void AddSprite(RegionAsset a, string key, Sprite s = null)
        {
            a.sprites.Add(new SpriteEntry { key = key, sprite = s != null ? s : Atlas(key) });
        }

        static string EnemyKey(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("fox")) return "enemy_fox";
            if (n.Contains("boar")) return "enemy_boar";
            return "enemy_" + n;
        }

        static Color GroundColor(string region)
        {
            switch (region)
            {
                case "center": return new Color(0.30f, 0.45f, 0.28f);
                case "farm": return new Color(0.55f, 0.50f, 0.25f);
                case "mine": return new Color(0.38f, 0.38f, 0.42f);
                case "fishing": return new Color(0.25f, 0.45f, 0.55f);
                case "volcano": return new Color(0.35f, 0.20f, 0.18f);
                case "grove": return new Color(0.20f, 0.42f, 0.30f);
                case "celestial": return new Color(0.30f, 0.28f, 0.50f);
                default: return new Color(0.3f, 0.4f, 0.3f);
            }
        }

        // ---------- helpers ----------
        static T Get<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void Dirty(Object o) { EditorUtility.SetDirty(o); }

        static void LoadAtlas()
        {
            atlas = new Dictionary<string, Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
                if (o is Sprite s) atlas[s.name] = s;
        }

        static Sprite Atlas(string key) => ArtIntake.Find(incoming, key) ?? (atlas != null && atlas.TryGetValue(key, out var s) ? s : null);

        /// <summary>Port-only quests have no ui_quest_* atlas art: use the item they make / the building they teach.</summary>
        static Sprite QuestFallbackIcon(GameDatabase db, QuestDef d)
        {
            if (d.goalKind == QuestGoalKind.ItemProducedThisRun && !string.IsNullOrEmpty(d.goalItem))
            {
                var it = db.FindItem(d.goalItem)?.icon;
                if (it != null) return it;
            }
            if (d.goalKind == QuestGoalKind.BridgePaired || d.goalKind == QuestGoalKind.BridgeDelivered || d.target?.kind == "bridge")
                return db.FindBuilding("spirit_bridge")?.icon;
            foreach (var b in d.builds) { var s = db.FindBuilding(b)?.icon; if (s != null) return s; }
            return null;
        }

        static Dictionary<string, Sprite> ImportItemIcons()
        {
            var result = new Dictionary<string, Sprite>();
            Directory.CreateDirectory(ItemSpriteDir);
            if (!Directory.Exists(OldIcons)) return result;
            foreach (var src in Directory.GetFiles(OldIcons, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(src);
                string dst = ItemSpriteDir + key + ".png";
                if (!File.Exists(dst)) File.Copy(src, dst);
            }
            AssetDatabase.Refresh();
            foreach (var dst in Directory.GetFiles(ItemSpriteDir, "*.png"))
            {
                string path = dst.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool changed = imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||
                               imp.spritePixelsPerUnit != 16 || imp.filterMode != FilterMode.Point;
                if (changed)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = 16;   // 16 px icon = 1 world unit
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.SaveAndReimport();
                }
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) result[Path.GetFileNameWithoutExtension(path)] = s;
            }
            return result;
        }
    }
}
