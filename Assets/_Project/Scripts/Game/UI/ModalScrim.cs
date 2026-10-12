using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Sits on the scrim of every modal (<c>ProgressionBuilder.Modal</c>): counts open modals so the HUD that must not show
    /// through a modal (quest panel, hand chip) can hide itself (<see cref="Any"/>).
    /// </summary>
    public class ModalScrim : MonoBehaviour
    {
        static int open;
        public static bool Any => open > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { open = 0; }

        void OnEnable() { open++; }
        void OnDisable() { if (open > 0) open--; }
    }
}
