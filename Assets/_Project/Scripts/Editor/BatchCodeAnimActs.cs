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
    ///   animPreview:KEY@island,row,col   temporary still + CodeAnimator for a key without a gameplay view (Shrine, NPCs):
    ///                             the still is Art/Incoming/*/KEY@64.png, else frame 0 of docs/art/rejected/KEY_idle_64x96_4f@64_v1_REJECTED.png
    ///                             (the accepted NPC frame-0 drawings); bottom-centre on the cell's bottom edge
    ///   animPreviewClear          remove every preview object
    /// </summary>
    public static partial class Batch
    {
        static readonly List<GameObject> animPreviews = new List<GameObject>();

        static bool CodeAnimAct(string name, string arg)
        {
            switch (name)
            {
                case "animTime": CodeAnimClock.Override = F(arg); return true;
                case "animPreviewClear":
                    foreach (var go in animPreviews) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                    animPreviews.Clear();
                    return true;
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
