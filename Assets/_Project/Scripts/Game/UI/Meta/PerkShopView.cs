using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Ascension Shrine / perk shop (ui-input-render §4.8, ui.js renderPerkShop): AP line (+ preview "+G on
    /// ascending" when opened from the ascend dialog), head starts, perks grouped Pace / Economy / Combat /
    /// Meta / Legacy / Other; card = icon, name, lvl/max, "★ good first pick", desc, "label now → next",
    /// buy button "{cost} AP" / "MAX". Affordable = gold border, maxed = 60 % opacity, unaffordable click =
    /// error SFX. Opening marks the shop seen. Rebuilt whenever AP / perk levels change.
    /// </summary>
    public class PerkShopView : ModalView
    {
        public static readonly (string name, string[] ids)[] Groups =
        {
            ("Pace", new[] { "haste", "regrow", "gale" }),
            ("Economy", new[] { "frugal", "ember", "bounty", "hands" }),
            ("Combat", new[] { "fury" }),
            ("Meta", new[] { "apgain", "hall", "autoboost", "slumber", "bless" }),
            ("Legacy", new[] { "paths", "legacy" }),
        };
        public static readonly string[] FirstPicks = { "haste", "hands", "paths" };

        [SerializeField] TextMeshProUGUI apLine;
        [SerializeField] RectTransform listRoot;
        [SerializeField] TextMeshProUGUI groupTemplate;
        [SerializeField] PerkCardView cardTemplate;
        [SerializeField] ScrollRect scroll;

        readonly List<TextMeshProUGUI> headers = new List<TextMeshProUGUI>();
        readonly List<PerkCardView> cards = new List<PerkCardView>();
        string key;

        public int CardCount { get; private set; }
        public int Bought { get; private set; }

        public override void Open()
        {
            if (Sim == null) return;
            Sim.MarkPerkShopSeen();
            key = null;
            base.Open();
            Rebuild();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f; }
        }

        void LateUpdate()
        {
            if (!IsOpen || Sim == null) return;
            if (StateKey() != key) Rebuild();
        }

        string StateKey()
        {
            var s = Sim.State;
            var sb = new System.Text.StringBuilder();
            sb.Append(s.ascendPoints).Append('|').Append(s.ascensions).Append('|').Append(s.ascendPrompt);
            foreach (var p in Sim.Config.perks) sb.Append('|').Append(Sim.PerkLevel(p.id));
            return sb.ToString();
        }

        static int GroupOf(string id)
        {
            for (int i = 0; i < Groups.Length; i++) if (System.Array.IndexOf(Groups[i].ids, id) >= 0) return i;
            return Groups.Length;     // "Other"
        }

        void Rebuild()
        {
            key = StateKey();
            var s = Sim.State;
            int ap = s.ascendPoints;
            bool preview = s.ascendPrompt;
            int gain = preview ? Sim.AscendReward() : 0;
            if (apLine != null)
            {
                apLine.text = (preview
                        ? "You have <b>" + ap + "</b> AP <color=" + MetaText.Gold + ">(+" + gain + " on ascending)</color>"
                        : "<b>" + ap + "</b> Ascension Point" + (ap == 1 ? "" : "s") + " to spend") +
                    "  ·  " + MetaText.Plural(s.ascensions, "ascension") +
                    "  ·  world speed ×" + Sim.Prestige.WorldSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                    "\n<size=80%>" + (preview ? "Next run — " : "") + MetaText.HeadStarts(Sim, preview ? s.ascensions + 1 : (int?)null) + "</size>";
            }

            int h = 0, c = 0;
            for (int g = 0; g <= Groups.Length; g++)
            {
                var perks = Sim.Config.perks.FindAll(p => GroupOf(p.id) == g);
                if (perks.Count == 0) continue;
                if (headers.Count <= h) headers.Add(Instantiate(groupTemplate, listRoot));
                var hdr = headers[h++];
                hdr.gameObject.SetActive(true);
                hdr.text = (g < Groups.Length ? Groups[g].name : "Other").ToUpperInvariant();
                hdr.transform.SetSiblingIndex(h + c - 1);
                foreach (var p in perks)
                {
                    if (cards.Count <= c) cards.Add(NewCard());
                    var card = cards[c++];
                    card.gameObject.SetActive(true);
                    card.transform.SetSiblingIndex(h + c - 1);
                    Fill(card, p, ap);
                }
            }
            for (int i = h; i < headers.Count; i++) headers[i].gameObject.SetActive(false);
            for (int i = c; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
            CardCount = c;
            groupTemplate.gameObject.SetActive(false);
            cardTemplate.gameObject.SetActive(false);
        }

        PerkCardView NewCard()
        {
            var card = Instantiate(cardTemplate, listRoot);
            card.buyButton.onClick.AddListener(() => Buy(card.perkId));
            card.cardButton.onClick.AddListener(() => CardClicked(card.perkId));
            return card;
        }

        void Fill(PerkCardView card, PerkDef p, int ap)
        {
            card.perkId = p.id;
            int lvl = Sim.PerkLevel(p.id);
            int? cost = Sim.PerkCost(p.id);
            bool maxed = cost == null, afford = !maxed && ap >= cost.Value;
            card.icon.sprite = runner.Database.PerkIcon(p.id);
            card.nameText.text = UiText.StripEmoji(p.name);
            card.levelText.text = lvl + "/" + p.max;
            card.pickTag.SetActive(System.Array.IndexOf(FirstPicks, p.id) >= 0);
            card.descText.text = UiText.StripEmoji(p.desc);
            card.fxText.text = MetaText.PerkFxLine(Sim, p, lvl);
            card.buyLabel.text = maxed ? "MAX" : cost.Value + " AP";
            card.buyButton.interactable = afford;
            card.border.effectColor = afford ? UiPalette.Gold : UiPalette.Line;
            card.group.alpha = maxed ? 0.6f : 1f;
        }

        /// <summary>Buy a perk (false = unaffordable / maxed: error buzz).</summary>
        public bool Buy(string id)
        {
            if (Sim == null) return false;
            if (!Sim.BuyPerk(id)) { AudioService.Play("error"); return false; }
            Bought++;
            Rebuild();
            return true;
        }

        void CardClicked(string id)
        {
            var cost = Sim.PerkCost(id);
            if (cost != null && Sim.State.ascendPoints < cost.Value) AudioService.Play("error");
        }
    }
}
