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

        // ---- real-art variant: one compact strip ABOVE the art in the shared WorldLabel style (VP-07 / VP-10):
        //      [status shape] [input icon + tick/cross + have/need]... → [result icon ×n], thin progress bar along the plate bottom.
        //      Zoom LOD: full at zoom ≤ 2.5, compact (status + result ×n) up to 4.5, status shape only beyond.
        WorldLabel strip;
        SpriteRenderer statusShape;
        readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        bool real, relayout;
        int lastLod = -1;
        BuildingView view;
        static readonly string HaveOk = ColorUtility.ToHtmlStringRGB(WorldLabel.GoodColour), HaveLow = ColorUtility.ToHtmlStringRGB(WorldLabel.BadColour), NeedCol = ColorUtility.ToHtmlStringRGB(WorldLabel.GoldColour);

        /// <summary>The strip label (null for placeholder art).</summary>
        public WorldLabel Strip => real ? strip : null;
        /// <summary>Shape shown for the current state (VP-10: never colour alone).</summary>
        public Sprite StatusShape => statusShape != null ? statusShape.sprite : null;

        void EnsureStrip()
        {
            if (strip != null) return;
            strip = WorldLabel.Create(transform, "Strip");
            strip.compactAboveZoom = 2.5f; strip.hideAboveZoom = 4.5f;
            backing = WorldLabel.NewPlate(strip.content);
            statusShape = WorldLabel.NewSprite(strip.transform, "Status", PixelSprites.Idle, WorldLabel.OrderKeep);
            strip.keep = statusShape.transform;
        }

        /// <summary>Move the strip parts under the label (real art) or back onto the face (placeholder).</summary>
        void Adopt(bool intoStrip)
        {
            var dst = intoStrip ? strip.content : transform;
            if (nameText.transform.parent != dst) nameText.transform.SetParent(dst, false);
            if (inputsRoot.parent != dst) inputsRoot.SetParent(dst, false);
            if (result.transform.parent != dst) result.transform.SetParent(dst, false);
            if (progressTrack.transform.parent != dst) progressTrack.transform.SetParent(dst, false);
            if (progressFill.transform.parent != dst) progressFill.transform.SetParent(dst, false);
        }

        void LayoutReal(BuildingView v)
        {
            view = v;
            EnsureStrip();
            Adopt(true);
            ViewKit.Show(strip, true);
            ViewKit.Show(nameIcon, false); ViewKit.Show(statusText, false); ViewKit.Show(statusIcon, false);
            // the (hidden-name) text doubles as the "→" between inputs and result
            ViewKit.Show(nameText, true);
            WorldLabel.Style(nameText, 9f, WorldLabel.MutedColour);
            nameText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nameText.rectTransform.sizeDelta = new Vector2(ViewKit.U(20f), ViewKit.U(12f));
            nameText.text = "→";
            WorldLabel.Style(result.b, 9f, WorldLabel.GoldColour);
            result.b.rectTransform.sizeDelta = new Vector2(ViewKit.U(40f), ViewKit.U(12f));
            WorldLabel.Sort(result.icon, WorldLabel.OrderContent);
            ViewKit.Show(inputsRoot, true);
            inputsRoot.localPosition = Vector3.zero;
            result.transform.localPosition = Vector3.zero;
            result.b.transform.localPosition = Vector3.zero;
            WorldLabel.Sort(progressTrack, WorldLabel.OrderPlate + 1);
            WorldLabel.Sort(progressFill, WorldLabel.OrderPlate + 2);
            progressTrack.color = Track;
            progressFill.color = WorldLabel.GoldColour;
            strip.SetAnchor(new Vector3(v.W * 0.5f, v.ArtTop + ViewKit.U(2f), 0f));
            lastRecipe = -2; LastFace = null; relayout = true; lastHadStatus = false; lastLod = -1;
        }

        int lastFontVersion = -1;
        float TextW(TextMeshPro t, string s) => t.GetPreferredValues(s).x + ViewKit.U(2f);

        void RelayoutReal(BuildingView v, ConverterFace f)
        {
            relayout = false;
            bool compact = strip.Compact;
            float gap = ViewKit.U(4f), ic = ViewKit.U(12f), ig = ViewKit.U(2f), sw = ViewKit.U(11f), pad = ViewKit.U(4f);
            float y = ViewKit.U(10.5f), ph = ViewKit.U(18f);
            int n = f.inputs.Count;
            float total = sw + gap;
            if (!compact)
                for (int i = 0; i < n; i++) total += ic + ig + TextW(inputs[i].a, inputs[i].a.text) + gap;
            float aw = TextW(nameText, "→");
            float rw = TextW(result.b, result.b.text);
            if (!compact) total += aw + gap;
            total += ic + ig + rw;
            float x = -total * 0.5f;
            var home = new Vector3(x + sw * 0.5f, y, 0f);
            x += sw + gap;
            ViewKit.Show(inputsRoot, !compact);
            ViewKit.Show(nameText, !compact);
            if (!compact)
            {
                for (int i = 0; i < n; i++)
                {
                    var c = inputs[i];
                    float iw = TextW(c.a, c.a.text);
                    c.icon.transform.localPosition = new Vector3(x + ic * 0.5f, y, 0f);
                    if (i < marks.Count && marks[i] != null) marks[i].transform.localPosition = new Vector3(x + ic - ViewKit.U(1f), y - ViewKit.U(4f), 0f);
                    x += ic + ig;
                    c.a.transform.localPosition = new Vector3(x + iw * 0.5f, y, 0f); x += iw + gap;
                }
                nameText.transform.localPosition = new Vector3(x + aw * 0.5f, y, 0f); x += aw + gap;
            }
            result.icon.transform.localPosition = new Vector3(x + ic * 0.5f, y, 0f); x += ic + ig;
            result.b.transform.localPosition = new Vector3(x + rw * 0.5f, y, 0f);
            float pw = total + 2f * pad;
            WorldLabel.SizePlate(backing, 0f, ph * 0.5f, pw, ph);
            barW = pw - ViewKit.U(6f); barX0 = -barW * 0.5f; barY = ViewKit.U(3.5f); barH = ViewKit.U(2f);
            ViewKit.Bar(progressTrack, barX0, barY, barW, barH);
            strip.SetBounds(-pw * 0.5f, 0f, pw, ph);
            strip.SetKeep(home, new Vector3(0f, ViewKit.U(6f), 0f));
            strip.SetAnchor(new Vector3(v.W * 0.5f, v.ArtTop + ViewKit.U(2f), 0f));
        }

        public override void Layout(BuildingView v)
        {
            real = v.RealArt;
            if (real) { LayoutReal(v); return; }
            if (strip != null) { Adopt(false); ViewKit.Show(strip, false); }
            foreach (var mk in marks) if (mk != null) ViewKit.Show(mk, false);
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
            statusText.textWrappingMode = TextWrappingModes.NoWrap; statusText.overflowMode = TextOverflowModes.Overflow;   // never clipped ("Output pile ful")
            ViewKit.Outline(statusText);
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
            ViewKit.Show(inputsRoot, !real || !strip.Compact); ViewKit.Show(result, true);
            var sprites = v.Sync.Sprites;
            if (f.recipeIndex != lastRecipe)
            {
                lastRecipe = f.recipeIndex;
                while (inputs.Count < f.inputs.Count)
                {
                    var ic = Instantiate(inputTemplate, inputsRoot);
                    ic.name = "Input" + inputs.Count;
                    inputs.Add(ic);
                    var mk = WorldLabel.NewSprite(ic.transform, "Mark", PixelSprites.Tick, WorldLabel.OrderContent + 2);
                    mk.gameObject.SetActive(false);
                    marks.Add(mk);
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
                        WorldLabel.Style(ic.a, 8.5f, WorldLabel.TextColour); ViewKit.Show(ic.b, false);
                        WorldLabel.Sort(ic.icon, WorldLabel.OrderContent);
                        ViewKit.Show(marks[i], true);
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
                        var want = iv.have >= iv.need ? PixelSprites.Tick : PixelSprites.Cross;
                        if (marks[i].sprite != want) marks[i].sprite = want;
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
                if (real) { statusShape.sprite = PixelSprites.ForState(st != null ? st.state : (BuildingState?)null); ViewKit.Text(statusText, txt); goto afterStatus; }   // strip: status is a shape (full text lives in the hover tooltip)
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
            if (real && strip.LodVersion != lastLod) { lastLod = strip.LodVersion; relayout = true; }
            if (real && lastFontVersion != UiFont.Version) { lastFontVersion = UiFont.Version; relayout = true; }      // Text font setting: the pill is measured in the face it is drawn in
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
