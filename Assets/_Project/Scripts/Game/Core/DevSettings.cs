using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Developer overrides read at boot (PlayerPrefs, so they also work in dev builds). The TEST fast mode
    /// (timers ×0.2, costs ×0.5) is off in the data (ADR 0004); flip it with the Editor menu
    /// <c>Idle Grounds/Dev/TEST Mode/…</c> — it applies the next time Play mode starts.
    /// </summary>
    public static class DevSettings
    {
        public const string TestModeKey = "IdleGrounds.Dev.TestMode";   // -1 / missing = from data, 0 = off, 1 = on

        /// <summary>null = use the data's TEST.ENABLED; true/false = forced.</summary>
        public static bool? TestModeOverride
        {
            get
            {
                int v = PlayerPrefs.GetInt(TestModeKey, -1);
                return v < 0 ? (bool?)null : v != 0;
            }
            set
            {
                if (value == null) PlayerPrefs.DeleteKey(TestModeKey);
                else PlayerPrefs.SetInt(TestModeKey, value.Value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
