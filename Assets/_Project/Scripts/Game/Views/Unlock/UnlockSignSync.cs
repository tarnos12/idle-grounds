using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Keeps one <see cref="UnlockSignView"/> (an unlock stele) per locked FRONTIER Island — one that borders
    /// an unlocked Island on the original 3×3 neighbour grid (RegionDef.rx/ry) — standing just inside the
    /// locked Island's edge that faces the Center (ADR 0003; the Island positions come from the scene).
    /// The steles' rects are handed to the <see cref="CameraController"/> as extra bounds, so the view can
    /// always pan over the sky to them. Steles vanish when the Island unlocks (veils lift via GameRunner).
    /// <see cref="Pay"/> = <see cref="Simulation.UnlockArea"/> (installments from the hand).
    /// </summary>
    public class UnlockSignSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] UnlockSignView prefab;
        [SerializeField] Transform root;
        [SerializeField] FxService fx;
        [SerializeField] HandController hand;
        [SerializeField] CameraController cameraController;
        [Tooltip("How far inside the facing edge the stele stands (cells).")]
        [SerializeField] float edgeInset = 3.5f;

        readonly Dictionary<int, UnlockSignView> views = new Dictionary<int, UnlockSignView>();
        readonly List<int> releaseList = new List<int>();
        ObjectPool<UnlockSignView> pool;
        SpriteCache sprites;
        int frame;
        string boundsSig;
        readonly List<Rect> boundsRects = new List<Rect>();
        readonly System.Text.StringBuilder sigBuilder = new System.Text.StringBuilder();

        public IEnumerable<UnlockSignView> Signs => views.Values;
        public int Count => views.Count;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<UnlockSignView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 8, 32);
        }

        void Start()
        {
            if (fx == null) fx = FindFirstObjectByType<FxService>();
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            sprites = new SpriteCache(runner.Database, null);
        }

        public UnlockSignView Find(string area)
        {
            foreach (var v in views.Values) if (v.Area == area) return v;
            return null;
        }

        public const string CenterKey = "center";

        /// <summary>
        /// Stele position for a locked Island: on the edge that faces the Center Island (dominant axis of the
        /// centre-to-centre vector), <see cref="edgeInset"/> cells inside it, slid along the edge toward the
        /// Center's centre (kept clear of the corners).
        /// </summary>
        public Vector3 StelePoint(string locked)
        {
            var sp = runner.Space;
            var o = sp.Origin(locked);
            float cells = sp.Cells;
            var cl = sp.IslandCentre(locked);
            var cc = sp.IslandCentre(locked == CenterKey ? locked : CenterKey);
            var d = cc - cl;
            const float corner = 8f;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            {
                float x = d.x >= 0 ? o.x + cells - edgeInset : o.x + edgeInset;
                float y = Mathf.Clamp(cc.y, o.y - cells + corner, o.y - corner);
                return new Vector3(x, y, 0f);
            }
            else
            {
                // facing up: the plate's top + stele must stay on the Island; facing down: stand just above the cliff
                float y = d.y >= 0 ? o.y - edgeInset - UnlockSignView.SteleH : o.y - cells + edgeInset;
                float x = Mathf.Clamp(cc.x, o.x + corner, o.x + cells - corner);
                return new Vector3(x, y, 0f);
            }
        }

        static bool Neighbours(RegionDef a, RegionDef b) => Mathf.Abs(a.rx - b.rx) + Mathf.Abs(a.ry - b.ry) == 1;

        /// <summary>Sign under a world point (null = none).</summary>
        public UnlockSignView HitTest(Vector2 world)
        {
            foreach (var v in views.Values) if (v.WorldRect.Contains(world)) return v;
            return null;
        }

        /// <summary>Pay the hand toward the stele's Island (left or right click on it).</summary>
        public UnlockResult Pay(UnlockSignView v, Vector2 at)
        {
            var res = runner.Sim.UnlockArea(v.Area);
            v.Invalidate();
            if (fx != null)
            {
                string nm = runner.Config.Region(v.Area)?.name ?? v.Area;
                if (res.kind == UnlockResultKind.Unlocked) fx.Floater(at, nm + " unlocked!", FxService.Gold);
                else if (res.kind == UnlockResultKind.Paid) fx.Floater(at, "Paid " + res.paid, FxService.Gold);
                else { fx.Floater(at, "Carry the cost to unlock", FxService.Danger); AudioService.Play("error"); }
            }
            return res;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null || runner.Space == null) return;
            frame++;
            var islands = runner.Config.regions;
            double now = runner.SimNow;
            sigBuilder.Clear();
            for (int li = 0; li < islands.Count; li++)
            {
                var l = islands[li];
                if (runner.IsUnlocked(l.key)) continue;
                string from = null;
                for (int ui = 0; ui < islands.Count && from == null; ui++)
                    if (runner.IsUnlocked(islands[ui].key) && Neighbours(islands[ui], l)) from = islands[ui].key;
                if (from == null) continue;            // not on the frontier yet
                if (!views.TryGetValue(li, out var v))
                {
                    v = pool.Get();
                    views[li] = v;
                    v.Bind(l.key, from, StelePoint(l.key), l.name);
                }
                sigBuilder.Append(li).Append(';');
                v.seenFrame = frame;
                v.Hovered = hand != null && hand.CursorOver && v.WorldRect.Contains(hand.CursorWorld);
                if (!ViewCull.Visible(v.transform.position, UnlockSignView.W)) continue;
                v.Refresh(runner.Sim, sprites, now);
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }

            // the camera may always pan over the sky to every stele
            string sig = sigBuilder.ToString();
            if (sig != boundsSig && cameraController != null)
            {
                boundsSig = sig;
                boundsRects.Clear();
                foreach (var v in views.Values) boundsRects.Add(v.WorldRect);
                cameraController.SetExtraBounds(boundsRects);
            }
        }
    }
}
