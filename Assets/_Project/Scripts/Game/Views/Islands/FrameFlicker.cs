using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Rare 2-frame flick on a still decor sprite (ART-SPEC §3.8 ice-fishing hole: frame 0 = rest, frame 1 = a fish tail flicks
    /// out). Every <see cref="period"/> seconds (offset by a seeded phase) the second sprite shows for <see cref="flick"/>
    /// seconds. Calm motion (<see cref="Juice.ReduceMotion"/>): the rest frame only. Time comes from
    /// <see cref="CodeAnimClock"/> so a capture can pin it (<c>animTime</c>). No allocation per frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FrameFlicker : MonoBehaviour
    {
        public Sprite rest, flicked;
        public float period = 7f, flick = 0.45f, phase;
        SpriteRenderer sr;

        void Awake() { sr = GetComponent<SpriteRenderer>(); }

        void Update()
        {
            if (sr == null || rest == null) return;
            bool on = flicked != null && !Juice.ReduceMotion && Mathf.Repeat(CodeAnimClock.Now + phase, period) < flick;
            var s = on ? flicked : rest;
            if (sr.sprite != s) sr.sprite = s;
        }
    }
}
