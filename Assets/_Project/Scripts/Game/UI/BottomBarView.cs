using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Bottom bar (ui-input-render §4.1): title + version badge on the left; area pill (region under
    /// the camera centre, lock + sprint tags), hand pill "n/cap" (green, gold ≥90%, red at cap) and the
    /// button row on the right. Help/Stats/Reset/mute/Shrine pill are wired by MetaBarView (M7).
    /// </summary>
    public class BottomBarView : MonoBehaviour
    {
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

        bool lastBuildOn, lastDemolishOn;

        string lastArea; bool areaPainted;
        int lastTotal = -1, lastCap = -1;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            if (build == null) build = FindFirstObjectByType<BuildController>();
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

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
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
