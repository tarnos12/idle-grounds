using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Phone / tablet facts shared by the touch layer (docs/guides/touch-controls.md): is this a touch device, its
    /// density, safe-area insets, the CanvasScaler reference height, and a UI hit test that works for any touch.
    /// Tools (screenshots, tests) can pretend to be a phone with <see cref="Force"/>.
    /// </summary>
    public static class MobileUi
    {
        /// <summary>Logical UI units per physical inch on a phone (desktop 1080p ~ 92). 200 puts a 20-unit label at ~2.5 mm.</summary>
        public const float LogicalUnitsPerInch = 200f;
        public const float RefHeightMin = 520f, RefHeightMax = 760f;
        /// <summary>Minimum finger target (Android guideline): 48 dp = 7.6 mm.</summary>
        public const float MinTargetDp = 48f;

        static bool? forced;
        static float forcedDpi;
        static Vector2Int forcedScreen;
        static bool hasForcedInsets;
        static Vector4 forcedInsets;      // left, bottom, right, top (screen px)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { forced = null; forcedDpi = 0; forcedScreen = default; hasForcedInsets = false; }

        public static bool IsMobile => forced ?? Application.isMobilePlatform;

        /// <summary>Pretend (or stop pretending) to be a phone: dpi, screen size in px and safe-area insets (left, bottom, right, top px).</summary>
        public static void Force(bool mobile, float dpi = 0f, Vector2Int screen = default, Vector4? insets = null)
        {
            forced = mobile; forcedDpi = dpi; forcedScreen = screen;
            hasForcedInsets = insets.HasValue; forcedInsets = insets ?? default;
            if (MobileLayer.Instance != null) MobileLayer.Instance.Refresh();
        }

        public static void ClearForce() { forced = null; forcedDpi = 0; forcedScreen = default; hasForcedInsets = false; if (MobileLayer.Instance != null) MobileLayer.Instance.Refresh(); }

        public static float Dpi => forcedDpi > 0f ? forcedDpi : (Screen.dpi > 0f ? Screen.dpi : 160f);
        public static int ScreenW => forcedScreen.x > 0 ? forcedScreen.x : Screen.width;
        public static int ScreenH => forcedScreen.y > 0 ? forcedScreen.y : Screen.height;

        public static float DpToPx(float dp) => dp * Dpi / 160f;

        /// <summary>CanvasScaler reference height (match = height): the UI shows ~200 units per physical inch on a phone, 520..760.</summary>
        public static float RefHeight()
        {
            float inches = ScreenH / Dpi;
            return Mathf.Clamp(inches * LogicalUnitsPerInch, RefHeightMin, RefHeightMax);
        }

        /// <summary>Safe-area insets in screen px: left, bottom, right, top (notch, rounded corners, gesture bar).</summary>
        public static Vector4 SafeInsets()
        {
            if (hasForcedInsets) return forcedInsets;
            var sa = Screen.safeArea;
            if (Screen.width <= 0) return Vector4.zero;
            return new Vector4(Mathf.Max(0f, sa.xMin), Mathf.Max(0f, sa.yMin), Mathf.Max(0f, Screen.width - sa.xMax), Mathf.Max(0f, Screen.height - sa.yMax));
        }

        static PointerEventData ped;
        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        /// <summary>True when a uGUI graphic that blocks raycasts is under this screen point (the touch belongs to the UI).</summary>
        public static bool IsOverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (ped == null || ped.currentInputModule != es.currentInputModule) ped = new PointerEventData(es);
            ped.position = screen;
            hits.Clear();
            es.RaycastAll(ped, hits);
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].module is GraphicRaycaster && hits[i].gameObject != null) return true;
            return false;
        }
    }
}
