using System.Collections.Generic;
using System.Text.RegularExpressions;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One-shot world FX (ui-input-render §5): pooled floaters (cap 60) and spark bursts (cap 240).
    /// GroundDropped → "+qty" item floater (unlocked + on-screen only), prized (gold) loot also pops 8 gold
    /// sparks; <see cref="Pickup"/> = green "+N" + 5 sparks + "pickup" SFX; player node swings / enemy strikes
    /// (HandController: at the cursor on every node click / auto-swing, at the enemy on every strike) = 4 light sparks. Sparks: random angle, 30-100 px/s, extra
    /// -30 px/s up, gravity 90 px/s², radius 1.5-3.5 px shrinking 40 %, life 380-640 ms, alpha 1→0.
    /// Nothing spawns while the sim events are muted.
    /// Everything clears on RunReset (ascension).
    /// </summary>
    public class FxService : MonoBehaviour
    {
        public const int MaxFloaters = 60, MaxSparks = 240;
        public static readonly Color Green = Hex("#4ade80"), Gold = Hex("#fbbf24"), Steel = Hex("#cbd5e1"), Danger = Hex("#f87171");
        public static readonly Color SwingSpark = new Color(226 / 255f, 232 / 255f, 240 / 255f, 0.9f);
        static readonly Regex GoldItems = new Regex("essence|scale|pill|elixir|talisman|jade");
        static readonly Regex SteelItems = new Regex("iron|steel|star|tools|glass");

        [SerializeField] GameRunner runner;
        [SerializeField] FloatingText floaterPrefab;
        [SerializeField] Camera worldCamera;
        [Tooltip("Round sprite (1 unit diameter) for the spark bursts.")]
        [SerializeField] Sprite sparkSprite;

        ObjectPool<FloatingText> pool;
        readonly List<FloatingText> live = new List<FloatingText>();
        SpriteCache sprites;

        struct Spark { public SpriteRenderer sr; public Vector3 origin; public Vector2 v; public float r, age, ttl; public Color c; }
        readonly List<Spark> sparks = new List<Spark>();
        readonly Stack<SpriteRenderer> sparkPool = new Stack<SpriteRenderer>();
        Transform sparkRoot;

        public int LiveFloaters => live.Count;
        public int LiveSparks => sparks.Count;
        public IReadOnlyList<FloatingText> Live => live;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static Color ItemColour(string item) =>
            GoldItems.IsMatch(item) ? Gold : SteelItems.IsMatch(item) ? Steel : Green;

        float Cell => runner != null && runner.Space != null ? runner.Space.Cell : 32f;
        bool Suppressed => runner == null;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (worldCamera == null) worldCamera = Camera.main;
            pool = new ObjectPool<FloatingText>(
                () => Instantiate(floaterPrefab, transform),
                f => f.gameObject.SetActive(true),
                f => f.gameObject.SetActive(false),
                f => Destroy(f.gameObject), false, MaxFloaters, MaxFloaters * 2);
            sparkRoot = new GameObject("Sparks").transform;
            sparkRoot.SetParent(transform, false);
        }

        void Start()
        {
            if (runner == null || runner.Sim == null) return;     // GameRunner disabled itself (no database)
            sprites = new SpriteCache(runner.Database, null);
            var ev = runner.Sim.Events;
            ev.GroundDropped += OnGroundDropped;
            ev.RunReset += ClearAll;
        }

        void OnDestroy()
        {
            if (runner == null || runner.Sim == null) return;
            var ev = runner.Sim.Events;
            ev.GroundDropped -= OnGroundDropped;
            ev.RunReset -= ClearAll;
        }

        void OnGroundDropped(string area, string item, int qty, double x, double y)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            var w = runner.Space.PxToWorld(area, x, y - 6);
            if (!OnScreen(w)) return;
            var col = ItemColour(item);
            Floater(w, "+" + qty, col, sprites.Item(item));
            if (col == Gold) Burst(runner.Space.PxToWorld(area, x, y), Gold, 8);   // prized loot pops
        }

        public bool OnScreen(Vector3 w)
        {
            if (worldCamera == null) return true;
            var v = worldCamera.WorldToViewportPoint(w);
            return v.x >= -0.05f && v.x <= 1.05f && v.y >= -0.05f && v.y <= 1.05f;
        }

        /// <summary>Floater at an area-local px point.</summary>
        public void FloaterAt(string area, double x, double y, string msg, Color c, Sprite icon = null) =>
            Floater(runner.Space.PxToWorld(area, x, y), msg, c, icon);

        public void Floater(Vector3 world, string msg, Color c, Sprite icon = null)
        {
            if (floaterPrefab == null || Suppressed) return;
            if (live.Count >= MaxFloaters) { var old = live[0]; live.RemoveAt(0); pool.Release(old); }
            var f = pool.Get();
            live.Add(f);
            f.Play(world, msg, c, icon, runner.Space.Cell, Done);
        }

        void Done(FloatingText f)
        {
            if (live.Remove(f)) pool.Release(f);
        }

        /// <summary>`fxPickup`: green "+N" at the point (y-8) + 5 sparks + "pickup" SFX.</summary>
        public void Pickup(string area, double x, double y, int n)
        {
            if (n <= 0 || area == null || Suppressed) return;
            FloaterAt(area, x, y - 8, "+" + n, Green);
            Burst(runner.Space.PxToWorld(area, x, y), Green, 5);
            AudioService.Play("pickup");
        }

        /// <summary>`fxSwing`: 4 light sparks at an area-local px point.</summary>
        public void Swing(string area, double x, double y)
        {
            if (area == null || Suppressed) return;
            var w = runner.Space.PxToWorld(area, x, y);
            if (OnScreen(w)) Burst(w, SwingSpark, 4);
        }

        /// <summary>`addBurst(wx, wy, color, n)` — capped at <see cref="MaxSparks"/> live sparks.</summary>
        public void Burst(Vector3 world, Color c, int n)
        {
            if (sparkSprite == null || Suppressed) return;
            float cell = Cell;
            for (int i = 0; i < n && sparks.Count < MaxSparks; i++)
            {
                float a = Random.value * Mathf.PI * 2f, sp = 30f + Random.value * 70f;
                // JS y grows downward: vy = sin*sp - 30 (up) → Unity +30 up
                var v = new Vector2(Mathf.Cos(a) * sp, -Mathf.Sin(a) * sp + 30f) / cell;
                var sr = sparkPool.Count > 0 ? sparkPool.Pop() : NewSparkRenderer();
                sr.gameObject.SetActive(true);
                sr.color = c;
                sparks.Add(new Spark
                {
                    sr = sr, origin = world, v = v, c = c,
                    r = (1.5f + Random.value * 2f) / cell, age = 0f, ttl = (380f + Random.value * 260f) / 1000f,
                });
                Place(sparks[sparks.Count - 1]);
            }
        }

        SpriteRenderer NewSparkRenderer()
        {
            var go = new GameObject("Spark");
            go.transform.SetParent(sparkRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sparkSprite;
            sr.sortingLayerName = "FX";
            sr.sortingOrder = -1;                 // under the floaters
            return sr;
        }

        void Place(Spark s)
        {
            float k = s.age / s.ttl, t = s.age;
            float g = 90f / Cell;                 // 90 px/s² downward
            s.sr.transform.position = new Vector3(s.origin.x + s.v.x * t, s.origin.y + s.v.y * t - g * t * t, s.origin.z);
            float d = 2f * Mathf.Max(0.5f / Cell, s.r * (1f - k * 0.4f));
            s.sr.transform.localScale = new Vector3(d, d, 1f);
            s.sr.color = new Color(s.c.r, s.c.g, s.c.b, s.c.a * Mathf.Max(0f, 1f - k));
        }

        void Update()
        {
            if (sparks.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.age += dt;
                if (s.age >= s.ttl) { Release(s.sr); sparks.RemoveAt(i); continue; }
                sparks[i] = s;
                Place(s);
            }
        }

        void Release(SpriteRenderer sr) { sr.gameObject.SetActive(false); sparkPool.Push(sr); }

        /// <summary>Drop every floater and spark (RunReset).</summary>
        public void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) pool.Release(live[i]);
            live.Clear();
            foreach (var s in sparks) Release(s.sr);
            sparks.Clear();
        }
    }
}
