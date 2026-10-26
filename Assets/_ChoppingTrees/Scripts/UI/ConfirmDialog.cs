using System;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Yes/no confirmation for destructive actions.</summary>
    public class ConfirmDialog : UIScreen
    {
        public static ConfirmDialog Instance { get; private set; }

        TextMeshProUGUI m_Title, m_Message;
        UIButton m_Yes;
        Action m_OnYes;

        protected override float FadeSpeed => 12f;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", new Color(0, 0, 0, 0.66f), false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var panel = UIFactory.Surface(Root, "Panel", UIStyle.Panel);
            panel.raycastTarget = true;
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 0));
            UIFactory.VStack(panel, 16f, new RectOffset(36, 36, 32, 32));
            UIFactory.Fit(panel, false, true);
            var head = UIFactory.Rect("Head", panel.transform);
            UIFactory.HStack(head, 14f);
            var badge = UIFactory.Panel(head, "Badge", UIStyle.Bad);
            badge.sprite = UIStyle.Circle;
            badge.Layout(46, 46);
            var ic = UIFactory.Icon(badge.transform, "refresh", UIStyle.Text, 26f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
            ic.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            m_Title = UIFactory.Text(head, "", 32, UIStyle.Text, true);
            m_Title.Layout(-1, -1, 1);
            m_Message = UIFactory.Text(panel.transform, "", 22, UIStyle.Muted);
            var row = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.HStack(row, 14f, new RectOffset(0, 0, 10, 0), TextAnchor.MiddleRight);
            UIFactory.Button(row, "Cancel", Close, 22, ButtonStyle.Ghost).Layout(180, 56);
            m_Yes = UIFactory.Button(row, "Confirm", () => { Close(); m_OnYes?.Invoke(); }, 22, ButtonStyle.Danger);
            m_Yes.Layout(230, 56);
        }

        protected override void OnOpened()
        {
            // Sits above everything, including the shadow of the window that opened it.
            transform.SetAsLastSibling();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Ask(string title, string message, string confirm, Action onYes)
        {
            var d = Instance;
            if (d == null) { onYes?.Invoke(); return; }
            d.m_Title.text = title;
            d.m_Message.text = message;
            d.m_Yes.Label.text = confirm;
            d.m_OnYes = onYes;
            d.Open();
        }
    }
}
