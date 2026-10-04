using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Bottom-bar buff pill (ui-input-render §4.1 #3): hidden unless a dragon blessing or Martial Vigor
    /// is active; purple chip with "{icon} {Blessing} {s}s" and "{icon} Martial Vigor {s}s", counting
    /// down every second (sim clock).
    /// </summary>
    public class BuffPillView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] GameObject pill;
        [SerializeField] GameObject blessing;
        [SerializeField] Image blessingIcon;
        [SerializeField] TextMeshProUGUI blessingText;
        [SerializeField] GameObject vigor;
        [SerializeField] Image vigorIcon;
        [SerializeField] TextMeshProUGUI vigorText;

        SpriteCache sprites;
        public string BlessingLabel => blessing != null && blessing.activeSelf ? blessingText.text : null;
        public string VigorLabel => vigor != null && vigor.activeSelf ? vigorText.text : null;

        void Awake() { if (runner == null) runner = GameRunner.Instance; }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            sprites ??= new SpriteCache(runner.Database, null);
            double now = runner.SimNow;
            var b = runner.Sim.ActiveBlessing();
            var v = runner.Sim.ActiveCombatBuff();
            bool any = b != null || v != null;
            if (pill.activeSelf != any) pill.SetActive(any);
            if (!any) return;
            if (blessing.activeSelf != (b != null)) blessing.SetActive(b != null);
            if (b != null)
            {
                var def = runner.Config.DragonBuff(b.kind);
                blessingIcon.sprite = sprites.Item(b.kind);
                ViewKit.SetText(blessingText, (def != null ? def.name : b.kind) + " " + Secs(b.until - now) + "s");
            }
            if (vigor.activeSelf != (v != null)) vigor.SetActive(v != null);
            if (v != null)
            {
                var vit = runner.Config.vitality;
                vigorIcon.sprite = sprites.Item(string.IsNullOrEmpty(vit.item) ? "vitality_pill" : vit.item);
                ViewKit.SetText(vigorText, (string.IsNullOrEmpty(vit.name) ? "Martial Vigor" : vit.name) + " " + Secs(v.until - now) + "s");
            }
        }

        static int Secs(double ms) => Mathf.Max(0, Mathf.CeilToInt((float)(ms / 1000.0)));
    }
}
