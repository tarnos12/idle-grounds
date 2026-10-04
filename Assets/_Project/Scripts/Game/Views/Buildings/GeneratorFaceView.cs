using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Generator building (Algae Farm / Herb Garden): default icon + name, plus its output count —
    /// "icon n/cap" of the produced item lying within 4 cells (the pile the sim caps, §8.6), red when full.
    /// </summary>
    public class GeneratorFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer itemIcon;
        [SerializeField] TextMeshPro count;

        public override bool ReplacesDefault => false;
        public int Near { get; private set; }

        public override void Layout(BuildingView v)
        {
            count.transform.localPosition = v.L(0.5f, 0.86f) + new Vector3(ViewKit.U(7f), 0f, 0f);
            ViewKit.Font(count, 10f);
            itemIcon.transform.localPosition = v.L(0.5f, 0.86f) - new Vector3(ViewKit.U(14f), 0f, 0f);
            if (v.Def.gen.enabled) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(v.Def.gen.item), 13f);
        }

        public override void Refresh(BuildingView v)
        {
            var g = v.Def.gen;
            if (!g.enabled) return;
            var area = v.Sync.Sim.State.Area(v.Area);
            var (bx, by) = v.Sync.Sim.World.BuildingCenterPx(v.Building);
            double R = 4 * v.Sync.Space.Cell;
            int near = 0;
            foreach (var gi in area.ground)
                if (gi.item == g.item && (gi.x - bx) * (gi.x - bx) + (gi.y - by) * (gi.y - by) <= R * R) near++;
            Near = near;
            ViewKit.Text(count, near + "/" + g.cap);
            ViewKit.Colour(count, near >= g.cap || v.Building.pileFull ? UiPalette.Danger : UiPalette.Gold);
        }
    }
}
