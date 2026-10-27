using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Shrinks a fixed-size window (and its shadow) to fit its parent when the screen is
    /// smaller than the window was designed for, such as a phone in landscape or a big interface
    /// scale. Never enlarges it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FitToParent : MonoBehaviour
    {
        [SerializeField] float m_Margin = 16f;
        [SerializeField] RectTransform m_Follower;

        Vector2 m_LastParent, m_LastSize;

        public void Configure(RectTransform follower, float margin)
        {
            m_Follower = follower;
            m_Margin = margin;
        }

        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            Vector2 p = parent.rect.size, size = rt.rect.size;
            if (p == m_LastParent && size == m_LastSize) return;
            m_LastParent = p;
            m_LastSize = size;
            if (size.x <= 0f || size.y <= 0f) return;
            float s = Mathf.Min(1f, (p.x - m_Margin * 2f) / size.x, (p.y - m_Margin * 2f) / size.y);
            s = Mathf.Max(0.3f, s);
            rt.localScale = Vector3.one * s;
            if (m_Follower != null) m_Follower.localScale = Vector3.one * s;
        }
    }
}
