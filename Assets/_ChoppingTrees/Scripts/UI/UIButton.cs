using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Button with hover lift, press squash, a disabled look and click sounds.</summary>
    public class UIButton : Button
    {
        Image m_Image;
        Image m_Icon;
        Color m_Base;
        float m_Punch, m_Hover;
        ButtonStyle m_Style;

        public TextMeshProUGUI Label { get; private set; }
        public Image Background => m_Image;
        public Image IconImage => m_Icon;

        public void Init(Image img, TextMeshProUGUI label, Color baseColor, Image icon = null, ButtonStyle style = ButtonStyle.Ghost)
        {
            m_Image = img;
            Label = label;
            m_Icon = icon;
            m_Base = baseColor;
            m_Style = style;
            targetGraphic = img;
            transition = Transition.None;
            // Selectable may already have cross-faded its default "disabled" tint (grey, 50% alpha)
            // onto the graphic while the menu was built inactive; clear it.
            img.canvasRenderer.SetColor(Color.white);
            onClick.AddListener(() => { AudioManager.Ui("ui_click", 0.6f); m_Punch = 1f; });
        }

        public void SetColor(Color c) => m_Base = c;

        /// <summary>All visual feedback is done in Update; never let Selectable tint the graphic.</summary>
        protected override void DoStateTransition(SelectionState state, bool instant) { }

        public void SetStyle(ButtonStyle style)
        {
            m_Style = style;
            m_Base = UIFactory.StyleColor(style);
            Color fg = style == ButtonStyle.Primary ? UIStyle.AccentDark : UIStyle.Text;
            if (Label != null) Label.color = fg;
            if (m_Icon != null) m_Icon.color = fg;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (interactable) AudioManager.Ui("ui_hover", 0.25f);
        }

        void Update()
        {
            if (m_Image == null) return;
            float dt = Time.unscaledDeltaTime;
            bool hot = interactable && (currentSelectionState == SelectionState.Highlighted || currentSelectionState == SelectionState.Selected);
            m_Hover = Mathf.MoveTowards(m_Hover, hot ? 1f : 0f, dt * 8f);
            Color c = interactable ? m_Base : (m_Style == ButtonStyle.Tab ? m_Base : UIStyle.ButtonDisabled);
            if (m_Style == ButtonStyle.Tab && hot) c = new Color(1f, 1f, 1f, 0.06f + c.a);
            else c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.14f), m_Hover);
            if (interactable && IsPressed()) c = Color.Lerp(c, Color.black, 0.18f);
            m_Image.color = Color.Lerp(m_Image.color, c, 1f - Mathf.Exp(-20f * dt));
            float alpha = interactable ? 1f : 0.42f;
            if (Label != null) Label.alpha = alpha;
            if (m_Icon != null) { var ic = m_Icon.color; ic.a = alpha; m_Icon.color = ic; }
            m_Punch = Mathf.MoveTowards(m_Punch, 0f, dt * 6f);
            float scale = 1f + 0.025f * m_Hover - 0.05f * Mathf.Sin(m_Punch * Mathf.PI);
            transform.localScale = Vector3.one * scale;
        }
    }
}
