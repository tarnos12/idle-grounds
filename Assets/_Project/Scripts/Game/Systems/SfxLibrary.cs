using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>SFX name (the JS audio.js sound names) → clip. Asset: Assets/_Project/Audio/SfxLibrary.asset.</summary>
    [CreateAssetMenu(menuName = "Idle Grounds/Sfx Library")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public AudioClip clip;
            [Range(0f, 2f)] public float volume = 1f;
            [Tooltip("Random pitch spread (± fraction), e.g. harvest 620±60 Hz = 0.097.")]
            [Range(0f, 0.5f)] public float pitchJitter;
        }

        [Range(0f, 2f)] public float masterVolume = 1f;
        public List<Entry> entries = new List<Entry>();

        public Entry Find(string name)
        {
            foreach (var e in entries) if (e != null && e.name == name) return e;
            return null;
        }
    }
}
