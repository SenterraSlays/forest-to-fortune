using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Achievements of the loaded save, one tab per group (LB / RB on a controller). Each card shows
    /// the goal, a progress bar and the reward; unlocked ones turn gold. The header counts how many
    /// are done. Opened from the pause menu.
    /// </summary>
    public class AchievementsMenu : UIScreen, ITabbedScreen
    {
        public static AchievementsMenu Instance { get; private set; }

        class Card
        {
            public AchievementDef def;
            public Image bezel, face, icon, glow, rewardIcon;
            public TextMeshProUGUI name, progress, reward, rewardCaption;
            public RectTransform bar;
            public Image barFill;
        }

        readonly List<UIButton> m_Tabs = new List<UIButton>();
        readonly List<RectTransform> m_Pages = new List<RectTransform>();
        readonly List<Card> m_Cards = new List<Card>();
        RectTransform m_TabLine;
        TextMeshProUGUI m_Count;
        RectTransform m_CountBar;
        int m_Tab;
        float m_Refresh;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Achievements", "trophy", new Vector2(1500, 960), out var body, "For this save only: every slot earns its own");
            UIFactory.TopTrim(window.rectTransform, UIStyle.Money);
            var header = window.transform.Find("Header");
            var close = UIFactory.Button(header, "", Close, 22, ButtonStyle.Ghost, "close");
            close.navigation = new Navigation { mode = Navigation.Mode.None }; // B closes
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));

            // Unlocked count with a progress bar.
            var total = UIFactory.Surface(header, "Total", UIStyle.Inset);
            total.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-104, 0), new Vector2(300, 52));
            UIFactory.HStack(total, 12f, new RectOffset(16, 18, 6, 6));
            UIFactory.Icon(total.transform, "trophy", UIStyle.Money, 24f);
            m_Count = UIFactory.Text(total.transform, "", 22, UIStyle.Money, true);
            m_Count.textWrappingMode = TextWrappingModes.NoWrap;
            var barBox = UIFactory.Rect("Bar", total.transform);
            barBox.Layout(-1, 8, 1);
            UIFactory.HStack(barBox, 0f);
            m_CountBar = UIFactory.Bar(barBox, 8, UIStyle.Money);

            // Tabs, one per group.
            var tabs = UIFactory.Rect("Tabs", body);
            tabs.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -14), new Vector2(1428, 50));
            UIFactory.HStack(tabs, 6f);
            for (int i = 0; i < Achievements.Groups.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(tabs, Achievements.Groups[i], () => ShowTab(index), 19, ButtonStyle.Tab, Achievements.GroupIcons[i]);
                b.Layout(-1, 46, 1);
                b.navigation = new Navigation { mode = Navigation.Mode.None }; // LB / RB on a controller
                m_Tabs.Add(b);
            }
            var line = UIFactory.Panel(body, "TabLine", new Color(1f, 0.95f, 0.82f, 0.08f), false);
            line.rectTransform.anchorMin = new Vector2(0, 1);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.sizeDelta = new Vector2(-72, 1.5f);
            line.rectTransform.anchoredPosition = new Vector2(0, -66);
            m_TabLine = UIFactory.Panel(body, "TabIndicator", UIStyle.Money).rectTransform;
            m_TabLine.GetComponent<Image>().sprite = UIStyle.RoundedSmall;
            m_TabLine.anchorMin = m_TabLine.anchorMax = new Vector2(0, 1);
            m_TabLine.pivot = new Vector2(0, 0.5f);
            m_TabLine.sizeDelta = new Vector2(200, 3);

            // One page of cards per group, two columns.
            foreach (string group in Achievements.Groups)
            {
                var page = UIFactory.Rect("Page_" + group, body);
                page.Stretch(36, 36, 86, 26);
                var g = page.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(707, 112);
                g.spacing = new Vector2(14, 12);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = 2;
                foreach (var a in Achievements.All)
                    if (a.group == group) m_Cards.Add(BuildCard(page, a));
                m_Pages.Add(page);
            }
            Game.Instance.PhaseChanged += _ => Close();
        }

        Card BuildCard(RectTransform page, AchievementDef a)
        {
            var c = new Card { def = a };
            var card = UIFactory.Surface(page, a.id, new Color(0.075f, 0.085f, 0.07f, 1f));
            UIFactory.HStack(card, 16f, new RectOffset(16, 18, 12, 12));
            c.glow = UIFactory.Panel(card.transform, "Glow", new Color(UIStyle.Money.r, UIStyle.Money.g, UIStyle.Money.b, 0f));
            c.glow.rectTransform.Stretch();
            c.glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var badge = UIFactory.Rect("Badge", card.transform);
            badge.Layout(76, 76);
            c.bezel = UIFactory.Panel(badge, "Bezel", UIStyle.Money, false);
            c.bezel.sprite = UIIcons.Bezel;
            c.bezel.rectTransform.Stretch();
            c.face = UIFactory.Panel(badge, "Face", UIStyle.Card, false);
            c.face.sprite = UIIcons.Face;
            c.face.rectTransform.Stretch(8, 8, 8, 8);
            c.icon = UIFactory.Icon(badge, a.icon, UIStyle.Text, 36f);
            c.icon.rectTransform.anchoredPosition = Vector2.zero;

            var mid = UIFactory.Rect("Text", card.transform);
            mid.Layout(-1, -1, 1);
            UIFactory.VStack(mid, 3f, null, TextAnchor.MiddleLeft);
            c.name = UIFactory.Text(mid, a.name, 21, UIStyle.Text, true);
            c.name.textWrappingMode = TextWrappingModes.NoWrap;
            c.name.overflowMode = TextOverflowModes.Ellipsis;
            var desc = UIFactory.Text(mid, a.description, 16, UIStyle.Muted);
            desc.textWrappingMode = TextWrappingModes.NoWrap;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            var row = UIFactory.Rect("Progress", mid);
            UIFactory.HStack(row, 10f);
            row.Layout(-1, 20);
            var barBox = UIFactory.Rect("Bar", row);
            barBox.Layout(-1, 7, 1);
            UIFactory.HStack(barBox, 0f);
            c.bar = UIFactory.Bar(barBox, 7, UIStyle.Money);
            c.barFill = c.bar.GetComponent<Image>();
            c.progress = UIFactory.Text(row, "", 15, UIStyle.Muted, true, TextAlignmentOptions.Right);
            c.progress.textWrappingMode = TextWrappingModes.NoWrap;
            c.progress.Layout(150, -1);

            // Reward: what it gives, and whether it has been collected.
            var reward = UIFactory.Panel(card.transform, "Reward", new Color(1f, 0.95f, 0.82f, 0.04f));
            reward.Layout(214, 82);
            UIFactory.VStack(reward, 2f, new RectOffset(14, 12, 10, 10), TextAnchor.MiddleLeft);
            var head = UIFactory.Rect("Head", reward.transform);
            UIFactory.HStack(head, 6f);
            c.rewardIcon = UIFactory.Icon(head, a.skillPoints > 0 ? "star" : a.money > 0 ? "coin" : "arrow_up", UIStyle.Muted, 15f);
            c.rewardCaption = UIFactory.Caption(head, "Reward", UIStyle.Dim, 12f);
            c.reward = UIFactory.Text(reward.transform, Achievements.RewardText(Game.Instance.Db, a), 16.5f, UIStyle.Text, true);
            c.reward.lineSpacing = -4f;
            return c;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            ShowTab(m_Tab, false);
        }

        void ShowTab(int index, bool sound = true)
        {
            m_Tab = (index + m_Pages.Count) % m_Pages.Count;
            for (int i = 0; i < m_Pages.Count; i++)
            {
                m_Pages[i].gameObject.SetActive(i == m_Tab);
                m_Tabs[i].Label.color = i == m_Tab ? UIStyle.Text : UIStyle.Muted;
            }
            Canvas.ForceUpdateCanvases();
            var active = (RectTransform)m_Tabs[m_Tab].transform;
            m_TabLine.sizeDelta = new Vector2(active.rect.width > 0 ? active.rect.width : 190, 3);
            m_TabLine.anchoredPosition = new Vector2(36 + active.anchoredPosition.x - active.rect.width * active.pivot.x, -66);
            if (sound) AudioManager.Ui("ui_click", 0.4f);
            Refresh();
        }

        void Refresh()
        {
            m_Refresh = 0.5f;
            var m = Game.Instance.Model;
            var got = m.State.Achievements;
            int done = 0;
            foreach (var a in Achievements.All) if (got.Contains(a.id)) done++;
            m_Count.text = $"{done} / {Achievements.All.Length}";
            UIFactory.SetBar(m_CountBar, done / (float)Achievements.All.Length);

            for (int i = 0; i < m_Tabs.Count; i++)
            {
                int n = 0, have = 0;
                foreach (var a in Achievements.All)
                    if (a.group == Achievements.Groups[i]) { n++; if (got.Contains(a.id)) have++; }
                m_Tabs[i].Label.text = have == n ? $"{Achievements.Groups[i]}  <color=#FFD466>{have}/{n}</color>" : $"{Achievements.Groups[i]}  <size=85%>{have}/{n}</size>";
            }

            foreach (var c in m_Cards)
            {
                if (c.def.group != Achievements.Groups[m_Tab]) continue;
                bool unlocked = got.Contains(c.def.id);
                double value = unlocked ? c.def.goal : c.def.Progress(m);
                float t = (float)System.Math.Min(1.0, c.def.goal <= 0 ? 1 : value / c.def.goal);
                UIFactory.SetBar(c.bar, t);
                c.barFill.color = unlocked ? UIStyle.Money : Color.Lerp(UIStyle.Accent, UIStyle.Money, t * 0.5f);
                c.progress.text = unlocked ? "<color=#FFD466>Unlocked</color>" : Achievements.ProgressText(c.def, value);
                c.bezel.color = unlocked ? UIStyle.Money : Color.Lerp(new Color(0.34f, 0.35f, 0.32f), UIStyle.Accent, t * 0.4f);
                c.face.color = unlocked ? Color.Lerp(new Color(0.13f, 0.12f, 0.08f), UIStyle.Money, 0.25f) : new Color(0.1f, 0.11f, 0.09f);
                c.icon.color = unlocked ? Color.Lerp(UIStyle.Money, Color.white, 0.4f) : UIStyle.Dim;
                c.name.color = unlocked ? UIStyle.Text : Color.Lerp(UIStyle.Text, UIStyle.Muted, 0.4f);
                c.glow.color = new Color(UIStyle.Money.r, UIStyle.Money.g, UIStyle.Money.b, unlocked ? 0.06f : 0f);
                c.rewardCaption.text = unlocked ? "Collected" : "Reward";
                c.rewardCaption.color = unlocked ? UIStyle.Good : UIStyle.Dim;
                c.rewardIcon.color = unlocked ? UIStyle.Good : c.def.skillPoints > 0 ? UIStyle.Skill : c.def.money > 0 ? UIStyle.Money : UIStyle.Accent;
                c.reward.color = unlocked ? UIStyle.Muted : UIStyle.Text;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            if (TopModal == this)
            {
                if (GameInput.TabLeftPressed) ShowTab(m_Tab - 1);
                else if (GameInput.TabRightPressed) ShowTab(m_Tab + 1);
            }
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh <= 0f) Refresh();
        }
    }
}
