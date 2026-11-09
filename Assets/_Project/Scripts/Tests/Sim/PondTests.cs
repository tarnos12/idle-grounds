using System.Linq;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Owner pick 2026-11-09 fish_zone "Only in the pond": fish and algae spawn only on the drawn frozen pond
    /// (<see cref="PondShape"/>, the same shape function that paints the pond sprite) and the Algae Farm places only there.
    /// </summary>
    public class PondTests
    {
        const string F = "fishing";

        static bool InPond(Simulation sim, int r, int c) => sim.World.CellInZone(PondShape.ZoneKey, r, c);

        [Test]
        public void PondZone_Is_The_Drawn_Pond()
        {
            var sim = SimTestUtil.NewSim(out _);
            var rects = sim.Config.ZoneRects(PondShape.ZoneKey);
            int cells = rects.Sum(z => (z.r1 - z.r0 + 1) * (z.c1 - z.c0 + 1));
            Assert.That(cells, Is.InRange(30, 140), "a ~14 x 10 cell pond, not the 43 x 43 centre");
            int N = sim.Config.grid.cells;
            for (int r = 0; r < N; r++)
                for (int c = 0; c < N; c++)
                    Assert.AreEqual(PondShape.CellIsPond(r, c), InPond(sim, r, c), $"cell {r},{c}");
            Assert.IsTrue(InPond(sim, PondShape.CentreRow, PondShape.CentreCol), "the pond centre is water");
            Assert.IsFalse(InPond(sim, 30, 30), "snow in the old centre zone is not water");
            foreach (var sp in sim.Config.Region(F).spawners) Assert.AreEqual(PondShape.ZoneKey, sp.zone, sp.kind);
        }

        [Test]
        public void FishAndAlgae_Spawn_Only_On_Pond_Cells()
        {
            var sim = SimTestUtil.NewSim(out _);
            var a = sim.State.Area(F);
            var reg = sim.Config.Region(F);
            foreach (var sp in reg.spawners) Assert.AreEqual(sim.Nodes.SpawnerTarget(sp), SimTestUtil.CountSpawner(a, sp.kind), sp.kind + " filled to target");
            for (int i = 0; i < 200; i++)
            {
                var sp = reg.spawners[i % reg.spawners.Count];
                var n = a.nodes.First(x => x.spawnerKind == sp.kind);
                sim.Nodes.DepleteNode(F, n);
                Assert.IsNotNull(sim.Nodes.SpawnFromSpawner(F, sp), "the pond has room for every " + sp.kind);
            }
            foreach (var n in a.nodes.Where(x => x.spawnerKind != null))
                Assert.IsTrue(InPond(sim, n.row, n.col), $"{n.kind} at {n.row},{n.col} is on the pond");
        }

        [Test]
        public void AlgaeFarm_Rejected_Off_The_Pond_Allowed_On_It()
        {
            var sim = M3Util.Volcano(out _);      // Algae Farm unlocked, Fishing open
            Assert.AreEqual("Water only", sim.PlaceReason(F, "algae_farm", 30, 30), "snow inside the old centre zone");
            Assert.AreEqual("Water only", sim.PlaceReason(F, "algae_farm", 60, 60), "snow south-east of the pond");
            // a 3 x 3 straddling the pond shore is refused too
            var top = sim.Config.ZoneRects(PondShape.ZoneKey).First();
            Assert.AreEqual("Water only", sim.PlaceReason(F, "algae_farm", top.r0 - 1, top.c0));
            // every fully-wet 3 x 3 is buildable, even with fish / algae surfacing on it
            var a = sim.State.Area(F);
            if (!a.nodes.Any(n => n.spawnerKind != null)) sim.Nodes.InitArea(F);
            Assert.Greater(a.nodes.Count(n => n.spawnerKind != null), 0);
            int ok = 0;
            for (int r = 30; r < 64; r++)
                for (int c = 30; c < 64; c++)
                {
                    bool wet = true;
                    for (int dr = 0; dr < 3; dr++) for (int dc = 0; dc < 3; dc++) wet &= InPond(sim, r + dr, c + dc);
                    var why = sim.PlaceReason(F, "algae_farm", r, c);
                    if (wet) { Assert.IsNull(why, $"{r},{c}"); ok++; }
                    else Assert.IsNotNull(why, $"{r},{c}");
                }
            Assert.Greater(ok, 0, "the pond fits an Algae Farm");
            // placing over a surfaced fish makes it dive (queued to respawn elsewhere in the pond)
            Node fish = null;
            int pr = -1, pc = -1;
            foreach (var n in a.nodes.Where(x => x.spawnerKind != null).ToList())
            {
                for (int r = n.row - 2; r <= n.row && pr < 0; r++)
                    for (int c = n.col - 2; c <= n.col && pr < 0; c++)
                        if (sim.PlaceReason(F, "algae_farm", r, c) == null) { pr = r; pc = c; }
                if (pr >= 0) { fish = n; break; }
            }
            Assert.IsNotNull(fish, "a farm spot over a surfacing fish / algae");
            int queued = a.spawnQueue.Count;
            Assert.IsNotNull(sim.PlaceGhost(F, "algae_farm", pr, pc));
            Assert.IsFalse(a.nodes.Contains(fish), "the node under the farm dived");
            Assert.Greater(a.spawnQueue.Count, queued, "…and will respawn");
            Assert.IsFalse(a.nodes.Any(n => n.spawnerKind != null && n.row >= pr && n.row < pr + 3 && n.col >= pc && n.col < pc + 3));
        }

        [Test]
        public void OldSave_With_Fish_On_Snow_Loads_And_Refills_In_The_Pond()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var a = sim.State.Area(F);
            // an old save: fish / algae spawned anywhere in the 43 x 43 centre, most on snow
            int moved = 0;
            foreach (var n in a.nodes.Where(x => x.spawnerKind != null).ToList())
            {
                n.row = 26 + moved; n.col = 26; moved++;
            }
            Assert.IsFalse(InPond(sim, 26, 26));
            var back = Simulation.LoadJson(sim.SaveJson(), sim.Config, clock.NowMs, out var why);
            Assert.IsNotNull(back, why);
            Assert.IsFalse(back.Area(F).nodes.Any(n => n.spawnerKind != null && !InPond(sim, n.row, n.col)), "off-pond nodes dropped");
            sim.ReplaceState(back);
            var a2 = sim.State.Area(F);
            foreach (var sp in sim.Config.Region(F).spawners)
                Assert.AreEqual(sim.Nodes.SpawnerTarget(sp), SimTestUtil.CountSpawner(a2, sp.kind), sp.kind + " refilled on boot");
            Assert.IsTrue(a2.nodes.Where(n => n.spawnerKind != null).All(n => InPond(sim, n.row, n.col)));
            // other Islands' spawner nodes survive the load untouched
            Assert.AreEqual(10, SimTestUtil.CountSpawner(sim.State.Area("center"), "bush"));
        }
    }
}
