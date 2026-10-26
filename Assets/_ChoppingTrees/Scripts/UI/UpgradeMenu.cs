using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// The workbench menu: category tabs, one card per upgrade with name, description, level,
    /// effect, cost and a buy button that is disabled (with the reason) when unavailable.
    /// Everything refreshes immediately after a purchase and a few times a second otherwise.
    /// </summary>
    public class UpgradeMenu : UIScreen
    {
        public static UpgradeMenu Instance { get; private set; }

        class Card
        {
            public UpgradeDef def;
            public Image bg;
            public TextMeshProUGUI title, level, effect, status;
            public UIButton buy;
            public float flash;
            public float shake;
        }

        class Tab
        {
            public UpgradeCategory category;
            public UIButton button;
            public TextMeshProUGUI badge;
        }

        static readonly UpgradeCategory[] s_Order = { UpgradeCategory.Axe, UpgradeCategory.Automation, UpgradeCategory.Economy, UpgradeCategory.Forest };
        static readonly string[] s_TabNames = { "Axe", "Automation", "Economy", "Forest" };

        UpgradeCategory m_Category = UpgradeCategory.Axe;
        readonly List<Card> m_Cards = new List<Card>();
        readonly List<Tab> m_Tabs = new List<Tab>();
        RectTransform m_List;
        TextMeshProUGUI m_Money, m_Summary;
        ScrollRect m_Scroll;
        float m_Refresh;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;

            var panel = UIFactory.Panel(Root, "Panel", UIStyle.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 780));
            panel.raycastTarget = true;

            // Header
            var title = UIFactory.Text(panel.transform, "Workbench", 44, UIStyle.Text, true);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -26), new Vector2(500, 56));
            m_Money = UIFactory.Text(panel.transform, "$0", 36, UIStyle.Money, true, TextAlignmentOptions.Right);
            m_Money.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -30), new Vector2(420, 50));
            var close = UIFactory.Button(panel.transform, "X", Close, 26);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -26), new Vector2(56, 56));

            // Tabs
            var tabs = UIFactory.Rect("Tabs", panel.transform);
            tabs.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -104), new Vector2(230, 400));
            UIFactory.VStack(tabs, 10f);
            for (int i = 0; i < s_Order.Length; i++)
            {
                var cat = s_Order[i];
                var b = UIFactory.Button(tabs, s_TabNames[i], () => SelectTab(cat), 26);
                b.Layout(-1, 62);
                b.Label.alignment = TextAlignmentOptions.Left;
                b.Label.rectTransform.Stretch(22, 40, 4, 4);
                var badge = UIFactory.Text(b.transform, "", 20, UIStyle.Money, true, TextAlignmentOptions.Right);
                badge.rectTransform.Stretch(10, 18, 4, 4);
                m_Tabs.Add(new Tab { category = cat, button = b, badge = badge });
            }
            m_Summary = UIFactory.Text(panel.transform, "", 19, UIStyle.Muted);
            m_Summary.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 30), new Vector2(230, 280));
            m_Summary.alignment = TextAlignmentOptions.BottomLeft;

            // Card list
            var view = UIFactory.Panel(panel.transform, "View", new Color(0, 0, 0, 0.12f));
            view.rectTransform.Stretch(290, 30, 104, 30);
            view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            m_List = UIFactory.Rect("List", view.transform);
            m_List.anchorMin = new Vector2(0, 1);
            m_List.anchorMax = new Vector2(1, 1);
            m_List.pivot = new Vector2(0.5f, 1);
            m_List.sizeDelta = Vector2.zero;
            UIFactory.VStack(m_List, 12f, new RectOffset(14, 14, 14, 14));
            UIFactory.Fit(m_List, false, true);
            m_Scroll = view.gameObject.AddComponent<ScrollRect>();
            m_Scroll.content = m_List;
            m_Scroll.horizontal = false;
            m_Scroll.movementType = ScrollRect.MovementType.Clamped;
            m_Scroll.scrollSensitivity = 40f;

            Game.Instance.Model.Purchased += _ => { if (IsOpen) RefreshAll(); };
            Game.Instance.PhaseChanged += _ => Close();
            SelectTab(UpgradeCategory.Axe);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            RefreshAll();
        }

        void SelectTab(UpgradeCategory cat)
        {
            m_Category = cat;
            foreach (Transform c in m_List) Destroy(c.gameObject);
            m_Cards.Clear();
            var db = Game.Instance.Db;
            foreach (var u in db.upgrades.Where(x => x != null && x.category == cat).OrderBy(x => x.sortOrder))
                m_Cards.Add(MakeCard(u));
            m_Scroll.verticalNormalizedPosition = 1f;
            RefreshAll();
        }

        Card MakeCard(UpgradeDef u)
        {
            var bg = UIFactory.Panel(m_List, u.id, UIStyle.Card);
            UIFactory.HStack(bg, 18f, new RectOffset(22, 18, 16, 16));
            bg.Layout(-1, -1, 1, -1, 120);

            var left = UIFactory.Rect("Info", bg.transform);
            UIFactory.VStack(left, 4f);
            var leftLayout = left.gameObject.AddComponent<LayoutElement>();
            leftLayout.flexibleWidth = 1f;
            leftLayout.preferredWidth = 1f; // let the text wrap instead of pushing the buy column
            leftLayout.minWidth = 0f;

            var head = UIFactory.Rect("Head", left);
            UIFactory.HStack(head, 12f);
            var title = UIFactory.Text(head, u.displayName, 28, UIStyle.Text, true);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            var level = UIFactory.Text(head, "", 20, UIStyle.Accent, true);
            level.textWrappingMode = TextWrappingModes.NoWrap;
            var desc = UIFactory.Text(left, u.description, 20, UIStyle.Muted);
            var effect = UIFactory.Text(left, "", 21, UIStyle.Text);

            var right = UIFactory.Rect("Buy", bg.transform);
            UIFactory.VStack(right, 6f, null, TextAnchor.MiddleCenter);
            var rightLayout = right.gameObject.AddComponent<LayoutElement>();
            rightLayout.minWidth = rightLayout.preferredWidth = 230f;
            rightLayout.flexibleWidth = 0f;
            var card = new Card { def = u, bg = bg, title = title, level = level, effect = effect };
            card.buy = UIFactory.Button(right, "Buy", () => Buy(card), 24, UIStyle.Good * 0.75f + new Color(0, 0, 0, 0.25f));
            card.buy.Layout(-1, 58);
            card.status = UIFactory.Text(right, "", 17, UIStyle.Muted, false, TextAlignmentOptions.Center);
            return card;
        }

        void Buy(Card c)
        {
            var model = Game.Instance.Model;
            if (model.TryBuy(c.def, out string why))
            {
                c.flash = 1f;
                AudioManager.Ui("purchase", 0.9f);
                string name = c.def.LevelName(model.Upgrades.Level(c.def)) ?? c.def.displayName;
                Toasts.Show("Bought " + name, Toasts.Kind.Success);
            }
            else
            {
                c.shake = 1f;
                AudioManager.Ui("error", 0.7f);
                Toasts.Show(why, Toasts.Kind.Warning);
            }
            RefreshAll();
        }

        void RefreshAll()
        {
            m_Refresh = 0.2f;
            var model = Game.Instance.Model;
            var up = model.Upgrades;
            m_Money.text = NumberUtil.Money(model.State.Money);
            foreach (var c in m_Cards)
            {
                var u = c.def;
                int lv = up.Level(u);
                bool maxed = u.IsMaxed(lv);
                string missing = up.MissingRequirement(u);
                string levelName = u.LevelName(lv);
                c.level.text = u.maxLevel == 1 ? (lv > 0 ? "OWNED" : "") : maxed ? $"MAX  {lv}/{u.maxLevel}" : $"Lv {lv}/{u.maxLevel}";
                if (levelName != null && u.effect == UpgradeEffect.AxeTier) c.level.text = levelName;
                c.effect.text = UpgradeText.Effect(model, u);
                if (maxed)
                {
                    c.buy.interactable = false;
                    c.buy.Label.text = u.maxLevel == 1 ? "Owned" : "Maxed";
                    c.status.text = "";
                }
                else
                {
                    double cost = up.NextCost(u);
                    c.buy.Label.text = NumberUtil.Money(cost);
                    c.buy.interactable = missing == null && model.State.Money + 1e-9 >= cost;
                    if (missing != null) c.status.text = $"<color=#E8705F>{missing}</color>";
                    else if (model.State.Money < cost) c.status.text = $"Need {NumberUtil.Money(cost - model.State.Money)} more";
                    else c.status.text = "<color=#86CC61>Affordable</color>";
                }
                c.title.color = missing != null ? UIStyle.Muted : UIStyle.Text;
            }

            foreach (var t in m_Tabs)
            {
                int n = 0;
                foreach (var u in Game.Instance.Db.upgrades)
                    if (u != null && u.category == t.category && up.Blocker(u) == null) n++;
                t.badge.text = n > 0 ? n.ToString() : "";
                t.button.SetColor(t.category == m_Category ? new Color(0.36f, 0.31f, 0.2f) : UIStyle.Button);
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("<b>Production</b>\n");
            foreach (var r in Game.Instance.Db.resources)
            {
                double g = model.Production.GrossRate(r.id);
                if (g <= 0) continue;
                sb.Append($"{r.displayName}: {NumberUtil.Rate(model.Production.NetRate(r.id))}\n");
            }
            foreach (var m in Game.Instance.Db.machines)
            {
                int count = up.MachineCount(m.id);
                if (count > 0) sb.Append($"{count} x {m.displayName}\n");
            }
            if (!model.Production.ProducesAnything) sb.Append("No machines yet.\n");
            sb.Append($"\n<b>Prices</b>\n");
            foreach (var r in Game.Instance.Db.resources) sb.Append($"{r.displayName}: {NumberUtil.Money(model.Market.Price(r))}\n");
            m_Summary.text = sb.ToString();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh <= 0f) RefreshAll();
            float dt = Time.unscaledDeltaTime;
            foreach (var c in m_Cards)
            {
                c.flash = Mathf.MoveTowards(c.flash, 0f, dt * 2.5f);
                c.shake = Mathf.MoveTowards(c.shake, 0f, dt * 4f);
                c.bg.color = Color.Lerp(UIStyle.Card, new Color(0.3f, 0.42f, 0.22f, 1f), c.flash);
                var bt = (RectTransform)c.buy.transform;
                bt.anchoredPosition = new Vector2(Mathf.Sin(c.shake * 40f) * 8f * c.shake, bt.anchoredPosition.y);
            }
        }
    }
}
