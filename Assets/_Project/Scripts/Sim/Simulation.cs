namespace IdleGrounds.Sim
{
    /// <summary>
    /// The single interface the Unity layer talks to (ADR 0001). Owns the
    /// systems; <see cref="Tick"/> runs gameTick in the engine-systems §2.3
    /// order. Steps 1-6 (step 6: stoker only) and 10 run; the logistics
    /// parts of step 6 and steps 7-9 are ordered hooks for M4/M5.
    /// </summary>
    public sealed class Simulation
    {
        public readonly SimContext Ctx;

        public GameConfig Config => Ctx.Config;
        public GameState State => Ctx.State;
        public SimEvents Events => Ctx.Events;
        public World World => Ctx.World;
        public HandSystem Hand => Ctx.Hand;
        public GroundSystem Ground => Ctx.Ground;
        public NodeSystem Nodes => Ctx.Nodes;
        public OccupancyGrid Occupancy => Ctx.Occupancy;
        public FieldGeneratorSystem FieldGenerators => Ctx.FieldGenerators;
        public Timing Timing => Ctx.Timing;

        /// <summary>Suppress every event (offline replay).</summary>
        public bool Muted { get => Ctx.Events.Muted; set => Ctx.Events.Muted = value; }

        /// <summary>Offline replay mode: skips ground physics (JS offlineSim/offlineReplay).</summary>
        public bool OfflineSim { get => Ctx.OfflineSim; set { Ctx.OfflineSim = value; Ctx.OfflineReplay = value; } }

        public Simulation(GameConfig cfg, GameState state, IClock clock, IRng rng)
        {
            Ctx = new SimContext(cfg, state, clock, rng);
            Ctx.World = new World(Ctx);
            Ctx.Occupancy = new OccupancyGrid(Ctx);
            Ctx.Hand = new HandSystem(Ctx);
            Ctx.Ground = new GroundSystem(Ctx);
            Ctx.Nodes = new NodeSystem(Ctx);
            Ctx.FieldGenerators = new FieldGeneratorSystem(Ctx);
            Ctx.Fuel = new FuelSystem(Ctx);
            Ctx.Converters = new ConverterSystem(Ctx);
            Ctx.Buildings = new BuildingSystem(Ctx);
            Ctx.Logistics = new LogisticsSystem(Ctx);
            Ctx.Dragon = new DragonSystem(Ctx);
            Ctx.Upgrades = new UpgradeSystem(Ctx);
            Ctx.Progression = new ProgressionSystem(Ctx);
            Ctx.Pavilions = new PavilionSystem(Ctx);
            Ctx.Combat = new CombatSystem(Ctx);
            Ctx.Automation = new AutomationSystem(Ctx);
            Ctx.Hand.FeedBuilding = Ctx.Buildings.FeedBuilding;
            Ctx.Buildings.AltarFeed = Ctx.Upgrades.AltarFeed;
            Ctx.Buildings.DragonFeed = Ctx.Dragon.Feed;
            Ctx.Buildings.GateFeed = Ctx.Progression.GateFeed;
            Prestige = new PrestigeSystem(this);
            Offline = new OfflineReplay(this);
        }

        // ---- M7 prestige, save, offline (§14, §1.4-1.5, §15, §2.1) ----

        public PrestigeSystem Prestige { get; }
        public OfflineReplay Offline { get; }

        /// <summary>
        /// Boot (§2.1 steps 2-4): initArea ×all + starter network, then the
        /// offline catch-up decision. Tier None ⇒ go live. Toast ⇒ already
        /// replayed synchronously (summary set). Full ⇒ drive
        /// <c>Offline.Step(boot.job, 50)</c> once per frame (500 when the app is
        /// in the background) without ticking live, then <c>Offline.Finish</c>
        /// for the summary; <c>Offline.Skip</c> forfeits the rest.
        /// </summary>
        public OfflineBoot Boot()
        {
            InitAllAreas();
            var job = Offline.Begin();
            if (job == null) return new OfflineBoot { tier = OfflineTier.None };
            if (job.awayMs < Config.balance.offlineModalMs)
            {
                try { Offline.Step(job); }
                finally { Offline.Finish(job); }
                return new OfflineBoot { tier = OfflineTier.Toast, summary = job.summary };
            }
            return new OfflineBoot { tier = OfflineTier.Full, job = job };
        }

        /// <summary>
        /// Swap in another state (ascension, in-place load): caches dropped,
        /// tick gap reset, any replay abandoned; <paramref name="boot"/> re-runs
        /// initArea ×all + starter network. Raises RunReset.
        /// </summary>
        public void ReplaceState(GameState state, bool boot = true)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            var job = Offline.Job;
            if (job != null) { Offline.Skip(job); Offline.Finish(job); }
            Ctx.State = state;
            Ctx.ManualSrc = null;
            Ctx.ClockOverride = null;
            Ctx.Occupancy.Invalidate();
            Ctx.Timing.Reset();
            if (boot) InitAllAreas();
            Ctx.Events.RaiseRunReset();
        }

        /// <summary>`saveState()` JSON: lastSeen = resume point while a replay is unfinished, else the real now.</summary>
        public string SaveJson() => SaveCodec.Serialize(State, Offline.ResumeAt() ?? Ctx.RealNow);

        /// <summary>Parse + sanitise a save (null + reason on any failure; never throws).</summary>
        public static GameState LoadJson(string json, GameConfig cfg, double nowMs, out string reason) =>
            SaveCodec.TryDeserialize(json, cfg, nowMs, out var s, out reason) ? s : null;

        /// <summary>`buyPerk(id)` — false when unaffordable / maxed / unknown.</summary>
        public bool BuyPerk(string id) => Prestige.BuyPerk(id);
        /// <summary>AP price of the perk's next level; null when maxed/unknown.</summary>
        public int? PerkCost(string id) => Prestige.PerkCost(id);
        public int PerkLevel(string id) => State.PerkLevel(id);
        /// <summary>`ascend(nextVows)` — resets the run in place (RunReset); save right after.</summary>
        public JustAscended Ascend(System.Collections.Generic.IEnumerable<string> nextVows = null) => Prestige.Ascend(nextVows);
        /// <summary>Close the ascend dialog (the gate re-opens it on click).</summary>
        public void SetAscendPrompt(bool open) => State.ascendPrompt = open;
        /// <summary>Dismiss the one-time "Ascension n complete" card.</summary>
        public void ClearJustAscended() => State.justAscended = null;
        public void MarkPerkShopSeen() => State.perkShopSeen = true;
        public void MarkIntroSeen() => State.introSeen = true;

        public BuildingSystem Buildings => Ctx.Buildings;
        public ConverterSystem Converters => Ctx.Converters;
        public FuelSystem Fuel => Ctx.Fuel;
        public LogisticsSystem Logistics => Ctx.Logistics;

        /// <summary>Boot step 2 (§2.1): initArea for every area (locked ones too).</summary>
        public void InitAllAreas()
        {
            foreach (var r in Config.regions) Ctx.Nodes.InitArea(r.key);
            Ctx.Buildings.SetupStarterNetwork();     // §16 — no-op once starterPlaced
        }

        /// <summary>`gameTick()` engine.js:2325. Returns the repaint hint.</summary>
        public bool Tick()
        {
            double now = Ctx.Now;
            Ctx.Timing.BeginTick(now);
            bool changed = false;
            foreach (var reg in Config.regions)
            {
                string k = reg.key;
                if (!Ctx.World.IsAreaUnlocked(k)) continue;     // locked areas freeze
                changed |= Ctx.Nodes.TickSurfaceDives(k, now);  // 1
                changed |= Ctx.Nodes.TickRespawnQueue(k, now);  // 2
                changed |= Ctx.FieldGenerators.Tick(k, now);    // 3
                changed |= TickGeneratorBuildings(k, now);      // 4  M2+
                changed |= TickConverters(k, now);              // 5  M2+
                changed |= TickBuildingLogistics(k, now);       // 6  M2+
                changed |= TickWisps(k, now);                   // 7  M2+
                changed |= TickEnemies(k, now);                 // 8  M2+
                changed |= TickDragonScales(k, now);            // 9
                if (!Ctx.OfflineSim)                            // 10 ground physics
                {
                    if (Ctx.Ground.SettleGround(k) > 0) changed = true;
                    if (Ctx.Ground.PushOutOfColliders(k) > 0) changed = true;
                }
            }
            return changed;
        }

        // ---- tick steps 4-9 ----
        // generator buildings §8.6
        bool TickGeneratorBuildings(string areaKey, double now) => Ctx.Buildings.TickGenBuildings(areaKey, now);
        // converters + burner fuel §9-10
        bool TickConverters(string areaKey, double now) => Ctx.Converters.Tick(areaKey, now);
        // per building — Gathering Stone eject+vacuum §11.2 (M4), lantern beat §11.5 (M4), stoker §10.4, pavilion §12.4 (M5)
        bool TickBuildingLogistics(string areaKey, double now) => Ctx.Buildings.TickLogistics(areaKey, now);
        // wisp flights/arrivals §11.6
        bool TickWisps(string areaKey, double now) => Ctx.Logistics.TickWisps(areaKey, now);
        // enemies spawn + wander §7 (M6)
        bool TickEnemies(string areaKey, double now) => Ctx.Combat.Tick(areaKey, now);
        // dragon scales (center, after awakening) §12.2
        bool TickDragonScales(string areaKey, double now) => Ctx.Dragon.TickScales(areaKey, now);

        /// <summary>`automationTick()` (§13.1) — bot swings over every unlocked area; returns total clicks.</summary>
        public int AutomationTick() => Ctx.Automation.Tick();

        // ---- commands ----

        /// <summary>Player/automation swing on a node (`harvestNode`).</summary>
        public bool Harvest(string areaKey, int nodeId, bool isAuto = false, bool held = false) =>
            Ctx.Nodes.Harvest(areaKey, nodeId, isAuto, held);

        /// <summary>Left-hold vacuum step (`suctionStep`); filter = type-lock item or null.</summary>
        public SuctionResult Suction(string areaKey, double x, double y, double radius, string itemFilter = null) =>
            Ctx.Ground.SuctionStep(areaKey, x, y, radius, itemFilter);

        /// <summary>Q (+1) / E (−1) hand rotation.</summary>
        public string RotateHand(int dir) => Ctx.Hand.Rotate(dir);

        /// <summary>Right-click dispatcher (`dropFromHand`) — building feeding is an M2 hook.</summary>
        public DropResult DropFromHand(string areaKey, double x, double y, bool noGround = false) =>
            Ctx.Hand.DropFromHand(areaKey, x, y, noGround);

        // ---- M3 buildings & converters ----

        /// <summary>Place an unpaid ghost (`placeBuilding`); null if locked / invalid spot.</summary>
        public Building PlaceGhost(string areaKey, string type, int row, int col) =>
            Ctx.Buildings.PlaceGhost(areaKey, type, row, col);

        /// <summary>Why PlaceGhost would refuse ("Locked", "Off the edge", "Blocked"…); null = allowed.</summary>
        public string PlaceReason(string areaKey, string type, int row, int col) =>
            Ctx.Buildings.PlaceGhostReason(areaKey, type, row, col);

        /// <summary>`demolishBuilding` — refunds drop at the footprint centre; false for Altar/Dragon/unknown.</summary>
        public bool Demolish(string areaKey, int buildingId) => Ctx.Buildings.Demolish(areaKey, buildingId);

        /// <summary>`setRecipe` — switch a converter's active recipe (refunds per §9.2).</summary>
        public bool SetRecipe(string areaKey, int buildingId, int recipeIndex) =>
            Ctx.Converters.SetRecipe(areaKey, buildingId, recipeIndex);

        /// <summary>Left-click withdraw from a storehouse / seal / gathering stone / stoker; returns items taken.</summary>
        public int Withdraw(string areaKey, int buildingId, int n = 1)
        {
            var b = State.Area(areaKey)?.BuildingById(buildingId);
            return b == null || !b.built ? 0 : Ctx.Hand.WithdrawFromBuilding(b, n);
        }

        /// <summary>Building whose footprint holds the area-local px point (racks excluded).</summary>
        public Building BuildingAt(string areaKey, double x, double y) => Ctx.Buildings.BuildingAtPx(areaKey, x, y);

        /// <summary>`buildingStatus` — null for ghosts / no status.</summary>
        public BuildingStatusInfo BuildingStatus(string areaKey, Building b) => Ctx.Buildings.Status(areaKey, b);

        /// <summary>Converter face/panel read-out (inputs have/need/cap, craftable, progress, fuel); null for non-converters.</summary>
        public ConverterFace ConverterFace(string areaKey, Building b) => Ctx.Converters.Face(areaKey, b);

        /// <summary>Remaining build cost of a ghost (`buildingNeeds`).</summary>
        public ItemCounts BuildingNeeds(Building b) => Ctx.Buildings.Needs(b);

        public bool IsBuildingUnlocked(string type) => Ctx.Buildings.IsBuildingUnlocked(type);

        // ---- M4 logistics (§11) ----

        /// <summary>`addLink` — wire from→to onto a lantern. Returns null when added, else the refusal reason text.</summary>
        public string AddLink(string areaKey, int lanternId, int fromId, int toId) =>
            Ctx.Logistics.AddLink(areaKey, lanternId, fromId, toId)?.text;

        /// <summary>`linkRefusal` — why from→to could never carry anything (null = allowed). Code: source|target|self|types.</summary>
        public LinkRefusal LinkRefusal(string areaKey, int fromId, int toId) => Ctx.Logistics.Refusal(areaKey, fromId, toId);

        /// <summary>`removeLink(area, lanternId, index)`.</summary>
        public bool RemoveLink(string areaKey, int lanternId, int index) => Ctx.Logistics.RemoveLink(areaKey, lanternId, index);

        /// <summary>Status dot of link #index on a lantern (ui.js linkDot); null if no such link.</summary>
        public LinkStatusInfo LinkStatus(string areaKey, int lanternId, int index)
        {
            var lb = State.Area(areaKey)?.BuildingById(lanternId);
            if (lb?.links == null || index < 0 || index >= lb.links.Count) return null;
            return Ctx.Logistics.Status(lb.links[index], Ctx.Now);
        }

        public bool CanBeLinkSource(Building b) => Ctx.Logistics.CanBeLinkSource(b);
        public bool CanBeLinkTarget(Building b) => Ctx.Logistics.CanBeLinkTarget(b);
        /// <summary>Link editor: buildings that can be picked as a source.</summary>
        public System.Collections.Generic.List<Building> LinkSources(string areaKey) => Ctx.Logistics.ValidSources(areaKey);
        /// <summary>Link editor: targets the source could be linked to without refusal.</summary>
        public System.Collections.Generic.List<Building> LinkTargets(string areaKey, int fromId) => Ctx.Logistics.ValidTargets(areaKey, fromId);

        /// <summary>`wispPos` — smooth wisp position at <paramref name="now"/> (frac ≥ 1 = arrived). Wisp.returning ⇒ draw red.</summary>
        public WispPosition WispPos(string areaKey, Wisp w, double now) => Ctx.Logistics.WispPos(areaKey, w, now);

        // ---- M5 progression (§12, §13.2, §3.6) ----

        public DragonSystem Dragon => Ctx.Dragon;
        public UpgradeSystem Upgrades => Ctx.Upgrades;
        public ProgressionSystem Progression => Ctx.Progression;
        public PavilionSystem Pavilions => Ctx.Pavilions;

        // dragon (feeding = DropFromHand on the dragon building)
        /// <summary>Current stage def (null once awakened).</summary>
        public DragonStageDef DragonStage => Ctx.Dragon.CurrentStage;
        /// <summary>`dragonRemaining()` — what the current tribute still wants.</summary>
        public ItemCounts DragonRemaining() => Ctx.Dragon.Remaining();
        /// <summary>`dragonTribute(i)` — full tribute of stage i (current = paid + remaining).</summary>
        public ItemCounts DragonTribute(int stage) => Ctx.Dragon.Tribute(stage);
        /// <summary>Close the dragon's story dialog (dragon.dialog = null).</summary>
        public void DismissDragonDialog() => Ctx.Dragon.DismissDialog();
        /// <summary>Dragon murmur to show above it now (null = none).</summary>
        public string DragonMessage() => Ctx.Dragon.ActiveMessage(Ctx.Now);
        /// <summary>Ending card pending: awakened this run and not yet seen.</summary>
        public bool EndingPending => State.won && !State.endingSeen;
        public void MarkEndingSeen() => State.endingSeen = true;
        /// <summary>Active dragon blessing or null (buff.kind = pill item id, until ms).</summary>
        public BuffState ActiveBlessing() => Ctx.Dragon.ActiveBlessing(Ctx.Now);
        /// <summary>Martial Vigor or null.</summary>
        public CombatBuffState ActiveCombatBuff() => Timing.CombatBuffActive(State, Ctx.Now) ? State.combatBuff : null;
        public bool ShrineBuilt() => Ctx.Dragon.ShrineBuilt();

        // Altar upgrade tree (feeding = DropFromHand on the Altar)
        /// <summary>`selectUpgrade(area,type)` — engine rule only (maxed ⇒ false; switching refunds the old job).</summary>
        public bool SelectUpgrade(string areaKey, string type) => Ctx.Upgrades.Select(areaKey, type);
        /// <summary>Select a tree node by id, enforcing the UI selectability rule.</summary>
        public bool SelectUpgradeNode(string nodeId) => Ctx.Upgrades.SelectNode(nodeId);
        /// <summary>Cancel the job (paid items drop at the Altar).</summary>
        public void CancelUpgradeJob() => Ctx.Upgrades.RefundJob();
        public (int lvl, int max) UpgradeLevel(string areaKey, string type) => Ctx.Upgrades.Level(areaKey, type);
        /// <summary>Scaled cost of the next level; null when maxed.</summary>
        public ItemCounts UpgradeCost(string areaKey, string type) => Ctx.Upgrades.Cost(areaKey, type);
        public ItemCounts UpgradeJobRemaining() => Ctx.Upgrades.JobRemaining(State.upgradeJob);
        /// <summary>Every node's tier/selectable/level/cost/links for the tree panel.</summary>
        public System.Collections.Generic.List<UpgradeNodeState> UpgradeTree() => Ctx.Upgrades.TreeStates();

        // quests + milestone
        public QuestProgressInfo QuestProgress(int index) => Ctx.Progression.Progress(index);
        public QuestProgressInfo CurrentQuestProgress() => Ctx.Progression.CurrentProgress();
        /// <summary>`claimQuest()` — null when the active quest isn't done.</summary>
        public QuestClaim ClaimQuest() => Ctx.Progression.Claim();
        public void SetQuestPanelHidden(bool hidden) => Ctx.Progression.SetHidden(hidden);
        public (System.Collections.Generic.List<BuildingDef> reveals, System.Collections.Generic.List<ItemQty> items) QuestRewardPreview(int index) =>
            Ctx.Progression.RewardPreview(index);
        public QuestTargetInfo QuestTarget() => Ctx.Progression.Target();
        public MilestoneInfo Milestone() => Ctx.Progression.Milestone();
        /// <summary>Build-menu 🎯 targets (quest builds then milestone builds).</summary>
        public System.Collections.Generic.List<string> BuildTargets() => Ctx.Progression.BuildTargets();

        // regions
        /// <summary>`unlockArea(k)` — pays installments from the hand.</summary>
        public UnlockResult UnlockArea(string areaKey) => Ctx.Progression.UnlockArea(areaKey);
        public ItemCounts AreaUnlockCost(string areaKey) => Ctx.Progression.UnlockCost(areaKey);
        public ItemCounts UnlockPaid(string areaKey) => Ctx.Progression.UnlockPaid(areaKey);
        public ItemCounts UnlockRemaining(string areaKey) => Ctx.Progression.UnlockRemaining(areaKey);
        public bool CanPayUnlock(string areaKey) => Ctx.Progression.CanPayUnlock(areaKey);
        public (UnlockPayState state, int have, int need) UnlockPayInfo(string areaKey) => Ctx.Progression.PayState(areaKey);

        // gate
        public GateOfferingInfo GateOfferings(Building gate) => Ctx.Progression.GateOfferings(gate);
        public int AscendReward() => Ctx.Progression.AscendReward();

        // pavilion
        public bool RecruitDisciple(string areaKey, int buildingId) => Ctx.Pavilions.Recruit(areaKey, buildingId);
        /// <summary>Null when recruiting is possible, else the reason.</summary>
        public string RecruitReason(string areaKey, int buildingId) => Ctx.Pavilions.RecruitReason(areaKey, buildingId);
        public int RosterCap(Building b) => Ctx.Pavilions.RosterCap(b);

        // ---- M6 combat & automation (§7, §13.1) ----

        public CombatSystem Combat => Ctx.Combat;
        public AutomationSystem Automation => Ctx.Automation;

        /// <summary>`attackEnemy(area,id)` — one strike (+ Spirit Wave splash). Unpaced: the input layer paces
        /// clicks (100 ms cooldown, else <see cref="FlinchEnemy"/>) and holds (<see cref="AttackIntervalMs"/>,
        /// re-hit-testing <see cref="EnemyAt"/> each swing). False if no such enemy.</summary>
        public bool Attack(string areaKey, int enemyId) => Ctx.Combat.Attack(areaKey, enemyId);
        /// <summary>Too-fast click: flinch only (hitAt = now), no damage.</summary>
        public void FlinchEnemy(string areaKey, int enemyId) => Ctx.Combat.Flinch(areaKey, enemyId);
        /// <summary>`enemyAt` — first enemy within 22 px of the area-local px point, or null.</summary>
        public Enemy EnemyAt(string areaKey, double x, double y) => Ctx.Combat.EnemyAt(areaKey, x, y);
        /// <summary>Hold-attack cadence (enemies.attackMs, default 400); 0 = area has no enemies.</summary>
        public int AttackIntervalMs(string areaKey) => Ctx.Combat.AttackIntervalMs(areaKey);
        /// <summary>Current strike damage (1 + Spirit Blade + Martial Vigor + Fury).</summary>
        public int AttackDamage(string areaKey) => Ctx.Combat.Damage(areaKey, Ctx.Now);
        /// <summary>Spirit Wave splash radius in px (0 = no splash).</summary>
        public double AoeRadius(string areaKey) => Ctx.Combat.AoeRadius(areaKey);
        /// <summary>Live enemies of an area (x/y, tx/ty wander target, hp/maxHp, hitAt, kind "boss", sprite).</summary>
        public System.Collections.Generic.IReadOnlyList<Enemy> Enemies(string areaKey) =>
            (System.Collections.Generic.IReadOnlyList<Enemy>)State.Area(areaKey)?.enemies ?? System.Array.Empty<Enemy>();
        /// <summary>Automation read-out: level, budget, paused, skipped (saturated) types.</summary>
        public AutomationStatus AutomationStatus(string areaKey) => Ctx.Automation.Status(areaKey);
        /// <summary>The node's AUTO badge should be shown now (autoFlash &gt; now).</summary>
        public bool NodeAutoFlashing(Node n) => Ctx.Automation.AutoFlashing(n, Ctx.Now);
    }
}
