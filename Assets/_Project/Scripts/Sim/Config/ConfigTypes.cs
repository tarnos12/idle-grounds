// Config POCOs mirroring old-game/js/data.js (DATA). Unity-serializer
// friendly: [Serializable], public fields, lists instead of maps (source key
// order preserved), string keys between defs, no polymorphism. Optional
// sub-configs carry an `enabled` flag (the Unity serializer never leaves a
// class field null). See docs/port/data-catalog.md.
using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>An (item, quantity) pair — replaces JS `{item: qty}` maps (order kept).</summary>
    [Serializable]
    public class ItemQty
    {
        public string item;
        public int qty;
        public ItemQty() { }
        public ItemQty(string item, int qty) { this.item = item; this.qty = qty; }
        public override string ToString() => item + ":" + qty;
    }

    /// <summary>Drop spec `d(item,min,max)` (data.js:86).</summary>
    [Serializable]
    public class DropSpec
    {
        public string item;
        public int min;
        public int max;
        public DropSpec() { }
        public DropSpec(string item, int min, int max) { this.item = item; this.min = min; this.max = max; }
    }

    /// <summary>`rareDrop {item, chance}`. Empty item = none.</summary>
    [Serializable]
    public class RareDrop
    {
        public string item;
        public double chance;
        public RareDrop() { }
        public RareDrop(string item, double chance) { this.item = item; this.chance = chance; }
        public static bool IsSet(RareDrop r) => r != null && !string.IsNullOrEmpty(r.item);
    }

    public enum NodeInteraction { None, Chop, Instant, Break, Surface, Quarry }

    [Serializable]
    public class ItemDef
    {
        public string key;
        public string name;
        public string emoji;
        /// <summary>FUEL burn-ms (data.js:56); 0 = not a fuel.</summary>
        public int fuelMs;
        /// <summary>SOURCES hint text.</summary>
        public string sourceHint;
    }

    /// <summary>Inclusive cell rect (row/col).</summary>
    [Serializable]
    public class ZoneRect
    {
        public int r0, c0, r1, c1;
        public ZoneRect() { }
        public ZoneRect(int r0, int c0, int r1, int c1) { this.r0 = r0; this.c0 = c0; this.r1 = r1; this.c1 = c1; }
        public bool Contains(int r, int c) => r >= r0 && r <= r1 && c >= c0 && c <= c1;
        public override string ToString() => $"{r0}..{r1},{c0}..{c1}";
    }

    [Serializable]
    public class ZoneDef
    {
        public string key;
        public List<ZoneRect> rects = new List<ZoneRect>();
    }

    [Serializable]
    public class GridDef
    {
        public int cell = 32;
        public int cells = 93;
        public int margin = 10;
        public int gap = 5;
        public int buildingW = 3;
        public int buildingH = 3;
        public int PlayPx => cells * cell;
    }

    [Serializable]
    public class TestScaling
    {
        /// <summary>Dev fast mode (ADR 0004: off by default; flip it from the Game layer's dev menu).</summary>
        public bool enabled = false;
        public double timeScale = 0.2;
        public double costScale = 0.5;
    }

    [Serializable]
    public class TierDef
    {
        public string name;
        public int hits;                // 0 = unset (`|| 1`)
        public List<DropSpec> perHit = new List<DropSpec>();
        public List<DropSpec> drops = new List<DropSpec>();
        public double timer;
    }

    [Serializable]
    public class SpawnerDef
    {
        public string kind;
        public string zone;
        public List<int> sizes = new List<int>();
        public int target;
        public bool scaleWithArea = true;   // JS: `scaleWithArea === false` disables
        public double spacing;              // 0 = none
        public NodeInteraction interaction;
        public bool useTiers;
        public int swingMs;                 // 0 = default 350
        public string sprite;
        public int hits;                    // 0 = default 1
        public double regrow;               // 0 = default 10
        public List<DropSpec> perHit = new List<DropSpec>();
        public List<DropSpec> drops = new List<DropSpec>();
        public RareDrop rareDrop = new RareDrop();
    }

    [Serializable]
    public class FixtureDef
    {
        public string kind;
        public string zone;
        public int size;
        public NodeInteraction interaction;
        public int swingMs;                 // 0 = default 1000 (placeFixture)
        public string sprite;
        public int clicksPerDrop;           // 0 = default 5
        public string drop;
        public int dropMin;                 // 0 = unset (drops 1)
        public int dropMax;
        public bool autoTap;
        public RareDrop rareDrop = new RareDrop();
    }

    [Serializable]
    public class FieldGeneratorDef
    {
        public string kind;
        public string zone;
        public string item;
        public int intervalMs;
        public int cap;
        /// <summary>Upgrade field name that speeds it (e.g. "quarry"); empty = none.</summary>
        public string upgrade;
        public RareDrop rareDrop = new RareDrop();
    }

    [Serializable]
    public class BaitSpawnDef
    {
        public bool enabled;
        public string name;
        public string sprite;
        public int hp;
        public double speed;
        public List<DropSpec> drops = new List<DropSpec>();
    }

    [Serializable]
    public class EnemyDef
    {
        public bool enabled;
        public string zone;
        public string name;
        public string sprite;
        public int cap;
        public int hp;
        public double speed;
        public int respawnMs;
        public int attackMs;
        public List<DropSpec> drops = new List<DropSpec>();
        public BaitSpawnDef baitSpawn = new BaitSpawnDef();
    }

    /// <summary>AREAS entry merged with its WORLD entry and TIER_SPRITES.</summary>
    [Serializable]
    public class RegionDef
    {
        public string key;
        public string name;
        public string icon;
        public string verb;
        public string actionIcon;
        public string baseItem;
        public List<string> noBuild = new List<string>();
        public string speedLabel;
        public string timerLabel;
        public double surfaceWindow;        // 0 = default 3 s
        public string tierSprite;
        // WORLD
        public int rx, ry;
        /// <summary>Default world offset (cells) of this Island's top-left cell (ADR 0003, WORLD.islands). The scene overrides it at boot via Simulation.SetIslandOffsets.</summary>
        public int islandCol, islandRow;
        public string unlockSide;
        public List<ItemQty> unlockCost = new List<ItemQty>();
        // content
        public List<TierDef> tiers = new List<TierDef>();
        public List<SpawnerDef> spawners = new List<SpawnerDef>();
        public List<FixtureDef> fixtures = new List<FixtureDef>();
        public List<FieldGeneratorDef> generators = new List<FieldGeneratorDef>();
        public EnemyDef enemies = new EnemyDef();
    }

    [Serializable]
    public class RecipeDef
    {
        public string name;
        public List<ItemQty> inputs = new List<ItemQty>();
        public string output;
        public int outputQty;
        public int timeMs;
        public int stockCap;                // 0 = default 20
    }

    [Serializable] public class GatherConfig { public bool enabled; public int radius; public int cap; }
    [Serializable] public class StokerConfig { public bool enabled; public int radius; public int cap; }
    [Serializable] public class LanternConfig { public bool enabled; public int rateMs; public double speed; }
    /// <summary>Spirit Bridge (ADR 0003): untyped buffer cap; beat + wisp speed base like a lantern.</summary>
    /// <summary>Spirit Bridge: <c>cap</c> = buffer size (and the receiver's in-flight reservation limit), <c>rateMs</c> = send beat,
    /// <c>speed</c> = sky wisp px/s, <c>carry</c> = items one sky wisp carries (balance pass 1).</summary>
    [Serializable] public class BridgeConfig { public bool enabled; public int cap = 20; public int rateMs = 1000; public double speed = 170; public int carry = 1; }
    [Serializable] public class SealConfig { public bool enabled; public int cap; }
    [Serializable] public class GenBuildingConfig { public bool enabled; public string item; public int intervalMs; public int cap; }

    [Serializable]
    public class RosterConfig
    {
        public bool enabled;
        public int cap;
        public string recruit;
        public string food;
        public int foodCap;
        public List<ItemQty> foodValues = new List<ItemQty>();
        public string produce;
        public int produceMs;
    }

    [Serializable]
    public class BuildingDef
    {
        public string key;
        public string name;
        public string icon;
        public int sizeW = 3;
        public int sizeH = 3;
        public List<ItemQty> cost = new List<ItemQty>();
        public bool unlocked;
        public int stageUnlock = -1;        // -1 = none
        public bool indestructible;
        public bool fuel;                   // burner with a fuel rack
        public bool anyZone;
        public bool waterOnly;
        public int cap;                     // storehouse cap (0 = none)
        public bool shrine;
        public bool gate;
        public List<RecipeDef> recipes = new List<RecipeDef>();
        public GatherConfig gather = new GatherConfig();
        public StokerConfig stoker = new StokerConfig();
        public LanternConfig lantern = new LanternConfig();
        public SealConfig seal = new SealConfig();
        public RosterConfig roster = new RosterConfig();
        public GenBuildingConfig gen = new GenBuildingConfig();
        public BridgeConfig bridge = new BridgeConfig();

        public bool IsConverter => recipes != null && recipes.Count > 0;
    }

    [Serializable]
    public class DragonStageDef
    {
        public List<ItemQty> needs = new List<ItemQty>();
        public string text;
    }

    [Serializable]
    public class DragonBuffDef
    {
        public string item;     // pill item id
        public string name;
        public string desc;
    }

    [Serializable]
    public class VitalityDef
    {
        public string item = "vitality_pill";
        public string name;
        public int ms = 45000;
        public int bonusDamage = 2;
        public int lootMult = 2;
    }

    [Serializable]
    public class CostLevel
    {
        public List<ItemQty> items = new List<ItemQty>();
    }

    [Serializable]
    public class UpgradeNodeDef
    {
        public string id;
        public string icon;
        public string name;
        public int x, y;
        public string area;
        public string type;
        public string desc;
        public List<string> links = new List<string>();
        public List<CostLevel> costs = new List<CostLevel>();
    }

    /// <summary>Translations of the JS quest goal lambdas (data.js:599-677).</summary>
    public enum QuestGoalKind
    {
        Unknown,
        /// <summary>cur = handCount(goalItem), need = goalNeed.</summary>
        HandCount,
        /// <summary>cur = Σ min(handCount(i.item), i.qty) over goalItems, need = goalNeed.</summary>
        HandCountCapped,
        /// <summary>cur = dragon.stage ≥ goalStage ? 1 : 0, need 1.</summary>
        DragonStageAtLeast,
        /// <summary>cur = stats[goalStat], need = goalNeed.</summary>
        StatAtLeast,
        /// <summary>cur = any of goalRegions unlocked ? 1 : 0, need 1.</summary>
        AnyRegionUnlocked,
        /// <summary>need = max(1, dragonTribute(goalStage)[goalItem]); stage &gt; goalStage ⇒ done;
        /// cur = min(need, handCount(goalItem) + (stage == goalStage ? dragon.paid[goalItem] : 0)).</summary>
        DragonTributeItem,
    }

    [Serializable]
    public class QuestTarget
    {
        public string area;
        public string kind;     // fixture | dragon | enemyZone | altar | building ; empty = none
        public string id;
    }

    [Serializable]
    public class QuestDef
    {
        public string id;
        public string icon;
        public string name;
        public string desc;
        public QuestGoalKind goalKind;
        public string goalItem;
        public string goalStat;
        public int goalStage;
        public int goalNeed;
        public List<string> goalRegions = new List<string>();
        public List<ItemQty> goalItems = new List<ItemQty>();
        /// <summary>Original JS lambda source (reference only).</summary>
        public string goalSource;
        public List<string> rewardReveal = new List<string>();
        public List<ItemQty> rewardItems = new List<ItemQty>();
        public List<string> builds = new List<string>();
        public QuestTarget target = new QuestTarget();
    }

    public enum RevealKind { Quest, Region, Stage, Islands }

    [Serializable]
    public class RevealCond
    {
        public RevealKind kind;
        public string key;      // quest id / region key
        public int stage;
        /// <summary>Islands: revealed once at least this many Islands are unlocked.</summary>
        public int count;
    }

    [Serializable]
    public class RevealRule
    {
        public string building;
        public List<RevealCond> any = new List<RevealCond>();
    }

    [Serializable]
    public class PerkDef
    {
        public string id;
        public string name;
        public string icon;
        public int max;
        public List<int> cost = new List<int>();
        public string desc;
    }

    [Serializable]
    public class VowDef
    {
        public string id;
        public string name;
        public string icon;
        public string desc;
    }

    [Serializable]
    public class GateOffering
    {
        public List<string> items = new List<string>();
        public int cap = 6;
        public int perType = 2;
    }

    /// <summary>Scalar DATA constants + the engine.js constants table (engine-systems §0).</summary>
    [Serializable]
    public class GameBalance
    {
        // DATA
        public int handCap = 20;
        public int fuelSlots = 6;
        public int fuelCap = 60000;
        /// <summary>AUTOMATION_CLICKS: index i = level i+1.</summary>
        public List<int> automationClicks = new List<int> { 2, 6, 20 };
        public List<double> vowMult = new List<double> { 1, 1.15, 1.3, 1.5, 1.75 };
        public int questChain = 2;
        public int versionNum = 52;
        public string versionDesc;
        public int worldCols = 3;
        public int worldRows = 3;
        // prestige pacing (ADR 0004 / balance pass 1: later runs about as long as the first)
        /// <summary>World speed gained per ascension: factor 1/(1 + this·ascensions). Was 0.2 (+20%/run).</summary>
        public double ascensionSpeedPerRun = 0;
        /// <summary>Eternal Haste: every timer ×this per perk level. Was 0.95.</summary>
        public double hasteStep = 0.98;
        /// <summary>Each vow completed at least once: timers ×this. Was 0.96.</summary>
        public double vowMarkStep = 0.99;
        /// <summary>Timers ×this once the dragon is awake in the current run (no longer carried into later runs).</summary>
        public double awakenedSpeed = 0.9;
        /// <summary>Dragon tributes ×max(floor, 1/(1 + this·ascensions)). Was 0.25 (floor 0.4).</summary>
        public double tributeShrinkPerRun = 0;
        public double tributeShrinkFloor = 0.4;
        /// <summary>Vow of the Restless Dragon: every tribute ×this. Was 2.</summary>
        public double restlessTributeMult = 1.5;
        // engine.js constants (§0)
        public int groundCap = 600;
        public int groundHardCap = 900;
        public int protectedShare = 480;
        public int manualGraceMs = 4000;
        public int fixtureGraceMs = 8000;
        public int genRareIdle = 2;
        public int outputPileMax = 12;
        public int outputPileCells = 3;
        public int pileRecheckMs = 500;
        public int craftWindowMs = 60000;
        public int maxTickGap = 10000;
        public int maxTickEvents = 400;
        public int autoSkipLoose = 120;
        // engine.js literals used by M1 systems
        public int dropJitterPx = 16;
        public int pickupCollectPx = 12;
        public int settleMinPx = 18;
        public int respawnRetryMs = 500;
        public int spawnAttempts = 40;
        public double defaultSurfaceWindowSec = 3;
        public int defaultStockCap = 20;
        public int storehouseDefaultCap = 200;

        public int AutomationClicks(int level)
        {
            if (level <= 0 || automationClicks == null || automationClicks.Count == 0) return 0;
            return automationClicks[Math.Min(level, automationClicks.Count) - 1];
        }
    }
}
