using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A rectangle outline made of four tiled edge strips (solid or dashed), inside the rect whose
    /// top-left is this transform's origin (x right, y down = −y). Used for building borders,
    /// hover/selection outlines, the placement preview and the fuel rack.
    /// </summary>
    public class EdgeFrame : MonoBehaviour
    {
        [SerializeField] SpriteRenderer top, bottom, left, right;
        [SerializeField] Sprite solid, dashed;

        float lw = -1, lh, lt; bool ldash;

        public void Set(float w, float h, float thickness, Color c, bool dash)
        {
            SetColour(c);
            if (Mathf.Approximately(w, lw) && Mathf.Approximately(h, lh) && Mathf.Approximately(thickness, lt) && dash == ldash) return;
            lw = w; lh = h; lt = thickness; ldash = dash;
            var s = dash ? dashed : solid;
            Edge(top, s, new Vector3(w * 0.5f, -thickness * 0.5f), w, thickness, 0f);
            Edge(bottom, s, new Vector3(w * 0.5f, -h + thickness * 0.5f), w, thickness, 0f);
            Edge(left, s, new Vector3(thickness * 0.5f, -h * 0.5f), h, thickness, 90f);
            Edge(right, s, new Vector3(w - thickness * 0.5f, -h * 0.5f), h, thickness, 90f);
        }

        public void SetColour(Color c)
        {
            if (top == null || top.color == c) return;
            top.color = bottom.color = left.color = right.color = c;
        }

        static void Edge(SpriteRenderer sr, Sprite s, Vector3 pos, float len, float th, float rot)
        {
            if (sr == null) return;
            sr.sprite = s;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.transform.localPosition = pos;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            sr.transform.localScale = Vector3.one;
            sr.size = new Vector2(Mathf.Max(0.001f, len), Mathf.Max(0.001f, th));
        }
    }
}
