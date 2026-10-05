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
        [Tooltip("Murmur (dragon.msg while msgUntil > now and no dialog): #e9d5ff 800 14 px, 42 chars x 3 lines, 12 px above the top.")]
        [SerializeField] TextMeshPro murmur;

        public string MurmurText => murmur != null && murmur.gameObject.activeSelf ? murmur.text : null;

        readonly List<IconRow.Entry> entries = new List<IconRow.Entry>();
        int lastStage = -1; bool lastAwake;
        string lastMsg; float murmurHalfW;

        /// <summary>drawDragonSpeech (ui.js:826): the murmur's centre x is clamped so the text stays 6 px inside the view.</summary>
        void ClampMurmur(BuildingView v)
        {
            float cx = v.transform.position.x + v.W * 0.5f;
            if (ViewCull.TryGetView(out var view))
            {
                float pad = murmurHalfW + ViewKit.U(6f);
                cx = Mathf.Clamp(cx, view.xMin + pad, Mathf.Max(view.xMin + pad, view.xMax - pad));
            }
            var p = murmur.transform.position;
            if (!Mathf.Approximately(p.x, cx)) murmur.transform.position = new Vector3(cx, p.y, p.z);
        }

        SpriteRenderer pill;
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
            float murmurY = ViewKit.U(12f);
            if (v.RealArt)
            {
                // real art is the body: no title/sub/icon; the tribute line is a compact pill under the footprint, the murmur floats above the art
                ViewKit.Show(icon, false); ViewKit.Show(title, false); ViewKit.Show(sub, false);
                var pos = new Vector3(v.W * 0.5f, -v.H - ViewKit.U(10f), 0f);
                feedRow.transform.localPosition = pos;
                if (pill == null)
                {
                    var go = new GameObject("TributePill");
                    go.transform.SetParent(transform, false);
                    pill = go.AddComponent<SpriteRenderer>();
                    pill.sprite = v.Panel.sprite;
                    pill.sortingLayerID = v.Panel.sortingLayerID;
                    pill.sortingOrder = v.Panel.sortingOrder + 1;
                    pill.color = new Color(0.08f, 0.07f, 0.12f, 0.8f);
                }
                pill.transform.localPosition = pos;
                pill.transform.localScale = new Vector3(Mathf.Max(1f, v.W * 0.85f), ViewKit.U(16f), 1f);
                murmurY = v.ArtTop + ViewKit.U(12f);
            }
            if (murmur != null)
            {
                ViewKit.Font(murmur, 14f);
                murmur.color = UiPalette.Hex("#e9d5ff");
                murmur.alignment = TextAlignmentOptions.Bottom;
                murmur.rectTransform.pivot = new Vector2(0.5f, 0f);
                murmur.rectTransform.sizeDelta = new Vector2(Mathf.Max(v.W + 2f, 9f), ViewKit.U(60f));
                murmur.transform.localPosition = new Vector3(v.W * 0.5f, murmurY, 0f);
                murmur.gameObject.SetActive(false);
            }
            lastStage = -1; lastMsg = null;
        }

        /// <summary>Word-wrap at <paramref name="cols"/> chars, at most <paramref name="lines"/> lines (ui.js murmur).</summary>
        public static string Wrap(string text, int cols, int lines)
        {
            var outLines = new List<string>();
            var cur = new System.Text.StringBuilder();
            foreach (var w in text.Split(' '))
            {
                if (cur.Length > 0 && cur.Length + 1 + w.Length > cols) { outLines.Add(cur.ToString()); cur.Clear(); }
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(w);
            }
            if (cur.Length > 0) outLines.Add(cur.ToString());
            if (outLines.Count > lines) outLines.RemoveRange(0, outLines.Count - lines);
            return string.Join("\n", outLines);
        }

        public override void Refresh(BuildingView v)
        {
            var sim = v.Sync.Sim; var s = sim.State;
            if (murmur != null)
            {
                string m = s.dragon.dialog == null ? sim.DragonMessage() : null;
                ViewKit.Show(murmur, m != null);
                if (m != null)
                {
                    if (!ReferenceEquals(m, lastMsg) && m != lastMsg)      // wrap only when the message changes
                    {
                        lastMsg = m;
                        ViewKit.Text(murmur, Wrap(UiText.StripEmoji(m), 42, 3));
                        murmurHalfW = murmur.GetPreferredValues(murmur.text).x * 0.5f;
                    }
                    ClampMurmur(v);
                }
            }
            int stage = s.dragon.stage;
            bool awake = s.won || stage >= sim.Config.dragonStages.Count;
            IsAwake = awake;
            if (stage != lastStage || awake != lastAwake)
            {
                lastStage = stage; lastAwake = awake;
                if (v.RealArt) v.SetBodyFrames(awake ? v.Sync.Sprites.DragonAwakeFrames() : null);
                else ViewKit.Fit(icon, awake ? awakeSprite : sleepingSprite, 64f);
                title.text = awake ? "Awakened Dragon" : "Sleeping Dragon";
                title.color = awake ? UiPalette.Gold : UiPalette.Hex("#d8b4fe");
                v.Border.Set(v.W, v.H, v.BorderUnits, awake ? UiPalette.Gold : UiPalette.Purple, false);
                sub.text = "watches over the grounds";
                sub.color = UiPalette.Muted;
                ViewKit.Show(sub, awake && !v.RealArt);
                ViewKit.Show(feedRow, !awake);
                ViewKit.Show(pill, !awake);
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
