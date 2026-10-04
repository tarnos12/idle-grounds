using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Maps the exported old-game DATA (`Assets/_Project/Data/Source/game-data.json`,
    /// written by tools/data-export/export.js; functions are `{"$fn": source}`)
    /// onto <see cref="GameConfig"/>. Quest goal lambdas are translated into
    /// <see cref="QuestGoalKind"/> + params by pattern matching their source.
    /// </summary>
    public static class GameConfigJson
    {
        public static GameConfig Load(string json)
        {
            var root = MiniJson.Parse(json) as JsonObject ?? throw new FormatException("game-data.json: root is not an object");
            var cfg = new GameConfig();

            // ---- GRID / TEST / scalars ----
            var grid = Obj(root, "GRID");
            if (grid != null)
            {
                cfg.grid.cell = Int(grid, "cell", 32);
                cfg.grid.cells = Int(grid, "cells", 93);
                cfg.grid.margin = Int(grid, "margin", 10);
                cfg.grid.gap = Int(grid, "gap", 5);
                var bs = Obj(grid, "building");
                if (bs != null) { cfg.grid.buildingW = Int(bs, "w", 3); cfg.grid.buildingH = Int(bs, "h", 3); }
            }
            var test = Obj(root, "TEST");
            if (test != null)
            {
                cfg.test.enabled = Bool(test, "ENABLED", false);
                cfg.test.timeScale = Num(test, "timeScale", 1);
                cfg.test.costScale = Num(test, "costScale", 1);
            }
            var bal = cfg.balance;
            bal.handCap = Int(root, "HAND_CAP", 20);
            bal.fuelSlots = Int(root, "FUEL_SLOTS", 6);
            bal.fuelCap = Int(root, "FUEL_CAP", 60000);
            bal.questChain = Int(root, "QUEST_CHAIN", 2);
            var ac = Obj(root, "AUTOMATION_CLICKS");
            if (ac != null)
            {
                bal.automationClicks = new List<int>();
                for (int lvl = 1; ac.Has(lvl.ToString(CultureInfo.InvariantCulture)); lvl++)
                    bal.automationClicks.Add(Int(ac, lvl.ToString(CultureInfo.InvariantCulture), 0));
            }
            var vm = Arr(root, "VOW_MULT");
            if (vm != null) { bal.vowMult = new List<double>(); foreach (var v in vm) bal.vowMult.Add(ToNum(v)); }
            var ver = Obj(root, "VERSION");
            if (ver != null) { bal.versionNum = Int(ver, "num", 0); bal.versionDesc = Str(ver, "desc"); }
            var world = Obj(root, "WORLD");
            if (world != null) { bal.worldCols = Int(world, "cols", 3); bal.worldRows = Int(world, "rows", 3); }

            // ---- items ----
            var names = Obj(root, "ITEM_NAMES");
            var icons = Obj(root, "ITEM_ICONS");
            var sources = Obj(root, "SOURCES");
            var fuel = Obj(root, "FUEL");
            if (names != null)
                foreach (var e in names.Entries)
                    cfg.items.Add(new ItemDef
                    {
                        key = e.Key,
                        name = e.Value as string,
                        emoji = icons != null ? Str(icons, e.Key) : null,
                        fuelMs = fuel != null ? Int(fuel, e.Key, 0) : 0,
                        sourceHint = sources != null ? Str(sources, e.Key) : null,
                    });

            // ---- zones ----
            var zones = Obj(root, "ZONES");
            if (zones != null)
                foreach (var e in zones.Entries)
                {
                    var z = new ZoneDef { key = e.Key };
                    foreach (var r in e.Value as List<object> ?? new List<object>())
                        if (r is JsonObject ro) z.rects.Add(new ZoneRect(Int(ro, "r0", 0), Int(ro, "c0", 0), Int(ro, "r1", 0), Int(ro, "c1", 0)));
                    cfg.zones.Add(z);
                }

            // ---- regions (AREAS + WORLD + TIER_SPRITES) ----
            var areas = Obj(root, "AREAS");
            var tierSprites = Obj(root, "TIER_SPRITES");
            var wRegions = world != null ? Obj(world, "regions") : null;
            var wSide = world != null ? Obj(world, "unlockSide") : null;
            var wCost = world != null ? Obj(world, "unlockCost") : null;
            var wIslands = world != null ? Obj(world, "islands") : null;
            if (areas != null)
                foreach (var e in areas.Entries)
                {
                    var a = (JsonObject)e.Value;
                    var reg = new RegionDef
                    {
                        key = e.Key,
                        name = Str(a, "name"),
                        icon = Str(a, "icon"),
                        verb = Str(a, "verb"),
                        actionIcon = Str(a, "actionIcon"),
                        baseItem = Str(a, "base"),
                        speedLabel = Str(a, "speedLabel"),
                        timerLabel = Str(a, "timerLabel"),
                        surfaceWindow = Num(a, "surfaceWindow", 0),
                        tierSprite = tierSprites != null ? Str(tierSprites, e.Key) : null,
                    };
                    var nb = a.Get("noBuild");
                    if (nb is string s) reg.noBuild.Add(s);
                    else if (nb is List<object> nl) foreach (var x in nl) reg.noBuild.Add(x as string);
                    var wr = wRegions != null ? Obj(wRegions, e.Key) : null;
                    if (wr != null) { reg.rx = Int(wr, "rx", 0); reg.ry = Int(wr, "ry", 0); }
                    // ADR 0003: default Island offsets; absent ⇒ the old neighbour grid spread out (93 cells + 30 sky)
                    var wi = wIslands != null ? Obj(wIslands, e.Key) : null;
                    reg.islandCol = wi != null ? Int(wi, "col", 0) : reg.rx * 123;
                    reg.islandRow = wi != null ? Int(wi, "row", 0) : reg.ry * 123;
                    reg.unlockSide = wSide != null ? Str(wSide, e.Key) : null;
                    reg.unlockCost = Map(wCost != null ? Obj(wCost, e.Key) : null);

                    foreach (var t in Objs(a, "tiers"))
                        reg.tiers.Add(new TierDef
                        {
                            name = Str(t, "name"), hits = Int(t, "hits", 0),
                            perHit = Specs(t, "perHit"), drops = Specs(t, "drops"), timer = Num(t, "timer", 0),
                        });
                    foreach (var sp in Objs(a, "spawners"))
                    {
                        var d = new SpawnerDef
                        {
                            kind = Str(sp, "kind"), zone = Str(sp, "zone"),
                            target = Int(sp, "target", 0),
                            scaleWithArea = !(sp.Get("scaleWithArea") is bool sw) || sw,
                            spacing = Num(sp, "spacing", 0),
                            interaction = Interaction(Str(sp, "interaction")),
                            useTiers = Bool(sp, "useTiers", false),
                            swingMs = Int(sp, "swingMs", 0),
                            sprite = Str(sp, "sprite"),
                            hits = Int(sp, "hits", 0),
                            regrow = Num(sp, "regrow", 0),
                            perHit = Specs(sp, "perHit"), drops = Specs(sp, "drops"),
                            rareDrop = Rare(sp),
                        };
                        foreach (var v in Arr(sp, "sizes") ?? new List<object>()) d.sizes.Add(ToInt(v));
                        reg.spawners.Add(d);
                    }
                    foreach (var fx in Objs(a, "fixtures"))
                        reg.fixtures.Add(new FixtureDef
                        {
                            kind = Str(fx, "kind"), zone = Str(fx, "zone"), size = Int(fx, "size", 1),
                            interaction = Interaction(Str(fx, "interaction")),
                            swingMs = Int(fx, "swingMs", 0), sprite = Str(fx, "sprite"),
                            clicksPerDrop = Int(fx, "clicksPerDrop", 0), drop = Str(fx, "drop"),
                            dropMin = Int(fx, "dropMin", 0), dropMax = Int(fx, "dropMax", 0),
                            autoTap = Bool(fx, "autoTap", false), rareDrop = Rare(fx),
                        });
                    foreach (var g in Objs(a, "generators"))
                        reg.generators.Add(new FieldGeneratorDef
                        {
                            kind = Str(g, "kind"), zone = Str(g, "zone"), item = Str(g, "item"),
                            intervalMs = Int(g, "intervalMs", 0), cap = Int(g, "cap", 0),
                            upgrade = Str(g, "upgrade"), rareDrop = Rare(g),
                        });
                    var en = Obj(a, "enemies");
                    if (en != null)
                    {
                        reg.enemies = new EnemyDef
                        {
                            enabled = true, zone = Str(en, "zone"), name = Str(en, "name"), sprite = Str(en, "sprite"),
                            cap = Int(en, "cap", 0), hp = Int(en, "hp", 1), speed = Num(en, "speed", 0),
                            respawnMs = Int(en, "respawnMs", 0), attackMs = Int(en, "attackMs", 0),
                            drops = Specs(en, "drops"),
                        };
                        var bs = Obj(en, "baitSpawn");
                        if (bs != null)
                            reg.enemies.baitSpawn = new BaitSpawnDef
                            {
                                enabled = true, name = Str(bs, "name"), sprite = Str(bs, "sprite"),
                                hp = Int(bs, "hp", 1), speed = Num(bs, "speed", 0), drops = Specs(bs, "drops"),
                            };
                    }
                    cfg.regions.Add(reg);
                }

            // ---- buildings ----
            var blds = Obj(root, "BUILDINGS");
            if (blds != null)
                foreach (var e in blds.Entries)
                {
                    var b = (JsonObject)e.Value;
                    var d = new BuildingDef
                    {
                        key = e.Key, name = Str(b, "name"), icon = Str(b, "icon"),
                        sizeW = cfg.grid.buildingW, sizeH = cfg.grid.buildingH,
                        cost = Map(Obj(b, "cost")),
                        unlocked = Bool(b, "unlocked", false),
                        stageUnlock = b.Has("stageUnlock") && b.Get("stageUnlock") != null ? Int(b, "stageUnlock", -1) : -1,
                        indestructible = Bool(b, "indestructible", false),
                        fuel = Bool(b, "fuel", false), anyZone = Bool(b, "anyZone", false),
                        waterOnly = Bool(b, "waterOnly", false), cap = Int(b, "cap", 0),
                        shrine = Bool(b, "shrine", false), gate = Bool(b, "gate", false),
                    };
                    var sz = Obj(b, "size");
                    if (sz != null) { d.sizeW = Int(sz, "w", 3); d.sizeH = Int(sz, "h", 3); }
                    foreach (var r in Objs(b, "recipes"))
                        d.recipes.Add(new RecipeDef
                        {
                            name = Str(r, "name"), inputs = Map(Obj(r, "inputs")), output = Str(r, "output"),
                            outputQty = Int(r, "outputQty", 1), timeMs = Int(r, "timeMs", 0), stockCap = Int(r, "stockCap", 0),
                        });
                    var gt = Obj(b, "gather");
                    if (gt != null) d.gather = new GatherConfig { enabled = true, radius = Int(gt, "radius", 0), cap = Int(gt, "cap", 0) };
                    var st = Obj(b, "stoker");
                    if (st != null) d.stoker = new StokerConfig { enabled = true, radius = Int(st, "radius", 0), cap = Int(st, "cap", 0) };
                    var ln = Obj(b, "lantern");
                    if (ln != null) d.lantern = new LanternConfig { enabled = true, rateMs = Int(ln, "rateMs", 1000), speed = Num(ln, "speed", 170) };
                    var br = Obj(b, "bridge");
                    if (br != null) d.bridge = new BridgeConfig { enabled = true, cap = Int(br, "cap", 20), rateMs = Int(br, "rateMs", 1000), speed = Num(br, "speed", 170) };
                    var sl = Obj(b, "seal");
                    if (sl != null) d.seal = new SealConfig { enabled = true, cap = Int(sl, "cap", 0) };
                    var gn = Obj(b, "gen");
                    if (gn != null) d.gen = new GenBuildingConfig { enabled = true, item = Str(gn, "item"), intervalMs = Int(gn, "intervalMs", 0), cap = Int(gn, "cap", 0) };
                    var ro = Obj(b, "roster");
                    if (ro != null)
                        d.roster = new RosterConfig
                        {
                            enabled = true, cap = Int(ro, "cap", 0), recruit = Str(ro, "recruit"), food = Str(ro, "food"),
                            foodCap = Int(ro, "foodCap", 0), foodValues = Map(Obj(ro, "foodValues")),
                            produce = Str(ro, "produce"), produceMs = Int(ro, "produceMs", 0),
                        };
                    cfg.buildings.Add(d);
                }

            // ---- dragon ----
            foreach (var s in Objs(root, "DRAGON_STAGES"))
                cfg.dragonStages.Add(new DragonStageDef { needs = Map(Obj(s, "needs")), text = Str(s, "text") });
            var buffs = Obj(root, "DRAGON_BUFFS");
            if (buffs != null)
                foreach (var e in buffs.Entries)
                {
                    var bo = (JsonObject)e.Value;
                    cfg.dragonBuffs.Add(new DragonBuffDef { item = e.Key, name = Str(bo, "name"), desc = Str(bo, "desc") });
                }
            var vit = Obj(root, "VITALITY");
            if (vit != null)
                cfg.vitality = new VitalityDef
                {
                    item = Str(vit, "item"), name = Str(vit, "name"), ms = Int(vit, "ms", 45000),
                    bonusDamage = Int(vit, "bonusDamage", 2), lootMult = Int(vit, "lootMult", 2),
                };

            // ---- upgrade tree ----
            foreach (var n in Objs(root, "UPGRADE_TREE"))
            {
                var u = new UpgradeNodeDef
                {
                    id = Str(n, "id"), icon = Str(n, "icon"), name = Str(n, "name"),
                    x = Int(n, "x", 0), y = Int(n, "y", 0), area = Str(n, "area"), type = Str(n, "type"), desc = Str(n, "desc"),
                    links = Strings(n, "links"),
                };
                foreach (var c in Arr(n, "costs") ?? new List<object>())
                    u.costs.Add(new CostLevel { items = Map(c as JsonObject) });
                cfg.upgradeTree.Add(u);
            }

            // ---- quests ----
            foreach (var q in Objs(root, "QUESTS"))
            {
                var qd = new QuestDef
                {
                    id = Str(q, "id"), icon = Str(q, "icon"), name = Str(q, "name"), desc = Str(q, "desc"),
                    builds = Strings(q, "builds"),
                };
                var goal = Obj(q, "goal");
                qd.goalSource = goal != null ? Str(goal, "$fn") : null;
                TranslateGoal(qd);
                var rw = Obj(q, "reward");
                if (rw != null) { qd.rewardReveal = Strings(rw, "reveal"); qd.rewardItems = Map(Obj(rw, "items")); }
                var tg = Obj(q, "target");
                if (tg != null) qd.target = new QuestTarget { area = Str(tg, "area"), kind = Str(tg, "kind"), id = Str(tg, "id") };
                cfg.quests.Add(qd);
            }

            // ---- reveal ----
            var rev = Obj(root, "REVEAL");
            if (rev != null)
                foreach (var e in rev.Entries)
                {
                    var rr = new RevealRule { building = e.Key };
                    foreach (var c in e.Value as List<object> ?? new List<object>())
                    {
                        if (!(c is JsonObject co)) continue;
                        if (co.Has("stage")) rr.any.Add(new RevealCond { kind = RevealKind.Stage, stage = Int(co, "stage", 0) });
                        else if (co.Has("quest")) rr.any.Add(new RevealCond { kind = RevealKind.Quest, key = Str(co, "quest") });
                        else if (co.Has("islands")) rr.any.Add(new RevealCond { kind = RevealKind.Islands, count = Int(co, "islands", 2) });
                        else if (co.Has("region")) rr.any.Add(new RevealCond { kind = RevealKind.Region, key = Str(co, "region") });
                    }
                    cfg.reveal.Add(rr);
                }

            // ---- prestige ----
            foreach (var p in Objs(root, "PERKS"))
            {
                var pd = new PerkDef { id = Str(p, "id"), name = Str(p, "name"), icon = Str(p, "icon"), max = Int(p, "max", 0), desc = Str(p, "desc") };
                foreach (var c in Arr(p, "cost") ?? new List<object>()) pd.cost.Add(ToInt(c));
                cfg.perks.Add(pd);
            }
            foreach (var v in Objs(root, "VOWS"))
                cfg.vows.Add(new VowDef { id = Str(v, "id"), name = Str(v, "name"), icon = Str(v, "icon"), desc = Str(v, "desc") });
            var go = Obj(root, "GATE_OFFERINGS");
            if (go != null)
                cfg.gateOfferings = new GateOffering { items = Strings(go, "items"), cap = Int(go, "cap", 6), perType = Int(go, "perType", 2) };

            return cfg.Build();
        }

        // ---- quest goal lambda translation ----
        static readonly Regex ReTribute = new Regex(@"dragonTribute\((\d+)\)\.(\w+)");
        static readonly Regex ReCapped = new Regex(@"Math\.min\(window\.ENGINE\.handCount\(""(\w+)""\),\s*(\d+)\)");
        static readonly Regex ReAnyRegion = new Regex(@"\bu\.(\w+)");
        static readonly Regex ReRegion = new Regex(@"world\.unlocked\.(\w+)");
        static readonly Regex ReStat = new Regex(@"stats\.(\w+)");
        static readonly Regex ReStage = new Regex(@"dragon\.stage\s*>=\s*(\d+)");
        static readonly Regex ReHand = new Regex(@"handCount\(""(\w+)""\)");
        static readonly Regex ReNeed = new Regex(@"need:\s*(\d+)");

        /// <summary>Pattern-match a JS goal lambda into QuestGoalKind + params. Unrecognised ⇒ Unknown (Validate reports it).</summary>
        public static void TranslateGoal(QuestDef q)
        {
            string src = q.goalSource ?? "";
            q.goalKind = QuestGoalKind.Unknown;
            q.goalItems = new List<ItemQty>();
            q.goalRegions = new List<string>();
            var needM = ReNeed.Match(src);
            int need = needM.Success ? int.Parse(needM.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            Match m;
            if ((m = ReTribute.Match(src)).Success)
            {
                q.goalKind = QuestGoalKind.DragonTributeItem;
                q.goalStage = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                q.goalItem = m.Groups[2].Value;
                q.goalNeed = 1;     // computed at runtime: max(1, tribute[item])
                return;
            }
            var caps = ReCapped.Matches(src);
            if (caps.Count > 0)
            {
                q.goalKind = QuestGoalKind.HandCountCapped;
                foreach (Match c in caps) q.goalItems.Add(new ItemQty(c.Groups[1].Value, int.Parse(c.Groups[2].Value, CultureInfo.InvariantCulture)));
                q.goalNeed = need;
                return;
            }
            if (src.Contains("const u = window.GS.world.unlocked"))
            {
                q.goalKind = QuestGoalKind.AnyRegionUnlocked;
                foreach (Match r in ReAnyRegion.Matches(src)) if (!q.goalRegions.Contains(r.Groups[1].Value)) q.goalRegions.Add(r.Groups[1].Value);
                q.goalNeed = 1;
                return;
            }
            if ((m = ReRegion.Match(src)).Success)
            {
                q.goalKind = QuestGoalKind.AnyRegionUnlocked;
                q.goalRegions.Add(m.Groups[1].Value);
                q.goalNeed = 1;
                return;
            }
            if ((m = ReStat.Match(src)).Success)
            {
                q.goalKind = QuestGoalKind.StatAtLeast;
                q.goalStat = m.Groups[1].Value;
                q.goalNeed = need;
                return;
            }
            if ((m = ReStage.Match(src)).Success)
            {
                q.goalKind = QuestGoalKind.DragonStageAtLeast;
                q.goalStage = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                q.goalNeed = 1;
                return;
            }
            if ((m = ReHand.Match(src)).Success)
            {
                q.goalKind = QuestGoalKind.HandCount;
                q.goalItem = m.Groups[1].Value;
                q.goalNeed = need;
            }
        }

        // ---- helpers ----
        static NodeInteraction Interaction(string s)
        {
            switch (s)
            {
                case "chop": return NodeInteraction.Chop;
                case "instant": return NodeInteraction.Instant;
                case "break": return NodeInteraction.Break;
                case "surface": return NodeInteraction.Surface;
                case "quarry": return NodeInteraction.Quarry;
                default: return NodeInteraction.None;
            }
        }

        static JsonObject Obj(JsonObject o, string k) => o?.Get(k) as JsonObject;
        static List<object> Arr(JsonObject o, string k) => o?.Get(k) as List<object>;
        static IEnumerable<JsonObject> Objs(JsonObject o, string k)
        {
            var a = Arr(o, k);
            if (a == null) yield break;
            foreach (var x in a) if (x is JsonObject jo) yield return jo;
        }
        static string Str(JsonObject o, string k) => o?.Get(k) as string;
        static bool Bool(JsonObject o, string k, bool def) => o?.Get(k) is bool b ? b : def;
        static double Num(JsonObject o, string k, double def) => o?.Get(k) is double d ? d : def;
        static int Int(JsonObject o, string k, int def) => o?.Get(k) is double d ? ToInt(d) : def;
        static double ToNum(object v) => v is double d ? d : 0;
        static int ToInt(object v) => v is double d ? (int)Math.Round(d) : 0;
        static List<string> Strings(JsonObject o, string k)
        {
            var r = new List<string>();
            foreach (var x in Arr(o, k) ?? new List<object>()) if (x is string s) r.Add(s);
            return r;
        }
        static List<ItemQty> Map(JsonObject o)
        {
            var r = new List<ItemQty>();
            if (o == null) return r;
            foreach (var e in o.Entries) r.Add(new ItemQty(e.Key, ToInt(e.Value)));
            return r;
        }
        static List<DropSpec> Specs(JsonObject o, string k)
        {
            var r = new List<DropSpec>();
            foreach (var s in Objs(o, k))
            {
                int min = Int(s, "min", 1);
                r.Add(new DropSpec(Str(s, "item"), min, Int(s, "max", min)));
            }
            return r;
        }
        static RareDrop Rare(JsonObject o)
        {
            var r = Obj(o, "rareDrop");
            return r == null ? new RareDrop() : new RareDrop(Str(r, "item"), Num(r, "chance", 0));
        }
    }
}
