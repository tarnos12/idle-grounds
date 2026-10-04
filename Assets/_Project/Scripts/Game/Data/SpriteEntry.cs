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
    }
}
