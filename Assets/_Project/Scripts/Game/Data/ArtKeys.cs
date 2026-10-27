namespace IdleGrounds.Game.Data
{
    /// <summary>
    /// Art key aliases: a building whose art is delivered under another key. The Heavenly Gate (ADR 0007) reuses
    /// the retired Ascension Gate's art (bld_ascension_gate*), so the art pipeline needs no new delivery.
    /// </summary>
    public static class ArtKeys
    {
        /// <summary>Art key of a building's body art: "bld_" + the art id.</summary>
        public static string Building(string buildingKey) => "bld_" + BuildingArtId(buildingKey);

        /// <summary>The id the building's art files are named by (usually the building key itself).</summary>
        public static string BuildingArtId(string buildingKey) => buildingKey == "heavenly_gate" ? "ascension_gate" : buildingKey;

        /// <summary>Default art density: texels per cell (PPU 32, ART-SPEC §2.1).</summary>
        public const int BaseDensity = 32;
        /// <summary>High density for standing objects (owner pick art_density = obj64, ART-SPEC §3.9): 64 texels per cell, PPU 64.</summary>
        public const int HighDensity = 64;
        /// <summary>File-name tag of high-density art, last thing before ".png": "bld_kiln@64.png", "bld_kiln_working_192x384_4f@64.png".</summary>
        public const string HighDensityTag = "@64";

        /// <summary>
        /// Split the density tag off a delivered file's base name (ART-SPEC §3.9 naming): "bld_kiln@64" -> "bld_kiln", 64;
        /// "bld_kiln_working_192x384_4f@64" -> "bld_kiln_working_192x384_4f", 64; no tag -> the name unchanged, 32.
        /// Only "@64" is a density tag; any other "@..." stays in the name (the file then has no wiring target and the
        /// intake warns), so a typo never imports at a wrong size.
        /// </summary>
        public static string SplitDensity(string baseName, out int density)
        {
            density = BaseDensity;
            if (string.IsNullOrEmpty(baseName) || baseName.Length <= HighDensityTag.Length ||
                !baseName.EndsWith(HighDensityTag, System.StringComparison.Ordinal)) return baseName;
            density = HighDensity;
            return baseName.Substring(0, baseName.Length - HighDensityTag.Length);
        }

        static readonly System.Text.RegularExpressions.Regex StripRx = new System.Text.RegularExpressions.Regex(@"_(\d+)x(\d+)_(\d+)f$");
        static readonly System.Text.RegularExpressions.Regex SizeOnlyRx = new System.Text.RegularExpressions.Regex(@"_(\d+)x(\d+)$");

        /// <summary>Delivered file base name -> art key, frame count, frame size and density (ART-SPEC §2 / §3.9 naming):
        /// "fox_idle_32x32_4f" -> "fox_idle", 4, 32x32, 32; "deco_snow_props_128x128_4f@64" -> "deco_snow_props", 4, 128x128, 64;
        /// "bld_kiln@64" -> "bld_kiln", 1, 0x0, 64 (no size in the name).</summary>
        public static string ParseFileName(string baseName, out int frames, out int w, out int h, out int density)
        {
            frames = 1; w = h = 0;
            baseName = SplitDensity(baseName, out density);
            var m = StripRx.Match(baseName);
            if (m.Success) { w = int.Parse(m.Groups[1].Value); h = int.Parse(m.Groups[2].Value); frames = int.Parse(m.Groups[3].Value); return baseName.Substring(0, m.Index); }
            m = SizeOnlyRx.Match(baseName);
            if (m.Success) { w = int.Parse(m.Groups[1].Value); h = int.Parse(m.Groups[2].Value); return baseName.Substring(0, m.Index); }
            return baseName;
        }

        /// <summary>
        /// Size aliases of one art key delivered in two or more <b>world sizes</b> (frame size in cells = texels / density), e.g.
        /// <c>deco_snow_props</c> = 1×1 props (32x48 at 32, 64x96 at 64) and 2×1 props (64x64 at 32, 128x128 at 64).
        /// <paramref name="files"/>: frame size (texels) and density of every file of the key (size 0 = no size in the name, ignored).
        /// Returns alias -> file index, empty when the key has a single world size:
        /// <list type="bullet">
        /// <item>a world alias per size, named in 32-px texels (<c>deco_snow_props_64x64</c> = the 2×1 props), pointing at the
        /// highest-density file of that size, so a data file that names it (island-decor.json) picks the @64 drawing up when it
        /// lands and keeps the 32-px fallback until then;</item>
        /// <item>a canvas alias per file, named by its own frame size (<c>deco_snow_props_128x128</c> = the @64 2×1 file); a world
        /// alias of the same name wins.</item>
        /// </list>
        /// <paramref name="bare"/>: the file the bare key shows (the highest density; there the smallest canvas), or -1.
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, int> SizeAliases(string key, System.Collections.Generic.IReadOnlyList<(int w, int h, int density)> files, out int bare)
        {
            var map = new System.Collections.Generic.Dictionary<string, int>();
            bare = -1;
            var worlds = new System.Collections.Generic.HashSet<(int, int)>();
            int top = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (!Sized(f)) continue;
                worlds.Add(World(f));
                if (f.density > top) top = f.density;
            }
            if (worlds.Count < 2) return map;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (!Sized(f) || f.density != top) continue;
                if (bare < 0 || f.w * f.h < files[bare].w * files[bare].h) bare = i;
            }
            // canvas aliases (the higher density wins a name clash), then the world aliases over them
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (!Sized(f)) continue;
                string a = key + "_" + f.w + "x" + f.h;
                if (!map.TryGetValue(a, out int had) || files[had].density < f.density) map[a] = i;
            }
            var bestByWorld = new System.Collections.Generic.Dictionary<(int, int), int>();
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (!Sized(f)) continue;
                var wk = World(f);
                if (!bestByWorld.TryGetValue(wk, out int had) || files[had].density < f.density) bestByWorld[wk] = i;
            }
            foreach (var kv in bestByWorld) map[key + "_" + kv.Key.Item1 + "x" + kv.Key.Item2] = kv.Value;
            return map;

            static bool Sized((int w, int h, int density) f) => f.w > 0 && f.h > 0 && f.density > 0;
            static (int, int) World((int w, int h, int density) f) => (f.w * BaseDensity / f.density, f.h * BaseDensity / f.density);
        }
    }
}
