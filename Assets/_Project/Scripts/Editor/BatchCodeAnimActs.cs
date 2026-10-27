using System;
using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Shot acts for the code animation layer (art reviews; editor only, nothing is saved to the scene or the game state):
    ///   animTime:SECONDS          pin <see cref="CodeAnimClock"/> (deterministic frames; negative = live time again)
    ///   calm:1|0                  calm motion on / off for this Play session (in memory; PlayerPrefs are not written)
    ///   animPreview:KEY@island,row,col   temporary still + CodeAnimator for a key without a gameplay view (Shrine, NPCs):
    ///                             the still is Art/Incoming/*/KEY@64.png, else frame 0 of docs/art/rejected/KEY_idle_64x96_4f@64_v1_REJECTED.png
    ///                             (the accepted NPC frame-0 drawings); bottom-centre on the cell's bottom edge
    ///   animPreviewClear          remove every preview object
    ///   decorAnimTest             add the temporary 32-px deco_tree crown-sway entry (DecorAnimStrip.Test32Entry, not shipped) to the
    ///                             loaded code-anim config for this Play session: decor rings and edge-ring trees sway (step D hook check)
    ///   stripPreview:KEY,W,H,FRAME@island,row,col   frame FRAME (W x H px) of Art/Incoming/*/KEY.png at PPU 32 (PPU 64 for a KEY ending in @64), bottom-left on the cell's
    ///                             bottom-left corner (temporary layout of FX pieces without a gameplay view, e.g. Sky Path tiles)
    ///   wheelStandIn:TYPE@island,cx,cy,rx,ry[,px,py,pw,ph[,topY]]   review-only stand-in for an ellipseSpin wheel layer that is not
    ///                             delivered yet (in memory, never saved as art): every built TYPE on the island gets its wheel cut
    ///                             out of the still by the ellipse (centre cx,cy, radii rx,ry; still texels): wood / gold / ink texels
    ///                             -> layer_wheel; water, stone and leaf texels, the rect px,py,pw,ph (a post in front) and rows
    ///                             above topY (roof) -> layer_front (behind the post and the water the wheel gets P20 as its hidden
    ///                             part); base = the still with the wheel texels painted P02; every other
    ///                             layer of the key is given empty. Then the building's CodeAnimator is re-baked with them.
    /// </summary>
    public static partial class Batch
    {
        static readonly List<GameObject> animPreviews = new List<GameObject>();

        static bool CodeAnimAct(string name, string arg)
        {
            switch (name)
            {
                case "animTime": CodeAnimClock.Override = F(arg); return true;
                case "calm":
                {
                    // review only: calm motion on (calm:1) / off (calm:0) for this Play session, in memory; PlayerPrefs are not
                    // written, so the owner's Motion setting stays as it is
                    var jt = typeof(Juice);
                    var bf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
                    jt.GetField("loaded", bf).SetValue(null, true);
                    jt.GetField("reduce", bf).SetValue(null, arg.Trim() != "0");
                    return true;
                }
                case "decorAnimTest":
                {
                    // review only (in memory, this Play session): the temporary 32-px deco_tree sway entry of DecorAnimStrip joins
                    // the loaded config; the NodeView decor rings and the edge-ring DecorMotion re-bind on the version change
                    var all = new List<CodeAnimEntry>(CodeAnimLibrary.File.entries);
                    all.AddRange(CodeAnimLibrary.Parse(DecorAnimStrip.Test32Entry).entries);
                    CodeAnimLibrary.Use(new CodeAnimFile { version = CodeAnimLibrary.File.version, entries = all.ToArray() });
                    foreach (var dm in UnityEngine.Object.FindObjectsByType<DecorMotion>(FindObjectsSortMode.None))
                    {
                        int n = dm.Scan();
                        var isl = dm.GetComponentInParent<Island>();
                        Vector3 first = n > 0 && isl != null ? dm.Animator(0).transform.position - (Vector3)isl.Centre : Vector3.zero;
                        Debug.Log(Tag + $"decorAnimTest: {(isl != null ? isl.islandKey : dm.name)} edge ring: {n} animated decor sprite(s)" + (n > 0 ? $", first at {first.x:0.#},{first.y:0.#} from the centre" : ""));
                    }
                    return true;
                }
                case "wheelStandIn": WheelStandIn(arg); return true;
                case "animPreviewClear":
                    foreach (var go in animPreviews) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                    animPreviews.Clear();
                    return true;
                case "stripPreview":
                {
                    int at = arg.LastIndexOf('@');      // the key may carry @64
                    var k = arg.Substring(0, at).Split(',');
                    var p = arg.Substring(at + 1).Split(',');
                    string file = null;
                    foreach (var dir in Directory.GetDirectories("Assets/_Project/Art/Incoming"))
                        if (File.Exists(dir + "/" + k[0].Trim() + ".png")) file = dir + "/" + k[0].Trim() + ".png";
                    if (file == null) { Debug.LogError(Tag + "stripPreview: no file for " + k[0]); return true; }
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    t.LoadImage(File.ReadAllBytes(file));
                    t.filterMode = FilterMode.Point;
                    int w = int.Parse(k[1]), h = int.Parse(k[2]), fr = int.Parse(k[3]);
                    float ppu = k[0].Trim().EndsWith("@64") ? 64f : 32f;      // @64 strips (e.g. dragon_stir_320x384_3f@64) keep their world size
                    var sp = Sprite.Create(t, new Rect(fr * w, t.height - h, w, h), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
                    var c = CellWorld(p[0].Trim(), F(p[1]), F(p[2]));
                    var go = new GameObject("StripPreview_" + k[0]);
                    go.transform.position = new Vector3(c.x - 0.5f, c.y - 0.5f, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    var art = UnityEngine.Object.FindFirstObjectByType<BuildingView>()?.ArtRenderer;
                    if (art != null) { sr.sortingLayerID = art.sortingLayerID; sr.sortingOrder = art.sortingOrder + 20; sr.sharedMaterial = art.sharedMaterial; }
                    else sr.sortingOrder = 500;
                    animPreviews.Add(go);
                    return true;
                }
                case "animPreview":
                {
                    int at = arg.IndexOf('@');
                    string key = arg.Substring(0, at).Trim();
                    var p = arg.Substring(at + 1).Split(',');
                    var still = PreviewSprite(key);
                    if (still == null) { Debug.LogError(Tag + "animPreview: no still for " + key); return true; }
                    var c = CellWorld(p[0].Trim(), F(p[1]), F(p[2]));
                    var go = new GameObject("CodeAnimPreview_" + key);
                    go.transform.position = new Vector3(c.x, c.y - 0.5f, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = still;
                    var art = UnityEngine.Object.FindFirstObjectByType<BuildingView>()?.ArtRenderer;
                    if (art != null) { sr.sortingLayerID = art.sortingLayerID; sr.sortingOrder = art.sortingOrder + 20; sr.sharedMaterial = art.sharedMaterial; }
                    else sr.sortingOrder = 500;
                    var entry = CodeAnimLibrary.Find(key);
                    var ca = CodeAnimator.For(sr);
                    bool ok = ca.Configure(entry, sr, still, suffix => PreviewSprite(CodeAnimLibrary.FileKey(key, suffix), false));
                    ca.Playing = true;
                    Debug.Log(Tag + $"animPreview {key}: configured={ok} still={still.rect.width}x{still.rect.height} missingLayers=[{string.Join(", ", ca.MissingLayers)}] overlays={ca.SlotCount} baseFrames={ca.BaseFrameCount}");
                    animPreviews.Add(go);
                    return true;
                }
            }
            return false;
        }

        static void WheelStandIn(string arg)
        {
            int at = arg.IndexOf('@');
            string type = arg.Substring(0, at).Trim();
            var p = arg.Substring(at + 1).Split(',');
            string island = p[0].Trim();
            float cx = F(p[1]), cy = F(p[2]), rx = F(p[3]), ry = F(p[4]);
            var post = p.Length >= 9 ? new RectInt(int.Parse(p[5].Trim()), int.Parse(p[6].Trim()), int.Parse(p[7].Trim()), int.Parse(p[8].Trim())) : new RectInt(0, 0, 0, 0);
            int topY = p.Length >= 10 ? int.Parse(p[9].Trim()) : 0;
            Color32 p02 = new Color32(0x1B, 0x1F, 0x2B, 255), p20 = new Color32(0x3D, 0x26, 0x16, 255);
            int done = 0;
            foreach (var bv in UnityEngine.Object.FindObjectsByType<BuildingView>(FindObjectsSortMode.None))
            {
                if (bv.Building == null || bv.Building.type != type || bv.Area != island) continue;
                var ca = bv.CodeAnim; var art = bv.ArtRenderer;
                if (ca == null || art == null || ca.Still == null) { Debug.LogError(Tag + "wheelStandIn: " + type + " has no code animation"); continue; }
                var still = ca.Still;
                var img = PixelImage.FromSprite(still);
                if (img == null) { Debug.LogError(Tag + "wheelStandIn: still not readable"); continue; }
                var wheel = new PixelImage(img.w, img.h); var front = new PixelImage(img.w, img.h); var bas = new PixelImage(img);
                int nw = 0, nf = 0;
                for (int y = 0; y < img.h; y++)
                    for (int x = 0; x < img.w; x++)
                    {
                        float ex = (x - cx) / rx, ey = (y - cy) / ry;
                        if (ex * ex + ey * ey > 1f) continue;
                        var c = img.Get(x, y);
                        if (c.a < 128) continue;
                        bool inPost = post.Contains(new Vector2Int(x, y));
                        if (y < topY || inPost || StandInStatic(c))
                        {
                            front.Set(x, y, c); nf++;
                            // the wheel goes on behind the post and the falling water: dark wood (P20) stands in for the hidden part
                            if (y >= topY && (inPost || IsWater(c))) { wheel.Set(x, y, p20); bas.Set(x, y, p02); }
                        }
                        else { wheel.Set(x, y, c); bas.Set(x, y, p02); nw++; }
                    }
                var pivotTL = new Vector2(still.pivot.x, img.h - still.pivot.y);
                Sprite S(PixelImage i, string nm) => i.ToSprite(type + "_standin_" + nm, still.pixelsPerUnit, pivotTL);
                Sprite sw = S(wheel, "wheel"), sf = S(front, "front"), sb = S(bas, "base"), se = S(new PixelImage(img.w, img.h), "empty");
                var entry = CodeAnimLibrary.Find(IdleGrounds.Game.Data.ArtKeys.Building(type));
                bool ok = ca.ConfigureUncached(entry, art, still, s => s == "layer_wheel" ? sw : s == "base" ? sb : s == "layer_front" ? sf : s.StartsWith("layer_") ? se : null);
                Debug.Log(Tag + $"wheelStandIn {type}@{island}: wheel={nw}px front={nf}px configured={ok} missing=[{string.Join(", ", ca.MissingLayers)}] overlays={ca.SlotCount} baseFrames={ca.BaseFrameCount}");
                done++;
            }
            if (done == 0) Debug.LogError(Tag + "wheelStandIn: no built " + type + " on " + island);
        }

        static bool IsWater(Color32 c) { Color.RGBToHSV(c, out float h, out float s, out _); h *= 360f; return (h >= 180f && h <= 215f && s > 0.15f) || s < 0.1f; }

        /// <summary>Stand-in classification: water (cyan / blue / white), stone (greys) and leaves stay still in front of the wheel.</summary>
        static bool StandInStatic(Color32 c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h *= 360f;
            if (s < 0.2f && v > 0.25f) return true;                 // foam white, pale stone
            if (h >= 210f && h <= 250f && v > 0.22f) return true;     // stone greys P03-P06 (ink P01 / P02 stays with the wheel)
            if (h >= 180f && h <= 215f && s > 0.15f) return true;    // water
            if (h >= 75f && h <= 170f && s > 0.25f) return true;     // leaves
            return false;
        }

        /// <summary>Delivered sprite for an art key (Art/Incoming, any category, @64 first), else (stillFallback) frame 0 of the v1 rejected NPC strip.</summary>
        static Sprite PreviewSprite(string key, bool stillFallback = true)
        {
            foreach (var tag in new[] { "@64", "" })
                foreach (var dir in Directory.GetDirectories("Assets/_Project/Art/Incoming"))
                {
                    string path = (dir + "/" + key + tag + ".png").Replace('\\', '/');
                    if (!File.Exists(path)) continue;
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite s) return s;
                }
            if (!stillFallback) return null;
            string strip = "docs/art/rejected/" + key + "_idle_64x96_4f@64_v1_REJECTED.png";
            if (!File.Exists(strip)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(strip));
            t.filterMode = FilterMode.Point;
            var sp = Sprite.Create(t, new Rect(0, t.height - 96, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
            sp.name = key + "_frame0_preview";
            return sp;
        }
    }
}
