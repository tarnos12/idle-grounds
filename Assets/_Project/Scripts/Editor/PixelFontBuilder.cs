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
    /// UI text). Builds a STATIC TMP font asset from the OFL TTF in <see cref="Dir"/> (bold is TMP's synthetic bold: a
    /// weight-table typeface would draw bold glyphs in a sub-mesh, which lost its sorting layer on world text), a Floater material twin
    /// (outlined world text), sets the regular as the TMP default font and keeps LiberationSans SDF as the fallback for
    /// glyphs Pixelify Sans lacks. Static atlases never change in Play, so they never dirty the repo; only the existing
    /// dynamic "LiberationSans SDF - Fallback" still collects rare glyphs (do not commit that churn).
    /// Existing texts keep their serialized LiberationSans reference; <see cref="IdleGrounds.Game.UiFont"/> swaps them at
    /// runtime, so the scene and prefabs do not need a rewrite.
    /// </summary>
    public static class PixelFontBuilder
    {
        public const string Dir = "Assets/_Project/Art/Fonts/PixelifySans";
        const string RegularTtf = Dir + "/PixelifySans-Regular.ttf";
        public const string RegularAsset = Dir + "/PixelifySans SDF.asset";
        const string OldBoldAsset = Dir + "/PixelifySans Bold SDF.asset";
        public const string FloaterMat = Dir + "/PixelifySans SDF - Floater.mat";
        const string LiberationAsset = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string LiberationFloater = "Assets/_Project/Art/Fonts/LiberationSans SDF - Floater.mat";
        const string SettingsAsset = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>Sampling size / padding: 48 pt with 6 px padding gives clean SDF edges from 12 to 40 px on screen.</summary>
        const int SamplingPt = 48, Padding = 6, AtlasSize = 1024;

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
            if (File.Exists(OldBoldAsset)) AssetDatabase.DeleteAsset(OldBoldAsset);     // first build made a bold typeface asset

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
            Debug.Log("[PixelFont] RESULT OK: " + RegularAsset + " (" + reg.characterTable.Count + " chars); TMP default font set");
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

        static TMP_FontAsset Make(string ttfPath, string assetPath, string name, TMP_FontAsset fallback)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null) { Debug.LogError("[PixelFont] missing " + ttfPath); return null; }
            var fa = TMP_FontAsset.CreateFontAsset(ttf, SamplingPt, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { Debug.LogError("[PixelFont] CreateFontAsset failed for " + ttfPath); return null; }
            fa.name = name;
            fa.TryAddCharacters(Charset(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[PixelFont] " + name + " lacks " + missing.Length + " char(s) (LiberationSans fallback): " + missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;       // never grows in Play: no repo churn
            fa.fallbackFontAssetTable = fallback != null ? new List<TMP_FontAsset> { fallback } : new List<TMP_FontAsset>();

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
