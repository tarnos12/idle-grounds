using UnityEngine;

namespace IdleGrounds.Game
{
    public enum TextRole { Auto, Title, Body }

    /// <summary>
    /// Marks a text's role for the "Text font" setting (<see cref="UiFont"/>): Title (panel titles and headings) or Body
    /// (everything else, every number). Texts without this component are classified by <see cref="UiFont.RoleOf"/>
    /// (a uGUI object named "...Title..." at 21+ authored size, or any uGUI text at 24+, is a Title). <see cref="face"/>
    /// pins a text to one face whatever the setting (the font previews in Menu > Settings).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTextRole : MonoBehaviour
    {
        public TextRole role = TextRole.Auto;
        /// <summary>FontFace id (TextFonts.Faces) this text always uses; empty = follow the setting.</summary>
        public string face = "";

        public static UiTextRole Set(Component text, TextRole role, string face = "")
        {
            if (text == null) return null;
            if (!text.TryGetComponent<UiTextRole>(out var r)) r = text.gameObject.AddComponent<UiTextRole>();
            r.role = role; r.face = face ?? "";
            return r;
        }
    }
}
