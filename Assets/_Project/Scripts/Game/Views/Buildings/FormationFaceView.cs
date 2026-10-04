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

        public bool ReachVisible => reach != null && reach.gameObject.activeSelf;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 20f);
            icon.transform.localPosition = v.L(0.5f, 0.5f);
            badge.transform.localPosition = new Vector3(0.5f, -1f - ViewKit.U(8f), 0f);
            ViewKit.Font(badge, 9f);
            if (itemIcon != null) itemIcon.transform.localPosition = new Vector3(0.5f, ViewKit.U(8f), 0f);
            if (badgeIcon != null) ViewKit.Fit(badgeIcon, badgeIcon.sprite, 10f);

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

        public override void Refresh(BuildingView v)
        {
            var b = v.Building; var def = v.Def;
            string txt; Color c = UiPalette.Gold;
            switch (kind)
            {
                case Kind.GatheringStone:
                case Kind.FurnaceSpirit:
                {
                    int cap = kind == Kind.GatheringStone ? def.gather.cap : def.stoker.cap;
                    int n = BuildingSystem.GatherTotal(b);
                    txt = n + "/" + cap;
                    if (n >= cap) c = UiPalette.Danger;
                    break;
                }
                case Kind.WardingSeal:
                    txt = b.qty.ToString();
                    if (b.qty >= v.Sync.Sim.Buildings.SealCap(def)) c = UiPalette.Danger;
                    break;
                default:
                    txt = (b.links != null ? b.links.Count : 0).ToString();
                    break;
            }
            ViewKit.Text(badge, txt);
            ViewKit.Colour(badge, c);
            if (badgeIcon != null)
            {
                ViewKit.Show(badgeIcon, kind == Kind.WispLantern);
                float tw = badge.GetPreferredValues(txt).x;
                badgeIcon.transform.localPosition = badge.transform.localPosition + new Vector3(tw * 0.5f + ViewKit.U(6f), 0f, 0f);
            }
            if (itemIcon != null)
            {
                bool show = kind == Kind.WardingSeal && b.item != null;
                ViewKit.Show(itemIcon, show);
                if (show) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(b.item), 13f);
            }
        }
    }
}
