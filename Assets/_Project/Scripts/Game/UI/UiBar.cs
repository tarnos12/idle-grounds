using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Progress-bar fill driver. Placeholder bars are Filled images (fillAmount); once UiSkin swaps the fill to the delivered
    /// 9-slice (Sliced, which cannot "fill"), the fraction drives the right anchor of the stretch-anchored fill rect instead.
    /// Allocation-free; only writes on change.
    /// </summary>
    public static class UiBar
    {
        public static void Set(Image fill, float fraction)
        {
            if (fill == null) return;
            fraction = Mathf.Clamp01(fraction);
            if (fill.type == Image.Type.Sliced)
            {
                var rt = fill.rectTransform;
                var max = rt.anchorMax;
                if (!Mathf.Approximately(max.x, fraction))
                {
                    max.x = fraction; rt.anchorMax = max;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                }
                bool on = fraction > 0.0001f;
                if (fill.enabled != on) fill.enabled = on;
            }
            else if (!Mathf.Approximately(fill.fillAmount, fraction)) fill.fillAmount = fraction;
        }
    }
}
