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

        public const int MaxFlyers = 64, AmbientCount = 18;
        HandController hand;
        WorldViewSync world;
        BuildingViewSync buildings;

        ObjectPool<FloatingText> pool;
        readonly List<FloatingText> live = new List<FloatingText>();
        SpriteCache sprites;

        struct Spark { public SpriteRenderer sr; public Vector3 origin; public Vector2 v; public float r, age, ttl, g, grow; public Color c; }
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
            flyRoot = new GameObject("Flyers").transform;
            flyRoot.SetParent(transform, false);
            if (GetComponent<UiJuice>() == null) gameObject.AddComponent<UiJuice>();
        }

        void Start()
        {
            if (runner == null || runner.Sim == null) return;     // GameRunner disabled itself (no database)
            sprites = new SpriteCache(runner.Database, null);
            var ev = runner.Sim.Events;
            ev.GroundDropped += OnGroundDropped;
            ev.NodeHit += OnNodeHit;
            ev.BuildingCompleted += OnBuildingCompleted;
            ev.BatchFinished += OnBatchFinished;
            ev.EnemyKilled += OnEnemyKilled;
            ev.RunReset += ClearAll;
            hand = FindFirstObjectByType<HandController>();
            world = FindFirstObjectByType<WorldViewSync>();
            buildings = FindFirstObjectByType<BuildingViewSync>();
            BuildAmbient();
        }

        void OnDestroy()
        {
            if (runner == null || runner.Sim == null) return;
            var ev = runner.Sim.Events;
            ev.GroundDropped -= OnGroundDropped;
            ev.NodeHit -= OnNodeHit;
            ev.BuildingCompleted -= OnBuildingCompleted;
            ev.BatchFinished -= OnBatchFinished;
            ev.EnemyKilled -= OnEnemyKilled;
            ev.RunReset -= ClearAll;
        }

        void OnGroundDropped(string area, string item, int qty, double x, double y)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            Juice.NoteDrop(area, item, runner.Space.PxToWorld(area, x, y));
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
            for (int i = 0; i < n && sparks.Count < MaxSparks; i++)
            {
                float a = Random.value * Mathf.PI * 2f, sp = 30f + Random.value * 70f;
                // JS y grows downward: vy = sin*sp - 30 (up) → Unity +30 up
                Spawn(world, new Vector2(Mathf.Cos(a) * sp, -Mathf.Sin(a) * sp + 30f), c,
                    1.5f + Random.value * 2f, 380f + Random.value * 260f, 1f, 0f);
            }
        }

        /// <summary>One pooled spark. vPx px/s (+y up), radius px, ttl ms, gravity multiplier, size growth over life.</summary>
        void Spawn(Vector3 world, Vector2 vPx, Color c, float rPx, float ttlMs, float g, float grow)
        {
            if (sparkSprite == null || sparks.Count >= MaxSparks) return;
            float cell = Cell;
            var sr = sparkPool.Count > 0 ? sparkPool.Pop() : NewSparkRenderer();
            sr.gameObject.SetActive(true);
            sr.color = c;
            var s = new Spark { sr = sr, origin = world, v = vPx / cell, c = c, r = rPx / cell, age = 0f, ttl = ttlMs / 1000f, g = g, grow = grow };
            sparks.Add(s);
            Place(s);
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
            float g = 90f / Cell * s.g;           // 90 px/s² downward (x g)
            s.sr.transform.position = new Vector3(s.origin.x + s.v.x * t, s.origin.y + s.v.y * t - g * t * t, s.origin.z);
            float d = 2f * Mathf.Max(0.5f / Cell, s.r * (1f - k * 0.4f + s.grow * k));
            s.sr.transform.localScale = new Vector3(d, d, 1f);
            s.sr.color = new Color(s.c.r, s.c.g, s.c.b, s.c.a * Mathf.Max(0f, 1f - k));
        }

        // ================= juice: harvest / build / craft / combat =================

        void OnNodeHit(string area, Node n, bool auto)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            bool final = (n.interaction == NodeInteraction.Chop || n.interaction == NodeInteraction.Break) && n.hitsLeft <= 1;
            if (world != null) { var nv = world.FindNodeView(area, n.id); if (nv != null) nv.Punch(final); }
            int cell = runner.Space.Cell;
            var w = runner.Space.PxToWorld(area, (n.col + n.size / 2.0) * cell, (n.row + n.size * 0.55) * cell);
            if (!OnScreen(w)) return;
            var col = Juice.MaterialColour(n.spawnerKind ?? n.kind);
            int cnt = final ? 9 : auto ? 2 : 4;
            for (int i = 0; i < cnt; i++)
            {
                float a = Random.Range(0.15f, 0.85f) * Mathf.PI, sp = 45f + Random.value * 55f;      // chips fly up and out
                Spawn(w, new Vector2(Mathf.Cos(a) * sp * 1.1f, Mathf.Sin(a) * sp), i % 3 == 2 ? Color.Lerp(col, Juice.Mist, 0.45f) : col,
                    1.2f + Random.value * 1.3f, 300f + Random.value * 200f, 1.7f, 0f);
            }
        }

        void OnBuildingCompleted(string area, Building b)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            var (bw, bh) = runner.Config.BuildingSize(b.type);
            int cell = runner.Space.Cell;
            var baseC = runner.Space.PxToWorld(area, (b.col + bw / 2.0) * cell, (b.row + bh) * cell);
            if (!OnScreen(baseC)) return;
            if (buildings != null) { var bv = buildings.Find(area, b.id); if (bv != null) bv.Pop(); }
            float speed = 30f + 14f * bw;
            for (int i = 0; i < 14; i++)                      // dust ring along the footprint base
            {
                float a = (i + Random.value * 0.6f) / 14f * Mathf.PI * 2f;
                Spawn(baseC, new Vector2(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed * 0.4f), Juice.Dust, 2.5f, 460f, 0f, 0.9f);
            }
            for (int i = 0; i < 16; i++)                      // gold sparkle burst over the footprint
            {
                var p = baseC + new Vector3((Random.value - 0.5f) * bw, Random.value * bh, 0f);
                Spawn(p, new Vector2((Random.value - 0.5f) * 40f, 20f + Random.value * 50f), i % 2 == 0 ? Juice.Gold : Juice.GoldGlow,
                    1.5f + Random.value * 1.5f, 550f + Random.value * 350f, 0.35f, 0f);
            }
        }

        void OnBatchFinished(string area, Building b, string item, int qty)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            var (bw, bh) = runner.Config.BuildingSize(b.type);
            int cell = runner.Space.Cell;
            var top = runner.Space.PxToWorld(area, (b.col + bw / 2.0) * cell, b.row * cell + 4);
            if (!OnScreen(top)) return;
            for (int i = 0; i < 5; i++)                       // small puff; the output item pops out via GroundDropped
                Spawn(top + new Vector3((Random.value - 0.5f) * 0.5f, 0f, 0f), new Vector2((Random.value - 0.5f) * 14f, 16f + Random.value * 18f),
                    new Color(Juice.Mist.r, Juice.Mist.g, Juice.Mist.b, 0.75f), 2f + Random.value * 1.5f, 600f + Random.value * 250f, -0.08f, 1.0f);
        }

        void OnEnemyKilled(string area, Enemy e)
        {
            if (Suppressed || !runner.IsUnlocked(area)) return;
            var w = runner.Space.PxToWorld(area, e.x, e.y);
            if (!OnScreen(w)) return;
            Juice.Shake(1.5f / Cell, 0.034f);                 // ~2 frames, 1-2 px
            for (int i = 0; i < 5; i++)                       // essence motes condense into the drop
            {
                float a = (i + Random.value) / 5f * Mathf.PI * 2f, r = (14f + Random.value * 8f) / Cell;
                AddFlyer(null, w + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), w, false, i % 2 == 0 ? Juice.Cyan : Juice.CyanCore, 0.42f + Random.value * 0.12f, 1f);
            }
        }

        // ================= flyers: vacuumed items (curved, accelerating) + essence motes =================

        struct Flyer
        {
            public SpriteRenderer sr; public Vector3 from, to; public bool toHand; public float age, ttl, scale0, side; public Color c; public bool mote;
        }
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Stack<SpriteRenderer> flyPool = new Stack<SpriteRenderer>();
        Transform flyRoot;
        public int LiveFlyers => flyers.Count;

        /// <summary>A vacuumed ground item flies from its spot to the cursor along a curved magnet path, then punches the hand chip.</summary>
        public void FlyToHand(GroundItemView gv)
        {
            if (Juice.ReduceMotion || Suppressed || hand == null || gv == null || gv.Renderer.sprite == null) return;
            var from = gv.transform.position;
            if (((Vector2)from - hand.CursorWorld).sqrMagnitude > 9f || !OnScreen(from)) return;
            AddFlyer(gv.Renderer.sprite, from, default, true, Color.white, 0.24f, gv.transform.localScale.x);
        }

        void AddFlyer(Sprite sprite, Vector3 from, Vector3 to, bool toHand, Color c, float ttl, float scale0)
        {
            if (Juice.ReduceMotion || flyers.Count >= MaxFlyers) return;
            bool mote = sprite == null;
            if (mote) sprite = sparkSprite;
            if (sprite == null) return;
            var sr = flyPool.Count > 0 ? flyPool.Pop() : NewFlyRenderer();
            sr.sprite = sprite; sr.gameObject.SetActive(true);
            var f = new Flyer { sr = sr, from = from, to = to, toHand = toHand, age = 0f, ttl = ttl, scale0 = mote ? 2.5f / Cell : scale0, c = c, mote = mote, side = Random.value < 0.5f ? -1f : 1f };
            flyers.Add(f);
            PlaceFlyer(f);
        }

        SpriteRenderer NewFlyRenderer()
        {
            var go = new GameObject("Flyer");
            go.transform.SetParent(flyRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "FX";
            sr.sortingOrder = 2;
            return sr;
        }

        void PlaceFlyer(Flyer f)
        {
            float t = Mathf.Clamp01(f.age / f.ttl);
            Vector3 target = f.toHand && hand != null ? new Vector3(hand.CursorWorld.x, hand.CursorWorld.y, f.from.z) : f.to;
            float e = f.mote ? Juice.OutQuad(t) : t * t;                 // magnet: accelerates into the hand
            Vector3 d = target - f.from;
            Vector3 ctrl = f.from + d * 0.5f + new Vector3(-d.y, d.x, 0f) * (0.45f * f.side);
            float u = 1f - e;
            f.sr.transform.position = u * u * f.from + 2f * u * e * ctrl + e * e * target;
            float sc = f.mote ? f.scale0 * (1f - 0.4f * t) : f.scale0 * (1f - 0.45f * e);
            f.sr.transform.localScale = new Vector3(sc, sc, 1f);
            f.sr.color = f.mote ? new Color(f.c.r, f.c.g, f.c.b, Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 0.9f + 0.1f))) : f.c;
        }

        void UpdateFlyers(float dt)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.age += dt;
                if (f.age >= f.ttl)
                {
                    f.sr.gameObject.SetActive(false); flyPool.Push(f.sr); flyers.RemoveAt(i);
                    if (f.toHand) Juice.PunchHand();
                    continue;
                }
                flyers[i] = f;
                PlaceFlyer(f);
            }
        }

        // ================= ambient spirit motes =================

        struct Mote { public SpriteRenderer sr; public Vector2 p, v; public float age, ttl, phase, size; public Color c; public bool live; }
        Mote[] motes;
        bool ambientHidden;

        void BuildAmbient()
        {
            if (sparkSprite == null) return;
            motes = new Mote[AmbientCount];
            var root = new GameObject("Ambient").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < motes.Length; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sparkSprite; sr.sortingLayerName = "FX"; sr.sortingOrder = -2;
                go.SetActive(false);
                motes[i].sr = sr;
            }
        }

        bool SpawnMote(ref Mote m)
        {
            if (worldCamera == null) return false;
            float h = worldCamera.orthographicSize, w = h * worldCamera.aspect;
            var cp = worldCamera.transform.position;
            for (int tries = 0; tries < 4; tries++)
            {
                var p = new Vector2(cp.x + (Random.value * 2f - 1f) * w, cp.y + (Random.value * 2f - 1f) * h);
                if (!runner.Space.WorldToArea(p, out var a, out _, out _) || !runner.IsUnlocked(a)) continue;
                bool gold = Random.value < 0.3f;
                m.p = p; m.live = true; m.age = 0f; m.phase = Random.value * 6.28f;
                m.ttl = 6f + Random.value * 5f;
                m.v = new Vector2((Random.value - 0.5f) * 5f, 2.5f + Random.value * 4f) / Cell;       // px/s, slow upward drift
                m.size = (gold ? 3f : 2f) / Cell;
                m.c = gold ? Juice.GoldGlow : Juice.Cyan;
                m.sr.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        void UpdateAmbient(float dt)
        {
            if (motes == null || runner == null || runner.Space == null) return;
            if (Juice.ReduceMotion)
            {
                if (!ambientHidden) { ambientHidden = true; for (int i = 0; i < motes.Length; i++) { motes[i].live = false; motes[i].sr.gameObject.SetActive(false); } }
                return;
            }
            ambientHidden = false;
            float cell = Cell;
            for (int i = 0; i < motes.Length; i++)
            {
                ref var m = ref motes[i];
                if (!m.live) { if (Random.value < 0.05f) SpawnMote(ref m); continue; }
                m.age += dt;
                if (m.age >= m.ttl) { m.live = false; m.sr.gameObject.SetActive(false); continue; }
                float k = m.age / m.ttl;
                var p = m.p + m.v * m.age + new Vector2(Mathf.Sin(m.age * 0.9f + m.phase) * 3f / cell, 0f);
                m.sr.transform.position = new Vector3(Mathf.Round(p.x * cell) / cell, Mathf.Round(p.y * cell) / cell, 0f);     // snap to the pixel grid
                m.sr.transform.localScale = new Vector3(m.size, m.size, 1f);
                m.sr.color = new Color(m.c.r, m.c.g, m.c.b, Mathf.Sin(Mathf.PI * k) * 0.6f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateAmbient(dt);
            if (flyers.Count > 0) UpdateFlyers(dt);
            if (sparks.Count == 0) return;
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

        /// <summary>Drop every floater, spark and flyer (RunReset).</summary>
        public void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) pool.Release(live[i]);
            live.Clear();
            foreach (var s in sparks) Release(s.sr);
            sparks.Clear();
            foreach (var f in flyers) { f.sr.gameObject.SetActive(false); flyPool.Push(f.sr); }
            flyers.Clear();
        }
    }
}
