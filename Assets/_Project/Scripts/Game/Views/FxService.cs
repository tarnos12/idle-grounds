using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One-shot world FX (§5): pooled floaters (cap 60). Listens to SimEvents.GroundDropped for
    /// "+qty" item floaters (unlocked + on-screen only). Sparks / SFX are deferred.
    /// </summary>
    public class FxService : MonoBehaviour
    {
        public const int MaxFloaters = 60;
        public static readonly Color Green = Hex("#4ade80"), Gold = Hex("#fbbf24"), Steel = Hex("#cbd5e1"), Danger = Hex("#f87171");
        static readonly Regex GoldItems = new Regex("essence|scale|pill|elixir|talisman|jade");
        static readonly Regex SteelItems = new Regex("iron|steel|star|tools|glass");

        [SerializeField] GameRunner runner;
        [SerializeField] FloatingText floaterPrefab;
        [SerializeField] Camera worldCamera;

        ObjectPool<FloatingText> pool;
        readonly List<FloatingText> live = new List<FloatingText>();
        SpriteCache sprites;

        public int LiveFloaters => live.Count;
        public IReadOnlyList<FloatingText> Live => live;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static Color ItemColour(string item) =>
            GoldItems.IsMatch(item) ? Gold : SteelItems.IsMatch(item) ? Steel : Green;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (worldCamera == null) worldCamera = Camera.main;
            pool = new ObjectPool<FloatingText>(
                () => Instantiate(floaterPrefab, transform),
                f => f.gameObject.SetActive(true),
                f => f.gameObject.SetActive(false),
                f => Destroy(f.gameObject), false, MaxFloaters, MaxFloaters * 2);
        }

        void Start()
        {
            sprites = new SpriteCache(runner.Database, null);
            runner.Sim.Events.GroundDropped += OnGroundDropped;
        }

        void OnDestroy()
        {
            if (runner != null && runner.Sim != null) runner.Sim.Events.GroundDropped -= OnGroundDropped;
        }

        void OnGroundDropped(string area, string item, int qty, double x, double y)
        {
            if (!runner.IsUnlocked(area)) return;
            var w = runner.Space.PxToWorld(area, x, y - 6);
            if (!OnScreen(w)) return;
            Floater(w, "+" + qty, ItemColour(item), sprites.Item(item));
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
            if (floaterPrefab == null) return;
            if (live.Count >= MaxFloaters) { var old = live[0]; live.RemoveAt(0); pool.Release(old); }
            var f = pool.Get();
            live.Add(f);
            f.Play(world, msg, c, icon, runner.Space.Cell, Done);
        }

        void Done(FloatingText f)
        {
            if (live.Remove(f)) pool.Release(f);
        }
    }
}
