using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>Slices the placeholder emoji atlas (built by tools/emoji-atlas/build.js) into named sprites.</summary>
    public static class EmojiAtlasImporter
    {
        const string Dir = "Assets/_Project/Art/Sprites/Emoji/";
        const string PngPath = Dir + "emoji_atlas.png";
        const string JsonPath = Dir + "emoji_atlas.json";

        [Serializable] class Entry { public string key; public int x; public int y; }
        [Serializable] class Atlas { public int cell; public int cols; public int width; public int height; public Entry[] sprites; }

        [MenuItem("Idle Grounds/Art/Slice Emoji Atlas")]
        public static void Slice()
        {
            AssetDatabase.Refresh();
            var atlas = JsonUtility.FromJson<Atlas>(File.ReadAllText(JsonPath));
            var importer = AssetImporter.GetAtPath(PngPath) as TextureImporter;
            if (importer == null) { Debug.LogError("EmojiAtlasImporter: missing " + PngPath); return; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 128;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            int cell = atlas.cell, h = tex.height;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            foreach (var s in atlas.sprites)
            {
                rects.Add(new SpriteRect
                {
                    name = s.key,
                    rect = new Rect(s.x * cell, h - (s.y + 1) * cell, cell, cell), // y flipped: Unity origin is bottom-left
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate(),
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            importer.SaveAndReimport();
            Debug.Log($"EmojiAtlasImporter: sliced {rects.Count} sprites from {PngPath}");
        }
    }
}
