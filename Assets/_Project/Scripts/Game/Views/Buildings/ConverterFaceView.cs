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

        SpriteRenderer backing;
        readonly List<IconCount> inputs = new List<IconCount>();
        int lastRecipe = -2;
        float barX0, barW, barY, barH;

        public ConverterFace LastFace { get; private set; }

        void Awake() { if (inputTemplate != null) inputTemplate.gameObject.SetActive(false); }

        // ---- real-art variant: one compact strip BELOW the footprint (inputs have/need → result ×n, thin bar, status dot) ----
        SpriteRenderer dot;
        bool real, relayout;
        BuildingView view;
        static readonly string HaveOk = ColorUtility.ToHtmlStringRGB(UiPalette.Accent), HaveLow = ColorUtility.ToHtmlStringRGB(UiPalette.Danger), NeedCol = ColorUtility.ToHtmlStringRGB(UiPalette.Gold);

        void LayoutReal(BuildingView v)
        {
            view = v;
            var like = progressTrack.GetComponent<SpriteRenderer>();
            if (backing == null) backing = ViewKit.NewSquare(transform, "Backing", like, -2, ViewKit.PlateColour);
            backing.gameObject.SetActive(true);
            if (dot == null) dot = ViewKit.NewSquare(transform, "StatusDot", like, 1, UiPalette.Muted);
            dot.gameObject.SetActive(true);
            ViewKit.Show(nameIcon, false); ViewKit.Show(statusText, false); ViewKit.Show(statusIcon, false);
            // the (hidden-name) text doubles as the "→" between inputs and result
            ViewKit.Show(nameText, true);
            nameText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nameText.rectTransform.sizeDelta = new Vector2(ViewKit.U(20f), ViewKit.U(12f));
            nameText.text = "→"; ViewKit.Font(nameText, 9f); nameText.color = UiPalette.Muted; ViewKit.Outline(nameText);
            ViewKit.Font(result.b, 9f); ViewKit.Outline(result.b);
            result.b.rectTransform.sizeDelta = new Vector2(ViewKit.U(40f), ViewKit.U(12f));
            ViewKit.Show(inputsRoot, true);
            inputsRoot.localPosition = Vector3.zero;
            result.transform.localPosition = Vector3.zero;
            result.b.transform.localPosition = Vector3.zero;
            barH = ViewKit.U(2f); barY = v.ArtTop + ViewKit.U(3.5f);   // strip sits ABOVE the art so the dumped output pile (below the footprint) never covers it
            progressTrack.color = Track;
            progressFill.color = UiPalette.Gold;
            ViewKit.ToOverlay(this);
            lastRecipe = -2; LastFace = null; relayout = true; lastHadStatus = false;
        }

        float TextW(TextMeshPro t, string s) => t.GetPreferredValues(s).x + ViewKit.U(2f);

        void RelayoutReal(BuildingView v, ConverterFace f)
        {
            relayout = false;
            float gap = ViewKit.U(5f), ic = ViewKit.U(12f), ig = ViewKit.U(2f), dotW = ViewKit.U(5f);
            float y = v.ArtTop + ViewKit.U(10.5f);
            int n = f.inputs.Count;
            float total = dotW + gap;
            var iw = new float[n];
            for (int i = 0; i < n; i++) { iw[i] = TextW(inputs[i].a, inputs[i].a.text); total += ic + ig + iw[i] + gap; }
            float aw = TextW(nameText, "→");
            float rw = TextW(result.b, result.b.text);
            total += aw + gap + ic + ig + rw;
            float x = v.W * 0.5f - total * 0.5f;
            dot.transform.localPosition = new Vector3(x + dotW * 0.5f, y, 0f);
            dot.transform.localScale = new Vector3(dotW, dotW, 1f);
            x += dotW + gap;
            for (int i = 0; i < n; i++)
            {
                var c = inputs[i];
                c.icon.transform.localPosition = new Vector3(x + ic * 0.5f, y, 0f); x += ic + ig;
                c.a.transform.localPosition = new Vector3(x + iw[i] * 0.5f, y, 0f); x += iw[i] + gap;
            }
            nameText.transform.localPosition = new Vector3(x + aw * 0.5f, y, 0f); x += aw + gap;
            result.icon.transform.localPosition = new Vector3(x + ic * 0.5f, y, 0f); x += ic + ig;
            result.b.transform.localPosition = new Vector3(x + rw * 0.5f, y, 0f);
            float pw = total + ViewKit.U(10f);
            backing.transform.localPosition = new Vector3(v.W * 0.5f, v.ArtTop + ViewKit.U(8.75f), 0f);
            backing.transform.localScale = new Vector3(pw, ViewKit.U(16.5f), 1f);
            barW = pw - ViewKit.U(4f); barX0 = v.W * 0.5f - barW * 0.5f;
            ViewKit.Bar(progressTrack, barX0, barY, barW, barH);
        }

        public override void Layout(BuildingView v)
        {
            real = v.RealArt;
            if (real) { LayoutReal(v); return; }
            if (backing != null) backing.gameObject.SetActive(false);
            if (dot != null) dot.gameObject.SetActive(false);
            ViewKit.Show(nameIcon, true); ViewKit.Show(nameText, true); ViewKit.Show(statusText, true);
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
            LastFace = null;
        }

        /// <summary>Per-view reusable face (non-allocating Sim.ConverterFace(.., into)), refreshed every frame;
        /// text is only rebuilt when its value changes.</summary>
        readonly ConverterFace faceBuf = new ConverterFace();
        int[] lastHave = new int[0], lastNeed = new int[0];
        int lastCraftable = int.MinValue;
        BuildingState lastState = (BuildingState)(-1);
        string lastStatusItem, lastStatusLabel; double lastCpm = -1; bool lastHadStatus;
        public override bool IsActive(BuildingView v) => v.Building.smeltDoneAt > v.Sync.Runner.SimNow;


        public override void Refresh(BuildingView v)
        {
            LastFace = v.Sync.Sim.ConverterFace(v.Area, v.Building, faceBuf) ? faceBuf : null;
            var f = LastFace;
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
                    if (real) ViewKit.ToOverlay(ic);
                    inputs.Add(ic);
                }
                float sp = ViewKit.U(24f), x = -(f.inputs.Count - 1) * sp * 0.5f;
                for (int i = 0; i < inputs.Count; i++)
                {
                    bool on = i < f.inputs.Count;
                    inputs[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var ic = inputs[i];
                    if (real)
                    {
                        ViewKit.Fit(ic.icon, sprites.Item(f.inputs[i].item), 12f);
                        ViewKit.Font(ic.a, 8.5f); ViewKit.Outline(ic.a); ViewKit.Show(ic.b, false);
                        ic.a.rectTransform.sizeDelta = new Vector2(ViewKit.U(44f), ViewKit.U(12f));
                        ic.transform.localPosition = Vector3.zero; ic.a.transform.localPosition = Vector3.zero; ic.icon.transform.localPosition = Vector3.zero;
                        continue;
                    }
                    ic.transform.localPosition = new Vector3(x + i * sp, 0f, 0f);
                    ViewKit.Fit(ic.icon, sprites.Item(f.inputs[i].item), 18f);
                    ViewKit.Font(ic.a, 9f); ViewKit.Font(ic.b, 9f);
                    ic.a.transform.localPosition = new Vector3(-ViewKit.U(9f), ViewKit.U(8f), 0f);
                    ic.b.transform.localPosition = new Vector3(ViewKit.U(9f), -ViewKit.U(8f), 0f);
                }
                ViewKit.Fit(result.icon, sprites.Item(f.recipe.output), real ? 12f : 22f);
                relayout = true;
                lastHave = new int[f.inputs.Count]; lastNeed = new int[f.inputs.Count];
                for (int i = 0; i < lastHave.Length; i++) lastHave[i] = lastNeed[i] = int.MinValue;
                lastCraftable = int.MinValue;
                lastHadStatus = false; lastState = (BuildingState)(-1);
            }
            for (int i = 0; i < f.inputs.Count && i < lastHave.Length; i++)
            {
                var iv = f.inputs[i];
                if (iv.have != lastHave[i] || iv.need != lastNeed[i])
                {
                    lastHave[i] = iv.have; lastNeed[i] = iv.need;
                    if (real)
                    {
                        ViewKit.Text(inputs[i].a, "<color=#" + (iv.have >= iv.need ? HaveOk : HaveLow) + ">" + iv.have + "</color><color=#" + NeedCol + ">/" + iv.need + "</color>");
                        relayout = true; continue;
                    }
                    ViewKit.Text(inputs[i].a, iv.have.ToString());
                    ViewKit.Colour(inputs[i].a, iv.have >= iv.need ? UiPalette.Accent : UiPalette.Danger);
                    ViewKit.Text(inputs[i].b, iv.need.ToString());
                    ViewKit.Colour(inputs[i].b, UiPalette.Gold);
                }
            }
            if (f.craftable != lastCraftable)
            {
                lastCraftable = f.craftable;
                ViewKit.Text(result.b, real ? "×" + f.craftable : f.craftable.ToString());
                relayout = true;
                ViewKit.Colour(result.b, UiPalette.Gold);
            }

            // status line (rebuilt only when the status / rate changes)
            var st = f.status;
            bool changed = (st != null) != lastHadStatus || st != null &&
                (st.state != lastState || st.item != lastStatusItem || st.label != lastStatusLabel || f.craftsPerMin != lastCpm);
            if (changed)
            {
                lastHadStatus = st != null;
                lastState = st != null ? st.state : (BuildingState)(-1);
                lastStatusItem = st?.item; lastStatusLabel = st?.label; lastCpm = f.craftsPerMin;
                string txt = ""; Color c = UiPalette.Muted; string icon = null;
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
                if (real) { dot.color = c; ViewKit.Text(statusText, txt); goto afterStatus; }   // strip: status is a coloured dot (full text lives in the hover tooltip)
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
            }

        afterStatus:
            if (real && relayout) RelayoutReal(v, f);

            // progress: extrapolated every frame from the batch end time and the sampled batch length
            var b = v.Building;
            double now = v.Sync.Runner.SimNow;
            float progress = b.smeltDoneAt > now && f.batchMs > 0 ? Mathf.Clamp01((float)(1 - (b.smeltDoneAt - now) / f.batchMs)) : 0f;
            ViewKit.Bar(progressFill, barX0, barY, barW * progress, barH);
            ViewKit.Show(progressFill, progress > 0);
        }
    }
}
