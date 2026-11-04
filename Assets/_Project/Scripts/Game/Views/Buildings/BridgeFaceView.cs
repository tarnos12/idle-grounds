using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Spirit Bridge face (ADR 0003, ART-SPEC §3.4): the bridge art (2×1 footprint, scaled to the footprint width and drawn
    /// from its bottom edge so it rises above it) in one of three states — unpaired (dormant), sending or receiving — plus a
    /// status line ("Unpaired" / "→ Mine" / "← Farm") and the buffer fill "n/cap". Left-click opens the
    /// <see cref="BridgePanelView"/> (pairing list); lanterns link to / from it like any endpoint.
    /// <para>Art: the delivered still is the building variant <c>unpaired</c> (<c>bld_spirit_bridge_unpaired@64</c>, wired by
    /// ArtIntake). When code-anim.json has the state entries <c>bld_spirit_bridge_sending</c> / <c>_receiving</c> for that
    /// still's size, the paired states are drawn in code on the still (ART-SPEC §3.9.8: pool tint + qi motes rising /
    /// falling; calm motion keeps the steady tint, no motes). Otherwise the prefab's 32-px still and its hand-drawn
    /// sending / receiving strips are used (one density per key, ART-SPEC §3.9.4).</para>
    /// </summary>
    public class BridgeFaceView : BuildingFace
    {
        public enum State { Unpaired, Sending, Receiving }

        public const string UnpairedVariant = "unpaired";
        public const string SendingSuffix = "_sending", ReceivingSuffix = "_receiving";

        [SerializeField] SpriteRenderer art;
        [SerializeField] Sprite unpairedSprite;
        [SerializeField] Sprite[] sendingFrames;
        [SerializeField] Sprite[] receivingFrames;
        [SerializeField] TextMeshPro status;
        [SerializeField] TextMeshPro buffer;
        [SerializeField] float fps = 6f;

        Sprite still;
        CodeAnimEntry sendEntry, recvEntry;
        CodeAnimator codeAnim;

        public State Current { get; private set; }
        public string StatusText => status != null ? status.text : null;
        /// <summary>The still the face draws (the delivered @64 still, or the prefab's 32-px one).</summary>
        public Sprite Still => still;
        /// <summary>True when the paired states are drawn in code on the still (code-anim.json state entries match it).</summary>
        public bool CodeStates => sendEntry != null && recvEntry != null;
        /// <summary>The code animation of the current paired state (null while unpaired or on the strip fallback).</summary>
        public CodeAnimator CodeAnim => codeAnim != null && codeAnim.Configured ? codeAnim : null;
        public SpriteRenderer Art => art;

        public override void Layout(BuildingView v)
        {
            if (codeAnim != null) codeAnim.Clear();
            sendEntry = recvEntry = null;
            still = unpairedSprite;
            var delivered = v.Sync != null && v.Building != null ? v.Sync.Sprites.BuildingVariant(v.Building.type, UnpairedVariant) : null;
            if (delivered != null && delivered.Length > 0 && delivered[0] != null)
            {
                string key = Data.ArtKeys.Building(v.Building.type);
                var se = Fits(CodeAnimLibrary.Find(key + SendingSuffix), delivered[0]);
                var re = Fits(CodeAnimLibrary.Find(key + ReceivingSuffix), delivered[0]);
                if (se != null && re != null) { sendEntry = se; recvEntry = re; still = delivered[0]; }
            }
            if (art != null)
            {
                var s = still != null ? still : art.sprite;
                art.sprite = s;
                float k = 1f;
                if (s != null) k = v.W / Mathf.Max(0.0001f, s.bounds.size.x);     // canvas width = footprint width
                art.transform.localScale = new Vector3(k, k, 1f);
                // bottom-aligned on the footprint's bottom edge and centred: works for the centre-pivot placeholder and
                // for the bottom-centre-pivot delivered still
                art.transform.localPosition = s != null
                    ? new Vector3(v.W * 0.5f - s.bounds.center.x * k, -v.H - s.bounds.min.y * k, 0f)
                    : new Vector3(v.W * 0.5f, -v.H * 0.5f, 0f);
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
                // VP-12: below the footprint (under the status line), not printed over the bridge art
                buffer.transform.localPosition = new Vector3(v.W * 0.5f, -v.H - ViewKit.U(18f), 0f);
            }
            Current = (State)(-1);
        }

        static CodeAnimEntry Fits(CodeAnimEntry e, Sprite s) =>
            e != null && e.HasEffects && s != null && e.StillW == Mathf.RoundToInt(s.rect.width) && e.StillH == Mathf.RoundToInt(s.rect.height) ? e : null;

        /// <summary>State changed (rare): bind the state's code animation on the still, or the plain still while unpaired.
        /// The bake is shared per entry + still, so this only picks slots.</summary>
        void BindState(State st)
        {
            if (art == null) return;
            var e = st == State.Sending ? sendEntry : st == State.Receiving ? recvEntry : null;
            if (e != null)
            {
                if (codeAnim == null) codeAnim = CodeAnimator.For(art);
                codeAnim.SelfTick = false;                      // ticked from Refresh: culled bridges cost nothing
                if (codeAnim.Configure(e, art, still, null)) { codeAnim.Playing = true; return; }
            }
            if (codeAnim != null) codeAnim.Clear();
            if (still != null && art.sprite != still) art.sprite = still;
        }

        public override void Refresh(BuildingView v)
        {
            // VP-12: the bridge art has its own transparent corners; the base view's opaque slate footprint plate behind it
            // read as a placeholder box. The plate and its border stay hidden while the bridge face is drawn.
            ViewKit.Show(v.Panel, false);
            ViewKit.Show(v.Border, false);
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
                if (CodeStates) BindState(st);
            }
            if (art != null)
            {
                if (CodeStates)
                {
                    if (codeAnim != null && codeAnim.Configured) codeAnim.Tick(CodeAnimClock.Now);
                }
                else
                {
                    var frames = st == State.Sending ? sendingFrames : st == State.Receiving ? receivingFrames : null;
                    Sprite s = frames != null && frames.Length > 0 ? frames[(int)(Time.unscaledTime * fps) % frames.Length] : unpairedSprite;
                    if (s != null && art.sprite != s) art.sprite = s;
                }
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
