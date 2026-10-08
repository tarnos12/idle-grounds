using System.IO;
using IdleGrounds.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGrounds.Editor.BuildingsBuilder;
using static IdleGrounds.Editor.CoreLoopBuilder;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// M5 "Progression" + M6 "Combat &amp; idle" view side, reproducible: Enemy + UnlockSign world
    /// prefabs; DragonDialog, UpgradeTree, QuestPanel, PavilionPanel, BuffPill and AutoSkipChips UI
    /// prefabs; scene wiring (EnemyViewSync under Runtime/Enemies, UnlockSignSync under
    /// Runtime/UnlockSigns, panels under UI/Canvas, BuildController / HandController refs, the buff
    /// pill inside the bottom bar after the hand pill). The Altar job line, dragon murmur, AUTO badge
    /// and Spirit Tree sparkles live in the Altar/Dragon/Node prefabs (BuildingsBuilder/CoreLoopBuilder).
    /// </summary>
    public static class ProgressionBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const float S = 1.5f;     // UI = 1.5x CSS px at 1080p

        [MenuItem("Idle Grounds/Prefabs/Build Progression + Combat Prefabs (M5+M6)")]
        public static void BuildAllPrefabs()
        {
            EnsureSortingLayers();
            EnsureShapes();
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            BuildEnemyPrefab();
            BuildUnlockSignPrefab();
            BuildDragonDialogPrefab();
            BuildUpgradeTreePrefab();
            BuildQuestPanelPrefab();
            BuildPavilionPanelPrefab();
            BuildBuffPillPrefab();
            BuildAutoSkipPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] M5+M6 prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Progression+Combat (M5+M6)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] M5+M6 installed into " + ScenePath);
        }

        // ------------------------------------------------------------------ world prefabs

        static void BuildEnemyPrefab()
        {
            var root = new GameObject("Enemy");
            var sg = root.AddComponent<SortingGroup>();
            sg.sortingLayerName = "Entities";
            var v = root.AddComponent<EnemyView>();
            var squash = Node(root.transform, "Squash");
            var sprite = Sr(squash, "Sprite", 1, Emoji("enemy_fox"), "Entities");
            var pips = Node(root.transform, "Pips");
            var pip = Sr(pips, "PipTemplate", 3, circle, "Entities");
            pip.color = EnemyView.PipLive;
            pip.gameObject.SetActive(false);
            var track = Sr(root.transform, "BarTrack", 2, square, "Entities");
            track.color = EnemyView.PipLost;
            var fill = Sr(root.transform, "BarFill", 3, square, "Entities");
            fill.color = EnemyView.PipLive;
            track.gameObject.SetActive(false); fill.gameObject.SetActive(false);
            Set(v, "squash", squash);
            Set(v, "spriteRenderer", sprite);
            Set(v, "pips", pips);
            Set(v, "pipTemplate", pip);
            Set(v, "barTrack", track);
            Set(v, "barFill", fill);
            Set(v, "sortingGroup", sg);
            Save(root, WorldPrefabDir + "/Enemy.prefab");
        }

        static void BuildUnlockSignPrefab()
        {
            const string L = "Overlay";
            var root = new GameObject("UnlockSign");
            var v = root.AddComponent<UnlockSignView>();
            var panel = Sr(root.transform, "Panel", 30, rounded, L);
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.color = UnlockSignView.PanelColour;
            var glow = Frame(root.transform, "Glow", 29);
            var frame = Frame(root.transform, "Frame", 31);
            var icon = Sr(root.transform, "Icon", 32, Emoji("ui_unlock"), L);
            var title = BuildingsBuilder.Text(root.transform, "Title", 15f, UiPalette.Gold, true, 33, TextAlignmentOptions.Center, 4f);
            var cost = IconRowNode(root.transform, "Cost", 32);
            var pay = BuildingsBuilder.Text(root.transform, "Pay", 12f, UiPalette.Gold, true, 33, TextAlignmentOptions.Center, 4f);
            IslandArtBuilder.EnsureArt();
            var stele = Sr(root.transform, "Stele", 28, IslandArtBuilder.Single(IslandArtBuilder.SteleLocked), L);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sortingLayerName = L;
            Set(v, "panel", panel);
            Set(v, "frame", frame);
            Set(v, "glow", glow);
            Set(v, "icon", icon);
            Set(v, "title", title);
            Set(v, "cost", cost);
            Set(v, "pay", pay);
            Set(v, "stele", stele);
            Set(v, "steleLocked", IslandArtBuilder.Single(IslandArtBuilder.SteleLocked));
            Set(v, "stelePartial", IslandArtBuilder.Single(IslandArtBuilder.StelePartial));
            SetArray(v, "steleReady", IslandArtBuilder.Frames(IslandArtBuilder.SteleReady));
            Save(root, WorldPrefabDir + "/UnlockSign.prefab");
        }

        // ------------------------------------------------------------------ UI helpers

        internal static LayoutElement Le(Component c, float w = -1, float h = -1, float flexW = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        internal static TextMeshProUGUI Wrap(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            Le(t, flexW: 1);
            return t;
        }

        internal static VerticalLayoutGroup ColW(GameObject go, int pad, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = Col(go, pad, spacing, align);
            v.childForceExpandWidth = true;
            return v;
        }

        internal static Button SmallButton(Transform parent, string name, string label, Color text, float w, float h)
        {
            var img = Chip(parent, name, UiPalette.Panel, UiPalette.Line, raycast: true);
            Le(img, w, h);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); cb.disabledColor = new Color(1f, 1f, 1f, 0.45f); b.colors = cb;
            var l = Label(img.rectTransform, "Label", label, 19f, text, true);
            Stretch(l.rectTransform);
            l.alignment = TextAlignmentOptions.Center;
            UiButtonRoles.AttachFor(b);
            return b;
        }

        /// <summary>Scrim rgba(0,0,0,.55) + centred box (bg-2, 2 px border).</summary>
        internal static (GameObject modal, Image box) Modal(RectTransform root, Color border, string skin = "ui_panel_jade")
        {
            var modal = Stretch(NewUi("Modal", root));
            var scrim = Stretch(NewUi("Scrim", modal)).gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrim.raycastTarget = true;
            var box = Chip(modal, "Box", UiPalette.Bg2, border, raycast: true);
            box.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            UiSkin.Attach(box.gameObject, skin);
            var brt = box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            return (modal.gameObject, box);
        }

        internal static (RectTransform bar, Image fill) Bar(Transform parent, string name, float h)
        {
            var bar = NewUi(name, parent);
            Le(bar, h: h);
            var track = Stretch(NewUi("Track", bar)).gameObject.AddComponent<Image>();
            track.sprite = square; track.color = new Color(1f, 1f, 1f, 0.12f); track.raycastTarget = false;
            var fill = Stretch(NewUi("Fill", bar)).gameObject.AddComponent<Image>();
            fill.sprite = square; fill.color = UiPalette.Accent; fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0.4f;
            return (bar, fill);
        }

        internal static UiIconLine IconLine(Transform parent, string name, Sprite s, float icon, float text, Color c, bool wrap)
        {
            var rt = NewUi(name, parent);
            var h = Row(rt.gameObject, 0, 0, 6);
            h.childAlignment = TextAnchor.UpperLeft;
            var line = rt.gameObject.AddComponent<UiIconLine>();
            line.icon = Icon(rt, "Icon", s, icon);
            line.text = Label(rt, "Text", "", text, c, false);
            line.text.richText = true;
            if (wrap) Wrap(line.text);
            return line;
        }

        // ------------------------------------------------------------------ UI prefabs

        static void BuildDragonDialogPrefab()
        {
            var root = Stretch(NewUi("DragonDialog", null));
            var view = root.gameObject.AddComponent<DragonDialogView>();
            var (modal, box) = Modal(root, UiPalette.Purple);
            var brt = box.rectTransform;
            brt.sizeDelta = new Vector2(780f, 300f);
            ColW(box.gameObject, 27, 14, TextAnchor.UpperCenter);
            Fit(box.gameObject, false, true);
            var icon = Icon(brt, "Icon", Emoji("dragon_sleeping"), 84);
            icon.GetComponent<LayoutElement>().preferredHeight = 84;
            var title = Label(brt, "Title", "The Dragon Awakens", 30f, UiPalette.Gold, true);
            title.alignment = TextAlignmentOptions.Center;
            var body = Wrap(Label(brt, "Body", "The dragon stirs...", 21f, UiPalette.Text, false));
            body.alignment = TextAlignmentOptions.Center;
            var stats = Wrap(Label(brt, "Stats", "", 19f, UiPalette.Muted, false));
            stats.alignment = TextAlignmentOptions.Center;
            var btnRow = NewUi("Buttons", brt);
            Row(btnRow.gameObject, 0, 0, 0).childAlignment = TextAnchor.MiddleCenter;
            var cont = SmallButton(btnRow, "Continue", "Continue >", UiPalette.Text, 210, 48);
            Set(view, "modal", modal);
            Set(view, "box", box);
            Set(view, "border", box.GetComponent<Outline>());
            Set(view, "icon", icon);
            Set(view, "titleText", title);
            Set(view, "bodyText", body);
            Set(view, "statsText", stats);
            Set(view, "continueButton", cont);
            Set(view, "sleepingSprite", Emoji("dragon_sleeping"));
            Set(view, "awakeSprite", Emoji("dragon_awake"));
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/DragonDialog.prefab");
        }

        static void BuildUpgradeTreePrefab()
        {
            var root = Stretch(NewUi("UpgradeTree", null));
            var view = root.gameObject.AddComponent<UpgradeTreeView>();
            var (modal, box) = Modal(root, UiPalette.Line);
            var brt = box.rectTransform;
            brt.sizeDelta = new Vector2(1380f, 980f);

            // header
            var head = NewUi("Header", brt);
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.sizeDelta = new Vector2(-36f, 54f);
            head.anchoredPosition = new Vector2(0f, -12f);
            Row(head.gameObject, 0, 0, 12);
            Icon(head, "Icon", Emoji("bld_center"), 36);
            Label(head, "Title", "Altar - Upgrades", 25f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var dbg = SmallButton(head, "Debug", "Debug", UiPalette.Muted, 105, 42);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 48, 42);

            // body (board) — WASD / drag pans; edges under nodes
            var body = NewUi("Body", brt);
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(18f, 78f); body.offsetMax = new Vector2(-18f, -78f);
            var bimg = body.gameObject.AddComponent<Image>();
            bimg.color = UiPalette.Hex("#232926");
            bimg.raycastTarget = true;
            body.gameObject.AddComponent<RectMask2D>();
            var tree = NewUi("Tree", body);
            tree.anchorMin = tree.anchorMax = new Vector2(0f, 1f);
            tree.pivot = new Vector2(0.5f, 0.5f);
            tree.sizeDelta = Vector2.zero;
            var edges = NewUi("Edges", tree);
            var nodes = NewUi("Nodes", tree);

            var edge = NewUi("EdgeTemplate", edges).gameObject.AddComponent<Image>();
            edge.raycastTarget = false;
            edge.color = new Color(225 / 255f, 232 / 255f, 224 / 255f, 0.22f);

            float n = 52f * S;
            var node = NewUi("NodeTemplate", nodes);
            node.sizeDelta = new Vector2(n, n);
            var nv = node.gameObject.AddComponent<UpgradeTreeNodeView>();
            var group = node.gameObject.AddComponent<CanvasGroup>();
            var brackets = Stretch(NewUi("Brackets", node)).gameObject.AddComponent<Image>();
            brackets.rectTransform.offsetMin = new Vector2(-8f, -8f); brackets.rectTransform.offsetMax = new Vector2(8f, 8f);
            brackets.sprite = UiSprite; brackets.type = Image.Type.Sliced; brackets.raycastTarget = false;
            var bg = Stretch(NewUi("Background", node)).gameObject.AddComponent<Image>();
            bg.sprite = UiSprite; bg.type = Image.Type.Sliced; bg.color = UpgradeTreeNodeView.Fill; bg.raycastTarget = true;
            var ol = bg.gameObject.AddComponent<Outline>();
            ol.effectColor = UiPalette.Accent; ol.effectDistance = new Vector2(3f * S * 0.7f, -3f * S * 0.7f);
            var ic = Icon(node, "Icon", Emoji("upg_hand"), 24f * S);
            ic.rectTransform.sizeDelta = new Vector2(24f * S, 24f * S);
            ic.rectTransform.anchoredPosition = new Vector2(0f, 3f * S);
            var lv = Label(node, "Level", "0/3", 9f * S, UiPalette.Accent, true);
            lv.alignment = TextAlignmentOptions.Center;
            lv.rectTransform.sizeDelta = new Vector2(n, 16f);
            lv.rectTransform.anchoredPosition = new Vector2(0f, -18f * S);
            var my = Label(node, "Mystery", "?", 26f * S, UiPalette.Danger, true);
            my.alignment = TextAlignmentOptions.Center;
            my.rectTransform.sizeDelta = new Vector2(n, n);
            Set(nv, "background", bg);
            Set(nv, "border", ol);
            Set(nv, "icon", ic);
            Set(nv, "level", lv);
            Set(nv, "mystery", my);
            Set(nv, "brackets", brackets);
            Set(nv, "group", group);

            // tooltip layer: same rect as the body, unmasked, never a raycast target
            var tipLayer = NewUi("TooltipLayer", brt);
            tipLayer.anchorMin = Vector2.zero; tipLayer.anchorMax = Vector2.one;
            tipLayer.offsetMin = body.offsetMin; tipLayer.offsetMax = body.offsetMax;
            var tipCg = tipLayer.gameObject.AddComponent<CanvasGroup>();
            tipCg.blocksRaycasts = false; tipCg.interactable = false;
            var tip = Chip(tipLayer, "Tooltip", new Color(8 / 255f, 11 / 255f, 13 / 255f, 0.97f), UiPalette.Line);
            UiSkin.Attach(tip.gameObject, "ui_tooltip_frame");
            var trt = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(345f, 100f);
            ColW(tip.gameObject, 12, 4);
            Fit(tip.gameObject, false, true);
            var tipText = Wrap(Label(trt, "Text", "", 18f, UiPalette.Text, false));
            tip.gameObject.SetActive(false);

            // footer: active job + cancel
            var foot = NewUi("Footer", brt);
            foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f);
            foot.pivot = new Vector2(0.5f, 0f);
            foot.sizeDelta = new Vector2(-36f, 60f);
            foot.anchoredPosition = new Vector2(0f, 10f);
            Row(foot.gameObject, 0, 0, 12);
            var job = Wrap(Label(foot, "Job", "", 18f, UiPalette.Text, false));
            var cancel = SmallButton(foot, "CancelJob", "Cancel & refund", UiPalette.Danger, 210, 44);

            Set(view, "modal", modal);
            Set(view, "box", brt);
            Set(view, "body", body);
            Set(view, "tree", tree);
            Set(view, "edgesRoot", edges);
            Set(view, "nodesRoot", nodes);
            Set(view, "edgeTemplate", edge);
            Set(view, "nodeTemplate", nv);
            Set(view, "tooltip", trt);
            Set(view, "tooltipText", tipText);
            Set(view, "jobText", job);
            Set(view, "cancelJobButton", cancel);
            Set(view, "closeButton", close);
            Set(view, "debugButton", dbg);
            edge.gameObject.SetActive(false);
            node.gameObject.SetActive(false);
            modal.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/UpgradeTree.prefab");
        }

        static void BuildQuestPanelPrefab()
        {
            var root = Stretch(NewUi("QuestPanel", null));
            var view = root.gameObject.AddComponent<QuestPanelView>();

            // collapsed chip
            var chipImg = Chip(root, "Chip", UiPalette.Bg2, UiPalette.Line, raycast: true);
            UiSkin.Attach(chipImg.gameObject, "ui_panel_dark");
            var crt = chipImg.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-21f, -21f);
            Row(chipImg.gameObject, 10, 6, 4);
            Fit(chipImg.gameObject, true, true);
            Icon(crt, "Icon", Emoji("ui_quests"), 36);
            var alert = Icon(crt, "Alert", Emoji("ui_alert"), 27);
            var chip = chipImg.gameObject.AddComponent<Button>();
            chip.targetGraphic = chipImg;

            // expanded panel
            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.96f), UiPalette.Line, raycast: true);
            UiSkin.Attach(panel.gameObject, "ui_questpanel_frame");
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-21f, -21f);
            prt.sizeDelta = new Vector2(375f, 300f);
            ColW(panel.gameObject, 34, 8);   // clear of the 32 px jade frame
            Fit(panel.gameObject, false, true);

            var head = NewUi("Header", prt);
            Row(head.gameObject, 0, 0, 8);
            var hIcon = Icon(head, "Icon", Emoji("ui_quests"), 27);
            var hText = Label(head, "Title", "Quest 1/14", 19.5f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var min = SmallButton(head, "Minimise", "-", UiPalette.Text, 36, 30);

            var qb = NewUi("Quest", prt);
            ColW(qb.gameObject, 0, 6);
            var nameLine = IconLine(qb, "Name", Emoji("ui_quest_wood"), 27, 21f, UiPalette.Text, true);
            nameLine.text.fontStyle = FontStyles.Bold;
            var desc = Wrap(Label(qb, "Desc", "", 18f, UiPalette.Muted, false));
            var unl = NewUi("Unlocks", qb);
            Row(unl.gameObject, 0, 0, 8);
            Label(unl, "Label", "Unlocks:", 16.5f, UiPalette.Muted, true);
            var unlTpl = IconLine(unl, "UnlockTemplate", Emoji("bld_workbench"), 24, 16.5f, UiPalette.Gold, false);
            unlTpl.GetComponent<HorizontalLayoutGroup>().spacing = 2;
            var (_, fill) = Bar(qb, "Bar", 9);
            var progRow = NewUi("ProgressRow", qb);
            Row(progRow.gameObject, 0, 0, 8);
            var prog = Label(progRow, "Progress", "0/5", 18f, UiPalette.Gold, true);
            Le(NewUi("Spacer", progRow), flexW: 1);
            var claim = SmallButton(progRow, "Claim", "Claim", UiPalette.Accent, 120, 39);
            var nextLine = IconLine(qb, "Next", Emoji("ui_quest_leaves"), 21, 16.5f, UiPalette.Text, true);

            var ms = NewUi("Milestone", prt);
            ColW(ms.gameObject, 0, 6);
            var msHead = NewUi("Header", ms);
            Row(msHead.gameObject, 0, 4, 6);
            Icon(msHead, "Icon", Emoji("ui_target"), 24);
            Label(msHead, "Title", "Next milestone", 18f, UiPalette.Text, true);
            var msLines = NewUi("Lines", ms);
            ColW(msLines.gameObject, 0, 4);
            var msTpl = IconLine(msLines, "LineTemplate", Emoji("item_wood"), 24, 16.5f, UiPalette.Text, true);
            var (msBar, msFill) = Bar(msLines, "Bar", 9);

            Set(view, "chip", chip);
            Set(view, "chipAlert", alert.gameObject);
            Set(view, "panel", panel.gameObject);
            Set(view, "headerText", hText);
            Set(view, "headerIcon", hIcon);
            Set(view, "minButton", min);
            Set(view, "questBlock", qb.gameObject);
            Set(view, "nameLine", nameLine);
            Set(view, "descText", desc);
            Set(view, "unlocksRow", unl);
            Set(view, "unlockTemplate", unlTpl);
            Set(view, "barFill", fill);
            Set(view, "progText", prog);
            Set(view, "claimButton", claim);
            Set(view, "claimLabel", claim.GetComponentInChildren<TextMeshProUGUI>());
            Set(view, "nextLine", nextLine);
            Set(view, "msBlock", ms.gameObject);
            Set(view, "msHeader", msHead.gameObject);
            Set(view, "msLines", msLines);
            Set(view, "msLineTemplate", msTpl);
            Set(view, "msBar", msBar.gameObject);
            Set(view, "msBarFill", msFill);
            Set(view, "questsSprite", Emoji("ui_quests"));
            Set(view, "targetSprite", Emoji("ui_target"));
            Set(view, "dragonSprite", Emoji("bld_dragon"));
            Set(view, "ascendSprite", Emoji("ui_ascend"));
            unlTpl.gameObject.SetActive(false);
            msTpl.gameObject.SetActive(false);
            chipImg.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/QuestPanel.prefab");
        }

        static void BuildPavilionPanelPrefab()
        {
            var root = Stretch(NewUi("PavilionPanel", null));
            var view = root.gameObject.AddComponent<PavilionPanelView>();
            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.97f), UiPalette.Line, raycast: true);
            UiSkin.Attach(panel.gameObject, "ui_panel_jade");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-24f, 100f);
            prt.anchoredPosition = new Vector2(0f, 68f);
            ColW(panel.gameObject, 14, 8);
            Fit(panel.gameObject, false, true);

            var head = NewUi("Header", prt);
            Row(head.gameObject, 0, 0, 10);
            var icon = Icon(head, "Icon", Emoji("bld_meditation_pavilion"), 33);
            var title = Label(head, "Title", "Meditation Pavilion", 21f, UiPalette.Text, true);
            Icon(head, "DiscipleIcon", Emoji("ui_disciple"), 27);
            var disc = Label(head, "Disciples", "Disciples 0/3", 19.5f, UiPalette.Text, true);
            var food = Icon(head, "FoodIcon", Emoji("item_spirit_buns"), 27);
            var foodText = Label(head, "Food", "0/20", 19.5f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 42, 36);

            var hint = Wrap(Label(prt, "Hint", "", 18f, UiPalette.Muted, false));

            var row = NewUi("RecruitRow", prt);
            Row(row.gameObject, 0, 0, 12);
            var recImg = Chip(row, "Recruit", UiPalette.Panel, UiPalette.Accent, raycast: true);
            Row(recImg.gameObject, 12, 6, 8);
            var rec = recImg.gameObject.AddComponent<Button>();
            rec.targetGraphic = recImg;
            var cb = rec.colors; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); cb.disabledColor = new Color(1f, 1f, 1f, 0.4f); rec.colors = cb;
            Icon(recImg.rectTransform, "Add", Emoji("ui_add"), 24);
            var recIcon = Icon(recImg.rectTransform, "Item", Emoji("item_robe"), 24);
            var recLabel = Label(recImg.rectTransform, "Label", "Recruit (1 Robe)", 19f, UiPalette.Text, true);
            var reason = Label(row, "Reason", "", 18f, UiPalette.Danger, true);

            Set(view, "panel", panel.gameObject);
            Set(view, "icon", icon);
            Set(view, "titleText", title);
            Set(view, "disciplesText", disc);
            Set(view, "foodIcon", food);
            Set(view, "foodText", foodText);
            Set(view, "hintText", hint);
            Set(view, "recruitButton", rec);
            Set(view, "recruitLabel", recLabel);
            Set(view, "recruitItemIcon", recIcon);
            Set(view, "reasonText", reason);
            Set(view, "closeButton", close);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/PavilionPanel.prefab");
        }

        static void BuildBuffPillPrefab()
        {
            var root = NewUi("BuffPill", null);
            Row(root.gameObject, 0, 0, 0);
            var view = root.gameObject.AddComponent<BuffPillView>();
            var pill = Chip(root, "Pill", ViewKit.Rgba(168, 85, 247, 0.18f), UiPalette.Purple);
            Row(pill.gameObject, 12, 6, 12);
            var bl = NewUi("Blessing", pill.rectTransform);
            Row(bl.gameObject, 0, 0, 4);
            var blIcon = Icon(bl, "Icon", Emoji("item_ember_pill"), 22);
            var blText = Label(bl, "Text", "Blessing 60s", 17f, UiPalette.Text, true);
            var vg = NewUi("Vigor", pill.rectTransform);
            Row(vg.gameObject, 0, 0, 4);
            var vgIcon = Icon(vg, "Icon", Emoji("item_vitality_pill"), 22);
            var vgText = Label(vg, "Text", "Martial Vigor 45s", 17f, UiPalette.Text, true);
            Set(view, "pill", pill.gameObject);
            Set(view, "blessing", bl.gameObject);
            Set(view, "blessingIcon", blIcon);
            Set(view, "blessingText", blText);
            Set(view, "vigor", vg.gameObject);
            Set(view, "vigorIcon", vgIcon);
            Set(view, "vigorText", vgText);
            pill.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/BuffPill.prefab");
        }

        static void BuildAutoSkipPrefab()
        {
            var root = Stretch(NewUi("AutoSkipChips", null));
            var view = root.gameObject.AddComponent<AutoSkipChipsView>();
            var chip = Chip(root, "ChipTemplate", ViewKit.Rgba(60, 40, 8, 0.92f), UiPalette.Amber);
            var crt = chip.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0f, 1f);
            Row(chip.gameObject, 9, 4, 4);
            Fit(chip.gameObject, true, true);
            Label(crt, "Label", "Skipping", 15f, UiPalette.Hex("#fde68a"), true);
            for (int i = 0; i < AutoSkipChipsView.MaxIcons; i++) Icon(crt, "Icon" + i, Emoji("item_wood"), 21);
            Set(view, "root", root);
            Set(view, "chipTemplate", crt);
            chip.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/AutoSkipChips.prefab");
        }

        // ------------------------------------------------------------------ scene wiring

        public static void InstallIntoActiveScene()
        {
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var hand = Object.FindFirstObjectByType<HandController>();
            var fx = Object.FindFirstObjectByType<FxService>();
            var bc = Object.FindFirstObjectByType<BuildController>();
            var cam = Object.FindFirstObjectByType<CameraController>();
            if (runner == null || hand == null || bc == null) { Debug.LogError("[IdleGrounds] Install M2-M4 first."); return; }

            var runtime = FindOrCreate("Runtime", null).transform;
            var enemiesRoot = FindOrCreate("Enemies", runtime);
            var es = Ensure<EnemyViewSync>(enemiesRoot);
            Set(es, "runner", runner);
            Set(es, "prefab", AssetDatabase.LoadAssetAtPath<EnemyView>(WorldPrefabDir + "/Enemy.prefab"));
            Set(es, "root", enemiesRoot.transform);
            Set(es, "fx", fx);

            var signsRoot = FindOrCreate("UnlockSigns", runtime);
            var us = Ensure<UnlockSignSync>(signsRoot);
            Set(us, "runner", runner);
            Set(us, "prefab", AssetDatabase.LoadAssetAtPath<UnlockSignView>(WorldPrefabDir + "/UnlockSign.prefab"));
            Set(us, "root", signsRoot.transform);
            Set(us, "fx", fx);
            Set(us, "hand", hand);
            Set(us, "cameraController", cam);
            Set(hand, "unlockSigns", us);

            // quest target ring (H2): Runtime/QuestRing — circle segments are created at runtime from the square sprite
            EnsureShapes();
            var ringGo = FindOrCreate("QuestRing", runtime);
            for (int i = ringGo.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(ringGo.transform.GetChild(i).gameObject);
            var ring = Ensure<QuestRingView>(ringGo);
            var ringFrame = Frame(ringGo.transform, "ZoneFrame", 5, "Overlay");
            ringFrame.gameObject.SetActive(false);
            Set(ring, "runner", runner);
            Set(ring, "segmentSprite", square);
            Set(ring, "zoneFrame", ringFrame);

            var canvas = GameObject.Find("UI/Canvas");
            if (canvas == null) { Debug.LogError("[IdleGrounds] UI/Canvas missing."); return; }
            foreach (var n in new[] { "AutoSkipChips", "QuestPanel", "PavilionPanel", "UpgradeTree", "DragonDialog" })
            {
                var old = canvas.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            GameObject Inst(string n, Transform parent) =>
                (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/" + n + ".prefab"), parent);
            var camera = cam != null ? cam.GetComponent<Camera>() : Camera.main;

            var skip = Inst("AutoSkipChips", canvas.transform).GetComponent<AutoSkipChipsView>();
            Set(skip, "runner", runner); Set(skip, "worldCamera", camera);
            skip.transform.SetAsFirstSibling();
            var quest = Inst("QuestPanel", canvas.transform).GetComponent<QuestPanelView>();
            Set(quest, "runner", runner); Set(quest, "fx", fx); Set(quest, "worldCamera", camera);
            var pav = Inst("PavilionPanel", canvas.transform).GetComponent<PavilionPanelView>();
            Set(pav, "runner", runner);

            // buff pill: inside the bottom bar, right after the hand pill
            var bar = Object.FindFirstObjectByType<BottomBarView>();
            if (bar != null)
            {
                var oldPill = bar.transform.Find("BuffPill");
                if (oldPill != null) Object.DestroyImmediate(oldPill.gameObject);
                var pillGo = Inst("BuffPill", bar.transform);
                var handPill = bar.transform.Find("HandPill");
                if (handPill != null) pillGo.transform.SetSiblingIndex(handPill.GetSiblingIndex() + 1);
                Set(pillGo.GetComponent<BuffPillView>(), "runner", runner);
                bar.transform.SetAsLastSibling();
            }

            var tree = Inst("UpgradeTree", canvas.transform).GetComponent<UpgradeTreeView>();
            Set(tree, "runner", runner);
            var dlg = Inst("DragonDialog", canvas.transform).GetComponent<DragonDialogView>();
            Set(dlg, "runner", runner);

            Set(bc, "upgradeTree", tree);
            Set(bc, "pavilionPanel", pav);
            Set(bc, "dragonDialog", dlg);

            var cursor = canvas.transform.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
