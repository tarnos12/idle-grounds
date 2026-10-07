using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGrounds.Editor.BuildingsBuilder;
using static IdleGrounds.Editor.CoreLoopBuilder;
using static IdleGrounds.Editor.ProgressionBuilder;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// M7 "Prestige, save" + M8 "Juice &amp; audio" view side, reproducible: the SfxLibrary asset; UI
    /// prefabs HelpModal, StatsPanel, PerkShop, AscendDialog, PostAscension, WelcomeModal, Toast, ConfirmDialog;
    /// scene wiring — SaveService / AudioService / MetaUiController under "--- Systems", the modals under
    /// UI/Canvas (above the M5 panels, below the hand cursor), the bottom-bar additions (Shrine pill, mute
    /// button, ascension tag in the area pill, MetaBarView) and the FxService spark sprite.
    /// </summary>
    public static class MetaBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string SfxDir = "Assets/_Project/Audio/SFX";
        const string SfxLibraryPath = "Assets/_Project/Audio/SfxLibrary.asset";
        static readonly string[] SfxNames = { "harvest", "pickup", "swing", "craft", "build", "upgrade", "unlock", "hit", "kill", "dragon", "ascend", "error", "click" };
        static readonly string[] Prefabs = { "PostAscension", "AscendDialog", "PerkShop", "HelpModal", "StatsPanel", "WelcomeModal", "Toast", "ConfirmDialog" };

        [MenuItem("Idle Grounds/Prefabs/Build Meta Prefabs (M7+M8)")]
        public static void BuildAllPrefabs()
        {
            EnsureShapes();
            Directory.CreateDirectory(UiPrefabDir);
            EnsureSfxLibrary();
            BuildHelpPrefab();
            BuildStatsPrefab();
            BuildPerkShopPrefab();
            BuildAscendPrefab();
            BuildPostAscensionPrefab();
            BuildWelcomePrefab();
            BuildToastPrefab();
            BuildConfirmPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] M7+M8 prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Meta (M7+M8)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] M7+M8 installed into " + ScenePath);
        }

        // ------------------------------------------------------------------ audio

        public static SfxLibrary EnsureSfxLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(lib, SfxLibraryPath);
            }
            lib.entries.Clear();
            foreach (var n in SfxNames)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + n + ".wav");
                if (clip == null) Debug.LogWarning("[IdleGrounds] missing SFX " + n);
                // harvest: JS randomises 620 ± 60 Hz → pitch ± 60/620
                lib.entries.Add(new SfxLibrary.Entry { name = n, clip = clip, volume = 1f, pitchJitter = n == "harvest" ? 60f / 620f : 0f });
            }
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------------ shared UI pieces

        static TextMeshProUGUI Text(Transform p, string name, string t, float size, Color c, bool bold, bool wrap,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var l = Label(p, name, t, size, c, bold);
            if (wrap) Wrap(l);
            l.alignment = align;
            return l;
        }

        /// <summary>Header row (icon? + title + spacer + ✕) anchored to the top of the box.</summary>
        static (TextMeshProUGUI title, Button close) Header(RectTransform box, string title, Sprite icon, float top = 18f)
        {
            var head = NewUi("Header", box);
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(-54f, 54f);
            head.anchoredPosition = new Vector2(0f, -top);
            Row(head.gameObject, 0, 0, 12);
            if (icon != null) Icon(head, "Icon", icon, 36);
            var t = Label(head, "Title", title, 25f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 48, 42);
            return (t, close);
        }

        /// <summary>Vertical ScrollRect filling the box between <paramref name="top"/> and <paramref name="bottom"/> px.</summary>
        static (ScrollRect scroll, RectTransform content) Scroll(RectTransform box, float top, float bottom, float spacing)
        {
            var vp = NewUi("Viewport", box);
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(27f, bottom); vp.offsetMax = new Vector2(-27f, -top);
            var vimg = vp.gameObject.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0f);
            vimg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            var content = NewUi("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var col = ColW(content.gameObject, 0, spacing);
            col.padding = new RectOffset(0, 12, 0, 12);
            Fit(content.gameObject, false, true);
            var sr = box.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;
            return (sr, content);
        }

        static RectTransform Templates(RectTransform root)
        {
            var t = NewUi("Templates", root);
            t.gameObject.SetActive(false);
            return t;
        }

        static Button BigButton(Transform parent, string name, string label, Color text, float w = 230, float h = 51) =>
            SmallButton(parent, name, label, text, w, h);

        static RectTransform ButtonRow(Transform parent)
        {
            var row = NewUi("Buttons", parent);
            Row(row.gameObject, 0, 6, 14).childAlignment = TextAnchor.MiddleCenter;
            return row;
        }

        static (GameObject modal, Image box, VerticalLayoutGroup col) FitBox(RectTransform root, Color border, float width, int pad = 27, float spacing = 14,
            TextAnchor align = TextAnchor.UpperCenter)
        {
            var (modal, box) = Modal(root, border);
            box.rectTransform.sizeDelta = new Vector2(width, 300f);
            var col = ColW(box.gameObject, pad, spacing, align);
            Fit(box.gameObject, false, true);
            return (modal, box, col);
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildHelpPrefab()
        {
            var root = Stretch(NewUi("HelpModal", null));
            var view = root.gameObject.AddComponent<HelpModalView>();
            var (modal, box) = Modal(root, UiPalette.Line);
            var brt = box.rectTransform;
            brt.sizeDelta = new Vector2(840f, 930f);
            var (title, close) = Header(brt, "How to play", null);
            var (scroll, content) = Scroll(brt, 84f, 24f, 15f);

            var tpl = Templates(root);
            var sec = NewUi("SectionTemplate", tpl);
            ColW(sec.gameObject, 0, 4);
            var row = sec.gameObject.AddComponent<UiTextRow>();
            row.a = Text(sec, "Title", "Section", 21f, UiPalette.Text, true, true);
            row.b = Text(sec, "Body", "Body", 19.5f, UiPalette.Muted, false, true);

            Set(view, "modal", modal);
            Set(view, "closeButton", close);
            Set(view, "titleText", title);
            Set(view, "content", content);
            Set(view, "sectionTemplate", row);
            Set(view, "scroll", scroll);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/HelpModal.prefab");
        }

        static void BuildStatsPrefab()
        {
            var root = Stretch(NewUi("StatsPanel", null));
            var view = root.gameObject.AddComponent<StatsPanelView>();
            var (modal, box, _) = FitBox(root, UiPalette.Line, 720f, 27, 10, TextAnchor.UpperLeft);
            var brt = box.rectTransform;
            var head = NewUi("Header", brt);
            Row(head.gameObject, 0, 0, 12);
            Icon(head, "Icon", Emoji("ui_stats"), 33);
            Label(head, "Title", "Stats", 25f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 48, 42);

            var tabs = NewUi("Tabs", brt);
            Row(tabs.gameObject, 0, 0, 10);
            var tabOverview = SmallButton(tabs, "OverviewTab", "Overview", UiPalette.Text, 150, 42);
            var tabFlow = SmallButton(tabs, "FlowTab", "Flow", UiPalette.Text, 150, 42);

            var rows = NewUi("Rows", brt);
            ColW(rows.gameObject, 0, 6);

            // ---- Flow tab (per-item produced / consumed / lost ledger)
            var flow = NewUi("FlowRoot", brt);
            ColW(flow.gameObject, 0, 8);
            var ctl = NewUi("Controls", flow);
            Row(ctl.gameObject, 0, 0, 10);
            var scopeBtn = SmallButton(ctl, "ScopeButton", "This run", UiPalette.Gold, 150, 40);
            var sortBtn = SmallButton(ctl, "SortButton", "Sort: net / min", UiPalette.Text, 210, 40);
            var hint = Label(ctl, "Hint", "net / min = last ~5 min", 16f, UiPalette.Muted, false);
            hint.alignment = TextAlignmentOptions.MidlineRight;
            Le(hint, flexW: 1);

            var hdr = NewUi("ColumnHeader", flow);
            Row(hdr.gameObject, 8, 0, 8);
            Le(NewUi("IconSpace", hdr), w: 30);
            Le(HeaderCell(hdr, "Item", TextAlignmentOptions.MidlineLeft), flexW: 1);
            Le(HeaderCell(hdr, "Made", TextAlignmentOptions.MidlineRight), w: 86);
            Le(HeaderCell(hdr, "Used", TextAlignmentOptions.MidlineRight), w: 86);
            Le(HeaderCell(hdr, "Lost", TextAlignmentOptions.MidlineRight), w: 76);
            Le(HeaderCell(hdr, "Net/min", TextAlignmentOptions.MidlineRight), w: 90);

            var scrollRt = NewUi("FlowScroll", flow);
            Le(scrollRt, h: 520);
            var vp = NewUi("Viewport", scrollRt);
            Stretch(vp);
            var vimg = vp.gameObject.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0f);
            vimg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            var content = NewUi("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var ccol = ColW(content.gameObject, 0, 4);
            ccol.padding = new RectOffset(0, 10, 0, 8);
            Fit(content.gameObject, false, true);
            var sr = scrollRt.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;
            var empty = Label(flow, "Empty", "Nothing has flowed yet - harvest, craft or build something.", 18f, UiPalette.Muted, false);
            empty.gameObject.SetActive(false);

            var tpl = Templates(root);
            var r = NewUi("RowTemplate", tpl);
            Row(r.gameObject, 0, 2, 12);
            var tr = r.gameObject.AddComponent<UiTextRow>();
            tr.a = Label(r, "Label", "Playtime", 19.5f, UiPalette.Muted, false);
            Le(tr.a, flexW: 1);
            tr.b = Label(r, "Value", "0", 19.5f, UiPalette.Text, true);
            tr.b.alignment = TextAlignmentOptions.MidlineRight;

            var fr = NewUi("FlowRowTemplate", tpl);
            var frImg = fr.gameObject.AddComponent<Image>();
            frImg.color = new Color(1f, 1f, 1f, 0.04f);
            frImg.raycastTarget = false;
            Row(fr.gameObject, 8, 3, 8);
            Le(fr, h: 38);
            var fv = fr.gameObject.AddComponent<FlowRowView>();
            fv.icon = Icon(fr, "Icon", null, 28);
            fv.nameText = Label(fr, "Name", "Item", 18.5f, UiPalette.Text, false);
            Le(fv.nameText, flexW: 1);
            fv.producedText = FlowCell(fr, "Made", UiPalette.Text, 86, false);
            fv.consumedText = FlowCell(fr, "Used", UiPalette.Text, 86, false);
            fv.lostText = FlowCell(fr, "Lost", UiPalette.Muted, 76, false);
            fv.netText = FlowCell(fr, "Net", UiPalette.Accent, 90, true);
            EditorUtility.SetDirty(fv);

            Set(view, "modal", modal);
            Set(view, "closeButton", close);
            Set(view, "rowsRoot", rows);
            Set(view, "rowTemplate", tr);
            Set(view, "overviewTab", tabOverview);
            Set(view, "flowTab", tabFlow);
            Set(view, "flowRoot", flow.gameObject);
            Set(view, "scopeButton", scopeBtn);
            Set(view, "sortButton", sortBtn);
            Set(view, "scopeLabel", scopeBtn.GetComponentInChildren<TextMeshProUGUI>());
            Set(view, "sortLabel", sortBtn.GetComponentInChildren<TextMeshProUGUI>());
            Set(view, "emptyText", empty);
            Set(view, "flowContent", content);
            Set(view, "flowRowTemplate", fv);
            flow.gameObject.SetActive(false);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/StatsPanel.prefab");
        }

        static TextMeshProUGUI HeaderCell(Transform parent, string text, TextAlignmentOptions align)
        {
            var t = Label(parent, text, text, 15.5f, UiPalette.Muted, true);
            t.alignment = align;
            return t;
        }

        static TextMeshProUGUI FlowCell(Transform parent, string name, Color c, float w, bool bold)
        {
            var t = Label(parent, name, "0", 18f, c, bold);
            t.alignment = TextAlignmentOptions.MidlineRight;
            Le(t, w: w);
            return t;
        }

        static void BuildPerkShopPrefab()
        {
            var root = Stretch(NewUi("PerkShop", null));
            var view = root.gameObject.AddComponent<PerkShopView>();
            var (modal, box) = Modal(root, UiPalette.Gold);
            var brt = box.rectTransform;
            brt.sizeDelta = new Vector2(840f, 930f);
            var (_, close) = Header(brt, "Ascension Shrine", Emoji("ui_ascend"));
            var ap = Label(brt, "APLine", "0 Ascension Points to spend", 19.5f, UiPalette.Text, false);
            ap.textWrappingMode = TextWrappingModes.Normal; ap.richText = true;
            var art = ap.rectTransform;
            art.anchorMin = new Vector2(0f, 1f); art.anchorMax = new Vector2(1f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.sizeDelta = new Vector2(-54f, 66f);
            art.anchoredPosition = new Vector2(0f, -78f);
            ap.alignment = TextAlignmentOptions.TopLeft;
            var (scroll, content) = Scroll(brt, 152f, 24f, 10f);

            var tpl = Templates(root);
            var grp = Label(tpl, "GroupTemplate", "PACE", 16.5f, UiPalette.Muted, true);
            grp.characterSpacing = 8f;
            Le(grp, h: 30);

            var cardImg = Chip(tpl, "CardTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(cardImg.gameObject, 15, 12, 15).childAlignment = TextAnchor.MiddleLeft;
            var card = cardImg.gameObject.AddComponent<PerkCardView>();
            card.background = cardImg;
            card.border = cardImg.GetComponent<Outline>();
            card.border.effectDistance = new Vector2(2f, -2f);
            card.group = cardImg.gameObject.AddComponent<CanvasGroup>();
            card.cardButton = cardImg.gameObject.AddComponent<Button>();
            card.cardButton.targetGraphic = cardImg;
            var nav = card.cardButton.navigation; nav.mode = Navigation.Mode.None; card.cardButton.navigation = nav;
            card.icon = Icon(cardImg.rectTransform, "Icon", Emoji("perk_haste"), 45);
            var body = NewUi("Body", cardImg.rectTransform);
            ColW(body.gameObject, 0, 3);
            Le(body, flexW: 1);
            var nameRow = NewUi("NameRow", body);
            Row(nameRow.gameObject, 0, 0, 9);
            card.nameText = Label(nameRow, "Name", "Haste", 22.5f, UiPalette.Text, true);
            card.levelText = Label(nameRow, "Level", "0/5", 19.5f, UiPalette.Gold, true);
            var pick = Chip(nameRow, "Pick", ViewKit.Rgba(251, 191, 36, 0.15f), UiPalette.Gold);
            Row(pick.gameObject, 8, 1, 0);
            Label(pick.rectTransform, "Label", "good first pick", 15f, UiPalette.Gold, true);
            card.pickTag = pick.gameObject;
            card.descText = Text(body, "Desc", "desc", 18.75f, UiPalette.Muted, false, true);
            card.fxText = Text(body, "Fx", "timers ×1.00 → ×0.95", 18f, UiPalette.Accent, false, true);
            card.buyButton = SmallButton(cardImg.rectTransform, "Buy", "1 AP", UiPalette.Gold, 120, 51);
            card.buyLabel = card.buyButton.GetComponentInChildren<TextMeshProUGUI>();
            EditorUtility.SetDirty(card);

            Set(view, "modal", modal);
            Set(view, "closeButton", close);
            Set(view, "apLine", ap);
            Set(view, "listRoot", content);
            Set(view, "groupTemplate", grp);
            Set(view, "cardTemplate", card);
            Set(view, "scroll", scroll);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/PerkShop.prefab");
        }

        static Toggle CheckBox(Transform parent)
        {
            var bg = Chip(parent, "Toggle", UiPalette.Panel, UiPalette.Line, raycast: true);
            Le(bg, 30, 30);
            var check = Icon(bg.rectTransform, "Check", null, 18);
            check.sprite = UiSprite; check.type = Image.Type.Sliced; check.color = UiPalette.Accent;
            var crt = check.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(18f, 18f);
            var t = bg.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg; t.graphic = check; t.isOn = false;
            return t;
        }

        static void BuildAscendPrefab()
        {
            var root = Stretch(NewUi("AscendDialog", null));
            var view = root.gameObject.AddComponent<AscendDialogView>();
            var (modal, box, _) = FitBox(root, UiPalette.Gold, 960f, 27, 12);
            var brt = box.rectTransform;
            Icon(brt, "Icon", Emoji("bld_ascension_gate"), 72).GetComponent<LayoutElement>().preferredHeight = 72;
            Text(brt, "Title", "The Ascension Gate stands complete", 28f, UiPalette.Gold, true, true, TextAlignmentOptions.Center);
            Text(brt, "Flavour", "Talisman, star steel and dragon scale hum as one. The path beyond the grounds opens — step through, and begin again as something greater.",
                19.5f, UiPalette.Text, false, true, TextAlignmentOptions.Center);
            var count = Text(brt, "Count", "", 19.5f, UiPalette.Text, false, true, TextAlignmentOptions.Center);

            var keep = NewUi("KeepReset", brt);
            var kr = Row(keep.gameObject, 0, 0, 15);
            kr.childAlignment = TextAnchor.UpperLeft; kr.childForceExpandWidth = true;
            void Column(string name, string head, Color c, string[] items)
            {
                var chip = Chip(keep, name, UiPalette.Panel, UiPalette.Line);
                ColW(chip.gameObject, 12, 4);
                Le(chip, flexW: 1);
                Text(chip.rectTransform, "Title", head, 19.5f, c, true, true);
                Text(chip.rectTransform, "Body", "• " + string.Join("\n• ", items), 18f, UiPalette.Muted, false, true);
            }
            Column("Keep", "Keep", UiPalette.Accent, new[] { "Perks + Ascension Points", "Ascension speed + vow marks", "Dragon's blessing", "Lifetime stats", "Know-how (tutorial skipped)" });
            Column("Reset", "Reset", UiPalette.Danger, new[] { "Buildings & regions", "Resources", "Dragon stages", "Upgrades" });

            var vows = Chip(brt, "Vows", UiPalette.Panel, UiPalette.Line);
            ColW(vows.gameObject, 12, 5);
            Text(vows.rectTransform, "Title", "Vows for the next run (optional)", 19.5f, UiPalette.Gold, true, true);
            var hint = Text(vows.rectTransform, "Hint", "", 18f, UiPalette.Muted, false, true);
            var vowRows = NewUi("Rows", vows.rectTransform);
            ColW(vowRows.gameObject, 0, 4);

            var row = ButtonRow(brt);
            var go = BigButton(row, "Ascend", "Ascend", UiPalette.Gold);
            var later = BigButton(row, "Later", "Keep playing", UiPalette.Text);
            var perks = BigButton(row, "Perks", "See perks", UiPalette.Text);

            var tpl = Templates(root);
            var vr = NewUi("VowTemplate", tpl);
            Row(vr.gameObject, 0, 2, 10).childAlignment = TextAnchor.MiddleLeft;
            var vv = vr.gameObject.AddComponent<VowRowView>();
            vv.toggle = CheckBox(vr);
            vv.icon = Icon(vr, "Icon", Emoji("ui_vow_burden"), 27);
            vv.label = Text(vr, "Label", "Vow", 18f, UiPalette.Text, false, true);
            EditorUtility.SetDirty(vv);

            Set(view, "modal", modal);
            Set(view, "countText", count);
            Set(view, "vowsBox", vows.gameObject);
            Set(view, "vowsHint", hint);
            Set(view, "vowsRoot", vowRows);
            Set(view, "vowTemplate", vv);
            Set(view, "ascendButton", go);
            Set(view, "laterButton", later);
            Set(view, "perksButton", perks);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/AscendDialog.prefab");
        }

        static void BuildPostAscensionPrefab()
        {
            var root = Stretch(NewUi("PostAscension", null));
            var view = root.gameObject.AddComponent<PostAscensionCardView>();
            var (modal, box, _) = FitBox(root, UiPalette.Gold, 780f);
            var brt = box.rectTransform;
            Icon(brt, "Icon", Emoji("ui_ascend"), 72).GetComponent<LayoutElement>().preferredHeight = 72;
            var title = Text(brt, "Title", "Ascension 1 complete", 28f, UiPalette.Gold, true, true, TextAlignmentOptions.Center);
            var body = Text(brt, "Body", "", 19.5f, UiPalette.Text, false, true, TextAlignmentOptions.Center);
            var row = ButtonRow(brt);
            var shrine = BigButton(row, "Shrine", "Open Shrine", UiPalette.Gold);
            var begin = BigButton(row, "Begin", "Begin run 2", UiPalette.Text);
            Set(view, "modal", modal);
            Set(view, "titleText", title);
            Set(view, "bodyText", body);
            Set(view, "shrineButton", shrine);
            Set(view, "beginButton", begin);
            Set(view, "beginLabel", begin.GetComponentInChildren<TextMeshProUGUI>());
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/PostAscension.prefab");
        }

        static void BuildWelcomePrefab()
        {
            var root = Stretch(NewUi("WelcomeModal", null));
            var view = root.gameObject.AddComponent<WelcomeModalView>();
            var (modal, box, _) = FitBox(root, UiPalette.Line, 840f);
            var brt = box.rectTransform;
            var icon = Icon(brt, "Icon", Emoji("ui_area_farm"), 72);     // 🌱
            icon.GetComponent<LayoutElement>().preferredHeight = 72;
            var title = Text(brt, "Title", "Welcome to Idle Grounds", 28f, UiPalette.Text, true, true, TextAlignmentOptions.Center);
            var body = Text(brt, "Body", "", 19.5f, UiPalette.Text, false, true, TextAlignmentOptions.Center);
            var hint = Text(brt, "Hint", "", 18f, UiPalette.Muted, false, true, TextAlignmentOptions.Center);
            var row = ButtonRow(brt);
            var cont = BigButton(row, "Continue", "Continue >", UiPalette.Text);

            Set(view, "modal", modal);
            Set(view, "icon", icon);
            Set(view, "introSprite", Emoji("ui_area_farm"));
            Set(view, "titleText", title);
            Set(view, "bodyText", body);
            Set(view, "hintText", hint);
            Set(view, "continueButton", cont);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/WelcomeModal.prefab");
        }

        static void BuildToastPrefab()
        {
            var root = Stretch(NewUi("Toast", null));
            var view = root.gameObject.AddComponent<ToastView>();
            var chip = Chip(root, "Chip", UiPalette.Bg2, UiPalette.AccentDk);
            chip.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            var crt = chip.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -21f);
            Row(chip.gameObject, 21, 12, 0);
            Fit(chip.gameObject, true, true);
            var cg = chip.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
            var t = Label(crt, "Text", "", 21f, UiPalette.Text, true);
            Set(view, "root", chip.gameObject);
            Set(view, "group", cg);
            Set(view, "text", t);
            chip.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/Toast.prefab");
        }

        static void BuildConfirmPrefab()
        {
            var root = Stretch(NewUi("ConfirmDialog", null));
            var view = root.gameObject.AddComponent<ConfirmDialogView>();
            var (modal, box, _) = FitBox(root, UiPalette.Line, 660f, 27, 18);
            var brt = box.rectTransform;
            var msg = Text(brt, "Message", "Are you sure?", 21f, UiPalette.Text, true, true, TextAlignmentOptions.Center);
            var row = ButtonRow(brt);
            var ok = BigButton(row, "OK", "OK", UiPalette.Gold, 200);
            var cancel = BigButton(row, "Cancel", "Cancel", UiPalette.Text, 200);
            Set(view, "modal", modal);
            Set(view, "messageText", msg);
            Set(view, "okButton", ok);
            Set(view, "okLabel", ok.GetComponentInChildren<TextMeshProUGUI>());
            Set(view, "cancelButton", cancel);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/ConfirmDialog.prefab");
        }

        // ------------------------------------------------------------------ scene wiring

        public static void InstallIntoActiveScene()
        {
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var bc = Object.FindFirstObjectByType<BuildController>();
            var fx = Object.FindFirstObjectByType<FxService>();
            var bar = Object.FindFirstObjectByType<BottomBarView>();
            if (runner == null || bc == null || bar == null) { Debug.LogError("[IdleGrounds] Install M2-M6 first."); return; }

            // systems
            var systems = FindOrCreate("--- Systems", null).transform;
            var save = Ensure<SaveService>(FindOrCreate("SaveService", systems));
            Set(save, "runner", runner);
            var audio = Ensure<AudioService>(FindOrCreate("AudioService", systems));
            Set(audio, "runner", runner);
            Set(audio, "library", EnsureSfxLibrary());
            if (fx != null) Set(fx, "sparkSprite", EnsureCircleSprite());

            // modals under UI/Canvas (removing old copies)
            var canvas = GameObject.Find("UI/Canvas");
            if (canvas == null) { Debug.LogError("[IdleGrounds] UI/Canvas missing."); return; }
            foreach (var n in Prefabs)
            {
                var old = canvas.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var made = new Dictionary<string, GameObject>();
            foreach (var n in Prefabs)
                made[n] = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/" + n + ".prefab"), canvas.transform);
            T Get<T>(string n) where T : Component { var c = made[n].GetComponent<T>(); Set(c, "runner", runner); return c; }
            var help = Get<HelpModalView>("HelpModal");
            var stats = Get<StatsPanelView>("StatsPanel");
            var shop = Get<PerkShopView>("PerkShop");
            var ascend = Get<AscendDialogView>("AscendDialog");
            var post = Get<PostAscensionCardView>("PostAscension");
            var welcome = Get<WelcomeModalView>("WelcomeModal");
            var toast = made["Toast"].GetComponent<ToastView>();
            var confirm = Get<ConfirmDialogView>("ConfirmDialog");
            Set(ascend, "confirm", confirm);
            Set(ascend, "perkShop", shop);
            Set(post, "perkShop", shop);

            var meta = Ensure<MetaUiController>(FindOrCreate("MetaUi", systems));
            Set(meta, "runner", runner);
            Set(meta, "build", bc);
            Set(meta, "help", help);
            Set(meta, "stats", stats);
            Set(meta, "perkShop", shop);
            Set(meta, "ascend", ascend);
            Set(meta, "postAscension", post);
            Set(meta, "welcome", welcome);
            Set(meta, "toast", toast);
            Set(meta, "confirm", confirm);

            // bottom bar additions
            InstallBar(bar, runner, help, stats, shop, confirm);

            var cursor = canvas.transform.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void InstallBar(BottomBarView bar, GameRunner runner, HelpModalView help, StatsPanelView stats, PerkShopView shop, ConfirmDialogView confirm)
        {
            var t = bar.transform;
            foreach (var n in new[] { "ShrinePill", "MuteButton" })
            {
                var old = t.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var area = t.Find("AreaPill");
            if (area != null) { var oldTag = area.Find("AscTag"); if (oldTag != null) Object.DestroyImmediate(oldTag.gameObject); }

            // ☯ Shrine pill after the buff pill (or the hand pill)
            var pillImg = Chip(t, "ShrinePill", UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(pillImg.gameObject, 12, 6, 6);
            Icon(pillImg.rectTransform, "Icon", Emoji("ui_ascend"), 22);
            var pillText = Label(pillImg.rectTransform, "Count", "0", 19, UiPalette.Gold, true);
            var pill = pillImg.gameObject.AddComponent<Button>();
            pill.targetGraphic = pillImg;
            var anchor = t.Find("BuffPill") ?? t.Find("HandPill");
            if (anchor != null) pillImg.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
            pillImg.gameObject.SetActive(false);

            // mute toggle before Help
            var mute = BarButton(t, "MuteButton", "Sound", null, UiPalette.Text);
            var helpBtn = t.Find("HelpButton");
            if (helpBtn != null) mute.transform.SetSiblingIndex(helpBtn.GetSiblingIndex());

            // ascensions + world speed tag in the area pill
            GameObject tag = null; TextMeshProUGUI tagText = null;
            if (area != null)
            {
                var tagRt = NewUi("AscTag", area);
                Row(tagRt.gameObject, 0, 0, 3);
                Icon(tagRt, "Icon", Emoji("ui_ascend"), 18);
                tagText = Label(tagRt, "Label", "1  ×1.20", 17, UiPalette.Gold, true);
                tag = tagRt.gameObject;
                tag.SetActive(false);
            }

            var view = Ensure<MetaBarView>(bar.gameObject);
            Set(view, "runner", runner);
            Set(view, "helpButton", helpBtn != null ? helpBtn.GetComponent<Button>() : null);
            var statsBtn = t.Find("StatsButton");
            Set(view, "statsButton", statsBtn != null ? statsBtn.GetComponent<Button>() : null);
            var reset = t.Find("ResetButton");
            if (reset != null) { var rb = reset.GetComponent<Button>(); rb.interactable = true; Set(view, "resetButton", rb); }
            Set(view, "muteButton", mute);
            Set(view, "muteLabel", mute.transform.Find("Label").GetComponent<TextMeshProUGUI>());
            Set(view, "shrinePill", pill);
            Set(view, "shrineText", pillText);
            Set(view, "shrineBorder", pillImg.GetComponent<Outline>());
            Set(view, "ascTag", tag);
            Set(view, "ascText", tagText);
            Set(view, "help", help);
            Set(view, "stats", stats);
            Set(view, "perkShop", shop);
            Set(view, "confirm", confirm);
        }
    }
}
