using IdleGrounds.Game;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>Developer toggles (balance pass 1): TEST fast mode is off in the data; force it on/off for play-testing.</summary>
    public static class DevMenu
    {
        const string Root = "Idle Grounds/Dev/TEST Mode/";

        [MenuItem(Root + "From Data (default)")]
        static void FromData() => Set(null);
        [MenuItem(Root + "Force On (timers x0.2, costs x0.5)")]
        static void On() => Set(true);
        [MenuItem(Root + "Force Off")]
        static void Off() => Set(false);

        [MenuItem(Root + "From Data (default)", true)]
        static bool FromDataCheck() { Menu.SetChecked(Root + "From Data (default)", DevSettings.TestModeOverride == null); return true; }
        [MenuItem(Root + "Force On (timers x0.2, costs x0.5)", true)]
        static bool OnCheck() { Menu.SetChecked(Root + "Force On (timers x0.2, costs x0.5)", DevSettings.TestModeOverride == true); return true; }
        [MenuItem(Root + "Force Off", true)]
        static bool OffCheck() { Menu.SetChecked(Root + "Force Off", DevSettings.TestModeOverride == false); return true; }

        static void Set(bool? v)
        {
            DevSettings.TestModeOverride = v;
            Debug.Log("[Idle Grounds] TEST mode " + (v == null ? "from data" : v.Value ? "FORCED ON" : "forced off") + " — applies the next time Play mode starts.");
        }
    }
}
