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
    }
}
