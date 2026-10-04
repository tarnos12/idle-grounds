using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Base of the M7 modals (ui-input-render §4: scrim rgba(0,0,0,.55) + centred bg-2 box). The scrim is a
    /// raycast target, so world clicks are blocked while open (HandController checks IsPointerOverGameObject).
    /// </summary>
    public abstract class ModalView : MonoBehaviour
    {
        [SerializeField] protected GameRunner runner;
        [SerializeField] protected GameObject modal;
        [SerializeField] protected Button closeButton;

        public bool IsOpen => modal != null && modal.activeSelf;
        protected Simulation Sim => runner != null ? runner.Sim : null;

        protected virtual void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (modal != null) modal.SetActive(false);
        }

        public virtual void Open() { if (modal != null) modal.SetActive(true); }
        public virtual void Close() { if (modal != null) modal.SetActive(false); }

        /// <summary>Esc. True = consumed.</summary>
        public virtual bool Escape()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }
    }
}
