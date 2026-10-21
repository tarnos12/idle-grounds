using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One pixel font for every text (owner pick vp_look = font, 2026-10-21: "all UI needs to be consistent").
    /// The TMP default font is Pixelify Sans (menu Idle Grounds/UI/Build Pixel Font). Texts serialized with the old
    /// LiberationSans asset (scene, prefabs, pooled world text) are switched at runtime: once per loaded scene and
    /// for every text that rebuilds later (TMP's text-changed event), so nothing in the scene has to be rewritten.
    /// Materials with extra styling (the outlined Floater, per-text outline instances) are cloned onto the Pixelify
    /// atlas so outlines and halos stay. Glyphs Pixelify lacks fall back to LiberationSans (font asset fallback).
    /// </summary>
    public static class UiFont
    {
        const string OldFontPrefix = "LiberationSans", NewFontPrefix = "PixelifySans";

        static TMP_FontAsset pixel;
        static bool resolved, hooked;
        static readonly Dictionary<Material, Material> mats = new Dictionary<Material, Material>();
        /// <summary>Texts switched inside TMP's own rebuild: their mesh is regenerated once more after the frame's updates.</summary>
        static readonly List<TMP_Text> pending = new List<TMP_Text>();
        static Pump pump;

        /// <summary>Rebuilds the meshes of texts switched during a TMP rebuild (a font change there is not always picked up).</summary>
        sealed class Pump : MonoBehaviour
        {
            readonly List<TMP_Text> work = new List<TMP_Text>();
            void LateUpdate()
            {
                if (pending.Count == 0) return;
                work.Clear(); work.AddRange(pending); pending.Clear();
                foreach (var t in work) if (t != null && t.isActiveAndEnabled) t.ForceMeshUpdate(true, true);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pixel = null; resolved = false; mats.Clear(); pending.Clear(); pump = null; }

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
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Swap(t)) n++;
            return n;
        }

        static void OnTextChanged(Object o)
        {
            if (o is TMP_Text t && Swap(t)) pending.Add(t);
        }

        /// <summary>Switch one text from LiberationSans to the pixel font (keeps its material styling). True = changed.</summary>
        public static bool Swap(TMP_Text t)
        {
            var p = Pixel;
            if (p == null || t == null) return false;
            var from = t.font;
            if (from == null || from == p || !from.name.StartsWith(OldFontPrefix)) return false;
            var old = t.fontSharedMaterial;
            t.font = p;
            t.fontSharedMaterial = MapMaterial(old, from, p);
            return true;
        }

        static Material MapMaterial(Material old, TMP_FontAsset from, TMP_FontAsset to)
        {
            if (old == null || old == from.material) return to.material;
            if (mats.TryGetValue(old, out var m) && m != null) return m;
            ShaderUtilities.GetShaderPropertyIDs();
            m = new Material(old) { name = old.name.Replace(OldFontPrefix, NewFontPrefix) };
            m.SetTexture(ShaderUtilities.ID_MainTex, to.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, to.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, to.atlasHeight);
            m.SetFloat(ShaderUtilities.ID_GradientScale, to.atlasPadding + 1);
            m.SetFloat(ShaderUtilities.ID_WeightNormal, to.normalStyle);
            m.SetFloat(ShaderUtilities.ID_WeightBold, to.boldStyle);
            mats[old] = m;
            return m;
        }
    }
}
