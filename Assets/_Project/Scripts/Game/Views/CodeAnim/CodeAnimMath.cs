using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Pure helpers of the code animation layer: texel snapping, the 4 alpha steps, stepped time, hash noise.</summary>
    public static class CodeAnimMath
    {
        /// <summary>The 4 alpha steps of ART-SPEC §2.1 (64 / 128 / 191 / 255); 0 stays 0.</summary>
        public static float QuantizeAlpha(float a)
        {
            if (a <= 0.125f) return a <= 0f ? 0f : 64f / 255f;
            int q = Mathf.Clamp(Mathf.RoundToInt(a * 4f), 1, 4);
            return q == 3 ? 191f / 255f : q == 2 ? 128f / 255f : q == 1 ? 64f / 255f : 1f;
        }

        /// <summary>Round a local position (art units) to the art texel grid of the given PPU.</summary>
        public static float Snap(float v, float ppu) => Mathf.Round(v * ppu) / ppu;
        public static Vector3 Snap(Vector3 v, float ppu) => new Vector3(Snap(v.x, ppu), Snap(v.y, ppu), v.z);

        /// <summary>Local position (relative to the still's pivot, in units) of the top-left corner of texel (x, y); both the
        /// texel and the pivot are counted from the still's top-left corner (y down).</summary>
        public static Vector3 TexelToLocal(float x, float y, Vector2 pivotTopLeft, float ppu)
            => new Vector3((x - pivotTopLeft.x) / ppu, (pivotTopLeft.y - y) / ppu, 0f);

        /// <summary>Global step counter at time t (never negative).</summary>
        public static int StepCount(float t, float fps) => t <= 0f ? 0 : (int)(t * fps);
        public static int Step(float t, float fps, int steps) => steps <= 1 ? 0 : StepCount(t, fps) % steps;

        /// <summary>Smooth 0..1..0 pulse over a period, quantised to 4 levels (pixel art: no 256-level fades).</summary>
        public static float Pulse(float t, float period, float phase)
        {
            float u = t / Mathf.Max(0.05f, period) + phase;
            float p = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f);
            return Mathf.Round(p * 3f) / 3f;
        }

        /// <summary>Deterministic hash noise 0..1 (no Random state, no allocation).</summary>
        public static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
