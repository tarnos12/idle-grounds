using System.Collections.Generic;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Cached sprite lookups for views (GameDatabase lookups are linear scans).</summary>
    public sealed class SpriteCache
    {
        readonly GameDatabase db;
        readonly Sprite decoSprite;
        readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public SpriteCache(GameDatabase db, Sprite decoSprite) { this.db = db; this.decoSprite = decoSprite; }

        public Sprite Item(string item)
        {
            string k = "i:" + item;
            if (!cache.TryGetValue(k, out var s)) cache[k] = s = db.ItemIcon(item);
            return s;
        }

        /// <summary>Fixture -> fix_&lt;kind&gt;, spawner node -> node_&lt;spawnerKind&gt;, deco -> the deco tree sprite.</summary>
        public Sprite Node(string area, Node n)
        {
            if (n.deco && !n.isFixed) return decoSprite != null ? decoSprite : db.missingSprite;
            string k = area + ":" + (n.isFixed ? "f:" : "n:") + (n.isFixed ? n.kind : n.spawnerKind ?? n.kind);
            if (!cache.TryGetValue(k, out var s))
                cache[k] = s = n.isFixed ? db.FixtureSprite(area, n.kind) : db.NodeSprite(area, n.spawnerKind ?? n.kind);
            return s;
        }

        readonly Dictionary<string, SpriteEntry> artCache = new Dictionary<string, SpriteEntry>();
        /// <summary>Real-art entry (sprite + frames) for a node/fixture, or null for the emoji placeholder.</summary>
        public SpriteEntry NodeArt(string area, Node n)
        {
            if (n.deco && !n.isFixed)
            {
                if (!artCache.TryGetValue("deco_tree", out var d))
                    artCache["deco_tree"] = d = db.fx.Find(x => x != null && x.key == "deco_tree" && x.realArt && x.sprite != null);
                return d;
            }
            string key = (n.isFixed ? "fix_" : "node_") + (n.isFixed ? n.kind : n.spawnerKind ?? n.kind);
            string k = area + ":" + key;
            if (!artCache.TryGetValue(k, out var e)) artCache[k] = e = db.RealNodeArt(area, key);
            return e;
        }

        public Sprite Building(string type)
        {
            string k = "b:" + type;
            if (!cache.TryGetValue(k, out var s)) cache[k] = s = db.BuildingIcon(type);
            return s;
        }

        public Sprite BuildingArt(string type)
        {
            string k = "ba:" + type;
            if (!cache.TryGetValue(k, out var s)) cache[k] = s = db.BuildingArt(type);
            return s;
        }

        readonly Dictionary<string, Sprite[]> framesCache = new Dictionary<string, Sprite[]>();
        /// <summary>Body-art animation frames (null = static).</summary>
        public Sprite[] BuildingFrames(string type)
        {
            string k = "bf:" + type;
            if (!framesCache.TryGetValue(k, out var f)) framesCache[k] = f = db.BuildingFrames(type);
            return f;
        }
        public Sprite[] BuildingVariant(string type, string variant)
        {
            string k = "bv:" + type + ":" + variant;
            if (!framesCache.TryGetValue(k, out var f)) framesCache[k] = f = db.BuildingVariantFrames(type, variant);
            return f;
        }
        public Sprite[] DragonAwakeFrames()
        {
            if (!framesCache.TryGetValue("dragon_awake", out var f)) framesCache["dragon_awake"] = f = db.DragonAwakeFrames();
            return f;
        }

        readonly Dictionary<string, Sprite[]> fxCache = new Dictionary<string, Sprite[]>();
        /// <summary>Delivered FX animation frames for an fx_* key, or null (callers fall back to procedural visuals).</summary>
        public Sprite[] FxFrames(string key)
        {
            if (!fxCache.TryGetValue(key, out var f)) fxCache[key] = f = db.FxFrames(key);
            return f;
        }

        public Sprite Get(string key)
        {
            if (!cache.TryGetValue(key, out var s)) cache[key] = s = db.GetSprite(key);
            return s;
        }
    }
}
