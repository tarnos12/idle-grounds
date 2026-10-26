using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Base building view (ui-input-render §2.4). The root sits at the footprint's TOP-LEFT corner;
    /// children are laid out in footprint fractions (<see cref="L"/>). Draws the panel + border
    /// (ghost = translucent green + dashed, built = dark slate), the default face (icon 24 px at 38%,
    /// name at 66%), the ghost's remaining-cost line at 86%, and the hover/selection outline.
    /// Family visuals are <see cref="BuildingFace"/> children added by the prefab variants.
    /// Pull-based: <see cref="BuildingViewSync"/> calls Bind/Refresh.
    /// </summary>
    public class BuildingView : MonoBehaviour
    {
        public static readonly Color GhostFill = ViewKit.Rgba(74, 222, 128, 0.10f), GhostBorder = UiPalette.Hex("#4ade80");
        public static readonly Color BuiltFill = ViewKit.Rgba(60, 70, 80, 0.95f), BuiltBorder = UiPalette.Hex("#3c4651");

        [SerializeField] SpriteRenderer panel;
        [SerializeField] EdgeFrame border;
        [SerializeField] EdgeFrame highlight;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro nameText;
        [SerializeField] IconRow needsRow;
        [Tooltip("Built panel colours (Altar / Dragon variants override).")]
        public Color builtFill = BuiltFill;
        public Color builtBorder = BuiltBorder;

        public string Area { get; private set; }
        public Building Building { get; private set; }
        public BuildingDef Def { get; private set; }
        public BuildingViewSync Sync { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }
        public bool Hovered { get; private set; }
        public bool Selected { get; private set; }
        /// <summary>Demolish-mode hover: outline turns red.</summary>
        public bool DemolishHover { get; private set; }
        /// <summary>Reach circle forced on (link editor open: every stone of the region shows its circle).</summary>
        public bool ReachForced { get; private set; }
        /// <summary>Delivered pixel art is drawn as the body (grey panel + small icon hidden).</summary>
        public bool RealArt { get; private set; }
        /// <summary>World size of the body art (units; 1 unit = 1 cell at PPU 32) and how far it rises above the footprint top.</summary>
        public Vector2 ArtSize { get; private set; }
        public float ArtTop { get; private set; }
        SpriteRenderer art;
        Sprite[] baseFrames, activeFrames;
        /// <summary>Code animation of the still (code-anim.json, owner pick anim_route): replaces any delivered working loop.</summary>
        CodeAnimator codeAnim;
        public CodeAnimator CodeAnim => codeAnim;
        static readonly string[] ActiveVariants = { "working", "glow", "pulse", "occupied" };
        const float IdleFps = 6f, ActiveFps = 8f;
        internal int seenFrame;
        internal GameObject sourcePrefab;

        BuildingFace[] faces;
        bool replaces;
        int lastBuilt = -1;
        readonly List<IconRow.Entry> needs = new List<IconRow.Entry>();
        WorldLabel needsLabel;
        SpriteRenderer needsPlate;
        int[] needLeft = new int[0], needHave = new int[0];
        string[] needText = new string[0];
        readonly List<bool> needOk = new List<bool>();
        static readonly string NeedOk = ColorUtility.ToHtmlStringRGB(WorldLabel.GoodColour), NeedBad = ColorUtility.ToHtmlStringRGB(WorldLabel.BadColour), NeedGold = ColorUtility.ToHtmlStringRGB(WorldLabel.GoldColour);
        /// <summary>The ghost's remaining-cost tag ("have/need" per item), for automation.</summary>
        public string NeedsShown { get { if (needsLabel == null || !needsLabel.isActiveAndEnabled) return null; var s = ""; foreach (var e in needs) s += e.item + " " + e.label + " "; return s.Trim(); } }

        int HandCount(string item)
        {
            int n = 0;
            var hand = Sync.Runner.State.hand;
            for (int i = 0; i < hand.Count; i++) if (hand[i].item == item) n += hand[i].qty;
            return n;
        }

        public IReadOnlyList<BuildingFace> Faces => faces;
        public SpriteRenderer Panel => panel;
        /// <summary>The real building art (null for placeholder buildings); used to anchor building panels beside the art.</summary>
        public SpriteRenderer ArtRenderer => RealArt ? art : null;
        public EdgeFrame Border => border;
        public float BorderUnits => ViewKit.U(2f);

        void Awake() => faces = GetComponentsInChildren<BuildingFace>(true);

        /// <summary>Local position of a footprint fraction (fx right, fy down).</summary>
        public Vector3 L(float fx, float fy) => new Vector3(fx * W, -fy * H, 0f);

        public void Bind(BuildingViewSync sync, string area, Building b, BuildingDef def)
        {
            faces ??= GetComponentsInChildren<BuildingFace>(true);
            Sync = sync; Area = area; Building = b; Def = def;
            W = def != null ? def.sizeW : 3; H = def != null ? def.sizeH : 3;
            name = $"Building_{area}_{b.id}_{b.type}";
            transform.position = sync.Space.PxToWorld(area, b.col * sync.Space.Cell, b.row * sync.Space.Cell);
            basePos = transform.position; popStart = -1f; transform.localScale = Vector3.one;
            replaces = false;
            foreach (var f in faces) if (f.ReplacesDefault) replaces = true;

            panel.drawMode = SpriteDrawMode.Simple;
            panel.transform.localPosition = new Vector3(W * 0.5f, -H * 0.5f, 0f);
            panel.transform.localScale = new Vector3(W, H, 1f);

            var artSprite = sync.Sprites.BuildingArt(b.type);
            RealArt = artSprite != null;
            if (codeAnim != null) { codeAnim.Clear(); codeAnim = null; }
            if (RealArt)
            {
                if (art == null)
                {
                    var go = new GameObject("Art");
                    go.transform.SetParent(transform, false);
                    art = go.AddComponent<SpriteRenderer>();
                    art.sortingLayerID = panel.sortingLayerID;
                    art.sortingOrder = panel.sortingOrder + 1;
                }
                art.sprite = artSprite;
                var bf = sync.Sprites.BuildingFrames(b.type);
                baseFrames = bf != null && bf.Length > 0 ? bf : null;
                activeFrames = null;
                foreach (var vn in ActiveVariants) { activeFrames = sync.Sprites.BuildingVariant(b.type, vn); if (activeFrames != null) break; }
                float k = ViewKit.NativeScale(artSprite);      // native size: PPU 32 or 64 (obj64, ART-SPEC §3.9) = bounds in cells
                art.transform.localScale = new Vector3(k, k, 1f);
                art.transform.localPosition = new Vector3(W * 0.5f, -H, 0f);   // bottom-centre pivot on the footprint's bottom edge
                ArtSize = new Vector2(artSprite.bounds.size.x * k, artSprite.bounds.size.y * k);
                BindCodeAnim(sync, b.type, artSprite);
                ArtTop = Mathf.Max(0f, ArtSize.y - H);
                // VP-13: 1-texel rim on the texel grid, 1 texel outside the art
                float pad = ViewKit.U(1f);
                float hx = Mathf.Round((W * 0.5f - ArtSize.x * 0.5f - pad) * ViewKit.Cell) / ViewKit.Cell;
                highlight.transform.localPosition = new Vector3(hx, -H + ArtSize.y + pad, 0f);
                highlight.Set(Mathf.Round((ArtSize.x + 2f * pad) * ViewKit.Cell) / ViewKit.Cell, ArtSize.y + 2f * pad, ViewKit.U(1f), Color.white, false);
            }
            else
            {
                ArtSize = Vector2.zero; ArtTop = 0f;
                highlight.transform.localPosition = new Vector3(-ViewKit.U(2f), ViewKit.U(2f), 0f);
                highlight.Set(W + ViewKit.U(4f), H + ViewKit.U(4f), ViewKit.U(1f), Color.white, false);
            }
            if (art != null) ViewKit.Show(art, RealArt);
            ViewKit.Show(highlight, false);

            var sprite = sync.Sprites.Building(b.type);
            bool small = W <= 1 && H <= 1;
            ViewKit.Fit(icon, sprite, small ? 20f : 24f);
            icon.transform.localPosition = small ? L(0.5f, 0.5f) : L(0.5f, 0.38f);
            ViewKit.Font(nameText, 10f);
            nameText.text = def != null ? def.name : b.type;
            nameText.rectTransform.sizeDelta = new Vector2(W - ViewKit.U(4f), ViewKit.U(14f));
            nameText.transform.localPosition = L(0.5f, 0.66f);
            // real art covers the footprint: the ghost's remaining-cost line sits just below it
            // VP-20: the ghost's remaining cost is a plated tag with "in hand / still needed" per item
            if (needsLabel == null)
            {
                needsLabel = WorldLabel.Create(transform, "NeedsTag");
                needsLabel.priority = 15;
                needsPlate = WorldLabel.NewPlate(needsLabel.content);
                needsRow.transform.SetParent(needsLabel.content, false);
                needsRow.MarkFor = i => i >= 0 && i < needOk.Count && needOk[i] ? PixelSprites.Tick : PixelSprites.Cross;
            }
            needsLabel.SetAnchor(RealArt ? new Vector3(W * 0.5f, -H - ViewKit.U(9f), 0f) : L(0.5f, small ? 1.4f : 0.86f));
            needsRow.transform.localPosition = Vector3.zero;
            foreach (var r in needsRow.GetComponentsInChildren<Renderer>(true)) WorldLabel.Sort(r, r is MeshRenderer ? WorldLabel.OrderContent + 1 : WorldLabel.OrderContent);
            int nc = def != null ? def.cost.Count : 0;
            if (needLeft.Length != nc) { needLeft = new int[nc]; needHave = new int[nc]; }
            for (int i = 0; i < nc; i++) needLeft[i] = needHave[i] = int.MinValue;
            needsRow.Clear();

            foreach (var f in faces) f.Layout(this);
            lastBuilt = -1;
            Hovered = Selected = DemolishHover = false;
            Refresh();
        }

        /// <summary>Swap the (animated) body art, e.g. the Dragon waking up. Cheap: no allocation.</summary>
        public void SetBodyFrames(Sprite[] frames)
        {
            if (!RealArt || frames == null || frames.Length == 0 || ReferenceEquals(baseFrames, frames)) return;
            baseFrames = frames; art.sprite = frames[0];
        }

        void BindCodeAnim(BuildingViewSync sync, string type, Sprite still)
        {
            var entry = CodeAnimLibrary.Find(Data.ArtKeys.Building(type));
            if (entry == null || !entry.HasEffects) return;
            var ca = CodeAnimator.For(art);
            ca.SelfTick = false;
            // artist layers (ART-SPEC §3.9.8) arrive as building variants: bld_<type>_layer_<part>, bld_<type>_base
            if (ca.Configure(entry, art, still, suffix => { var fr = sync.Sprites.BuildingVariant(type, suffix); return fr != null && fr.Length > 0 ? fr[0] : null; }))
                codeAnim = ca;
        }

        /// <summary>Plays the body strip at 6 fps, or the working/glow/pulse/occupied variant at 8 fps while a face reports the building active; static art otherwise.
        /// A code animation (code-anim.json) takes over while active and replaces the delivered variant.</summary>
        void AnimateArt()
        {
            bool act = false;
            if (activeFrames != null || codeAnim != null) foreach (var f in faces) if (f.IsActive(this)) { act = true; break; }
            if (codeAnim != null)
            {
                codeAnim.Playing = act;
                codeAnim.Tick(CodeAnimClock.Now);
                if (act) return;
            }
            var set = act ? activeFrames : baseFrames;
            if (set == null || set.Length == 0) return;
            var s = set.Length > 1 ? set[(int)(Time.unscaledTime * (act ? ActiveFps : IdleFps)) % set.Length] : set[0];
            if (art.sprite != s) art.sprite = s;
        }

        public void SetHighlight(bool hovered, bool selected, bool demolish, bool reach = false)
        {
            if (hovered == Hovered && selected == Selected && demolish == DemolishHover && reach == ReachForced) return;
            Hovered = hovered; Selected = selected; DemolishHover = demolish; ReachForced = reach;
            bool on = hovered || selected || demolish;
            ViewKit.Show(highlight, on);
            if (on) highlight.SetColour(demolish ? PixelSprites.C(PixelSprites.P18) : selected ? PixelSprites.C(PixelSprites.P24) : PixelSprites.C(PixelSprites.P07, 0.9f));
            foreach (var f in faces) f.SetHighlight(this, (hovered || selected || reach) && !demolish);
        }

        Vector3 basePos;
        float popStart = -1f;
        const float PopSec = 0.18f;

        /// <summary>Juice: brief scale pop about the footprint's bottom-centre when a ghost completes.</summary>
        public void Pop() { if (!Juice.ReduceMotion) popStart = Time.unscaledTime; }

        void ApplyPop()
        {
            if (popStart < 0f) return;
            float t = (Time.unscaledTime - popStart) / PopSec;
            float s = t >= 1f ? 1f : 1f + 0.12f * Mathf.Sin(Mathf.PI * t);
            if (t >= 1f) popStart = -1f;
            transform.localScale = new Vector3(s, s, 1f);
            transform.position = basePos + new Vector3(W * 0.5f, -H, 0f) * (1f - s);
        }

        public void Refresh()
        {
            var b = Building;
            ApplyPop();
            int built = b.built ? 1 : 0;
            if (built != lastBuilt)
            {
                lastBuilt = built;
                panel.color = b.built ? builtFill : GhostFill;
                border.transform.localPosition = Vector3.zero;
                border.Set(W, H, BorderUnits, b.built ? builtBorder : GhostBorder, !b.built);
                bool showDefault = !b.built || !replaces;
                ViewKit.Show(panel, !RealArt);
                ViewKit.Show(border, !RealArt || !b.built);       // real art: only the ghost keeps its dashed footprint outline
                if (RealArt) { var ac = art.color; ac.a = b.built ? 1f : 0.5f; art.color = ac; }
                ViewKit.Show(icon, showDefault && !RealArt);
                ViewKit.Show(nameText, showDefault && !RealArt && !(W <= 1 && H <= 1));
                var ic = icon.color; ic.a = b.built ? 1f : 0.7f; icon.color = ic;
                ViewKit.Show(needsRow, !b.built);
                if (needsLabel != null) ViewKit.Show(needsLabel, !b.built);
                foreach (var f in faces) ViewKit.Show(f.gameObject, b.built);
                if (b.built) needsRow.Clear();
            }
            if (!b.built)
            {
                // = Sim.BuildingNeeds(b) (cost − paid) without the per-frame ItemCounts allocation; label "have/need"
                // strings are rebuilt only when a remaining amount or the hand count changes
                bool changed = false;
                if (Def != null)
                {
                    if (needText.Length != Def.cost.Count) needText = new string[Def.cost.Count];
                    for (int i = 0; i < Def.cost.Count && i < needLeft.Length; i++)
                    {
                        var c = Def.cost[i];
                        int r = c.qty - b.paid.Get(c.item), h = r > 0 ? HandCount(c.item) : 0;
                        if (r != needLeft[i] || h != needHave[i])
                        {
                            changed = true; needLeft[i] = r; needHave[i] = h;
                            needText[i] = r > 0 ? "<color=#" + (h >= r ? NeedOk : NeedBad) + ">" + h + "</color><color=#" + NeedGold + ">/" + r + "</color>" : null;
                        }
                    }
                }
                if (changed || needs.Count == 0)
                {
                    needs.Clear(); needOk.Clear();
                    if (Def != null)
                        for (int i = 0; i < Def.cost.Count && i < needText.Length; i++)
                            if (needText[i] != null) { needs.Add(new IconRow.Entry(Def.cost[i].item, needText[i])); needOk.Add(needHave[i] >= needLeft[i]); }
                    needsRow.Set(needs, 9f, UiPalette.Gold, Sync.Sprites, null, 0f);
                    float pw = needsRow.Width + ViewKit.U(8f), ph = needsRow.Height + ViewKit.U(5f);
                    WorldLabel.SizePlate(needsPlate, 0f, 0f, pw, ph);
                    needsLabel.SetBounds(-pw * 0.5f, -ph * 0.5f, pw, ph);
                    ViewKit.Show(needsPlate, needs.Count > 0);
                }
                return;
            }
            foreach (var f in faces) f.Refresh(this);
            if (RealArt) AnimateArt();
        }
    }
}
