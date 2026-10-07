using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Juice &amp; feel shared state (no allocations at steady state): the persisted "Reduce motion" flag
    /// (PlayerPrefs "ig_reduce_motion"), the tiny camera-shake channel, the hand-chip punch / full pulse
    /// timestamps, the recent ground-drop origins (so a dropped item can arc out of its node) and the
    /// palette colours (ART-SPEC §1.3) used by the FX. Reduce motion turns off shake, squash/pop tweens,
    /// arcs/flight paths, bobs and ambient motes; plain particle bursts and floaters stay.
    /// </summary>
    public static class Juice
    {
        public const string ReducePref = "ig_reduce_motion";

        // palette (ART-SPEC §1.3)
        public static readonly Color Wood = UiPalette.Hex("#A8703A"), Rock = UiPalette.Hex("#B7BCCD"), Jade = UiPalette.Hex("#5FCF9C"),
            Leaf = UiPalette.Hex("#A8ECC0"), Gold = UiPalette.Hex("#F2C94C"), GoldGlow = UiPalette.Hex("#FFF0A0"),
            Cyan = UiPalette.Hex("#5ED4E8"), CyanCore = UiPalette.Hex("#C4F6FF"), Water = UiPalette.Hex("#2B86B3"),
            Sand = UiPalette.Hex("#D9A45C"), Mist = UiPalette.Hex("#E8ECF3"), Violet = UiPalette.Hex("#7D5FC4"),
            Lava = UiPalette.Hex("#FF8A1F"), Dust = new Color(0.72f, 0.74f, 0.80f, 0.85f);

        static bool loaded, reduce;
        public static event System.Action ReduceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false; shakeStart = -10f; shakeDur = 0f; HandPunchAt = HandFullAt = -10f; dropN = 0; ReduceChanged = null;
        }

        public static bool ReduceMotion
        {
            get { if (!loaded) { reduce = PlayerPrefs.GetInt(ReducePref, 0) == 1; loaded = true; } return reduce; }
        }

        public static void SetReduceMotion(bool v)
        {
            loaded = true; reduce = v;
            PlayerPrefs.SetInt(ReducePref, v ? 1 : 0);
            PlayerPrefs.Save();
            ReduceChanged?.Invoke();
        }

        // ---- camera shake: 2-frame-ish, camera-offset based ----
        static float shakeStart = -10f, shakeDur, shakeAmp;

        /// <summary>Tiny camera shake: <paramref name="amp"/> world units for <paramref name="dur"/> seconds (ignored when reduced).</summary>
        public static void Shake(float amp, float dur = 0.05f)
        {
            if (ReduceMotion) return;
            shakeStart = Time.unscaledTime; shakeDur = dur; shakeAmp = amp;
        }

        public static bool ShakeActive => !ReduceMotion && Time.unscaledTime < shakeStart + shakeDur;

        /// <summary>Camera offset this frame (alternating whole-pixel kicks).</summary>
        public static Vector2 ShakeOffset()
        {
            if (!ShakeActive) return Vector2.zero;
            int f = Time.frameCount;
            return new Vector2(((f & 1) == 0 ? 1f : -1f) * shakeAmp, (((f >> 1) & 1) == 0 ? 0.5f : -0.5f) * shakeAmp);
        }

        // ---- hand chip feedback ----
        public static float HandPunchAt = -10f, HandFullAt = -10f;
        public static void PunchHand() => HandPunchAt = Time.unscaledTime;
        public static void PulseHandFull() => HandFullAt = Time.unscaledTime;

        // ---- recent ground-drop origins (area, item -> world point of the node / building) ----
        struct DropOrigin { public string area, item; public Vector3 world; public int frame; }
        static readonly DropOrigin[] drops = new DropOrigin[16];
        static int dropN;

        public static void NoteDrop(string area, string item, Vector3 world)
        {
            drops[dropN & 15] = new DropOrigin { area = area, item = item, world = world, frame = Time.frameCount };
            dropN++;
        }

        public static bool TryDropOrigin(string area, string item, out Vector3 world)
        {
            int f = Time.frameCount;
            for (int i = 1; i <= 16 && i <= dropN; i++)
            {
                var d = drops[(dropN - i) & 15];
                if (f - d.frame <= 2 && d.item == item && d.area == area) { world = d.world; return true; }
            }
            world = default; return false;
        }

        // ---- easing ----
        public static float OutBack(float t) { const float c = 1.70158f; t -= 1f; return 1f + (c + 1f) * t * t * t + c * t * t; }
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        // ---- material colour of a node kind (cached; allocation only the first time a kind is seen) ----
        static readonly Dictionary<string, Color> kindColours = new Dictionary<string, Color>();
        public static Color MaterialColour(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return Rock;
            if (kindColours.TryGetValue(kind, out var c)) return c;
            string k = kind.ToLowerInvariant();
            c = k.Contains("wood") || k.Contains("tree") || k.Contains("bamboo") ? Wood
              : k.Contains("jade") ? Jade
              : k.Contains("starrock") || k.Contains("obsidian") || k.Contains("star") ? Violet
              : k.Contains("fire") ? Lava
              : k.Contains("clay") || k.Contains("sand") ? Sand
              : k.Contains("fish") || k.Contains("water") || k.Contains("spring") ? Water
              : k.Contains("herb") || k.Contains("bush") || k.Contains("shrub") || k.Contains("cotton") || k.Contains("crop") || k.Contains("algae") ? Leaf
              : Rock;
            kindColours[kind] = c;
            return c;
        }
    }
}
