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
    /// (phone only), New game (asks first) and Save and quit (not in WebGL or the editor). Built at runtime on the root
    /// canvas as a centred jade modal with the flat scrim, so the scene stays untouched.
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
        GameObject closeX;

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
            root.SetAsLastSibling();
            var cursor = root.parent != null ? root.parent.Find("HandCursor") : null;
            if (cursor != null) cursor.SetAsLastSibling();
            Fit();
            AudioService.Play("click");
        }

        public void Close() { if (root != null) root.gameObject.SetActive(false); }

        /// <summary>Esc / Back: true when the menu was open (and is now closed).</summary>
        public bool Escape()
        {
            if (!IsOpen) return false;
            Close();
            return true;
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
            var pad = NewRect("Pad", head); var ple = pad.gameObject.AddComponent<LayoutElement>(); ple.preferredWidth = 44f;
            var title = NewText(head, "Menu", 30f, UiPalette.Gold);
            title.fontStyle = FontStyles.Bold; title.textWrappingMode = TextWrappingModes.NoWrap;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var close = NewRect("Close", head);
            var ci = close.gameObject.AddComponent<Image>(); ci.color = UiPalette.Panel;
            var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = ci; cb.onClick.AddListener(Close);
            var cle = close.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = cle.minWidth = 40f; cle.preferredHeight = cle.minHeight = 40f;
            closeSkin = UiButtonSkin.Attach(close.gameObject, UiButtonVariant.Close);
            closeImg = ci;
            closeX = NewText(close, "x", 24f, UiPalette.Text).gameObject;
            TooltipTrigger.Attach(cb, "Close (Esc)");

            var grid = NewRect("Grid", box);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250f, 64f); gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;

            void Row(string label, UnityAction act, out TextMeshProUGUI t, UiButtonVariant v = UiButtonVariant.Standard, string icon = null, string tip = null)
            {
                var b = NewButton(grid, label, label, act, out t, v, icon != null ? UiIcons.Get(icon) : null);
                t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 22f;
                if (tip != null) TooltipTrigger.Attach(b, tip);
            }
            Row("Resume", Close, out _, UiButtonVariant.Primary);
            Row("Help", () => { Close(); var m = MetaUiController.Instance; if (m != null) m.Help.Open(); }, out _, icon: UiIcons.Help, tip: "How to play");
            Row("Stats", () => { Close(); var m = MetaUiController.Instance; if (m != null) { m.Stats.Show(false, false); m.Stats.Open(); } }, out _, icon: "ui_stats", tip: "Your progress so far");
            Row("Sound", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMute(); RefreshLabels(); }, out soundLabel, icon: UiIcons.Sound, tip: "Mute / unmute sound");
            Row("Motion", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMotion(); RefreshLabels(); }, out motionLabel, icon: UiIcons.Motion, tip: "Reduce motion: no screen shake, squash or flight tweens, no ambient motes");
            Row("Keep awake", () => { MobileLayer.KeepAwake = !MobileLayer.KeepAwake; RefreshLabels(); }, out keepAwakeLabel, tip: "Keep the screen on while the game runs");
            keepAwakeRow = keepAwakeLabel.transform.parent.gameObject;
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
            root.gameObject.SetActive(false);
        }
    }
}
