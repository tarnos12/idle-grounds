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
    }
}
