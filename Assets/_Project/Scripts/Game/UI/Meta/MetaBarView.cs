using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// M7 parts of the bottom bar (ui-input-render §4.1): the ☯ Shrine pill "N" (hidden until ascended / AP &gt; 0 /
    /// a Gate exists; gold glow when any perk is affordable), the ascension tag in the area pill
    /// ("☯ N · ×speed" — ascensions + world speed), the mute toggle (persisted, "click" on unmute) and the
    /// Help / Stats / Reset buttons (Reset → confirm "Reset ALL progress and start over?" → wipe + reload).
    /// </summary>
    public class MetaBarView : MonoBehaviour
    {
        public const string ResetText = "Reset ALL progress and start over?";

        [SerializeField] GameRunner runner;
        [SerializeField] Button helpButton;
        [SerializeField] Button statsButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button muteButton;
        [SerializeField] TextMeshProUGUI muteLabel;
        [SerializeField] Button motionButton;
        [SerializeField] TextMeshProUGUI motionLabel;
        [SerializeField] Button shrinePill;
        [SerializeField] TextMeshProUGUI shrineText;
        [SerializeField] Outline shrineBorder;
        [SerializeField] GameObject ascTag;
        [SerializeField] TextMeshProUGUI ascText;
        [SerializeField] HelpModalView help;
        [SerializeField] StatsPanelView stats;
        [SerializeField] PerkShopView perkShop;
        [SerializeField] ConfirmDialogView confirm;


        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (helpButton != null) helpButton.onClick.AddListener(() => help.Open());
            if (statsButton != null) statsButton.onClick.AddListener(() => stats.Open());
            if (resetButton != null) { resetButton.interactable = true; resetButton.onClick.AddListener(AskReset); }
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (motionButton != null) motionButton.onClick.AddListener(ToggleMotion);
            if (shrinePill != null) shrinePill.onClick.AddListener(() => perkShop.Open());
        }

        void Start()
        {
            PaintMute();
            PaintMotion();
            TooltipTrigger.Attach(motionButton, "Reduce motion: no screen shake, squash or flight tweens, no ambient motes");
            TooltipTrigger.Attach(helpButton, "How to play");
            TooltipTrigger.Attach(statsButton, "Your progress so far");
            TooltipTrigger.Attach(resetButton, "Wipe the save and start over");
            TooltipTrigger.Attach(muteButton, "Mute / unmute sound");
            TooltipTrigger.Attach(shrinePill, "Ascension Shrine - spend Ascension Points on permanent perks");
            if (ascTag != null)
            {
                Graphic g = ascTag.GetComponent<Graphic>(); if (g == null) g = ascText;
                if (g != null) { g.raycastTarget = true; TooltipTrigger.Attach(g, null, AscTip); }
            }
        }

        string AscTip()
        {
            if (runner == null || runner.Sim == null) return null;
            var s = runner.Sim.State;
            var sb = new System.Text.StringBuilder("Ascensions: ").Append(s.ascensions).Append(" · world speed ×")
                .Append(runner.Sim.Prestige.WorldSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            var vows = MetaText.ActiveVows(runner.Sim);
            if (vows.Count > 0) sb.Append("\nVows this run: ").Append(string.Join(", ", vows.ConvertAll(v => v.name)));
            return sb.ToString();
        }

        public void AskReset()
        {
            confirm.Ask(ResetText, "Reset", () => { if (SaveService.Instance != null) SaveService.Instance.ResetAll(); });
        }

        public void ToggleMute()
        {
            var a = AudioService.Instance;
            if (a == null) return;
            a.SetMuted(!a.Muted);
            PaintMute();
            if (!a.Muted) AudioService.Play("click");
        }

        public void ToggleMotion()
        {
            Juice.SetReduceMotion(!Juice.ReduceMotion);
            PaintMotion();
            AudioService.Play("click");
        }

        void PaintMotion()
        {
            bool calm = Juice.ReduceMotion;
            if (motionLabel != null) motionLabel.text = calm ? "Calm" : "Motion";
            if (motionButton != null && motionButton.targetGraphic is Image img)
                img.color = calm ? UiPalette.AccentDk : UiPalette.Panel;
        }

        void PaintMute()
        {
            var a = AudioService.Instance;
            if (muteLabel != null) muteLabel.text = a != null && a.Muted ? "Muted" : "Sound";
            if (muteButton != null && muteButton.targetGraphic is Image img)
                img.color = a != null && a.Muted ? new Color(0.45f, 0.12f, 0.12f) : UiPalette.Panel;
        }

        // last painted values (compared every frame; strings only rebuilt on change)
        bool lastShow, lastAfford, painted;
        int lastAp = -1, lastAsc = -1, lastVowCount = -1, lastVowHash;
        float lastSpeed = -1f;

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            var sim = runner.Sim; var s = sim.State;
            bool gateExists = false;
            foreach (var a in s.areas) foreach (var b in a.buildings) if (b.type == "ascension_gate") { gateExists = true; break; }
            bool showShrine = s.ascensions > 0 || s.ascendPoints > 0 || gateExists;
            bool afford = false;
            foreach (var p in sim.Config.perks) if (sim.Prestige.CanAfford(p.id)) { afford = true; break; }
            float speed = (float)System.Math.Round(sim.Prestige.WorldSpeed, 2);
            var vows = s.vows.active;
            int vowHash = 17;
            foreach (var id in vows) vowHash = vowHash * 31 + (id != null ? id.GetHashCode() : 0);
            if (painted && showShrine == lastShow && s.ascendPoints == lastAp && afford == lastAfford && s.ascensions == lastAsc
                && speed == lastSpeed && vows.Count == lastVowCount && vowHash == lastVowHash) return;
            painted = true;
            lastShow = showShrine; lastAp = s.ascendPoints; lastAfford = afford; lastAsc = s.ascensions; lastSpeed = speed;
            lastVowCount = vows.Count; lastVowHash = vowHash;
            if (shrinePill != null)
            {
                shrinePill.gameObject.SetActive(showShrine);
                shrineText.text = s.ascendPoints.ToString();
                shrineBorder.effectColor = afford ? UiPalette.Gold : UiPalette.Line;
                shrineBorder.effectDistance = afford ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            if (ascTag != null)
            {
                // "☯N" (the tag's icon is the ☯) + world speed, then the vow chip (ui.js vowChipHTML: vow icons)
                var vowDefs = MetaText.ActiveVows(sim);
                ascTag.SetActive(s.ascensions > 0 || vowDefs.Count > 0);
                var sb = new System.Text.StringBuilder();
                sb.Append(s.ascensions).Append("  ×").Append(sim.Prestige.WorldSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                if (vowDefs.Count > 0)
                {
                    sb.Append("   ");
                    foreach (var v in vowDefs) sb.Append(UiText.StripEmoji(v.icon));
                }
                ascText.text = sb.ToString();
            }
        }
    }
}
