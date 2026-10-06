using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>`linkRefusal` result: code ∈ source | target | self | types (+ lantern for AddLink).</summary>
    public sealed class LinkRefusal
    {
        public readonly string code;
        public readonly string text;
        public LinkRefusal(string code, string text) { this.code = code; this.text = text; }
        public override string ToString() => code + ": " + text;
    }

    /// <summary>Link status dot (ui.js:1955 `linkDot`): red refused, amber empty/nomatch, green sent &lt; 3 s, grey idle.</summary>
    public enum LinkDot { Grey, Green, Amber, Red }

    public sealed class LinkStatusInfo
    {
        public LinkDot dot;
        /// <summary>null (ok/idle) | "empty" | "refused" | "nomatch" — the engine's l._stat.fail.</summary>
        public string fail;
        public string text;
        public LinkStatusInfo(LinkDot dot, string fail, string text) { this.dot = dot; this.fail = fail; this.text = text; }
        /// <summary>Empty instance for the non-allocating status query.</summary>
        public LinkStatusInfo() { }
        public LinkStatusInfo Set(LinkDot dot, string fail, string text) { this.dot = dot; this.fail = fail; this.text = text; return this; }
        public override string ToString() => dot + " " + text;
    }

    /// <summary>A Spirit Bridge the player could pair with (PairableBridges): Island, building, Island-local centre px, world distance px.</summary>
    public sealed class BridgeCandidate
    {
        public string island;
        public Building building;
        public double x, y;
        public double distancePx;
    }

    /// <summary>`wispPos` result: area-local px + flight fraction (≥1 = arrived).</summary>
    public struct WispPosition
    {
        public double x, y, frac;
        public WispPosition(double x, double y, double frac) { this.x = x; this.y = y; this.frac = frac; }
    }

    /// <summary>
    /// Wisp logistics (engine-systems §11, engine.js:1417-1791 + 2470-2618):
    /// Gathering Stone eject + vacuum, lantern beats (least-recently-served
    /// with rotation tie-break, in-flight reservations, back-dated catch-up
    /// wisps), wisp flight/arrival/return/drop, links (add/remove/refusal/
    /// status). Endpoint accept/give/take live in <see cref="BuildingSystem"/>.
    /// </summary>
    public sealed class LogisticsSystem
    {
        readonly SimContext _ctx;
        public LogisticsSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        BuildingSystem B => _ctx.Buildings;

        // ================================================================
        // type queries (§11.2 / §11.4)
        // ================================================================

        /// <summary>`targetTypes(t, room)` engine.js:1509 — null = any.</summary>
        public List<string> TargetTypes(Building t, bool room)
        {
            var l = new List<string>();
            return TargetTypes(t, room, l) ? l : null;
        }

        /// <summary>
        /// Non-allocating <see cref="TargetTypes(Building, bool)"/>: clears and fills <paramref name="into"/>
        /// and returns true, or returns false for "any" (null) — into is then left empty.
        /// </summary>
        public bool TargetTypes(Building t, bool room, List<string> into)
        {
            into.Clear();
            if (t == null || !t.built) return true;
            var cfg = Cfg.Building(t.type);
            if (cfg == null) return true;
            if (cfg.gather.enabled) return false;
            if (cfg.bridge.enabled)
            {
                // a receiving bridge is only emptied by local lanterns; a sending/unpaired one takes anything
                if (BridgeReceiving(t)) return true;
                return room && BuildingSystem.GatherTotal(t) >= cfg.bridge.cap;
            }
            if (cfg.seal.enabled)
            {
                if (t.item == null) return false;
                if (!(room && t.qty >= B.SealCap(cfg))) into.Add(t.item);
                return true;
            }
            if (t.type == "storehouse")
            {
                if (room && t.qty >= B.StorehouseCap()) return true;
                if (t.item == null) return false;
                into.Add(t.item);
                return true;
            }
            var fuelKeys = Cfg.FuelKeys;
            if (cfg.stoker.enabled)
            {
                for (int i = 0; i < fuelKeys.Count; i++) into.Add(fuelKeys[i]);
                return true;
            }
            if (cfg.roster.enabled)
            {
                if (cfg.roster.foodValues != null && cfg.roster.foodValues.Count > 0) foreach (var f in cfg.roster.foodValues) into.Add(f.item);
                else into.Add(cfg.roster.food);
                return true;
            }
            if (cfg.IsConverter)
            {
                var rec = _ctx.Converters.RecipeOf(t);
                if (rec != null) foreach (var i in rec.inputs) into.Add(i.item);
                if (cfg.fuel) for (int i = 0; i < fuelKeys.Count; i++) if (!into.Contains(fuelKeys[i])) into.Add(fuelKeys[i]);
                return true;
            }
            return true;
        }

        readonly List<string> _typesScratch = new List<string>();
        readonly List<string> _matchScratch = new List<string>();
        readonly List<string> _stoneAccScratch = new List<string>();
        readonly InFlight _flyScratch = new InFlight();

        /// <summary>`stoneAccepts(area,b,ever)` — null = collects everything.</summary>
        public List<string> StoneAccepts(string areaKey, Building b, bool ever = false)
        {
            var l = new List<string>();
            return StoneAccepts(areaKey, b, l, ever) ? l : null;
        }

        /// <summary>
        /// Non-allocating <see cref="StoneAccepts(string, Building, bool)"/>: clears and fills
        /// <paramref name="into"/> and returns true, or returns false for null ("collects everything").
        /// </summary>
        public bool StoneAccepts(string areaKey, Building b, List<string> into, bool ever = false)
        {
            into.Clear();
            if (b == null || Cfg.Building(b.type)?.gather.enabled != true) return false;
            var area = S.Area(areaKey);
            bool linked = false;
            var types = _typesScratch;
            foreach (var lb in area.buildings)
            {
                if (lb.links == null || lb.links.Count == 0) continue;
                foreach (var l in lb.links)
                {
                    if (l.from != b.id) continue;
                    linked = true;
                    if (!TargetTypes(area.BuildingById(l.to), !ever, types)) { into.Clear(); return false; }
                    foreach (var it in types) if (!into.Contains(it)) into.Add(it);
                }
            }
            return linked;
        }

        /// <summary>`sourceHolds(src)`.</summary>
        public bool SourceHolds(Building src)
        {
            var cfg = Cfg.Building(src.type);
            if (cfg.gather.enabled || cfg.stoker.enabled || cfg.bridge.enabled) return BuildingSystem.GatherTotal(src) > 0;
            return src.item != null && src.qty > 0;
        }

        /// <summary>`sourceMatches(src,dst)` — could dst EVER take anything src holds?</summary>
        public bool SourceMatches(Building src, Building dst)
        {
            var types = _matchScratch;
            if (!TargetTypes(dst, false, types)) return true;   // null = any
            var cfg = Cfg.Building(src.type);
            if (cfg.gather.enabled || cfg.stoker.enabled || cfg.bridge.enabled)
            {
                if (src.inv != null) foreach (var s in src.inv) if (s.qty > 0 && types.Contains(s.item)) return true;
                return false;
            }
            return src.item != null && types.Contains(src.item);
        }

        /// <summary>`pickTransfer(src,dst,fly)` — item src can send that dst accepts (null = nothing).</summary>
        public string PickTransfer(Building src, Building dst, InFlight fly)
        {
            var cfg = Cfg.Building(src.type);
            if (BridgeSending(src) || BridgeReceiving(dst)) return null;   // bridges: senders only send across the sky, receivers only give
            if (cfg.gather.enabled || cfg.stoker.enabled || cfg.bridge.enabled)
            {
                if (src.inv != null) foreach (var s in src.inv) if (s.qty > 0 && B.EndpointAccepts(dst, s.item, fly)) return s.item;
                return null;
            }
            if (cfg.seal.enabled || src.type == "storehouse")
                return src.item != null && src.qty > 0 && B.EndpointAccepts(dst, src.item, fly) ? src.item : null;
            return null;
        }

        // ================================================================
        // links (§11.4)
        // ================================================================

        public bool CanBeLinkSource(Building b)
        {
            if (b == null || !b.built) return false;
            var cfg = Cfg.Building(b.type);
            if (cfg != null && cfg.bridge.enabled) return !BridgeSending(b);   // receiving (or unpaired) bridge
            return cfg != null && (cfg.gather.enabled || cfg.stoker.enabled || cfg.seal.enabled || b.type == "storehouse");
        }

        public bool CanBeLinkTarget(Building b)
        {
            if (b == null || !b.built) return false;
            var cfg = Cfg.Building(b.type);
            if (cfg != null && cfg.bridge.enabled) return !BridgeReceiving(b);  // sending (or unpaired) bridge
            return cfg != null && (cfg.gather.enabled || cfg.stoker.enabled || cfg.seal.enabled || b.type == "storehouse" || cfg.IsConverter || cfg.roster.enabled);
        }

        /// <summary>`linkSourceTypes(src)` — null = unknown/anything.</summary>
        public List<string> LinkSourceTypes(Building src)
        {
            var cfg = Cfg.Building(src.type);
            if (cfg.stoker.enabled) return new List<string>(Cfg.FuelKeys);
            if (cfg.seal.enabled || src.type == "storehouse") return src.item != null ? new List<string> { src.item } : null;
            return null;
        }

        /// <summary>`linkTargetTypesEver(t)` — across recipe switches; null = anything.</summary>
        public List<string> LinkTargetTypesEver(Building t)
        {
            var cfg = Cfg.Building(t.type);
            if (cfg.gather.enabled) return null;
            if (cfg.seal.enabled || t.type == "storehouse") return t.item != null ? new List<string> { t.item } : null;
            if (cfg.IsConverter)
            {
                var outL = new List<string>();
                foreach (var r in cfg.recipes) foreach (var i in r.inputs) if (!outL.Contains(i.item)) outL.Add(i.item);
                if (cfg.fuel) foreach (var f in Cfg.FuelKeys) if (!outL.Contains(f)) outL.Add(f);
                return outL;
            }
            return TargetTypes(t, false);
        }

        string ItemName(string key) => Cfg.Item(key)?.name ?? key;

        /// <summary>`linkRefusal(area,from,to)` — null = the link may carry something.</summary>
        public LinkRefusal Refusal(string areaKey, int fromId, int toId)
        {
            var area = S.Area(areaKey);
            var src = area?.BuildingById(fromId);
            var dst = area?.BuildingById(toId);
            if (!CanBeLinkSource(src)) return new LinkRefusal("source", "Not a link source");
            if (!CanBeLinkTarget(dst)) return new LinkRefusal("target", "Not a link target");
            if (src.id == dst.id) return new LinkRefusal("self", "A building can't feed itself");
            var from = LinkSourceTypes(src);
            var to = LinkTargetTypesEver(dst);
            if (from == null || to == null) return null;
            foreach (var it in from) if (to.Contains(it)) return null;
            var names = new List<string>();
            for (int i = 0; i < from.Count && i < 2; i++) names.Add(ItemName(from[i]));
            string txt = string.Join("/", names) + (from.Count > 2 ? "…" : "");
            return new LinkRefusal("types", Cfg.Building(dst.type).name + " can't use " + txt);
        }

        /// <summary>`addLink` — null on success, else the refusal (code "lantern" = not a lantern).</summary>
        public LinkRefusal AddLink(string areaKey, int lanternId, int fromId, int toId)
        {
            var lb = S.Area(areaKey)?.BuildingById(lanternId);
            if (lb == null || Cfg.Building(lb.type)?.lantern.enabled != true) return new LinkRefusal("lantern", "Not a Wisp Lantern");
            var why = Refusal(areaKey, fromId, toId);
            if (why != null) return why;
            lb.links ??= new List<Link>();
            lb.links.Add(new Link { from = fromId, to = toId });
            S.stats.linksAdded++;
            return null;
        }

        /// <summary>`removeLink(area,lanternId,index)`.</summary>
        public bool RemoveLink(string areaKey, int lanternId, int index)
        {
            var lb = S.Area(areaKey)?.BuildingById(lanternId);
            if (lb == null || lb.links == null || index < 0 || index >= lb.links.Count) return false;
            lb.links.RemoveAt(index);
            if (lb.connIdx >= lb.links.Count) lb.connIdx = 0;
            return true;
        }

        /// <summary>Status dot of a link (ui.js `linkDot`); green = sent within 3 s of <paramref name="now"/>.</summary>
        public LinkStatusInfo Status(Link l, double now) => Status(l, now, new LinkStatusInfo());

        /// <summary>Non-allocating <see cref="Status(Link, double)"/>: overwrites and returns <paramref name="into"/>.</summary>
        public LinkStatusInfo Status(Link l, double now, LinkStatusInfo into)
        {
            var st = l?.stat;
            if (st != null && st.fail == "refused") return into.Set(LinkDot.Red, "refused", "Target refused the item");
            if (st != null && st.fail == "empty") return into.Set(LinkDot.Amber, "empty", "Source is empty");
            if (st != null && st.fail == "nomatch") return into.Set(LinkDot.Amber, "nomatch", "Source holds nothing this target uses");
            if (st != null && st.sentAt > 0 && now - st.sentAt < 3000) return into.Set(LinkDot.Green, null, "Sent an item just now");
            return into.Set(LinkDot.Grey, null, "Idle");
        }

        /// <summary>Built buildings in the area that may anchor a link as source (link editor picking).</summary>
        public List<Building> ValidSources(string areaKey)
        {
            var r = new List<Building>();
            foreach (var b in S.Area(areaKey).buildings) if (CanBeLinkSource(b)) r.Add(b);
            return r;
        }

        /// <summary>Built targets the given source could be linked to (no refusal).</summary>
        public List<Building> ValidTargets(string areaKey, int fromId)
        {
            var r = new List<Building>();
            foreach (var b in S.Area(areaKey).buildings)
                if (CanBeLinkTarget(b) && b.id != fromId && Refusal(areaKey, fromId, b.id) == null) r.Add(b);
            return r;
        }

        // ================================================================
        // tick step 6 — Gathering Stone (§11.2)
        // ================================================================

        /// <summary>`ejectUnwanted(area,b)` — drops stock no link target could ever take; records accEver.</summary>
        public bool EjectUnwanted(string areaKey, Building b)
        {
            // accEver is refilled in place (the building's own runtime buffer) — no list per tick
            var buf = b.accEverBuf ??= new List<string>();
            var ever = StoneAccepts(areaKey, b, buf, true) ? buf : null;
            b.accEver = ever; b.hasAccEver = true;
            if (ever == null || b.inv == null || b.inv.Count == 0) return false;
            bool any = false;
            for (int i = 0; i < b.inv.Count && !any; i++) if (!ever.Contains(b.inv[i].item)) any = true;
            if (!any) return false;                 // nothing unwanted: same result, no rebuild
            var keep = new List<HandStack>();
            bool ejected = false;
            var (cx, cy) = _ctx.World.BuildingCenterPx(b);
            foreach (var st in b.inv)
            {
                if (ever.Contains(st.item)) { keep.Add(st); continue; }
                if (st.qty > 0) _ctx.Ground.DropGround(areaKey, st.item, st.qty, cx, cy + _ctx.Cell, GroundTag.Manual);
                ejected = true;
            }
            if (ejected) b.inv = keep;
            return ejected;
        }

        /// <summary>Stone eject + vacuum for one built stone (engine.js:2474-2506).</summary>
        public bool TickStone(string areaKey, AreaState area, Building b, BuildingDef def, double now)
        {
            bool changed = EjectUnwanted(areaKey, b);
            int stoneFly = B.InFlightTo(area, b, _flyScratch).n;
            int cap = def.gather.cap;
            if (BuildingSystem.GatherTotal(b) + stoneFly >= cap) return changed;
            var (cx, cy) = _ctx.World.BuildingCenterPx(b);
            double R = def.gather.radius * _ctx.Cell;
            HashSet<int> taken = null;
            var acc = StoneAccepts(areaKey, b, _stoneAccScratch) ? _stoneAccScratch : null;
            double gap = _ctx.Timing.TickGap;
            double mul = Math.Min(13, Math.Max(1, (gap > 0 ? gap : 50) / 50));
            foreach (var g in area.ground)
            {
                if (acc != null && !acc.Contains(g.item)) continue;
                if (_ctx.Ground.InGrace(g, now)) continue;
                double dx = cx - g.x, dy = cy - g.y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > R) continue;
                if (d <= 22)
                {
                    if (BuildingSystem.GatherTotal(b) + stoneFly < cap && B.EndpointGive(b, g.item))
                    { (taken ??= ClearedTaken()).Add(g.id); changed = true; }
                    continue;
                }
                double pull = (2 + (1 - d / R) * 4) * mul;
                double step = Math.Min(pull, d);
                g.x += dx / d * step;
                g.y += dy / d * step;
                g.pullAt = now; g.hasPullTo = true; g.pullToX = cx; g.pullToY = cy;
            }
            if (taken != null) area.ground.RemoveAll(_isTakenGround ??= g => _taken.Contains(g.id));
            return changed;
        }

        // ================================================================
        // tick step 6 — Lantern beat (§11.5)
        // ================================================================

        /// <summary>Current beat interval (ms) of a lantern in this area (§11.7).</summary>
        public double BeatMs(AreaState area, BuildingDef def, double now) =>
            BeatMs(area, def.bridge.enabled ? def.bridge.rateMs : def.lantern.rateMs, now);

        /// <summary>
        /// Beat interval (ms) of a lantern or Spirit Bridge with base rate <paramref name="rateMs"/> (§11.7):
        /// Wisp Haste, Swiftwind, prestige, Wisp Gale, and Tireless Wisps (ADR 0002: +15% beat rate per level).
        /// </summary>
        public double BeatMs(AreaState area, double rateMs, double now)
        {
            int haste = area.upgrades.wispRate;
            double wind = Timing.BuffActive(S, "swiftwind_pill", now) ? 0.5 : 1;
            double rate = rateMs > 0 ? rateMs : 1000;
            return rate * _ctx.Timing.TimeScale * Math.Pow(0.85, haste) * wind * _ctx.Timing.PrestigeFactor(S) * Math.Pow(0.9, S.PerkLevel("gale"))
                   / TirelessFactor;
        }

        /// <summary>Wisp flight speed (px/s) launched by this lantern / bridge now (§11.7).</summary>
        public double WispSpeed(AreaState area, BuildingDef def, double now) =>
            WispSpeed(area, def.bridge.enabled ? def.bridge.speed : def.lantern.speed, now);

        /// <summary>Wisp flight speed (px/s) from base <paramref name="speed"/>: Wisp Haste, Swiftwind, Tireless Wisps (+15%/level).</summary>
        public double WispSpeed(AreaState area, double speed, double now)
        {
            int haste = area.upgrades.wispRate;
            double wind = Timing.BuffActive(S, "swiftwind_pill", now) ? 0.5 : 1;
            double sp = speed > 0 ? speed : 170;
            return sp * (1 + 0.25 * haste) / wind * TirelessFactor;
        }

        /// <summary>Tireless Wisps perk (save id "slumber", ADR 0002): 1 + 0.15 per level.</summary>
        public double TirelessFactor => 1 + 0.15 * S.PerkLevel(TirelessPerk);

        /// <summary>Perk id of Tireless Wisps — the old Long Slumber id, kept so existing saves keep their levels.</summary>
        public const string TirelessPerk = "slumber";

        public bool TickLantern(string areaKey, AreaState area, Building b, BuildingDef def, double now)
        {
            if (b.links == null || b.links.Count == 0 || now < b.nextSend) return false;
            bool changed = false;
            double beat = BeatMs(area, def, now);
            double speed = WispSpeed(area, def, now);
            var tm = _ctx.Timing.Periodic(b.nextSend, beat, now);
            double due = tm.next - tm.n * beat;
            bool idle = tm.n == 0;
            for (int ev = 0; ev < tm.n; ev++, due += beat)
            {
                int L = b.links.Count;
                int pick = -1; string pickItem = null; long pickSeq = long.MaxValue;
                for (int k = 0; k < L; k++)
                {
                    int idx = (b.connIdx + k) % L;
                    var l = b.links[idx];
                    var src = area.BuildingById(l.from);
                    var dst = area.BuildingById(l.to);
                    if (src == null || dst == null || !src.built) continue;
                    string item = PickTransfer(src, dst, B.InFlightTo(area, dst, _flyScratch));
                    l.stat ??= new LinkStat();
                    if (item == null)
                    {
                        l.stat.fail = !SourceHolds(src) ? "empty" : SourceMatches(src, dst) ? "refused" : "nomatch";
                        continue;
                    }
                    l.stat.fail = null;
                    if (l.seq < pickSeq) { pick = idx; pickItem = item; pickSeq = l.seq; }
                }
                if (pick < 0) { idle = true; break; }
                var pl = b.links[pick];
                var psrc = area.BuildingById(pl.from);
                pl.stat.sentAt = now;
                pl.seq = b.seq = b.seq + 1;
                B.EndpointTake(psrc, pickItem);
                var (sx, sy) = _ctx.World.BuildingCenterPx(psrc);
                var w = new Wisp
                {
                    id = area.nextWispId++, x0 = sx, y0 = sy, x = sx, y = sy, item = pickItem,
                    toId = pl.to, fromId = pl.from, t0 = Math.Min(now, due), sp = speed,
                };
                area.wisps.Add(w);
                b.connIdx = (pick + 1) % L;
                changed = true;
                _ctx.Events.RaiseWispLaunched(areaKey, w);
            }
            b.nextSend = idle ? now + 250 : tm.next;
            return changed;
        }

        // ================================================================
        // tick step 7 — wisps (§11.6)
        // ================================================================

        /// <summary>`wispPos(area,w,now)` — pure function of time (smooth rendering at any framerate).</summary>
        public WispPosition WispPos(string areaKey, Wisp w, double now)
        {
            var dst = S.Area(areaKey)?.BuildingById(w.toId);
            if (dst == null) return new WispPosition(w.x, w.y, 1);
            var (tx, ty) = _ctx.World.BuildingCenterPx(dst);
            double D = Math.Sqrt((tx - w.x0) * (tx - w.x0) + (ty - w.y0) * (ty - w.y0));
            double sp = w.sp > 0 ? w.sp : 170;
            double frac = D > 0 ? Math.Min(1, (now - w.t0) / 1000 * sp / D) : 1;
            return new WispPosition(w.x0 + (tx - w.x0) * frac, w.y0 + (ty - w.y0) * frac, frac);
        }

        public bool TickWisps(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            if (area.wisps == null || area.wisps.Count == 0) return false;
            bool changed = false;
            HashSet<int> done = null;
            // snapshot: event handlers must not mutate the list mid-pass (reused buffer, no per-tick array)
            var list = _wispSnap;
            list.Clear(); list.AddRange(area.wisps);
            for (int wi = 0; wi < list.Count; wi++)
            {
                var w = list[wi];
                var dst = area.BuildingById(w.toId);
                if (dst == null || !dst.built)
                {
                    _ctx.Ground.DropGround(areaKey, w.item, 1, w.x, w.y);
                    (done ??= ClearedDone()).Add(w.id); changed = true;
                    _ctx.Events.RaiseWispDropped(areaKey, w);
                    continue;
                }
                var p = WispPos(areaKey, w, now);
                w.x = p.x; w.y = p.y;
                if (p.frac >= 1)
                {
                    if (B.EndpointGive(dst, w.item))
                    {
                        (done ??= ClearedDone()).Add(w.id); changed = true;
                        _ctx.Events.RaiseWispArrived(areaKey, w);
                    }
                    else if (!w.returning && w.fromId != 0 && area.BuildingById(w.fromId) != null)
                    {
                        w.returning = true;
                        w.toId = w.fromId;
                        w.x0 = p.x; w.y0 = p.y; w.t0 = now;
                        changed = true;
                        _ctx.Events.RaiseWispReturned(areaKey, w);
                    }
                    else
                    {
                        _ctx.Ground.DropGround(areaKey, w.item, 1, p.x, p.y + 24);
                        (done ??= ClearedDone()).Add(w.id); changed = true;
                        _ctx.Events.RaiseWispDropped(areaKey, w);
                    }
                }
            }
            list.Clear();
            if (done != null) area.wisps.RemoveAll(_isDoneWisp ??= w => _done.Contains(w.id));
            return changed;
        }

        // reusable tick scratch (the sim is single-threaded; none of these passes nest)
        readonly List<Wisp> _wispSnap = new List<Wisp>();
        readonly List<SkyWisp> _skySnap = new List<SkyWisp>();
        readonly HashSet<int> _done = new HashSet<int>(), _taken = new HashSet<int>();
        Predicate<Wisp> _isDoneWisp;
        Predicate<SkyWisp> _isDoneSky;
        Predicate<GroundItem> _isTakenGround;
        HashSet<int> ClearedDone() { _done.Clear(); return _done; }
        HashSet<int> ClearedTaken() { _taken.Clear(); return _taken; }

        // ================================================================
        // Spirit Bridges (ADR 0003) — one-way cross-Island pairs
        // ================================================================

        public bool IsBridge(Building b) => b != null && Cfg.Building(b.type)?.bridge.enabled == true;
        /// <summary>Paired, and this end sends.</summary>
        public bool BridgeSending(Building b) => b != null && b.pairIsland != null && b.pairSends && IsBridge(b);
        /// <summary>Paired, and this end receives.</summary>
        public bool BridgeReceiving(Building b) => b != null && b.pairIsland != null && !b.pairSends && IsBridge(b);

        /// <summary>The partner of a paired bridge when the pair is intact (both bridges pointing at each other), else null.</summary>
        public Building PairOf(string island, Building b)
        {
            if (b == null || b.pairIsland == null || !IsBridge(b)) return null;
            var o = S.Area(b.pairIsland)?.BuildingById(b.pairId);
            if (o == null || !IsBridge(o) || o.pairIsland != island || o.pairId != b.id || o.pairSends == b.pairSends) return null;
            return o;
        }

        /// <summary>Sky wisps still heading for this bridge (its in-flight reservations).</summary>
        public int SkyInFlightTo(string island, int bridgeId)
        {
            int n = 0;
            foreach (var w in S.skyWisps) if (!w.returning && w.toIsland == island && w.toId == bridgeId) n++;
            return n;
        }

        /// <summary>Why <see cref="Pair"/> would refuse (null = allowed).</summary>
        public string PairReason(string islandA, int bridgeA, string islandB, int bridgeB)
        {
            if (S.Area(islandA) == null || S.Area(islandB) == null) return "Unknown island";
            if (islandA == islandB) return "Pair with a bridge on another island";
            if (!_ctx.World.IsAreaUnlocked(islandA) || !_ctx.World.IsAreaUnlocked(islandB)) return "Island is locked";
            var a = S.Area(islandA).BuildingById(bridgeA);
            var b = S.Area(islandB).BuildingById(bridgeB);
            if (!IsBridge(a) || !IsBridge(b)) return "Not a Spirit Bridge";
            if (!a.built || !b.built) return "Bridge not built yet";
            if (a.pairIsland != null || b.pairIsland != null) return "Already paired";
            return null;
        }

        /// <summary>Pair two bridges one-way: <paramref name="bridgeA"/> on <paramref name="islandA"/> sends to <paramref name="bridgeB"/>. Null = done, else the reason.</summary>
        public string Pair(string islandA, int bridgeA, string islandB, int bridgeB)
        {
            var why = PairReason(islandA, bridgeA, islandB, bridgeB);
            if (why != null) return why;
            var a = S.Area(islandA).BuildingById(bridgeA);
            var b = S.Area(islandB).BuildingById(bridgeB);
            a.pairIsland = islandB; a.pairId = bridgeB; a.pairSends = true; a.nextSend = 0; a.beatArmed = false;
            b.pairIsland = islandA; b.pairId = bridgeA; b.pairSends = false; b.nextSend = 0; b.beatArmed = false;
            return null;
        }

        /// <summary>Break the pair this bridge is in (both ends cleared). Wisps already in the sky return to the sender.</summary>
        public bool Unpair(string island, int bridgeId)
        {
            var b = S.Area(island)?.BuildingById(bridgeId);
            if (b == null || b.pairIsland == null) return false;
            var o = S.Area(b.pairIsland)?.BuildingById(b.pairId);
            if (o != null && o.pairIsland == island && o.pairId == b.id) ClearPair(o);
            ClearPair(b);
            return true;
        }

        static void ClearPair(Building b) { b.pairIsland = null; b.pairId = 0; b.pairSends = false; }

        /// <summary>Unpaired built bridges on OTHER unlocked Islands (the bridge panel's pairing list), Island order.</summary>
        public List<BridgeCandidate> PairableBridges(string island, int bridgeId)
        {
            var res = new List<BridgeCandidate>();
            PairableBridges(island, bridgeId, res);
            return res;
        }

        /// <summary>
        /// Non-allocating <see cref="PairableBridges(string, int)"/>: refills <paramref name="into"/>,
        /// reusing the <see cref="BridgeCandidate"/> objects already in it (extra ones go to an internal pool).
        /// </summary>
        public void PairableBridges(string island, int bridgeId, List<BridgeCandidate> into)
        {
            var pool = _candidatePool;
            for (int i = 0; i < into.Count; i++) if (into[i] != null) pool.Add(into[i]);
            into.Clear();
            var self = S.Area(island)?.BuildingById(bridgeId);
            (double x, double y) from = self != null ? _ctx.World.BuildingWorldCenterPx(island, self) : (0, 0);
            foreach (var r in Cfg.regions)
            {
                if (r.key == island || !_ctx.World.IsAreaUnlocked(r.key)) continue;
                foreach (var b in S.Area(r.key).buildings)
                {
                    if (!b.built || !IsBridge(b) || b.pairIsland != null) continue;
                    var (lx, ly) = _ctx.World.BuildingCenterPx(b);
                    var (wx, wy) = _ctx.World.BuildingWorldCenterPx(r.key, b);
                    BridgeCandidate c;
                    if (pool.Count > 0) { c = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); }
                    else c = new BridgeCandidate();
                    c.island = r.key; c.building = b; c.x = lx; c.y = ly;
                    c.distancePx = self != null ? Math.Sqrt((wx - from.x) * (wx - from.x) + (wy - from.y) * (wy - from.y)) : 0;
                    into.Add(c);
                }
            }
        }

        readonly List<BridgeCandidate> _candidatePool = new List<BridgeCandidate>();

        /// <summary>
        /// Sending bridge beat (tick step 6): lantern beat rules (Wisp Haste of the sending Island,
        /// Swiftwind, prestige, Wisp Gale, Tireless Wisps). Each beat launches one sky wisp with the
        /// buffer's first item if the receiver has room counting wisps already on the way.
        /// </summary>
        public bool TickBridge(string areaKey, AreaState area, Building b, BuildingDef def, double now)
        {
            if (!BridgeSending(b) || now < b.nextSend) return false;
            var dst = PairOf(areaKey, b);
            if (dst == null || !dst.built) { b.nextSend = now + 250; b.beatArmed = false; return false; }
            bool changed = false;
            double beat = BeatMs(area, def.bridge.rateMs, now);
            double speed = WispSpeed(area, def.bridge.speed, now);
            // An unarmed clock (fresh pair, load, or an idle poll: no cargo / no receiver room last time)
            // restarts at now and fires once — catch-up only covers beats due while the bridge was sending.
            var tm = _ctx.Timing.Periodic(b.beatArmed ? b.nextSend : now, beat, now);
            double due = tm.next - tm.n * beat;
            bool idle = tm.n == 0;
            int dstCap = Cfg.Building(dst.type).bridge.cap;
            for (int ev = 0; ev < tm.n; ev++, due += beat)
            {
                if (b.inv == null || b.inv.Count == 0) { idle = true; break; }
                if (BuildingSystem.GatherTotal(dst) + SkyInFlightTo(b.pairIsland, dst.id) >= dstCap) { idle = true; break; }
                string item = b.inv[0].item;
                if (!B.EndpointTake(b, item)) { idle = true; break; }
                var (sx, sy) = _ctx.World.BuildingWorldCenterPx(areaKey, b);
                var (tx, ty) = _ctx.World.BuildingWorldCenterPx(b.pairIsland, dst);
                var w = new SkyWisp
                {
                    id = S.nextSkyWispId++, item = item, x0 = sx, y0 = sy, x = sx, y = sy, sx = sx, sy = sy, tx = tx, ty = ty,
                    fromIsland = areaKey, fromId = b.id, toIsland = b.pairIsland, toId = dst.id,
                    t0 = Math.Min(now, due), sp = speed,
                };
                S.skyWisps.Add(w);
                changed = true;
                _ctx.Events.RaiseWispLaunched(areaKey, w);
            }
            b.nextSend = idle ? now + 250 : tm.next;
            // stays armed only while it still has cargo and receiver room for the next beat
            b.beatArmed = !idle && b.inv != null && b.inv.Count > 0
                          && BuildingSystem.GatherTotal(dst) + SkyInFlightTo(b.pairIsland, dst.id) < dstCap;
            return changed;
        }

        /// <summary>Sky wisp position (world px) at <paramref name="now"/>: straight line x0,y0 → tx,ty at sp px/s.</summary>
        public WispPosition SkyWispPos(SkyWisp w, double now)
        {
            double D = Math.Sqrt((w.tx - w.x0) * (w.tx - w.x0) + (w.ty - w.y0) * (w.ty - w.y0));
            double sp = w.sp > 0 ? w.sp : 170;
            double frac = D > 0 ? Math.Min(1, Math.Max(0, (now - w.t0) / 1000 * sp / D)) : 1;
            return new WispPosition(w.x0 + (w.tx - w.x0) * frac, w.y0 + (w.ty - w.y0) * frac, frac);
        }

        /// <summary>Turn a sky wisp around towards its sending bridge (WispReturned on the sending Island).</summary>
        void SendBack(SkyWisp w, double px, double py, double now)
        {
            w.returning = true;
            w.toIsland = w.fromIsland; w.toId = w.fromId;
            w.x0 = px; w.y0 = py; w.x = px; w.y = py; w.t0 = now;
            w.tx = w.sx; w.ty = w.sy;
            _ctx.Events.RaiseWispReturned(w.fromIsland, w);
        }

        /// <summary>
        /// Tick step 7b — sky wisps (once per tick, after every Island). Outbound: a broken pair
        /// (unpaired / either end demolished) turns it back at once; on arrival the receiver takes the
        /// item or refuses (then it returns). Returning: the sending bridge takes it back, else (gone
        /// or full) it drops on the sending Island's ground at the sender's spot.
        /// </summary>
        public bool TickSkyWisps(double now)
        {
            if (S.skyWisps == null || S.skyWisps.Count == 0) return false;
            bool changed = false;
            HashSet<int> done = null;
            var snap = _skySnap;
            snap.Clear(); snap.AddRange(S.skyWisps);
            for (int si = 0; si < snap.Count; si++)
            {
                var w = snap[si];
                var p = SkyWispPos(w, now);
                w.x = p.x; w.y = p.y;
                if (!w.returning)
                {
                    var src = S.Area(w.fromIsland)?.BuildingById(w.fromId);
                    var dst = S.Area(w.toIsland)?.BuildingById(w.toId);
                    bool intact = src != null && src.built && dst != null && dst.built && src.pairSends && PairOf(w.fromIsland, src) == dst;
                    if (!intact) { SendBack(w, p.x, p.y, now); changed = true; continue; }
                    if (p.frac < 1) continue;
                    if (B.EndpointGive(dst, w.item))
                    {
                        (done ??= ClearedDone()).Add(w.id); changed = true;
                        _ctx.Events.RaiseWispArrived(w.toIsland, w);
                    }
                    else { SendBack(w, p.x, p.y, now); changed = true; }
                    continue;
                }
                if (p.frac < 1) continue;
                var home = S.Area(w.fromIsland)?.BuildingById(w.fromId);
                if (home != null && home.built && IsBridge(home) && B.EndpointGive(home, w.item))
                {
                    (done ??= ClearedDone()).Add(w.id); changed = true;
                    _ctx.Events.RaiseWispArrived(w.fromIsland, w);
                    continue;
                }
                if (S.Area(w.fromIsland) != null)
                {
                    var (ox, oy) = _ctx.World.IslandOffsetPx(w.fromIsland);
                    _ctx.Ground.DropGround(w.fromIsland, w.item, 1, _ctx.World.ClampPx(w.sx - ox), _ctx.World.ClampPx(w.sy - oy + 24));
                }
                (done ??= ClearedDone()).Add(w.id); changed = true;
                _ctx.Events.RaiseWispDropped(w.fromIsland, w);
            }
            snap.Clear();
            if (done != null) S.skyWisps.RemoveAll(_isDoneSky ??= w => _done.Contains(w.id));
            return changed;
        }
    }
}
