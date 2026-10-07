using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Save format (engine-systems §1.4-1.5, ADR 0001 "Saves"): the GameState
    /// object graph as JSON, written/read by reflection over the public,
    /// non-[NonSerialized] instance fields (so transients never persist and
    /// new fields need no codec change). Pure C# — no Newtonsoft, testable
    /// without Unity.
    ///
    /// Shape notes: field names = C# field names (≈ the JS GS keys);
    /// <see cref="ItemCounts"/> is a JSON object {item: qty} in insertion
    /// order; enums are strings; NaN doubles are null (lastSeen /
    /// offlineAwayFrom null = "none" — both are legacy, read but unused since
    /// ADR 0002). `schemaVersion` is stamped on every save; a save from a
    /// newer schema is refused. Spirit Bridge pairs (Building.pairIsland /
    /// pairId / pairSends) and sky wisps (GameState.skyWisps) round-trip like
    /// every other field.
    ///
    /// Load = merge onto <see cref="GameState.CreateInitial"/> (missing fields
    /// keep their defaults) + the §1.5 sanitisation rules on EVERY load
    /// (unknown ids scrubbed, clamps, defaults). Idempotent:
    /// load(save(load(x))) == load(x). Legacy web-save migrations
    /// (quest-chain remap, starter inference, scalar fuel/queue formats) are
    /// not ported — C# saves never contain them.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>Nesting deeper than this is rejected before parsing (MiniJson recurses).</summary>
        public const int MaxDepth = 64;

        // ================================================================
        // serialize
        // ================================================================

        /// <summary>
        /// `saveState()`: the state as JSON. <paramref name="lastSeenStamp"/> is
        /// written as lastSeen (informational only — no offline progress, ADR
        /// 0002); null keeps state.lastSeen. `build` is always written closed.
        /// The state itself is not modified.
        /// </summary>
        public static string Serialize(GameState s, double? lastSeenStamp = null)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var w = new JsonWriter(64 * 1024);
            w.BeginObject();
            foreach (var f in Fields(typeof(GameState)))
            {
                w.Key(f.Name);
                switch (f.Name)
                {
                    case "schemaVersion": w.Value(GameState.SchemaVersion); break;
                    case "build": w.BeginObject().Key("open").Value(false).Key("placing").Null().EndObject(); break;
                    case "lastSeen": w.Value(lastSeenStamp ?? s.lastSeen); break;
                    default: WriteValue(w, f.GetValue(s), f.FieldType); break;
                }
            }
            w.EndObject();
            return w.ToString();
        }

        static void WriteValue(JsonWriter w, object v, Type t)
        {
            if (v == null) { w.Null(); return; }
            if (t == typeof(int)) { w.Value((int)v); return; }
            if (t == typeof(long)) { w.Value((long)v); return; }
            if (t == typeof(double)) { w.Value((double)v); return; }
            if (t == typeof(float)) { w.Value((double)(float)v); return; }
            if (t == typeof(bool)) { w.Value((bool)v); return; }
            if (t == typeof(string)) { w.Value((string)v); return; }
            if (t.IsEnum) { w.Value(v.ToString()); return; }
            if (t == typeof(ItemCounts))
            {
                w.BeginObject();
                foreach (var e in (ItemCounts)v) if (e != null && e.item != null) w.Key(e.item).Value(e.qty);
                w.EndObject();
                return;
            }
            var et = ListElementType(t);
            if (et != null)
            {
                w.BeginArray();
                foreach (var x in (IList)v) WriteValue(w, x, et);
                w.EndArray();
                return;
            }
            w.BeginObject();
            foreach (var f in Fields(t)) { w.Key(f.Name); WriteValue(w, f.GetValue(v), f.FieldType); }
            w.EndObject();
        }

        // ================================================================
        // deserialize
        // ================================================================

        /// <summary>`loadState()` — the sanitised state, or null on any failure (never throws).</summary>
        public static GameState Deserialize(string json, GameConfig cfg, double now) =>
            TryDeserialize(json, cfg, now, out var s, out _) ? s : null;

        /// <summary>
        /// Parse + merge onto a fresh state + sanitise. False (state null,
        /// reason set) when the text is empty, not JSON, too deep, not a C#
        /// save (missing `areas` array / `world` object), from a newer schema,
        /// or anything throws. Never throws.
        /// </summary>
        public static bool TryDeserialize(string json, GameConfig cfg, double now, out GameState state, out string reason)
        {
            state = null;
            reason = null;
            try
            {
                if (cfg == null) { reason = "no config"; return false; }
                if (string.IsNullOrWhiteSpace(json)) { reason = "empty save"; return false; }
                if (!DepthOk(json, MaxDepth)) { reason = "nesting too deep"; return false; }
                object root;
                try { root = MiniJson.Parse(json); }
                catch (Exception e) { reason = "unparsable: " + e.Message; return false; }
                if (!(root is JsonObject o)) { reason = "root is not an object"; return false; }
                if (!(o.Get("areas") is List<object>)) { reason = "missing areas"; return false; }
                if (!(o.Get("world") is JsonObject)) { reason = "missing world"; return false; }
                if (o.Get("schemaVersion") is double sv && sv > GameState.SchemaVersion)
                { reason = "save is from a newer version (schema " + sv + ")"; return false; }

                cfg.Build();
                var fresh = GameState.CreateInitial(cfg, (long)now);
                // the generic merge overwrites these lists wholesale: keep the fresh defaults to merge by key
                var freshUnlocked = new List<RegionFlag>(fresh.world.unlocked);
                int savedGrid = o.Get("gridCells") is double gc ? (int)gc : -1;
                Populate(fresh, o);
                // lastSeen / offlineAwayFrom: absent or null ⇒ null (NaN). Legacy, unused (ADR 0002).
                fresh.lastSeen = o.Get("lastSeen") is double ls ? ls : double.NaN;
                fresh.offlineAwayFrom = o.Get("offlineAwayFrom") is double af ? af : double.NaN;
                Sanitize(fresh, cfg, now, freshUnlocked, savedGrid);
                state = fresh;
                return true;
            }
            catch (Exception e)
            {
                state = null;
                reason = "load failed: " + e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>Max bracket depth outside strings ≤ limit (guards MiniJson's recursion).</summary>
        static bool DepthOk(string s, int limit)
        {
            int d = 0;
            bool inStr = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') inStr = true;
                else if (c == '{' || c == '[') { if (++d > limit) return false; }
                else if (c == '}' || c == ']') d--;
            }
            return true;
        }

        /// <summary>Overlay every present key of <paramref name="src"/> onto <paramref name="target"/>'s fields.</summary>
        static void Populate(object target, JsonObject src)
        {
            foreach (var f in Fields(target.GetType()))
            {
                if (!src.Has(f.Name)) continue;
                var cur = f.GetValue(target);
                if (Convert(f.FieldType, src.Get(f.Name), cur, out var v)) f.SetValue(target, v);
            }
        }

        /// <summary>
        /// JSON value → field value. False = keep the field's current value
        /// (wrong type, or null for a field whose default is non-null).
        /// Doubles: null / non-number ⇒ NaN (sanitised later).
        /// </summary>
        static bool Convert(Type t, object j, object cur, out object v)
        {
            v = null;
            if (t == typeof(double))
            {
                v = j is double d ? d : double.NaN;
                return true;
            }
            if (t == typeof(float)) { v = j is double fd ? (float)fd : float.NaN; return true; }
            if (t == typeof(int))
            {
                if (!(j is double d) || double.IsNaN(d)) return false;
                v = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Floor(d)));
                return true;
            }
            if (t == typeof(long))
            {
                if (!(j is double d) || double.IsNaN(d)) return false;
                v = (long)Math.Max(-9e15, Math.Min(9e15, Math.Floor(d)));
                return true;
            }
            if (t == typeof(bool))
            {
                if (j is bool b) { v = b; return true; }
                if (j is double bd) { v = bd != 0; return true; }
                return false;
            }
            if (t == typeof(string))
            {
                if (j == null || j is string) { v = j; return true; }
                return false;
            }
            if (t.IsEnum)
            {
                if (j is string es)
                {
                    try { v = Enum.Parse(t, es, true); return true; }
                    catch (ArgumentException) { return false; }
                }
                if (j is double ed && Enum.IsDefined(t, (int)ed)) { v = Enum.ToObject(t, (int)ed); return true; }
                return false;
            }
            // reference types: null ⇒ null only where the default is null
            if (j == null) { v = null; return cur == null; }
            if (t == typeof(ItemCounts))
            {
                if (!(j is JsonObject jo)) return false;
                var ic = new ItemCounts();
                foreach (var kv in jo.Entries)
                    if (kv.Value is double q && !double.IsNaN(q) && !double.IsInfinity(q))
                        ic.Set(kv.Key, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Floor(q))));
                v = ic;
                return true;
            }
            var et = ListElementType(t);
            if (et != null)
            {
                if (!(j is List<object> ja)) return false;
                var list = (IList)Activator.CreateInstance(t);
                foreach (var x in ja)
                {
                    if (x == null && !et.IsValueType && et != typeof(string)) continue;   // drop null entries
                    if (Convert(et, x, null, out var ev))
                    {
                        if (ev == null && !et.IsValueType && et != typeof(string)) continue;
                        list.Add(ev);
                    }
                }
                v = list;
                return true;
            }
            if (t.IsClass)
            {
                if (!(j is JsonObject so)) return false;
                var obj = cur ?? Activator.CreateInstance(t);
                Populate(obj, so);
                v = obj;
                return true;
            }
            return false;
        }

        // ================================================================
        // reflection helpers
        // ================================================================

        static readonly Dictionary<Type, FieldInfo[]> _fields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Persisted fields: public instance, not [NonSerialized], not readonly/const.</summary>
        static FieldInfo[] Fields(Type t)
        {
            lock (_fields)
            {
                if (_fields.TryGetValue(t, out var fs)) return fs;
                var res = new List<FieldInfo>();
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (!f.IsNotSerialized && !f.IsInitOnly && !f.IsLiteral) res.Add(f);
                fs = res.ToArray();
                _fields[t] = fs;
                return fs;
            }
        }

        static Type ListElementType(Type t) =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) ? t.GetGenericArguments()[0] : null;

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        // ================================================================
        // sanitisation (§1.5) — runs on every load; idempotent
        // ================================================================

        /// <summary>
        /// Apply the §1.5 sanitising rules to a merged state in place (unknown
        /// ids scrubbed, clamps, defaults). Exposed for tests / the
        /// idempotence check; <see cref="TryDeserialize"/> calls it.
        /// </summary>
        public static void Sanitize(GameState s, GameConfig cfg, double now) =>
            Sanitize(s, cfg, now, null, s.gridCells);

        static void Sanitize(GameState s, GameConfig cfg, double now, List<RegionFlag> freshUnlocked, int savedGrid)
        {
            cfg.Build();
            bool Live(string it) => cfg.IsItem(it);

            s.schemaVersion = GameState.SchemaVersion;
            s.build = new BuildUiState();

            // areas: one per config region, in region order (unknown keys dropped, missing ones fresh)
            var savedAreas = s.areas ?? new List<AreaState>();
            s.areas = new List<AreaState>();
            foreach (var r in cfg.regions)
            {
                var a = savedAreas.Find(x => x != null && x.key == r.key) ?? new AreaState(r.key);
                a.key = r.key;
                s.areas.Add(a);
            }

            // world.unlocked = fresh defaults ⊕ saved (known regions, region order)
            s.world ??= new WorldState();
            var savedFlags = s.world.unlocked ?? new List<RegionFlag>();
            s.world.unlocked = new List<RegionFlag>();
            foreach (var r in cfg.regions)
            {
                var sf = savedFlags.Find(x => x != null && x.region == r.key);
                var ff = freshUnlocked?.Find(x => x.region == r.key);
                bool def = ff?.unlocked ?? r.unlockCost.Count == 0;
                s.world.unlocked.Add(new RegionFlag { region = r.key, unlocked = sf != null ? sf.unlocked : def });
            }
            // world.unlockPaid: known, still-locked regions; live items > 0; no empty maps; one entry per region
            var paidIn = s.world.unlockPaid ?? new List<RegionPaid>();
            s.world.unlockPaid = new List<RegionPaid>();
            foreach (var e in paidIn)
            {
                if (e == null || cfg.Region(e.region) == null || s.world.IsUnlocked(e.region) || e.paid == null) continue;
                if (s.world.unlockPaid.Exists(x => x.region == e.region)) continue;
                ScrubCounts(e.paid, Live, positiveOnly: true);
                if (e.paid.Count > 0) s.world.unlockPaid.Add(e);
            }

            // hand
            s.hand = s.hand ?? new List<HandStack>();
            s.hand.RemoveAll(h => h == null || !Live(h.item) || !(h.qty > 0));
            if (s.handCap <= 0) s.handCap = cfg.balance.handCap;
            if (s.handLevel < 0) s.handLevel = 0;

            // upgrade job: needs a needs map (legacy single-item jobs dropped)
            if (s.upgradeJob != null && (s.upgradeJob.needs == null || s.upgradeJob.paid == null)) s.upgradeJob = null;

            // dragon
            s.dragon ??= new DragonState();
            s.dragon.paid ??= new ItemCounts();
            ScrubCounts(s.dragon.paid, Live, positiveOnly: false);
            s.dragon.stage = Math.Max(0, Math.Min(s.dragon.stage, cfg.dragonStages.Count));
            if (!Finite(s.dragon.msgUntil)) s.dragon.msgUntil = 0;
            s.dragonScaleAt = 0;   // JS loadState never restores it: the scale timer restarts on every load (parity-audit-sim L1)

            // buffs
            if (s.buff != null && (!Finite(s.buff.until) || cfg.DragonBuff(s.buff.kind) == null)) s.buff = null;
            if (s.combatBuff != null && !Finite(s.combatBuff.until)) s.combatBuff = null;

            // prestige
            if (s.ascensions < 0) s.ascensions = 0;
            if (s.ascendPoints < 0) s.ascendPoints = 0;
            s.vows ??= new VowsState();
            var act = new List<string>();
            foreach (var id in s.vows.active ?? new List<string>())
                if (id != null && cfg.vows.Exists(v => v.id == id) && !act.Contains(id)) act.Add(id);
            s.vows.active = act;
            s.vows.done ??= new ItemCounts();
            ScrubCounts(s.vows.done, id => cfg.vows.Exists(v => v.id == id), positiveOnly: true);
            var ja = s.justAscended;
            if (ja != null && !(Finite(ja.speedFrom) && Finite(ja.speedTo))) s.justAscended = null;
            s.perks ??= new ItemCounts();
            foreach (var e in new List<ItemQty>(s.perks.entries))
            {
                var pd = cfg.Perk(e.item);
                if (pd == null || !(e.qty > 0)) s.perks.Remove(e.item);
                else if (e.qty > pd.max) s.perks.Set(e.item, pd.max);
            }

            // legacy offline stamps (unused, ADR 0002)
            if (!Finite(s.lastSeen)) s.lastSeen = double.NaN;
            if (!(Finite(s.offlineAwayFrom) && Finite(s.lastSeen) && s.offlineAwayFrom < s.lastSeen)) s.offlineAwayFrom = double.NaN;

            s.stats ??= new GameStats();
            s.flow ??= new FlowLedger();
            s.flow.Sanitize(Live);

            // quest cursor (chain stamp mismatch: a veteran skips to the end)
            s.quest ??= new QuestState();
            bool legacyChain = s.quest.chain != cfg.balance.questChain;
            s.quest.chain = cfg.balance.questChain;
            s.quest.idx = legacyChain && (s.ascensions > 0 || s.won)
                ? cfg.quests.Count
                : Math.Max(0, Math.Min(s.quest.idx, cfg.quests.Count));

            // build-menu reveal
            var bt = new List<string>();
            foreach (var t in s.builtTypes ?? new List<string>()) if (cfg.Building(t) != null && !bt.Contains(t)) bt.Add(t);
            s.builtTypes = bt;
            s.buildSeen ??= new ItemCounts();
            foreach (var e in new List<ItemQty>(s.buildSeen.entries))
            {
                if (cfg.Building(e.item) == null || !(e.qty > 0)) s.buildSeen.Remove(e.item);
                else if (e.qty > 2) s.buildSeen.Set(e.item, 2);
            }

            // per-area scrub
            bool regrid = savedGrid != cfg.grid.cells;
            s.gridCells = cfg.grid.cells;
            foreach (var a in s.areas) SanitizeArea(s, a, cfg, now, regrid, Live);
            SanitizeBridges(s, cfg, now, Live);

            // whatever non-finite double is left (no rule above) becomes 0
            ZeroNonFinite(s, 0);
        }

        static void SanitizeArea(GameState s, AreaState a, GameConfig cfg, double now, bool regrid, Func<string, bool> Live)
        {
            var reg = cfg.Region(a.key);
            a.nodes ??= new List<Node>();
            a.ground ??= new List<GroundItem>();
            a.buildings ??= new List<Building>();
            a.spawnQueue ??= new List<SpawnQueueEntry>();
            a.genTimers ??= new List<double>();
            a.enemies ??= new List<Enemy>();
            a.enemyRespawns ??= new List<double>();
            a.wisps ??= new List<Wisp>();
            a.upgrades ??= new AreaUpgrades();
            a.upgrades.paid ??= new ItemCounts();
            a.autoSkip ??= new List<string>();

            a.enemyRespawns.RemoveAll(t => !Finite(t));
            a.genTimers.RemoveAll(t => !Finite(t));
            if (regrid)
            {
                a.nodes.Clear(); a.spawnQueue.Clear(); a.enemies.Clear(); a.genTimers.Clear(); a.enemyRespawns.Clear();
            }

            // nodes: only what the CURRENT config still spawns/places (+ the centre's deco rings, kept so a
            // round trip is exact — the JS regenerated them, initArea only refills when none exist)
            a.nodes.RemoveAll(n => n == null || !(n.size > 0) ||
                !(n.deco && n.kind == "deco" ? a.key == "center"
                  : n.isFixed ? reg.fixtures.Exists(fx => fx.kind == n.kind)
                  : reg.spawners.Exists(sp => sp.kind == n.spawnerKind)));
            a.spawnQueue.RemoveAll(e => e == null || !Finite(e.at) || !reg.spawners.Exists(sp => sp.kind == e.kind));
            a.buildings.RemoveAll(b => b == null || cfg.Building(b.type) == null);

            // config-owned node fields follow the current config (same expressions as placeFixture / spawnFromSpawner)
            foreach (var n in a.nodes)
            {
                if (n.deco && n.kind == "deco") continue;
                if (n.isFixed)
                {
                    var fx = reg.fixtures.Find(f => f.kind == n.kind);
                    n.swingMs = fx.swingMs > 0 ? fx.swingMs : 1000;
                    n.sprite = string.IsNullOrEmpty(fx.sprite) ? "⛰️" : fx.sprite;
                    n.clicksPerDrop = fx.clicksPerDrop; n.dropItem = fx.drop;
                    n.dropMin = fx.dropMin; n.dropMax = fx.dropMax;
                    n.rareDrop = RareDrop.IsSet(fx.rareDrop) ? fx.rareDrop : null;
                }
                else
                {
                    var sp = reg.spawners.Find(x => x.kind == n.spawnerKind);
                    n.swingMs = sp.swingMs > 0 ? sp.swingMs : 350;
                    n.sprite = string.IsNullOrEmpty(sp.sprite) ? null : sp.sprite;
                }
                if (n.tier != 1)
                {
                    n.tier = 1;
                    int h1 = reg.tiers.Count > 0 && reg.tiers[0].hits > 0 ? reg.tiers[0].hits : 1;
                    if (n.useTiers) n.hitsLeft = Math.Min(n.hitsLeft > 0 ? n.hitsLeft : 1, h1);
                }
                if (n.pending != null) ScrubCounts(n.pending, Live, positiveOnly: false);
                if (!Finite(n.regrowSec) || n.regrowSec < 0) n.regrowSec = 0;
            }

            // ground: live items at finite spots; crafted ⇒ never gen
            a.ground.RemoveAll(g => g == null || !Live(g.item) || !Finite(g.x) || !Finite(g.y));
            foreach (var g in a.ground) if (g.crafted) g.gen = false;

            a.enemies.RemoveAll(e => e == null || !Finite(e.x) || !Finite(e.y) || !(e.hp > 0));
            foreach (var e in a.enemies)
            {
                if (e.maxHp < e.hp) e.maxHp = e.hp;
                if (!Finite(e.tx)) e.tx = e.x;
                if (!Finite(e.ty)) e.ty = e.y;
            }

            a.wisps.RemoveAll(w => w == null || !Live(w.item) || !Finite(w.x) || !Finite(w.y));
            foreach (var w in a.wisps)
            {
                // a flight without parameters restarts from where it is
                if (!Finite(w.t0)) { w.x0 = w.x; w.y0 = w.y; w.t0 = now; }
                if (!Finite(w.x0) || !Finite(w.y0)) { w.x0 = w.x; w.y0 = w.y; }
                if (!Finite(w.sp) || w.sp < 50) w.sp = 170;
            }

            var ids = new HashSet<int>();
            foreach (var b in a.buildings) ids.Add(b.id);
            var G = cfg.gateOfferings;
            foreach (var b in a.buildings)
            {
                var def = cfg.Building(b.type);
                b.paid ??= new ItemCounts();
                ScrubCounts(b.paid, Live, positiveOnly: false);
                b.inv?.RemoveAll(st => st == null || !Live(st.item) || !(st.qty > 0));
                b.links?.RemoveAll(l => l == null || !ids.Contains(l.from) || !ids.Contains(l.to));
                if (b.item != null && !Live(b.item)) { b.item = null; b.qty = 0; }
                if (b.qty < 0) b.qty = 0;
                if (b.connIdx < 0) b.connIdx = 0;

                // Ascension Gate offerings: per item (≤ perType each, ≤ cap total); offerings mirrors the total
                if (def.gate && (b.offered != null || b.offerings != 0))
                {
                    int per = G.perType > 0 ? G.perType : G.cap;
                    var off = new ItemCounts();
                    int total = 0;
                    if (b.offered != null)
                    {
                        foreach (var it in G.items)
                        {
                            int q = Math.Max(0, Math.Min(Math.Min(per, b.offered.Get(it)), G.cap - total));
                            if (q > 0) { off.Set(it, q); total += q; }
                        }
                    }
                    else
                    {
                        // count-only gate: attribute to the last items first (dragon scales, star steel, talismans)
                        int n = Math.Max(0, Math.Min(G.cap, b.offerings));
                        var by = new ItemCounts();
                        for (int i = G.items.Count - 1; i >= 0; i--)
                        {
                            int q = Math.Min(per, n);
                            if (q > 0) { by.Set(G.items[i], q); total += q; n -= q; }
                        }
                        foreach (var it in G.items) if (by.Get(it) > 0) off.Set(it, by.Get(it));
                    }
                    b.offered = off;
                    b.offerings = total;
                }

                if (def.roster.enabled)
                {
                    if (b.disciples < 0) b.disciples = 0;
                    if (b.buns < 0) b.buns = 0;
                }

                if (def.IsConverter)
                {
                    if (b.recipe < 0 || b.recipe >= def.recipes.Count) b.recipe = 0;
                    if (b.stock != null) ScrubCounts(b.stock, Live, positiveOnly: false);
                    if (!Finite(b.smeltDoneAt) || b.smeltDoneAt < 0) b.smeltDoneAt = 0;
                    if (def.fuel)
                    {
                        if (b.fuelQ != null)
                        {
                            b.fuelQ.RemoveAll(f => f == null || !cfg.IsFuel(f.item) || !Finite(f.rem) || !(f.rem > 0));
                            foreach (var f in b.fuelQ) f.total = cfg.FuelMs(f.item);
                            int slots = cfg.balance.fuelSlots;
                            if (b.fuelQ.Count > slots) b.fuelQ.RemoveRange(slots, b.fuelQ.Count - slots);
                        }
                        if (!Finite(b.fuelBurnAt)) b.fuelBurnAt = 0;
                    }
                }
            }

            a.upgrades.maxTier = 1;

            // id counters always ahead of every live id (guards hand-edited saves)
            foreach (var n in a.nodes) if (n.id >= a.nextNodeId) a.nextNodeId = n.id + 1;
            foreach (var g in a.ground) if (g.id >= a.nextGroundId) a.nextGroundId = g.id + 1;
            foreach (var b in a.buildings) if (b.id >= a.nextBuildId) a.nextBuildId = b.id + 1;
            foreach (var e in a.enemies) if (e.id >= a.nextEnemyId) a.nextEnemyId = e.id + 1;
            foreach (var w in a.wisps) if (w.id >= a.nextWispId) a.nextWispId = w.id + 1;
            if (a.nextNodeId < 1) a.nextNodeId = 1;
            if (a.nextGroundId < 1) a.nextGroundId = 1;
            if (a.nextBuildId < 1) a.nextBuildId = 1;
            if (a.nextEnemyId < 1) a.nextEnemyId = 1;
            if (a.nextWispId < 1) a.nextWispId = 1;
        }

        /// <summary>
        /// Spirit Bridges (ADR 0003): a pair survives only when both ends are bridges pointing at
        /// each other with opposite roles (else both ends are cleared); sky wisps need known
        /// Islands, a live item and finite coordinates. Runs after the per-area scrub; idempotent.
        /// </summary>
        static void SanitizeBridges(GameState s, GameConfig cfg, double now, Func<string, bool> Live)
        {
            bool IsBridge(Building b) => b != null && cfg.Building(b.type)?.bridge.enabled == true;
            foreach (var a in s.areas)
                foreach (var b in a.buildings)
                {
                    if (b.pairIsland == null) { b.pairId = 0; b.pairSends = false; continue; }
                    var o = IsBridge(b) && b.pairIsland != a.key ? s.Area(b.pairIsland)?.BuildingById(b.pairId) : null;
                    bool ok = IsBridge(o) && o.pairIsland == a.key && o.pairId == b.id && o.pairSends != b.pairSends;
                    if (!ok) { b.pairIsland = null; b.pairId = 0; b.pairSends = false; }
                }
            s.skyWisps ??= new List<SkyWisp>();
            s.skyWisps.RemoveAll(w => w == null || !Live(w.item) || cfg.Region(w.fromIsland) == null || cfg.Region(w.toIsland) == null
                                      || !Finite(w.x) || !Finite(w.y) || !Finite(w.sx) || !Finite(w.sy) || !Finite(w.tx) || !Finite(w.ty));
            foreach (var w in s.skyWisps)
            {
                if (!Finite(w.t0)) { w.x0 = w.x; w.y0 = w.y; w.t0 = now; }
                if (!Finite(w.x0) || !Finite(w.y0)) { w.x0 = w.x; w.y0 = w.y; }
                if (!Finite(w.sp) || w.sp < 50) w.sp = 170;
                if (w.id >= s.nextSkyWispId) s.nextSkyWispId = w.id + 1;
            }
            if (s.nextSkyWispId < 1) s.nextSkyWispId = 1;
        }

        /// <summary>Drop entries whose key fails <paramref name="keep"/> (and non-positive ones when asked).</summary>
        static void ScrubCounts(ItemCounts c, Func<string, bool> keep, bool positiveOnly)
        {
            c.entries.RemoveAll(e => e == null || e.item == null || !keep(e.item) || (positiveOnly && !(e.qty > 0)));
        }

        /// <summary>Every non-finite double left in the graph ⇒ 0 (lastSeen / offlineAwayFrom: NaN = null, kept).</summary>
        static void ZeroNonFinite(object o, int depth)
        {
            if (o == null || depth > 32) return;
            var t = o.GetType();
            if (o is IList list)
            {
                var et = ListElementType(t);
                if (et == typeof(double))
                {
                    for (int i = 0; i < list.Count; i++) if (!Finite((double)list[i])) list[i] = 0.0;
                }
                else if (et != null && et.IsClass && et != typeof(string))
                    foreach (var x in list) ZeroNonFinite(x, depth + 1);
                return;
            }
            if (o is ItemCounts || o is string || !t.IsClass) return;
            foreach (var f in Fields(t))
            {
                if (f.FieldType == typeof(double))
                {
                    if (o is GameState && (f.Name == "lastSeen" || f.Name == "offlineAwayFrom")) continue;
                    if (!Finite((double)f.GetValue(o))) f.SetValue(o, 0.0);
                }
                else if (f.FieldType.IsClass && f.FieldType != typeof(string))
                    ZeroNonFinite(f.GetValue(o), depth + 1);
            }
        }
    }
}
