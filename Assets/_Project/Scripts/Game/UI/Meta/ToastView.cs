using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    public enum ToastKind { Info, Success, Warning }

    /// <summary>
    /// Top-centre toasts (VP-26): each message is a dark pill with a category icon; at most <see cref="MaxStacked"/> stack
    /// (the oldest drops out), each shows for <see cref="HoldSeconds"/> then fades over <see cref="FadeSeconds"/>.
    /// The offline toast is gone (ADR 0002); kept as a generic toast.
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        public const float HoldSeconds = 3f, FadeSeconds = 0.6f;
        public const int MaxStacked = 3;

        [SerializeField] GameObject root;
        [SerializeField] RectTransform stack;
        [SerializeField] GameObject itemTemplate;
        [SerializeField] Sprite infoIcon, successIcon, warningIcon;

        class Item { public GameObject go; public CanvasGroup group; public Image icon; public TextMeshProUGUI text; public float shownAt; }

        readonly List<Item> live = new List<Item>();
        readonly List<Item> pool = new List<Item>();
        string lastText;

        public bool Visible => live.Count > 0;
        public int Count => live.Count;
        public string Text => lastText;

        void Awake()
        {
            if (itemTemplate != null) itemTemplate.SetActive(false);
            if (root != null) root.SetActive(false);
        }

        public void Show(string msg) => Show(msg, ToastKind.Info);

        public void Show(string msg, ToastKind kind)
        {
            if (string.IsNullOrEmpty(msg) || itemTemplate == null || stack == null) return;
            while (live.Count >= MaxStacked) Release(live[0]);
            var it = Acquire();
            it.text.text = msg;
            it.icon.sprite = kind == ToastKind.Success ? successIcon : kind == ToastKind.Warning ? warningIcon : infoIcon;
            it.icon.enabled = it.icon.sprite != null;
            it.shownAt = Time.unscaledTime;
            it.group.alpha = 1f;
            it.go.transform.SetAsLastSibling();
            it.go.SetActive(true);
            live.Add(it);
            lastText = msg;
            root.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        }

        Item Acquire()
        {
            if (pool.Count > 0) { var p = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); return p; }
            var go = Instantiate(itemTemplate, stack);
            go.name = "Toast";
            return new Item
            {
                go = go, group = go.GetComponent<CanvasGroup>(),
                icon = go.transform.Find("Icon").GetComponent<Image>(), text = go.transform.Find("Text").GetComponent<TextMeshProUGUI>(),
            };
        }

        void Release(Item it)
        {
            live.Remove(it);
            it.go.SetActive(false);
            pool.Add(it);
        }

        void Update()
        {
            if (live.Count == 0) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var it = live[i];
                float t = Time.unscaledTime - it.shownAt;
                if (t >= HoldSeconds + FadeSeconds) { Release(it); continue; }
                it.group.alpha = t <= HoldSeconds ? 1f : 1f - (t - HoldSeconds) / FadeSeconds;
            }
            if (live.Count == 0) root.SetActive(false);
        }
    }
}
