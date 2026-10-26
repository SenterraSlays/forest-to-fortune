using UnityEngine;
using UnityEngine.EventSystems;

namespace ChoppingTrees
{
    /// <summary>
    /// Makes the skill tree movable: drag with any mouse button to pan, scroll to zoom towards the
    /// cursor. Zoom 1 (the default) shows the whole tree. The view eases to its target and is
    /// clamped so the tree can't be lost.
    /// Sits on the tree area; nodes still get their clicks (a drag that starts on a node pans
    /// instead of clicking it).
    /// </summary>
    public class SkillTreeView : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        public const float MaxZoom = 2.2f;

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

        /// <summary>Back to the whole tree, centred.</summary>
        public void ResetView(bool instant = false)
        {
            m_TargetZoom = 1f;
            m_TargetPan = Vector2.zero;
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
            if (m_Ready) Apply(false);
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
