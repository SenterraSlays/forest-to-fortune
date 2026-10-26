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
            var backdrop = UIFactory.Panel(Root, "Backdrop", new Color(0, 0, 0, 0.6f), false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var panel = UIFactory.Panel(Root, "Panel", UIStyle.PanelLight);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 0));
            panel.raycastTarget = true;
            UIFactory.VStack(panel, 18f, new RectOffset(36, 36, 30, 30));
            UIFactory.Fit(panel, false, true);
            m_Title = UIFactory.Text(panel.transform, "", 34, UIStyle.Text, true);
            m_Message = UIFactory.Text(panel.transform, "", 23, UIStyle.Muted);
            var row = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.HStack(row, 14f, null, TextAnchor.MiddleRight);
            var no = UIFactory.Button(row, "Cancel", Close, 24);
            no.Layout(180, 56);
            m_Yes = UIFactory.Button(row, "Confirm", () => { Close(); m_OnYes?.Invoke(); }, 24, UIStyle.Bad * 0.8f + new Color(0, 0, 0, 0.2f));
            m_Yes.Layout(200, 56);
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
