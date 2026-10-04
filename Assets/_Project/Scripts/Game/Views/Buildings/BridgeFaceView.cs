using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Spirit Bridge face (ADR 0003, ART-SPEC §3.4): the 64×64 bridge art (2×1 footprint, drawn from the
    /// footprint's bottom edge so it rises one cell above it) in one of three states — unpaired (dormant),
    /// sending (cyan, frames cycle) or receiving (gold-green, frames cycle) — plus a status line
    /// ("Unpaired" / "→ Mine" / "← Farm") and the buffer fill "n/cap". Left-click opens the
    /// <see cref="BridgePanelView"/> (pairing list); lanterns link to / from it like any endpoint.
    /// </summary>
    public class BridgeFaceView : BuildingFace
    {
        public enum State { Unpaired, Sending, Receiving }

        [SerializeField] SpriteRenderer art;
        [SerializeField] Sprite unpairedSprite;
        [SerializeField] Sprite[] sendingFrames;
        [SerializeField] Sprite[] receivingFrames;
        [SerializeField] TextMeshPro status;
        [SerializeField] TextMeshPro buffer;
        [SerializeField] float fps = 6f;

        public State Current { get; private set; }
        public string StatusText => status != null ? status.text : null;

        public override void Layout(BuildingView v)
        {
            if (art != null)
            {
                var s = unpairedSprite != null ? unpairedSprite : art.sprite;
                art.sprite = s;
                float k = 1f;
                if (s != null) k = v.W / Mathf.Max(0.0001f, s.bounds.size.x);     // canvas width = footprint width
                art.transform.localScale = new Vector3(k, k, 1f);
                float hUnits = s != null ? s.bounds.size.y * k : v.H;
                art.transform.localPosition = new Vector3(v.W * 0.5f, -v.H + hUnits * 0.5f, 0f);   // bottom-aligned
            }
            if (status != null)
            {
                ViewKit.Font(status, 9f);
                status.rectTransform.sizeDelta = new Vector2(v.W + 1f, ViewKit.U(12f));
                status.transform.localPosition = new Vector3(v.W * 0.5f, -v.H - ViewKit.U(7f), 0f);
            }
            if (buffer != null)
            {
                ViewKit.Font(buffer, 9f);
                buffer.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(12f));
                buffer.transform.localPosition = new Vector3(v.W * 0.5f, -v.H * 0.5f, 0f);
            }
            Current = (State)(-1);
        }

        public override void Refresh(BuildingView v)
        {
            var sim = v.Sync.Sim;
            var b = v.Building;
            var partner = sim.BridgePartner(v.Area, b);
            var st = partner == null ? State.Unpaired : b.pairSends ? State.Sending : State.Receiving;
            if (st != Current)
            {
                Current = st;
                string other = partner != null ? (sim.Config.Region(b.pairIsland)?.name ?? b.pairIsland) : null;
                ViewKit.Text(status, st == State.Unpaired ? "Unpaired" : st == State.Sending ? "→ " + other : "← " + other);
                ViewKit.Colour(status, st == State.Unpaired ? UiPalette.Muted : st == State.Sending ? UiPalette.Hex("#67e8f9") : UiPalette.Hex("#bef264"));
            }
            if (art != null)
            {
                var frames = st == State.Sending ? sendingFrames : st == State.Receiving ? receivingFrames : null;
                Sprite s = frames != null && frames.Length > 0 ? frames[(int)(Time.unscaledTime * fps) % frames.Length] : unpairedSprite;
                if (s != null && art.sprite != s) art.sprite = s;
            }
            if (buffer != null)
            {
                int n = BuildingSystem.GatherTotal(b);
                int cap = sim.Config.Building(b.type)?.bridge.cap ?? 20;
                ViewKit.Text(buffer, st == State.Unpaired && n == 0 ? "" : n + "/" + cap);
                ViewKit.Colour(buffer, n >= cap ? UiPalette.Danger : UiPalette.Text);
            }
        }
    }
}
