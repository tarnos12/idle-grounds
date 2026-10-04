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
        [SerializeField] Button shrinePill;
        [SerializeField] TextMeshProUGUI shrineText;
        [SerializeField] Outline shrineBorder;
        [SerializeField] GameObject ascTag;
        [SerializeField] TextMeshProUGUI ascText;
        [SerializeField] HelpModalView help;
        [SerializeField] StatsPanelView stats;
        [SerializeField] PerkShopView perkShop;
        [SerializeField] ConfirmDialogView confirm;

        string lastKey;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (helpButton != null) helpButton.onClick.AddListener(() => help.Open());
            if (statsButton != null) statsButton.onClick.AddListener(() => stats.Open());
            if (resetButton != null) { resetButton.interactable = true; resetButton.onClick.AddListener(AskReset); }
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (shrinePill != null) shrinePill.onClick.AddListener(() => perkShop.Open());
        }

        void Start() => PaintMute();

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

        void PaintMute()
        {
            var a = AudioService.Instance;
            if (muteLabel != null) muteLabel.text = a != null && a.Muted ? "Muted" : "Sound";
            if (muteButton != null && muteButton.targetGraphic is Image img)
                img.color = a != null && a.Muted ? new Color(0.45f, 0.12f, 0.12f) : UiPalette.Panel;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            var sim = runner.Sim; var s = sim.State;
            bool gateExists = false;
            foreach (var a in s.areas) foreach (var b in a.buildings) if (b.type == "ascension_gate") { gateExists = true; break; }
            bool showShrine = s.ascensions > 0 || s.ascendPoints > 0 || gateExists;
            bool afford = false;
            foreach (var p in sim.Config.perks) if (sim.Prestige.CanAfford(p.id)) { afford = true; break; }
            string key = showShrine + "|" + s.ascendPoints + "|" + afford + "|" + s.ascensions + "|" + sim.Prestige.WorldSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            if (key == lastKey) return;
            lastKey = key;
            if (shrinePill != null)
            {
                shrinePill.gameObject.SetActive(showShrine);
                shrineText.text = s.ascendPoints.ToString();
                shrineBorder.effectColor = afford ? UiPalette.Gold : UiPalette.Line;
                shrineBorder.effectDistance = afford ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            if (ascTag != null)
            {
                ascTag.SetActive(s.ascensions > 0);
                ascText.text = s.ascensions + "  ×" + sim.Prestige.WorldSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
