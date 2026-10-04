using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One link row of the lantern editor (ui.js link-row): status dot, "n.", source label, arrow,
    /// target label, remove button. Hovering the row shows the status tooltip text in the editor.
    /// </summary>
    public class LinkRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Serializable]
        public class Endpoint
        {
            public Image icon;
            public Image item;
            public TextMeshProUGUI text;
        }

        [SerializeField] Image dot;
        [SerializeField] TextMeshProUGUI indexText;
        [SerializeField] Endpoint source = new Endpoint();
        [SerializeField] Endpoint target = new Endpoint();
        [SerializeField] Button remove;

        LinkEditorView editor;

        public int Index { get; private set; }
        public string Tip { get; private set; }
        public Color DotColour => dot.color;
        public Endpoint Source => source;
        public Endpoint Target => target;
        public Button RemoveButton => remove;

        public void Init(LinkEditorView owner)
        {
            editor = owner;
            remove.onClick.RemoveAllListeners();
            remove.onClick.AddListener(() => editor.RemoveLink(Index));
        }

        public void SetIndex(int i)
        {
            Index = i;
            string s = (i + 1) + ".";
            if (indexText.text != s) indexText.text = s;
        }

        public void SetStatus(Color c, string tip)
        {
            dot.color = c;
            Tip = tip;
        }

        public void OnPointerEnter(PointerEventData e) { if (editor != null) editor.SetTip(Tip); }
        public void OnPointerExit(PointerEventData e) { if (editor != null) editor.SetTip(null); }

        public static void SetEndpoint(Endpoint e, Sprite icon, Sprite item, string text)
        {
            e.icon.sprite = icon;
            e.icon.enabled = icon != null;
            bool showItem = item != null;
            if (e.item.gameObject.activeSelf != showItem) e.item.gameObject.SetActive(showItem);
            if (showItem) e.item.sprite = item;
            if (e.text.text != text) e.text.text = text;
        }
    }
}
