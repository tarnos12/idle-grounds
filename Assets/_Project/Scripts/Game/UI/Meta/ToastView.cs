using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Top-centre toast line (ui.js showOfflineToast): 5 s, then a 0.6 s fade. The offline toast is gone (ADR 0002); kept as a generic toast.</summary>
    public class ToastView : MonoBehaviour
    {
        public const float HoldSeconds = 5f, FadeSeconds = 0.6f;

        [SerializeField] GameObject root;
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI text;

        float shownAt = -100f;
        public bool Visible => root != null && root.activeSelf;
        public string Text => text != null ? text.text : null;

        void Awake() { if (root != null) root.SetActive(false); }

        public void Show(string msg)
        {
            text.text = msg;
            shownAt = Time.unscaledTime;
            group.alpha = 1f;
            root.SetActive(true);
        }

        void Update()
        {
            if (!Visible) return;
            float t = Time.unscaledTime - shownAt;
            if (t >= HoldSeconds + FadeSeconds) { root.SetActive(false); return; }
            group.alpha = t <= HoldSeconds ? 1f : 1f - (t - HoldSeconds) / FadeSeconds;
        }
    }
}
