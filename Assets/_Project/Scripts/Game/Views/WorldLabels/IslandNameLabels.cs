using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Island name banners (VP-07): the scene's plain "Label" text of every <see cref="Island"/> is moved
    /// at runtime into a <see cref="WorldLabel"/> (dark plate, outline, constant screen size). Shown only
    /// when zoomed out (zoom 2.75+), where the Island reads as a place; hidden close up, where the name
    /// would lie on the grass. No scene or prefab change.
    /// </summary>
    public class IslandNameLabels : MonoBehaviour
    {
        public const float ShowFromZoom = 2.75f, Px = 12f;
        bool done;
        int tries;

        void LateUpdate()
        {
            if (done || ++tries > 600) return;
            var islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (islands.Length == 0) return;
            foreach (var isl in islands) Restyle(isl);
            done = true;
        }

        static void Restyle(Island isl)
        {
            var old = isl.transform.Find("Label");
            if (old == null || !old.TryGetComponent<TextMeshPro>(out var t)) return;
            var label = WorldLabel.Create(isl.transform, "NameBanner");
            label.scaleMode = WorldLabelScale.Banner;
            label.hideBelowZoom = ShowFromZoom; label.hideAboveZoom = 99f;
            label.collide = false; label.priority = 10;
            label.SetAnchor(old.localPosition);
            old.SetParent(label.content, false);
            old.localPosition = Vector3.zero;
            WorldLabel.Style(t, Px, PixelSprites.C(PixelSprites.P07));
            t.alpha = 1f;
            t.ForceMeshUpdate();
            float tw = t.GetPreferredValues(t.text).x, th = ViewKit.U(Px * 1.2f);
            t.rectTransform.sizeDelta = new Vector2(tw + 0.2f, th);
            var plate = WorldLabel.NewPlate(label.content);
            float pw = tw + ViewKit.U(14f), ph = th + ViewKit.U(4f);
            WorldLabel.SizePlate(plate, 0f, 0f, pw, ph);
            label.SetBounds(-pw * 0.5f, -ph * 0.5f, pw, ph);
        }
    }
}
