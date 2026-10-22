using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One font face the UI can draw in (owner pick font_read = "setting", 2026-10-22): its TMP assets (Resources names
    /// built by Idle Grounds/UI/Build Text Font Choices) and its size rule. Pixel faces are only legible on whole font
    /// pixels, so every face has a desktop body size where one font pixel is a whole number of screen pixels (the raster
    /// cut is drawn 1:1 there) and a step (one font pixel on screen at that size) that every other size snaps to.
    /// </summary>
    public sealed class FontFace
    {
        public readonly string id, name;
        /// <summary>Resources names (null = none). Pixelify's SDF is the TMP default font (not in Resources).</summary>
        public readonly string sdfResource, rasterResource, boldResource;
        /// <summary>Desktop body size in screen px (the raster cut's size) and the snap step (screen px).</summary>
        public readonly float bodyPx, stepPx;
        /// <summary>World text floor in world px: one font pixel per art pixel.</summary>
        public readonly float worldFloorPx;
        /// <summary>True for pixel fonts (no synthetic bold: it fills the 1-pixel counters).</summary>
        public readonly bool pixel;

        public FontFace(string id, string name, string sdf, string raster, string bold, float bodyPx, float stepPx, float worldFloorPx, bool pixel)
        {
            this.id = id; this.name = name; sdfResource = sdf; rasterResource = raster; boldResource = bold;
            this.bodyPx = bodyPx; this.stepPx = stepPx; this.worldFloorPx = worldFloorPx; this.pixel = pixel;
        }

        /// <summary>Pixelify's body size: every authored size in the game was made for it (22 px = 2 screen px per font pixel).</summary>
        public const float BasePx = 22f, BaseStep = 11f;

        /// <summary>
        /// Screen size for an authored uGUI size at canvas scale <paramref name="scale"/>: first Pixelify's rule (whole Pixelify
        /// pixels, at least 22: 15-27 -> 22, 28-38 -> 33 at scale 1), then the same step on this face's grid (22 -> body,
        /// 33 -> body x 1.5 rounded down to a whole font pixel, ...), never under the face's body size.
        /// </summary>
        public float SnapScreenPx(float authored, float scale)
        {
            if (authored <= 0f) return authored;
            float basePx = Mathf.Max(BasePx, Mathf.Round(authored * Mathf.Max(0.01f, scale) / BaseStep) * BaseStep);
            if (Mathf.Approximately(bodyPx, BasePx) && Mathf.Approximately(stepPx, BaseStep)) return basePx;
            float want = basePx * bodyPx / BasePx;
            float n = Mathf.Round(want / stepPx - 0.02f);           // halves round down: text stays inside its box
            return Mathf.Max(bodyPx, n * stepPx);
        }

        /// <summary>World text size (TMP font size units) for an authored world size: the same ratio as the UI body, never
        /// under one font pixel per art pixel.</summary>
        public float WorldSize(float authored, float fontPerPx)
        {
            if (Mathf.Approximately(bodyPx, BasePx)) return authored;
            return Mathf.Max(worldFloorPx * fontPerPx, authored * bodyPx / BasePx);
        }

        public bool IsRasterPx(float px) => rasterResource != null && Mathf.Abs(px - bodyPx) < 0.01f;
    }

    /// <summary>A "Text font" setting: the face for titles and headings and the face for everything else.</summary>
    public sealed class TextFontOption
    {
        public readonly string id, label, tip;
        public readonly FontFace title, body;
        public TextFontOption(string id, string label, FontFace title, FontFace body, string tip) { this.id = id; this.label = label; this.title = title; this.body = body; this.tip = tip; }
    }

    /// <summary>The faces and the six "Text font" options (Menu > Settings).</summary>
    public static class TextFonts
    {
        /// <summary>
        /// Pixelify Sans: one font pixel = 1/11 em; raster 22 (2 px per font pixel), steps of 11.
        /// Jersey 10: drawn on 150 / 1400 em units (half-pixel diagonals at 75): raster 28 = 3 px per pixel, steps of 9.33.
        /// VT323: 40 / 1000 em units: raster 25 = 1 px per pixel (its strokes are 2 pixels wide), steps of 12.5.
        /// Press Start 2P: 125 / 1000 em units (8 px per em, monospaced and wide): raster 16 = 2 px per pixel, steps of 8.
        /// Atkinson Hyperlegible: a smooth font, no grid: SDF at Pixelify's sizes (22, 33, 44).
        /// </summary>
        public static readonly FontFace Pixelify = new FontFace("pixelify", "Pixelify Sans", null, "PixelifySans Pixel22", "PixelifySans Bold SDF", 22f, 11f, 11f, true);
        public static readonly FontFace Jersey = new FontFace("jersey10", "Jersey 10", "Jersey10 SDF", "Jersey10 Pixel28", null, 28f, 28f / 3f, 1400f / 150f, true);
        public static readonly FontFace Vt323 = new FontFace("vt323", "VT323", "VT323 SDF", "VT323 Pixel25", null, 25f, 12.5f, 12.5f, true);
        public static readonly FontFace PressStart = new FontFace("pressstart2p", "Press Start 2P", "PressStart2P SDF", "PressStart2P Pixel16", null, 16f, 8f, 8f, true);
        public static readonly FontFace Atkinson = new FontFace("atkinson", "Atkinson Hyperlegible", "AtkinsonHyperlegible SDF", null, "AtkinsonHyperlegible Bold SDF", 22f, 11f, 11f, false);

        public static readonly FontFace[] Faces = { Pixelify, Jersey, Vt323, PressStart, Atkinson };

        public const string DefaultId = "pixelify_jersey";

        /// <summary>Menu order. The game starts on Pixelify titles + Jersey 10 (owner pick font_read, 2026-10-22).</summary>
        public static readonly TextFontOption[] Options =
        {
            new TextFontOption("pixelify", "Pixelify Sans", Pixelify, Pixelify, "Pixelify Sans everywhere (the first look; 5 and S, B and 8 look alike)"),
            new TextFontOption(DefaultId, "Pixelify + Jersey 10", Pixelify, Jersey, "Pixelify Sans titles, Jersey 10 for text and numbers (default)"),
            new TextFontOption("jersey10", "Jersey 10", Jersey, Jersey, "Jersey 10 everywhere"),
            new TextFontOption("vt323", "VT323", Vt323, Vt323, "VT323 everywhere: thin terminal letters"),
            new TextFontOption("pressstart2p", "Press Start 2P", PressStart, PressStart, "Press Start 2P everywhere: wide arcade letters"),
            new TextFontOption("smooth", "Smooth", Atkinson, Atkinson, "Atkinson Hyperlegible everywhere: a smooth font made for easy reading, not pixel art"),
        };

        public static TextFontOption Find(string id)
        {
            foreach (var o in Options) if (o.id == id) return o;
            return null;
        }

        public static FontFace FindFace(string id)
        {
            foreach (var f in Faces) if (f.id == id) return f;
            return null;
        }

        /// <summary>Name prefixes of every TMP font asset the game owns (texts on any of these follow the setting).</summary>
        public static readonly string[] OwnPrefixes = { "LiberationSans", "PixelifySans", "Jersey10", "VT323", "PressStart2P", "AtkinsonHyperlegible" };

        public static bool IsOwn(string fontName)
        {
            if (string.IsNullOrEmpty(fontName)) return false;
            foreach (var p in OwnPrefixes) if (fontName.StartsWith(p)) return true;
            return false;
        }

        /// <summary>A short line drawn in the font itself in the settings (numbers and the look-alike pairs).</summary>
        public const string Sample = "Iron Bar 0/10  25 Wood  [B] Build  5S 38 B8";
    }
}
