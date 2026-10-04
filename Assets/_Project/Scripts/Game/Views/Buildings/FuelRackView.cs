using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Burner fuel rack (ui.js U:957; spec §2.4): 3 cols x 2 rows OUTSIDE the footprint on the LEFT
    /// (x = building.x − 3 cells), top edge flush with the building top. Slots fill oldest → newest
    /// row-major; slot 0 (burning) shows a 22% ghost icon plus the real icon clipped (SpriteMask) to
    /// its left rem/total fraction. Empty rack: red "No fuel" just above it.
    /// </summary>
    public class FuelRackView : BuildingFace
    {
        public const int Cols = 3, Rows = 2;
        public static readonly Color PanelFill = ViewKit.Rgba(40, 28, 18, 0.82f), PanelBorder = ViewKit.Rgba(251, 146, 60, 0.5f);

        [SerializeField] SpriteRenderer panel;
        [SerializeField] EdgeFrame frame;
        [SerializeField] SpriteRenderer[] dividers = new SpriteRenderer[3];
        [SerializeField] SpriteRenderer[] slots = new SpriteRenderer[Cols * Rows];
        [SerializeField] SpriteRenderer burningGhost;
        [SerializeField] SpriteMask burnMask;
        [SerializeField] TextMeshPro noFuelText;

        public override bool ReplacesDefault => false;   // the converter face draws the footprint
        public int ShownSlots { get; private set; }
        ConverterFace lastFace;

        public override void Layout(BuildingView v)
        {
            lastFace = null;
            transform.localPosition = new Vector3(-Cols, 0f, 0f);
            panel.transform.localPosition = new Vector3(Cols * 0.5f, -Rows * 0.5f, 0f);
            panel.transform.localScale = new Vector3(Cols, Rows, 1f);
            panel.color = PanelFill;
            frame.transform.localPosition = Vector3.zero;
            frame.Set(Cols, Rows, ViewKit.U(1.5f), PanelBorder, false);
            var div = new Color(1f, 1f, 1f, 0.07f);
            float t = ViewKit.U(1f);
            if (dividers.Length >= 3)
            {
                ViewKit.Bar(dividers[0], 1f - t * 0.5f, -1f, t, Rows); dividers[0].color = div;
                ViewKit.Bar(dividers[1], 2f - t * 0.5f, -1f, t, Rows); dividers[1].color = div;
                ViewKit.Bar(dividers[2], 0f, -1f, Cols, t); dividers[2].color = div;
            }
            for (int i = 0; i < slots.Length; i++)
                slots[i].transform.localPosition = SlotCentre(i);
            burningGhost.transform.localPosition = SlotCentre(0);
            noFuelText.transform.localPosition = new Vector3(Cols * 0.5f, ViewKit.U(8f), 0f);
            ViewKit.Font(noFuelText, 10f);
        }

        static Vector3 SlotCentre(int i) => new Vector3(i % Cols + 0.5f, -(i / Cols) - 0.5f, 0f);

        public override void Refresh(BuildingView v)
        {
            var conv = v.Faces != null ? FindConverter(v) : null;
            // the converter face samples Sim.ConverterFace at 5 Hz; reuse it (no second allocation per frame)
            var f = conv != null ? conv.LastFace : v.Sync.Sim.ConverterFace(v.Area, v.Building);
            if (conv != null && ReferenceEquals(f, lastFace)) return;
            lastFace = f;
            int n = f != null ? Mathf.Min(f.fuel.Count, slots.Length) : 0;
            ShownSlots = n;
            var sprites = v.Sync.Sprites;
            for (int i = 0; i < slots.Length; i++)
            {
                bool on = i < n;
                ViewKit.Show(slots[i], on);
                if (on) ViewKit.Fit(slots[i], sprites.Item(f.fuel[i].item), 0.8f * ViewKit.Cell);
            }
            ViewKit.Show(burningGhost, n > 0);
            ViewKit.Show(burnMask, n > 0);
            if (n > 0)
            {
                var s0 = f.fuel[0];
                ViewKit.Fit(burningGhost, sprites.Item(s0.item), 0.8f * ViewKit.Cell);
                var gc = burningGhost.color; gc.a = 0.22f; burningGhost.color = gc;
                float frac = s0.total > 0 ? Mathf.Clamp01((float)(s0.rem / s0.total)) : 1f;
                burnMask.transform.localPosition = new Vector3(frac * 0.5f, -0.5f, 0f);
                burnMask.transform.localScale = new Vector3(Mathf.Max(0.0001f, frac), 1f, 1f);
            }
            ViewKit.Show(noFuelText, n == 0);
        }

        static ConverterFaceView FindConverter(BuildingView v)
        {
            foreach (var x in v.Faces) if (x is ConverterFaceView c) return c;
            return null;
        }
    }
}
