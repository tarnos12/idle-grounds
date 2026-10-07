using System;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// The Sleeping Dragon (engine-systems §12.1-12.2, engine.js:1932-1957,
    /// 2162-2207, 2655-2673): scaled tributes, stage advance + texts,
    /// awakening, dragon-pill blessings, scale shedding (tick step 9).
    /// </summary>
    public sealed class DragonSystem
    {
        readonly SimContext _ctx;
        public DragonSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;

        /// <summary>Dragon messages linger this long (dragon.msgUntil).</summary>
        public const int MsgMs = 8000;
        public const int ScaleIntervalMs = 45000;
        public const int ScalePileCap = 5;
        public const string ScaleItem = "dragon_scale";

        /// <summary>`dragonStage()` — current stage def, null once awakened.</summary>
        public DragonStageDef CurrentStage
        {
            get { int i = S.dragon.stage; return i >= 0 && i < Cfg.dragonStages.Count ? Cfg.dragonStages[i] : null; }
        }

        public bool Awakened => CurrentStage == null;

        /// <summary>`tributeMult(asc)` = max(0.4, 1/(1+0.25·asc)).</summary>
        public double TributeMult(int? asc = null) => Math.Max(0.4, 1.0 / (1 + 0.25 * (asc ?? S.ascensions)));

        int RestlessMult => S.VowActive("restless") ? 2 : 1;

        /// <summary>`dragonNeeds()` — full per-item tribute of the current stage.</summary>
        public ItemCounts Needs()
        {
            var res = new ItemCounts();
            NeedsInto(res);
            return res;
        }

        /// <summary>Non-allocating <see cref="Needs"/> into a caller-owned map (cleared with reuse).</summary>
        public void NeedsInto(ItemCounts into)
        {
            into.ClearReuse();
            var st = CurrentStage;
            if (st == null) return;
            double m = TributeMult();
            foreach (var n in st.needs)
                into.Set(n.item, Math.Max(1, (int)Math.Ceiling(_ctx.Timing.Scaled(n.qty) * m)) * RestlessMult);
        }

        /// <summary>`dragonRemaining()` — what the current stage still wants.</summary>
        public ItemCounts Remaining()
        {
            var rem = new ItemCounts();
            RemainingInto(rem);
            return rem;
        }

        readonly ItemCounts _needsScratch = new ItemCounts();

        /// <summary>Non-allocating <see cref="Remaining"/> into a caller-owned map (cleared with reuse).</summary>
        public void RemainingInto(ItemCounts into)
        {
            into.ClearReuse();
            if (CurrentStage == null) return;
            NeedsInto(_needsScratch);
            foreach (var e in _needsScratch)
            {
                int r = e.qty - S.dragon.paid.Get(e.item);
                if (r > 0) into.Set(e.item, r);
            }
        }

        /// <summary>Non-allocating <c>Tribute(stage).Get(item)</c>.</summary>
        public int TributeOf(int i, string item)
        {
            if (i < 0 || i >= Cfg.dragonStages.Count) return 0;
            var st = Cfg.dragonStages[i];
            bool listed = false;
            foreach (var n in st.needs) if (n.item == item) { listed = true; break; }
            if (!listed) return 0;
            if (i == S.dragon.stage)
            {
                RemainingInto(_needsScratch2);
                return S.dragon.paid.Get(item) + _needsScratch2.Get(item);
            }
            int v = 0;
            foreach (var n in st.needs) if (n.item == item) v = _ctx.Timing.Scaled(n.qty) * RestlessMult;
            return v;
        }

        readonly ItemCounts _needsScratch2 = new ItemCounts();

        /// <summary>
        /// `dragonTribute(i)` — full tribute of stage i. Current stage = paid + remaining;
        /// other stages = scaled(q)·restless WITHOUT tributeMult (faithful JS display quirk).
        /// </summary>
        public ItemCounts Tribute(int i)
        {
            var res = new ItemCounts();
            if (i < 0 || i >= Cfg.dragonStages.Count) return res;
            var st = Cfg.dragonStages[i];
            if (i == S.dragon.stage)
            {
                var rem = Remaining();
                foreach (var n in st.needs) res.Set(n.item, S.dragon.paid.Get(n.item) + rem.Get(n.item));
                return res;
            }
            foreach (var n in st.needs) res.Set(n.item, _ctx.Timing.Scaled(n.qty) * RestlessMult);
            return res;
        }

        /// <summary>`shrineBuilt()` — any built `shrine` building in any area.</summary>
        public bool ShrineBuilt()
        {
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                    if (b.built && Cfg.Building(b.type)?.shrine == true) return true;
            return false;
        }

        /// <summary>Blessing duration (ms) a pill fed now would grant.</summary>
        public double BlessingDurationMs()
        {
            int affinity = S.Area("center")?.upgrades.affinity ?? 0;
            return (60000 + 30000 * affinity + (ShrineBuilt() ? 60000 : 0))
                   * Math.Pow(1.2, S.PerkLevel("bless")) * _ctx.Timing.BuffScale;
        }

        public bool IsPill(string item) => item != null && Cfg.DragonBuff(item) != null;

        /// <summary>Active blessing (null when none / expired).</summary>
        public BuffState ActiveBlessing(double now) => S.buff != null && S.buff.until > now ? S.buff : null;

        /// <summary>
        /// dropFromHand step 4 (engine.js:1932) — always handles the press.
        /// Front pill ⇒ blessing; else feed the tribute; else bring a pill forward.
        /// </summary>
        public bool Feed(string areaKey, Building b, out DropResult result)
        {
            var H = S.hand;
            var hand = _ctx.Hand;
            double now = _ctx.Now;
            var first = H.Count > 0 ? H[0] : null;
            if (first != null && IsPill(first.item))
            {
                string kind = first.item;
                hand.Take(kind, 1);
                _ctx.Flow.Consume(kind, 1);
                S.buff = new BuffState { kind = kind, until = now + BlessingDurationMs() };
                _ctx.Events.RaiseBlessingStarted(kind, S.buff.until);
                result = new DropResult { kind = DropResultKind.Fed, item = kind, once = true };
                return true;
            }
            string pill = null;
            foreach (var h in H) if (IsPill(h.item)) { pill = h.item; break; }
            DropResult PillFront() { hand.MoveToFront(pill); return DropResult.Of(DropResultKind.Reordered, pill); }
            if (CurrentStage == null) { result = pill != null ? PillFront() : null; return true; }
            var dr = S.dragon;
            var res = hand.FeedNeeds(Remaining(), dr.paid);
            if (res != null && res.kind == DropResultKind.Fed) _ctx.Flow.Consume(res.item, 1);
            if (res != null && res.kind == DropResultKind.Fed && Remaining().Count == 0)
                Advance(areaKey, now);
            result = res ?? (pill != null ? PillFront() : null);
            return true;
        }

        /// <summary>Stage completed: stage++, paid reset, message/dialog, sfx; awakening after the last.</summary>
        void Advance(string areaKey, double now)
        {
            var st = CurrentStage;
            var dr = S.dragon;
            dr.stage++;
            dr.paid = new ItemCounts();
            dr.msg = st.text; dr.msgUntil = now + MsgMs;
            dr.dialog = st.text;
            _ctx.Events.RaiseSound("dragon", areaKey);
            _ctx.Events.RaiseDragonStageAdvanced(dr.stage, st.text);
            if (CurrentStage == null)
            {
                S.won = S.dragonBlessed = true;
                _ctx.Events.RaiseDragonAwakened();
            }
        }

        /// <summary>Dismiss the story dialog box.</summary>
        public void DismissDialog() => S.dragon.dialog = null;

        /// <summary>Dragon murmur still showing (msg until msgUntil).</summary>
        public string ActiveMessage(double now) => S.dragon.msg != null && now < S.dragon.msgUntil ? S.dragon.msg : null;

        /// <summary>Scale shedding interval (ms) right now.</summary>
        public double ScaleIntervalNowMs() =>
            ScaleIntervalMs * _ctx.Timing.TimeScale * (ShrineBuilt() ? 0.5 : 1) * _ctx.Timing.PrestigeFactor(S);

        /// <summary>gameTick step 9 (engine.js:2655) — center only, after awakening. Not catch-up.</summary>
        public bool TickScales(string areaKey, double now)
        {
            if (areaKey != "center" || CurrentStage != null) return false;
            double interval = ScaleIntervalNowMs();
            if (S.dragonScaleAt == 0) { S.dragonScaleAt = now + interval; return false; }
            if (now < S.dragonScaleAt) return false;
            S.dragonScaleAt = now + interval;
            var area = S.Area(areaKey);
            var drg = area.buildings.Find(x => x.type == "dragon");
            if (drg == null) return false;
            var (cx, cy) = _ctx.World.BuildingCenterPx(drg);
            double R = 6 * _ctx.Cell;
            int near = 0;
            foreach (var g in area.ground)
                if (g.item == ScaleItem && Math.Sqrt((g.x - cx) * (g.x - cx) + (g.y - cy) * (g.y - cy)) <= R) near++;
            if (near >= ScalePileCap) return false;
            _ctx.Ground.DropGround(areaKey, ScaleItem, 1, cx + _ctx.Rng.Rand(-60, 60), cy + 90);
            _ctx.Flow.Produce(ScaleItem, 1);
            return true;
        }
    }
}
