using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Attaches the delivered-art UI roles (pills, progress bars, checkbox, ingredient frames + count badges, scrollbars) by name on a UI
    /// hierarchy. Idempotent. Called from <see cref="CoreLoopBuilder.Save"/> (so builder rebuilds keep the roles) and from
    /// "Idle Grounds/UI/Apply UI Skin Roles" (existing prefabs + scene). Cards / recipe cells / HP pips / cursors are runtime-driven
    /// (BuildCard, RecipeCell, PerkShopView, EnemyView, GameCursor) and need no attachment here.
    /// </summary>
    public static class UiArtRoles
    {
        public const string Track = "ui_progress_track", FillGreen = "ui_progress_fill_green", FillGold = "ui_progress_fill_gold";

        /// <summary>Returns the number of roles attached/updated. <paramref name="db"/> may be null (attach only, no art applied).</summary>
        public static int AttachAll(Transform root, GameDatabase db, System.Func<Transform, bool> skip = null)
        {
            int n = 0;
            // snapshot: we add/reparent objects while iterating
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == null || (skip != null && skip(t))) continue;
                string nm = t.name;
                var img = t.GetComponent<Image>();
                var p = t.parent;
                string pn = p != null ? p.name : "";

                // ---- pills (rim colour per role) ----
                if (img != null)
                {
                    string pill = null;
                    if (nm == "AreaPill" || nm == "HandPill") pill = "ui_pill_green";
                    else if (nm == "Pill" && pn == "BuffPill") pill = "ui_pill_purple";
                    else if (nm == "ShrinePill" || nm == "ChipTemplate" || (nm == "Pick" && pn == "NameRow")) pill = "ui_pill_gold";
                    if (pill != null) { Apply(UiSkin.Attach(t.gameObject, pill, fit: true, uiPerArt: 1f), db); n++; }
                }

                // ---- progress bars: <X>Bar/Track + Fill ----
                if (img != null && nm == "Track" && pn.EndsWith("Bar"))
                {
                    Apply(UiSkin.Attach(t.gameObject, Track, fit: true), db); n++;
                    var ble = p.GetComponent<LayoutElement>();       // 8 art px tall bar = 16 UI px (crisp 2x); the placeholder was thinner
                    if (ble != null && db != null && db.UiSprite(Track) != null) { ble.minHeight = ble.preferredHeight = 16f; }
                    var fill = p.Find("Fill");
                    if (fill != null && fill.GetComponent<Image>() != null)
                    {
                        bool milestone = IsUnder(p, "Milestone");
                        var fs = UiSkin.Attach(fill.gameObject, milestone ? FillGold : FillGreen, fit: true, fitParent: true);
                        Apply(fs, db); n++;
                    }
                }

                // ---- checkbox ----
                if (nm == "Toggle" && t.GetComponent<Toggle>() != null && t.GetComponent<UiCheckbox>() == null) { t.gameObject.AddComponent<UiCheckbox>(); n++; }

                // ---- recipe ingredient rows: icon frame + count badge ----
                if (nm == "IconBox" && img != null && pn != "IconSlot") { WrapIconBox(t, db); n++; }

                // ---- count badge text: centred on the 1x badge, small enough for its 8 px interior ----
                if (nm == "Qty" && pn == "IconBox" && t.parent.Find("QtyBadge") != null)
                {
                    var q = (RectTransform)t;
                    q.sizeDelta = new Vector2(30f, 20f);
                    var tx = t.GetComponent<TMPro.TMP_Text>();
                    if (tx != null) { tx.alignment = TMPro.TextAlignmentOptions.Center; tx.fontSize = 12f; tx.textWrappingMode = TMPro.TextWrappingModes.NoWrap; }
                }

                // ---- scrollbars ----
                var sr = t.GetComponent<ScrollRect>();
                if (sr != null && EnsureScrollbar(sr, db)) n++;
            }
            return n;
        }

        static void Apply(UiSkin s, GameDatabase db) { if (db != null) s.Apply(db); }

        static bool IsUnder(Transform t, string name)
        {
            for (; t != null; t = t.parent) if (t.name == name) return true;
            return false;
        }

        /// <summary>IconBox (the icon Image + a Qty label) goes into a fixed 36 px "IconSlot" carrying ui_iconframe; Qty gets a ui_badge_count behind it.</summary>
        static void WrapIconBox(Transform box, GameDatabase db)
        {
            var parent = box.parent;
            var slotGo = new GameObject("IconSlot", typeof(RectTransform));
            var slot = (RectTransform)slotGo.transform;
            slot.SetParent(parent, false);
            slot.SetSiblingIndex(box.GetSiblingIndex());
            var le = slotGo.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 36f; le.minHeight = le.preferredHeight = 36f;
            var frame = slotGo.AddComponent<Image>();
            frame.raycastTarget = false;
            frame.color = new Color(1f, 1f, 1f, 0f);          // invisible until the art is applied (placeholder look = the bare icon)
            Apply(UiSkin.Attach(slotGo, "ui_iconframe"), db);
            var brt = (RectTransform)box;
            brt.SetParent(slot, false);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(2f, 2f); brt.offsetMax = new Vector2(-2f, -2f);     // 32 px icon inside the 2 px frame border
            var own = box.GetComponent<LayoutElement>(); if (own != null) own.ignoreLayout = true;

            var qty = box.Find("Qty");
            if (qty != null)
            {
                var bgo = new GameObject("QtyBadge", typeof(RectTransform));
                var b = (RectTransform)bgo.transform;
                b.SetParent(box, false);
                b.SetSiblingIndex(qty.GetSiblingIndex());
                var q = (RectTransform)qty;
                b.anchorMin = q.anchorMin; b.anchorMax = q.anchorMax; b.pivot = q.pivot;
                b.sizeDelta = new Vector2(30f, 20f);
                b.anchoredPosition = q.anchoredPosition;
                var bi = bgo.AddComponent<Image>(); bi.raycastTarget = false;
                bi.color = new Color(1f, 1f, 1f, 0f);
                Apply(UiSkin.Attach(bgo, "ui_badge_count", fit: true, uiPerArt: 1f), db);
            }
        }

        /// <summary>Adds a Scrollbar (delivered ui_scrollbar art: tinted track + handle) to a ScrollRect that has none for its scroll axis.</summary>
        static bool EnsureScrollbar(ScrollRect sr, GameDatabase db)
        {
            bool vert = sr.vertical;
            if (vert ? sr.verticalScrollbar != null : sr.horizontalScrollbar != null) return false;
            const float W = 16f;
            var go = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var rt = (RectTransform)go.transform;
            rt.SetParent(sr.transform, false);
            if (vert)
            {
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(W, 0f); rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, W); rt.anchoredPosition = Vector2.zero;
            }
            var track = go.GetComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.35f);          // placeholder look when the art is missing
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var hrt = (RectTransform)handle.transform;
            hrt.SetParent(rt, false);
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            var himg = handle.GetComponent<Image>();
            himg.color = new Color(1f, 1f, 1f, 0.35f);
            var sb = go.GetComponent<Scrollbar>();
            sb.handleRect = hrt; sb.targetGraphic = himg;
            sb.direction = vert ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;
            Apply(UiSkin.Attach(go, "ui_scrollbar", fit: true, tint: new Color(1f, 1f, 1f, 0.6f)), db);
            Apply(UiSkin.Attach(handle, "ui_scrollbar", fit: true), db);
            if (vert) { sr.verticalScrollbar = sb; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; sr.verticalScrollbarSpacing = 0f; }
            else { sr.horizontalScrollbar = sb; sr.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; sr.horizontalScrollbarSpacing = 0f; }
            return true;
        }
    }
}
