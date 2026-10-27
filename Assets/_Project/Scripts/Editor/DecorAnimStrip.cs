using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Review strips of the decor code motion (ART-SPEC §3.9.8 step D), pixel-exact from the same bake the game runs, no Play
    /// mode: per variant frame the 4 sway steps, the same mirrored, and a diff row (texels that moved against step 0 in
    /// magenta, the rest dimmed), scaled up 6x. Writes <c>Logs/bg/decor/&lt;key&gt;_sway_strip.png</c>:
    /// <list type="bullet">
    /// <item>the 32-px <c>deco_tree</c> with a temporary test entry (crown sway, not shipped: the shipped entry needs the @64 still);</item>
    /// <item>every delivered <c>deco_*@64</c> strip with its shipped code-anim.json entry (once step D art lands).</item>
    /// </list>
    /// Run: <c>bash tools/bg/unity.sh IdleGrounds.Editor.DecorAnimStrip.Write</c>
    /// </summary>
    public static class DecorAnimStrip
    {
        /// <summary>The temporary 32-px test entry (same as the EditMode test CodeAnimTests.Decor32Entry).</summary>
        public const string Test32Entry =
            "{\"entries\":[{\"key\":\"deco_tree\",\"still\":[32,48],\"fps\":2,\"steps\":4,\"play\":\"always\",\"effects\":[" +
            "{\"type\":\"sway\",\"name\":\"crown sway\",\"rect\":[0,0,32,36],\"pivot\":[16,36],\"axis\":\"x\",\"offsets\":[0,1,0,-1]}]}]}";

        const int Scale = 6;

        public static void Write()
        {
            string outDir = Path.GetFullPath("Logs/bg/decor");
            Directory.CreateDirectory(outDir);
            var written = new List<string>();
            var t32 = CodeAnimLibrary.Parse(Test32Entry).entries[0];
            var img = Png("Assets/_Project/Art/Incoming/fixtures/deco_tree_32x48_3f.png");
            if (img != null) written.Add(Strip(img, 32, t32, Path.Combine(outDir, "deco_tree_32_test_sway_strip.png")));
            foreach (var f in Directory.GetFiles("Assets/_Project/Art/Incoming/fixtures", "deco_*@64.png"))
            {
                string bn = Path.GetFileNameWithoutExtension(f);
                string key = Game.Data.ArtKeys.ParseFileName(bn, out _, out int fw, out int fh, out _);
                var e = CodeAnimLibrary.Find(key + "_" + fw + "x" + fh) ?? CodeAnimLibrary.Find(key);
                var im = Png(f);
                if (e == null || im == null || fw <= 0) continue;
                written.Add(Strip(im, fw, e, Path.Combine(outDir, bn.Replace("@", "_at") + "_sway_strip.png")));
            }
            Debug.Log("[Batch] RESULT decor strips: " + string.Join(", ", written));
        }

        static string Strip(PixelImage src, int fw, CodeAnimEntry e, string path)
        {
            int fh = src.h, variants = src.w / fw, steps = Mathf.Max(1, e.steps), gap = 4;
            int cw = fw + gap, W = variants * steps * cw, H = 3 * (fh + gap);
            var sheet = new PixelImage(W, H);
            for (int i = 0; i < sheet.px.Length; i++) sheet.px[i] = new Color32(58, 64, 86, 255);
            for (int v = 0; v < variants; v++)
            {
                var still = new PixelImage(fw, fh);
                for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++) still.Set(x, y, src.Get(v * fw + x, y));
                for (int s = 0; s < steps; s++)
                {
                    var f = new PixelImage(still);
                    foreach (var x in e.effects) if (x != null && x.IsBaked) CodeAnimBake.ApplyBaked(x, f, s);
                    int ox = (v * steps + s) * cw;
                    for (int y = 0; y < fh; y++)
                        for (int x = 0; x < fw; x++)
                        {
                            var c = f.Get(x, y);
                            if (c.a >= 128) { sheet.Set(ox + x, y, c); sheet.Set(ox + fw - 1 - x, fh + gap + y, c); }
                            var c0 = still.Get(x, y);
                            bool moved = !c.Equals(c0);
                            if (moved) sheet.Set(ox + x, 2 * (fh + gap) + y, new Color32(255, 0, 200, 255));
                            else if (c.a >= 128) sheet.Set(ox + x, 2 * (fh + gap) + y, new Color32((byte)(c.r / 3 + 40), (byte)(c.g / 3 + 40), (byte)(c.b / 3 + 40), 255));
                        }
                }
            }
            var big = new Texture2D(W * Scale, H * Scale, TextureFormat.RGBA32, false);
            var px = new Color32[W * Scale * H * Scale];
            for (int y = 0; y < H * Scale; y++)
                for (int x = 0; x < W * Scale; x++)
                    px[(H * Scale - 1 - y) * W * Scale + x] = sheet.px[(y / Scale) * W + x / Scale];
            big.SetPixels32(px); big.Apply();
            File.WriteAllBytes(path, big.EncodeToPNG());
            Object.DestroyImmediate(big);
            return path;
        }

        static PixelImage Png(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(t); return null; }
            var raw = t.GetPixels32();
            var img = new PixelImage(t.width, t.height);
            for (int y = 0; y < t.height; y++) System.Array.Copy(raw, (t.height - 1 - y) * t.width, img.px, y * t.width, t.width);
            Object.DestroyImmediate(t);
            return img;
        }
    }
}
