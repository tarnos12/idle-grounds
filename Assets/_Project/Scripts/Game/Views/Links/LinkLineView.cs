using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A dashed thread between two world points (spec 2.3 link threads / 2.7 rubber band): a tiled
    /// dash sprite stretched + rotated between the endpoints, plus an optional status dot at the
    /// midpoint. Pooled by <see cref="LinkLineSync"/>; also used directly for the rubber band.
    /// </summary>
    public class LinkLineView : MonoBehaviour
    {
        public static readonly Color DotGrey = UiPalette.Hex("#64748b"), DotGreen = UiPalette.Accent, DotAmber = UiPalette.Amber, DotRed = UiPalette.Danger;

        [SerializeField] SpriteRenderer line;
        [SerializeField] SpriteRenderer dot;

        internal int seenFrame;
        public Vector3 From { get; private set; }
        public Vector3 To { get; private set; }
        public Color LineColour => line.color;
        public bool DotVisible => dot != null && dot.gameObject.activeSelf;

        public void Set(Vector3 a, Vector3 b, Color c, float widthPx)
        {
            From = a; To = b;
            var d = b - a;
            float len = d.magnitude;
            var t = line.transform;
            t.position = (a + b) * 0.5f;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.size = new Vector2(Mathf.Max(len, 0.001f), Mathf.Max(1f, widthPx) / ViewKit.Cell);
            line.color = c;
        }

        public void SetDot(bool show, Color c)
        {
            if (dot == null) return;
            ViewKit.Show(dot, show);
            if (!show) return;
            dot.transform.position = (From + To) * 0.5f;
            dot.color = c;
        }

        public static Color DotColour(IdleGrounds.Sim.LinkDot d)
        {
            switch (d)
            {
                case IdleGrounds.Sim.LinkDot.Green: return DotGreen;
                case IdleGrounds.Sim.LinkDot.Amber: return DotAmber;
                case IdleGrounds.Sim.LinkDot.Red: return DotRed;
                default: return DotGrey;
            }
        }
    }
}
