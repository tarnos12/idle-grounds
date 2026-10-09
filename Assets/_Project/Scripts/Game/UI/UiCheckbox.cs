using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Skins a uGUI Toggle with the 2-frame art (ui_checkbox: frame 0 off, frame 1 on). No art -> does nothing (the placeholder
    /// background + Check graphic stay). With art the Check graphic is hidden and the background sprite swaps on change.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class UiCheckbox : MonoBehaviour
    {
        public const string Key = "ui_checkbox";
        Toggle toggle;
        Image bg;
        Sprite[] frames;
        bool applied;

        void OnEnable()
        {
            TryApply();
            if (applied) { toggle.onValueChanged.AddListener(Sync); Sync(toggle.isOn); }
        }

        void OnDisable() { if (toggle != null) toggle.onValueChanged.RemoveListener(Sync); }

        void Start() { if (!applied) { TryApply(); if (applied) { toggle.onValueChanged.AddListener(Sync); Sync(toggle.isOn); } } }

        void TryApply()
        {
            if (applied) return;
            var runner = GameRunner.Instance;
            if (runner == null || runner.Database == null) return;
            var f = runner.Database.UiFrames(Key);
            if (f == null || f.Length < 2) return;
            toggle = GetComponent<Toggle>();
            bg = toggle.targetGraphic as Image;
            if (bg == null) return;
            frames = f;
            var ol = GetComponent<Outline>(); if (ol != null) ol.enabled = false;
            if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(false);
            toggle.graphic = null;
            bg.type = Image.Type.Simple; bg.preserveAspect = true; bg.color = Color.white;
            var le = GetComponent<LayoutElement>();   // 16 art px at exactly 2 UI px each
            if (le != null) { le.minWidth = le.preferredWidth = 32f; le.minHeight = le.preferredHeight = 32f; }
            applied = true;
        }

        void Sync(bool on)
        {
            var s = frames[on ? 1 : 0];
            if (bg.sprite != s) bg.sprite = s;
        }
    }
}
