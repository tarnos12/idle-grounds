using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Icons for the icon-only bottom bar and the menu (owner pick vp_bar, 2026-10-21). A delivered ui_* sprite wins
    /// (GameDatabase.ui); until the art exists, small pixel icons are drawn in code (12x12 texels + 1-texel ink ring,
    /// palette colours only, shown at 2x). These placeholders are on the art list: ui_sound, ui_sound_off, ui_motion,
    /// ui_motion_calm, ui_menu, ui_help, ui_reset.
    /// </summary>
    public static class UiIcons
    {
        public const string Sound = "ui_sound", SoundOff = "ui_sound_off", Motion = "ui_motion", MotionCalm = "ui_motion_calm",
            Menu = "ui_menu", Help = "ui_help", Reset = "ui_reset";

        /// <summary>On-screen size of a bar icon (canvas units): 14 texels x 2.</summary>
        public const float BarIconSize = 28f;

        /// <summary>Delivered art for <paramref name="key"/>, else the code-drawn placeholder (null when neither exists).</summary>
        public static Sprite Get(string key)
        {
            var db = GameRunner.Instance != null ? GameRunner.Instance.Database : null;
            var s = db != null ? db.UiSprite(key) : null;
            return s != null ? s : Placeholder(key);
        }

        /// <summary>True when <paramref name="key"/> still shows the code-drawn placeholder.</summary>
        public static bool IsPlaceholder(string key)
        {
            var db = GameRunner.Instance != null ? GameRunner.Instance.Database : null;
            return (db == null || db.UiSprite(key) == null) && Rows(key) != null;
        }

        static Color32 Ink(char ch)
        {
            switch (ch)
            {
                case 'w': return PixelSprites.P07;
                case 'r': return PixelSprites.P19;
                case 'j': return PixelSprites.P11;
                case 'g': return PixelSprites.P24;
                default: return PixelSprites.P07;
            }
        }

        static Sprite Placeholder(string key)
        {
            var rows = Rows(key);
            return rows == null ? null : PixelSprites.FromRows("uiicon_" + key, rows, Ink, true);
        }

        static string[] Rows(string key)
        {
            switch (key)
            {
                case Sound: return new[]
                {
                    "....w.......",
                    "...ww...w...",
                    "..www....w..",
                    "wwwww..w..w.",
                    "wwwww...w.w.",
                    "wwwww...w.w.",
                    "wwwww...w.w.",
                    "wwwww...w.w.",
                    "wwwww..w..w.",
                    "..www....w..",
                    "...ww...w...",
                    "....w.......",
                };
                case SoundOff: return new[]
                {
                    "....w.......",
                    "...ww.......",
                    "..www.......",
                    "wwwww.r...r.",
                    "wwwww..r.r..",
                    "wwwww...r...",
                    "wwwww..r.r..",
                    "wwwww.r...r.",
                    "wwwww.......",
                    "..www.......",
                    "...ww.......",
                    "....w.......",
                };
                case Motion: return new[]
                {
                    "............",
                    "......wwww..",
                    ".....wwwwww.",
                    "www..wwwwww.",
                    "....wwwwwwww",
                    "wwww.wwwwwww",
                    "....wwwwwwww",
                    "www..wwwwww.",
                    ".....wwwwww.",
                    "......wwww..",
                    "............",
                    "............",
                };
                case MotionCalm: return new[]
                {
                    "............",
                    "....wwww....",
                    "...wjjjjw...",
                    "..wjjjjjjw..",
                    "..wjjjjjjw..",
                    "..wjjjjjjw..",
                    "..wjjjjjjw..",
                    "...wjjjjw...",
                    "....wwww....",
                    "............",
                    "wwwwwwwwwwww",
                    "............",
                };
                case Menu: return new[]
                {
                    "............",
                    "wwwwwwwwwwww",
                    "wwwwwwwwwwww",
                    "............",
                    "............",
                    "wwwwwwwwwwww",
                    "wwwwwwwwwwww",
                    "............",
                    "............",
                    "wwwwwwwwwwww",
                    "wwwwwwwwwwww",
                    "............",
                };
                case Help: return new[]
                {
                    "...wwwwww...",
                    "..wwwwwwww..",
                    "..ww....ww..",
                    "........ww..",
                    ".......www..",
                    ".....www....",
                    ".....ww.....",
                    ".....ww.....",
                    "............",
                    ".....ww.....",
                    ".....ww.....",
                    "............",
                };
                case Reset: return new[]
                {
                    "....rrrr.r..",
                    "..rr....rr..",
                    ".r.....rrr..",
                    ".r..........",
                    "r...........",
                    "r...........",
                    "r...........",
                    "r..........r",
                    ".r........r.",
                    ".r........r.",
                    "..rr....rr..",
                    "....rrrr....",
                };
                default: return null;
            }
        }
    }
}
