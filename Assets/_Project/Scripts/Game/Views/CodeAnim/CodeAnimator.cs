using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Time source of the code animation layer (unscaled time; tools can pin it for deterministic screenshots).</summary>
    public static class CodeAnimClock
    {
        /// <summary>Negative = live time. Set by the editor shot act <c>animTime:&lt;seconds&gt;</c>.</summary>
        public static float Override = -1f;
        public static float Now => Override >= 0f ? Override : Time.unscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Override = -1f;
    }

    /// <summary>
    /// Code animation of one still (owner pick anim_route 2026-10-26; data in <see cref="CodeAnimLibrary"/>). Sits on the
    /// GameObject of the SpriteRenderer that draws the still (building art, a future NPC view). Bakes once per art key
    /// (shared between instances): pixel-exact deform frames (sway / shift / scroll / line) and overlay sprites (glow mask,
    /// flame tongues, puffs, glints, sheet, artist layers turned / slid / squashed with nearest-neighbour). Per frame it only
    /// picks sprites, alphas and texel-snapped positions from preallocated arrays: no GC allocation.
    /// Calm motion (<see cref="Juice.ReduceMotion"/>): the rest frame, steady glows / flames, no smoke, toggles held.
    /// </summary>
    public sealed class CodeAnimator : MonoBehaviour
    {
        sealed class FxBake
        {
            public CodeAnimEffect e;
            public int index;
            public Sprite[] sprites;          // variants (flicker), stages (smoke), per-step frames (layer), single (glow, toggle)
            public Vector3[] anchors;         // local position per point (glow / toggle / layer: one)
            public Vector2Int emitter;        // smoke emitter texel
            public int[] layerSlide;          // layer: per-step [dx, dy] texels
            public bool spinFrames;           // layer frames are a 360° spin, picked by time
            public bool sway;                 // flicker flames: tips step 1 texel sideways at random
        }

        sealed class Baked
        {
            public Sprite still, rest;        // rest = still, or the artist base when every layer is delivered
            public Sprite[] baseFrames;       // null = no baked effects (rest stays)
            public FxBake[] fx;
            public Sprite front;              // artist front layer (drawn above the moving layers), or null
            public string[] missing;          // artist files the config waits for
            public float ppu;
            public Vector2 pivotTL;           // still pivot, texels, top-left origin
            public int h;
        }

        struct Slot
        {
            public SpriteRenderer sr;
            public FxBake fx;
            public int point;
            public Sprite sprite;
            public Color color;
            public Vector3 pos;
            public bool shown;
        }

        static readonly Dictionary<string, Baked> cache = new Dictionary<string, Baked>();
        static Material additive;
        static bool additiveTried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); additive = null; additiveTried = false; }

        public CodeAnimEntry Entry { get; private set; }
        public SpriteRenderer Target { get; private set; }
        /// <summary>Buildings: true while the building works. NPC entries (play "always") ignore it.</summary>
        public bool Playing { get; set; }
        /// <summary>Artist files (<c>&lt;key&gt;_layer_&lt;part&gt;</c>, ...) the config names but that are not delivered yet.</summary>
        public IReadOnlyList<string> MissingLayers => baked != null ? baked.missing : Array.Empty<string>();
        public bool Configured => baked != null;
        /// <summary>When false, the owner (BuildingView) calls <see cref="Tick"/> itself.</summary>
        public bool SelfTick = true;

        Baked baked;
        Slot[] slots = new Slot[0];
        int slotCount;
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        bool wasOn;
        Sprite lastBase;

        public int SlotCount => slotCount;
        public SpriteRenderer SlotRenderer(int i) => slots[i].sr;
        public int BaseFrameCount => baked?.baseFrames?.Length ?? 0;
        public Sprite BaseFrame(int i) => baked.baseFrames[i];
        public Sprite Still => baked?.still;

        /// <summary>Get or add the animator on the target's GameObject.</summary>
        public static CodeAnimator For(SpriteRenderer target)
        {
            var a = target.GetComponent<CodeAnimator>();
            return a != null ? a : target.gameObject.AddComponent<CodeAnimator>();
        }

        /// <summary>
        /// Set up for an entry and its still. <paramref name="resolve"/> maps an artist-file suffix ("base", "layer_wheel",
        /// "layer_arm_raised", "layer_front"; ART-SPEC §3.9.8) to its sprite or null. False (and nothing drawn) when the entry
        /// has no effects, the still does not match the config, or pixels cannot be read (no graphics device).
        /// </summary>
        public bool Configure(CodeAnimEntry entry, SpriteRenderer target, Sprite still, Func<string, Sprite> resolve)
        {
            Clear();
            if (entry == null || !entry.HasEffects || target == null || still == null) return false;
            var b = Bake(entry, still, resolve);
            if (b == null) return false;
            Entry = entry; Target = target; baked = b;
            BuildSlots();
            wasOn = false; lastBase = null;
            Tick(CodeAnimClock.Now);
            return true;
        }

        /// <summary>Stop and hide every overlay (pooled renderers stay as children).</summary>
        public void Clear()
        {
            if (baked != null && Target != null && wasOn && Target.sprite != baked.still) Target.sprite = baked.still;
            for (int i = 0; i < pool.Count; i++) if (pool[i] != null) pool[i].enabled = false;
            baked = null; Entry = null; slotCount = 0; wasOn = false; Playing = false;
        }

        void Update() { if (SelfTick) Tick(CodeAnimClock.Now); }

        void OnDisable()
        {
            for (int i = 0; i < slotCount; i++) { slots[i].sr.enabled = false; slots[i].shown = false; }
            wasOn = false;
        }

        // ---------------------------------------------------------------- per frame (no allocation)

        public void Tick(float t)
        {
            if (baked == null) return;
            bool on = Playing || Entry.PlaysAlways;
            if (!on)
            {
                if (wasOn)
                {
                    for (int i = 0; i < slotCount; i++) Hide(ref slots[i]);
                    if (Target.sprite != baked.still) Target.sprite = baked.still;
                    lastBase = null;
                }
                wasOn = false;
                return;
            }
            wasOn = true;
            bool calm = Juice.ReduceMotion;
            int steps = Entry.steps;
            int step = calm ? 0 : CodeAnimMath.Step(t, Entry.fps, steps);
            int count = calm ? 0 : CodeAnimMath.StepCount(t, Entry.fps);

            var bs = baked.baseFrames != null ? baked.baseFrames[step % baked.baseFrames.Length] : baked.rest;
            if (bs != lastBase || Target.sprite != bs) { Target.sprite = bs; lastBase = bs; }

            for (int i = 0; i < slotCount; i++)
            {
                ref var s = ref slots[i];
                var fx = s.fx;
                if (fx == null) { Show(ref s, frontSprite, Color.white, Vector3.zero); continue; }   // artist front layer: static, above the moving parts
                var e = fx.e;
                switch (e.kind)
                {
                    case CodeAnimKind.Glow:
                    {
                        float v;
                        if (calm) { float ca = e.CalmAlpha; if (ca < 0f) { Hide(ref s); break; } v = 0.5f; Show(ref s, fx.sprites[0], Tint(e, v, ca), fx.anchors[0] + Follow(e, step)); break; }
                        v = CodeAnimMath.Pulse(t, e.period, e.phase);
                        Show(ref s, fx.sprites[0], Tint(e, v, CodeAnimMath.QuantizeAlpha(Mathf.Lerp(e.alpha[0], e.alpha[1], v))), fx.anchors[0] + Follow(e, step));
                        break;
                    }
                    case CodeAnimKind.Flicker:
                    {
                        int n = fx.anchors.Length;
                        if (calm)
                        {
                            float ca = e.CalmAlpha;
                            if (ca < 0f || (e.mode == "cycle" && s.point != 0)) { Hide(ref s); break; }
                            Show(ref s, fx.sprites[0], WithAlpha(e.colA, ca), fx.anchors[s.point] + Follow(e, step));
                            break;
                        }
                        if (e.mode == "cycle")
                        {
                            int every = Mathf.Max(n, e.onSteps != null && e.onSteps.Length > 0 ? e.onSteps[0] : n);
                            if (count % every != s.point) { Hide(ref s); break; }
                            Show(ref s, fx.sprites[0], WithAlpha(e.colA, CodeAnimMath.QuantizeAlpha(e.alpha[1])), fx.anchors[s.point] + Follow(e, step));
                            break;
                        }
                        float h = CodeAnimMath.Hash01(count, s.point, fx.index);
                        int vi = fx.sprites.Length > 1 ? (int)(CodeAnimMath.Hash01(count, s.point, fx.index + 97) * fx.sprites.Length) % fx.sprites.Length : 0;
                        var at = fx.anchors[s.point] + Follow(e, step);
                        if (fx.sway) at.x += ((int)(CodeAnimMath.Hash01(count, s.point, fx.index + 13) * 3f) - 1) / baked.ppu;
                        Show(ref s, fx.sprites[vi], WithAlpha(e.colA, CodeAnimMath.QuantizeAlpha(Mathf.Lerp(e.alpha[0], e.alpha[1], h))), at);
                        break;
                    }
                    case CodeAnimKind.Smoke:
                    {
                        if (calm)
                        {
                            float ca = e.CalmAlpha;
                            if (ca < 0f || s.point != 0) { Hide(ref s); break; }
                            Show(ref s, fx.sprites[0], new Color(1f, 1f, 1f, ca), SmokePos(fx, 0, 0f, 0)); break;
                        }
                        float life = Mathf.Max(0.2f, e.period);
                        float cyc = t / life + s.point / (float)e.count;
                        int gen = Mathf.FloorToInt(cyc);
                        float u = cyc - gen;
                        int stage = Mathf.Min(3, (int)(u * 4f));
                        var sp = fx.sprites[Mathf.Min(stage, fx.sprites.Length - 1)];
                        float a = e.alphaSteps[Mathf.Min(stage, e.alphaSteps.Length - 1)];
                        int wob = (int)(CodeAnimMath.Hash01(gen, s.point, stage) * 3f) - 1;
                        Show(ref s, sp, new Color(1f, 1f, 1f, CodeAnimMath.QuantizeAlpha(a)), SmokePos(fx, s.point, u, wob));
                        break;
                    }
                    case CodeAnimKind.Toggle:
                    {
                        bool show;
                        if (calm) show = e.CalmAlpha >= 0f;
                        else { show = false; if (e.onSteps != null) for (int k = 0; k < e.onSteps.Length; k++) if (e.onSteps[k] == step) { show = true; break; } }
                        if (show) Show(ref s, fx.sprites[0], Color.white, fx.anchors[0] + Follow(e, step)); else Hide(ref s);
                        break;
                    }
                    case CodeAnimKind.Layer:
                    {
                        int fi;
                        if (calm) fi = 0;
                        else if (fx.spinFrames) fi = (int)(Mathf.Repeat(t * e.spin / 360f, 1f) * fx.sprites.Length) % fx.sprites.Length;
                        else fi = step % fx.sprites.Length;
                        var pos = fx.anchors[0];
                        if (!calm && fx.layerSlide != null && fx.layerSlide.Length >= 2)
                        {
                            int k = (step % (fx.layerSlide.Length / 2)) * 2;
                            pos += new Vector3(fx.layerSlide[k] / baked.ppu, -fx.layerSlide[k + 1] / baked.ppu, 0f);
                        }
                        Show(ref s, fx.sprites[fi], Color.white, pos);
                        break;
                    }
                }
            }
        }

        Vector3 SmokePos(FxBake fx, int point, float u, int wob)
        {
            var e = fx.e;
            int x = fx.emitter.x + Mathf.RoundToInt(e.drift * u) + wob, y = fx.emitter.y - Mathf.RoundToInt(e.rise * u);
            return Local(x, y);
        }

        /// <summary>Per-step texel offset of an overlay that rides on a baked shift (the lamp on a nodding head).</summary>
        Vector3 Follow(CodeAnimEffect e, int step)
        {
            int off = CodeAnimBake.Off(e.offsets, step);
            if (off == 0) return Vector3.zero;
            return e.axis == "x" ? new Vector3(off / baked.ppu, 0f, 0f) : new Vector3(0f, -off / baked.ppu, 0f);
        }

        static Color Tint(CodeAnimEffect e, float v, float a) { var c = Color.Lerp(e.colA, e.colB, v); c.a = a; return c; }
        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static void Hide(ref Slot s)
        {
            if (!s.shown) return;
            s.sr.enabled = false; s.shown = false;
        }

        static void Show(ref Slot s, Sprite sp, Color c, Vector3 pos)
        {
            if (!s.shown) { s.sr.enabled = true; s.shown = true; }
            if (s.sprite != sp) { s.sr.sprite = sp; s.sprite = sp; }
            if (s.color != c) { s.sr.color = c; s.color = c; }
            if (s.pos != pos) { s.sr.transform.localPosition = pos; s.pos = pos; }
        }

        /// <summary>Local position of texel corner (x, y) (top-left origin) under the still's pivot: always on the texel grid.</summary>
        Vector3 Local(int x, int y) => CodeAnimMath.TexelToLocal(x, y, baked.pivotTL, baked.ppu);

        // ---------------------------------------------------------------- setup

        void BuildSlots()
        {
            int need = 0;
            foreach (var fx in baked.fx) need += SlotsFor(fx);
            if (baked.front != null) need++;
            if (slots.Length < need) slots = new Slot[need];
            slotCount = need;
            int si = 0;
            var lit = Target.sharedMaterial;
            foreach (var fx in baked.fx)
                for (int p = 0; p < SlotsFor(fx); p++)
                {
                    var sr = Renderer(si);
                    sr.sharedMaterial = fx.e.additive && Additive() != null ? additive : lit;
                    sr.sortingLayerID = Target.sortingLayerID;
                    sr.sortingOrder = Target.sortingOrder + 1 + fx.e.sort;
                    slots[si] = new Slot { sr = sr, fx = fx, point = p, pos = new Vector3(float.NaN, 0f, 0f) };
                    si++;
                }
            if (baked.front != null)
            {
                var sr = Renderer(si);
                sr.sharedMaterial = lit; sr.sortingLayerID = Target.sortingLayerID; sr.sortingOrder = Target.sortingOrder + 3;
                slots[si] = new Slot { sr = sr, fx = null, pos = new Vector3(float.NaN, 0f, 0f) };
                si++;
            }
            frontSprite = baked.front;
            for (int i = si; i < pool.Count; i++) pool[i].enabled = false;
            for (int i = 0; i < si; i++) { slots[i].sr.enabled = false; slots[i].shown = false; slots[i].sr.sprite = null; }
        }

        Sprite frontSprite;

        static int SlotsFor(FxBake fx)
        {
            switch (fx.e.kind)
            {
                case CodeAnimKind.Smoke: return fx.e.count;
                case CodeAnimKind.Flicker: return fx.anchors.Length;
                default: return 1;
            }
        }

        SpriteRenderer Renderer(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("CodeAnimFx" + pool.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                pool.Add(sr);
            }
            return pool[i];
        }

        static Material Additive()
        {
            if (additiveTried) return additive;
            additiveTried = true;
            var sh = Resources.Load<Shader>("CodeAnimAdditive");
            if (sh != null && sh.isSupported) additive = new Material(sh) { name = "CodeAnimAdditive" };
            return additive;
        }

        // ---------------------------------------------------------------- bake (once per key + still, shared)

        static Baked Bake(CodeAnimEntry entry, Sprite still, Func<string, Sprite> resolve)
        {
            string ck = entry.key + "#" + still.GetInstanceID();
            if (cache.TryGetValue(ck, out var hit)) return hit;
            var img = PixelImage.FromSprite(still);
            if (img == null) return null;     // no graphics device: no animation (tests, -nographics)
            if (entry.StillW > 0 && (img.w != entry.StillW || img.h != entry.StillH))
            {
                Debug.LogWarning($"[CodeAnim] {entry.key}: still is {img.w}x{img.h}, config expects {entry.StillW}x{entry.StillH}; animation off");
                cache[ck] = null;
                return null;
            }
            var b = BakeImage(entry, still, img, resolve);
            cache[ck] = b;
            return b;
        }

        /// <summary>Bake from pixels already in memory (tests use readable textures; the runtime path reads the still first).</summary>
        static Baked BakeImage(CodeAnimEntry entry, Sprite still, PixelImage img, Func<string, Sprite> resolve)
        {
            float ppu = still.pixelsPerUnit;
            var pivotTL = new Vector2(still.pivot.x, img.h - still.pivot.y);
            var b = new Baked { still = still, rest = still, ppu = ppu, pivotTL = pivotTL, h = img.h };

            // artist layers (ART-SPEC §3.9.8): every layer effect needs its files; the base replaces the still only when all are here
            var missing = new List<string>();
            var layerImgs = new Dictionary<string, PixelImage>();
            bool anyLayer = false;
            foreach (var e in entry.effects)
            {
                if (e == null || e.kind != CodeAnimKind.Layer) continue;
                anyLayer = true;
                string part = string.IsNullOrEmpty(e.sprite) || e.sprite == "mask" ? e.name : e.sprite;
                Need(CodeAnimLibrary.LayerSuffix(part));
                if (e.poses != null) foreach (var p in e.poses) if (!string.IsNullOrEmpty(p)) Need(CodeAnimLibrary.PoseSuffix(part, p));
            }
            PixelImage baseImg = img;
            if (anyLayer)
            {
                if (missing.Count == 0)
                {
                    var bs = resolve?.Invoke(CodeAnimLibrary.BaseSuffix);
                    var bi = bs != null ? PixelImage.FromSprite(bs) : null;
                    if (bi != null && bi.w == img.w && bi.h == img.h) { baseImg = bi; b.rest = bs; }
                    else missing.Add(CodeAnimLibrary.FileKey(entry.key, CodeAnimLibrary.BaseSuffix));
                    var fs = resolve?.Invoke(CodeAnimLibrary.FrontSuffix);
                    if (fs != null && missing.Count == 0) b.front = fs;
                }
                if (missing.Count > 0) { baseImg = img; b.rest = still; b.front = null; }
            }
            b.missing = missing.ToArray();
            bool layersLive = anyLayer && missing.Count == 0;

            // baked deform frames
            bool anyBaked = false;
            foreach (var e in entry.effects) if (e != null && e.IsBaked && Active(e, layersLive)) anyBaked = true;
            if (anyBaked)
            {
                b.baseFrames = new Sprite[entry.steps];
                for (int s = 0; s < entry.steps; s++)
                {
                    var f = new PixelImage(baseImg);
                    foreach (var e in entry.effects) if (e != null && e.IsBaked && Active(e, layersLive)) CodeAnimBake.ApplyBaked(e, f, s);
                    b.baseFrames[s] = f.ToSprite(entry.key + "_code_" + s, ppu, pivotTL);
                }
            }

            // overlays
            var list = new List<FxBake>();
            int idx = 0;
            foreach (var e in entry.effects)
            {
                idx++;
                if (e == null || e.IsBaked || e.kind == CodeAnimKind.Unknown || !Active(e, layersLive)) continue;
                if (e.kind == CodeAnimKind.Layer && !layersLive) continue;
                var fx = new FxBake { e = e, index = idx * 31 };
                switch (e.kind)
                {
                    case CodeAnimKind.Glow:
                    case CodeAnimKind.Flicker when e.sprite == "mask" || e.sprite == "maskColor":
                    {
                        var r = CodeAnimBake.Rect(e, img);
                        var cut = CodeAnimBake.Cut(e, img, r, e.sprite == "maskColor");
                        fx.sprites = new[] { cut.ToSprite(entry.key + "_" + e.name, ppu, Vector2.zero) };
                        fx.anchors = new[] { Loc(r.x, r.y) };
                        break;
                    }
                    case CodeAnimKind.Flicker:
                    {
                        int w = e.size != null && e.size.Length >= 2 ? e.size[0] : 5, h = e.size != null && e.size.Length >= 2 ? e.size[1] : 5;
                        int n = e.sprite == "flame" ? Mathf.Max(1, e.count) : 1;
                        fx.sprites = new Sprite[n];
                        for (int v = 0; v < n; v++)
                        {
                            var pi = e.sprite == "flame" ? CodeAnimBake.Flame(w, Mathf.Max(3, h - (v * 3) % Mathf.Max(1, h / 2)), v) : CodeAnimBake.Star(w);
                            fx.sprites[v] = pi.ToSprite(entry.key + "_" + e.name + v, ppu, new Vector2(pi.w / 2, pi.h));
                        }
                        fx.anchors = Points(e);
                        fx.sway = e.sprite == "flame";
                        break;
                    }
                    case CodeAnimKind.Smoke:
                    {
                        int w0 = e.size != null && e.size.Length >= 2 ? e.size[0] : 6, h0 = e.size != null && e.size.Length >= 2 ? e.size[1] : 6;
                        int w1 = e.growTo != null && e.growTo.Length >= 2 ? e.growTo[0] : w0 * 3, h1 = e.growTo != null && e.growTo.Length >= 2 ? e.growTo[1] : h0 * 2;
                        fx.sprites = new Sprite[4];
                        for (int k = 0; k < 4; k++)
                        {
                            int pw = Mathf.RoundToInt(Mathf.Lerp(w0, w1, k / 3f)), ph = Mathf.RoundToInt(Mathf.Lerp(h0, h1, k / 3f));
                            var pi = CodeAnimBake.Puff(pw, ph, k);
                            fx.sprites[k] = pi.ToSprite(entry.key + "_puff" + k, ppu, new Vector2(pi.w / 2, pi.h));
                        }
                        fx.emitter = e.points != null && e.points.Length >= 2 ? new Vector2Int(e.points[0], e.points[1]) : new Vector2Int(img.w / 2, 0);
                        fx.anchors = new[] { Vector3.zero };
                        break;
                    }
                    case CodeAnimKind.Toggle:
                    {
                        var r = CodeAnimBake.Rect(e, img);
                        PixelImage pi = e.sprite == "sheet"
                            ? CodeAnimBake.Sheet(r.width, r.height, string.IsNullOrEmpty(e.colorA) ? CodeAnimBake.DefaultSheetFill : e.colA, string.IsNullOrEmpty(e.colorB) ? CodeAnimBake.DefaultSheetEdge : e.colB)
                            : CodeAnimBake.Cut(e, img, r, true);
                        fx.sprites = new[] { pi.ToSprite(entry.key + "_" + e.name, ppu, Vector2.zero) };
                        fx.anchors = new[] { Loc(r.x, r.y) };
                        break;
                    }
                    case CodeAnimKind.Layer:
                    {
                        string part = string.IsNullOrEmpty(e.sprite) || e.sprite == "mask" ? e.name : e.sprite;
                        var rest = layerImgs[CodeAnimLibrary.LayerSuffix(part)];
                        var pv = e.pivot != null && e.pivot.Length >= 2 ? new Vector2(e.pivot[0], e.pivot[1]) : new Vector2(rest.w / 2, rest.h / 2);
                        var frames = new List<Sprite>();
                        if (e.spin != 0f)
                        {
                            int n = Mathf.Max(4, e.count > 1 ? e.count : 24);
                            for (int k = 0; k < n; k++) frames.Add(CodeAnimBake.Transform(rest, pv, 360f * k / n, 1f).ToSprite(entry.key + "_" + part + k, ppu, pivotTL));
                            fx.spinFrames = true;
                        }
                        else
                        {
                            for (int s = 0; s < entry.steps; s++)
                            {
                                string pose = e.poses != null && e.poses.Length > 0 ? e.poses[s % e.poses.Length] : "";
                                var src = string.IsNullOrEmpty(pose) ? rest : layerImgs[CodeAnimLibrary.PoseSuffix(part, pose)];
                                float ang = e.angles != null && e.angles.Length > 0 ? e.angles[s % e.angles.Length] : 0f;
                                float sc = e.scales != null && e.scales.Length > 0 ? e.scales[s % e.scales.Length] : 1f;
                                var o = ang == 0f && sc == 1f ? src : CodeAnimBake.Transform(src, pv, ang, sc);
                                frames.Add(o.ToSprite(entry.key + "_" + part + "_" + s, ppu, pivotTL));
                            }
                        }
                        fx.sprites = frames.ToArray();
                        fx.anchors = new[] { Vector3.zero };
                        fx.layerSlide = e.offsets != null && e.offsets.Length >= 2 ? e.offsets : null;
                        break;
                    }
                    default: continue;
                }
                list.Add(fx);
            }
            b.fx = list.ToArray();
            return b;

            void Need(string suffix)
            {
                string fk = CodeAnimLibrary.FileKey(entry.key, suffix);
                if (layerImgs.ContainsKey(suffix) || missing.Contains(fk)) return;
                var sp = resolve?.Invoke(suffix);
                var pi = sp != null ? PixelImage.FromSprite(sp) : null;
                if (pi == null || pi.w != img.w || pi.h != img.h) { missing.Add(fk); return; }
                layerImgs[suffix] = pi;
            }
            Vector3 Loc(int x, int y) => CodeAnimMath.TexelToLocal(x, y, pivotTL, ppu);
            Vector3[] Points(CodeAnimEffect e)
            {
                if (e.points == null || e.points.Length < 2) return new[] { Loc(img.w / 2, img.h / 2) };
                var a = new Vector3[e.points.Length / 2];
                for (int i = 0; i < a.Length; i++) a[i] = Loc(e.points[i * 2], e.points[i * 2 + 1]);
                return a;
            }
        }

        static bool Active(CodeAnimEffect e, bool layersLive) => !(e.onlyWithoutLayers && layersLive);

        /// <summary>Configure without the shared bake cache (tests, art tools): a fresh bake every call.</summary>
        public bool ConfigureUncached(CodeAnimEntry entry, SpriteRenderer target, Sprite still, Func<string, Sprite> resolve)
        {
            Clear();
            var img = PixelImage.FromSprite(still);
            if (entry == null || img == null) return false;
            Entry = entry; Target = target; baked = BakeImage(entry, still, img, resolve);
            BuildSlots();
            wasOn = false; lastBase = null;
            return true;
        }
    }
}
