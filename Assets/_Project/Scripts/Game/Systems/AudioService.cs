using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// SFX playback (ui-input-render §6): a small round-robin pool of 2D AudioSources playing clips from the
    /// <see cref="SfxLibrary"/>. Plays every <c>Sim.Events.SoundRequested</c> (unless the sim is muted)
    /// plus UI sounds via <see cref="Play"/>
    /// ("pickup", "error", "click"). Mute is persisted in PlayerPrefs "ig_muted" (default unmuted).
    /// Harvest gets the JS pitch jitter (620 ± 60 Hz → pitch ±9.7 %).
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public const string MutePref = "ig_muted";

        [SerializeField] GameRunner runner;
        [SerializeField] SfxLibrary library;
        [SerializeField, Range(2, 24)] int voices = 10;

        public static AudioService Instance { get; private set; }
        public bool Muted { get; private set; }
        public SfxLibrary Library => library;
        /// <summary>Total sounds started / last name (verification + tests).</summary>
        public int PlayCount { get; private set; }
        public string LastPlayed { get; private set; }
        public event Action<string> Played;

        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GameRunner.Instance;
            Muted = PlayerPrefs.GetInt(MutePref, 0) == 1;
            for (int i = 0; i < voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.loop = false;
                pool.Add(src);
            }
        }

        void Start()
        {
            if (runner != null && runner.Sim != null) runner.Sim.Events.SoundRequested += OnSound;
        }

        void OnDestroy()
        {
            if (runner != null && runner.Sim != null) runner.Sim.Events.SoundRequested -= OnSound;
            if (Instance == this) Instance = null;
        }

        void OnSound(string name, string area) => PlayInternal(name);

        /// <summary>Play a UI / gameplay sound by its audio.js name (no-op without a service).</summary>
        public static void Play(string name) { if (Instance != null) Instance.PlayInternal(name); }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MutePref, muted ? 1 : 0);
            PlayerPrefs.Save();
            if (muted) foreach (var s in pool) s.Stop();
        }

        public void PlayInternal(string name)
        {
            if (Muted || library == null || string.IsNullOrEmpty(name)) return;
            var e = library.Find(name);
            if (e == null || e.clip == null) return;
            var src = pool[next];
            next = (next + 1) % pool.Count;                          // round robin: the oldest voice is stolen
            src.clip = e.clip;
            src.volume = Mathf.Clamp01(e.volume * library.masterVolume);
            src.pitch = e.pitchJitter > 0f ? 1f + UnityEngine.Random.Range(-e.pitchJitter, e.pitchJitter) : 1f;
            src.Play();
            PlayCount++;
            LastPlayed = name;
            Played?.Invoke(name);
        }
    }
}
