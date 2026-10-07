using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// UI juice (added at runtime by <see cref="FxService"/>): button press scale feedback for every uGUI Button
    /// (0.94 while held, springs back with a tiny overshoot) without touching individual prefabs, and a pooled
    /// pixel-square sparkle burst on a panel (<see cref="Sparkle"/>) used by claim / quest-complete celebrations.
    /// Zero allocations at steady state; Reduce motion skips the tweens.
    /// </summary>
    public class UiJuice : MonoBehaviour
    {
        const float PressScale = 0.94f, RestSec = 0.12f, ParticleG = 360f;
        const int MaxParticles = 40;
        public static UiJuice Instance { get; private set; }

        Transform held; Vector3 heldBase;
        struct Rest { public Transform t; public Vector3 baseScale; public float age; }
        readonly Rest[] rests = new Rest[6];

        struct P { public Image img; public Vector2 pos, vel; public float age, ttl, size; public Color c; public bool live; }
        P[] parts;
        RectTransform root;
        Canvas canvas;
        public int LiveParticles { get { int n = 0; if (parts != null) foreach (var p in parts) if (p.live) n++; return n; } }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void EnsureRoot()
        {
            if (root != null) return;
            var go = GameObject.Find("UI/Canvas");
            canvas = go != null ? go.GetComponent<Canvas>() : FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var rg = new GameObject("JuiceFx", typeof(RectTransform));
            root = rg.GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            parts = new P[MaxParticles];
            for (int i = 0; i < parts.Length; i++)
            {
                var ig = new GameObject("P", typeof(RectTransform), typeof(Image));
                var img = ig.GetComponent<Image>();
                img.raycastTarget = false;
                ig.transform.SetParent(root, false);
                var rt = (RectTransform)ig.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
                ig.SetActive(false);
                parts[i].img = img;
            }
        }

        /// <summary>Gold/cyan pixel-sparkle burst around a UI rect (claim / quest-complete celebration).</summary>
        public void Sparkle(RectTransform target, int n = 22)
        {
            if (target == null) return;
            EnsureRoot();
            if (root == null) return;
            root.SetAsLastSibling();
            float sf = Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
            var corners = cornerBuf;
            target.GetWorldCorners(corners);             // overlay canvas: world == screen px
            Vector2 lo = corners[0] / sf, hi = corners[2] / sf, mid = (lo + hi) * 0.5f;
            for (int k = 0; k < n; k++)
            {
                int i = FreeSlot(); if (i < 0) return;
                ref var p = ref parts[i];
                var at = new Vector2(Mathf.Lerp(lo.x, hi.x, Random.value), Mathf.Lerp(lo.y, hi.y, Random.value));
                var dir = at - mid; if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();
                p.pos = at; p.vel = dir * (90f + Random.value * 150f) + new Vector2(0f, 60f + Random.value * 80f);
                p.age = 0f; p.ttl = 0.5f + Random.value * 0.35f; p.live = true;
                p.size = Random.value < 0.35f ? 8f : 5f;
                float r = Random.value;
                p.c = r < 0.5f ? Juice.Gold : r < 0.8f ? Juice.GoldGlow : Juice.Cyan;
                p.img.gameObject.SetActive(true);
                Draw(ref p);
            }
        }
        readonly Vector3[] cornerBuf = new Vector3[4];

        int FreeSlot() { for (int i = 0; i < parts.Length; i++) if (!parts[i].live) return i; return -1; }

        void Draw(ref P p)
        {
            float k = p.age / p.ttl;
            var rt = (RectTransform)p.img.transform;
            rt.anchoredPosition = new Vector2(Mathf.Round(p.pos.x), Mathf.Round(p.pos.y));        // pixel snapped
            rt.sizeDelta = new Vector2(p.size, p.size);
            p.img.color = new Color(p.c.r, p.c.g, p.c.b, 1f - k * k);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Press(dt);
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                ref var p = ref parts[i];
                if (!p.live) continue;
                p.age += dt;
                if (p.age >= p.ttl) { p.live = false; p.img.gameObject.SetActive(false); continue; }
                p.vel.y -= ParticleG * dt;
                p.pos += p.vel * dt;
                Draw(ref p);
            }
        }

        void Press(float dt)
        {
            for (int i = 0; i < rests.Length; i++)
            {
                ref var r = ref rests[i];
                if (r.t == null) continue;
                r.age += dt;
                float k = r.age / RestSec;
                if (k >= 1f) { r.t.localScale = r.baseScale; r.t = null; continue; }
                r.t.localScale = r.baseScale * (PressScale + (1f - PressScale) * Juice.OutBack(k));
            }
            var m = Mouse.current; var es = EventSystem.current;
            if (m == null || es == null) return;
            if (m.leftButton.wasPressedThisFrame && !Juice.ReduceMotion && es.IsPointerOverGameObject())
            {
                var sel = es.currentSelectedGameObject;
                if (sel != null && sel.TryGetComponent<Button>(out var b) && b.interactable)
                {
                    var t = sel.transform;
                    for (int i = 0; i < rests.Length; i++) if (rests[i].t == t) { heldBase = rests[i].baseScale; rests[i].t = null; goto have; }
                    heldBase = t.localScale;
                    have:
                    held = t;
                    held.localScale = heldBase * PressScale;
                }
            }
            if (held != null && (m.leftButton.wasReleasedThisFrame || !m.leftButton.isPressed))
            {
                for (int i = 0; i < rests.Length; i++)
                    if (rests[i].t == null) { rests[i] = new Rest { t = held, baseScale = heldBase, age = 0f }; held = null; break; }
                if (held != null) { held.localScale = heldBase; held = null; }
            }
        }
    }
}
