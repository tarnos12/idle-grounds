using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// "Idle Grounds/UI/Build Pixel Font (Pixelify Sans)" (owner pick vp_look = font, 2026-10-21: one pixel font for all
    /// UI text). Builds two STATIC TMP font assets (regular and bold, sampled on the font's pixel grid) from the OFL TTFs in
    /// <see cref="Dir"/>, a Floater material twin
    /// (outlined world text), sets the regular as the TMP default font and keeps LiberationSans SDF as the fallback for
    /// glyphs Pixelify Sans lacks. Static atlases never change in Play, so they never dirty the repo; only the existing
    /// dynamic "LiberationSans SDF - Fallback" still collects rare glyphs (do not commit that churn).
    /// Existing texts keep their serialized LiberationSans reference; <see cref="IdleGrounds.Game.UiFont"/> swaps them at
    /// runtime, so the scene and prefabs do not need a rewrite.
    /// </summary>
    public static class PixelFontBuilder
    {
        public const string Dir = "Assets/_Project/Art/Fonts/PixelifySans";
        const string RegularTtf = Dir + "/PixelifySans-Regular.ttf", BoldTtf = Dir + "/PixelifySans-Bold.ttf";
        public const string RasterAsset = Dir + "/Resources/" + IdleGrounds.Game.UiFont.RasterResource + ".asset";
        public const string RegularAsset = Dir + "/PixelifySans SDF.asset", BoldAsset = Dir + "/Resources/" + IdleGrounds.Game.UiFont.BoldResource + ".asset";
        public const string FloaterMat = Dir + "/PixelifySans SDF - Floater.mat";
        const string LiberationAsset = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string LiberationFloater = "Assets/_Project/Art/Fonts/LiberationSans SDF - Floater.mat";
        const string SettingsAsset = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>
        /// Pixelify Sans is drawn on a grid of ~91 font units (1000 / 11): one font pixel = 1/11 em. Sampling at 44 pt puts
        /// every font pixel on exactly 4 x 4 atlas texels, so the SDF keeps square corners and the 1-pixel counters (c, e,
        /// B, 8) stay open; 6 texels of padding is 1.5 font pixels of spread. (The first build sampled at 48 pt = 4.36
        /// texels per font pixel and, with the synthetic bold, closed the counters.)
        /// </summary>
        public const int GridPerEm = 11, SamplingPt = 44, Padding = 6, AtlasSize = 1024;

        /// <summary>ASCII, Latin-1 and the symbols the UI strings use (missing ones fall back to LiberationSans).</summary>
        static string Charset()
        {
            var sb = new System.Text.StringBuilder();
            for (int c = 32; c < 127; c++) sb.Append((char)c);
            for (int c = 160; c < 256; c++) sb.Append((char)c);
            sb.Append("–—‘’‚“”„…•·→←↑↓×÷±≥≤✓✗★☆♥♦€§¶°′″‹›«»ąćęłńóśźżĄĆĘŁŃÓŚŹŻ");
            return sb.ToString();
        }

        [MenuItem("Idle Grounds/UI/Build Pixel Font (Pixelify Sans)")]
        public static void Build()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationAsset);
            var reg = Make(RegularTtf, RegularAsset, "PixelifySans SDF", liberation);
            if (reg == null) { Debug.LogError("[PixelFont] build failed"); return; }
            // the real bold cut as its own asset: UiFont gives it to Bold texts (TMP's synthetic bold dilates the glyphs
            // and fills the 1-pixel counters of a pixel font; a weight-table link would draw bold in a sub-mesh)
            var bold = Make(BoldTtf, BoldAsset, "PixelifySans Bold SDF", liberation);
            if (bold == null) { Debug.LogError("[PixelFont] bold build failed"); return; }
            // pixel-exact cuts for screen UI: hinted raster at 22 px (2 px per font pixel), point-filtered, drawn 1:1
            var r22 = Make(RegularTtf, RasterAsset, IdleGrounds.Game.UiFont.RasterResource, reg, raster: true);
            if (r22 == null) { Debug.LogError("[PixelFont] raster build failed"); return; }

            // outlined world-text twin of the LiberationSans Floater material
            var lf = AssetDatabase.LoadAssetAtPath<Material>(LiberationFloater);
            if (lf != null)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(FloaterMat);
                bool isNew = m == null;
                if (isNew) m = new Material(lf);
                else m.CopyPropertiesFromMaterial(lf);
                m.name = "PixelifySans SDF - Floater";
                Retarget(m, reg);
                if (isNew) AssetDatabase.CreateAsset(m, FloaterMat);
                EditorUtility.SetDirty(m);
            }

            // TMP default font = Pixelify Sans (texts created at runtime without a font get it)
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsAsset);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultFontAsset");
                if (p != null) p.objectReferenceValue = reg;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PixelFont] RESULT OK: " + RegularAsset + " (" + reg.characterTable.Count + " chars) + " + BoldAsset + " (" + bold.characterTable.Count + " chars); TMP default font set");
        }

        /// <summary>Point an SDF material at <paramref name="font"/>'s atlas (texture, size, gradient scale).</summary>
        static void Retarget(Material m, TMP_FontAsset font)
        {
            ShaderUtilities.GetShaderPropertyIDs();
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
            m.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
            m.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            m.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
        }

        static TMP_FontAsset Make(string ttfPath, string assetPath, string name, TMP_FontAsset fallback, bool raster = false)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null) { Debug.LogError("[PixelFont] missing " + ttfPath); return null; }
            var fa = raster
                ? TMP_FontAsset.CreateFontAsset(ttf, (int)IdleGrounds.Game.UiFont.PixelSize, 2, GlyphRenderMode.RASTER_HINTED, 512, 512, AtlasPopulationMode.Dynamic, true)
                : TMP_FontAsset.CreateFontAsset(ttf, SamplingPt, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogError("[PixelFont] CreateFontAsset failed for " + ttfPath); return null; }
            fa.name = name;
            fa.TryAddCharacters(Charset(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[PixelFont] " + name + " lacks " + missing.Length + " char(s) (LiberationSans fallback): " + missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;       // never grows in Play: no repo churn
            if (raster)
            {
                if (fa.atlasTexture != null) fa.atlasTexture.filterMode = FilterMode.Point;
                var bmp = Shader.Find("TextMeshPro/Bitmap");
                if (fa.material != null && bmp != null && fa.material.shader != bmp) fa.material.shader = bmp;
                if (fa.material != null) fa.material.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            }
            fa.boldStyle = 0.2f;        // inline <b> on the regular cut: a light dilation only (0.75 fills 1-pixel counters)
            fa.fallbackFontAssetTable = fallback != null ? new List<TMP_FontAsset> { fallback } : new List<TMP_FontAsset>();

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            var tex = fa.atlasTextures != null && fa.atlasTextures.Length > 0 ? fa.atlasTextures[0] : fa.atlasTexture;
            if (tex != null) { tex.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            if (fa.material != null) { fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }
    }
}
