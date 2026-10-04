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

        const float FxFps = 8f;
        Sprite glowCircle;
        Sprite[] fxNormal, fxReturning;
        bool useFx;
        float phase;

        public void Bind(string area, Wisp w, Sprite cargoSprite, Sprite[] fxFrames = null, Sprite[] fxReturningFrames = null)
        {
            Area = area; Wisp = w;
            name = $"Wisp_{area}_{w.id}";
            if (glowCircle == null) glowCircle = glow.sprite;
            fxNormal = fxFrames; fxReturning = fxReturningFrames != null ? fxReturningFrames : fxFrames;
            useFx = fxNormal != null && fxNormal.Length > 0;
            phase = (w.id * 0.37f) % 1f;      // per-wisp animation phase offset
            if (useFx)
            {
                glow.transform.localScale = Vector3.one;   // native size (12 px at PPU 32)
                glow.color = Color.white;
                core.enabled = false;
            }
            else
            {
                float glowSize = ViewKit.U(14f), coreSize = ViewKit.U(5f);
                glow.sprite = glowCircle;
                glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
                core.transform.localScale = new Vector3(coreSize, coreSize, 1f);
                core.color = CoreColour;
                core.enabled = true;
                glow.color = new Color(0, 0, 0, 0);
            }
            ViewKit.Fit(cargo, cargoSprite, 14f);
            cargo.transform.localPosition = new Vector3(0f, ViewKit.U(12f), 0f);
            ViewKit.Show(cargo, cargoSprite != null);
            Red = !w.returning;      // force the colour write on the first Refresh
            Refresh(transform.position);
        }

        public void Refresh(Vector3 world)
        {
            transform.position = world;
            bool red = Wisp.returning;
            if (useFx)
            {
                Red = red;
                var frames = red ? fxReturning : fxNormal;
                int i = (int)(Time.time * FxFps + phase * frames.Length) % frames.Length;
                glow.sprite = frames[i];
                return;
            }
            if (red != Red || glow.color.a == 0f)
            {
                Red = red;
                glow.color = red ? GlowRed : GlowGreen;
            }
        }
    }
}
