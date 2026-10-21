using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One pixel font for every text (owner pick vp_look = font, 2026-10-21: "all UI needs to be consistent").
    /// The TMP default font is Pixelify Sans (menu Idle Grounds/UI/Build Pixel Font). Texts serialized with the old
    /// LiberationSans asset (scene, prefabs, pooled world text) are switched at runtime: once per loaded scene and for every
    /// text that rebuilds later (TMP's text-changed event), so nothing in the scene has to be rewritten.
    /// <para>Pixelify Sans tells letters apart by 1-pixel slits (C vs O, 5 vs 8, e, G), so it is only legible on whole font
    /// pixels. Screen UI therefore uses a hinted raster cut at 22 px (2 UI px per font pixel, like the 2x UI art), point
    /// filtered, on pixel-perfect canvases, with every uGUI size snapped (<see cref="SnappedSize"/>). World text and outlined
    /// text use the SDF cut (sampled on the font grid) and keep their material styling. Glyphs Pixelify lacks (arrows,
    /// ticks) fall back to LiberationSans.</para>
    /// </summary>
    public static class UiFont
    {
        const string OldFontPrefix = "LiberationSans", NewFontPrefix = "PixelifySans";

        /// <summary>Resources name of the bold cut (Art/Fonts/PixelifySans/Resources).</summary>
        public const string BoldResource = "PixelifySans Bold SDF", RasterResource = "PixelifySans Pixel22";
        static TMP_FontAsset pixel, pixelBold, raster22;
        static bool resolved, hooked;
        static readonly Dictionary<(Material, TMP_FontAsset), Material> mats = new Dictionary<(Material, TMP_FontAsset), Material>();
        /// <summary>Texts switched inside TMP's own rebuild: their mesh is regenerated once more after the frame's updates.</summary>
        static readonly List<TMP_Text> pending = new List<TMP_Text>();
        static Pump pump;

        /// <summary>Rebuilds the meshes of texts switched during a TMP rebuild (a font change there is not always picked up).</summary>
        sealed class Pump : MonoBehaviour
        {
            readonly List<TMP_Text> work = new List<TMP_Text>();
            readonly List<TMP_Text> waiting = new List<TMP_Text>();
            void LateUpdate()
            {
                var ui = UiCanvasScale();
                if (ui > 0f && !Mathf.Approximately(ui, lastScale)) { lastScale = ui; SwapAll(); }     // the phone scaler lands after boot: re-snap every text for the real scale
                for (int i = waiting.Count - 1; i >= 0; i--)
                {
                    var w = waiting[i];
                    if (w == null) { waiting.RemoveAt(i); continue; }
                    if (w.isActiveAndEnabled) { w.ForceMeshUpdate(true, true); waiting.RemoveAt(i); }
                }
                if (pending.Count == 0) return;
                work.Clear(); work.AddRange(pending); pending.Clear();
                foreach (var t in work)
                {
                    if (t == null) continue;
                    // switched while hidden (bar labels on a phone, closed panels): rebuild once it is shown, or the old
                    // atlas UVs are drawn with the new font material (garbled glyphs)
                    if (t.isActiveAndEnabled) t.ForceMeshUpdate(true, true); else waiting.Add(t);
                }
            }
        }

        static Canvas uiCanvas;
        /// <summary>Scale factor of the game's UI canvas (UI/Canvas), or -1.</summary>
        static float UiCanvasScale()
        {
            if (uiCanvas == null) { var go = GameObject.Find("UI/Canvas"); uiCanvas = go != null ? go.GetComponent<Canvas>() : null; }
            return uiCanvas != null ? PixelsPerUnit(uiCanvas.rootCanvas) : -1f;
        }

        /// <summary>Rendered pixels per canvas unit: the target (screen or camera render texture) height over the canvas height.
        /// (scaleFactor follows Screen, which differs from the render target in the screenshot tool.)</summary>
        public static float PixelsPerUnit(Canvas root)
        {
            if (root == null || root.renderMode == RenderMode.WorldSpace) return 1f;
            var cam = root.renderMode == RenderMode.ScreenSpaceCamera ? root.worldCamera : null;
            float h = cam != null ? cam.pixelHeight : MobileUi.ScreenH;      // MobileUi: the forced phone size in tools, else Screen
            float ch = ((RectTransform)root.transform).rect.height;
            return ch > 1f && h > 1f ? h / ch : Mathf.Max(0.01f, root.scaleFactor);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lastScale = -1f; uiCanvas = null; pixel = null; pixelBold = null; raster22 = null; resolved = false; mats.Clear(); pending.Clear(); pump = null; }

        /// <summary>The pixel font (TMP default font asset when it is Pixelify Sans), else null (no swapping).</summary>
        public static TMP_FontAsset Pixel
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    var d = TMP_Settings.defaultFontAsset;
                    pixel = d != null && d.name.StartsWith(NewFontPrefix) ? d : null;
                    pixelBold = pixel != null ? Resources.Load<TMP_FontAsset>(BoldResource) : null;
                    raster22 = pixel != null ? Resources.Load<TMP_FontAsset>(RasterResource) : null;
                }
                return pixel;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Pixel == null) return;
            if (pump == null)
            {
                var go = new GameObject("UiFontPump") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                pump = go.AddComponent<Pump>();
            }
            if (!hooked)
            {
                hooked = true;
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
                SceneManager.sceneLoaded += (s, m) => SwapAll();
            }
            SwapAll();
        }

        /// <summary>Switch every text in the loaded scenes (active or not) to the pixel font.</summary>
        public static int SwapAll()
        {
            if (Pixel == null) return 0;
            int n = 0;
            // screen-space canvases snap their vertices to whole screen pixels, so the point-filtered raster glyphs map 1:1
            // (otherwise half-pixel offsets drop or double the 1-pixel slits that tell 5 from 8 and C from O)
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && !c.pixelPerfect) c.pixelPerfect = true;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Apply(t)) { n++; pending.Add(t); }     // rebuild the mesh after the frame (a font switched outside a rebuild is not always redrawn)
            }
            return n;
        }

        static void OnTextChanged(Object o)
        {
            if (o is TMP_Text t && Apply(t)) pending.Add(t);
        }

        /// <summary>Font pixels per em of Pixelify Sans (one font pixel = 1/11 em, ~91 font units).</summary>
        public const float GridPerEm = 11f;
        /// <summary>The native size of the pixel-exact (raster) cut: 2 UI px per font pixel, like the 2x UI art.</summary>
        public const float PixelSize = 22f;

        /// <summary>
        /// uGUI text sizes land on whole font pixels ON SCREEN (canvas scale included): the authored size times the canvas
        /// scale is rounded to a multiple of 11 screen px, at least 22 (2 px per font pixel, like the 2x UI art). Multiples of
        /// 22 px draws the raster cut 1:1 (crisp blocks); larger sizes (33, 44 = 3, 4 px per font pixel) use the SDF
        /// cut. Desktop 1080p (scale 1): 15-27 -> 22, 28-38 -> 33. A phone (scale ~2): 17 -> 33 px, so phone text keeps its
        /// size. Fractional sizes (17 px = 1.5 px per font pixel) blur the 1-pixel slits shut: C read as O, 5 as 8.
        /// </summary>
        public static float SnappedScreenPx(float size, float scale)
        {
            if (size <= 0f) return size;
            return Mathf.Max(PixelSize, Mathf.Round(size * Mathf.Max(0.01f, scale) / GridPerEm) * GridPerEm);
        }

        /// <summary>Canvas-unit size for <paramref name="size"/> at canvas scale 1 (see <see cref="SnappedScreenPx"/>).</summary>
        public static float SnappedSize(float size) => SnappedScreenPx(size, 1f);

        // only 1:1: the bitmap cut drawn at 2x on a phone canvas (scale ~2.07) sampled the wrong texels
        static bool IsRasterPx(float px) => Mathf.Abs(px - PixelSize) < 0.01f;

        static float lastScale = -1f;

        /// <summary>
        /// Put one text on the right Pixelify cut: uGUI text without a custom material uses the raster cut at a snapped
        /// size; world text and styled (outlined) text use the SDF cut and keep their material styling (cloned onto the
        /// Pixelify atlas). Bold texts get the real bold cut and drop the Bold flag (TMP's synthetic bold dilates glyphs and
        /// fills the 1-pixel counters). True = changed.
        /// </summary>
        public static bool Apply(TMP_Text t)
        {
            var p = Pixel;
            if (p == null || t == null) return false;
            var from = t.font;
            if (from == null) return false;
            bool fromOld = from.name.StartsWith(OldFontPrefix), fromOurs = from.name.StartsWith(NewFontPrefix);
            if (!fromOld && !fromOurs) return false;
            bool changed = false;
            bool bold = (t.fontStyle & FontStyles.Bold) != 0 || from == pixelBold;
            var old = t.fontSharedMaterial;
            bool styled = old != null && old != from.material;
            bool ui = t is TextMeshProUGUI;
            bool rasterPx = false;
            // auto-sized texts (phone area pill, menu buttons) pick in-between sizes: they keep auto-size on the SDF cut
            if (ui && !t.enableAutoSizing)
            {
                float cur = t.fontSize;
                // the authored size travels with the object (clones of a snapped template keep the original size)
                if (!t.TryGetComponent<PixelTextSize>(out var rec)) { rec = t.gameObject.AddComponent<PixelTextSize>(); rec.hideFlags = HideFlags.HideInInspector; rec.authored = cur; rec.set = -1f; }
                if (!Mathf.Approximately(rec.set, cur)) rec.authored = cur;          // code set a new size since we snapped it
                var cv = t.canvas != null ? t.canvas.rootCanvas : null;
                float scale = PixelsPerUnit(cv);
                float px = SnappedScreenPx(rec.authored, scale);
                float target = px / scale;
                // phones: canvas scale ~2 and not a whole number, so text is never 1:1 with the raster cut: SDF at 3-4 px per
                // font pixel (still on whole font pixels, slits stay open)
                rasterPx = IsRasterPx(px) && !MobileUi.IsMobile;
                // a parent scale (quest panel 0.85 on phones, the hand pill throb) would resample the point-filtered raster cut: SDF there
                if (cv != null) { float rel = t.transform.lossyScale.y / Mathf.Max(1e-5f, cv.transform.lossyScale.y); if (Mathf.Abs(rel - 1f) > 0.01f) rasterPx = false; }
                if (!Mathf.Approximately(cur, target)) { t.fontSize = target; changed = true; }
                rec.set = t.fontSize;
            }
            bool raster = ui && !styled && raster22 != null && rasterPx;
            // the bold cut of this variable font is off the pixel grid (counters of e, a, G close at 22 px): screen UI uses
            // the regular raster cut for bold too (hierarchy comes from size and colour); SDF text keeps the bold cut
            var to = raster ? raster22 : (bold && pixelBold != null ? pixelBold : p);
            if ((t.fontStyle & FontStyles.Bold) != 0 && (raster || to == pixelBold)) { t.fontStyle &= ~FontStyles.Bold; changed = true; }
            if (to == from) return changed;
            t.font = to;
            t.fontSharedMaterial = raster ? to.material : MapMaterial(old, from, to);
            return true;
        }

        static Material MapMaterial(Material old, TMP_FontAsset from, TMP_FontAsset to)
        {
            if (old == null || old == from.material) return to.material;
            if (mats.TryGetValue((old, to), out var m) && m != null) return m;
            ShaderUtilities.GetShaderPropertyIDs();
            m = new Material(old) { name = old.name.Replace(OldFontPrefix, NewFontPrefix) };
            m.SetTexture(ShaderUtilities.ID_MainTex, to.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, to.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, to.atlasHeight);
            m.SetFloat(ShaderUtilities.ID_GradientScale, to.atlasPadding + 1);
            m.SetFloat(ShaderUtilities.ID_WeightNormal, to.normalStyle);
            m.SetFloat(ShaderUtilities.ID_WeightBold, to.boldStyle);
            mats[(old, to)] = m;
            return m;
        }
    }
}

