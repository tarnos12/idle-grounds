using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>"How to play" (ui-input-render §4.15): scrollable titled sections from <see cref="HelpText"/>.</summary>
    public class HelpModalView : ModalView
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] RectTransform content;
        [SerializeField] UiTextRow sectionTemplate;
        [SerializeField] ScrollRect scroll;

        readonly List<UiTextRow> rows = new List<UiTextRow>();
        public int SectionCount { get; private set; }

        public override void Open()
        {
            if (Sim == null) return;
            if (titleText != null) titleText.text = UiText.StripAllEmoji(HelpText.Title).Trim();   // the title emoji has no sprite -> tofu box
            var secs = HelpText.Sections(Sim);
            while (rows.Count < secs.Count)
            {
                var r = Instantiate(sectionTemplate, content);
                rows.Add(r);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < secs.Count;
                rows[i].gameObject.SetActive(on);
                if (on) rows[i].Set(UiText.StripEmoji(secs[i].title), UiText.StripEmoji(secs[i].body));
            }
            SectionCount = secs.Count;
            base.Open();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f; }
        }
    }
}
