using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Controller menu aids, shown only while a controller is in use and a menu is open:
    /// a bold, softly pulsing frame that glides to whatever is selected (round around skill tree
    /// nodes), and a hint bar along the bottom with the Select / Back / Switch tab buttons drawn
    /// in the chosen controller's icons. Sits above every screen.
    /// </summary>
    public class ControllerFocus : MonoBehaviour
    {
        RectTransform m_Root, m_Frame, m_Bar, m_Tabs;
        Image m_Ring, m_Glow, m_Circle, m_CircleGlow;
        Image m_SelectGlyph, m_BackGlyph, m_TabLeftGlyph, m_TabRightGlyph;
        CanvasGroup m_FrameGroup, m_BarGroup;
        Vector2 m_Pos, m_Size;
        GameObject m_Last;

        public void Create(RectTransform root)
        {
            m_Root = (RectTransform)transform;
            m_Root.SetParent(root, false);
            m_Root.Stretch();
            gameObject.layer = 5;

            m_Frame = UIFactory.Rect("Frame", m_Root);
            m_Frame.anchorMin = m_Frame.anchorMax = new Vector2(0.5f, 0.5f);
            m_FrameGroup = m_Frame.gameObject.AddComponent<CanvasGroup>();
            m_FrameGroup.blocksRaycasts = false;
            m_FrameGroup.interactable = false;
            m_Glow = Layer(m_Frame, "Glow", UIIcons.Shadow, true, -22f);
            m_Ring = Layer(m_Frame, "Ring", UIIcons.FocusRing, true, 0f);
            m_CircleGlow = Layer(m_Frame, "CircleGlow", UIIcons.Glow, false, -40f);
            m_Circle = Layer(m_Frame, "Circle", UIIcons.ThickRing, false, 0f);

            m_Bar = UIFactory.Surface(m_Root, "Hints", new Color(0.04f, 0.05f, 0.04f, 0.92f)).rectTransform;
            m_Bar.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), Vector2.zero);
            UIFactory.HStack(m_Bar, 22f, new RectOffset(18, 20, 8, 8), TextAnchor.MiddleCenter);
            UIFactory.Fit(m_Bar, true, true);
            m_BarGroup = m_Bar.gameObject.AddComponent<CanvasGroup>();
            m_BarGroup.blocksRaycasts = false;
            m_SelectGlyph = Item(m_Bar, "Select");
            m_BackGlyph = Item(m_Bar, "Back");
            var tabs = UIFactory.Rect("Tabs", m_Bar);
            UIFactory.HStack(tabs, 6f);
            m_TabLeftGlyph = Glyph(tabs);
            m_TabRightGlyph = Glyph(tabs);
            UIFactory.Text(tabs, "Switch tab", 18, UIStyle.Text).textWrappingMode = TextWrappingModes.NoWrap;
            m_Tabs = tabs;
            m_FrameGroup.alpha = m_BarGroup.alpha = 0f;
        }

        static Image Layer(RectTransform parent, string name, Sprite sprite, bool sliced, float inset)
        {
            var img = UIFactory.Panel(parent, name, UIStyle.Accent, false);
            img.sprite = sprite;
            if (sliced) img.type = Image.Type.Sliced;
            img.rectTransform.Stretch(inset, inset, inset, inset);
            return img;
        }

        static Image Glyph(RectTransform parent)
        {
            var g = UIFactory.Panel(parent, "Glyph", Color.white, false);
            g.preserveAspect = true;
            g.Layout(34, 34);
            return g;
        }

        static Image Item(RectTransform bar, string text)
        {
            var item = UIFactory.Rect(text, bar);
            UIFactory.HStack(item, 6f);
            var g = Glyph(item);
            UIFactory.Text(item, text, 18, UIStyle.Text).textWrappingMode = TextWrappingModes.NoWrap;
            return g;
        }

        void LateUpdate()
        {
            transform.SetAsLastSibling();
            float dt = Time.unscaledDeltaTime;
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            var top = UIScreen.TopModal;
            bool menus = GameInput.UsingGamepad && top != null;
            bool frame = menus && sel != null && sel.activeInHierarchy && sel.transform is RectTransform;

            m_BarGroup.alpha = Mathf.MoveTowards(m_BarGroup.alpha, menus ? 1f : 0f, dt * 8f);
            if (menus)
            {
                m_SelectGlyph.sprite = GameInput.Glyph("select");
                m_BackGlyph.sprite = GameInput.Glyph("back");
                bool tabs = top is ITabbedScreen;
                if (m_Tabs.gameObject.activeSelf != tabs) m_Tabs.gameObject.SetActive(tabs);
                m_TabLeftGlyph.sprite = GameInput.Glyph("tabLeft");
                m_TabRightGlyph.sprite = GameInput.Glyph("tabRight");
            }

            m_FrameGroup.alpha = Mathf.MoveTowards(m_FrameGroup.alpha, frame ? 1f : 0f, dt * 10f);
            if (!frame) { m_Last = null; return; }

            // Target rectangle of the selection in this overlay's space.
            var rt = (RectTransform)sel.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 a = m_Root.InverseTransformPoint(corners[0]), b = m_Root.InverseTransformPoint(corners[2]);
            bool round = sel.GetComponent<SkillNode>() != null;
            Vector2 size = (b - a) + Vector2.one * (round ? 18f : 14f);
            if (round) size = Vector2.one * Mathf.Max(size.x, size.y);
            Vector2 pos = (a + b) * 0.5f;
            if (sel != m_Last && m_Last == null) { m_Pos = pos; m_Size = size * 1.25f; }
            m_Last = sel;
            float k = 1f - Mathf.Exp(-22f * dt);
            m_Pos = Vector2.Lerp(m_Pos, pos, k);
            m_Size = Vector2.Lerp(m_Size, size, k);
            m_Frame.anchoredPosition = m_Pos;
            m_Frame.sizeDelta = m_Size;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Color c = Color.Lerp(UIStyle.Accent, Color.white, 0.25f * pulse);
            m_Ring.enabled = m_Glow.enabled = !round;
            m_Circle.enabled = m_CircleGlow.enabled = round;
            m_Ring.color = m_Circle.color = c;
            m_Glow.color = m_CircleGlow.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.35f + 0.25f * pulse);
        }
    }

    /// <summary>A screen with tabs that LB / RB switch (the hint bar shows them).</summary>
    public interface ITabbedScreen { }
}
