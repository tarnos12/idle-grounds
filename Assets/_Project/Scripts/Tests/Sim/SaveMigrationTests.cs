using System.Linq;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// ADR 0007 phase 1: schema 1 saves (ascension, perks, vows, the Ascension Gate + offerings) load into the
    /// no-ascension game (<see cref="SaveCodec.MigrateFromV1"/>).
    /// </summary>
    public class SaveMigrationTests
    {
        /// <summary>
        /// A veteran schema-1 save written from a played state: ascended twice, perks (successor and retired),
        /// active/done vows, the post-ascension card, AP, the old ending flag, Fleet Hands folded into handCap,
        /// a built Ascension Gate with offerings on the Center and a ghost one (partly paid) on the Mine.
        /// </summary>
        static string VeteranV1Json(out Simulation sim, out ManualClock clock, out Building builtGate, out Building ghostGate)
        {
            sim = SaveCodecTests.PlayedSim(out clock, 3000);
            var S = sim.State;
            S.world.SetUnlocked("mine", true);
            S.won = true;
            S.handCap = 20 + 5 + 5 * 2;            // Hand Size L1 + Fleet Hands 2
            S.handLevel = 1;
            S.bonuses.entries.Clear();
            S.legacyPerks.entries.Clear();
            S.quest.chain = 2; S.quest.idx = 3;     // an older quest chain
            builtGate = new Building { id = S.Area("center").nextBuildId++, type = "ascension_gate", row = 40, col = 70, built = true };
            S.Area("center").buildings.Add(builtGate);
            ghostGate = new Building { id = S.Area("mine").nextBuildId++, type = "ascension_gate", row = 10, col = 60, built = false };
            ghostGate.paid.Set("talisman", 3); ghostGate.paid.Set("dragon_scale", 1);
            S.Area("mine").buildings.Add(ghostGate);
            string j = SaveCodec.Serialize(S, clock.NowMs);
            j = j.Replace("\"schemaVersion\":" + GameState.SchemaVersion + ",",
                "\"schemaVersion\":1,\"ascensions\":2,\"ascendPoints\":5,\"ascendPrompt\":true,\"perkShopSeen\":true,\"dragonBlessed\":true," +
                "\"perks\":{\"haste\":2,\"hands\":2,\"slumber\":1,\"frugal\":1,\"apgain\":3,\"paths\":2,\"legacy\":0}," +
                "\"vows\":{\"active\":[\"burden\",\"restless\"],\"done\":{\"solitude\":1}}," +
                "\"justAscended\":{\"n\":2,\"ap\":7,\"speedFrom\":1,\"speedTo\":1},");
            j = j.Replace("\"endingSeen\":false", "\"endingSeen\":true");
            // the built gate carried offerings (field no longer in the C# model)
            string bg = "\"id\":" + builtGate.id + ",\"type\":\"ascension_gate\"";
            Assert.IsTrue(j.Contains(bg), "built gate serialized");
            j = j.Replace(bg, bg + ",\"offered\":{\"talisman\":2,\"dragon_scale\":1,\"star_steel\":2},\"offerings\":5");
            Assert.IsTrue(j.Contains("\"perks\":{\"haste\":2"), "v1 fields injected");
            return j;
        }

        static int Ground(GameState s, string area, string item) => s.Area(area).ground.Count(g => g.item == item);

        [Test]
        public void VeteranV1Save_MigratesCleanly()
        {
            string json = VeteranV1Json(out var sim, out var clock, out var built, out var ghost);
            var before = sim.State;
            int tal0 = Ground(before, "center", "talisman"), star0 = Ground(before, "center", "star_steel"), scale0 = Ground(before, "center", "dragon_scale");
            int mineTal0 = Ground(before, "mine", "talisman"), mineScale0 = Ground(before, "mine", "dragon_scale");

            Assert.IsTrue(SaveCodec.TryDeserialize(json, sim.Config, clock.NowMs, out var s, out var why), why);
            Assert.AreEqual(GameState.SchemaVersion, s.schemaVersion);

            // perks: all recorded; successors keep working as bonuses; retired ones give nothing
            Assert.AreEqual(2, s.legacyPerks.Get("haste"));
            Assert.AreEqual(1, s.legacyPerks.Get("frugal"));
            Assert.AreEqual(3, s.legacyPerks.Get("apgain"));
            Assert.IsFalse(s.legacyPerks.Has("legacy"), "level 0 = not owned");
            Assert.AreEqual(2, s.Bonus("haste"));
            Assert.AreEqual(1, s.Bonus("slumber"));
            Assert.AreEqual(2, s.Bonus("hands"));
            Assert.AreEqual(0, s.Bonus("frugal"));
            Assert.AreEqual(0, s.Bonus("apgain"));
            Assert.AreEqual(0, s.Bonus("paths"));

            // Fleet Hands moved out of the stored cap into the bonus: the effective cap is unchanged
            Assert.AreEqual(20 + 5, s.handCap);
            var sim2 = new Simulation(sim.Config, s, clock, new XorShiftRng(1));
            Assert.AreEqual(20 + 5 + 10, sim2.Hand.Cap());

            // the old ending flag no longer counts; the awakened dragon stays awake
            Assert.IsFalse(s.endingSeen);
            Assert.IsFalse(s.ascended);
            Assert.IsTrue(s.won);
            Assert.IsFalse(sim2.EndingPending);

            // veteran on an old chain: the tutorial chain is skipped
            Assert.AreEqual(sim.Config.quests.Count, s.quest.idx);
            Assert.AreEqual(sim.Config.balance.questChain, s.quest.chain);

            // gates gone; built: full legacy cost at its cell + offerings at the Altar; ghost: its paid part
            Assert.IsFalse(s.areas.Any(a => a.buildings.Any(b => b.type == "ascension_gate")));
            Assert.IsNull(s.Area("center").BuildingById(built.id));
            Assert.AreEqual(tal0 + 14 + 2, Ground(s, "center", "talisman"));
            Assert.AreEqual(star0 + 20 + 2, Ground(s, "center", "star_steel"));
            Assert.AreEqual(scale0 + 6 + 1, Ground(s, "center", "dragon_scale"));
            Assert.AreEqual(mineTal0 + 3, Ground(s, "mine", "talisman"));
            Assert.AreEqual(mineScale0 + 1, Ground(s, "mine", "dragon_scale"));
            double cell = sim.Config.grid.cell;
            var talAtGate = s.Area("center").ground.Where(g => g.item == "talisman" && System.Math.Abs(g.x - (70 + 2.5) * cell) < 40 && System.Math.Abs(g.y - (40 + 2.5) * cell) < 40).Count();
            Assert.AreEqual(14, talAtGate, "the gate's cost drops at its cell");
            foreach (var a in s.areas)
            {
                Assert.AreEqual(a.ground.Count, a.ground.Select(g => g.id).Distinct().Count(), "unique ground ids on " + a.key);
                Assert.IsTrue(a.ground.All(g => g.id < a.nextGroundId), "nextGroundId ahead on " + a.key);
                Assert.IsTrue(a.ground.All(g => g.x >= 0 && g.y >= 0 && g.x <= sim.Config.grid.PlayPx && g.y <= sim.Config.grid.PlayPx));
            }

            // Islands, installments and the other buildings are untouched
            Assert.IsTrue(s.world.IsUnlocked("mine"));
            Assert.IsTrue(s.world.unlockPaid.Any(e => e.region == "farm"));
            Assert.AreEqual(before.areas.Sum(a => a.buildings.Count) - 2, s.areas.Sum(a => a.buildings.Count));

            // no trace of the removed fields; migrating is one-shot and the result round-trips exactly
            string j1 = SaveCodec.Serialize(s, clock.NowMs);
            foreach (var gone in new[] { "\"ascensions\"", "\"ascendPoints\"", "\"perks\"", "\"vows\"", "\"justAscended\"", "\"perkShopSeen\"", "\"ascendPrompt\"", "\"dragonBlessed\"", "\"offered\"", "\"offerings\"" })
                StringAssert.DoesNotContain(gone, j1, gone + " dropped");
            var s3 = SaveCodec.Deserialize(j1, sim.Config, clock.NowMs);
            Assert.AreEqual(j1, SaveCodec.Serialize(s3, clock.NowMs), "load(save(migrated)) is exact");
            Assert.AreEqual(Ground(s, "center", "talisman"), Ground(s3, "center", "talisman"), "refunds are not paid twice");

            // the migrated world plays on
            for (int i = 0; i < 100; i++) { clock.Advance(50); sim2.Tick(); }
        }

        [Test]
        public void CountOnlyOfferings_AttributedFromTheLastItem()
        {
            string json = VeteranV1Json(out var sim, out var clock, out _, out _);
            json = json.Replace(",\"offered\":{\"talisman\":2,\"dragon_scale\":1,\"star_steel\":2},\"offerings\":5", ",\"offered\":null,\"offerings\":3");
            int tal0 = Ground(sim.State, "center", "talisman"), scale0 = Ground(sim.State, "center", "dragon_scale"), star0 = Ground(sim.State, "center", "star_steel");
            var s = SaveCodec.Deserialize(json, sim.Config, clock.NowMs);
            Assert.IsNotNull(s);
            Assert.AreEqual(scale0 + 6 + 2, Ground(s, "center", "dragon_scale"), "2 dragon scales (last item first)");
            Assert.AreEqual(star0 + 20 + 1, Ground(s, "center", "star_steel"), "then 1 star steel");
            Assert.AreEqual(tal0 + 14, Ground(s, "center", "talisman"));
        }

        [Test]
        public void FreshV1Save_NothingToMigrate_LoadsAsV2()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            string j = SaveCodec.Serialize(sim.State, clock.NowMs).Replace("\"schemaVersion\":" + GameState.SchemaVersion, "\"schemaVersion\":1");
            var s = SaveCodec.Deserialize(j, sim.Config, clock.NowMs);
            Assert.IsNotNull(s);
            Assert.AreEqual(0, s.bonuses.Count);
            Assert.AreEqual(0, s.legacyPerks.Count);
            Assert.AreEqual(sim.Config.balance.handCap, s.handCap);
            Assert.AreEqual(sim.State.areas.Sum(a => a.ground.Count), s.areas.Sum(a => a.ground.Count), "no refunds");
            Assert.AreEqual(sim.State.quest.idx, s.quest.idx);
        }

        [Test]
        public void NewerSchema_IsRefused()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            string j = SaveCodec.Serialize(sim.State, clock.NowMs).Replace("\"schemaVersion\":" + GameState.SchemaVersion, "\"schemaVersion\":" + (GameState.SchemaVersion + 1));
            Assert.IsFalse(SaveCodec.TryDeserialize(j, sim.Config, clock.NowMs, out _, out var why));
            StringAssert.Contains("newer", why);
        }
    }
}
