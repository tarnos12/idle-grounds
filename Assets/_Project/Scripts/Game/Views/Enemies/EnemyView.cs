using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A fox spirit / lured boar (ui-input-render §2.6): sprite 26 px (boss 36 px) centred on the sim
    /// position, hit squash about the centre for 180 ms after hitAt (+ a small flinch shake), and an
    /// HP pip row (maxHp circles r=3 px, spacing 10 px, centred 24 px above; red remaining, faint lost;
    /// a 40 px bar instead when maxHp &gt; <see cref="MaxPips"/>).
    /// Pull-based: <see cref="Refresh"/> each LateUpdate from <see cref="EnemyViewSync"/>.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public class EnemyView : MonoBehaviour
    {
        const float SquashMs = 180f;
        public const int MaxPips = 16;
        public static readonly Color PipLive = UiPalette.Hex("#f87171"), PipLost = new Color(1f, 1f, 1f, 0.25f);

        [SerializeField] Transform squash;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Transform pips;
        [SerializeField] SpriteRenderer pipTemplate;
        [SerializeField] SpriteRenderer barTrack;
        [SerializeField] SpriteRenderer barFill;
        [SerializeField] SortingGroup sortingGroup;

        readonly List<SpriteRenderer> pipPool = new List<SpriteRenderer>();
        public string Area { get; private set; }
        public Enemy Enemy { get; private set; }
        internal int seenFrame;
        int lastHp = -1, lastMax = -1;

        /// <summary>Red (remaining) pips currently shown.</summary>
        public int LivePips
        {
            get { int n = 0; foreach (var p in pipPool) if (p.gameObject.activeSelf && p.color == PipLive) n++; return n; }
        }

        public void Bind(string area, Enemy e, Sprite sprite)
        {
            Area = area; Enemy = e;
            name = $"Enemy_{area}_{e.id}{(e.kind == "boss" ? "_boss" : "")}";
            ViewKit.Fit(spriteRenderer, sprite, e.kind == "boss" ? 36f : 26f);
            if (pipTemplate != null) pipTemplate.gameObject.SetActive(false);
            lastHp = lastMax = -1;
            squash.localScale = Vector3.one;
        }

        public void Refresh(AreaSpace space, double now)
        {
            var e = Enemy;
            transform.position = space.PxToWorld(Area, e.x, e.y);
            sortingGroup.sortingOrder = (int)(e.y / space.Cell);

            float sy = 1f, shake = 0f;
            double dt = now - e.hitAt;
            if (e.hitAt > 0 && dt >= 0 && dt < SquashMs)
            {
                float t = (float)(dt / SquashMs);
                sy = t < 0.35f ? Mathf.Lerp(1f, 0.84f, t / 0.35f)
                   : t < 0.7f ? Mathf.Lerp(0.84f, 1.06f, (t - 0.35f) / 0.35f)
                   : Mathf.Lerp(1.06f, 1f, (t - 0.7f) / 0.3f);
                shake = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * ViewKit.U(2.5f);
            }
            squash.localScale = new Vector3(1f + (1f - sy) * 0.4f, sy, 1f);
            squash.localPosition = new Vector3(shake, 0f, 0f);

            if (e.hp != lastHp || e.maxHp != lastMax) { lastHp = e.hp; lastMax = e.maxHp; LayoutHp(); }
        }

        void LayoutHp()
        {
            var e = Enemy;
            float y = ViewKit.U(24f);
            bool usePips = e.maxHp <= MaxPips;
            ViewKit.Show(barTrack, !usePips); ViewKit.Show(barFill, !usePips);
            int n = usePips ? Mathf.Max(0, e.maxHp) : 0;
            while (pipPool.Count < n)
            {
                var p = Instantiate(pipTemplate, pips);
                p.name = "Pip" + pipPool.Count;
                pipPool.Add(p);
            }
            float spacing = ViewKit.U(10f), x0 = -(n - 1) * spacing * 0.5f;
            for (int i = 0; i < pipPool.Count; i++)
            {
                var p = pipPool[i];
                bool on = i < n;
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                p.transform.localPosition = new Vector3(x0 + i * spacing, y, 0f);
                p.transform.localScale = Vector3.one * ViewKit.U(6f);
                p.color = i < e.hp ? PipLive : PipLost;
            }
            if (!usePips)
            {
                float w = ViewKit.U(40f), h = ViewKit.U(4f);
                ViewKit.Bar(barTrack, -w * 0.5f, y, w, h);
                ViewKit.Bar(barFill, -w * 0.5f, y, w * Mathf.Clamp01(e.hp / (float)Mathf.Max(1, e.maxHp)), h);
            }
        }
    }
}
