using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Hardware cursor art (ui_cursor_hand_open / hand_closed / target, 32 px art, hotspots from the art README).
    /// Open hand over the world, closed hand while LMB is held (harvest / vacuum / strike), target over something that can be hit
    /// and in demolish / placement modes. Over UI (or with a debug cursor / no art) the system arrow is restored.
    /// Textures are 2x nearest-neighbour copies built once; <see cref="Cursor.SetCursor(Texture2D, Vector2, CursorMode)"/> is only
    /// called when the state changes (no per-frame allocation).
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        enum Mode { Default, Open, Closed, Target }
        const int Scale = 2;

        [SerializeField] HandController hand;
        [SerializeField] BuildController build;

        Mode mode = Mode.Default;
        Texture2D[] tex;      // by Mode (null = system cursor)
        Vector2[] hot;
        bool built;

        void Awake()
        {
            if (hand == null) hand = GetComponent<HandController>();
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            if (build == null) build = FindFirstObjectByType<BuildController>();
        }

        void OnDisable() { Apply(Mode.Default, force: true); }

        void OnDestroy() { if (tex != null) foreach (var t in tex) if (t != null) Destroy(t); }

        void Build()
        {
            built = true;
            var runner = GameRunner.Instance;
            var db = runner != null ? runner.Database : null;
            if (db == null) return;
            tex = new Texture2D[4]; hot = new Vector2[4];
            Make(db, Mode.Open, "ui_cursor_hand_open", new Vector2(12f, 4f));
            Make(db, Mode.Closed, "ui_cursor_hand_closed", new Vector2(12f, 8f));
            Make(db, Mode.Target, "ui_cursor_target", new Vector2(16f, 16f));
            if (tex[(int)Mode.Open] == null) tex = null;      // art missing -> keep the system cursor everywhere
        }

        void Make(Data.GameDatabase db, Mode m, string key, Vector2 hotspot)
        {
            var sp = db.UiSprite(key);
            if (sp == null) return;
            var t = Upscale(sp, Scale);
            if (t == null) return;
            tex[(int)m] = t; hot[(int)m] = hotspot * Scale;
        }

        /// <summary>Nearest-neighbour k-times copy of a sprite's pixels (works on non-readable textures via a temporary RT).</summary>
        static Texture2D Upscale(Sprite sp, int k)
        {
            var src = sp.texture;
            var r = sp.rect;          // the full art canvas (textureRect is trimmed by tight meshes, which would shift the hotspot)
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Texture2D small = null, big = null;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                small = new Texture2D(w, h, TextureFormat.RGBA32, false);
                small.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
                var sp32 = small.GetPixels32();
                var bp = new Color32[w * k * h * k];
                int bw = w * k;
                for (int y = 0; y < h * k; y++)
                    for (int x = 0; x < bw; x++) bp[y * bw + x] = sp32[(y / k) * w + x / k];
                big = new Texture2D(bw, h * k, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "cursor_" + sp.name };
                big.SetPixels32(bp);
                big.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (small != null) Destroy(small);
            }
            return big;
        }

        void Update()
        {
            if (!built) Build();
            if (tex == null || hand == null) return;
            Apply(Pick(), false);
        }

        Mode Pick()
        {
            if (hand.useDebugCursor || hand.PointerOverUI || !hand.CursorOver) return Mode.Default;
            if (hand.LeftHeld) return Mode.Closed;
            if (build != null && (build.Demolishing || build.Placing != null)) return Mode.Target;
            return hand.HoverHittable ? Mode.Target : Mode.Open;
        }

        void Apply(Mode m, bool force)
        {
            if (m == mode && !force) return;
            mode = m;
            var t = tex != null ? tex[(int)m] : null;
            if (t != null) Cursor.SetCursor(t, hot[(int)m], CursorMode.Auto);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
