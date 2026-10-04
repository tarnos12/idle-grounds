using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Bottom bar (ui-input-render §4.1): title + version badge on the left; area pill (region under
    /// the camera centre, lock + sprint tags), hand pill "n/cap" (green, gold ≥90%, red at cap) and the
    /// button row on the right. Help/Stats/Build/Demolish are placeholders until M3/M4; Reset is disabled.
    /// </summary>
    public class BottomBarView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] CameraController cameraController;
        [SerializeField] TextMeshProUGUI versionText;
        [SerializeField] TextMeshProUGUI areaText;
        [SerializeField] Image areaIcon;
        [SerializeField] GameObject areaLock;
        [SerializeField] GameObject sprintTag;
        [SerializeField] TextMeshProUGUI handText;
        [SerializeField] Image handPill;
        [SerializeField] Button resetButton;

        string lastArea;
        int lastTotal = -1, lastCap = -1;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            if (resetButton != null) resetButton.interactable = false;   // save/reset arrives with M5
        }

        void Start()
        {
            if (runner == null || runner.Sim == null) return;
            var bal = runner.Config.balance;
            if (versionText != null) versionText.text = "v" + bal.versionNum;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            var cam = cameraController != null ? cameraController.transform.position : Vector3.zero;
            string area = runner.Space.RegionAtOrNearest(cam);
            if (area != lastArea && area != null)
            {
                lastArea = area;
                var def = runner.Config.Region(area);
                if (areaText != null) areaText.text = def != null ? def.name : area;
                if (areaIcon != null) areaIcon.sprite = runner.Database.RegionIcon(area);
            }
            if (areaLock != null)
            {
                bool locked = area != null && !runner.IsUnlocked(area);
                if (areaLock.activeSelf != locked) areaLock.SetActive(locked);
            }
            if (sprintTag != null && cameraController != null && sprintTag.activeSelf != cameraController.SprintActive)
                sprintTag.SetActive(cameraController.SprintActive);

            int total = runner.Sim.Hand.Total(), cap = runner.Sim.Hand.Cap();
            if (total != lastTotal || cap != lastCap)
            {
                lastTotal = total; lastCap = cap;
                if (handText != null)
                {
                    handText.text = total + "/" + cap;
                    var c = total >= cap ? UiPalette.Danger : total >= 0.9f * cap ? UiPalette.Gold : UiPalette.Accent;
                    handText.color = c;
                }
            }
        }
    }
}
