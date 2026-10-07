// Full GS shape of engine-systems §1 (state.js makeAreaState / makeInitialState),
// including fields only later systems use. Times are ms as double (JS numbers:
// regrow delays are fractional). Transient runtime fields carry
// [NonSerialized] (the JS strips every "_"-key + manualAt + autoPaused on save).
using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    [Serializable]
    public class HandStack
    {
        public string item;
        public int qty;
        public HandStack() { }
        public HandStack(string item, int qty) { this.item = item; this.qty = qty; }
    }

    /// <summary>A resource node: spawner node, fixture, or deco tree (§1.3).</summary>
    [Serializable]
    public class Node
    {
        public int id;
        public int row, col, size;
        public string kind;
        public string spawnerKind;          // spawner nodes only
        public NodeInteraction interaction;
        public bool useTiers;
        public int tier = 1;
        public int hitsLeft;
        public double regrowSec;
        public int swingMs;
        public string sprite;
        public List<DropSpec> perHit;       // null = none (tier nodes read the tier)
        public List<DropSpec> drops;
        public RareDrop rareDrop;           // null = none
        public double surfaceUntil;         // 0 = not a surfacing node
        public double autoFlash;
        public double hitAt;
        public ItemCounts pending;          // chop: accumulated per-swing yield
        // fixtures
        public bool isFixed;                // JS `fixed`
        public bool deco;
        public int clicks;
        public int clicksPerDrop;
        public string dropItem;
        public int dropMin, dropMax;
        // deco
        public double decoScale;
        public int decoDx, decoDy;
    }

    [Serializable]
    public class GroundItem
    {
        public int id;
        public string item;
        public double x, y;
        public bool crafted;
        public bool gen;
        // transients
        [NonSerialized] public double manualAt;      // 0 = no grace
        [NonSerialized] public int src;              // node id that dropped it (0 = none)
        [NonSerialized] public double pullAt;        // last Gathering Stone pull
        [NonSerialized] public bool hasPullTo;
        [NonSerialized] public double pullToX, pullToY;
    }

    [Serializable]
    public class SpawnQueueEntry
    {
        public double at;
        public string kind;
        public SpawnQueueEntry() { }
        public SpawnQueueEntry(double at, string kind) { this.at = at; this.kind = kind; }
    }

    [Serializable]
    public class Enemy
    {
        public int id;
        public double x, y;
        public int hp, maxHp;
        public double tx, ty;
        public double hitAt;
        public string kind;     // null or "boss"
        public string sprite;
        public double spd;      // 0 = config speed
    }

    [Serializable]
    public class Wisp
    {
        public int id;
        public double x0, y0, x, y;
        public string item;
        public int toId, fromId;
        public double t0;
        public double sp;
        public bool returning;
    }

    /// <summary>
    /// A wisp crossing the sky between two Spirit Bridges (ADR 0003). Lives in
    /// <see cref="GameState.skyWisps"/>, not in an area. All coordinates are
    /// WORLD px (Island offset + Island-local px): x0/y0 = start of the current
    /// leg, x/y = last ticked position, sx/sy = the sending bridge's centre at
    /// launch (where a returning wisp heads), tx/ty = the current leg's target.
    /// fromIsland/fromId = sending bridge; toIsland/toId = current target bridge
    /// (the sender again once <see cref="Wisp.returning"/>).
    /// </summary>
    [Serializable]
    public class SkyWisp : Wisp
    {
        public string fromIsland, toIsland;
        public double sx, sy, tx, ty;
        /// <summary>Items of <see cref="Wisp.item"/> this wisp carries (bridge <c>carry</c>; ≥ 1, older saves load as 1).</summary>
        public int qty = 1;
    }

    [Serializable]
    public class LinkStat
    {
        public double sentAt;
        public string fail;     // null | empty | refused | nomatch
    }

    [Serializable]
    public class Link
    {
        public int from, to;
        [NonSerialized] public LinkStat stat;
        [NonSerialized] public int seq;
    }

    [Serializable]
    public class FuelSlot
    {
        public string item;
        public double rem;
        public double total;
    }

    /// <summary>A placed building incl. ghosts; one class with optional per-kind blocks (§1.3, §18.2).</summary>
    [Serializable]
    public class Building
    {
        public int id;
        public string type;
        public int row, col;
        public ItemCounts paid = new ItemCounts();
        public bool built;
        public string item;                 // storehouse / seal type
        public int qty;
        public bool starter;
        public bool locked;                 // JS `lock`
        // gatherer / stoker
        public List<HandStack> inv;
        // lantern
        public List<Link> links;
        public int connIdx;
        public double nextSend;
        // roster
        public int disciples;
        public int buns;
        public double nextCultivate;
        // converter
        public int recipe;
        public ItemCounts stock;
        public double smeltDoneAt;
        public List<FuelSlot> fuelQ;
        public double fuelBurnAt;
        // generator building
        public double nextGen;
        // Spirit Bridge (ADR 0003): partner bridge (null island = unpaired); pairSends = this end is the sender.
        // Buffer = inv (untyped); the beat clock reuses nextSend.
        public string pairIsland;
        public int pairId;
        public bool pairSends;
        // gate
        public ItemCounts offered;
        public int offerings;
        // transients
        [NonSerialized] public List<double> crafts;
        [NonSerialized] public double craftFirst;
        [NonSerialized] public bool pileFull;
        [NonSerialized] public double pileAt;
        [NonSerialized] public string pileItem;
        [NonSerialized] public List<string> accEver;   // null = accept anything
        [NonSerialized] public bool hasAccEver;
        /// <summary>Runtime buffer <see cref="accEver"/> points at when non-null (refilled each tick, never saved).</summary>
        [NonSerialized] public List<string> accEverBuf;
        [NonSerialized] public int seq;
        /// <summary>
        /// Spirit Bridge sender: nextSend is a genuine beat due time (the last evaluation launched and had
        /// cargo + receiver room to spare). False (fresh / just paired / loaded / idle poll) = the beat
        /// clock restarts at now and fires once — no catch-up for time spent idle (§2.2).
        /// </summary>
        [NonSerialized] public bool beatArmed;
    }

    [Serializable]
    public class AreaUpgrades
    {
        public int maxTier = 1;
        public int speed, harvestSpeed, automation, quarry, enemyCap, damage, aoe, wispRate, affinity, discipleCap;
        public ItemCounts paid = new ItemCounts();  // legacy, unused

        /// <summary>Upgrade level by JS field name (generators' `upgrade`, tree node `type`).</summary>
        public int Get(string field)
        {
            switch (field)
            {
                case "speed": return speed;
                case "harvestSpeed": return harvestSpeed;
                case "automation": return automation;
                case "quarry": return quarry;
                case "enemyCap": return enemyCap;
                case "damage": return damage;
                case "aoe": return aoe;
                case "wispRate": return wispRate;
                case "affinity": return affinity;
                case "discipleCap": return discipleCap;
                case "maxTier": return maxTier;
                default: return 0;
            }
        }

        public void Set(string field, int v)
        {
            switch (field)
            {
                case "speed": speed = v; break;
                case "harvestSpeed": harvestSpeed = v; break;
                case "automation": automation = v; break;
                case "quarry": quarry = v; break;
                case "enemyCap": enemyCap = v; break;
                case "damage": damage = v; break;
                case "aoe": aoe = v; break;
                case "wispRate": wispRate = v; break;
                case "affinity": affinity = v; break;
                case "discipleCap": discipleCap = v; break;
                case "maxTier": maxTier = v; break;
            }
        }
    }

    /// <summary>Per-area state (§1.1).</summary>
    [Serializable]
    public class AreaState
    {
        public string key;
        public List<Node> nodes = new List<Node>();
        public List<GroundItem> ground = new List<GroundItem>();
        public List<Building> buildings = new List<Building>();
        public List<SpawnQueueEntry> spawnQueue = new List<SpawnQueueEntry>();
        public List<double> genTimers = new List<double>();
        public List<Enemy> enemies = new List<Enemy>();
        public List<double> enemyRespawns = new List<double>();
        public List<Wisp> wisps = new List<Wisp>();
        public int nextNodeId = 1, nextGroundId = 1, nextBuildId = 1, nextEnemyId = 1, nextWispId = 1;
        public AreaUpgrades upgrades = new AreaUpgrades();
        [NonSerialized] public List<string> autoSkip = new List<string>();
        [NonSerialized] public bool autoPaused;

        public AreaState() { }
        public AreaState(string key) { this.key = key; }

        public Node NodeById(int id) { foreach (var n in nodes) if (n.id == id) return n; return null; }
        public Building BuildingById(int id) { foreach (var b in buildings) if (b.id == id) return b; return null; }
    }

    [Serializable]
    public class RegionFlag
    {
        public string region;
        public bool unlocked;
    }

    [Serializable]
    public class RegionPaid
    {
        public string region;
        public ItemCounts paid = new ItemCounts();
    }

    [Serializable]
    public class WorldState
    {
        public List<RegionFlag> unlocked = new List<RegionFlag>();
        public List<RegionPaid> unlockPaid = new List<RegionPaid>();

        public bool IsUnlocked(string region)
        {
            foreach (var f in unlocked) if (f.region == region) return f.unlocked;
            return false;
        }

        public void SetUnlocked(string region, bool v)
        {
            foreach (var f in unlocked) if (f.region == region) { f.unlocked = v; return; }
            unlocked.Add(new RegionFlag { region = region, unlocked = v });
        }
    }

    [Serializable]
    public class BuildUiState
    {
        public bool open;
        public string placing;
    }

    [Serializable]
    public class UpgradeJob
    {
        public string area;
        public string type;
        public ItemCounts needs = new ItemCounts();
        public ItemCounts paid = new ItemCounts();
    }

    [Serializable]
    public class DragonState
    {
        public int stage;
        public ItemCounts paid = new ItemCounts();
        public string msg;
        public double msgUntil;
        public string dialog;
    }

    [Serializable]
    public class BuffState
    {
        public string kind;     // pill item id
        public double until;
    }

    [Serializable]
    public class CombatBuffState
    {
        public double until;
    }

    [Serializable]
    public class VowsState
    {
        public List<string> active = new List<string>();
        public ItemCounts done = new ItemCounts();
    }

    [Serializable]
    public class JustAscended
    {
        public int n;
        public int ap;
        public double speedFrom, speedTo;
    }

    [Serializable]
    public class QuestState
    {
        public int idx;
        public bool hidden;
        public int chain;
    }

    [Serializable]
    public class GameStats
    {
        public double started;
        public long totalGathered, totalCrafted;
        public int foxKills, buildingsBuilt, upgradesApplied, linksAdded, recipeSwitches, disciplesRecruited;
        /// <summary>Items sky wisps handed to a receiving Spirit Bridge (ADR 0003; the "Sky caravan" quest).</summary>
        public long bridgeDelivered;

        public long Get(string stat)
        {
            switch (stat)
            {
                case "totalGathered": return totalGathered;
                case "totalCrafted": return totalCrafted;
                case "foxKills": return foxKills;
                case "buildingsBuilt": return buildingsBuilt;
                case "upgradesApplied": return upgradesApplied;
                case "linksAdded": return linksAdded;
                case "recipeSwitches": return recipeSwitches;
                case "disciplesRecruited": return disciplesRecruited;
                case "bridgeDelivered": return bridgeDelivered;
                default: return 0;
            }
        }
    }

    /// <summary>Root state GS (§1.2).</summary>
    [Serializable]
    public class GameState
    {
        public const int SchemaVersion = 1;
        public int schemaVersion = SchemaVersion;

        public List<HandStack> hand = new List<HandStack>();
        public int handCap;
        public int handLevel;
        public List<AreaState> areas = new List<AreaState>();
        public WorldState world = new WorldState();
        public BuildUiState build = new BuildUiState();
        public UpgradeJob upgradeJob;
        public DragonState dragon = new DragonState();
        public double dragonScaleAt;            // 0 = unset
        public bool starterPlaced;
        public bool won;
        public bool dragonBlessed;
        public BuffState buff;
        public CombatBuffState combatBuff;
        public int ascensions;
        public bool ascendPrompt;
        public int ascendPoints;
        public ItemCounts perks = new ItemCounts();
        public VowsState vows = new VowsState();
        public JustAscended justAscended;
        public bool introSeen, endingSeen;
        public int gridCells;
        public QuestState quest = new QuestState();
        public List<string> builtTypes = new List<string>();
        public ItemCounts buildSeen = new ItemCounts();
        public bool pavilionSeeded;
        public bool perkShopSeen;
        /// <summary>Save stamp (ADR 0002: no offline progress — kept readable for old saves, never used).</summary>
        public double lastSeen;                 // NaN = null
        /// <summary>Legacy (ADR 0002): read from old saves, unused.</summary>
        public double offlineAwayFrom = double.NaN;  // NaN = null
        /// <summary>Wisps crossing the sky between paired Spirit Bridges (ADR 0003).</summary>
        public List<SkyWisp> skyWisps = new List<SkyWisp>();
        public int nextSkyWispId = 1;
        public GameStats stats = new GameStats();
        /// <summary>Per-item produced / consumed / lost counters (Stats - Flow). Lifetime survives ascension, the run section resets.</summary>
        public FlowLedger flow = new FlowLedger();

        /// <summary>`makeInitialState()` (state.js:31). Areas in config region order.</summary>
        public static GameState CreateInitial(GameConfig cfg, long nowMs)
        {
            cfg.Build();
            var s = new GameState
            {
                handCap = cfg.balance.handCap,
                gridCells = cfg.grid.cells,
                lastSeen = nowMs,
            };
            foreach (var r in cfg.regions)
            {
                s.areas.Add(new AreaState(r.key));
                // only the region with no unlock cost (center) starts open
                s.world.SetUnlocked(r.key, r.unlockCost.Count == 0);
            }
            s.quest.chain = cfg.balance.questChain;
            s.stats.started = nowMs;
            return s;
        }

        public AreaState Area(string key)
        {
            foreach (var a in areas) if (a.key == key) return a;
            return null;
        }

        public int PerkLevel(string id) => perks.Get(id);
        public bool VowActive(string id) => vows.active.Contains(id);
    }
}
