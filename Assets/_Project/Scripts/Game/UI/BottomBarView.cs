using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Bottom bar (ui-input-render §4.1): title + version badge on the left; area pill (region under
    /// the camera centre, lock + sprint tags), hand pill "n/cap" (green, gold ≥90%, red at cap) and the
    /// button row on the right. Help/Stats/Reset/mute/Shrine pill are wired by MetaBarView (M7).
    /// <para><b>Icon bar (owner pick vp_bar, 2026-10-21):</b> on desktop Stats, Build, Demolish, Sound, Motion and Menu are
    /// icon-only square buttons with hover tooltips; Help and New game (Reset) live in the shared <see cref="GameMenuView"/>
    /// opened by the Menu button (or Esc). The phone bar (MobileLayer) shows the same buttons, icons and menu, with short
    /// labels next to the icons because touch has no hover.</para>
    /// </summary>
    public class BottomBarView : MonoBehaviour
    {
        public static BottomBarView Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        /// <summary>Square icon-button size on the desktop bar (canvas units).</summary>
        public const float IconButtonSize = 44f;
        static readonly string[] IconButtons = { "StatsButton", "BuildButton", "DemolishButton", "MuteButton", "MotionButton", "MenuButton" };
        bool styled, styledMobile;
        Button menuButton;
        public Button MenuButton => menuButton;

        [SerializeField] GameRunner runner;
        [SerializeField] CameraController cameraController;
        [SerializeField] TextMeshProUGUI versionText;
        [SerializeField] TextMeshProUGUI areaText;
        [SerializeField] Image areaIcon;
        [SerializeField] GameObject areaLock;
        [Tooltip("🌫 icon for the open sky between Islands.")]
        [SerializeField] Sprite wildsIcon;
        [SerializeField] GameObject sprintTag;
        [SerializeField] TextMeshProUGUI handText;
        [SerializeField] Image handPill;
        [SerializeField] Button resetButton;
        [Header("M3 build / demolish")]
        [SerializeField] BuildController build;
        [SerializeField] Button buildButton;
        [SerializeField] Button demolishButton;
        [SerializeField] GameObject buildNewDot;
        [Tooltip("\"Demolish: click a building - Esc to stop\" above the bar while demolish mode is on.")]
        [SerializeField] GameObject modeBanner;

        bool lastBuildOn, lastDemolishOn;

        string lastArea; bool areaPainted;
        int lastTotal = -1, lastCap = -1;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GameRunner.Instance;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            if (build == null) build = FindFirstObjectByType<BuildController>();
            // every button label shares one row height and centring, including the scene-added Sound / Motion buttons (VP-23)
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                var l = b.transform.Find("Label");
                if (l == null || l.GetComponent<LayoutElement>() != null) continue;
                l.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
                var lt = l.GetComponent<TextMeshProUGUI>();
                if (lt != null && b.GetComponent<LayoutGroup>() != null) lt.alignment = TextAlignmentOptions.Center;
            }
            if (build != null)
            {
                if (buildButton != null) buildButton.onClick.AddListener(build.ToggleBuildMenu);
                if (demolishButton != null) demolishButton.onClick.AddListener(build.ToggleDemolish);
            }
        }

        /// <summary>"On" toggle styling (§4.1): accent-dk fill + accent border; Demolish turns red when on.</summary>
        static void Toggle(Button b, bool on, bool danger)
        {
            if (b == null) return;
            var skin = b.GetComponent<UiButtonSkin>();   // delivered art: swap sprites (only on state change), no tint
            if (skin != null && skin.SetToggle(on ? (danger ? UiToggleState.Danger : UiToggleState.On) : UiToggleState.Off)) return;
            var img = b.targetGraphic as Image;
            if (img != null) img.color = on ? (danger ? new Color(0.45f, 0.12f, 0.12f) : UiPalette.AccentDk) : UiPalette.Panel;
            var ol = b.GetComponent<Outline>();
            if (ol != null) ol.effectColor = on ? (danger ? UiPalette.Danger : UiPalette.Accent) : UiPalette.Line;
        }

        void SyncBuildButtons()
        {
            if (build == null) return;
            bool bOn = build.BuildMenuOpen, dOn = build.Demolishing;
            if (bOn != lastBuildOn) { lastBuildOn = bOn; Toggle(buildButton, bOn, false); }
            if (dOn != lastDemolishOn) { lastDemolishOn = dOn; Toggle(demolishButton, dOn, true); }
            if (modeBanner != null && modeBanner.activeSelf != dOn) modeBanner.SetActive(dOn);
            if (buildNewDot != null)
            {
                bool dot = !bOn && runner.Sim.Buildings.BuildMenuHasNew();
                if (buildNewDot.activeSelf != dot) buildNewDot.SetActive(dot);
            }
        }

        void Start()
        {
            if (runner == null || runner.Sim == null) return;
            var bal = runner.Config.balance;
            if (versionText != null)
            {
                versionText.text = "v" + bal.versionNum;
                versionText.raycastTarget = true;
                TooltipTrigger.Attach(versionText, "v" + bal.versionNum + (string.IsNullOrEmpty(bal.versionDesc) ? "" : " - " + bal.versionDesc));
            }
            TooltipTrigger.Attach(handPill, null, HandTip);
            TooltipTrigger.Attach(buildButton, "Build: place buildings");
            TooltipTrigger.Attach(demolishButton, "Destroy a building (refunds its resources)");
        }

        string HandTip()
        {
            var sb = new System.Text.StringBuilder("Carried items. Q: send the front stack to the back · E: bring the back stack to the front");
            foreach (var h in runner.State.hand)
                sb.Append('\n').Append(h.qty).Append("× ").Append(runner.Config.Item(h.item)?.name ?? h.item);
            return sb.ToString();
        }

        UiSkin handSkin;
        bool wasFull;
        float lastPillScale = 1f;

        /// <summary>Hand pill: two-beat throb when full, small pop when items arrive (Reduce motion: none).</summary>
        void PillJuice()
        {
            if (handPill == null) return;
            float s = 1f;
            if (!Juice.ReduceMotion)
            {
                float tf = (Time.unscaledTime - Juice.HandFullAt) / 0.45f;
                if (tf >= 0f && tf < 1f) s = 1f + 0.16f * Mathf.Abs(Mathf.Sin(tf * Mathf.PI * 2f)) * (1f - tf);
                float tp = (Time.unscaledTime - Juice.HandPunchAt) / 0.12f;
                if (tp >= 0f && tp < 1f) s = Mathf.Max(s, 1f + 0.07f * Mathf.Sin(tp * Mathf.PI));
            }
            if (s != lastPillScale) { lastPillScale = s; handPill.rectTransform.localScale = new Vector3(s, s, 1f); }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ================================================================ icon bar + menu button (vp_bar)

        /// <summary>The Menu button (Help / settings / New game): a copy of the Help button, created once.</summary>
        void EnsureMenuButton()
        {
            if (menuButton != null) return;
            var t = transform;
            var existing = t.Find("MenuButton");
            if (existing == null)
            {
                var help = t.Find("HelpButton");
                if (help == null) return;
                var go = Instantiate(help.gameObject, t);
                go.name = "MenuButton";
                existing = go.transform;
                var old = go.GetComponent<TooltipTrigger>(); if (old != null) Destroy(old);
            }
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
            menuButton = existing.GetComponent<Button>();
            menuButton.onClick = new Button.ButtonClickedEvent();
            menuButton.onClick.AddListener(GameMenuView.Toggle);
            var lbl = existing.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) { lbl.text = "Menu"; lbl.color = UiPalette.Text; }
        }

        static int IconPx(Sprite s)
        {
            if (s == null) return 28;
            int w = Mathf.RoundToInt(s.rect.width);
            if (w <= 0 || w > 32) return 32;
            return w * Mathf.Max(1, 32 / w);     // whole multiples of the art size (14 -> 28, 16 -> 32, 32 -> 32)
        }

        /// <summary>The button's icon Image (created for text-only buttons), sized to whole art pixels.</summary>
        static Image EnsureIcon(Transform b, Sprite sprite)
        {
            var ic = b.Find("Icon")?.GetComponent<Image>();
            if (ic == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform));
                go.transform.SetParent(b, false);
                go.transform.SetSiblingIndex(0);
                ic = go.AddComponent<Image>();
                ic.raycastTarget = false;
                ic.preserveAspect = true;
            }
            if (sprite != null) ic.sprite = sprite;
            int px = IconPx(ic.sprite);
            var le = ic.GetComponent<LayoutElement>() ?? ic.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = px; le.minHeight = le.preferredHeight = px;
            ic.rectTransform.sizeDelta = new Vector2(px, px);
            ic.enabled = ic.sprite != null;
            return ic;
        }

        /// <summary>Sound / Motion icons follow their state (MetaBarView calls this after a toggle).</summary>
        public void PaintToggleIcons()
        {
            var a = AudioService.Instance;
            var mute = transform.Find("MuteButton");
            if (mute != null) EnsureIcon(mute, UiIcons.Get(a != null && a.Muted ? UiIcons.SoundOff : UiIcons.Sound));
            var motion = transform.Find("MotionButton");
            if (motion != null) EnsureIcon(motion, UiIcons.Get(Juice.ReduceMotion ? UiIcons.MotionCalm : UiIcons.Motion));
        }

        /// <summary>Desktop: icon-only square buttons with tooltips, Help and Reset hidden (they are in the menu). Phone: the
        /// same icons with a label each (MobileLayer hides the rest and sizes the bar for fingers).</summary>
        void ApplyStyle(bool mobile)
        {
            EnsureMenuButton();
            PaintToggleIcons();
            var menu = transform.Find("MenuButton");
            if (menu != null) EnsureIcon(menu, UiIcons.Get(UiIcons.Menu));
            // order: actions (Stats, Build, Demolish), then settings (Sound, Motion), then Menu at the far right
            foreach (var n in new[] { "MuteButton", "MotionButton", "MenuButton" }) { var c = transform.Find(n); if (c != null) c.SetAsLastSibling(); }
            foreach (var n in new[] { "HelpButton", "ResetButton" })
            {
                var c = transform.Find(n);
                if (c != null && c.gameObject.activeSelf) c.gameObject.SetActive(false);
            }
            foreach (var n in IconButtons)
            {
                var b = transform.Find(n);
                if (b == null) continue;
                EnsureIcon(b, null);
                var lbl = b.Find("Label");
                if (lbl != null) lbl.gameObject.SetActive(mobile);
                var h = b.GetComponent<HorizontalLayoutGroup>();
                if (h != null) { h.padding = mobile ? new RectOffset(12, 12, 6, 6) : new RectOffset(6, 6, 6, 6); h.childAlignment = TextAnchor.MiddleCenter; }
                var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = mobile ? -1f : IconButtonSize;
                if (!mobile) le.minHeight = le.preferredHeight = IconButtonSize;
            }
            // icon-only controls say what they do in a tooltip (keys in brackets)
            TooltipTrigger.Attach(transform.Find("StatsButton"), "Stats: your progress so far");
            TooltipTrigger.Attach(buildButton, "Build (B): place buildings");
            TooltipTrigger.Attach(demolishButton, "Demolish: remove a building (refunds its resources)");
            TooltipTrigger.Attach(transform.Find("MuteButton"), null, () => AudioService.Instance != null && AudioService.Instance.Muted ? "Sound is off - click to turn it on" : "Sound is on - click to mute");
            TooltipTrigger.Attach(transform.Find("MotionButton"), null, () => Juice.ReduceMotion ? "Calm motion is on (no shake or flight tweens) - click for full motion" : "Full motion - click for calm (no screen shake, squash or flight tweens)");
            TooltipTrigger.Attach(menuButton, "Menu (Esc): help, sound, motion, new game");
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            styled = true; styledMobile = mobile;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            if (!styled || styledMobile != MobileUi.IsMobile) ApplyStyle(MobileUi.IsMobile);      // after every Start (tooltips, toggle paint)
            SyncBuildButtons();
            var cam =cameraController != null ? cameraController.transform.position : Vector3.zero;
            // regionAtCamCentre (ui.js:377): the Island under the view centre, "🌫 Open sky" between Islands
            string area = runner.Space.WorldToArea(cam, out var hit, out _, out _) ? hit : null;
            if (area != lastArea || !areaPainted)
            {
                lastArea = area; areaPainted = true;
                var def = area != null ? runner.Config.Region(area) : null;
                if (areaText != null) areaText.text = area == null ? "Open sky" : def != null ? def.name : area;
                if (areaIcon != null)
                {
                    var icon = area != null ? runner.Database.RegionIcon(area) : wildsIcon;
                    areaIcon.sprite = icon;
                    areaIcon.enabled = icon != null;
                }
            }
            if (areaLock != null)
            {
                bool locked = area != null && !runner.IsUnlocked(area);
                if (areaLock.activeSelf != locked) areaLock.SetActive(locked);
            }
            if (sprintTag != null && cameraController != null && sprintTag.activeSelf != cameraController.SprintActive)
                sprintTag.SetActive(cameraController.SprintActive);

            int total = runner.Sim.Hand.Total(), cap = runner.Sim.Hand.Cap();
            PillJuice();
            if (total != lastTotal || cap != lastCap)
            {
                lastTotal = total; lastCap = cap;
                if (handText != null)
                {
                    handText.text = total + "/" + cap;
                    var c = total >= cap ? UiPalette.Danger : total >= 0.9f * cap ? UiPalette.Gold : UiPalette.Accent;
                    handText.color = c;
                }
                if (handPill != null)
                {
                    if (handSkin == null) handSkin = handPill.GetComponent<UiSkin>();
                    // delivered pill art: rim colour follows the hand state (green ok / gold >= 90% / red full)
                    if (handSkin != null) handSkin.SetKey(total >= cap ? "ui_pill_red" : total >= 0.9f * cap ? "ui_pill_gold" : "ui_pill_green");
                }
                if (total >= cap && cap > 0 && !wasFull) Juice.PulseHandFull();      // juice: pill throbs when the hand just filled
                wasFull = total >= cap;
            }
        }
    }
}
