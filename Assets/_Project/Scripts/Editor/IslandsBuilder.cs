using System.IO;
using IdleGrounds.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGrounds.Editor.BuildingsBuilder;
using static IdleGrounds.Editor.CoreLoopBuilder;
using static IdleGrounds.Editor.ProgressionBuilder;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// ADR 0003 view side, reproducible: the Sky root (3/4 top-down: depth vignette + a CloudSea of cloud puffs at 3 depths, all
    /// on the "Sky" sorting layer), camera clear colour = sky base (the abyss), the QiTrail
    /// world prefab + <see cref="QiTrailSync"/> (Runtime/QiTrails), <see cref="SkyWispViewSync"/>
    /// (Runtime/SkyWisps, reusing the Wisp prefab), the BridgePanel UI prefab under UI/Canvas wired into
    /// BuildController, and the unlock steles' camera link. The Islands themselves: <see cref="WorldBuilder"/>.
    /// </summary>
    public static class IslandsBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string Sky = WorldBuilder.SkyLayer;

        [MenuItem("Idle Grounds/Prefabs/Build Island + Bridge Prefabs (ADR 0003)")]
        public static void BuildAllPrefabs()
        {
            EnsureSortingLayers();
            WorldBuilder.EnsureSkySortingLayer();
            EnsureShapes();
            IslandArtBuilder.EnsureArt();
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            BuildQiTrailPrefab();
            BuildBridgePanelPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] Island + bridge prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Islands, Sky + Bridges (ADR 0003)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] Islands, sky + bridges installed into " + ScenePath);
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildQiTrailPrefab()
        {
            var root = new GameObject("QiTrail");
            var v = root.AddComponent<QiTrailView>();
            var frames = IslandArtBuilder.Frames(IslandArtBuilder.QiTrail);
            // under buildings, above every tilemap (Ground layer like the link threads)
            var line = Sr(root.transform, "Line", 45, frames.Length > 0 ? frames[0] : null, "Ground");
            line.drawMode = SpriteDrawMode.Tiled;
            line.tileMode = SpriteTileMode.Continuous;
            Set(v, "line", line);
            SetArray(v, "frames", frames);
            Save(root, WorldPrefabDir + "/QiTrail.prefab");
        }

        static void BuildBridgePanelPrefab()
        {
            var root = Stretch(NewUi("BridgePanel", null));
            var view = root.gameObject.AddComponent<BridgePanelView>();
            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.97f), UiPalette.Line, raycast: true);
            UiSkin.Attach(panel.gameObject, "ui_panel_jade");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-24f, 100f);
            prt.anchoredPosition = new Vector2(0f, 68f);
            ColW(panel.gameObject, 14, 8);
            Fit(panel.gameObject, false, true);

            var head = NewUi("Header", prt);
            Row(head.gameObject, 0, 0, 10);
            var icon = Icon(head, "Icon", Emoji("bld_wisp_lantern"), 33);
            var title = Label(head, "Title", "Spirit Bridge", 21f, UiPalette.Text, true);
            var status = Label(head, "Status", "Unpaired", 19f, UiPalette.Muted, true);
            var buffer = Label(head, "Buffer", "Buffer 0/20", 19f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var unpair = SmallButton(head, "Unpair", "Unpair", UiPalette.Danger, 120, 36);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 42, 36);

            var hint = Wrap(Label(prt, "Hint", "", 18f, UiPalette.Muted, false));
            var empty = Wrap(Label(prt, "Empty", "", 18f, UiPalette.Amber, false));

            var rows = NewUi("Candidates", prt);
            ColW(rows.gameObject, 0, 6);
            Fit(rows.gameObject, false, true);

            // candidate row template: [island icon] "Mine bridge - 128 cells"  [Send to]  [Receive from]
            var rowImg = Chip(rows, "RowTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(rowImg.gameObject, 10, 4, 10);
            Le(rowImg, -1, 44);
            var br = rowImg.gameObject.AddComponent<BridgeRow>();
            br.icon = Icon(rowImg.rectTransform, "Icon", Emoji("ui_area_mine"), 26);
            br.label = Label(rowImg.rectTransform, "Label", "Mine bridge - 128 cells", 18f, UiPalette.Text, true);
            Le(NewUi("Spacer", rowImg.rectTransform), flexW: 1);
            br.sendButton = SmallButton(rowImg.rectTransform, "Send", "Send to >", UiPalette.Hex("#67e8f9"), 160, 34);
            br.receiveButton = SmallButton(rowImg.rectTransform, "Receive", "< Receive from", UiPalette.Hex("#bef264"), 200, 34);
            EditorUtility.SetDirty(br);

            var reason = Label(prt, "Reason", "", 18f, UiPalette.Danger, true);

            Set(view, "panel", panel.gameObject);
            Set(view, "icon", icon);
            Set(view, "titleText", title);
            Set(view, "statusText", status);
            Set(view, "bufferText", buffer);
            Set(view, "hintText", hint);
            Set(view, "rowsRoot", rows);
            Set(view, "rowTemplate", br);
            Set(view, "emptyText", empty);
            Set(view, "unpairButton", unpair);
            Set(view, "reasonText", reason);
            Set(view, "closeButton", close);
            rowImg.gameObject.SetActive(false);
            empty.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/BridgePanel.prefab");
        }

        // ------------------------------------------------------------------ scene

        static ParallaxLayer Layer(Transform parent, string name, Sprite s, int order, ParallaxLayer.Mode mode,
            float centre, float height, float scroll, float drift, Camera cam, Color tint)
        {
            var sr = Sr(parent, name, order, s, Sky);
            sr.color = tint;
            var p = sr.gameObject.AddComponent<ParallaxLayer>();
            p.mode = mode;
            p.bandCentre = centre;
            p.bandHeight = height;
            p.scroll = scroll;
            p.drift = drift;
            Set(p, "target", cam);
            return p;
        }

        // ------------------------------------------------------------------ sky: looking DOWN into a sea of clouds

        /// <summary>Camera clear colour: the deep blue-teal of the abyss under the Islands (screen edges).</summary>
        public static readonly Color AbyssEdge = IslandArtBuilder.SkyBase;
        /// <summary>Slightly lighter depth colour toward the screen centre (no horizon band).</summary>
        public static readonly Color AbyssCentre = new Color32(0x1b, 0x46, 0x58, 0xff);
        const string DepthPath = IslandArtBuilder.SkyDir + "/sky_depth_vignette_64x36.png";
        const string CloudPuffPath = "Assets/_Project/Art/Incoming/sky/sky_cloudpuff_128x64_3f.png";

        /// <summary>A smooth elliptical vignette (lighter centre, abyss edges), stretched over the view by a Fill <see cref="ParallaxLayer"/>.</summary>
        static Sprite EnsureDepthSprite()
        {
            if (!File.Exists(DepthPath))
            {
                const int w = 64, h = 36;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.25f);
                        float k = d * d * (3f - 2f * d);
                        px[y * w + x] = Color.Lerp(AbyssCentre, AbyssEdge, k);
                    }
                tex.SetPixels(px);
                File.WriteAllBytes(DepthPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(DepthPath, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(DepthPath);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 32;
                imp.filterMode = FilterMode.Bilinear;      // a smooth light falloff, not pixel art
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(DepthPath);
        }

        /// <summary>
        /// Sky root for the 3/4 top-down view: no horizon, moon or mountains - the camera looks DOWN past the
        /// Islands into a calm abyss. A depth vignette (fill) plus a <see cref="CloudSea"/> of cloud puffs
        /// gathered in loose banks at three parallax depths (far = 1x, low contrast, tinted toward the abyss; mid/near = 2x),
        /// all on the Sky sorting layer BEHIND the Islands' drop shadows / undersides (orders 20+).
        /// </summary>
        public static void BuildSky(Camera cam)
        {
            var old = GameObject.Find("Sky");
            if (old != null && old.transform.parent == null) Object.DestroyImmediate(old);
            var root = new GameObject("Sky").transform;
            Layer(root, "Depth", EnsureDepthSprite(), -100, ParallaxLayer.Mode.Fill, 0f, 1f, 0f, 0f, cam, Color.white);

            var puffs = IslandArtBuilder.Frames(CloudPuffPath);
            // (sky_sunbeam light shafts were tried: on the dark abyss they read as grey smears, so they are left out)
            var fieldGo = new GameObject("CloudSea");
            fieldGo.transform.SetParent(root, false);
            var field = fieldGo.AddComponent<CloudSea>();
            Set(field, "target", cam);
            var list = new System.Collections.Generic.List<CloudSea.Puff>();
            var rng = new System.Random(0x5EA0C1D);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector2 tile = field.tile;
            var placed = new System.Collections.Generic.List<(Vector2 p, float r)>();
            // (name, count, scale, parallax, drift, tint toward the abyss, alpha, order)
            var layers = new (string name, int count, int scale, float parallax, float drift, float toAbyss, float alpha, int order)[]
            {
                ("Far", 64, 1, 0.12f, 0.12f, 0.66f, 0.60f, -80),
                ("Mid", 24, 2, 0.28f, 0.25f, 0.42f, 0.72f, -70),
                ("Near", 8, 2, 0.48f, 0.45f, 0.18f, 0.85f, -60),
            };
            if (puffs.Length > 0)
                foreach (var L in layers)
                {
                    var tint = Color.Lerp(Color.white, AbyssCentre, L.toAbyss);
                    tint.a = L.alpha;
                    // puffs gather into loose cloud banks (a few per tile) instead of an even confetti
                    int banks = Mathf.Max(2, L.count / 5);
                    var centres = new Vector2[banks];
                    for (int b = 0; b < banks; b++) centres[b] = new Vector2(R(-tile.x * 0.5f, tile.x * 0.5f), R(-tile.y * 0.5f, tile.y * 0.5f));
                    float radius = 1.5f * L.scale;          // puffs of one layer overlap a little, never pile up
                    placed.Clear();
                    for (int i = 0; i < L.count; i++)
                    {
                        Vector2 home = default;
                        var c = centres[i % banks];
                        for (int tries = 0; tries < 40; tries++)
                        {
                            home = c + new Vector2(R(-6f, 6f) * L.scale, R(-2.5f, 2.5f) * L.scale);
                            bool ok = true;
                            foreach (var q in placed) if ((q.p - home).sqrMagnitude < (q.r + radius) * (q.r + radius)) { ok = false; break; }
                            if (ok) break;
                        }
                        placed.Add((home, radius));
                        var sr = Sr(fieldGo.transform, "Cloud" + L.name + i, L.order, puffs[rng.Next(puffs.Length)], Sky);
                        sr.color = tint;
                        sr.flipX = rng.NextDouble() < 0.5;
                        sr.transform.localScale = new Vector3(L.scale, L.scale, 1f);
                        list.Add(new CloudSea.Puff { t = sr.transform, home = home, parallax = L.parallax, drift = L.drift });
                    }
                }
            field.puffs = list.ToArray();
            EditorUtility.SetDirty(field);
        }

        public static void InstallIntoActiveScene()
        {
            WorldBuilder.EnsureSkySortingLayer();
            IslandArtBuilder.EnsureArt();
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var camCtl = Object.FindFirstObjectByType<CameraController>();
            var cam = camCtl != null ? camCtl.GetComponent<Camera>() : Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = IslandArtBuilder.SkyBase;
                EditorUtility.SetDirty(cam);
            }
            BuildSky(cam);

            var runtime = FindOrCreate("Runtime", null).transform;

            var trailsRoot = FindOrCreate("QiTrails", runtime);
            var ts = Ensure<QiTrailSync>(trailsRoot);
            Set(ts, "runner", runner);
            Set(ts, "prefab", AssetDatabase.LoadAssetAtPath<QiTrailView>(WorldPrefabDir + "/QiTrail.prefab"));
            Set(ts, "root", trailsRoot.transform);

            var skyRoot = FindOrCreate("SkyWisps", runtime);
            var sw = Ensure<SkyWispViewSync>(skyRoot);
            Set(sw, "runner", runner);
            Set(sw, "prefab", AssetDatabase.LoadAssetAtPath<WispView>(WorldPrefabDir + "/Wisp.prefab"));
            Set(sw, "root", skyRoot.transform);

            var signs = Object.FindFirstObjectByType<UnlockSignSync>();
            if (signs != null && camCtl != null) Set(signs, "cameraController", camCtl);

            var canvas = GameObject.Find("UI/Canvas");
            var bc = Object.FindFirstObjectByType<BuildController>();
            if (canvas != null)
            {
                var old = canvas.transform.Find("BridgePanel");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/BridgePanel.prefab"), canvas.transform);
                var panel = go.GetComponent<BridgePanelView>();
                Set(panel, "runner", runner);
                if (bc != null) Set(bc, "bridgePanel", panel);
                // keep the bottom bar + hand cursor on top
                var pav = canvas.transform.Find("PavilionPanel");
                if (pav != null) go.transform.SetSiblingIndex(pav.GetSiblingIndex() + 1);
                var bar = Object.FindFirstObjectByType<BottomBarView>();
                if (bar != null) bar.transform.SetAsLastSibling();
                var cursor = canvas.transform.Find("HandCursor");
                if (cursor != null) cursor.SetAsLastSibling();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
