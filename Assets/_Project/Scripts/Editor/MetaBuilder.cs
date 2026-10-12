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
    /// M7 "Save" + M8 "Juice &amp; audio" view side, reproducible: the SfxLibrary asset; UI
    /// prefabs HelpModal, StatsPanel, WelcomeModal, Toast, ConfirmDialog; scene wiring — SaveService /
    /// AudioService / MetaUiController under "--- Systems", the modals under UI/Canvas (above the M5 panels,
    /// below the hand cursor), the bottom-bar additions (mute + motion buttons, MetaBarView) and the FxService
    /// spark sprite. The ascension UI (PerkShop, AscendDialog, PostAscension, Shrine pill, ascension tag) was
    /// removed by ADR 0007; installing deletes leftover copies of it from the scene.
    /// </summary>
    public static class MetaBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string SfxDir = "Assets/_Project/Audio/SFX";
        const string SfxLibraryPath = "Assets/_Project/Audio/SfxLibrary.asset";
        static readonly string[] SfxNames = { "harvest", "pickup", "swing", "craft", "build", "upgrade", "unlock", "hit", "kill", "dragon", "ascend", "error", "click" };
        static readonly string[] Prefabs = { "HelpModal", "StatsPanel", "WelcomeModal", "Toast", "ConfirmDialog" };
        /// <summary>Retired ascension modals (ADR 0007): removed from the canvas on install.</summary>
        static readonly string[] RetiredPrefabs = { "PostAscension", "AscendDialog", "PerkShop" };

        [MenuItem("Idle Grounds/Prefabs/Build Meta Prefabs (M7+M8)")]
        public static void BuildAllPrefabs()
        {
            EnsureShapes();
            Directory.CreateDirectory(UiPrefabDir);
            EnsureSfxLibrary();
            BuildHelpPrefab();
            BuildStatsPrefab();
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
        static (TextMeshProUGUI title, Button close) Header(RectTransform box, string title, Sprite icon, float top = 34f)
        {
            var head = NewUi("Header", box);
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(-72f, 54f);
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
            // the ScrollRect lives on its own area inside the frame (36 px clear of the 32 px frame art), so its scrollbar sits inside it too
            var area = NewUi("ScrollArea", box);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(36f, Mathf.Max(bottom, 36f)); area.offsetMax = new Vector2(-36f, -top);
            var vp = NewUi("Viewport", area);
            Stretch(vp);
            var vimg = vp.gameObject.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0f);
            vimg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            var content = NewUi("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var col = ColW(content.gameObject, 0, spacing);
            col.padding = new RectOffset(0, 26, 0, 12);      // clear of the 16 px scrollbar
            Fit(content.gameObject, false, true);
            var sr = area.gameObject.AddComponent<ScrollRect>();
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
            TextAnchor align = TextAnchor.UpperCenter, string skin = "ui_panel_jade")
        {
            var (modal, box) = Modal(root, border, skin);
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
            var (modal, box) = Modal(root, UiPalette.Line, "ui_panel_scroll");
            var brt = box.rectTransform;
            brt.sizeDelta = new Vector2(840f, 930f);
            var (title, close) = Header(brt, "How to play", null);
            var (scroll, content) = Scroll(brt, 100f, 36f, 15f);

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
            ColW(rows.gameObject, 0, 4);
            rows.gameObject.AddComponent<LayoutElement>().minHeight = StatsBodyHeight;    // same body height as the Flow tab: the tabs never jump

            // ---- Flow tab (per-item produced / consumed / lost ledger)
            var flow = NewUi("FlowRoot", brt);
            ColW(flow.gameObject, 0, 8);
            flow.gameObject.AddComponent<LayoutElement>().minHeight = StatsBodyHeight;
            var ctl = NewUi("Controls", flow);
            Row(ctl.gameObject, 0, 0, 10);
            var scopeBtn = SmallButton(ctl, "ScopeButton", "This run", UiPalette.Gold, 150, 40);
            var sortBtn = SmallButton(ctl, "SortButton", "Sort: net / min", UiPalette.Text, 210, 40);
            var hint = Label(ctl, "Hint", "net / min = last ~5 min", 17f, UiPalette.MutedSoft, false);
            hint.alignment = TextAlignmentOptions.MidlineRight;
            Le(hint, flexW: 1);

            var hdr = NewUi("ColumnHeader", flow);
            var hdrRow = Row(hdr.gameObject, 8, 0, 8);
            hdrRow.padding = new RectOffset(8, 8 + 26, 0, 0);       // aligned with the rows, which leave room for the scrollbar
            Le(NewUi("IconSpace", hdr), w: 30);
            Le(HeaderCell(hdr, "Item", TextAlignmentOptions.MidlineLeft), flexW: 1);
            Le(HeaderCell(hdr, "Made", TextAlignmentOptions.MidlineRight), w: 86);
            Le(HeaderCell(hdr, "Used", TextAlignmentOptions.MidlineRight), w: 86);
            Le(HeaderCell(hdr, "Lost", TextAlignmentOptions.MidlineRight), w: 76);
            Le(HeaderCell(hdr, "Net/min", TextAlignmentOptions.MidlineRight), w: 90);

            var scrollRt = NewUi("FlowScroll", flow);
            Le(scrollRt, h: 470);
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
            ccol.padding = new RectOffset(0, 26, 0, 8);          // the 16 px scrollbar never touches the Net/min column
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
            fv.lostText = FlowCell(fr, "Lost", UiPalette.MutedSoft, 76, false);
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

        const float StatsBodyHeight = 570f;

        static TextMeshProUGUI HeaderCell(Transform parent, string text, TextAlignmentOptions align)
        {
            var t = Label(parent, text, text, 16.5f, UiPalette.MutedSoft, true);
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

        static void BuildWelcomePrefab()
        {
            var root = Stretch(NewUi("WelcomeModal", null));
            var view = root.gameObject.AddComponent<WelcomeModalView>();
            var (modal, box, _) = FitBox(root, UiPalette.Line, 840f, skin: "ui_panel_scroll");
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
            // stack: top-centre column of up to 3 dark pills (ToastView clones the template)
            var stack = NewUi("Stack", root);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 1f);
            stack.pivot = new Vector2(0.5f, 1f);
            stack.anchoredPosition = new Vector2(0f, -21f);
            var col = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 8f; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = false; col.childForceExpandHeight = false;
            Fit(stack.gameObject, true, true);
            var chip = Chip(stack, "ItemTemplate", new Color(UiPalette.Bg.r, UiPalette.Bg.g, UiPalette.Bg.b, 0.92f), UiPalette.AccentDk);
            UiSkin.Attach(chip.gameObject, "ui_panel_dark");
            chip.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            Row(chip.gameObject, 21, 12, 12);
            var cg = chip.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
            Icon(chip.rectTransform, "Icon", Emoji("ui_quests"), 30);
            Label(chip.rectTransform, "Text", "", 21f, UiPalette.Text, true);
            Set(view, "root", stack.gameObject);
            Set(view, "stack", stack);
            Set(view, "itemTemplate", chip.gameObject);
            Set(view, "infoIcon", Emoji("ui_quests"));
            Set(view, "successIcon", Emoji("ui_claim"));
            Set(view, "warningIcon", Emoji("ui_alert"));
            chip.gameObject.SetActive(false);
            stack.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/Toast.prefab");
        }

        static void BuildConfirmPrefab()
        {
            var root = Stretch(NewUi("ConfirmDialog", null));
            var view = root.gameObject.AddComponent<ConfirmDialogView>();
            var (modal, box, _) = FitBox(root, UiPalette.Line, 660f, 27, 18);
            var brt = box.rectTransform;
            var ctitle = Text(brt, "Title", "Are you sure?", 27f, UiPalette.Gold, true, true, TextAlignmentOptions.Center);
            var msg = Text(brt, "Message", "", 20f, UiPalette.Text, false, true, TextAlignmentOptions.Center);
            var row = ButtonRow(brt);
            var ok = BigButton(row, "OK", "OK", UiPalette.Gold, 200);
            var cancel = BigButton(row, "Cancel", "Cancel", UiPalette.Text, 200);
            Set(view, "modal", modal);
            Set(view, "titleText", ctitle);
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
            foreach (var n in RetiredPrefabs)
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
            var welcome = Get<WelcomeModalView>("WelcomeModal");
            var toast = made["Toast"].GetComponent<ToastView>();
            var confirm = Get<ConfirmDialogView>("ConfirmDialog");

            var meta = Ensure<MetaUiController>(FindOrCreate("MetaUi", systems));
            Set(meta, "runner", runner);
            Set(meta, "build", bc);
            Set(meta, "help", help);
            Set(meta, "stats", stats);
            Set(meta, "welcome", welcome);
            Set(meta, "toast", toast);
            Set(meta, "confirm", confirm);

            // bottom bar additions
            InstallBar(bar, runner, help, stats, confirm);

            var cursor = canvas.transform.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void InstallBar(BottomBarView bar, GameRunner runner, HelpModalView help, StatsPanelView stats, ConfirmDialogView confirm)
        {
            var t = bar.transform;
            foreach (var n in new[] { "ShrinePill", "MuteButton", "MotionButton" })   // ShrinePill: retired (ADR 0007)
            {
                var old = t.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var area = t.Find("AreaPill");
            if (area != null) { var oldTag = area.Find("AscTag"); if (oldTag != null) Object.DestroyImmediate(oldTag.gameObject); }

            // mute toggle before Help
            var mute = BarButton(t, "MuteButton", "Sound", null, UiPalette.Text);
            var helpBtn = t.Find("HelpButton");
            if (helpBtn != null) mute.transform.SetSiblingIndex(helpBtn.GetSiblingIndex());
            // Reduce motion toggle right after Sound (persisted in PlayerPrefs ig_reduce_motion)
            var motion = BarButton(t, "MotionButton", "Motion", null, UiPalette.Text);
            motion.transform.SetSiblingIndex(mute.transform.GetSiblingIndex() + 1);

            var view = Ensure<MetaBarView>(bar.gameObject);
            Set(view, "runner", runner);
            Set(view, "helpButton", helpBtn != null ? helpBtn.GetComponent<Button>() : null);
            var statsBtn = t.Find("StatsButton");
            Set(view, "statsButton", statsBtn != null ? statsBtn.GetComponent<Button>() : null);
            var reset = t.Find("ResetButton");
            if (reset != null) { var rb = reset.GetComponent<Button>(); rb.interactable = true; Set(view, "resetButton", rb); }
            Set(view, "muteButton", mute);
            Set(view, "muteLabel", mute.transform.Find("Label").GetComponent<TextMeshProUGUI>());
            Set(view, "motionButton", motion);
            Set(view, "motionLabel", motion.transform.Find("Label").GetComponent<TextMeshProUGUI>());
            Set(view, "help", help);
            Set(view, "stats", stats);
            Set(view, "confirm", confirm);
        }
    }
}
