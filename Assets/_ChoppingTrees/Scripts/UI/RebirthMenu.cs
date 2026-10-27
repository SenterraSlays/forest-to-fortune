using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Rebirth screen with two tabs.
    /// <b>Rebirth</b>: an animated emblem with the current bonus, a 1 / 5 / 10 / Max picker, a
    /// summary of what you pay and get (earnings multiplier, skill points) and what resets or
    /// stays. Buying asks for confirmation, then <see cref="Game.Rebirth"/> restarts the run.
    /// <b>Legacy Perks</b>: cards for the perks bought with skill points, kept through rebirths.
    /// Opened from the Workbench header and the pause menu.
    /// </summary>
    public class RebirthMenu : UIScreen, ITabbedScreen
    {
        public static RebirthMenu Instance { get; private set; }

        /// <summary>Quantity choices; 0 means "as many as you can afford".</summary>
        static readonly int[] Choices = { 1, 5, 10, 0 };

        class PerkCard
        {
            public UpgradeDef def;
            public Image bezel, face, icon, glow;
            public TextMeshProUGUI level, effect;
            public readonly List<Image> pips = new List<Image>();
            public UIButton buy;
        }

        RectTransform m_RebirthTab, m_LegacyTab;
        UIButton m_TabRebirth, m_TabLegacy;
        RectTransform m_TabLine;
        TextMeshProUGUI m_Money, m_Points;
        TextMeshProUGUI m_Count, m_Bonus, m_Title, m_Cost, m_EarnFrom, m_EarnTo, m_PointsGain, m_Status, m_LegacyPoints;
        RectTransform m_Ring;
        Image m_EmblemGlow;
        readonly UIButton[] m_ChoiceButtons = new UIButton[Choices.Length];
        readonly List<PerkCard> m_Cards = new List<PerkCard>();
        UIButton m_Go;
        int m_Choice;
        bool m_Legacy;
        float m_Refresh;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Rebirth", "rebirth", new Vector2(1240, 820), out var body, "Start over stronger: bigger earnings and skill points");
            UIFactory.TopTrim(window.rectTransform, UIStyle.Skill);
            var header = window.transform.Find("Header");
            m_Money = UIFactory.Chip(header, "coin", UIStyle.Money, new Vector2(-104, 0), 250);
            m_Points = UIFactory.Chip(header, "star", UIStyle.Skill, new Vector2(-370, 0), 150);
            var close = UIFactory.Button(header, "", Close, 22, ButtonStyle.Ghost, "close");
            close.navigation = new Navigation { mode = Navigation.Mode.None }; // B closes
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));

            // Tabs.
            var tabs = UIFactory.Rect("Tabs", body);
            tabs.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -14), new Vector2(520, 50));
            UIFactory.HStack(tabs, 6f);
            m_TabRebirth = UIFactory.Button(tabs, "Rebirth", () => ShowTab(false), 21, ButtonStyle.Tab, "rebirth");
            m_TabRebirth.Layout(200, 46);
            m_TabLegacy = UIFactory.Button(tabs, "Legacy Perks", () => ShowTab(true), 21, ButtonStyle.Tab, "star");
            m_TabLegacy.Layout(250, 46);
            // A controller switches tabs with LB / RB.
            m_TabRebirth.navigation = m_TabLegacy.navigation = new Navigation { mode = Navigation.Mode.None };
            var line = UIFactory.Panel(body, "TabLine", new Color(1f, 0.95f, 0.82f, 0.08f), false);
            line.rectTransform.anchorMin = new Vector2(0, 1);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.sizeDelta = new Vector2(-72, 1.5f);
            line.rectTransform.anchoredPosition = new Vector2(0, -66);
            m_TabLine = UIFactory.Panel(body, "TabIndicator", UIStyle.Skill).rectTransform;
            m_TabLine.GetComponent<Image>().sprite = UIStyle.RoundedSmall;
            m_TabLine.anchorMin = m_TabLine.anchorMax = new Vector2(0, 1);
            m_TabLine.pivot = new Vector2(0, 0.5f);
            m_TabLine.sizeDelta = new Vector2(200, 3);

            m_RebirthTab = UIFactory.Rect("RebirthTab", body);
            m_RebirthTab.Stretch(36, 36, 84, 30);
            BuildRebirthTab(m_RebirthTab);
            m_LegacyTab = UIFactory.Rect("LegacyTab", body);
            m_LegacyTab.Stretch(36, 36, 84, 30);
            BuildLegacyTab(m_LegacyTab);

            Game.Instance.PhaseChanged += _ => Close();
        }

        // ------------------------------------------------------------------ rebirth tab

        void BuildRebirthTab(RectTransform tab)
        {
            UIFactory.HStack(tab, 26f, null, TextAnchor.UpperLeft);
            tab.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            // Left: emblem, bonus, what resets and what stays.
            var left = UIFactory.Panel(tab, "Left", new Color(0.075f, 0.085f, 0.07f, 1f));
            UIFactory.Border(left.transform, UIStyle.Border);
            left.Layout(500, -1);
            UIFactory.VStack(left, 10f, new RectOffset(28, 28, 22, 24), TextAnchor.UpperCenter);
            var banner = UIFactory.Panel(left.transform, "Banner", new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0.16f));
            banner.sprite = UIIcons.GradientV;
            banner.type = Image.Type.Simple;
            banner.rectTransform.anchorMin = new Vector2(0, 1);
            banner.rectTransform.anchorMax = new Vector2(1, 1);
            banner.rectTransform.pivot = new Vector2(0.5f, 0.5f); // flipped about its centre so it stays inside
            banner.rectTransform.sizeDelta = new Vector2(-4, 260);
            banner.rectTransform.anchoredPosition = new Vector2(0, -2 - 130);
            banner.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            banner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var emblem = UIFactory.Rect("Emblem", left.transform);
            emblem.Layout(150, 150);
            m_EmblemGlow = UIFactory.Panel(emblem, "Glow", UIStyle.Skill, false);
            m_EmblemGlow.sprite = UIIcons.Glow;
            m_EmblemGlow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300));
            m_Ring = UIFactory.Panel(emblem, "Ring", new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0.7f), false).rectTransform;
            m_Ring.GetComponent<Image>().sprite = UIIcons.ArcRing;
            var ringImg = m_Ring.GetComponent<Image>();
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillAmount = 0.72f;
            m_Ring.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
            var bezel = UIFactory.Panel(emblem, "Bezel", UIStyle.Skill, false);
            bezel.sprite = UIIcons.Bezel;
            bezel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 132));
            var face = UIFactory.Panel(emblem, "Face", new Color(0.16f, 0.13f, 0.21f), false);
            face.sprite = UIIcons.Face;
            face.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106, 106));
            UIFactory.Icon(emblem, "rebirth", new Color(0.92f, 0.86f, 1f), 62f).rectTransform.anchoredPosition = Vector2.zero;

            m_Count = UIFactory.Caption(left.transform, "", UIStyle.Skill, 14f);
            m_Count.alignment = TextAlignmentOptions.Center;
            m_Bonus = UIFactory.Text(left.transform, "", 46, UIStyle.Money, true, TextAlignmentOptions.Center);
            m_Bonus.textWrappingMode = TextWrappingModes.NoWrap;
            var sub = UIFactory.Text(left.transform, "on everything you earn", 17, UIStyle.Muted, false, TextAlignmentOptions.Center);
            sub.Layout(-1, 22);

            var lists = UIFactory.Rect("Lists", left.transform);
            UIFactory.HStack(lists, 18f, new RectOffset(0, 0, 12, 0), TextAnchor.UpperLeft);
            lists.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            List(lists, "Resets", UIStyle.Bad, new[] { ("coin", "Money"), ("axe", "Axes, backpack, all upgrades"), ("saw", "Machines and forests"), ("log", "Yard and backpack wood") });
            List(lists, "You keep", UIStyle.Good, new[] { ("rebirth", "Rebirth bonus"), ("star", "Skill points and perks"), ("chart", "Lifetime stats"), ("gear", "Settings") });

            // Right: choose how many, see what you pay and get, rebirth.
            var right = UIFactory.Rect("Right", tab);
            right.Layout(-1, -1, 1);
            UIFactory.VStack(right, 14f);

            UIFactory.Caption(right, "How many at once", UIStyle.Dim, 13f);
            var choices = UIFactory.Panel(right, "Choices", new Color(0.055f, 0.065f, 0.052f, 1f));
            UIFactory.Border(choices.transform, UIStyle.Border);
            UIFactory.HStack(choices, 6f, new RectOffset(6, 6, 6, 6));
            for (int i = 0; i < Choices.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(choices.transform, Choices[i] == 0 ? "Max" : "x" + Choices[i], () => Choose(index), 22, ButtonStyle.Ghost);
                b.Layout(-1, 52, 1);
                m_ChoiceButtons[i] = b;
            }

            var card = UIFactory.Panel(right, "Summary", new Color(0.06f, 0.07f, 0.058f, 1f));
            UIFactory.Border(card.transform, new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0.25f));
            UIFactory.VStack(card, 10f, new RectOffset(26, 26, 20, 22));
            m_Title = UIFactory.Text(card.transform, "", 26, UIStyle.Text, true);
            var costRow = UIFactory.Rect("Cost", card.transform);
            UIFactory.HStack(costRow, 10f);
            UIFactory.Icon(costRow, "coin", UIStyle.Money, 34f);
            m_Cost = UIFactory.Text(costRow, "", 42, UIStyle.Money, true);
            m_Cost.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Caption(card.transform, "You get", UIStyle.Dim, 13f);
            var gains = UIFactory.Rect("Gains", card.transform);
            UIFactory.HStack(gains, 12f);
            gains.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var earn = Gain(gains, "coin", UIStyle.Money, "Earnings");
            m_EarnFrom = UIFactory.Text(earn, "", 20, UIStyle.Muted, true);
            m_EarnFrom.textWrappingMode = TextWrappingModes.NoWrap;
            m_EarnTo = UIFactory.Text(earn, "", 30, UIStyle.Money, true);
            m_EarnTo.textWrappingMode = TextWrappingModes.NoWrap;
            var pts = Gain(gains, "star", UIStyle.Skill, "Skill points");
            m_PointsGain = UIFactory.Text(pts, "", 30, UIStyle.Skill, true);
            m_PointsGain.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Text(pts, "for Legacy perks", 15, UIStyle.Muted);
            m_Status = UIFactory.Text(card.transform, "", 17, UIStyle.Muted);

            var spacer = UIFactory.Rect("Spacer", right);
            spacer.Layout(-1, -1, -1, 1);
            var buttons = UIFactory.Rect("Buttons", right);
            UIFactory.HStack(buttons, 14f, null, TextAnchor.MiddleRight);
            UIFactory.Button(buttons, "Cancel", Close, 22, ButtonStyle.Ghost).Layout(170, 64);
            m_Go = UIFactory.Button(buttons, "Rebirth", Confirm, 26, ButtonStyle.Primary, "rebirth");
            m_Go.Layout(-1, 64, 1);
        }

        static RectTransform Gain(RectTransform parent, string icon, Color color, string label)
        {
            var box = UIFactory.Panel(parent, label, new Color(color.r, color.g, color.b, 0.07f));
            box.Layout(-1, -1, 1);
            UIFactory.VStack(box, 2f, new RectOffset(16, 16, 12, 14));
            var head = UIFactory.Rect("Head", box.transform);
            UIFactory.HStack(head, 7f);
            UIFactory.Icon(head, icon, color, 16f);
            UIFactory.Caption(head, label, UIStyle.Muted, 12f);
            return box.rectTransform;
        }

        static void List(RectTransform parent, string title, Color color, (string icon, string text)[] rows)
        {
            var box = UIFactory.Rect(title, parent);
            box.Layout(-1, -1, 1);
            UIFactory.VStack(box, 8f);
            UIFactory.Caption(box, title, color, 13f);
            foreach (var (icon, text) in rows)
            {
                var row = UIFactory.Rect("Row", box);
                UIFactory.HStack(row, 8f);
                UIFactory.Icon(row, icon, new Color(color.r, color.g, color.b, 0.85f), 16f);
                var t = UIFactory.Text(row, text, 16, UIStyle.Muted);
                t.Layout(-1, -1, 1);
            }
        }

        // ------------------------------------------------------------------ legacy tab

        void BuildLegacyTab(RectTransform tab)
        {
            UIFactory.VStack(tab, 14f);
            var intro = UIFactory.Rect("Intro", tab);
            UIFactory.HStack(intro, 12f);
            var text = UIFactory.Text(intro, "Legacy perks are bought with skill points (one per rebirth) and are <b>never reset</b>. Head starts apply right away and again at the start of every run.", 17, UIStyle.Muted);
            text.Layout(-1, -1, 1);
            m_LegacyPoints = UIFactory.Pill(intro, "", new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0.18f), UIStyle.Skill, 20f);

            var grid = UIFactory.Rect("Grid", tab);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            // Two rows of five cards fill the tab.
            g.cellSize = new Vector2(222, 262);
            g.spacing = new Vector2(14, 14);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            var perks = new List<UpgradeDef>();
            foreach (var u in Game.Instance.Db.upgrades) if (u != null && u.legacy) perks.Add(u);
            perks.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            foreach (var u in perks) m_Cards.Add(Card(grid, u));
        }

        PerkCard Card(RectTransform grid, UpgradeDef u)
        {
            var c = new PerkCard { def = u };
            var card = UIFactory.Surface(grid, u.id, new Color(0.075f, 0.085f, 0.07f, 1f));
            UIFactory.VStack(card, 6f, new RectOffset(14, 14, 12, 12));
            c.glow = UIFactory.Panel(card.transform, "Glow", new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0f));
            c.glow.rectTransform.Stretch();
            c.glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 10f);
            var badge = UIFactory.Rect("Badge", head);
            badge.Layout(48, 48);
            c.bezel = UIFactory.Panel(badge, "Bezel", UIStyle.Skill, false);
            c.bezel.sprite = UIIcons.Bezel;
            c.bezel.rectTransform.Stretch();
            c.face = UIFactory.Panel(badge, "Face", new Color(0.16f, 0.13f, 0.21f), false);
            c.face.sprite = UIIcons.Face;
            c.face.rectTransform.Stretch(6, 6, 6, 6);
            c.icon = UIFactory.Icon(badge, u.icon, UIStyle.Text, 24f);
            c.icon.rectTransform.anchoredPosition = Vector2.zero;
            var titles = UIFactory.Rect("Titles", head);
            UIFactory.VStack(titles, 0f);
            titles.Layout(-1, -1, 1);
            var name = UIFactory.Text(titles, u.displayName, 17, UIStyle.Text, true);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            c.level = UIFactory.Text(titles, "", 14, UIStyle.Muted, true);

            var pips = UIFactory.Rect("Pips", card.transform);
            UIFactory.HStack(pips, 3f);
            for (int i = 0; i < u.maxLevel; i++)
            {
                var pip = UIFactory.Panel(pips, "Pip", UIStyle.Inset);
                pip.sprite = UIStyle.RoundedSmall;
                pip.Layout(Mathf.Min(22f, 190f / u.maxLevel - 3f), 6);
                c.pips.Add(pip);
            }
            c.effect = UIFactory.Text(card.transform, "", 14.5f, UIStyle.Text);
            c.effect.lineSpacing = 2f;
            c.effect.Layout(-1, -1, -1, 1);
            c.buy = UIFactory.Button(card.transform, "Unlock", () => BuyPerk(c), 17, ButtonStyle.Ghost, "star");
            c.buy.Layout(-1, 40);
            return c;
        }

        void BuyPerk(PerkCard c)
        {
            var m = Game.Instance.Model;
            if (m.TryBuy(c.def, out var why))
            {
                AudioManager.Ui("unlock", 0.8f);
                Toasts.Show($"{c.def.displayName} level {m.Upgrades.Level(c.def)}", Toasts.Kind.Success);
            }
            else
            {
                AudioManager.Ui("error", 0.6f);
                Toasts.Show(why, Toasts.Kind.Warning);
            }
            Refresh();
        }

        // ------------------------------------------------------------------ behaviour

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Opens straight on the Legacy Perks tab.</summary>
        public void OpenLegacy()
        {
            Open();
            ShowTab(true);
            Focus(DefaultSelection());
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            ShowTab(false, false);
            Choose(0, false);
        }

        void ShowTab(bool legacy, bool sound = true)
        {
            m_Legacy = legacy;
            m_RebirthTab.gameObject.SetActive(!legacy);
            m_LegacyTab.gameObject.SetActive(legacy);
            m_TabRebirth.Label.color = legacy ? UIStyle.Muted : UIStyle.Text;
            m_TabLegacy.Label.color = legacy ? UIStyle.Text : UIStyle.Muted;
            var active = (RectTransform)(legacy ? m_TabLegacy : m_TabRebirth).transform;
            m_TabLine.sizeDelta = new Vector2(active.rect.width > 0 ? active.rect.width : 200, 3);
            m_TabLine.anchoredPosition = new Vector2(36 + (legacy ? 206 : 0), -66);
            if (sound) AudioManager.Ui("ui_click", 0.4f);
            Refresh();
        }

        void Choose(int index, bool sound = true)
        {
            m_Choice = index;
            for (int i = 0; i < m_ChoiceButtons.Length; i++) m_ChoiceButtons[i].SetStyle(i == index ? ButtonStyle.Primary : ButtonStyle.Ghost);
            if (sound) AudioManager.Ui("ui_click", 0.4f);
            Refresh();
        }

        /// <summary>The chosen quantity ("Max" is what the money pays for, at least 1).</summary>
        int Count(GameModel m) => Choices[m_Choice] == 0 ? Mathf.Max(1, m.MaxRebirths()) : Choices[m_Choice];

        void Refresh()
        {
            m_Refresh = 0.25f;
            var m = Game.Instance.Model;
            var cfg = m.Db.config;
            m_Money.text = NumberUtil.Money(m.State.Money);
            m_Points.text = m.State.SkillPoints + " SP";
            if (m_Legacy) RefreshLegacy(m);
            else RefreshRebirth(m, cfg);
        }

        void RefreshRebirth(GameModel m, GameConfig cfg)
        {
            int done = m.State.Rebirths, n = Count(m);
            double cost = m.RebirthCost(n);
            bool afford = m.State.Money + 1e-9 >= cost;
            m_Count.text = done == 0 ? "No rebirths yet" : done == 1 ? "1 rebirth" : $"{done:N0} rebirths";
            m_Bonus.text = $"x{Rebirth.Multiplier(cfg, done):0.0#}";
            m_Title.text = n > 1 ? $"Rebirth {n} times" : "Rebirth once";
            m_Cost.text = NumberUtil.Money(cost);
            m_EarnFrom.text = $"x{Rebirth.Multiplier(cfg, done):0.0#}  ->";
            m_EarnTo.text = $"x{Rebirth.Multiplier(cfg, done + n):0.0#}";
            m_PointsGain.text = $"+{n * cfg.rebirthSkillPoints} SP";
            int max = m.MaxRebirths();
            m_Status.text = afford
                ? $"<color=#86CC61>You can afford this.</color>  You have {NumberUtil.Money(m.State.Money)}" + (max > n ? $" (enough for {max})" : "")
                : $"<color=#E8705F>Need {NumberUtil.Money(cost - m.State.Money)} more.</color>  You have {NumberUtil.Money(m.State.Money)}. Next rebirth: {NumberUtil.Money(m.RebirthCost(1))}";
            m_Go.interactable = afford;
            m_Go.Label.text = n > 1 ? $"Rebirth x{n}" : "Rebirth";
        }

        void RefreshLegacy(GameModel m)
        {
            var up = m.Upgrades;
            m_LegacyPoints.text = $"{m.State.SkillPoints} skill points";
            foreach (var c in m_Cards)
            {
                int lv = up.Level(c.def);
                bool maxed = c.def.IsMaxed(lv);
                bool afford = !maxed && up.CanAfford(c.def);
                c.level.text = maxed ? "MAXED" : $"Level {lv} / {c.def.maxLevel}";
                c.level.color = maxed ? UIStyle.Money : UIStyle.Muted;
                for (int i = 0; i < c.pips.Count; i++) c.pips[i].color = i < lv ? (maxed ? UIStyle.Money : UIStyle.Skill) : new Color(1f, 0.95f, 0.82f, 0.1f);
                c.effect.text = c.def.description + "\n<size=95%>" + UpgradeText.Effect(m, c.def) + "</size>";
                c.bezel.color = maxed ? UIStyle.Money : lv > 0 ? UIStyle.Skill : Color.Lerp(UIStyle.Skill, new Color(0.4f, 0.4f, 0.4f), 0.5f);
                c.icon.color = lv > 0 || afford ? UIStyle.Text : UIStyle.Muted;
                c.glow.color = new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, afford ? 0.08f : 0f);
                c.buy.interactable = afford;
                c.buy.SetStyle(afford ? ButtonStyle.Primary : ButtonStyle.Ghost);
                c.buy.Label.text = maxed ? "Maxed" : $"{(lv > 0 ? "Upgrade" : "Unlock")}  ·  {up.SkillPointCost(c.def)} SP";
            }
        }

        void Confirm()
        {
            var m = Game.Instance.Model;
            int n = Count(m);
            double cost = m.RebirthCost(n);
            string times = n > 1 ? $"{n} times" : "once";
            ConfirmDialog.Ask($"Rebirth {times}?",
                $"You pay {NumberUtil.Money(cost)} and start over: money, upgrades, machines, forests and all wood reset. " +
                $"Everything you earn afterwards is worth x{Rebirth.Multiplier(m.Db.config, m.State.Rebirths + n):0.0#}, and you get {n * m.Db.config.rebirthSkillPoints} skill points.",
                "Rebirth", () =>
                {
                    if (!Game.Instance.Rebirth(n, out var why))
                    {
                        AudioManager.Ui("error", 0.6f);
                        Toasts.Show(why, Toasts.Kind.Warning);
                    }
                });
        }

        /// <summary>A controller starts on the first control of the open tab.</summary>
        public override GameObject DefaultSelection() => FirstSelectable(m_Legacy ? m_LegacyTab : m_RebirthTab) ?? base.DefaultSelection();

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            if (TopModal == this && (GameInput.TabLeftPressed || GameInput.TabRightPressed))
            {
                ShowTab(!m_Legacy);
                Focus(DefaultSelection());
            }
            float t = Time.unscaledTime;
            if (m_Ring != null) m_Ring.localRotation = Quaternion.Euler(0, 0, -t * 25f);
            if (m_EmblemGlow != null) m_EmblemGlow.color = new Color(UIStyle.Skill.r, UIStyle.Skill.g, UIStyle.Skill.b, 0.22f + 0.08f * Mathf.Sin(t * 2f));
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh <= 0f) Refresh();
        }
    }
}
