using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The text fonts of every TMP text (owner picks vp_look = font, 2026-10-21, and font_read = "setting", 2026-10-22:
    /// Menu > Settings "Text font", default Pixelify Sans titles + Jersey 10 for text and numbers). The TMP default font is
    /// Pixelify Sans (menu Idle Grounds/UI/Build Pixel Font); the other faces are built by Idle Grounds/UI/Build Text Font
    /// Choices into Resources. Texts serialized with LiberationSans or any of our faces (scene, prefabs, pooled world text)
    /// are switched at runtime: once per loaded scene, for every text that rebuilds later (TMP's text-changed event) and all
    /// at once when the player picks another font (<see cref="SetChoice"/>, live, no restart), so nothing in the scene has
    /// to be rewritten.
    /// <para>Every text has a role (<see cref="RoleOf"/>): Title (panel titles and headings) or Body (everything else and
    /// every number); the setting gives each role a <see cref="FontFace"/>. Pixel faces tell letters apart by 1-pixel slits,
    /// so they are only legible on whole font pixels: screen UI uses a hinted raster cut at the face's body size (point
    /// filtered, drawn 1:1 on pixel-perfect canvases) and snaps every uGUI size to the face's grid
    /// (<see cref="FontFace.SnapScreenPx"/>). World text and outlined text use the SDF cut and keep their material styling.
    /// Glyphs a face lacks (arrows, ticks) fall back to LiberationSans.</para>
    /// </summary>
    public static class UiFont
    {
        /// <summary>PlayerPrefs key of the "Text font" setting (a player preference: not in the game save).</summary>
        public const string PrefKey = "ig_text_font";

        /// <summary>Resources names of Pixelify's bold and raster cuts (Art/Fonts/PixelifySans/Resources).</summary>
        public const string BoldResource = "PixelifySans Bold SDF", RasterResource = "PixelifySans Pixel22";

        sealed class Cuts { public TMP_FontAsset sdf, raster, bold; }

        static TMP_FontAsset pixel;
        static bool resolved, hooked;
        static readonly Dictionary<string, Cuts> cuts = new Dictionary<string, Cuts>();
        static TextFontOption choice;
        static readonly Dictionary<(Material, TMP_FontAsset), Material> mats = new Dictionary<(Material, TMP_FontAsset), Material>();
        /// <summary>Texts switched inside TMP's own rebuild: their mesh is regenerated once more after the frame's updates.</summary>
        static readonly List<TMP_Text> pending = new List<TMP_Text>();
        static Pump pump;

        /// <summary>Bumped when the font setting changes (layouts that measured text refit when it moves).</summary>
        public static int Version { get; private set; }
        /// <summary>Raised after the font setting changed and every text was switched.</summary>
        public static event Action Changed;

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
                    if (t.isActiveAndEnabled) { t.ForceMeshUpdate(true, true); if (t is TextMeshProUGUI) UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(t.rectTransform); }     // fitters follow the new size
                    else if (!waiting.Contains(t)) waiting.Add(t);
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
        static void ResetStatics() { lastScale = -1f; uiCanvas = null; pixel = null; resolved = false; cuts.Clear(); choice = null; mats.Clear(); pending.Clear(); pump = null; widthFactor = -1f; }

        /// <summary>The pixel font (TMP default font asset when it is Pixelify Sans), else null (no swapping at all).</summary>
        public static TMP_FontAsset Pixel
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    var d = TMP_Settings.defaultFontAsset;
                    pixel = d != null && d.name.StartsWith("PixelifySans") ? d : null;
                }
                return pixel;
            }
        }

        static Cuts CutsOf(FontFace f)
        {
            if (f == null) return null;
            if (cuts.TryGetValue(f.id, out var c)) return c;
            c = new Cuts
            {
                sdf = f.sdfResource == null ? Pixel : Resources.Load<TMP_FontAsset>(f.sdfResource),
                raster = f.rasterResource == null ? null : Resources.Load<TMP_FontAsset>(f.rasterResource),
                bold = f.boldResource == null ? null : Resources.Load<TMP_FontAsset>(f.boldResource),
            };
            if (c.sdf == null) c = null;            // not built: callers use Pixelify
            cuts[f.id] = c;
            return c;
        }

        /// <summary>True when the face's assets exist (Resources).</summary>
        public static bool Available(FontFace f) => Pixel != null && CutsOf(f) != null;

        /// <summary>The face's main (SDF) asset, or Pixelify when it is not built.</summary>
        public static TMP_FontAsset SdfOf(FontFace f) { var c = CutsOf(f); return c != null ? c.sdf : Pixel; }

        // ================================================================ the setting

        /// <summary>The "Text font" option in use (PlayerPrefs <see cref="PrefKey"/>, default Pixelify titles + Jersey 10).</summary>
        public static TextFontOption Choice
        {
            get
            {
                if (choice == null)
                {
                    choice = TextFonts.Find(PlayerPrefs.GetString(PrefKey, TextFonts.DefaultId)) ?? TextFonts.Find(TextFonts.DefaultId);
                    widthFactor = -1f;
                }
                return choice;
            }
        }

        /// <summary>Read the setting from PlayerPrefs again (tests; a pref changed outside the game).</summary>
        public static void ReloadChoice() { choice = null; widthFactor = -1f; }

        /// <summary>Pick a "Text font" option: saved in PlayerPrefs (this device, not the game save) and applied to every text now.</summary>
        public static void SetChoice(string id)
        {
            var o = TextFonts.Find(id);
            if (o == null) return;
            PlayerPrefs.SetString(PrefKey, o.id);
            PlayerPrefs.Save();
            if (o == choice) return;
            choice = o;
            widthFactor = -1f;
            Version++;
            if (Application.isPlaying) SwapAll();
            Changed?.Invoke();
        }

        /// <summary>The face a role is drawn in under the current setting (Pixelify when that face is not built).</summary>
        public static FontFace FaceFor(TextRole role)
        {
            var o = Choice;
            var f = role == TextRole.Title ? o.title : o.body;
            return Available(f) ? f : TextFonts.Pixelify;
        }

        static float widthFactor = -1f;
        /// <summary>
        /// Width of body text in the chosen face relative to Pixelify Sans at 22 px (same text, both at their body sizes):
        /// 1 for Pixelify, more for wider faces (Press Start 2P). Layouts that grow with the text (<see cref="UiFit.TextGrowth"/>) use it.
        /// </summary>
        public static float BodyWidthFactor
        {
            get
            {
                if (widthFactor > 0f) return widthFactor;
                var f = FaceFor(TextRole.Body);
                float a = AdvanceOf(Pixel, TextFonts.Sample, FontFace.BasePx), b = AdvanceOf(SdfOf(f), TextFonts.Sample, f.bodyPx);
                widthFactor = a > 0f && b > 0f ? b / a : 1f;
                return widthFactor;
            }
        }

        /// <summary>Sum of the glyph advances of <paramref name="s"/> at <paramref name="px"/> (0 when unknown).</summary>
        public static float AdvanceOf(TMP_FontAsset fa, string s, float px)
        {
            if (fa == null || fa.faceInfo.pointSize <= 0) return 0f;
            float sum = 0f;
            var table = fa.characterLookupTable;
            if (table == null) return 0f;
            foreach (char ch in s) if (table.TryGetValue(ch, out var c) && c.glyph != null) sum += c.glyph.metrics.horizontalAdvance;
            return sum * px / fa.faceInfo.pointSize;
        }

        // ================================================================ switching

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

        /// <summary>Switch every text in the loaded scenes (active or not) to the chosen fonts and sizes.</summary>
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
                if (Apply(t)) { n++; if (!pending.Contains(t)) pending.Add(t); }     // rebuild the mesh after the frame (a font switched outside a rebuild is not always redrawn)
            }
            return n;
        }

        static void OnTextChanged(Object o)
        {
            if (o is TMP_Text t && Apply(t) && !pending.Contains(t)) pending.Add(t);
        }

        /// <summary>Pixelify font pixels per em (one font pixel = 1/11 em, ~91 font units).</summary>
        public const float GridPerEm = 11f;
        /// <summary>Pixelify's raster size (2 UI px per font pixel, like the 2x UI art) and the authored body size of the UI.</summary>
        public const float PixelSize = 22f;

        /// <summary>Screen size of an authored uGUI body text at canvas scale <paramref name="scale"/> in the chosen body face.</summary>
        public static float SnappedScreenPx(float size, float scale) => FaceFor(TextRole.Body).SnapScreenPx(size, scale);

        /// <summary>Screen size for <paramref name="size"/> at scale <paramref name="scale"/> in <paramref name="text"/>'s face (its role).</summary>
        public static float SnappedScreenPx(TMP_Text text, float size, float scale) => FaceOf(text).SnapScreenPx(size, scale);

        /// <summary>Canvas-unit size for <paramref name="size"/> at canvas scale 1 (see <see cref="FontFace.SnapScreenPx"/>).</summary>
        public static float SnappedSize(float size) => SnappedScreenPx(size, 1f);

        static float lastScale = -1f;

        /// <summary>
        /// Title or Body. A <see cref="UiTextRole"/> decides; else world text is Body, and a uGUI text is a Title when its
        /// object is named "...Title..." at an authored size of 21+ (panel titles, Help section heads) or when it was authored
        /// at 24+ (tooltip and recipe headings, the big tree "?"). Everything else, and every small label, is Body.
        /// </summary>
        public static TextRole RoleOf(TMP_Text t)
        {
            if (t == null) return TextRole.Body;
            if (t.TryGetComponent<UiTextRole>(out var r) && r.role != TextRole.Auto) return r.role;
            if (!(t is TextMeshProUGUI)) return TextRole.Body;
            if (t.enableAutoSizing) return TextRole.Body;
            float authored = t.TryGetComponent<PixelTextSize>(out var rec) && rec.recorded ? rec.authored : t.fontSize;
            if (authored >= 24f) return TextRole.Title;
            return authored >= 21f && t.gameObject.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ? TextRole.Title : TextRole.Body;
        }

        /// <summary>The face <paramref name="t"/> is drawn in: its pinned face (previews), else its role's face.</summary>
        public static FontFace FaceOf(TMP_Text t)
        {
            if (t != null && t.TryGetComponent<UiTextRole>(out var r) && !string.IsNullOrEmpty(r.face))
            {
                var f = TextFonts.FindFace(r.face);
                if (f != null && Available(f)) return f;
            }
            return FaceFor(RoleOf(t));
        }

        static bool IsBoldCut(TMP_FontAsset fa) => fa != null && fa.name.Contains("Bold");

        /// <summary>
        /// Put one text on the right face and cut: uGUI text without a custom material uses the raster cut at a snapped size;
        /// world text and styled (outlined) text use the SDF cut and keep their material styling (cloned onto the face's
        /// atlas). Bold texts get the face's real bold cut where it has one, and never TMP's synthetic bold on a pixel face
        /// (it dilates the glyphs and fills the 1-pixel counters). True = changed.
        /// </summary>
        public static bool Apply(TMP_Text t)
        {
            var p = Pixel;
            if (p == null || t == null) return false;
            var from = t.font;
            if (from == null || !TextFonts.IsOwn(from.name)) return false;
            var cur = t.fontSharedMaterial;

            if (!t.TryGetComponent<PixelTextSize>(out var rec)) { rec = t.gameObject.AddComponent<PixelTextSize>(); rec.hideFlags = HideFlags.HideInInspector; }
            if (!rec.recorded)
            {
                rec.recorded = true;
                rec.authored = t.fontSize; rec.set = -1f;
                rec.bold = (t.fontStyle & FontStyles.Bold) != 0 || IsBoldCut(from);
                rec.baseMat = cur != null && cur != from.material ? cur : null;
                rec.setMat = cur;
            }
            else
            {
                if ((t.fontStyle & FontStyles.Bold) != 0) rec.bold = true;                                // code asked for bold since
                if (cur != rec.setMat) rec.baseMat = cur != null && cur != from.material ? cur : null;    // code set a material since
            }

            bool changed = false;
            bool styled = rec.baseMat != null;
            bool ui = t is TextMeshProUGUI;
            var face = FaceOf(t);
            var cs = CutsOf(face) ?? CutsOf(TextFonts.Pixelify);
            if (cs == null) return false;
            bool rasterPx = false;
            if (t.enableAutoSizing)
            {
                // auto-sized texts stay on the SDF cut; their range follows the face (same look as Pixelify at 22 = the face's body size)
                if (!Mathf.Approximately(rec.autoSetMax, t.fontSizeMax)) { rec.autoMin = t.fontSizeMin; rec.autoMax = t.fontSizeMax; }
                float k = face.bodyPx / FontFace.BasePx, mn = rec.autoMin * k, mx = rec.autoMax * k;
                if (!Mathf.Approximately(t.fontSizeMin, mn) || !Mathf.Approximately(t.fontSizeMax, mx)) { t.fontSizeMin = mn; t.fontSizeMax = mx; changed = true; }
                rec.autoSetMax = t.fontSizeMax;
            }
            else
            {
                float now = t.fontSize;
                if (!Mathf.Approximately(rec.set, now)) rec.authored = now;          // code set a new size since we snapped it
                float target;
                if (ui)
                {
                    var cv = t.canvas != null ? t.canvas.rootCanvas : null;
                    float scale = PixelsPerUnit(cv);
                    float px = face.SnapScreenPx(rec.authored, scale);
                    target = px / scale;
                    // phones: canvas scale ~2 and not a whole number, so text is never 1:1 with the raster cut: SDF at 3-4 px per
                    // font pixel (still on whole font pixels, slits stay open)
                    rasterPx = face.IsRasterPx(px) && !MobileUi.IsMobile;
                    // a parent scale (quest panel 0.85 on phones, the hand pill throb) would resample the point-filtered raster cut: SDF there
                    if (cv != null) { float rel = t.transform.lossyScale.y / Mathf.Max(1e-5f, cv.transform.lossyScale.y); if (Mathf.Abs(rel - 1f) > 0.01f) rasterPx = false; }
                }
                else target = face.WorldSize(rec.authored, ViewKit.FontPerPx);
                if (!Mathf.Approximately(now, target)) { t.fontSize = target; changed = true; }
                rec.set = t.fontSize;
            }
            bool raster = ui && !styled && cs.raster != null && rasterPx;
            var to = raster ? cs.raster : (rec.bold && cs.bold != null ? cs.bold : cs.sdf);
            // no synthetic bold on pixel faces: hierarchy comes from size, colour and the title face; real bold cuts carry it
            if ((t.fontStyle & FontStyles.Bold) != 0 && (raster || to == cs.bold || face.pixel)) { t.fontStyle &= ~FontStyles.Bold; changed = true; }
            var mat = raster || !styled ? to.material : MapMaterial(rec.baseMat, to);
            if (to == from && cur == mat) { rec.setMat = cur; return changed; }
            t.font = to;
            t.fontSharedMaterial = mat;
            rec.setMat = mat;
            return true;
        }

        static Material MapMaterial(Material old, TMP_FontAsset to)
        {
            if (old == null) return to.material;
            if (mats.TryGetValue((old, to), out var m) && m != null) return m;
            ShaderUtilities.GetShaderPropertyIDs();
            m = new Material(old) { name = old.name + " @" + to.name };
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
