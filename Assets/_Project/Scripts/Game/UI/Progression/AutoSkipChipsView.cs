using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Automation "paused" marker per area (ui-input-render §2.8 "Skipping [icons]" chip, ui.js:744):
    /// an amber pill at the top-left of each visible unlocked region whose automation skipped
    /// saturated item types last tick (<see cref="IdleGrounds.Sim.Simulation.AutomationStatus"/>),
    /// showing up to 4 of those item icons. Placed at the region's on-screen top-left (+6 px),
    /// clamped into the visible part; skipped when less than 40 px of the region is visible.
    /// </summary>
    public class AutoSkipChipsView : MonoBehaviour
    {
        public const int MaxIcons = 4;

        [SerializeField] GameRunner runner;
        [SerializeField] Camera worldCamera;
        [SerializeField] RectTransform root;
        [SerializeField] RectTransform chipTemplate;

        readonly List<RectTransform> chips = new List<RectTransform>();
        readonly Dictionary<RectTransform, Image[]> icons = new Dictionary<RectTransform, Image[]>();
        SpriteCache sprites;
        float next;

        public int ShownCount { get; private set; }
        public List<string> ShownAreas { get; } = new List<string>();

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = (RectTransform)transform;
            if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
        }

        void Start() { if (worldCamera == null) worldCamera = Camera.main; }

        RectTransform Chip(int i)
        {
            while (chips.Count <= i)
            {
                var c = Instantiate(chipTemplate, root);
                c.name = "SkipChip" + chips.Count;
                var list = new List<Image>();
                foreach (var img in c.GetComponentsInChildren<Image>(true)) if (img.transform != c) list.Add(img);
                icons[c] = list.ToArray();
                chips.Add(c);
            }
            return chips[i];
        }

        readonly IdleGrounds.Sim.AutomationStatus stBuf = new IdleGrounds.Sim.AutomationStatus();

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || worldCamera == null || chipTemplate == null) return;
            sprites ??= new SpriteCache(runner.Database, null);
            int n = 0;
            ShownAreas.Clear();
            int cells = runner.Space.Cells;
            float scale = root.lossyScale.x > 0 ? root.lossyScale.x : 1f;
            foreach (var r in runner.Config.regions)
            {
                if (!runner.IsUnlocked(r.key)) continue;
                if (!runner.Sim.AutomationStatus(r.key, stBuf)) continue;
                var st = stBuf;
                if (!st.paused || st.skipped.Count == 0) continue;
                var o = runner.Space.Origin(r.key);
                Vector2 tl = worldCamera.WorldToScreenPoint(new Vector3(o.x, o.y, 0f));
                Vector2 br = worldCamera.WorldToScreenPoint(new Vector3(o.x + cells, o.y - cells, 0f));
                float x0 = Mathf.Max(tl.x, 0f), x1 = Mathf.Min(br.x, Screen.width);
                float yTop = Mathf.Min(tl.y, Screen.height), yBot = Mathf.Max(br.y, 0f);
                if (x1 - x0 < 40f * scale || yTop - yBot < 20f * scale) continue;
                var chip = Chip(n++);
                if (!chip.gameObject.activeSelf) chip.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(x0 + 9f * scale, yTop - 9f * scale), null, out var lp);
                chip.anchoredPosition = lp;
                var imgs = icons[chip];
                for (int i = 0; i < imgs.Length; i++)
                {
                    bool on = i < st.skipped.Count && i < MaxIcons;
                    if (imgs[i].gameObject.activeSelf != on) imgs[i].gameObject.SetActive(on);
                    if (on) imgs[i].sprite = sprites.Item(st.skipped[i]);
                }
                ShownAreas.Add(r.key);
            }
            for (int i = n; i < chips.Count; i++) if (chips[i].gameObject.activeSelf) chips[i].gameObject.SetActive(false);
            ShownCount = n;
        }
    }
}
