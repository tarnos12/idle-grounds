using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One wisp (spec 2.7): glow circle r 7 px rgba(74,222,128,.30) (red rgba(248,113,113,.35) while
    /// returning = delivery refused), core #eafff2 r 2.5 px, cargo icon 14 px, 12 px above. Pooled by
    /// <see cref="WispViewSync"/>.
    /// </summary>
    public class WispView : MonoBehaviour
    {
        public static readonly Color GlowGreen = ViewKit.Rgba(74, 222, 128, 0.30f), GlowRed = ViewKit.Rgba(248, 113, 113, 0.35f);
        public static readonly Color CoreColour = UiPalette.Hex("#eafff2");

        [SerializeField] SpriteRenderer glow;
        [SerializeField] SpriteRenderer core;
        [SerializeField] SpriteRenderer cargo;

        internal int seenFrame;
        public string Area { get; private set; }
        public Wisp Wisp { get; private set; }
        public bool Red { get; private set; }

        public void Bind(string area, Wisp w, Sprite cargoSprite)
        {
            Area = area; Wisp = w;
            name = $"Wisp_{area}_{w.id}";
            float glowSize = ViewKit.U(14f), coreSize = ViewKit.U(5f);
            glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
            core.transform.localScale = new Vector3(coreSize, coreSize, 1f);
            core.color = CoreColour;
            ViewKit.Fit(cargo, cargoSprite, 14f);
            cargo.transform.localPosition = new Vector3(0f, ViewKit.U(12f), 0f);
            ViewKit.Show(cargo, cargoSprite != null);
            Red = !w.returning;      // force the colour write on the first Refresh
            glow.color = new Color(0, 0, 0, 0);
            Refresh(transform.position);
        }

        public void Refresh(Vector3 world)
        {
            transform.position = world;
            bool red = Wisp.returning;
            if (red != Red || glow.color.a == 0f)
            {
                Red = red;
                glow.color = red ? GlowRed : GlowGreen;
            }
        }
    }
}
