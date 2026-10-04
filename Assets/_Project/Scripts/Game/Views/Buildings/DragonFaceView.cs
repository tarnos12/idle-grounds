using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The Sleeping Dragon (spec §2.4): sleeping / awake sprite 64 px at 36% (by State.dragon.stage /
    /// State.won); name "Sleeping Dragon" #d8b4fe / "Awakened Dragon" gold at 64%; while asleep a gold
    /// "Feed: qty icon …" line of the current stage's remaining tribute at 84%; awake: muted
    /// "watches over the grounds". Border purple, gold once awake. (Murmur bubble: M5.)
    /// </summary>
    public class DragonFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;
        [SerializeField] IconRow feedRow;
        [SerializeField] Sprite sleepingSprite;
        [SerializeField] Sprite awakeSprite;

        readonly List<IconRow.Entry> entries = new List<IconRow.Entry>();
        int lastStage = -1; bool lastAwake;

        public bool IsAwake { get; private set; }

        public override void Layout(BuildingView v)
        {
            icon.transform.localPosition = v.L(0.5f, 0.36f);
            title.transform.localPosition = v.L(0.5f, 0.64f);
            title.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(22f));
            ViewKit.Font(title, 18f);
            sub.transform.localPosition = v.L(0.5f, 0.84f);
            sub.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(16f));
            ViewKit.Font(sub, 12f);
            feedRow.transform.localPosition = v.L(0.5f, 0.84f);
            lastStage = -1;
        }

        public override void Refresh(BuildingView v)
        {
            var sim = v.Sync.Sim; var s = sim.State;
            int stage = s.dragon.stage;
            bool awake = s.won || stage >= sim.Config.dragonStages.Count;
            IsAwake = awake;
            if (stage != lastStage || awake != lastAwake)
            {
                lastStage = stage; lastAwake = awake;
                ViewKit.Fit(icon, awake ? awakeSprite : sleepingSprite, 64f);
                title.text = awake ? "Awakened Dragon" : "Sleeping Dragon";
                title.color = awake ? UiPalette.Gold : UiPalette.Hex("#d8b4fe");
                v.Border.Set(v.W, v.H, v.BorderUnits, awake ? UiPalette.Gold : UiPalette.Purple, false);
                sub.text = "watches over the grounds";
                sub.color = UiPalette.Muted;
                ViewKit.Show(sub, awake);
                ViewKit.Show(feedRow, !awake);
            }
            if (awake) return;
            entries.Clear();
            foreach (var n in sim.Config.dragonStages[stage].needs)
            {
                int left = sim.Timing.Scaled(n.qty) - s.dragon.paid.Get(n.item);
                if (left > 0) entries.Add(new IconRow.Entry(n.item, left));
            }
            feedRow.Set(entries, 15f, UiPalette.Gold, v.Sync.Sprites, "Feed:", v.W - ViewKit.U(8f));
        }
    }
}
