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
        /// <summary>Last frame's <see cref="IdleGrounds.Sim.BuildingSystem.GenProducing(IdleGrounds.Sim.AreaState, IdleGrounds.Sim.Building, out int)"/>: the generator is growing (pile and output pile not full).</summary>
        public bool Producing { get; private set; }

        /// <summary>Working state (plays the code animation / working variant) while the generator produces. Refresh runs before the art animates each frame.</summary>
        public override bool IsActive(BuildingView v) => Producing && v.Building.built;
        PlatedText tag;
        bool real;

        public override void Layout(BuildingView v)
        {
            lastNear = -1; lastCap = -1; Producing = false;
            count.transform.localPosition = v.L(0.5f, 0.86f) + new Vector3(ViewKit.U(7f), 0f, 0f);
            ViewKit.Font(count, 10f);
            itemIcon.transform.localPosition = v.L(0.5f, 0.86f) - new Vector3(ViewKit.U(14f), 0f, 0f);
            if (v.Def.gen.enabled) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(v.Def.gen.item), 13f);
            real = v.RealArt;
            if (real)
            {
                if (tag == null) tag = PlatedText.Create(transform, "Tag", WorldLabel.TextPx, PlatedText.Pivot.Below);
                tag.Show(true);
                tag.Label.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(2f), 0f));
            }
            else if (tag != null) tag.Show(false);
            ViewKit.Show(count, !real); ViewKit.Show(itemIcon, !real);
        }

        int lastNear = -1, lastCap = -1;

        public override void Refresh(BuildingView v)
        {
            var g = v.Def.gen;
            if (!g.enabled) return;
            // one ground pass per frame: own pile count + the sim's live "is producing" query (§8.6 / §9.5 stalls)
            var sim = v.Sync.Sim;
            Producing = sim.Buildings.GenProducing(sim.State.Area(v.Area), v.Building, out int near);
            Near = near;
            if (near != lastNear || g.cap != lastCap) { lastNear = near; lastCap = g.cap; ViewKit.Text(count, near + "/" + g.cap); }
            bool full = !Producing;
            ViewKit.Colour(count, full ? UiPalette.Danger : UiPalette.Gold);
            if (real) tag.Set(count.text, full ? WorldLabel.BadColour : WorldLabel.GoldColour, full ? PixelSprites.Box : null, v.Sync.Sprites.Item(g.item));
        }
    }
}
