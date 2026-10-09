using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The sea of clouds far BELOW the floating Islands (3/4 top-down view, ADR 0003): loose cloud puffs
    /// scattered over a repeating world tile, each with its own parallax depth (0 = glued to the screen,
    /// 1 = moves with the world) and a slow wind drift. Positions wrap toroidally around the camera, so the
    /// field never runs out at any pan/zoom. Allocation-free per frame; sprites are drawn at native or 2x
    /// integer scale and snapped to the 1/32 world grid to keep the pixel art crisp.
    /// Built by IslandsBuilder.BuildSky.
    /// </summary>
    public class CloudSea : MonoBehaviour
    {
        [System.Serializable]
        public struct Puff
        {
            public Transform t;
            [Tooltip("Position inside the repeating tile (world units, centred on the camera).")]
            public Vector2 home;
            [Tooltip("How much the puff moves with the world (0 = screen-fixed, 1 = world-fixed).")]
            [Range(0f, 1f)] public float parallax;
            [Tooltip("Wind drift in world units per second (+x = east).")]
            public float drift;
        }

        public Puff[] puffs = new Puff[0];
        [Tooltip("Repeating tile size (world units); widened at runtime if the view is larger.")]
        public Vector2 tile = new Vector2(160f, 100f);
        [Tooltip("Z of the field (in front of the camera's near plane).")]
        public float z = 10f;
        [Tooltip("Snap grid (world units) - 1/32 = one art pixel.")]
        public float snap = 1f / 32f;

        [SerializeField] Camera target;

        void Awake()
        {
            if (target == null) target = Camera.main;
        }

        void LateUpdate()
        {
            if (target == null) { target = Camera.main; if (target == null) return; }
            float h = target.orthographicSize * 2f, w = h * target.aspect;
            float tw = Mathf.Max(tile.x, w + 24f), th = Mathf.Max(tile.y, h + 16f);
            var cp = target.transform.position;
            float time = Time.unscaledTime;
            float inv = snap > 0f ? 1f / snap : 0f;
            for (int i = 0; i < puffs.Length; i++)
            {
                var p = puffs[i];
                if (p.t == null) continue;
                float x = p.home.x + p.drift * time - cp.x * p.parallax;
                float y = p.home.y - cp.y * p.parallax;
                x = Mathf.Repeat(x + tw * 0.5f, tw) - tw * 0.5f;
                y = Mathf.Repeat(y + th * 0.5f, th) - th * 0.5f;
                float wx = cp.x + x, wy = cp.y + y;
                if (inv > 0f) { wx = Mathf.Round(wx * inv) / inv; wy = Mathf.Round(wy * inv) / inv; }
                p.t.position = new Vector3(wx, wy, z);
            }
        }
    }
}
