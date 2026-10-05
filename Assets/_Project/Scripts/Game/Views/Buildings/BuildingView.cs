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
        static readonly string[] ActiveVariants = { "working", "glow", "pulse", "occupied" };
        const float IdleFps = 6f, ActiveFps = 8f;
        internal int seenFrame;
        internal GameObject sourcePrefab;

        BuildingFace[] faces;
        bool replaces;
        int lastBuilt = -1;
        readonly List<IconRow.Entry> needs = new List<IconRow.Entry>();

        public IReadOnlyList<BuildingFace> Faces => faces;
        public SpriteRenderer Panel => panel;
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
            replaces = false;
            foreach (var f in faces) if (f.ReplacesDefault) replaces = true;

            panel.drawMode = SpriteDrawMode.Simple;
            panel.transform.localPosition = new Vector3(W * 0.5f, -H * 0.5f, 0f);
            panel.transform.localScale = new Vector3(W, H, 1f);

            var artSprite = sync.Sprites.BuildingArt(b.type);
            RealArt = artSprite != null;
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
                float k = artSprite.pixelsPerUnit / ViewKit.Cell;      // native pixels: PPU 32 = 1 unit per cell
                art.transform.localScale = new Vector3(k, k, 1f);
                art.transform.localPosition = new Vector3(W * 0.5f, -H, 0f);   // bottom-centre pivot on the footprint's bottom edge
                ArtSize = new Vector2(artSprite.bounds.size.x * k, artSprite.bounds.size.y * k);
                ArtTop = Mathf.Max(0f, ArtSize.y - H);
                float pad = ViewKit.U(2f);
                highlight.transform.localPosition = new Vector3(W * 0.5f - ArtSize.x * 0.5f - pad, -H + ArtSize.y + pad, 0f);
                highlight.Set(ArtSize.x + 2f * pad, ArtSize.y + 2f * pad, ViewKit.U(2f), Color.white, false);
            }
            else
            {
                ArtSize = Vector2.zero; ArtTop = 0f;
                highlight.transform.localPosition = new Vector3(-ViewKit.U(3f), ViewKit.U(3f), 0f);
                highlight.Set(W + ViewKit.U(6f), H + ViewKit.U(6f), ViewKit.U(2f), Color.white, false);
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
            needsRow.transform.localPosition = RealArt ? new Vector3(W * 0.5f, -H - ViewKit.U(9f), 0f) : L(0.5f, small ? 1.4f : 0.86f);
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

        /// <summary>Plays the body strip at 6 fps, or the working/glow/pulse/occupied variant at 8 fps while a face reports the building active; static art otherwise.</summary>
        void AnimateArt()
        {
            bool act = false;
            if (activeFrames != null) foreach (var f in faces) if (f.IsActive(this)) { act = true; break; }
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
            if (on) highlight.SetColour(demolish ? UiPalette.Danger : selected ? UiPalette.Gold : new Color(1f, 1f, 1f, 0.55f));
            foreach (var f in faces) f.SetHighlight(this, (hovered || selected || reach) && !demolish);
        }

        public void Refresh()
        {
            var b = Building;
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
                foreach (var f in faces) ViewKit.Show(f.gameObject, b.built);
                if (b.built) needsRow.Clear();
            }
            if (!b.built)
            {
                needs.Clear();
                // = Sim.BuildingNeeds(b) (cost − paid) without the per-frame ItemCounts allocation
                if (Def != null)
                    foreach (var c in Def.cost) { int r = c.qty - b.paid.Get(c.item); if (r > 0) needs.Add(new IconRow.Entry(c.item, r)); }
                needsRow.Set(needs, 10f, UiPalette.Gold, Sync.Sprites, null, W - ViewKit.U(4f));
                return;
            }
            foreach (var f in faces) f.Refresh(this);
            if (RealArt) AnimateArt();
        }
    }
}
