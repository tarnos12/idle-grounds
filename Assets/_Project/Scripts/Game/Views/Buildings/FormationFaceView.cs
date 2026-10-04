using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// 1x1 formations (spec §2.4): icon 20 px centred; badge 9 px under the tile — Gathering Stone /
    /// Furnace Spirit "qty/cap" (red at cap, else gold), Warding Seal "qty" with its tuned item icon
    /// 13 px above the tile, Wisp Lantern "N" + link icon. Stone / spirit show a reach circle (§2.1
    /// step 3) while hovered or selected.
    /// </summary>
    public class FormationFaceView : BuildingFace
    {
        public enum Kind { GatheringStone, FurnaceSpirit, WardingSeal, WispLantern }
        public const float StokeSlackCells = 1.5f;

        public Kind kind;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] SpriteRenderer itemIcon;
        [SerializeField] TextMeshPro badge;
        [SerializeField] SpriteRenderer badgeIcon;
        [SerializeField] Transform reach;
        [SerializeField] SpriteRenderer reachFill;
        [SerializeField] SpriteRenderer reachRing;
        [Tooltip("Lantern beat indicator: thin bar along the tile bottom filling toward the next beat.")]
        [SerializeField] SpriteRenderer beatTrack;
        [SerializeField] SpriteRenderer beatFill;

        public const float BeatBarPx = 18f;

        public bool ReachVisible => reach != null && reach.gameObject.activeSelf;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 20f);
            icon.transform.localPosition = v.L(0.5f, 0.5f);
            shownAcceptCount = -1; nextAcceptsAt = 0f; lastA = lastB = int.MinValue;
            foreach (var a in accepts) if (a != null) ViewKit.Show(a, false);
            badge.transform.localPosition = new Vector3(0.5f, -1f - ViewKit.U(8f), 0f);
            ViewKit.Font(badge, 9f);
            if (itemIcon != null) itemIcon.transform.localPosition = new Vector3(0.5f, ViewKit.U(8f), 0f);
            if (badgeIcon != null) ViewKit.Fit(badgeIcon, badgeIcon.sprite, 10f);
            if (beatTrack != null)
            {
                ViewKit.Bar(beatTrack, 0.5f - ViewKit.U(BeatBarPx) * 0.5f, -0.9f, ViewKit.U(BeatBarPx), ViewKit.U(2f));
                beatTrack.color = ViewKit.Rgba(255, 255, 255, 0.18f);
                beatFill.color = UiPalette.Gold;
            }

            if (reach != null)
            {
                float r = 0; Color rgb = UiPalette.Accent;
                if (kind == Kind.GatheringStone) { r = v.Def.gather.radius; rgb = ViewKit.Rgba(74, 222, 128, 1f); }
                else if (kind == Kind.FurnaceSpirit) { r = v.Def.stoker.radius + StokeSlackCells; rgb = ViewKit.Rgba(251, 146, 60, 1f); }
                reach.localPosition = v.L(0.5f, 0.5f);
                reach.localScale = new Vector3(2f * r, 2f * r, 1f);
                reachFill.color = new Color(rgb.r, rgb.g, rgb.b, 0.07f);
                reachRing.color = new Color(rgb.r, rgb.g, rgb.b, 0.5f);
                ViewKit.Show(reach, false);
            }
        }

        public override void SetHighlight(BuildingView v, bool on)
        {
            if (reach != null && (kind == Kind.GatheringStone || kind == Kind.FurnaceSpirit)) ViewKit.Show(reach, on);
        }

        int lastA = int.MinValue, lastB = int.MinValue;

        public override void Refresh(BuildingView v)
        {
            var b = v.Building; var def = v.Def;
            int a, cap2 = 0; Color c = UiPalette.Gold;
            switch (kind)
            {
                case Kind.GatheringStone:
                case Kind.FurnaceSpirit:
                    cap2 = kind == Kind.GatheringStone ? def.gather.cap : def.stoker.cap;
                    a = BuildingSystem.GatherTotal(b);
                    if (a >= cap2) c = UiPalette.Danger;
                    break;
                case Kind.WardingSeal:
                    a = b.qty;
                    if (b.qty >= v.Sync.Sim.Buildings.SealCap(def)) c = UiPalette.Danger;
                    break;
                default:
                    a = b.links != null ? b.links.Count : 0;
                    break;
            }
            if (a != lastA || cap2 != lastB)       // the badge string is only rebuilt when its numbers change
            {
                lastA = a; lastB = cap2;
                string txt = kind == Kind.GatheringStone || kind == Kind.FurnaceSpirit ? a + "/" + cap2 : a.ToString();
                ViewKit.Text(badge, txt);
                if (badgeIcon != null)
                {
                    float tw = badge.GetPreferredValues(txt).x;
                    badgeIcon.transform.localPosition = badge.transform.localPosition + new Vector3(tw * 0.5f + ViewKit.U(6f), 0f, 0f);
                }
            }
            ViewKit.Colour(badge, c);
            if (beatTrack != null)
            {
                // beat indicator: fills toward the next beat while the lantern has links
                bool active = kind == Kind.WispLantern && b.links != null && b.links.Count > 0;
                ViewKit.Show(beatTrack, active);
                ViewKit.Show(beatFill, active);
                if (active)
                {
                    var sim = v.Sync.Sim;
                    double now = v.Sync.Runner.SimNow;
                    double beat = sim.Logistics.BeatMs(sim.State.Area(v.Area), def, now);
                    float frac = beat > 0 ? Mathf.Clamp01(1f - (float)((b.nextSend - now) / beat)) : 1f;
                    float w = ViewKit.U(BeatBarPx);
                    ViewKit.Bar(beatFill, 0.5f - w * 0.5f, -0.9f, w * frac, ViewKit.U(2f));
                }
            }
            if (badgeIcon != null) ViewKit.Show(badgeIcon, kind == Kind.WispLantern);
            if (itemIcon != null)
            {
                bool show = kind == Kind.WardingSeal && b.item != null;
                ViewKit.Show(itemIcon, show);
                if (show) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(b.item), 13f);
            }
            if (kind == Kind.GatheringStone) RefreshAccepts(v);
        }

        // ---- linked Gathering Stone: what its link targets use (ui.js:1100-1107) ----
        // Up to 4 icons round(9*1.2)=11 px, 1 px apart, centred under the badge. StoneAccepts allocates,
        // so it is sampled at 4 Hz; the renderers are pooled children created on first use.
        public const int MaxAccepts = 4;
        const float AcceptPx = 11f;
        readonly SpriteRenderer[] accepts = new SpriteRenderer[MaxAccepts];
        readonly string[] shownAccepts = new string[MaxAccepts];
        int shownAcceptCount = -1;
        float nextAcceptsAt;

        public int AcceptIconCount => shownAcceptCount < 0 ? 0 : shownAcceptCount;

        void RefreshAccepts(BuildingView v)
        {
            float t = Time.unscaledTime;
            if (t < nextAcceptsAt && shownAcceptCount >= 0) return;
            nextAcceptsAt = t + 0.25f;
            var acc = v.Sync.Sim.Logistics.StoneAccepts(v.Area, v.Building);
            int n = acc != null ? Mathf.Min(acc.Count, MaxAccepts) : 0;
            bool same = n == shownAcceptCount;
            for (int i = 0; same && i < n; i++) if (acc[i] != shownAccepts[i]) same = false;
            if (same) return;
            shownAcceptCount = n;
            float ip = ViewKit.U(AcceptPx), step = ViewKit.U(AcceptPx + 1f);
            // badge centre is 8 px under the tile; icons sit bpx*0.5 + ip*0.5 + 1 px lower (bpx = 10 floor)
            float y = badge.transform.localPosition.y - ViewKit.U(5f + AcceptPx * 0.5f + 1f);
            float x0 = 0.5f - (n - 1) * step * 0.5f;
            for (int i = 0; i < MaxAccepts; i++)
            {
                bool on = i < n;
                shownAccepts[i] = on ? acc[i] : null;
                if (!on) { if (accepts[i] != null) ViewKit.Show(accepts[i], false); continue; }
                if (accepts[i] == null) accepts[i] = NewAcceptIcon(i);
                ViewKit.Show(accepts[i], true);
                ViewKit.Fit(accepts[i], v.Sync.Sprites.Item(acc[i]), AcceptPx);
                accepts[i].transform.localPosition = new Vector3(x0 + i * step, y, 0f);
            }
        }

        SpriteRenderer NewAcceptIcon(int i)
        {
            var go = new GameObject("Accept" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            var mr = badge.GetComponent<MeshRenderer>();
            if (mr != null) { sr.sortingLayerID = mr.sortingLayerID; sr.sortingOrder = mr.sortingOrder; }
            return sr;
        }
    }
}
