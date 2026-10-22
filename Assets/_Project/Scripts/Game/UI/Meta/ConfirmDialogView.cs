using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>The browser confirm() of the original (Reset, Ascend to Immortality): title + message + OK / Cancel. The OK button grows to its label.</summary>
    public class ConfirmDialogView : ModalView
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] Button okButton;
        [SerializeField] TextMeshProUGUI okLabel;
        [SerializeField] Button cancelButton;

        Action onYes;
        public string Message => messageText != null ? messageText.text : null;
        public string Title => titleText != null && titleText.gameObject.activeSelf ? titleText.text : null;

        protected override void Awake()
        {
            base.Awake();
            if (okButton != null) okButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        }

        public const float ButtonMinWidth = 200f, ButtonPad = 56f;
        float baseBoxW;

        public void Ask(string message, string okText, Action yes) => Ask(null, message, okText, yes);

        /// <summary>Title line (gold, optional) + body + OK label; OK is at least 200 px and widens to fit its label (VP-01).</summary>
        public void Ask(string title, string message, string okText, Action yes)
        {
            onYes = yes;
            if (titleText != null)
            {
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
                titleText.text = title ?? "";
            }
            if (messageText != null) messageText.text = message;
            if (okLabel != null) { okLabel.text = okText; FitButtons(); }
            Open();
        }

        /// <summary>OK is at least 200 and widens to its label; a wide Text font (Press Start 2P) makes "Ascend to Immortality" wider than the 660 box, so the box grows with the button row.
        /// Re-run while open: the label is measured in the font and size it ends up drawn in (a font switch or a canvas change re-snaps the size after the first measure).</summary>
        void FitButtons()
        {
            if (okLabel == null || okButton == null) return;
            float w = Mathf.Max(ButtonMinWidth, Mathf.Ceil(UiFit.WidestText(okLabel, okLabel.text)) + ButtonPad);
            if (Mathf.Approximately(w, lastOkW)) return;
            lastOkW = w;
            var le = okButton.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = w;
            var row = okButton.transform.parent as RectTransform;
            if (row == null || Box == null) return;
            if (baseBoxW <= 0f) baseBoxW = Box.sizeDelta.x;
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            float need = Mathf.Ceil(LayoutUtility.GetPreferredWidth(row)) + 2f * 27f;
            Box.sizeDelta = new Vector2(Mathf.Max(baseBoxW, need), Box.sizeDelta.y);
            MarkFitDirty();
        }

        float lastOkW = -1f;

        protected override void LateUpdate()
        {
            if (IsOpen) FitButtons();
            base.LateUpdate();
        }

        public void Confirm()
        {
            var a = onYes;
            onYes = null;
            Close();
            a?.Invoke();
        }

        public override void Close() { onYes = null; base.Close(); }
    }
}
