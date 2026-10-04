using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>The browser confirm() of the original (Reset, Ascend): message + OK / Cancel.</summary>
    public class ConfirmDialogView : ModalView
    {
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] Button okButton;
        [SerializeField] TextMeshProUGUI okLabel;
        [SerializeField] Button cancelButton;

        Action onYes;
        public string Message => messageText != null ? messageText.text : null;

        protected override void Awake()
        {
            base.Awake();
            if (okButton != null) okButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        }

        public void Ask(string message, string okText, Action yes)
        {
            onYes = yes;
            if (messageText != null) messageText.text = message;
            if (okLabel != null) okLabel.text = okText;
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
