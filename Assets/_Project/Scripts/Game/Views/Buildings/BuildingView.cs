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
            highlight.transform.localPosition = new Vector3(-ViewKit.U(3f), ViewKit.U(3f), 0f);
            highlight.Set(W + ViewKit.U(6f), H + ViewKit.U(6f), ViewKit.U(2f), Color.white, false);
            ViewKit.Show(highlight, false);

            var sprite = sync.Sprites.Building(b.type);
            bool small = W <= 1 && H <= 1;
            ViewKit.Fit(icon, sprite, small ? 20f : 24f);
            icon.transform.localPosition = small ? L(0.5f, 0.5f) : L(0.5f, 0.38f);
            ViewKit.Font(nameText, 10f);
            nameText.text = def != null ? def.name : b.type;
            nameText.rectTransform.sizeDelta = new Vector2(W - ViewKit.U(4f), ViewKit.U(14f));
            nameText.transform.localPosition = L(0.5f, 0.66f);
            needsRow.transform.localPosition = L(0.5f, small ? 1.4f : 0.86f);
            needsRow.Clear();

            foreach (var f in faces) f.Layout(this);
            lastBuilt = -1;
            Hovered = Selected = DemolishHover = false;
            Refresh();
        }

        public void SetHighlight(bool hovered, bool selected, bool demolish)
        {
            if (hovered == Hovered && selected == Selected && demolish == DemolishHover) return;
            Hovered = hovered; Selected = selected; DemolishHover = demolish;
            bool on = hovered || selected || demolish;
            ViewKit.Show(highlight, on);
            if (on) highlight.SetColour(demolish ? UiPalette.Danger : selected ? UiPalette.Gold : new Color(1f, 1f, 1f, 0.55f));
            foreach (var f in faces) f.SetHighlight(this, (hovered || selected) && !demolish);
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
                ViewKit.Show(icon, showDefault);
                ViewKit.Show(nameText, showDefault && !(W <= 1 && H <= 1));
                var ic = icon.color; ic.a = b.built ? 1f : 0.7f; icon.color = ic;
                ViewKit.Show(needsRow, !b.built);
                foreach (var f in faces) ViewKit.Show(f.gameObject, b.built);
                if (b.built) needsRow.Clear();
            }
            if (!b.built)
            {
                needs.Clear();
                foreach (var e in Sync.Sim.BuildingNeeds(b)) needs.Add(new IconRow.Entry(e.item, e.qty));
                needsRow.Set(needs, 10f, UiPalette.Gold, Sync.Sprites, null, W - ViewKit.U(4f));
                return;
            }
            foreach (var f in faces) f.Refresh(this);
        }
    }
}
