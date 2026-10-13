using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Everything the phone build adds on top of the desktop HUD, created at runtime so the scene is untouched:
    /// touch gestures (<see cref="TouchInputController"/>), the canvas scale for a small physical screen, safe-area insets,
    /// 48 dp finger targets, the on-screen hand buttons (Swap = Q/E, Give = right-click mode), the Place / Demolish / Cancel
    /// bar for build aiming, the Back-button pause menu, frame-rate / orientation / sleep settings and a collapsed quest panel.
    /// On desktop only the gesture controller exists (and does nothing without touches).
    /// </summary>
    public class MobileLayer : MonoBehaviour
    {
        public static MobileLayer Instance { get; private set; }

        const string KeepAwakePref = "ig.keepAwake";

        // ---- runtime pieces (rebuilt for every loaded scene)
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform canvasRect, safe, hud, aimBar, pausePanel;
        Button swapBtn, giveBtn, aimConfirm, aimCancel;
        Image giveImg;
        TextMeshProUGUI giveLabel, aimLabel, aimConfirmLabel, keepAwakeLabel;
        TMP_FontAsset font;
        GameRunner runner;
        BuildController build;
        TouchInputController touch;
        bool built, dirty = true, appSettingsDone, questHandled, lastGive;
        const string SwapLabel = "<sprite name=\"glyph_redo\">\nSwap";
        static string GiveLabel(bool on) => "<sprite name=\"ui_hand\">\n" + (on ? "Give ON" : "Give");
        static float HandButtonSize => 66f;
        Vector4 lastInsets = new Vector4(-1, -1, -1, -1);
        float lastScaleKey, nextHitScan;
        int setupCountdown;

        public bool PauseOpen => pausePanel != null && pausePanel.gameObject.activeSelf;
        public bool Built => built;
        public RectTransform SafeArea => safe;
        public RectTransform Hud => hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        /// <summary>The persistent touch layer (creates it once).</summary>
        public static MobileLayer Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MobileLayer");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchInputController>();
            return go.AddComponent<MobileLayer>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            touch = GetComponent<TouchInputController>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            TMPro.TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        /// <summary>Touch wording for every text the game shows (see <see cref="ControlWords"/>).</summary>
        static void OnTextChanged(Object o)
        {
            if (!MobileUi.IsMobile || !(o is TMP_Text tmp)) return;
            string s = tmp.text, r = ControlWords.Rewrite(s);
            if (s != r) tmp.text = r;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TMPro.TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            if (Instance == this) Instance = null;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) { built = false; dirty = true; setupCountdown = 3; }

        /// <summary>Re-apply scale / safe area / HUD (after <see cref="MobileUi.Force"/>).</summary>
        public void Refresh() { dirty = true; lastInsets = new Vector4(-1, -1, -1, -1); lastScaleKey = 0; }

        /// <summary>Android Back with no panel open: toggle the pause menu (no-op on desktop).</summary>
        public static void OnBackWithNothingOpen()
        {
            if (!MobileUi.IsMobile || Instance == null) return;
            if (Instance.PauseOpen) Instance.ClosePause(); else Instance.OpenPause();
        }

        // ================================================================ lifecycle

        void Update()
        {
            if (!MobileUi.IsMobile) return;
            if (!appSettingsDone) ApplyAppSettings();
            if (setupCountdown > 0) { setupCountdown--; return; }     // let the scene's own Start()s (modal order) run first
            if (!built || canvas == null)
            {
                if (!TryBuild()) return;
            }
            float key = MobileUi.RefHeight() + MobileUi.ScreenH * 7f + MobileUi.ScreenW * 13f;
            if (dirty || key != lastScaleKey) { ApplyScale(); StyleBar(); lastScaleKey = key; dirty = false; }
            var ins = MobileUi.SafeInsets();
            if (ins != lastInsets) { ApplySafeArea(ins); lastInsets = ins; }
            if (Time.unscaledTime >= nextHitScan) { nextHitScan = Time.unscaledTime + 0.75f; ExpandHitAreas(); FitModals(); }
            SyncHud();
            if (!questHandled) HandleQuestPanel();
        }

        void ApplyAppSettings()
        {
            appSettingsDone = true;
            Application.targetFrameRate = 60;            // phones default to 30; 60 is smooth and still battery-friendly
            QualitySettings.vSyncCount = 0;              // targetFrameRate is ignored while vSync is on
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
            ApplyKeepAwake();
        }

        public static bool KeepAwake
        {
            get { try { return PlayerPrefs.GetInt(KeepAwakePref, 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(KeepAwakePref, value ? 1 : 0); PlayerPrefs.Save(); } catch { /* ignore */ } if (Instance != null) Instance.ApplyKeepAwake(); }
        }

        void ApplyKeepAwake() => Screen.sleepTimeout = KeepAwake ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

        bool TryBuild()
        {
            var cvGo = GameObject.Find("UI/Canvas");
            canvas = cvGo != null ? cvGo.GetComponent<Canvas>() : null;
            runner = GameRunner.Instance;
            build = FindFirstObjectByType<BuildController>();
            if (canvas == null || runner == null || runner.Sim == null) return false;
            canvas = canvas.rootCanvas;
            canvasRect = (RectTransform)canvas.transform;
            scaler = canvas.GetComponent<CanvasScaler>();
            var tmp = FindFirstObjectByType<TextMeshProUGUI>();
            font = tmp != null ? tmp.font : null;

            // 1. a safe-area root under the canvas holding the whole HUD (modals' scrims still bleed to the screen edge)
            var existing = canvasRect.Find("SafeArea");
            if (existing != null) Destroy(existing.gameObject);
            safe = NewRect("SafeArea", canvasRect);
            Stretch(safe);
            int at = int.MaxValue;
            var move = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < canvasRect.childCount; i++)
            {
                var c = canvasRect.GetChild(i);
                if (c == safe || c.name == "HandCursor" || c.name == "Tooltip") continue;
                move.Add(c);
                at = Mathf.Min(at, i);
            }
            foreach (var c in move) c.SetParent(safe, false);
            safe.SetSiblingIndex(at == int.MaxValue ? 0 : at);
            if (EventSystem.current != null) EventSystem.current.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(MobileUi.DpToPx(12f)));

            // the version badge crowds the title on a narrow phone canvas
            var bar = FindFirstObjectByType<BottomBarView>();
            var vf = typeof(BottomBarView).GetField("versionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (bar != null && vf != null && vf.GetValue(bar) is TextMeshProUGUI vt)
            {
                var vp = vt.transform.parent;      // the badge chip holds the text; on the bar itself hide the text
                (vp != null && vp != bar.transform && vp.GetComponent<Image>() != null ? vp.gameObject : vt.gameObject).SetActive(false);
            }

            // 2. on-screen buttons
            BuildHud();
            lastInsets = new Vector4(-1, -1, -1, -1);
            built = true; dirty = true; questHandled = false;
            return true;
        }

        // ================================================================ scale + safe area + finger targets

        void ApplyScale()
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, MobileUi.RefHeight());
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;                    // height decides: the phone shows the same UI height in any aspect
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Compact phone bottom bar: the logo, the area pill, the hand pill and the frequent buttons (Stats, Build, Demolish) plus a Menu
        /// button; the title, version, Help, Sound, Motion and Reset move into the Menu. Every button is at least 48 dp tall and the docks
        /// above the bar move up with it.
        /// </summary>
        void StyleBar()
        {
            var bar = FindFirstObjectByType<BottomBarView>();
            if (bar == null || canvas == null) return;
            var bt = (RectTransform)bar.transform;
            float f = Mathf.Max(0.01f, MobileUi.ScreenH / MobileUi.RefHeight());   // px per canvas unit (canvas.scaleFactor lags a frame)
            float btnH = Mathf.Max(40f, Mathf.Min(Mathf.Ceil(MobileUi.DpToPx(MobileUi.MinTargetDp) / f) + 2f, 0.125f * MobileUi.RefHeight()));   // 48 dp, but never more than 1/8 of the screen (tiny / odd densities)
            var menu = bt.Find("MenuButton");
            if (menu == null)
            {
                var help = bt.Find("HelpButton");
                if (help != null)
                {
                    var go = Instantiate(help.gameObject, bt);
                    go.name = "MenuButton";
                    go.SetActive(true);
                    var mb = go.GetComponent<Button>();
                    mb.onClick = new Button.ButtonClickedEvent();
                    mb.onClick.AddListener(() => { if (PauseOpen) ClosePause(); else OpenPause(); });
                    var lbl = go.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (lbl != null) lbl.text = "Menu";
                    var tt = go.GetComponent<TooltipTrigger>(); if (tt != null) Destroy(tt);
                    TooltipTrigger.Attach(mb, "Menu: help, stats, sound, new game");
                    go.transform.SetAsLastSibling();
                    menu = go.transform;
                }
            }
            foreach (var n in new[] { "Title", "VersionBadge", "MuteButton", "MotionButton", "HelpButton", "ResetButton" })
            {
                var c = bt.Find(n);
                if (c != null && c.gameObject.activeSelf) c.gameObject.SetActive(false);
            }
            var area = bt.Find("AreaPill");
            if (area != null)
            {
                var le = area.GetComponent<LayoutElement>(); if (le != null) le.preferredWidth = 180f;
                var rn = area.Find("RegionName")?.GetComponent<TextMeshProUGUI>();
                if (rn != null) { rn.textWrappingMode = TextWrappingModes.NoWrap; rn.overflowMode = TextOverflowModes.Ellipsis; rn.enableAutoSizing = true; rn.fontSizeMin = 13f; rn.fontSizeMax = 19f; }
            }
            foreach (var b in bt.GetComponentsInChildren<Button>(false))
            {
                var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = btnH;
                var lt = b.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (lt != null) lt.fontSize = 20f;
            }
            float barH = btnH + 16f;
            if (!Mathf.Approximately(bt.sizeDelta.y, barH)) bt.sizeDelta = new Vector2(bt.sizeDelta.x, barH);
            foreach (var d in FindObjectsByType<UiDock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rt = (RectTransform)d.transform;
                if (rt.anchorMin.y == 0f && rt.anchorMax.y == 0f) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, barH + 8f);
            }
            LayoutRebuilder.MarkLayoutForRebuild(bt);
        }

        void ApplySafeArea(Vector4 insetsPx)
        {
            if (safe == null || canvas == null) return;
            float f = Mathf.Max(0.01f, canvas.scaleFactor);
            var l = new Vector2(insetsPx.x / f, insetsPx.y / f);
            var r = new Vector2(insetsPx.z / f, insetsPx.w / f);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = l; safe.offsetMax = -r;
            // scrims dim the whole screen, including the notch strips
            foreach (var s in safe.GetComponentsInChildren<ModalScrim>(true))
            {
                var rt = (RectTransform)s.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = -l; rt.offsetMax = r;
            }
        }

        void ExpandHitAreas()
        {
            if (canvas == null) return;
            float minPx = MobileUi.DpToPx(MobileUi.MinTargetDp);
            foreach (var sel in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var g = sel.targetGraphic;
                if (g == null || !g.raycastTarget) continue;
                var rt = g.rectTransform;
                var sc = rt.lossyScale;
                if (sc.x <= 0.0001f || sc.y <= 0.0001f) continue;
                float wpx = rt.rect.width * sc.x, hpx = rt.rect.height * sc.y;
                float px = Mathf.Max(0f, (minPx - wpx) * 0.5f) / sc.x;
                float py = Mathf.Max(0f, (minPx - hpx) * 0.5f) / sc.y;
                // a scrollbar / slider handle the user drags is fine as is; list rows & cards are already big
                var want = new Vector4(-px, -py, -px, -py);
                if (g.raycastPadding != want) g.raycastPadding = want;
            }
        }

        /// <summary>Modal boxes taller / wider than the phone canvas shrink to fit (they scroll inside, so the text stays readable).</summary>
        void FitModals()
        {
            if (canvasRect == null) return;
            float cw = canvasRect.rect.width, ch = canvasRect.rect.height;
            foreach (var s in FindObjectsByType<ModalScrim>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var box = s.transform.parent != null ? s.transform.parent.Find("Box") as RectTransform : null;
                if (box == null) continue;
                var ls = box.localScale;
                box.localScale = Vector3.one;
                float w = box.rect.width, h = box.rect.height;
                float k = Mathf.Min(1f, 0.92f * ch / Mathf.Max(1f, h), 0.94f * cw / Mathf.Max(1f, w));
                box.localScale = new Vector3(k, k, 1f);
            }
        }

        // ================================================================ quest panel

        void HandleQuestPanel()
        {
            var q = FindFirstObjectByType<QuestPanelView>();
            if (q == null || runner == null || runner.Sim == null) return;
            questHandled = true;
            // the expanded quest panel eats a big slice of a phone screen: start every session collapsed (tap the chip to expand)
            runner.Sim.SetQuestPanelHidden(true);
            q.transform.localScale = new Vector3(0.85f, 0.85f, 1f);      // expanded, it still leaves most of the view
        }

        // ================================================================ HUD buttons

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI NewText(Transform parent, string text, float size, TextAlignmentOptions align, Color color)
        {
            var rt = NewRect("Text", parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        Button NewButton(Transform parent, string name, string label, Vector2 size, Color bg, UnityAction onClick, out TextMeshProUGUI text, out Image img, UiButtonVariant variant = UiButtonVariant.Standard, float fontSize = 26f)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = UiPalette.Line; ol.effectDistance = new Vector2(2f, -2f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            UiButtonSkin.Attach(rt.gameObject, variant);        // the game's button art (falls back to the flat placeholder without it)
            text = NewText(rt, label, fontSize, TextAlignmentOptions.Center, UiPalette.Text);
            text.fontStyle = FontStyles.Bold;
            return b;
        }

        void BuildHud()
        {
            if (hud != null) Destroy(hud.gameObject);
            hud = NewRect("TouchHud", safe);
            Stretch(hud);

            // left edge, lower half: Swap (Q/E) and Give (right-click mode). The quest panel lives on the right, the world centre stays free
            var col = NewRect("HandButtons", hud);
            col.anchorMin = col.anchorMax = new Vector2(0f, 0.34f);
            col.pivot = new Vector2(0f, 0.5f);
            col.anchoredPosition = new Vector2(12f, 0f);
            float bs = HandButtonSize;
            col.sizeDelta = new Vector2(bs, bs * 2f + 12f);
            swapBtn = NewButton(col, "Swap", SwapLabel, new Vector2(bs, bs), UiPalette.Panel, OnSwap, out _, out _, UiButtonVariant.Standard, 17f);
            var sr = (RectTransform)swapBtn.transform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f); sr.pivot = new Vector2(0.5f, 1f); sr.anchoredPosition = Vector2.zero;
            giveBtn = NewButton(col, "Give", GiveLabel(false), new Vector2(bs, bs), UiPalette.Panel, OnGive, out giveLabel, out giveImg, UiButtonVariant.Standard, 17f);
            var gr = (RectTransform)giveBtn.transform; gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0f); gr.pivot = new Vector2(0.5f, 0f); gr.anchoredPosition = Vector2.zero;
            foreach (var hb in new[] { swapBtn, giveBtn }) { var tx = hb.GetComponentInChildren<TextMeshProUGUI>(); if (tx != null) { tx.textWrappingMode = TextWrappingModes.NoWrap; tx.lineSpacing = -12f; } }
            TooltipTrigger.Attach(swapBtn, "Swap the hand stacks: the front stack goes to the back");
            TooltipTrigger.Attach(giveBtn, "Give mode: every touch gives. Tap drops or feeds one item, hold keeps feeding. (Or hold on a building with items in hand.)");

            // bottom centre, above the bar: Place / Demolish + Cancel and the reason line
            aimBar = NewRect("AimBar", hud);
            aimBar.anchorMin = aimBar.anchorMax = new Vector2(0.5f, 0f);
            aimBar.pivot = new Vector2(0.5f, 0f);
            aimBar.anchoredPosition = new Vector2(0f, 96f);
            aimBar.sizeDelta = new Vector2(520f, 150f);
            aimLabel = NewText(aimBar, "", 24f, TextAlignmentOptions.Center, UiPalette.Text);
            var lr = aimLabel.rectTransform;
            var lbgRt = NewRect("LabelBg", aimBar);
            var lbg = lbgRt.gameObject.AddComponent<Image>(); lbg.color = new Color(UiPalette.Bg.r, UiPalette.Bg.g, UiPalette.Bg.b, 0.85f); lbg.raycastTarget = false;
            lbgRt.anchorMin = new Vector2(0f, 1f); lbgRt.anchorMax = new Vector2(1f, 1f); lbgRt.pivot = new Vector2(0.5f, 1f); lbgRt.anchoredPosition = Vector2.zero; lbgRt.sizeDelta = new Vector2(0f, 40f);
            lbgRt.SetAsFirstSibling(); lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f); lr.anchoredPosition = Vector2.zero; lr.sizeDelta = new Vector2(0f, 40f);
            aimConfirm = NewButton(aimBar, "Confirm", "Place", new Vector2(300f, 84f), UiPalette.AccentDk, OnAimConfirm, out aimConfirmLabel, out _, UiButtonVariant.Primary);
            var cr = (RectTransform)aimConfirm.transform; cr.anchorMin = cr.anchorMax = new Vector2(0f, 0f); cr.pivot = new Vector2(0f, 0f); cr.anchoredPosition = Vector2.zero;
            aimCancel = NewButton(aimBar, "Cancel", "Cancel", new Vector2(200f, 84f), UiPalette.Panel, OnAimCancel, out _, out _);
            var xr = (RectTransform)aimCancel.transform; xr.anchorMin = xr.anchorMax = new Vector2(1f, 0f); xr.pivot = new Vector2(1f, 0f); xr.anchoredPosition = Vector2.zero;
            aimBar.gameObject.SetActive(false);
            lastGive = touch != null && !touch.GiveMode;
        }

        void OnSwap()
        {
            if (runner != null && runner.Sim != null) runner.Sim.RotateHand(+1);
        }

        void OnGive()
        {
            if (touch == null) return;
            touch.GiveMode = !touch.GiveMode;
        }

        void OnAimConfirm() { if (touch != null) touch.ConfirmAim(); }
        void OnAimCancel() { if (touch != null) touch.CancelAim(); }

        void SyncHud()
        {
            if (hud == null) return;
            bool modal = ModalScrim.Any;
            if (hud.gameObject.activeSelf == modal) hud.gameObject.SetActive(!modal);
            if (modal || touch == null) return;
            bool dockOpen = build != null && build.BuildMenuOpen;
            var cols = hud.Find("HandButtons");
            if (cols != null && cols.gameObject.activeSelf == dockOpen) cols.gameObject.SetActive(!dockOpen);

            bool give = touch.GiveMode;
            if (give != lastGive)
            {
                lastGive = give;
                var skin = giveBtn != null ? giveBtn.GetComponent<UiButtonSkin>() : null;
                if (skin == null || !skin.SetToggle(give ? UiToggleState.On : UiToggleState.Off))
                    if (giveImg != null) giveImg.color = give ? UiPalette.AccentDk : UiPalette.Panel;     // no art: tint
                if (giveLabel != null) giveLabel.text = GiveLabel(give);
            }

            bool aim = build != null && build.AimActive;
            if (aimBar.gameObject.activeSelf != aim) aimBar.gameObject.SetActive(aim);
            if (aim) UpdateAimBar();
            // keep the aim bar above the bottom bar / open docks
            if (aim)
            {
                float dock = UiDockTop();
                aimBar.anchoredPosition = new Vector2(0f, dock + (build.Demolishing ? 70f : 16f));     // above the "Demolish: ..." mode banner
            }
        }

        float UiDockTop()
        {
            // height (canvas units) of the bottom bar / any open dock above the safe-area bottom edge
            var bar = FindFirstObjectByType<BottomBarView>();
            float h = bar != null ? ((RectTransform)bar.transform).rect.height : 90f;
            return h;
        }

        void UpdateAimBar()
        {
            string what, why = null;
            var hand = FindFirstObjectByType<HandController>();
            if (build.Placing != null)
            {
                var def = runner.Config.Building(build.Placing);
                what = "Place " + (def != null ? def.name : build.Placing);
                if (hand != null && hand.CursorArea != null) why = build.ReasonAt(hand.CursorArea, build.Placing, hand.LRow, hand.LCol);
                else why = "Drag on the island to aim";
            }
            else
            {
                what = "Demolish";
                why = build.Hovered == null ? "Touch a building to aim" : null;
            }
            aimConfirmLabel.text = what;
            aimConfirm.interactable = why == null;
            aimLabel.text = why ?? "Tap the button to confirm";
            aimLabel.color = why == null ? UiPalette.Muted : UiPalette.Danger;
        }

        // ================================================================ pause menu (Android Back)

        public void OpenPause()
        {
            if (!built) return;
            if (pausePanel == null) BuildPause();
            RefreshPauseLabels();
            pausePanel.gameObject.SetActive(true);
            pausePanel.SetAsLastSibling();
            FitPause();
        }

        public void ClosePause() { if (pausePanel != null) pausePanel.gameObject.SetActive(false); }

        TextMeshProUGUI soundLabel, motionLabel;
        RectTransform pauseBox;

        void RefreshPauseLabels()
        {
            if (keepAwakeLabel != null) keepAwakeLabel.text = "Keep awake: " + (KeepAwake ? "On" : "Off");
            var a = AudioService.Instance;
            if (soundLabel != null) soundLabel.text = "Sound: " + (a != null && a.Muted ? "Off" : "On");
            if (motionLabel != null) motionLabel.text = "Motion: " + (Juice.ReduceMotion ? "Calm" : "On");
        }

        /// <summary>The menu box shrinks (never grows) to fit the canvas height inside the safe area, so every button stays on screen.</summary>
        void FitPause()
        {
            if (pauseBox == null || canvasRect == null) return;
            pauseBox.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(pauseBox);
            float f = Mathf.Max(0.01f, MobileUi.ScreenH / MobileUi.RefHeight());
            var bar = FindFirstObjectByType<BottomBarView>();
            float reserve = (bar != null ? ((RectTransform)bar.transform).rect.height : 0f) + MobileUi.SafeInsets().y / f + 10f;      // the bar (and the gesture inset) stay clear
            float availH = canvasRect.rect.height - reserve - MobileUi.SafeInsets().w / f - 16f;
            pauseBox.anchoredPosition = new Vector2(0f, (reserve - MobileUi.SafeInsets().w / f) * 0.5f);
            float availW = canvasRect.rect.width - 24f;
            float h = Mathf.Max(1f, pauseBox.rect.height), w = Mathf.Max(1f, pauseBox.rect.width);
            float k = Mathf.Min(1f, availH / h, availW / w);
            pauseBox.localScale = new Vector3(k, k, 1f);
        }

        void BuildPause()
        {
            pausePanel = NewRect("PauseMenu", canvasRect);     // full screen, above the safe-area HUD
            Stretch(pausePanel);
            var scrim = pausePanel.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrim.raycastTarget = true;
            pausePanel.gameObject.AddComponent<ModalScrim>();
            var box = NewRect("Box", pausePanel);
            pauseBox = box;
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            var bi = box.gameObject.AddComponent<Image>(); bi.color = UiPalette.Bg2;
            var bo = box.gameObject.AddComponent<Outline>(); bo.effectColor = UiPalette.Accent; bo.effectDistance = new Vector2(2f, -2f);
            var vl = box.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(24, 24, 20, 22); vl.spacing = 12f;
            vl.childAlignment = TextAnchor.UpperCenter; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiSkin.Attach(box.gameObject, "ui_panel_jade");       // jade panel like the other modals (applies the frame padding)
            var title = NewText(box, "Menu", 34f, TextAlignmentOptions.Center, UiPalette.Gold);
            title.fontStyle = FontStyles.Bold;
            var tle = title.gameObject.AddComponent<LayoutElement>(); tle.preferredHeight = 44f; tle.minHeight = 44f;
            // two rows of buttons in a 3 column grid: all of them fit a 533 unit tall phone canvas with room to spare
            var grid = NewRect("Grid", box);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            float cellH = Mathf.Ceil(MobileUi.DpToPx(MobileUi.MinTargetDp) / Mathf.Max(0.01f, MobileUi.ScreenH / MobileUi.RefHeight())) + 4f;
            gl.cellSize = new Vector2(240f, Mathf.Max(60f, cellH)); gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperCenter;
            void Row(string label, UnityAction act, out TextMeshProUGUI t, UiButtonVariant v = UiButtonVariant.Standard)
            {
                var b = NewButton(grid, label, label, new Vector2(240f, gl.cellSize.y), UiPalette.Panel, act, out t, out _, v, 22f);
                t.textWrappingMode = TextWrappingModes.Normal; t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 22f;
            }
            Row("Resume", ClosePause, out _, UiButtonVariant.Primary);
            Row("Help", () => { ClosePause(); var m = MetaUiController.Instance; if (m != null) m.Help.Open(); }, out _);
            Row("Stats", () => { ClosePause(); var m = MetaUiController.Instance; if (m != null) { m.Stats.Show(false, false); m.Stats.Open(); } }, out _);
            Row("Sound", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMute(); RefreshPauseLabels(); }, out soundLabel);
            Row("Motion", () => { var mb = FindFirstObjectByType<MetaBarView>(); if (mb != null) mb.ToggleMotion(); RefreshPauseLabels(); }, out motionLabel);
            Row("Keep awake", () => { KeepAwake = !KeepAwake; RefreshPauseLabels(); }, out keepAwakeLabel);
            Row("New game", () =>
            {
                ClosePause();
                var m = MetaUiController.Instance;
                if (m != null) m.Confirm.Ask(MetaBarView.NewGameTitle, MetaBarView.ResetText, MetaBarView.NewGameButton, () => { if (SaveService.Instance != null) SaveService.Instance.ResetAll(); });
            }, out _);
            Row("Save and quit", () => { if (SaveService.Instance != null) SaveService.Instance.Save(); Application.Quit(); }, out _);
            pausePanel.gameObject.SetActive(false);
        }
    }
}
