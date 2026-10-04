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
        public override string ToString() => dot + " " + text;
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
            if (t == null || !t.built) return new List<string>();
            var cfg = Cfg.Building(t.type);
            if (cfg == null) return new List<string>();
            if (cfg.gather.enabled) return null;
            if (cfg.seal.enabled)
            {
                if (t.item == null) return null;
                return room && t.qty >= B.SealCap(cfg) ? new List<string>() : new List<string> { t.item };
            }
            if (t.type == "storehouse")
            {
                if (room && t.qty >= B.StorehouseCap()) return new List<string>();
                return t.item != null ? new List<string> { t.item } : null;
            }
            if (cfg.stoker.enabled) return new List<string>(Cfg.FuelKeys);
            if (cfg.roster.enabled)
            {
                var r = new List<string>();
                if (cfg.roster.foodValues != null && cfg.roster.foodValues.Count > 0) foreach (var f in cfg.roster.foodValues) r.Add(f.item);
                else r.Add(cfg.roster.food);
                return r;
            }
            if (cfg.IsConverter)
            {
                var rec = _ctx.Converters.RecipeOf(t);
                var outL = new List<string>();
                if (rec != null) foreach (var i in rec.inputs) outL.Add(i.item);
                if (cfg.fuel) foreach (var f in Cfg.FuelKeys) if (!outL.Contains(f)) outL.Add(f);
                return outL;
            }
            return new List<string>();
        }

        /// <summary>`stoneAccepts(area,b,ever)` — null = collects everything.</summary>
        public List<string> StoneAccepts(string areaKey, Building b, bool ever = false)
        {
            if (b == null || Cfg.Building(b.type)?.gather.enabled != true) return null;
            var area = S.Area(areaKey);
            List<string> outL = null;
            bool linked = false;
            foreach (var lb in area.buildings)
            {
                if (lb.links == null || lb.links.Count == 0) continue;
                foreach (var l in lb.links)
                {
                    if (l.from != b.id) continue;
                    linked = true;
                    var types = TargetTypes(area.BuildingById(l.to), !ever);
                    if (types == null) return null;
                    outL ??= new List<string>();
                    foreach (var it in types) if (!outL.Contains(it)) outL.Add(it);
                }
            }
            return linked ? outL : null;
        }

        /// <summary>`sourceHolds(src)`.</summary>
        public bool SourceHolds(Building src)
        {
            var cfg = Cfg.Building(src.type);
            if (cfg.gather.enabled || cfg.stoker.enabled) return BuildingSystem.GatherTotal(src) > 0;
            return src.item != null && src.qty > 0;
        }

        /// <summary>`sourceMatches(src,dst)` — could dst EVER take anything src holds?</summary>
        public bool SourceMatches(Building src, Building dst)
        {
            var types = TargetTypes(dst, false);
            if (types == null) return true;
            var cfg = Cfg.Building(src.type);
            if (cfg.gather.enabled || cfg.stoker.enabled)
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
            if (cfg.gather.enabled || cfg.stoker.enabled)
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
            return cfg != null && (cfg.gather.enabled || cfg.stoker.enabled || cfg.seal.enabled || b.type == "storehouse");
        }

        public bool CanBeLinkTarget(Building b)
        {
            if (b == null || !b.built) return false;
            var cfg = Cfg.Building(b.type);
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
        public LinkStatusInfo Status(Link l, double now)
        {
            var st = l?.stat;
            if (st != null && st.fail == "refused") return new LinkStatusInfo(LinkDot.Red, "refused", "Target refused the item");
            if (st != null && st.fail == "empty") return new LinkStatusInfo(LinkDot.Amber, "empty", "Source is empty");
            if (st != null && st.fail == "nomatch") return new LinkStatusInfo(LinkDot.Amber, "nomatch", "Source holds nothing this target uses");
            if (st != null && st.sentAt > 0 && now - st.sentAt < 3000) return new LinkStatusInfo(LinkDot.Green, null, "Sent an item just now");
            return new LinkStatusInfo(LinkDot.Grey, null, "Idle");
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
            var ever = StoneAccepts(areaKey, b, true);
            b.accEver = ever; b.hasAccEver = true;
            if (ever == null || b.inv == null || b.inv.Count == 0) return false;
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
            int stoneFly = B.InFlightTo(area, b).n;
            int cap = def.gather.cap;
            if (BuildingSystem.GatherTotal(b) + stoneFly >= cap) return changed;
            var (cx, cy) = _ctx.World.BuildingCenterPx(b);
            double R = def.gather.radius * _ctx.Cell;
            HashSet<int> taken = null;
            var acc = StoneAccepts(areaKey, b);
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
                    { (taken ??= new HashSet<int>()).Add(g.id); changed = true; }
                    continue;
                }
                double pull = (2 + (1 - d / R) * 4) * mul;
                double step = Math.Min(pull, d);
                g.x += dx / d * step;
                g.y += dy / d * step;
                g.pullAt = now; g.hasPullTo = true; g.pullToX = cx; g.pullToY = cy;
            }
            if (taken != null) area.ground.RemoveAll(g => taken.Contains(g.id));
            return changed;
        }

        // ================================================================
        // tick step 6 — Lantern beat (§11.5)
        // ================================================================

        /// <summary>Current beat interval (ms) of a lantern in this area (§11.7).</summary>
        public double BeatMs(AreaState area, BuildingDef def, double now)
        {
            int haste = area.upgrades.wispRate;
            double wind = Timing.BuffActive(S, "swiftwind_pill", now) ? 0.5 : 1;
            double rate = def.lantern.rateMs > 0 ? def.lantern.rateMs : 1000;
            return rate * _ctx.Timing.TimeScale * Math.Pow(0.85, haste) * wind * _ctx.Timing.PrestigeFactor(S) * Math.Pow(0.9, S.PerkLevel("gale"));
        }

        /// <summary>Wisp flight speed (px/s) launched by this lantern now (§11.7).</summary>
        public double WispSpeed(AreaState area, BuildingDef def, double now)
        {
            int haste = area.upgrades.wispRate;
            double wind = Timing.BuffActive(S, "swiftwind_pill", now) ? 0.5 : 1;
            double sp = def.lantern.speed > 0 ? def.lantern.speed : 170;
            return sp * (1 + 0.25 * haste) / wind;
        }

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
                    string item = PickTransfer(src, dst, B.InFlightTo(area, dst));
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
            // snapshot: event handlers must not mutate the list mid-pass
            var list = area.wisps.ToArray();
            foreach (var w in list)
            {
                var dst = area.BuildingById(w.toId);
                if (dst == null || !dst.built)
                {
                    _ctx.Ground.DropGround(areaKey, w.item, 1, w.x, w.y);
                    (done ??= new HashSet<int>()).Add(w.id); changed = true;
                    _ctx.Events.RaiseWispDropped(areaKey, w);
                    continue;
                }
                var p = WispPos(areaKey, w, now);
                w.x = p.x; w.y = p.y;
                if (p.frac >= 1)
                {
                    if (B.EndpointGive(dst, w.item))
                    {
                        (done ??= new HashSet<int>()).Add(w.id); changed = true;
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
                        (done ??= new HashSet<int>()).Add(w.id); changed = true;
                        _ctx.Events.RaiseWispDropped(areaKey, w);
                    }
                }
            }
            if (done != null) area.wisps.RemoveAll(w => done.Contains(w.id));
            return changed;
        }
    }
}
