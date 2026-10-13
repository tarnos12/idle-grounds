using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Interim veil art built in code from the delivered mist (VP-11; the art redo <c>island_veil_mist_256x256_4f</c> v2 is
    /// batch C). The delivered frames do not wrap, so every 256 texels the fog showed a brightness seam. Here:
    /// <list type="bullet">
    /// <item><b>Seamless mist tiles</b> in two non-commensurate periods (256 and 192 texels, from different frames): each is
    ///   a four-way mix of the frame and its half-period shifted copies, picked per pixel by a smooth noise threshold, with
    ///   each copy's own seam always at weight 0 (pixel copies only: no new colours, no filtering).</item>
    /// <item><b>Base</b>: a plain opaque texel so the Island's ground never shows through the fog's holes.</item>
    /// <item><b>Halo</b>: per Island, the fog colour fading 2-3 cells into the sky outside the veil mask in 3 dithered alpha
    ///   steps, so the veil no longer keeps the Island's outline as a hard edge.</item>
    /// </list>
    /// </summary>
    public static class VeilArt
    {
        public const string Dir = IslandArtBuilder.IslandDir + "/Veil";
        public const string MistA = Dir + "/veil_mist_a_256.png", MistB = Dir + "/veil_mist_b_192.png", Base = Dir + "/veil_base_4.png";
        const int Ppu = 32;
        static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }

        static float VNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float H(int a, int b) => (Hash(a, b, seed) & 0xFFFF) / 65535f;
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        /// <summary>The three veil sprites (mist A 256, mist B 192, opaque base), built from <paramref name="srcPath"/> when missing.</summary>
        public static bool Ensure(string srcPath, out Sprite a, out Sprite b, out Sprite baseSprite)
        {
            a = b = baseSprite = null;
            Directory.CreateDirectory(Dir);
            var src = CoastCurves.ReadPng(srcPath, out int sw, out int sh);
            if (src == null || sh < 192) return false;
            int frames = Mathf.Max(1, sw / sh), fs = sh;
            Color32[] Frame(int f, int ox, int oy, int size)
            {
                f = Mathf.Clamp(f, 0, frames - 1);
                var o = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) o[y * size + x] = src[((oy + y) % fs) * sw + f * fs + (ox + x) % fs];
                return o;
            }
            Write(MistA, Seamless(Frame(0, 0, 0, fs), fs, 4101), fs);
            Write(MistB, Seamless(Frame(Mathf.Min(2, frames - 1), 37, 53, 192), 192, 4202), 192);
            var bp = new Color32[4 * 4];
            for (int i = 0; i < bp.Length; i++) bp[i] = new Color32(255, 255, 255, 255);
            Write(Base, bp, 4);
            a = AssetDatabase.LoadAssetAtPath<Sprite>(MistA);
            b = AssetDatabase.LoadAssetAtPath<Sprite>(MistB);
            baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Base);
            return a != null && b != null && baseSprite != null;
        }

        /// <summary>Four-way half-period mix (see the class summary): wraps on both axes by construction.</summary>
        static Color32[] Seamless(Color32[] f, int s, int seed)
        {
            var o = new Color32[s * s];
            int h = s / 2;
            float m = s / 8f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // weight of the unshifted copy along each axis: 0 on the tile edge (its seam), 1 from s/8 inward (and on
                    // the centre lines, where the shifted copies have their seams)
                    float wx = Mathf.Clamp01(Mathf.Min(x, s - 1 - x) / m), wy = Mathf.Clamp01(Mathf.Min(y, s - 1 - y) / m);
                    float tx = VNoise(x / 7f, y / 7f, seed), ty = VNoise(x / 7f + 31.7f, y / 7f + 11.3f, seed + 1);
                    int sx = wx >= 1f || (wx > 0f && tx < wx) ? x : (x + h) % s;
                    int sy = wy >= 1f || (wy > 0f && ty < wy) ? y : (y + h) % s;
                    o[y * s + x] = f[sy * s + sx];
                }
            return o;
        }

        static void Write(string path, Color32[] px, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            Import(path, true, FilterMode.Point, Ppu, TextureWrapMode.Repeat);
        }

        static void Import(string path, bool center, FilterMode filter, float ppu, TextureWrapMode wrap)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = filter;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = wrap;
            imp.maxTextureSize = 8192;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = false;
            st.spriteAlignment = (int)SpriteAlignment.Center;
            st.spritePivot = new Vector2(0.5f, 0.5f);
            imp.SetTextureSettings(st);
            if (EditorUtility.IsDirty(imp)) imp.SaveAndReimport();
        }

        /// <summary>
        /// The feathered rim outside the veil mask (<paramref name="mask"/>: coverage in [0,1] on a grid of
        /// <paramref name="mppu"/> per cell): fog colour, alpha 0.55 / 0.35 / 0.18 over ~0.8 / 1.6 / 2.6 cells (noise-wobbled,
        /// Bayer-dithered steps), drawn at <paramref name="hppu"/> px per cell. Returns the sprite (centre pivot = mask centre).
        /// </summary>
        public static Sprite Halo(string key, float[,] mask, int mppu, int hppu)
        {
            int mw = mask.GetLength(0), mh = mask.GetLength(1);
            int k = hppu / mppu;
            int w = mw * k, h = mh * k;
            // chamfer distance (in halo px) to the masked region
            var d = new float[w, h];
            const float Inf = 1e9f;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) d[x, y] = mask[x / k, y / k] >= 0.75f ? 0f : Inf;      // starts a little inside the mask edge: no gap line between veil and halo
            const float D1 = 1f, D2 = 1.41421f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = d[x, y];
                    if (x > 0) v = Mathf.Min(v, d[x - 1, y] + D1);
                    if (y > 0) v = Mathf.Min(v, d[x, y - 1] + D1);
                    if (x > 0 && y > 0) v = Mathf.Min(v, d[x - 1, y - 1] + D2);
                    if (x < w - 1 && y > 0) v = Mathf.Min(v, d[x + 1, y - 1] + D2);
                    d[x, y] = v;
                }
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    float v = d[x, y];
                    if (x < w - 1) v = Mathf.Min(v, d[x + 1, y] + D1);
                    if (y < h - 1) v = Mathf.Min(v, d[x, y + 1] + D1);
                    if (x < w - 1 && y < h - 1) v = Mathf.Min(v, d[x + 1, y + 1] + D2);
                    if (x > 0 && y < h - 1) v = Mathf.Min(v, d[x - 1, y + 1] + D2);
                    d[x, y] = v;
                }
            var px = new Color32[w * h];
            var fog = new Color32(196, 194, 214, 255);
            int seed = (int)(Hash(key.Length, key[0], 977) & 0xFFFF);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dc = d[x, y] / hppu;                       // cells
                    if (dc <= 0f || dc > 3.2f) continue;
                    dc += (VNoise(x / (hppu * 1.3f), y / (hppu * 1.3f), seed) - 0.5f) * 1.2f;    // wobble: puffs, not a ring
                    float t = Mathf.Clamp01(1f - dc / 2.6f) * 3f;       // 3 .. 0
                    int lv = Mathf.FloorToInt(t);
                    if (Bayer4[((y & 3) << 2) | (x & 3)] < (t - lv) * 16f) lv++;
                    if (lv <= 0) continue;
                    byte a = lv >= 3 ? (byte)140 : lv == 2 ? (byte)90 : (byte)46;
                    px[y * w + x] = new Color32(fog.r, fog.g, fog.b, a);
                }
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/veil_halo_{key}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Import(path, true, FilterMode.Point, hppu, TextureWrapMode.Clamp);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
