using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>"While you were away" summary of offline production, shown once after loading.</summary>
    public class OfflinePopup : UIScreen
    {
        TextMeshProUGUI m_Away, m_Note;
        RectTransform m_List;

        protected override void Build()
        {
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var panel = UIFactory.Surface(Root, "Panel", UIStyle.Panel);
            panel.raycastTarget = true;
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 0));
            UIFactory.VStack(panel, 14f, new RectOffset(36, 36, 32, 32));
            UIFactory.Fit(panel, false, true);

            var head = UIFactory.Rect("Head", panel.transform);
            UIFactory.HStack(head, 14f);
            var badge = UIFactory.Panel(head, "Badge", UIStyle.CategoryColor(UpgradeCategory.Forest));
            badge.sprite = UIStyle.Circle;
            badge.Layout(52, 52);
            var ic = UIFactory.Icon(badge.transform, "moon", UIStyle.AccentDark, 30f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            var titles = UIFactory.Rect("Titles", head);
            UIFactory.VStack(titles, 0f);
            titles.Layout(-1, -1, 1);
            UIFactory.Text(titles, "Welcome back!", 34, UIStyle.Text, true);
            m_Away = UIFactory.Text(titles, "", 19, UIStyle.Muted);

            UIFactory.Caption(panel.transform, "Your machines made", UIStyle.Dim, 14f);
            m_List = UIFactory.Rect("List", panel.transform);
            UIFactory.VStack(m_List, 8f);
            m_Note = UIFactory.Text(panel.transform, "", 17, UIStyle.Muted);
            UIFactory.Button(panel.transform, "Collect", Close, 26, ButtonStyle.Primary, "check").Layout(-1, 64);
            Game.Instance.PhaseChanged += p => { if (p == GamePhase.Playing) TryShow(); };
        }

        void TryShow()
        {
            var g = Game.Instance;
            var r = g.PendingOffline;
            if (r == null) return;
            g.ConsumeOfflineReport();
            foreach (Transform c in m_List) Destroy(c.gameObject);
            m_Away.text = $"You were away for {NumberUtil.Duration(r.awaySeconds)}";
            foreach (var kv in r.gained)
            {
                var res = g.Db.Resource(kv.Key);
                if (res == null || kv.Value <= 0.5) continue;
                var row = UIFactory.Surface(m_List, res.id, UIStyle.Card);
                UIFactory.HStack(row, 12f, new RectOffset(16, 18, 10, 10));
                UIFactory.Icon(row.transform, res.icon, res.color, 30f);
                UIFactory.Text(row.transform, res.displayName, 22, UIStyle.Text).Layout(-1, -1, 1);
                var v = UIFactory.Text(row.transform, "+" + NumberUtil.Format(kv.Value), 28, UIStyle.Accent, true, TextAlignmentOptions.Right);
                v.textWrappingMode = TextWrappingModes.NoWrap;
            }
            m_Note.text = r.Capped
                ? $"Production is limited to {NumberUtil.Duration(r.creditedSeconds)} while away. The <color=#6BCC9E>Night Shift</color> upgrade raises the limit."
                : "Sell it at the Lumber Market.";
            Open();
            AudioManager.Ui("purchase", 0.7f);
        }
    }
}
