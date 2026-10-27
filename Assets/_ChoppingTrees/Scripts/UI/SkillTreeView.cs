using UnityEngine;
using UnityEngine.EventSystems;

namespace ChoppingTrees
{
    /// <summary>
    /// Makes the skill tree movable: drag with any mouse button to pan, scroll to zoom towards the
    /// cursor. The tree is bigger than the window: the default view is close to full size around
    /// <see cref="Home"/> (the trunk), zooming all the way out (zoom 1) shows the whole tree. The
    /// view eases to its target and is clamped so the tree can't be lost.
    /// Sits on the tree area; nodes still get their clicks (a drag that starts on a node pans
    /// instead of clicking it).
    /// </summary>
    public class SkillTreeView : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public const float MaxZoom = 2.8f;
        /// <summary>Scale of the default view (about full size).</summary>
        const float HomeScale = 0.92f;

        /// <summary>Content point (origin bottom-left) the default view is centred on.</summary>
        public Vector2 Home { get; set; }

        RectTransform m_Viewport, m_Content;
        Vector2 m_Size;
        float m_Fit = 1f, m_Zoom = 1f, m_TargetZoom = 1f;
        Vector2 m_Pan, m_TargetPan;
        bool m_Ready;

        public float Zoom => m_TargetZoom;

        public void Init(RectTransform viewport, RectTransform content)
        {
            m_Viewport = viewport;
            m_Content = content;
        }

        /// <summary>Sets the tree's design size and the scale that fits it in the viewport.</summary>
        public void Configure(Vector2 contentSize, float fitScale)
        {
            m_Size = contentSize;
            m_Fit = fitScale;
            m_Content.sizeDelta = contentSize;
            if (!m_Ready) ResetView(true);
            else
            {
                m_TargetPan = Clamp(m_TargetPan, m_TargetZoom);
                Apply(true);
            }
            m_Ready = true;
        }

        /// <summary>Back to the default view: about full size, centred on <see cref="Home"/>.</summary>
        public void ResetView(bool instant = false)
        {
            m_TargetZoom = Mathf.Clamp(HomeScale / m_Fit, 1f, MaxZoom);
            float s = m_Fit * m_TargetZoom;
            m_TargetPan = Clamp(-(Home - m_Size * 0.5f) * s, m_TargetZoom, 0f);
            if (instant)
            {
                m_Zoom = m_TargetZoom;
                m_Pan = m_TargetPan;
                Apply(true);
            }
        }

        /// <summary>Zooms by a factor about a point in viewport space (centre = 0,0).</summary>
        public void ZoomBy(float factor, Vector2 pivot)
        {
            float z = Mathf.Clamp(m_TargetZoom * factor, 1f, MaxZoom);
            float sOld = m_Fit * m_TargetZoom, sNew = m_Fit * z;
            Vector2 contentPoint = (pivot - m_TargetPan) / sOld;
            m_TargetZoom = z;
            m_TargetPan = Clamp(pivot - contentPoint * sNew, z);
        }

        Vector2 Clamp(Vector2 pan, float zoom, float margin = 60f)
        {
            Vector2 view = m_Viewport.rect.size * 0.5f;
            Vector2 half = m_Size * (m_Fit * zoom) * 0.5f;
            // Bigger than the view: its edges may come in up to a margin. Smaller: stay inside.
            float lx = half.x > view.x ? half.x - view.x + margin : view.x - half.x;
            float ly = half.y > view.y ? half.y - view.y + margin : view.y - half.y;
            return new Vector2(Mathf.Clamp(pan.x, -lx, lx), Mathf.Clamp(pan.y, -ly, ly));
        }

        void Apply(bool instant)
        {
            float k = instant ? 1f : 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            m_Zoom = Mathf.Lerp(m_Zoom, m_TargetZoom, k);
            m_Pan = Vector2.Lerp(m_Pan, m_TargetPan, k);
            m_Content.localScale = Vector3.one * (m_Fit * m_Zoom);
            m_Content.anchoredPosition = m_Pan;
        }

        void Update()
        {
            if (!m_Ready) return;
            if (GameInput.UsingGamepad)
            {
                // Right stick pans, RT zooms in, LT zooms out.
                Vector2 stick = GameInput.PanStick;
                if (stick.sqrMagnitude > 0f) m_TargetPan = Clamp(m_TargetPan - stick * (900f * Time.unscaledDeltaTime), m_TargetZoom);
                float z = GameInput.ZoomAxis;
                if (Mathf.Abs(z) > 0.05f) ZoomBy(1f + z * 1.6f * Time.unscaledDeltaTime, Vector2.zero);
            }
            Apply(false);
        }

        /// <summary>Pans so a point of the tree (content coordinates, origin bottom-left) is comfortably in view.</summary>
        public void EnsureVisible(Vector2 contentPoint, float margin = 140f)
        {
            float s = m_Fit * m_TargetZoom;
            Vector2 p = m_TargetPan + (contentPoint - m_Size * 0.5f) * s;
            Vector2 half = m_Viewport.rect.size * 0.5f - Vector2.one * margin;
            Vector2 shift = Vector2.zero;
            if (p.x > half.x) shift.x = half.x - p.x;
            else if (p.x < -half.x) shift.x = -half.x - p.x;
            if (p.y > half.y) shift.y = half.y - p.y;
            else if (p.y < -half.y) shift.y = -half.y - p.y;
            if (shift != Vector2.zero) m_TargetPan = Clamp(m_TargetPan + shift, m_TargetZoom);
        }

        Vector2 LocalPoint(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Viewport, e.position, e.pressEventCamera, out var p);
            return p - m_Viewport.rect.center;
        }

        float CanvasScale => m_Viewport.lossyScale.x > 1e-5f ? m_Viewport.lossyScale.x : 1f;

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            m_TargetPan = Clamp(m_TargetPan + e.delta / CanvasScale, m_TargetZoom);
            m_Pan = m_TargetPan; // follow the mouse directly while dragging
            Apply(true);
        }

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 1e-3f) return;
            ZoomBy(e.scrollDelta.y > 0 ? 1.15f : 1f / 1.15f, LocalPoint(e));
        }
    }
}
