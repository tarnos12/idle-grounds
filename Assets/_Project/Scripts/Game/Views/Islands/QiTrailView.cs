using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The sky "qi trail" between a paired sending → receiving Spirit Bridge (ART-SPEC §3.4
    /// <c>fx_qi_trail_64x16_4f</c>): a Tiled SpriteRenderer stretched + rotated between the two bridge
    /// centres, cycling the strip's 4 frames (the art animates the sparkles flowing from the
    /// sender to the receiver). Pooled by <see cref="QiTrailSync"/>.
    /// </summary>
    public class QiTrailView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer line;
        [SerializeField] Sprite[] frames;
        [Tooltip("Trail thickness in world px.")]
        [SerializeField] float widthPx = 12f;
        [SerializeField] float fps = 8f;

        internal int seenFrame;
        public Vector3 From { get; private set; }
        public Vector3 To { get; private set; }
        public string Key { get; private set; }

        public void Bind(string key) { Key = key; name = "QiTrail_" + key; }

        public void Set(Vector3 a, Vector3 b, Color tint)
        {
            From = a; To = b;
            var d = b - a;
            float len = d.magnitude;
            var t = line.transform;
            int fi = frames != null && frames.Length > 0 ? (int)(Time.unscaledTime * fps) % frames.Length : -1;
            if (fi >= 0 && line.sprite != frames[fi]) line.sprite = frames[fi];
            if (line.sprite == null) return;
            var sb = line.sprite.bounds.size;
            float k = ViewKit.U(widthPx) / Mathf.Max(0.0001f, sb.y);       // scale the strip to the trail width
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(k, k, 1f);
            line.size = new Vector2(Mathf.Max(0.001f, len) / k, sb.y);
            t.position = (a + b) * 0.5f;
            line.color = tint;
        }
    }
}
