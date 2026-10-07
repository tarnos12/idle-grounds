using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [Serializable]
    public class SpriteEntry
    {
        public string key;
        public Sprite sprite;
        /// <summary>All frames when the art is an animated strip (null for static art); sprite = frame 0.</summary>
        public Sprite[] frames;
        /// <summary>True when ArtIntake wired delivered art (native PPU-32 size) rather than the emoji placeholder.</summary>
        public bool realArt;
        /// <summary>Animated overlay drawn just above the sprite at the same pivot/scale (e.g. fix_spirittree_sparkle); null when none.</summary>
        public Sprite[] overlay;
    }
}
