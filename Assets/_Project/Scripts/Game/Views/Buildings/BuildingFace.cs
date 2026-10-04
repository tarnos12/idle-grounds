using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A family-specific part of a building prefab variant (converter face, fuel rack, storehouse
    /// label, …). Lives on a child of the base Building prefab; <see cref="BuildingView"/> finds every
    /// face, lays them out on bind and refreshes them each frame while the building is BUILT
    /// (ghosts always use the base ghost face).
    /// </summary>
    public abstract class BuildingFace : MonoBehaviour
    {
        /// <summary>True = hide the base icon + name when built (the face draws its own).</summary>
        public virtual bool ReplacesDefault => true;

        /// <summary>Called on bind (footprint size known via view.W/H).</summary>
        public abstract void Layout(BuildingView v);

        /// <summary>Called every LateUpdate while built.</summary>
        public abstract void Refresh(BuildingView v);

        /// <summary>Hover/selection changed (reach circles etc.).</summary>
        public virtual void SetHighlight(BuildingView v, bool on) { }
    }
}
