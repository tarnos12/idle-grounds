using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Data for the code animation layer (owner pick anim_route 2026-10-26, ART-SPEC §3.9.8): per art key, the effects the
    /// game draws on top of (or bakes into) an accepted still. One JSON file, <c>Assets/_Project/Art/CodeAnim/Resources/code-anim.json</c>
    /// (loaded with <c>Resources.Load("code-anim")</c>). All coordinates are <b>texels of the still, origin top-left</b>
    /// (the way ART-SPEC names areas: "mouth x73–121, y305–357"). Effect types:
    /// <list type="bullet">
    /// <item>baked into the still frames (pixel-exact, cycle of <see cref="CodeAnimEntry.steps"/> frames): <c>sway</c> (rows of a masked
    /// part shear sideways, growing from the pivot to the tip), <c>shift</c> (a masked part moves as one: nod, press bob), <c>scroll</c>
    /// (masked pixels scroll along their column / row: water), <c>line</c> (a 1-px line drawn between two texels; its end sways).</item>
    /// <item>overlays (pooled SpriteRenderers on the art): <c>glow</c> (masked still pixels, tinted pulse colorA→colorB, 4 alpha steps),
    /// <c>flicker</c> (mask, generated flame tongues or star glints with stepped alpha/shape jitter), <c>smoke</c> (pooled puffs: rise,
    /// drift, grow, fade through 4 alpha steps), <c>toggle</c> (a sprite on for some steps: paper sheet), <c>layer</c> (an artist layer
    /// <c>&lt;key&gt;_layer_&lt;part&gt;@64.png</c>: rotate / slide / squash / pose swap).</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class CodeAnimFile
    {
        public int version;
        public string note;
        public CodeAnimEntry[] entries;
    }

    [Serializable]
    public class CodeAnimEntry
    {
        /// <summary>Art key without density tag: "bld_kiln", "npc_kang".</summary>
        public string key;
        public string note;
        /// <summary>Expected still size in texels [w, h]; regions are skipped with a warning when the still does not match.</summary>
        public int[] still;
        /// <summary>Step rate of the cycle (frames per second) and cycle length in steps.</summary>
        public float fps;
        public int steps;
        /// <summary>"working" (buildings: only while the building works) or "always" (NPC idles).</summary>
        public string play;
        /// <summary>Buildings: the code animation replaces any delivered working loop (Kiln interim, Paper Mill pilot loop).</summary>
        public bool replaceDeliveredLoop;
        /// <summary>The accepted hand-drawn loop / idle strip stays (Workbench saw, Lin, Ouyang): no code effects.</summary>
        public bool keepDeliveredLoop;
        public CodeAnimEffect[] effects;

        public bool PlaysAlways => play == "always";
        public bool HasEffects => effects != null && effects.Length > 0 && !keepDeliveredLoop;
        public int StillW => still != null && still.Length >= 2 ? still[0] : 0;
        public int StillH => still != null && still.Length >= 2 ? still[1] : 0;

        internal void Normalize()
        {
            if (fps <= 0f) fps = 6f;
            if (steps <= 0) steps = 4;
            if (string.IsNullOrEmpty(play)) play = key != null && key.StartsWith("npc_") ? "always" : "working";
            if (effects == null) effects = new CodeAnimEffect[0];
            foreach (var e in effects) e?.Normalize();
        }
    }

    /// <summary>HSV + alpha predicate over still pixels (all ranges inclusive; hue in degrees, wraps when min &gt; max).</summary>
    [Serializable]
    public class CodeAnimMask
    {
        public float[] hue;
        public float[] sat;
        public float[] val;

        public bool Match(Color32 c)
        {
            if (c.a < 128) return false;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h *= 360f;
            if (sat != null && sat.Length >= 2 && (s < sat[0] || s > sat[1])) return false;
            if (val != null && val.Length >= 2 && (v < val[0] || v > val[1])) return false;
            if (hue != null && hue.Length >= 2)
            {
                bool inside = hue[0] <= hue[1] ? h >= hue[0] && h <= hue[1] : h >= hue[0] || h <= hue[1];
                if (!inside) return false;
            }
            return true;
        }
    }

    public enum CodeAnimKind { Unknown, Sway, Shift, Scroll, Line, Glow, Flicker, Smoke, Toggle, Layer }

    [Serializable]
    public class CodeAnimEffect
    {
        public string type;
        public string name;
        public string note;
        /// <summary>Area [x, y, w, h] in still texels (top-left origin).</summary>
        public int[] rect;
        /// <summary>Pixel predicates, OR-ed (none = every opaque pixel in <see cref="rect"/>).</summary>
        public CodeAnimMask[] masks;
        /// <summary>Pivot texel [x, y] (sway / layer rotation).</summary>
        public int[] pivot;
        /// <summary>"x" or "y": sway / shift / scroll direction.</summary>
        public string axis;
        /// <summary>Per-step texel offsets (sway tip, shift, line end); for <c>layer</c> slide: [dx0, dy0, dx1, dy1, ...].</summary>
        public int[] offsets;
        /// <summary>Scroll: texels per step (sign = direction).</summary>
        public int speed;
        /// <summary>Anchor texels [x0, y0, x1, y1, ...]: flicker sprites (bottom-centre), smoke emitter (bottom-centre of the first puff), line ends.</summary>
        public int[] points;
        /// <summary>Hex colours (#RRGGBB): tint from → to (glow), fill / outline (sheet), line colour.</summary>
        public string colorA, colorB;
        /// <summary>Alpha range [min, max] (quantised to the 4 alpha steps 64 / 128 / 191 / 255).</summary>
        public float[] alpha;
        /// <summary>Calm motion: "hide", "rest" (default: hold the middle value) or a number = held alpha.</summary>
        public string calm;
        /// <summary>Pulse period / smoke puff life in seconds; phase in cycles.</summary>
        public float period;
        public float phase;
        /// <summary>Additive blend (glows); falls back to alpha blend when the shader is missing.</summary>
        public bool additive;
        /// <summary>Overlay sprite source: "mask" (masked still pixels, shaded white, tinted), "maskColor" (masked still pixels
        /// in their own colours), "flame", "star", "puff", "sheet". For <c>layer</c>: the part name ("wheel" → &lt;key&gt;_layer_wheel).</summary>
        public string sprite;
        /// <summary>Generated sprite size [w, h] (smoke: first puff), smoke: last puff size.</summary>
        public int[] size;
        public int[] growTo;
        /// <summary>Smoke: puffs in flight; flicker: shape variants.</summary>
        public int count;
        /// <summary>Smoke: total rise / sideways drift in texels over a puff's life.</summary>
        public int rise, drift;
        /// <summary>Smoke: alpha per life quarter (4 values).</summary>
        public float[] alphaSteps;
        /// <summary>Toggle: steps of the cycle the sprite is shown; flicker "cycle" mode shows one point per step.</summary>
        public int[] onSteps;
        public string mode;
        /// <summary>Layer: per-step rotation in degrees (rotate) or horizontal scale (squash, 0..1), or one rotation per second (spin).</summary>
        public float[] angles;
        public float[] scales;
        public float spin;
        /// <summary>Layer: per-step pose names ("" = rest layer, "raised" → &lt;key&gt;_layer_&lt;part&gt;_raised).</summary>
        public string[] poses;
        /// <summary>Sorting offset above the art.</summary>
        public int sort;
        /// <summary>Effect only runs while the named layers are missing (code stand-in, e.g. a nod until the arm layer arrives).</summary>
        public bool onlyWithoutLayers;

        [NonSerialized] public CodeAnimKind kind;
        [NonSerialized] public Color colA, colB;

        public bool IsBaked => kind == CodeAnimKind.Sway || kind == CodeAnimKind.Shift || kind == CodeAnimKind.Scroll || kind == CodeAnimKind.Line;
        public bool HasRect => rect != null && rect.Length >= 4;

        internal void Normalize()
        {
            kind = ParseKind(type);
            if (string.IsNullOrEmpty(axis)) axis = kind == CodeAnimKind.Sway ? "x" : "y";
            if (period <= 0f) period = kind == CodeAnimKind.Smoke ? 2.4f : 1.2f;
            if (alpha == null || alpha.Length < 2) alpha = kind == CodeAnimKind.Glow ? new[] { 0.25f, 0.75f } : new[] { 0.75f, 1f };
            if (alphaSteps == null || alphaSteps.Length == 0) alphaSteps = new[] { 0.75f, 0.75f, 0.5f, 0.25f };
            if (string.IsNullOrEmpty(sprite)) sprite = kind == CodeAnimKind.Smoke ? "puff" : kind == CodeAnimKind.Toggle ? "sheet" : "mask";
            if (count <= 0) count = kind == CodeAnimKind.Smoke ? 4 : kind == CodeAnimKind.Flicker ? 4 : 1;
            if (string.IsNullOrEmpty(calm)) calm = kind == CodeAnimKind.Smoke ? "hide" : "rest";
            colA = ParseColor(colorA, Color.white);
            colB = ParseColor(colorB, colA);
        }

        /// <summary>Alpha held while calm motion is on; negative = hidden.</summary>
        public float CalmAlpha
        {
            get
            {
                if (calm == "hide") return -1f;
                if (calm != "rest" && float.TryParse(calm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a)) return a;
                return CodeAnimMath.QuantizeAlpha(0.5f * (alpha[0] + alpha[1]));
            }
        }

        public static CodeAnimKind ParseKind(string t)
        {
            switch (t)
            {
                case "sway": return CodeAnimKind.Sway;
                case "shift": case "nod": case "bob": return CodeAnimKind.Shift;
                case "scroll": case "water": return CodeAnimKind.Scroll;
                case "line": return CodeAnimKind.Line;
                case "glow": return CodeAnimKind.Glow;
                case "flicker": case "glint": return CodeAnimKind.Flicker;
                case "smoke": case "steam": return CodeAnimKind.Smoke;
                case "toggle": return CodeAnimKind.Toggle;
                case "layer": return CodeAnimKind.Layer;
                default: return CodeAnimKind.Unknown;
            }
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            return ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c) ? c : fallback;
        }

        public bool InMask(Color32 c)
        {
            if (c.a < 128) return false;
            if (masks == null || masks.Length == 0) return true;
            foreach (var m in masks) if (m != null && m.Match(c)) return true;
            return false;
        }
    }

    /// <summary>Loads and indexes <c>code-anim.json</c>; also names the artist files of ART-SPEC §3.9.8.</summary>
    public static class CodeAnimLibrary
    {
        public const string ResourceName = "code-anim";
        static Dictionary<string, CodeAnimEntry> byKey;
        static CodeAnimFile file;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { byKey = null; file = null; }

        public static CodeAnimFile File { get { Ensure(); return file; } }

        /// <summary>Parse a config text (tests, tools). Never throws: bad JSON gives an empty file and an error log.</summary>
        public static CodeAnimFile Parse(string json)
        {
            CodeAnimFile f = null;
            try { f = JsonUtility.FromJson<CodeAnimFile>(json); }
            catch (Exception e) { Debug.LogError("[CodeAnim] bad code-anim.json: " + e.Message); }
            if (f == null) f = new CodeAnimFile();
            if (f.entries == null) f.entries = new CodeAnimEntry[0];
            foreach (var e in f.entries) e?.Normalize();
            return f;
        }

        /// <summary>Replace the loaded config (tests).</summary>
        public static void Use(CodeAnimFile f)
        {
            file = f ?? new CodeAnimFile { entries = new CodeAnimEntry[0] };
            byKey = new Dictionary<string, CodeAnimEntry>();
            foreach (var e in file.entries) if (e != null && !string.IsNullOrEmpty(e.key)) byKey[e.key] = e;
        }

        public static void Reload() { byKey = null; file = null; Ensure(); }

        static void Ensure()
        {
            if (byKey != null) return;
            var ta = Resources.Load<TextAsset>(ResourceName);
            Use(ta != null ? Parse(ta.text) : null);
        }

        /// <summary>Entry for an art key ("bld_kiln", "npc_kang"), or null.</summary>
        public static CodeAnimEntry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Ensure();
            return byKey.TryGetValue(key, out var e) ? e : null;
        }

        // ---- artist file names (ART-SPEC §3.9.8); the resolver passed to CodeAnimator gets these suffixes after "<key>_"
        public const string BaseSuffix = "base", FrontSuffix = "layer_front";
        public static string LayerSuffix(string part) => "layer_" + part;
        public static string PoseSuffix(string part, string pose) => string.IsNullOrEmpty(pose) ? LayerSuffix(part) : "layer_" + part + "_" + pose;
        /// <summary>Full art key of an artist file: ("bld_paper_mill", "layer_wheel") → "bld_paper_mill_layer_wheel" (file: + "@64.png").</summary>
        public static string FileKey(string key, string suffix) => key + "_" + suffix;
    }
}
