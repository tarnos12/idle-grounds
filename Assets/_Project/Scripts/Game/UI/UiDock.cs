using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Marks a bottom dock (build menu, link editor, bridge / pavilion panel): the hand chip parks above the highest
    /// open dock instead of covering it (<see cref="TopInCanvas"/>).
    /// </summary>
    public class UiDock : MonoBehaviour
    {
        static readonly List<UiDock> active = new List<UiDock>();
        readonly Vector3[] corners = new Vector3[4];

        void OnEnable() { active.Add(this); }
        void OnDisable() { active.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active.Clear(); }

        /// <summary>Highest top edge of any open dock in <paramref name="canvasRect"/> local space (-infinity when none).</summary>
        public static float TopInCanvas(RectTransform canvasRect)
        {
            float top = float.NegativeInfinity;
            foreach (var d in active)
            {
                if (d == null || !d.isActiveAndEnabled) continue;
                ((RectTransform)d.transform).GetWorldCorners(d.corners);
                float y = canvasRect.InverseTransformPoint(d.corners[1]).y;
                if (y > top) top = y;
            }
            return top;
        }
    }
}
