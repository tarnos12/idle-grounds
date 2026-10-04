using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>M6: enemies (§7) + automationTick (§13.1).</summary>
    public class CombatAutomationTests
    {
        static List<Enemy> Foxes(AreaState a) => a.enemies.Where(e => e.kind != "boss").ToList();
        static Enemy Boss(AreaState a) => a.enemies.FirstOrDefault(e => e.kind == "boss");

        static void Kill(Simulation sim, Enemy en)
        {
            int guard = 50;
            while (sim.State.Area("center").enemies.Contains(en) && guard-- > 0) sim.Attack("center", en.id);
        }

        // ---------------- enemies ----------------

        [Test]
        public void Foxes_FillCapImmediately_InsideWalkRect_AndRespawnOnOwnClock()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            var spawned = 0;
            sim.Events.EnemySpawned += (_, __) => spawned++;
            sim.Tick();
            Assert.AreEqual(1, Foxes(c).Count, "base cap 1");
            Assert.AreEqual(1, spawned);
            var fox = Foxes(c)[0];
            Assert.AreEqual(3, fox.hp); Assert.AreEqual(3, fox.maxHp);
            Assert.That(fox.x, Is.InRange(69 * 32, 92 * 32));
            Assert.That(fox.y, Is.InRange(32, 24 * 32));

            Kill(sim, fox);
            Assert.AreEqual(0, Foxes(c).Count);
            Assert.AreEqual(1, sim.State.stats.foxKills);
            // respawn = respawnMs 6000 · TEST timeScale 0.2 · prestigeFactor 1 = 1200 ms
            Assert.AreEqual(1, c.enemyRespawns.Count);
            Assert.AreEqual(clock.NowMs + 1200, c.enemyRespawns[0], 1e-6);
            clock.Advance(1150); sim.Tick();
            Assert.AreEqual(0, Foxes(c).Count, "clock not yet due");
            clock.Advance(50); sim.Tick();
            Assert.AreEqual(1, Foxes(c).Count, "due ⇒ respawned");
            Assert.AreEqual(0, c.enemyRespawns.Count);
        }

        [Test]
        public void Foxes_SpiritCallRaisesCap_PrestigeShortensRespawn_CoarseTickCatchesUp()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            c.upgrades.enemyCap = 2;
            sim.State.ascensions = 1;                          // prestigeFactor = 1/1.2
            sim.Tick();
            Assert.AreEqual(3, Foxes(c).Count, "cap 1 + Spirit Call 2");
            double t0 = clock.NowMs;
            foreach (var f in Foxes(c)) Kill(sim, f);
            Assert.AreEqual(3, sim.State.stats.foxKills);
            Assert.AreEqual(3, c.enemyRespawns.Count);
            foreach (var at in c.enemyRespawns) Assert.AreEqual(t0 + 1200 / 1.2, at, 1e-6);
            // one coarse (offline-style) tick long after: every due slot fills
            clock.Advance(10_000); sim.Tick();
            Assert.AreEqual(3, Foxes(c).Count);
            Assert.AreEqual(0, c.enemyRespawns.Count);
        }

        [Test]
        public void RespawnQueue_TruncatesToMissing_KeepingEarliestClocks()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            sim.Tick();
            Kill(sim, Foxes(c)[0]);
            c.enemyRespawns.Clear();
            double now = clock.NowMs;
            c.enemyRespawns.AddRange(new[] { now + 5000, now + 300, now + 900 });
            clock.Advance(50); sim.Tick();
            Assert.AreEqual(1, c.enemyRespawns.Count, "cap 1 ⇒ one slot clock");
            Assert.AreEqual(now + 300, c.enemyRespawns[0], 1e-6, "earliest kept");
            clock.Advance(250); sim.Tick();
            Assert.AreEqual(1, Foxes(c).Count);
        }

        [Test]
        public void Wander_StaysInWalkRect_AndIsFrameRateIndependent()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            sim.Tick();
            var fox = Foxes(c)[0];
            for (int i = 0; i < 400; i++)
            {
                clock.Advance(50); sim.Tick();
                Assert.That(fox.x, Is.InRange(69 * 32 - 1e-6, 92 * 32 + 1e-6));
                Assert.That(fox.y, Is.InRange(32 - 1e-6, 24 * 32 + 1e-6));
            }
            // a 200 ms tick moves 30 px/s · 0.2 s = 6 px toward a far target (or retargets)
            fox.x = 69 * 32; fox.y = 32; fox.tx = 92 * 32; fox.ty = 32;
            clock.Advance(200); sim.Tick();
            if (fox.tx == 92 * 32 && fox.ty == 32) Assert.AreEqual(69 * 32 + 6, fox.x, 1e-6);
        }

        [Test]
        public void Attack_DealsDamage_KillDropsLoot_StatsSfxEvents()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            sim.Tick();
            var fox = Foxes(c)[0];
            var sfx = new List<string>();
            int hits = 0, kills = 0;
            sim.Events.SoundRequested += (n, _) => sfx.Add(n);
            sim.Events.EnemyHit += (_, __) => hits++;
            sim.Events.EnemyKilled += (_, __) => kills++;
            Assert.AreSame(fox, sim.EnemyAt("center", fox.x + 15, fox.y + 15), "within 22 px");
            Assert.IsNull(sim.EnemyAt("center", fox.x + 20, fox.y + 20), "28 px away");
            Assert.AreEqual(1, sim.AttackDamage("center"));
            Assert.AreEqual(400, sim.AttackIntervalMs("center"));
            Assert.IsFalse(sim.Attack("center", 9999));

            int essBefore = SimTestUtil.CountGround(c, "spirit_essence");
            long gathered = sim.State.stats.totalGathered;
            Assert.IsTrue(sim.Attack("center", fox.id));
            Assert.AreEqual(2, fox.hp);
            Assert.AreEqual(sim.Ctx.Now, fox.hitAt, 1e-9);
            sim.Attack("center", fox.id);
            sim.Attack("center", fox.id);
            Assert.IsFalse(c.enemies.Contains(fox));
            int ess = SimTestUtil.CountGround(c, "spirit_essence") - essBefore;
            Assert.That(ess, Is.InRange(1, 2));
            Assert.AreEqual(gathered + ess, sim.State.stats.totalGathered);
            Assert.AreEqual(1, sim.State.stats.foxKills);
            Assert.AreEqual(2, hits); Assert.AreEqual(1, kills);
            Assert.AreEqual(3, sfx.Count(s => s == "hit"));
            Assert.AreEqual(1, sfx.Count(s => s == "kill"));
            // loot is a manual (graced) drop at the fox's spot
            var drop = c.ground.Last(g => g.item == "spirit_essence");
            Assert.Greater(drop.manualAt, 0);
        }

        [Test]
        public void SpiritBladeAndFury_AddDamage()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            c.upgrades.damage = 2;
            sim.State.perks.Set("fury", 1);
            Assert.AreEqual(4, sim.AttackDamage("center"));
            sim.Tick();
            var fox = Foxes(c)[0];
            sim.Attack("center", fox.id);
            Assert.IsFalse(c.enemies.Contains(fox), "4 dmg ≥ 3 hp: one strike");
        }

        [Test]
        public void MartialVigor_PlusTwoDamage_AndDoublesBeastLoot()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            sim.Hand.Add("vitality_pill", 1);
            sim.DropFromHand("center", 30 * 32, 60 * 32);    // quaff
            Assert.AreEqual(3, sim.AttackDamage("center"), "1 + Vigor 2");

            sim.Hand.Add("beast_bait", 1);
            var r = sim.DropFromHand("center", 80 * 32, 10 * 32);
            Assert.IsTrue(r.lured);
            var boar = Boss(c);
            Assert.AreEqual(8, boar.hp);
            int bone0 = SimTestUtil.CountGround(c, "beast_bone"), ess0 = SimTestUtil.CountGround(c, "spirit_essence");
            int kills0 = sim.State.stats.foxKills, rq0 = c.enemyRespawns.Count;
            sim.Attack("center", boar.id); sim.Attack("center", boar.id);
            Assert.AreEqual(2, boar.hp);
            sim.Attack("center", boar.id);
            Assert.IsNull(Boss(c));
            int bone = SimTestUtil.CountGround(c, "beast_bone") - bone0;
            Assert.That(bone, Is.EqualTo(2).Or.EqualTo(4), "beast_bone 1-2 ×2");
            Assert.AreEqual(2, SimTestUtil.CountGround(c, "spirit_essence") - ess0, "spirit_essence 1 ×2");
            Assert.AreEqual(kills0, sim.State.stats.foxKills, "bosses are not fox kills");
            Assert.AreEqual(rq0, c.enemyRespawns.Count, "bosses never respawn");

            clock.Advance(45_001);
            Assert.AreEqual(1, sim.AttackDamage("center"), "buff expired");
        }

        [Test]
        public void SpiritWave_SplashesNeighboursWithinRadius_IncludingBosses()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            c.upgrades.enemyCap = 2;
            c.upgrades.aoe = 1;                                // R = 48 px
            Assert.AreEqual(48, sim.AoeRadius("center"), 1e-9);
            sim.Tick();
            var f = Foxes(c);
            Assert.AreEqual(3, f.Count);
            f[0].x = 2500; f[0].y = 300;
            f[1].x = 2540; f[1].y = 300;                       // 40 px — splashed
            f[2].x = 2600; f[2].y = 300;                       // 100 px — not
            sim.Hand.Add("beast_bait", 1);
            sim.DropFromHand("center", 2500, 330);             // boar 30 px away
            var boar = Boss(c);
            sim.Attack("center", f[0].id);
            Assert.AreEqual(2, f[0].hp);
            Assert.AreEqual(2, f[1].hp);
            Assert.AreEqual(3, f[2].hp);
            Assert.AreEqual(7, boar.hp);
        }

        [Test]
        public void BeastBait_LuresOnlyInsideFoxZone_BoarDropsBeastBone()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            int spawned = 0;
            sim.Events.EnemySpawned += (_, e) => { if (e.kind == "boss") spawned++; };
            sim.Hand.Add("beast_bait", 3);
            var r = sim.DropFromHand("center", 30 * 32 + 16, 30 * 32 + 16);   // open ground outside cornerTR
            Assert.AreEqual(DropResultKind.Dropped, r.kind);
            Assert.IsNull(Boss(c));
            Assert.AreEqual(1, SimTestUtil.CountGround(c, "beast_bait"));

            sim.State.world.SetUnlocked("farm", true);
            var farm = sim.State.Area("farm");
            r = sim.DropFromHand("farm", 80 * 32, 10 * 32);             // farm has no enemies
            Assert.IsTrue(r == null || !r.lured);
            Assert.AreEqual(0, farm.enemies.Count);

            r = sim.DropFromHand("center", 68 * 32 + 1, 24 * 32 + 31);   // zone corner cell (r24,c68)
            Assert.IsTrue(r.lured); Assert.IsTrue(r.once);
            var boar = Boss(c);
            Assert.IsNotNull(boar);
            Assert.AreEqual(1, spawned);
            Assert.AreEqual(18, boar.spd, 1e-9);
            Assert.AreEqual(0, sim.Hand.Count("beast_bait"));
            // the boar doesn't take a fox slot
            sim.Tick();
            Assert.AreEqual(1, Foxes(c).Count);

            int bone0 = SimTestUtil.CountGround(c, "beast_bone"), ess0 = SimTestUtil.CountGround(c, "spirit_essence");
            Kill(sim, boar);
            Assert.That(SimTestUtil.CountGround(c, "beast_bone") - bone0, Is.InRange(1, 2));
            Assert.AreEqual(1, SimTestUtil.CountGround(c, "spirit_essence") - ess0);
        }

        // ---------------- automation ----------------

        static Simulation AutoSim(out ManualClock clock, int level)
        {
            var sim = SimTestUtil.NewSim(out clock);
            var c = sim.State.Area("center");
            c.ground.Clear();
            c.upgrades.automation = level;
            return sim;
        }

        [Test]
        public void Automation_ClicksPerLevel_PlusSpiritTreeTaps()
        {
            var sim = AutoSim(out _, 0);
            Assert.AreEqual(0, sim.AutomationTick());
            Assert.IsFalse(sim.State.Area("center").autoPaused);

            int bushes = SimTestUtil.CountSpawner(sim.State.Area("center"), "bush");
            Assert.AreEqual(10, bushes);
            Assert.AreEqual(2 + 1, AutoSim(out _, 1).AutomationTick(), "lvl1: 2 node clicks + 1 tree tap");
            Assert.AreEqual(6 + 2, AutoSim(out _, 2).AutomationTick(), "lvl2: 6 + 2 taps");
            Assert.AreEqual(10 + 3, AutoSim(out _, 3).AutomationTick(), "lvl3: budget 20 but only 10 nodes, + 3 taps");

            var s = AutoSim(out _, 1);
            var st = s.AutomationStatus("center");
            Assert.AreEqual(1, st.level); Assert.AreEqual(2, st.budget);
        }

        [Test]
        public void Automation_KeenAutomationPerk_AddsBudget()
        {
            var sim = AutoSim(out _, 1);
            sim.State.perks.Set("autoboost", 2);
            Assert.AreEqual(4, sim.AutomationStatus("center").budget);
            Assert.AreEqual(4 + 1, sim.AutomationTick());
        }

        [Test]
        public void Automation_OneSwingPerNode_BotDropsUngraced_AutoFlash_FixturesManualOnly()
        {
            var sim = AutoSim(out var clock, 3);
            var c = sim.State.Area("center");
            var quarry = SimTestUtil.FirstOfKind(c, "quarry");
            var tree = SimTestUtil.FirstOfKind(c, "spirittree");
            var bush = c.nodes.First(n => n.spawnerKind == "bush");
            int sfx = 0;
            sim.Events.SoundRequested += (n, _) => { if (n == "harvest") sfx++; };
            sim.AutomationTick();
            Assert.AreEqual(1, bush.hitsLeft, "chop hits 2: one swing per tick");
            Assert.AreEqual(0, quarry.clicks, "quarry rock never automated");
            Assert.AreEqual(0, tree.clicks, "3 taps with clicksPerDrop 3 ⇒ dropped and reset");
            Assert.That(SimTestUtil.CountGround(c, "wood"), Is.InRange(2, 3));
            Assert.IsTrue(sim.NodeAutoFlashing(bush));
            Assert.IsTrue(sim.NodeAutoFlashing(tree));
            Assert.IsFalse(sim.NodeAutoFlashing(quarry));
            Assert.AreEqual(0, sfx, "bot swings are silent");
            Assert.IsTrue(c.ground.All(g => g.manualAt == 0), "no player grace on bot drops");
            Assert.IsFalse(sim.Ctx.AutoHarvesting);
            // second tick finishes the bushes ⇒ leaves on the ground
            sim.AutomationTick();
            Assert.IsFalse(c.nodes.Contains(bush));
            Assert.Greater(SimTestUtil.CountGround(c, "leaves"), 0);
            // locked areas are skipped entirely
            sim.State.Area("mine").upgrades.automation = 3;
            var mineNode = sim.State.Area("mine").nodes.First(n => !n.deco && !n.isFixed);
            int hl = mineNode.hitsLeft;
            sim.AutomationTick();
            Assert.AreEqual(hl, mineNode.hitsLeft);
        }

        [Test]
        public void Automation_SkipsSaturatedTypes_CraftedLitterDoesNotCount()
        {
            var sim = AutoSim(out _, 1);
            var c = sim.State.Area("center");
            for (int i = 0; i < 120; i++) c.ground.Add(new GroundItem { id = 100000 + i, item = "leaves", x = 1500, y = 1500, crafted = true });
            Assert.AreEqual(3, sim.AutomationTick(), "crafted (class 2) leaves don't saturate");
            Assert.IsFalse(c.autoPaused);

            sim = AutoSim(out _, 1);
            c = sim.State.Area("center");
            for (int i = 0; i < 119; i++) c.ground.Add(new GroundItem { id = 100000 + i, item = "leaves", x = 1500, y = 1500 });
            Assert.AreEqual(3, sim.AutomationTick(), "119 < AUTO_SKIP_LOOSE");

            sim = AutoSim(out _, 1);
            c = sim.State.Area("center");
            for (int i = 0; i < 120; i++) c.ground.Add(new GroundItem { id = 100000 + i, item = "leaves", x = 1500, y = 1500 });
            Assert.AreEqual(1, sim.AutomationTick(), "bushes skipped, tree tap still runs");
            Assert.IsTrue(c.autoPaused);
            CollectionAssert.AreEqual(new[] { "leaves" }, c.autoSkip);
            var st = sim.AutomationStatus("center");
            Assert.IsTrue(st.paused);
            CollectionAssert.AreEqual(new[] { "leaves" }, st.skipped);

            for (int i = 0; i < 120; i++) c.ground.Add(new GroundItem { id = 200000 + i, item = "wood", x = 1500, y = 1500 });
            Assert.AreEqual(0, sim.AutomationTick(), "tree tap skipped too");
            CollectionAssert.AreEqual(new[] { "leaves", "wood" }, c.autoSkip);

            c.upgrades.automation = 0;
            sim.AutomationTick();
            Assert.IsFalse(c.autoPaused);
            Assert.AreEqual(0, c.autoSkip.Count);
        }

        [Test]
        public void Automation_CoarseGameTicks_GiveSameClicksAsFineTicks()
        {
            int Run(int tickMs)
            {
                var sim = SimTestUtil.NewSim(out var clock, seed: 777);
                sim.OfflineSim = true;
                var c = sim.State.Area("center");
                c.ground.Clear();
                foreach (var b in c.buildings) if (b.type == "gathering_stone") b.built = false;
                c.upgrades.automation = 3;
                int total = 0;
                for (int s = 0; s < 30; s++)
                {
                    for (int t = 0; t < 1000; t += tickMs) { clock.Advance(tickMs); sim.Tick(); }
                    c.ground.Clear();   // keep saturation out of it (drop amounts are RNG-order dependent)
                    total += sim.AutomationTick();
                }
                return total;
            }
            int fine = Run(50), coarse = Run(1000);
            Assert.Greater(fine, 30 * 3);
            Assert.AreEqual(fine, coarse);
        }
    }
}
