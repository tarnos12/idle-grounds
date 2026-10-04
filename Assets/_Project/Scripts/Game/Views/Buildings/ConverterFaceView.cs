using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Converter face (ui.js drawConverterFace U:905; spec §2.4): the whole footprint is the face.
    /// Name row at y+8 px; inputs row at 30% (icon 18 px, 24 px spacing, top-left = in stock green/red,
    /// bottom-right = need gold); result icon 22 px at 55% with the craftable count; status line at
    /// 75.5%; 1-cell progress bar (4 px) at 90%.
    /// </summary>
    public class ConverterFaceView : BuildingFace
    {
        static readonly Color Track = new Color(1f, 1f, 1f, 0.15f);

        [SerializeField] SpriteRenderer nameIcon;
        [SerializeField] TextMeshPro nameText;
        [SerializeField] Transform inputsRoot;
        [SerializeField] IconCount inputTemplate;
        [SerializeField] IconCount result;
        [SerializeField] TextMeshPro statusText;
        [SerializeField] SpriteRenderer statusIcon;
        [SerializeField] SpriteRenderer progressTrack;
        [SerializeField] SpriteRenderer progressFill;

        readonly List<IconCount> inputs = new List<IconCount>();
        int lastRecipe = -2;
        float barX0, barW, barY, barH;

        public ConverterFace LastFace { get; private set; }

        void Awake() { if (inputTemplate != null) inputTemplate.gameObject.SetActive(false); }

        public override void Layout(BuildingView v)
        {
            float wU = v.W;
            // name row (emoji + muted name 9.5 px), y + 8 px
            var nameY = -ViewKit.U(8f);
            ViewKit.Fit(nameIcon, v.Sync.Sprites.Building(v.Building.type), 11f);
            ViewKit.Font(nameText, 9.5f);
            nameText.text = v.Def != null ? v.Def.name : v.Building.type;
            float tw = Mathf.Min(nameText.GetPreferredValues(nameText.text).x, wU - ViewKit.U(20f));
            nameText.rectTransform.sizeDelta = new Vector2(wU - ViewKit.U(20f), ViewKit.U(12f));
            nameText.rectTransform.pivot = new Vector2(0f, 0.5f);
            float total = ViewKit.U(13f) + tw;
            float x0 = wU * 0.5f - total * 0.5f;
            nameIcon.transform.localPosition = new Vector3(x0 + ViewKit.U(5.5f), nameY, 0f);
            nameText.transform.localPosition = new Vector3(x0 + ViewKit.U(13f), nameY, 0f);

            inputsRoot.localPosition = v.L(0.5f, 0.30f);
            result.transform.localPosition = v.L(0.5f, 0.55f);
            ViewKit.Font(result.b, 10f);
            result.b.transform.localPosition = new Vector3(ViewKit.U(11f), -ViewKit.U(8f), 0f);
            statusText.transform.localPosition = v.L(0.5f, 0.755f);
            statusText.rectTransform.sizeDelta = new Vector2(wU - ViewKit.U(4f), ViewKit.U(12f));
            ViewKit.Font(statusText, 9.5f);
            barW = 1f; barH = ViewKit.U(4f);
            barX0 = wU * 0.5f - barW * 0.5f; barY = -0.90f * v.H;
            ViewKit.Bar(progressTrack, barX0, barY, barW, barH);
            progressTrack.color = Track;
            progressFill.color = UiPalette.Gold;
            lastRecipe = -2;
        }

        public override void Refresh(BuildingView v)
        {
            var f = v.Sync.Sim.ConverterFace(v.Area, v.Building);
            LastFace = f;
            if (f == null) { ViewKit.Show(inputsRoot, false); ViewKit.Show(result, false); return; }
            ViewKit.Show(inputsRoot, true); ViewKit.Show(result, true);
            var sprites = v.Sync.Sprites;
            if (f.recipeIndex != lastRecipe)
            {
                lastRecipe = f.recipeIndex;
                while (inputs.Count < f.inputs.Count)
                {
                    var ic = Instantiate(inputTemplate, inputsRoot);
                    ic.name = "Input" + inputs.Count;
                    inputs.Add(ic);
                }
                float sp = ViewKit.U(24f), x = -(f.inputs.Count - 1) * sp * 0.5f;
                for (int i = 0; i < inputs.Count; i++)
                {
                    bool on = i < f.inputs.Count;
                    inputs[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var ic = inputs[i];
                    ic.transform.localPosition = new Vector3(x + i * sp, 0f, 0f);
                    ViewKit.Fit(ic.icon, sprites.Item(f.inputs[i].item), 18f);
                    ViewKit.Font(ic.a, 9f); ViewKit.Font(ic.b, 9f);
                    ic.a.transform.localPosition = new Vector3(-ViewKit.U(9f), ViewKit.U(8f), 0f);
                    ic.b.transform.localPosition = new Vector3(ViewKit.U(9f), -ViewKit.U(8f), 0f);
                }
                ViewKit.Fit(result.icon, sprites.Item(f.recipe.output), 22f);
            }
            for (int i = 0; i < f.inputs.Count; i++)
            {
                var iv = f.inputs[i];
                ViewKit.Text(inputs[i].a, iv.have.ToString());
                ViewKit.Colour(inputs[i].a, iv.have >= iv.need ? UiPalette.Accent : UiPalette.Danger);
                ViewKit.Text(inputs[i].b, iv.need.ToString());
                ViewKit.Colour(inputs[i].b, UiPalette.Gold);
            }
            ViewKit.Text(result.b, f.craftable.ToString());
            ViewKit.Colour(result.b, UiPalette.Gold);

            // status line
            string txt = ""; Color c = UiPalette.Muted; string icon = null;
            var st = f.status;
            if (st != null)
            {
                switch (st.state)
                {
                    case BuildingState.Starved: txt = "Needs"; c = UiPalette.Danger; icon = st.item; break;
                    case BuildingState.NoFuel: txt = "No fuel"; c = UiPalette.Danger; break;
                    case BuildingState.Full: txt = st.label; c = UiPalette.Amber; break;
                    case BuildingState.Working:
                        txt = f.craftsPerMin > 0 ? "Working · " + ViewKit.Fmt(f.craftsPerMin) + "/min" : "Working";
                        c = UiPalette.Accent; break;
                    default: txt = ""; break;
                }
            }
            ViewKit.Text(statusText, txt);
            ViewKit.Colour(statusText, c);
            ViewKit.Show(statusIcon, icon != null);
            if (icon != null)
            {
                ViewKit.Fit(statusIcon, sprites.Item(icon), 12f);
                float tw = statusText.GetPreferredValues(txt).x;
                statusText.transform.localPosition = v.L(0.5f, 0.755f) - new Vector3(ViewKit.U(7f), 0f, 0f);
                statusIcon.transform.localPosition = v.L(0.5f, 0.755f) + new Vector3(tw * 0.5f, 0f, 0f);
            }
            else statusText.transform.localPosition = v.L(0.5f, 0.755f);

            ViewKit.Bar(progressFill, barX0, barY, barW * (float)f.progress, barH);
            ViewKit.Show(progressFill, f.progress > 0);
        }
    }
}
