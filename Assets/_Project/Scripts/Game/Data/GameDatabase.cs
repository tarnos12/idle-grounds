using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    /// <summary>Root data asset: ordered lists of all def assets + the non-per-asset config.</summary>
    [CreateAssetMenu(menuName = "Idle Grounds/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<ItemAsset> items = new List<ItemAsset>();
        public List<RegionAsset> regions = new List<RegionAsset>();
        public List<BuildingAsset> buildings = new List<BuildingAsset>();
        public List<UpgradeNodeAsset> upgrades = new List<UpgradeNodeAsset>();
        public List<PerkAsset> perks = new List<PerkAsset>();
        public List<VowAsset> vows = new List<VowAsset>();
        public List<QuestAsset> quests = new List<QuestAsset>();
        public List<DragonStageAsset> dragonStages = new List<DragonStageAsset>();

        public GameBalance balance = new GameBalance();
        public GridDef grid = new GridDef();
        public TestScaling test = new TestScaling();
        public List<ZoneDef> zones = new List<ZoneDef>();
        public List<DragonBuffDef> dragonBuffs = new List<DragonBuffDef>();
        public VitalityDef vitality = new VitalityDef();
        public List<RevealRule> reveal = new List<RevealRule>();
        public GateOffering gateOfferings = new GateOffering();

        public Sprite missingSprite;

        /// <summary>Delivered FX art (fx_* keys, e.g. fx_wisp), wired generically by ArtIntake.</summary>
        public List<SpriteEntry> fx = new List<SpriteEntry>();

        /// <summary>Delivered enemy animation strips keyed enemy_&lt;kind&gt;_&lt;anim&gt; (idle/move/hit/die), wired by ArtIntake.</summary>
        public List<SpriteEntry> enemyAnims = new List<SpriteEntry>();

        /// <summary>"Fox Spirit" -> "fox", "Boar" -> "boar" (region enemy / bait-spawn display name).</summary>
        public static string EnemyKind(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("fox")) return "fox";
            if (n.Contains("boar")) return "boar";
            return n.Replace(' ', '_');
        }

        /// <summary>Frames of a delivered enemy animation, or null (emoji placeholder / not delivered).</summary>
        public Sprite[] EnemyAnim(string kind, string anim)
        {
            string key = "enemy_" + kind + "_" + anim;
            foreach (var e in enemyAnims)
                if (e != null && e.key == key && e.realArt && e.sprite != null)
                    return e.frames != null && e.frames.Length > 0 ? e.frames : new[] { e.sprite };
            return null;
        }

        /// <summary>Kind ("fox"/"boar") of the enemy a region spawns (bait = lured boss spawn).</summary>
        public string EnemyKindOf(string regionKey, bool bait)
        {
            var d = FindRegion(regionKey)?.def.enemies;
            return d == null ? "" : EnemyKind(bait ? d.baitSpawn?.name : d.name);
        }

        /// <summary>Animation frames of delivered fx art (1+ frames), or null when not delivered.</summary>
        public Sprite[] FxFrames(string key)
        {
            foreach (var e in fx)
                if (e != null && e.key == key && e.realArt && e.sprite != null)
                    return e.frames != null && e.frames.Length > 0 ? e.frames : new[] { e.sprite };
            return null;
        }

        /// <summary>Assemble the sim config (list order preserved) and Build() it.</summary>
        public GameConfig BuildConfig()
        {
            var c = new GameConfig
            {
                grid = grid, test = test, balance = balance, vitality = vitality, gateOfferings = gateOfferings,
                zones = new List<ZoneDef>(zones),
                dragonBuffs = new List<DragonBuffDef>(dragonBuffs),
                reveal = new List<RevealRule>(reveal),
            };
            foreach (var a in items) if (a) c.items.Add(a.def);
            foreach (var a in regions) if (a) c.regions.Add(a.def);
            foreach (var a in buildings) if (a) c.buildings.Add(a.def);
            foreach (var a in dragonStages) if (a) c.dragonStages.Add(a.def);
            foreach (var a in upgrades) if (a) c.upgradeTree.Add(a.def);
            foreach (var a in quests) if (a) c.quests.Add(a.def);
            foreach (var a in perks) if (a) c.perks.Add(a.def);
            foreach (var a in vows) if (a) c.vows.Add(a.def);
            return c.Build();
        }

        Sprite Or(Sprite s) => s != null ? s : missingSprite;

        public ItemAsset FindItem(string key) { foreach (var a in items) if (a && a.def.key == key) return a; return null; }
        public BuildingAsset FindBuilding(string key) { foreach (var a in buildings) if (a && a.def.key == key) return a; return null; }
        public RegionAsset FindRegion(string key) { foreach (var a in regions) if (a && a.def.key == key) return a; return null; }

        public Sprite ItemIcon(string itemKey) => Or(FindItem(itemKey)?.icon);
        public Sprite BuildingIcon(string key) => Or(FindBuilding(key)?.icon);
        /// <summary>Delivered body art for the building, or null when it only has the placeholder emoji.</summary>
        public Sprite BuildingArt(string key) { var a = FindBuilding(key); return a != null && a.hasRealArt ? a.icon : null; }
        public Sprite RegionIcon(string regionKey) => Or(FindRegion(regionKey)?.icon);
        public Sprite RegionActionIcon(string regionKey) => Or(FindRegion(regionKey)?.actionIcon);
        public Sprite NodeSprite(string regionKey, string kind) => Or(FindRegion(regionKey)?.Find("node_" + kind));
        public Sprite FixtureSprite(string regionKey, string kind) => Or(FindRegion(regionKey)?.Find("fix_" + kind));
        /// <summary>Delivered (real-art) entry for node_/fix_ keys, or null for emoji placeholders.</summary>
        public SpriteEntry RealNodeArt(string regionKey, string key) { var e = FindRegion(regionKey)?.FindEntry(key); return e != null && e.realArt && e.sprite != null ? e : null; }
        public Sprite EnemySprite(string regionKey) => Or(FindRegion(regionKey)?.Find("enemy"));
        public Sprite BaitSprite(string regionKey) => Or(FindRegion(regionKey)?.Find("enemy_bait"));
        public Sprite UpgradeIcon(string id) { foreach (var a in upgrades) if (a && a.def.id == id) return Or(a.icon); return missingSprite; }
        public Sprite PerkIcon(string id) { foreach (var a in perks) if (a && a.def.id == id) return Or(a.icon); return missingSprite; }
        public Sprite VowIcon(string id) { foreach (var a in vows) if (a && a.def.id == id) return Or(a.icon); return missingSprite; }
        public Sprite QuestIcon(string id) { foreach (var a in quests) if (a && a.def.id == id) return Or(a.icon); return missingSprite; }
        public Sprite DragonStageIcon(int stage) =>
            stage >= 0 && stage < dragonStages.Count && dragonStages[stage] ? Or(dragonStages[stage].icon) : missingSprite;

        /// <summary>Key lookup: item_*, bld_*, then any region's sprite entries.</summary>
        public Sprite GetSprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return missingSprite;
            if (key.StartsWith("item_")) return ItemIcon(key.Substring(5));
            if (key.StartsWith("bld_")) return BuildingIcon(key.Substring(4));
            foreach (var r in regions) { var s = r ? r.Find(key) : null; if (s) return s; }
            return missingSprite;
        }
    }
}
