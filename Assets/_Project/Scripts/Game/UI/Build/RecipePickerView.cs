using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Recipe picker (ui-input-render §4.3): left-click a built converter → popup centred above the
    /// building top (follows the camera, clamped on screen): "Recipes" + a 3-column icon grid. Hover a
    /// cell → the detail panel (bottom-right): output icon, name, gold "→ qty× Item · secs s" (secs =
    /// timeMs × prestigeFactor), one row per input (icon + gold count badge + name). Click =
    /// <see cref="Simulation.SetRecipe"/> + close. Closes when the building disappears / unbuilds.
    /// </summary>
    public class RecipePickerView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] Camera worldCamera;
        [SerializeField] RectTransform popup;
        [SerializeField] RectTransform grid;
        [SerializeField] RecipeCell cellTemplate;
        [SerializeField] RectTransform detail;
        [SerializeField] Image detailIcon;
        [SerializeField] TextMeshProUGUI detailName;
        [SerializeField] TextMeshProUGUI detailYield;
        [SerializeField] RectTransform detailRows;
        [SerializeField] UiIconCount rowTemplate;

        readonly List<RecipeCell> cells = new List<RecipeCell>();
        readonly List<UiIconCount> rows = new List<UiIconCount>();
        SpriteCache sprites;
        RectTransform canvasRect;
        Canvas canvas;

        public bool IsOpen => popup != null && popup.gameObject.activeSelf;
        public string Area { get; private set; }
        public int BuildingId { get; private set; }
        public int HoverIndex { get; private set; } = -1;
        public IReadOnlyList<RecipeCell> Cells => cells;

        Simulation Sim => runner.Sim;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (worldCamera == null) worldCamera = Camera.main;
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (popup != null) popup.gameObject.SetActive(false);
            if (detail != null) detail.gameObject.SetActive(false);
        }

        Building Target => Sim?.State.Area(Area)?.BuildingById(BuildingId);

        public void Open(string area, Building b)
        {
            var def = runner.Config.Building(b.type);
            if (def == null || !def.IsConverter) return;
            sprites ??= new SpriteCache(runner.Database, null);
            Area = area; BuildingId = b.id; HoverIndex = -1;
            var recs = def.recipes;
            while (cells.Count < recs.Count) cells.Add(Instantiate(cellTemplate, grid));
            for (int i = 0; i < cells.Count; i++)
            {
                bool on = i < recs.Count;
                cells[i].gameObject.SetActive(on);
                if (on) cells[i].Bind(this, i, sprites.Item(recs[i].output), BadgeFor(recs, i), i == b.recipe);
            }
            popup.gameObject.SetActive(true);
            detail.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
            Position();
        }

        /// <summary>Same-output siblings: the first input of this recipe that some sibling lacks.</summary>
        Sprite BadgeFor(List<RecipeDef> recs, int i)
        {
            var r = recs[i];
            bool sibling = false;
            foreach (var o in recs) if (o != r && o.output == r.output) { sibling = true; break; }
            if (!sibling) return null;
            foreach (var inp in r.inputs)
                foreach (var o in recs)
                {
                    if (o == r || o.output != r.output) continue;
                    if (!o.inputs.Exists(x => x.item == inp.item)) return sprites.Item(inp.item);
                }
            return null;
        }

        public void Close()
        {
            if (popup != null) popup.gameObject.SetActive(false);
            if (detail != null) detail.gameObject.SetActive(false);
            HoverIndex = -1;
        }

        public void Select(int index)
        {
            if (!IsOpen) return;
            Sim.SetRecipe(Area, BuildingId, index);
            Close();
        }

        public void Hover(int index)
        {
            HoverIndex = index;
            var b = Target;
            var def = b != null ? runner.Config.Building(b.type) : null;
            if (index < 0 || def == null || index >= def.recipes.Count) { detail.gameObject.SetActive(false); return; }
            var r = def.recipes[index];
            detail.gameObject.SetActive(true);
            detailIcon.sprite = sprites.Item(r.output);
            string outName = runner.Config.Item(r.output)?.name ?? r.output;
            detailName.text = string.IsNullOrEmpty(r.name) ? outName : r.name;
            double secs = r.timeMs * Sim.Timing.PrestigeFactor(Sim.State) / 1000.0;
            detailYield.text = $"Makes {r.outputQty}× {outName} · " + secs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";   // ui.js "→ …" (no → glyph in LiberationSans)
            while (rows.Count < r.inputs.Count) rows.Add(Instantiate(rowTemplate, detailRows));
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < r.inputs.Count;
                rows[i].gameObject.SetActive(on);
                if (on) rows[i].Set(sprites.Item(r.inputs[i].item), r.inputs[i].qty.ToString(), runner.Config.Item(r.inputs[i].item)?.name ?? r.inputs[i].item);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
        }

        void LateUpdate()
        {
            if (!IsOpen || runner.Sim == null) return;
            var b = Target;
            if (b == null || !b.built) { Close(); return; }
            foreach (var c in cells) if (c.gameObject.activeSelf) c.SetActive(c.Index == b.recipe);
            Position();
        }

        /// <summary>Centred above the building's top edge, clamped inside the canvas.</summary>
        void Position()
        {
            var b = Target;
            if (b == null || canvasRect == null || worldCamera == null) return;
            var def = runner.Config.Building(b.type);
            int cell = runner.Space.Cell;
            var top = runner.Space.PxToWorld(Area, (b.col + def.sizeW * 0.5) * cell, b.row * cell);
            Vector2 screen = worldCamera.WorldToScreenPoint(top);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out var local);
            var size = popup.rect.size;
            var half = canvasRect.rect.size * 0.5f;
            // pivot (0.5, 0): bottom-centre 8 px above the building
            float x = Mathf.Clamp(local.x, -half.x + size.x * 0.5f + 8f, half.x - size.x * 0.5f - 8f);
            float y = Mathf.Clamp(local.y + 8f, -half.y + 90f, half.y - size.y - 8f);
            popup.anchoredPosition = new Vector2(x, y);
        }
    }
}
