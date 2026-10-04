using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Shows an Island's faint boundary frame only while the player is placing a building, so the playable
    /// square does not read from afar but building limits stay discoverable (ADR 0003 visual coast).
    /// </summary>
    public class PlacementGuide : MonoBehaviour
    {
        public GameObject guide;
        BuildController build;

        void Update()
        {
            if (guide == null) return;
            if (build == null) build = FindFirstObjectByType<BuildController>();
            bool on = build != null && (build.Placing != null || build.Demolishing);
            if (guide.activeSelf != on) guide.SetActive(on);
        }
    }
}
