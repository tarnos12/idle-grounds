using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The one Help / Settings menu for desktop and phone (owner pick vp_bar, 2026-10-21: icon-only bar buttons, "Reset"
    /// moves into a Help/Settings menu). Opened by the bar's Menu button, by Esc / Android Back when nothing else is open,
    /// and closed by Esc / Back / Resume / the Close button. Rows: Resume (primary), Help, Stats, Sound, Motion, Keep awake
    /// (phone only), Settings, New game (asks first) and Save and quit (not in WebGL or the editor). Built at runtime on the
    /// root canvas as a centred jade modal with the flat scrim, so the scene stays untouched.
    /// <para>Settings is a second page of the same box (title "Settings", a Back arrow left of the title; Esc / Back returns
    /// to the menu page first): "Text font" (owner pick font_read = "setting", 2026-10-22) with six options, each labelled in its own
    /// face; picking one applies at once (<see cref="UiFont.SetChoice"/>) and is kept in PlayerPrefs, not the game save.</para>
    /// </summary>
    public class GameMenuView : MonoBehaviour
    {
        public static GameMenuView Instance { get; private set; }

        RectTransform root, box;
        TextMeshProUGUI soundLabel, motionLabel, keepAwakeLabel;
        GameObject keepAwakeRow, quitRow;
        TMP_FontAsset font;
        Image closeImg;
        UiButtonSkin closeSkin;
        GameObject closeX, backButton;
        TextMeshProUGUI titleText, inUseLabel, sampleLabel;
        RectTransform menuPage, settingsPage, fontGrid, menuGrid;
        readonly List<(TextMeshProUGUI label, bool icon)> menuLabels = new List<(TextMeshProUGUI, bool)>();
        readonly List<(TextFontOption opt, Button button, UiButtonSkin skin, TextMeshProUGUI label)> fontRows = new List<(TextFontOption, Button, UiButtonSkin, TextMeshProUGUI)>();

        /// <summary>The Settings page is showing (inside the open menu).</summary>
        public bool SettingsOpen => IsOpen && settingsPage != null && settingsPage.gameObject.activeSelf;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        /// <summary>The menu on the scene's UI canvas (built on first use). Null when there is no UI canvas.</summary>
        public static GameMenuView Ensure()
        {
            if (Instance != null) return Instance;
            var cvGo = GameObject.Find("UI/Canvas");
            var canvas = cvGo != null ? cvGo.GetComponent<Canvas>() : null;
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            var go = new GameObject("GameMenu", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var v = go.AddComponent<GameMenuView>();
            v.Build();
            Instance = v;
            return v;
        }

        public static void Toggle()
        {
            var m = Ensure();
            if (m == null) return;
            if (m.IsOpen) m.Close(); else m.Open();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Open()
        {
            if (root == null) return;
            Tooltip.DismissAll();
            RefreshLabels();
            bool art = closeSkin != null && closeSkin.HasArt;      // the close art carries its own colours; the x is the no-art fallback
            if (closeImg != null) closeImg.color = art ? Color.white : UiPalette.Panel;
            if (closeX != null) closeX.SetActive(!art);
            if (keepAwakeRow != null) keepAwakeRow.SetActive(MobileUi.IsMobile);
            root.gameObject.SetActive(true);
            ShowPage(false);
            root.SetAsLastSibling();
            var cursor = root.parent != null ? root.parent.Find("HandCursor") : null;
            if (cursor != null) cursor.SetAsLastSibling();
            Fit();
            AudioService.Play("click");
        }

        public void Close() { if (root != null) root.gameObject.SetActive(false); }

        /// <summary>Esc / Back: true when the menu was open (Settings goes back to the menu page; the menu page closes).</summary>
        public bool Escape()
        {
            if (!IsOpen) return false;
            if (SettingsOpen) { ShowPage(false); AudioService.Play("click"); return true; }
            Close();
            return true;
        }

        /// <summary>Open the menu on its Settings page.</summary>
        public void OpenSettings()
        {
            if (!IsOpen) Open();
            ShowPage(true);
        }

        /// <summary>Menu page (false) or Settings page (true); refits the box.</summary>
        void ShowPage(bool settings)
        {
            if (menuPage == null || settingsPage == null) return;
            Tooltip.DismissAll();
            menuPage.gameObject.SetActive(!settings);
            settingsPage.gameObject.SetActive(settings);
            if (titleText != null) titleText.text = settings ? "Settings" : "Menu";
            if (backButton != null) backButton.SetActive(settings);
            if (settings) RefreshFonts(); else FitMenuGrid();
            Fit();
        }

        /// <summary>
        /// Menu cells as wide as the widest label at the snapped size of the chosen face (+ icon): labels never auto-size
        /// under the text floor or wrap (720p and wide faces such as Press Start 2P; Text font setting).
        /// </summary>
        void FitMenuGrid()
        {
            if (menuGrid == null || !menuGrid.TryGetComponent<GridLayoutGroup>(out var gl)) return;
            float w = 250f, h = 64f;
            foreach (var (label, icon) in menuLabels)
            {
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                string[] alts = label == soundLabel ? new[] { "Sound: On", "Sound: Off" } : label == motionLabel ? new[] { "Motion: On", "Motion: Calm" }
                    : label == keepAwakeLabel ? new[] { "Keep awake: On", "Keep awake: Off" } : new[] { label.text };
                w = Mathf.Max(w, UiFit.WidestText(label, alts) + (icon ? 42f : 0f) + 40f);
                h = Mathf.Max(h, Mathf.Ceil(label.GetPreferredValues("Ag", 9999f, 9999f).y + 20f));
            }
            gl.cellSize = new Vector2(Mathf.Ceil(w), h);
        }

        /// <summary>Selected state, the "In use" line and the option cell width (the widest label in its own face).</summary>
        void RefreshFonts()
        {
            var cur = UiFont.Choice;
            foreach (var r in fontRows)
            {
                bool on = r.opt == cur;
                r.button.interactable = UiFont.Available(r.opt.body) && UiFont.Available(r.opt.title);
                if (r.skin != null) r.skin.SetToggle(on ? UiToggleState.On : UiToggleState.Off);
            }
            if (inUseLabel != null) inUseLabel.text = "In use: " + cur.label + (cur.id == TextFonts.DefaultId ? " (default)" : "");
            if (fontGrid != null && fontGrid.TryGetComponent<GridLayoutGroup>(out var gl))
            {
                float w = 250f, h = 64f;
                foreach (var r in fontRows)
                {
                    w = Mathf.Max(w, UiFit.WidestText(r.label, r.label.text) + 40f);
                    h = Mathf.Max(h, Mathf.Ceil(r.label.GetPreferredValues("Ag", 9999f, 9999f).y + 20f));
                }
                gl.cellSize = new Vector2(Mathf.Ceil(w), h);
            }
        }

        void PickFont(TextFontOption o)
        {
            UiFont.SetChoice(o.id);
            AudioService.Play("click");
            RefreshFonts();
            Fit();
        }

        void RefreshLabels()
        {
            if (keepAwakeLabel != null) keepAwakeLabel.text = "Keep awake: " + (MobileLayer.KeepAwake ? "On" : "Off");
            var a = AudioService.Instance;
            if (soundLabel != null) soundLabel.text = "Sound: " + (a != null && a.Muted ? "Off" : "On");
            if (motionLabel != null) motionLabel.text = "Motion: " + (Juice.ReduceMotion ? "Calm" : "On");
        }

        /// <summary>The box shrinks (never grows) to fit between the bottom bar and the top of the safe area.</summary>
        void Fit()
        {
            if (box == null) return;
            var canvasRect = (RectTransform)root.parent;
            box.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
            var bar = BottomBarView.Instance;
            float barH = bar != null && bar.isActiveAndEnabled ? ((RectTransform)bar.transform).rect.height : 0f;
            var ins = MobileUi.IsMobile ? MobileUi.SafeInsets() : Vector4.zero;
            float cs = Mathf.Max(0.01f, (canvasRect.GetComponentInParent<Canvas>()?.scaleFactor) ?? 1f);
            float bottom = barH + ins.y / cs + 10f, top = ins.w / cs + 16f;
            float availH = canvasRect.rect.height - bottom - top;
            box.anchoredPosition = new Vector2(0f, Mathf.Round((bottom - top) * 0.5f));
            float availW = canvasRect.rect.width - 24f - (ins.x + ins.z) / cs;
            float h = Mathf.Max(1f, box.rect.height), w = Mathf.Max(1f, box.rect.width);
            float k = Mathf.Min(1f, availH / h, availW / w);
            box.localScale = new Vector3(k, k, 1f);
        }

        // ================================================================ build

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        TextMeshProUGUI NewText(Transform parent, string text, float size, Color color)
        {
            var rt = NewRect("Label", parent);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = color; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        Button NewButton(Transform parent, string name, string label, UnityAction onClick, out TextMeshProUGUI text, UiButtonVariant variant, Sprite icon = null)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UiPalette.Panel;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            UiButtonSkin.Attach(rt.gameObject, variant);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 6, 6); h.spacing = 10f; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            if (icon != null)
            {
                var ir = NewRect("Icon", rt);
                var ii = ir.gameObject.AddComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
                var ile = ir.gameObject.AddComponent<LayoutElement>(); ile.preferredWidth = ile.minWidth = 32f; ile.preferredHeight = ile.minHeight = 32f;
            }
            text = NewText(rt, label, 22f, UiPalette.Text);
            text.fontStyle = FontStyles.Bold;
            return b;
        }

        void Build()
        {
            var tmp = FindFirstObjectByType<TextMeshProUGUI>();
            font = UiFont.Pixel != null ? UiFont.Pixel : tmp != null ? tmp.font : null;
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.45f);     // the flat modal dim (VP-05)
            scrim.raycastTarget = true;
            root.gameObject.AddComponent<ModalScrim>();

            box = NewRect("Box", root);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            var bi = box.gameObject.AddComponent<Image>(); bi.color = UiPalette.Bg2; bi.raycastTarget = true;
            var vl = box.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(24, 24, 18, 22); vl.spacing = 12f;
            vl.childAlignment = TextAnchor.UpperCenter; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiSkin.Attach(box.gameObject, "ui_panel_jade");       // jade like every modal (applies the frame padding)

            // title bar: "Menu" + Close (x) top-right, like every panel
            var head = NewRect("TitleBar", box);
            var hh = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hh.childAlignment = TextAnchor.MiddleCenter; hh.childControlWidth = true; hh.childControlHeight = true; hh.childForceExpandWidth = false; hh.childForceExpandHeight = false;
            var hle = head.gameObject.AddComponent<LayoutElement>(); hle.minHeight = hle.preferredHeight = 46f;
            var pad = NewRect("Pad", head); var ple = pad.gameObject.AddComponent<LayoutElement>(); ple.preferredWidth = ple.minWidth = 44f; ple.preferredHeight = 40f;
            // Settings page: Back sits where the menu page has an empty pad, mirroring Close (keeps the page short enough for phones)
            var back = NewRect("Back", pad);
            back.anchorMin = back.anchorMax = new Vector2(0f, 0.5f); back.pivot = new Vector2(0f, 0.5f); back.sizeDelta = new Vector2(40f, 40f);
            var bimg = back.gameObject.AddComponent<Image>(); bimg.color = UiPalette.Panel;
            var bb = back.gameObject.AddComponent<Button>(); bb.targetGraphic = bimg; bb.onClick.AddListener(() => { ShowPage(false); AudioService.Play("click"); });
            UiButtonSkin.Attach(back.gameObject, UiButtonVariant.Standard);
            var bl = NewText(back, "<", 22f, UiPalette.Text); bl.textWrappingMode = TextWrappingModes.NoWrap;
            TooltipTrigger.Attach(bb, "Back to the menu [Esc]");
            backButton = back.gameObject;
            backButton.SetActive(false);
            var title = NewText(head, "Menu", 30f, UiPalette.Gold);
            title.fontStyle = FontStyles.Bold; title.textWrappingMode = TextWrappingModes.NoWrap;
            UiTextRole.Set(title, TextRole.Title);
            titleText = title;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var close = NewRect("Close", head);
            var ci = close.gameObject.AddComponent<Image>(); ci.color = UiPalette.Panel;
            var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = ci; cb.onClick.AddListener(Close);
            var cle = close.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = cle.minWidth = 40f; cle.preferredHeight = cle.minHeight = 40f;
            closeSkin = UiButtonSkin.Attach(close.gameObject, UiButtonVariant.Close);
            closeImg = ci;
            closeX = NewText(close, "x", 24f, UiPalette.Text).gameObject;
            TooltipTrigger.Attach(cb, "Close [Esc]");

            menuPage = NewRect("MenuPage", box);
            var mv = menuPage.gameObject.AddComponent<VerticalLayoutGroup>();
            mv.childAlignment = TextAnchor.UpperCenter; mv.childControlWidth = true; mv.childControlHeight = true; mv.childForceExpandWidth = true; mv.childForceExpandHeight = false;
            var grid = NewRect("Grid", menuPage);
            menuGrid = grid;
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250f, 64f); gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;

            void Row(string label, UnityAction act, out TextMeshProUGUI t, UiButtonVariant v = UiButtonVariant.Standard, string icon = null, string tip = null)
            {
                var b = NewButton(grid, label, label, act, out t, v, icon != null ? UiIcons.Get(icon) : null);
                // body size, snapped to the face's grid; the cells grow to the widest label (FitMenuGrid) instead of auto-sizing
                t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
                t.fontStyle = FontStyles.Normal;
                menuLabels.Add((t, icon != null));
                if (tip != null) TooltipTrigger.Attach(b, tip);
            }
            Row("Resume", Close, out _, UiButtonVariant.Primary);
            Row("Help", () => { Close(); var m = MetaUiController.Instance; if (m != null) m.Help.Open(); }, out _, icon: UiIcons.Help, tip: "How to play");
            Row("Stats", () => { Close(); var m = MetaUiController.Instance; if (m != null) { m.Stats.Show(false, false); m.Stats.Open(); } }, out _, icon: "ui_stats", tip: "Your progress so far");
            Row("Sound", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMute(); RefreshLabels(); }, out soundLabel, icon: UiIcons.Sound, tip: "Mute / unmute sound");
            Row("Motion", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMotion(); RefreshLabels(); }, out motionLabel, icon: UiIcons.Motion, tip: "Reduce motion: no screen shake, squash or flight tweens, no ambient motes");
            Row("Keep awake", () => { MobileLayer.KeepAwake = !MobileLayer.KeepAwake; RefreshLabels(); }, out keepAwakeLabel, tip: "Keep the screen on while the game runs");
            keepAwakeRow = keepAwakeLabel.transform.parent.gameObject;
            Row("Settings", () => ShowPage(true), out _, tip: "Settings: text font");
            Row("New game", () =>
            {
                Close();
                var m = MetaUiController.Instance;
                if (m != null) m.Confirm.Ask(MetaBarView.NewGameTitle, MetaBarView.ResetText, MetaBarView.NewGameButton, () => { if (SaveService.Instance != null) SaveService.Instance.ResetAll(); });
            }, out var ng, icon: UiIcons.Reset, tip: "New game: wipe the save and start again with no buildings placed (asks first)");
            ng.color = UiPalette.Danger;
            Row("Save and quit", () => { if (SaveService.Instance != null) SaveService.Instance.Save(); Application.Quit(); }, out var sq, tip: "Save now and close the game");
            quitRow = sq.transform.parent.gameObject;
            quitRow.SetActive(!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer);
            BuildSettings();
            root.gameObject.SetActive(false);
        }

        /// <summary>The Settings page: "Text font" options (3 x 2, each in its own face), the option in use, a sample line in
        /// the chosen fonts, where the choice is kept, and Back (primary).</summary>
        void BuildSettings()
        {
            settingsPage = NewRect("SettingsPage", box);
            var sv = settingsPage.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 10f; sv.childAlignment = TextAnchor.UpperCenter;
            sv.childControlWidth = true; sv.childControlHeight = true; sv.childForceExpandWidth = true; sv.childForceExpandHeight = false;

            TextMeshProUGUI Line(string name, string text, Color color, TextRole role)
            {
                var r = NewRect(name, settingsPage);
                var f = r.gameObject.AddComponent<VerticalLayoutGroup>(); f.childControlWidth = true; f.childControlHeight = true; f.childForceExpandWidth = true; f.childForceExpandHeight = false;
                f.padding = new RectOffset(16, 16, 0, 0);
                // the lines take the width of the option grid and wrap inside it (a wide face made the note the widest item, so it ran to the frame)
                var wle = r.gameObject.AddComponent<LayoutElement>(); wle.minWidth = 0f; wle.preferredWidth = 0f; wle.flexibleWidth = 1f;
                var t = NewText(r, text, 22f, color);
                UiTextRole.Set(t, role);
                return t;
            }

            var head = Line("FontHead", "Text font", UiPalette.Gold, TextRole.Title);
            head.alignment = TextAlignmentOptions.Left;

            fontGrid = NewRect("FontGrid", settingsPage);
            var gl = fontGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250f, 64f); gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperCenter;
            fontRows.Clear();
            foreach (var o in TextFonts.Options)
            {
                var opt = o;
                var b = NewButton(fontGrid, "Font_" + o.id, o.label, () => PickFont(opt), out var t, UiButtonVariant.Standard);
                t.fontStyle = FontStyles.Normal;
                t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
                UiTextRole.Set(t, TextRole.Body, o.body.id);       // each option drawn in its own (body) face: the preview
                TooltipTrigger.Attach(b, o.tip);
                fontRows.Add((o, b, b.GetComponent<UiButtonSkin>(), t));
            }

            inUseLabel = Line("InUse", "In use:", UiPalette.Text, TextRole.Body);
            sampleLabel = Line("Sample", TextFonts.Sample, UiPalette.Gold, TextRole.Body);
            Line("Note", "Changes at once. Kept on this device, not in your game save.", UiPalette.MutedSoft, TextRole.Body);

            settingsPage.gameObject.SetActive(false);
        }
    }
}
