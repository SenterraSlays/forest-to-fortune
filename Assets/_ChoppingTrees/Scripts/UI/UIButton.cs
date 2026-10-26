using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Button with hover/press feedback, a disabled look and click sounds.</summary>
    public class UIButton : Button
    {
        Image m_Image;
        Color m_Base;
        float m_Punch;

        public TextMeshProUGUI Label { get; private set; }
        public Image Background => m_Image;

        public void Init(Image img, TextMeshProUGUI label, Color baseColor)
        {
            m_Image = img;
            Label = label;
            m_Base = baseColor;
            targetGraphic = img;
            transition = Transition.None;
            onClick.AddListener(() => { AudioManager.Ui("ui_click", 0.6f); m_Punch = 1f; });
        }

        public void SetColor(Color c) => m_Base = c;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (interactable) AudioManager.Ui("ui_hover", 0.25f);
        }

        void Update()
        {
            if (m_Image == null) return;
            Color c = interactable ? m_Base : UIStyle.ButtonDisabled;
            if (interactable && IsHot()) c = Color.Lerp(c, Color.white, 0.12f);
            if (interactable && IsPressed()) c = Color.Lerp(c, Color.black, 0.15f);
            m_Image.color = Color.Lerp(m_Image.color, c, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            if (Label != null) Label.alpha = interactable ? 1f : 0.45f;
            m_Punch = Mathf.MoveTowards(m_Punch, 0f, Time.unscaledDeltaTime * 6f);
            transform.localScale = Vector3.one * (1f - 0.04f * Mathf.Sin(m_Punch * Mathf.PI));
        }

        bool IsHot() => currentSelectionState == SelectionState.Highlighted || currentSelectionState == SelectionState.Selected;
    }
}
