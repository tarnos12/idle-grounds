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
            if (okLabel != null)
            {
                okLabel.text = okText;
                var le = okButton != null ? okButton.GetComponent<LayoutElement>() : null;
                if (le != null) le.preferredWidth = Mathf.Max(ButtonMinWidth, Mathf.Ceil(okLabel.GetPreferredValues(okText).x) + ButtonPad);
            }
            Open();
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
