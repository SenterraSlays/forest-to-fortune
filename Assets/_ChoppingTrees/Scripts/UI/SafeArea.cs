using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Keeps a full-screen rect inside the screen's safe area (clear of notches, camera
    /// cut-outs and rounded corners on phones). Does nothing on screens without one.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect m_Applied;
        Vector2Int m_Screen;

        void Update()
        {
            var safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == m_Applied && size == m_Screen) return;
            m_Applied = safe;
            m_Screen = size;
            var rt = (RectTransform)transform;
            if (size.x <= 0 || size.y <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rt.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
