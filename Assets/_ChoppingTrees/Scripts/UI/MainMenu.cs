using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Title screen over the drifting forest camera.</summary>
    public class MainMenu : UIScreen
    {
        UIButton m_Continue, m_New;
        GameObject m_Stats;
        TextMeshProUGUI m_StatMoney, m_StatTrees, m_StatTime;
        RectTransform m_Column;
        float m_Intro;

        protected override void Build()
        {
            // Dark gradient from the left so the title reads over the forest.
            var shade = UIFactory.Panel(Root, "Shade", new Color(0.035f, 0.05f, 0.035f, 0.92f), false);
            shade.sprite = UIIcons.GradientH;
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.raycastTarget = true;
            var bottom = UIFactory.Panel(Root, "Bottom", new Color(0.03f, 0.04f, 0.03f, 0.7f), false);
            bottom.sprite = UIIcons.GradientV;
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.22f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;

            m_Column = UIFactory.Rect("Column", Root);
            m_Column.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 10), new Vector2(640, 900));
            UIFactory.VStack(m_Column, 14f, null, TextAnchor.MiddleLeft);

            var brand = UIFactory.Rect("Brand", m_Column);
            UIFactory.HStack(brand, 14f);
            var badge = UIFactory.Panel(brand, "Badge", UIStyle.Accent);
            badge.sprite = UIStyle.Circle;
            badge.Layout(54, 54);
            var axe = UIFactory.Icon(badge.transform, "axe", UIStyle.AccentDark, 34f);
            axe.rectTransform.anchoredPosition = Vector2.zero;
            axe.GetComponent<LayoutElement>().ignoreLayout = true;
            UIFactory.Caption(brand, "A Game About", UIStyle.Accent, 26f).characterSpacing = 14f;

            var title = UIFactory.Text(m_Column, "Chopping\nTrees", 128, UIStyle.Text, true);
            title.lineSpacing = -22f;
            title.Layout(-1, 270);
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.45f);
            titleShadow.effectDistance = new Vector2(0, -6);
            var tagline = UIFactory.Text(m_Column, "Chop. Sell. Upgrade. Automate. Expand.", 24, UIStyle.Muted);
            tagline.Layout(-1, 52);

            m_Continue = UIFactory.Button(m_Column, "Continue", () => Game.Instance.StartPlaying(), 28, ButtonStyle.Primary, "play");
            m_Continue.Layout(420, 70);
            m_New = UIFactory.Button(m_Column, "New Game", NewGame, 24, ButtonStyle.Ghost, "refresh");
            m_New.Layout(420, 60);
            UIFactory.Button(m_Column, "Settings", () => SettingsMenu.Instance.Open(), 24, ButtonStyle.Ghost, "gear").Layout(420, 60);
            UIFactory.Button(m_Column, "Quit", () => Game.Instance.Quit(), 24, ButtonStyle.Ghost, "power").Layout(420, 60);

            var stats = UIFactory.Rect("Stats", m_Column);
            UIFactory.HStack(stats, 12f);
            stats.Layout(-1, 84);
            m_StatMoney = Stat(stats, "coin", UIStyle.Money, "Money");
            m_StatTrees = Stat(stats, "pine", UIStyle.Good, "Trees felled");
            m_StatTime = Stat(stats, "clock", UIStyle.CategoryColor(UpgradeCategory.Forest), "Played");
            m_Stats = stats.gameObject;

            var credit = UIFactory.Text(Root, "Models built in Blender  •  sounds synthesised  •  v1.1", 16, new Color(1, 1, 1, 0.45f), false, TextAlignmentOptions.Right);
            credit.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 22), new Vector2(900, 30));

            Game.Instance.PhaseChanged += p => { if (p == GamePhase.MainMenu) Open(); else Close(); };
        }

        static TextMeshProUGUI Stat(Transform parent, string icon, Color color, string label)
        {
            var card = UIFactory.Surface(parent, label, new Color(0.08f, 0.09f, 0.07f, 0.8f));
            card.Layout(-1, 78, 1);
            UIFactory.HStack(card, 10f, new RectOffset(14, 12, 10, 10));
            UIFactory.Icon(card.transform, icon, color, 28f);
            var col = UIFactory.Rect("Col", card.transform);
            UIFactory.VStack(col, 0f);
            col.Layout(-1, -1, 1);
            var v = UIFactory.Text(col, "-", 24, UIStyle.Text, true);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            var l = UIFactory.Text(col, label, 14, UIStyle.Muted);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            return v;
        }

        protected override void OnOpened()
        {
            var g = Game.Instance;
            bool hasProgress = g.Model.State.HasPlayed;
            m_Continue.gameObject.SetActive(hasProgress);
            m_New.Label.text = hasProgress ? "New Game" : "Start Chopping";
            m_New.SetStyle(hasProgress ? ButtonStyle.Ghost : ButtonStyle.Primary);
            if (m_New.IconImage != null) m_New.IconImage.sprite = UIIcons.Get(hasProgress ? "refresh" : "play");
            var s = g.Model.State;
            m_Stats.SetActive(hasProgress);
            m_StatMoney.text = NumberUtil.Money(s.Money);
            m_StatTrees.text = NumberUtil.Format(s.TreesFelled);
            m_StatTime.text = NumberUtil.Duration(s.PlaySeconds);
            if (g.LoadProblem != null) Toasts.Show(g.LoadProblem, Toasts.Kind.Warning);
            m_Intro = 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            // Gentle slide-in of the title column.
            m_Intro = Mathf.MoveTowards(m_Intro, 1f, Time.unscaledDeltaTime * 1.6f);
            float e = 1f - Mathf.Pow(1f - m_Intro, 3f);
            m_Column.anchoredPosition = new Vector2(Mathf.Lerp(70f, 130f, e), 10f);
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
