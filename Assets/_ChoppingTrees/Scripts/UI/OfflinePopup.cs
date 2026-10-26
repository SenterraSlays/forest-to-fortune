using System.Text;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>"While you were away" summary of offline production, shown once after loading.</summary>
    public class OfflinePopup : UIScreen
    {
        TextMeshProUGUI m_Body;

        protected override void Build()
        {
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var panel = UIFactory.Panel(Root, "Panel", UIStyle.PanelLight);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 0));
            panel.raycastTarget = true;
            UIFactory.VStack(panel, 16f, new RectOffset(40, 40, 32, 32));
            UIFactory.Fit(panel, false, true);
            UIFactory.Text(panel.transform, "Welcome back!", 40, UIStyle.Text, true);
            m_Body = UIFactory.Text(panel.transform, "", 24, UIStyle.Muted);
            UIFactory.Button(panel.transform, "Collect", Close, 28, new Color(0.34f, 0.47f, 0.24f)).Layout(-1, 64);
            Game.Instance.PhaseChanged += p => { if (p == GamePhase.Playing) TryShow(); };
        }

        void TryShow()
        {
            var g = Game.Instance;
            var r = g.PendingOffline;
            if (r == null) return;
            g.ConsumeOfflineReport();
            var sb = new StringBuilder();
            sb.Append($"You were away for <color=#F5EFE0>{NumberUtil.Duration(r.awaySeconds)}</color>. Your machines kept working:\n\n");
            foreach (var kv in r.gained)
            {
                var res = g.Db.Resource(kv.Key);
                if (res == null || kv.Value <= 0.5) continue;
                sb.Append($"<size=120%><color=#FBBD52>+{NumberUtil.Format(kv.Value)}</color></size>  {res.displayName}\n");
            }
            if (r.Capped)
                sb.Append($"\n<size=85%>Production is limited to {NumberUtil.Duration(r.creditedSeconds)} while away. The Night Shift upgrade raises the limit.</size>");
            m_Body.text = sb.ToString();
            Open();
            AudioManager.Ui("purchase", 0.7f);
        }
    }
}
