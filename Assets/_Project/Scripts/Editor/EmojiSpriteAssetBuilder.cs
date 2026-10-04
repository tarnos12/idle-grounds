using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Builds the TMP sprite asset from the emoji atlas: each sprite is addressable by name
    /// (&lt;sprite name="item_wood"&gt;) and by its emoji codepoint (raw emoji in text). Registers it as
    /// the TMP default sprite asset.
    /// </summary>
    public static class EmojiSpriteAssetBuilder
    {
        const string Dir = "Assets/_Project/Art/Sprites/Emoji/";
        const string PngPath = Dir + "emoji_atlas.png";
        const string AssetPath = Dir + "EmojiSprites.asset";
        const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [Serializable] class Entry { public string key; public string emoji; }

        [MenuItem("Idle Grounds/Art/Build TMP Emoji Sprite Asset")]
        public static void Build()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var json = File.ReadAllText(Path.Combine(root, "tools", "emoji-atlas", "emoji-manifest.json"));
            var manifest = JsonUtility.FromJson<Wrapper>("{\"items\":" + json + "}").items;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(PngPath).OfType<Sprite>().ToDictionary(s => s.name, s => s);
            if (tex == null || sprites.Count == 0) { Debug.LogError("EmojiSpriteAssetBuilder: slice the atlas first (Idle Grounds/Art/Slice Emoji Atlas)"); return; }

            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            bool created = sa == null;
            if (created)
            {
                sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(sa, AssetPath);
                var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "EmojiSprites Material" };
                mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
                AssetDatabase.AddObjectToAsset(mat, sa);
                sa.material = mat;
            }
            { var vso = new SerializedObject(sa); var vp = vso.FindProperty("m_Version"); if (vp != null) { vp.stringValue = "1.1.0"; vso.ApplyModifiedPropertiesWithoutUndo(); } }   // skip the legacy-upgrade path
            sa.spriteSheet = tex;
            if (sa.material != null) sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);

            const float size = 128f;
            sa.spriteGlyphTable.Clear();
            sa.spriteCharacterTable.Clear();
            var used = new HashSet<uint>();
            uint pua = 0xE000;
            uint idx = 0;
            foreach (var e in manifest)
            {
                if (!sprites.TryGetValue(e.key, out var sp)) { Debug.LogWarning("EmojiSpriteAssetBuilder: no sprite " + e.key); continue; }
                var r = sp.rect;
                var g = new TMP_SpriteGlyph
                {
                    index = idx,
                    metrics = new GlyphMetrics(size, size, 0, size * 0.85f, size),
                    glyphRect = new GlyphRect((int)r.x, (int)r.y, (int)r.width, (int)r.height),
                    scale = 1f,
                    atlasIndex = 0,
                    sprite = sp,
                };
                sa.spriteGlyphTable.Add(g);

                uint cp = (uint)char.ConvertToUtf32(e.emoji, 0);   // base codepoint (VS16 ignored)
                if (!used.Add(cp)) { cp = pua++; used.Add(cp); }    // duplicate emoji: name-only (private-use unicode)
                var c = new TMP_SpriteCharacter(cp, g) { name = e.key, scale = 1f };
                sa.spriteCharacterTable.Add(c);
                idx++;
            }
            sa.faceInfo = new FaceInfo { pointSize = 128, scale = 1f, lineHeight = size, ascentLine = size * 0.85f, baseline = 0, descentLine = -size * 0.15f };
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
                var em = so.FindProperty("m_enableEmojiSupport"); if (em != null) em.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"EmojiSpriteAssetBuilder: {sa.spriteCharacterTable.Count} sprites -> {AssetPath}; default sprite asset set");
        }

        [Serializable] class Wrapper { public Entry[] items; }
    }
}
