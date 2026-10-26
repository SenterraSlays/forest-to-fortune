using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Title screen over the drifting forest camera.</summary>
    public class MainMenu : UIScreen
    {
        UIButton m_Continue, m_New;
        TextMeshProUGUI m_Stats;

        protected override void Build()
        {
            var shade = UIFactory.Panel(Root, "Shade", new Color(0.04f, 0.05f, 0.03f, 0.55f), false);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(0.48f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.raycastTarget = true;

            var col = UIFactory.Rect("Column", Root);
            col.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(620, 820));
            UIFactory.VStack(col, 16f, null, TextAnchor.MiddleLeft);

            var small = UIFactory.Text(col, "A GAME ABOUT", 30, UIStyle.Accent, true);
            small.characterSpacing = 12f;
            var title = UIFactory.Text(col, "Chopping\nTrees", 112, UIStyle.Text, true);
            title.lineSpacing = -18f;
            title.Layout(-1, 250);
            UIFactory.Text(col, "Chop. Sell. Upgrade. Automate. Expand.", 24, UIStyle.Muted).Layout(-1, 56);

            m_Continue = UIFactory.Button(col, "Continue", () => Game.Instance.StartPlaying(), 30, new Color(0.34f, 0.47f, 0.24f));
            m_Continue.Layout(380, 70);
            m_New = UIFactory.Button(col, "New Game", NewGame, 28);
            m_New.Layout(380, 64);
            UIFactory.Button(col, "Settings", () => SettingsMenu.Instance.Open(), 28).Layout(380, 64);
            UIFactory.Button(col, "Quit", () => Game.Instance.Quit(), 28).Layout(380, 64);
            m_Stats = UIFactory.Text(col, "", 20, UIStyle.Muted);
            m_Stats.Layout(-1, 80);

            var credit = UIFactory.Text(Root, "Made in Unity  -  models built in Blender  -  sounds synthesised", 17, new Color(1, 1, 1, 0.4f), false, TextAlignmentOptions.Right);
            credit.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20), new Vector2(900, 30));

            Game.Instance.PhaseChanged += p => { if (p == GamePhase.MainMenu) Open(); else Close(); };
        }

        protected override void OnOpened()
        {
            var g = Game.Instance;
            bool hasProgress = g.Model.State.HasPlayed;
            m_Continue.gameObject.SetActive(hasProgress);
            m_Continue.Label.text = "Continue";
            m_New.Label.text = hasProgress ? "New Game" : "Start Chopping";
            var s = g.Model.State;
            m_Stats.text = hasProgress
                ? $"{NumberUtil.Money(s.Money)}  -  {NumberUtil.Format(s.TreesFelled)} trees felled  -  {NumberUtil.Duration(s.PlaySeconds)} played"
                : "";
            if (g.LoadProblem != null) Toasts.Show(g.LoadProblem, Toasts.Kind.Warning);
        }

        void NewGame()
        {
            if (!Game.Instance.Model.State.HasPlayed)
            {
                Game.Instance.StartPlaying();
                return;
            }
            ConfirmDialog.Ask("Start a new game?", "This erases all your progress: money, upgrades, machines and forests. Settings are kept.", "Erase and start",
                () => Game.Instance.NewGame());
        }
    }
}
