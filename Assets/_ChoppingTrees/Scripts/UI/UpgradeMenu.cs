using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// The Workbench skill tree. Every upgrade is a node placed by its data
    /// (<see cref="UpgradeDef.treePosition"/>); links follow the prerequisites, so the tree grows
    /// from the trunk (your first axe, machine and buyer) up to the forests and harvesters.
    /// The detail panel shows the hovered or selected node with its effect, requirements and price.
    /// Everything refreshes immediately after a purchase and a few times a second otherwise.
    /// </summary>
    public class UpgradeMenu : UIScreen
    {
        public static UpgradeMenu Instance { get; private set; }

        class Link
        {
            public Image image;
            public UpgradeDef from, to;
            public int level;
        }

        const float UnitX = 152f, UnitY = 128f, MinX = -4.4f, MinY = -0.55f;

        readonly List<SkillNode> m_Nodes = new List<SkillNode>();
        readonly List<Link> m_Links = new List<Link>();
        readonly List<Image> m_RootLinks = new List<Image>();
        RectTransform m_Canvas;
        SkillNode m_Selected, m_Hovered;
        float m_Refresh;

        // detail panel
        Image m_DIconBg, m_DIcon;
        TextMeshProUGUI m_DName, m_DCategory, m_DLevel, m_DDesc, m_DEffect, m_DReq, m_DCost, m_DStatus;
        RectTransform m_DLevelBar;
        UIButton m_Buy;
        TextMeshProUGUI m_Money, m_Summary;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;

            var window = UIFactory.Window(Root, "Skill Tree", "leaf", new Vector2(1800, 960), out var body, "Grow your operation: every branch unlocks the next");

            // Money chip and close button in the header.
            var header = window.transform.Find("Header");
            var chip = UIFactory.Surface(header, "Money", UIStyle.Inset);
            chip.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-104, 0), new Vector2(260, 52));
            UIFactory.HStack(chip, 10f, new RectOffset(16, 18, 6, 6), TextAnchor.MiddleRight);
            UIFactory.Icon(chip.transform, "coin", UIStyle.Money, 28f);
            m_Money = UIFactory.Text(chip.transform, "$0", 30, UIStyle.Money, true, TextAlignmentOptions.Right);
            m_Money.textWrappingMode = TextWrappingModes.NoWrap;
            m_Money.Layout(-1, -1, 1);
            var close = UIFactory.Button(header, "", Close, 22, ButtonStyle.Ghost, "close");
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));

            // Tree area.
            var treeArea = UIFactory.Panel(body, "TreeArea", new Color(0.06f, 0.07f, 0.055f, 0.6f));
            treeArea.rectTransform.Stretch(28, 470, 18, 28);
            UIFactory.Border(treeArea.transform, UIStyle.Border);
            m_Canvas = UIFactory.Rect("Canvas", treeArea.transform);
            m_Canvas.Stretch(10, 10, 10, 10);
            BuildDecor();
            BuildTree();

            // Legend and production summary.
            m_Summary = UIFactory.Text(treeArea.transform, "", 17, UIStyle.Muted);
            m_Summary.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 16), new Vector2(300, 140));
            m_Summary.alignment = TextAlignmentOptions.BottomRight;

            BuildDetails(body);

            Game.Instance.Model.Purchased += _ => { if (IsOpen) RefreshAll(); };
            Game.Instance.PhaseChanged += _ => Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Vector2 ToCanvas(Vector2 tree)
        {
            var size = new Vector2(1302f - 20f, 828f - 20f);
            float x = (tree.x - MinX) * UnitX;
            float y = (tree.y - MinY) * UnitY;
            return new Vector2(Mathf.Clamp(x, 60f, size.x - 60f), Mathf.Clamp(y, 60f, size.y - 70f));
        }

        static readonly Vector2 RootPos = new Vector2(0.15f, -0.05f);

        void BuildDecor()
        {
            // Branch captions.
            void Caption(string text, Vector2 at, UpgradeCategory cat)
            {
                var c = UIFactory.Caption(m_Canvas, text, UIStyle.CategoryColor(cat) * 0.9f, 15f);
                c.alignment = TextAlignmentOptions.Center;
                c.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), ToCanvas(at), new Vector2(240, 24));
            }
            Caption("Axe", new Vector2(-3.45f, 0.45f), UpgradeCategory.Axe);
            Caption("Automation", new Vector2(1.35f, 1.05f), UpgradeCategory.Automation);
            Caption("Economy", new Vector2(3.75f, 0.45f), UpgradeCategory.Economy);
            Caption("Forests", new Vector2(-3.25f, 4.15f), UpgradeCategory.Forest);
        }

        void BuildTree()
        {
            var db = Game.Instance.Db;
            var byId = new Dictionary<string, UpgradeDef>();
            foreach (var u in db.upgrades) if (u != null) byId[u.id] = u;

            // Links first so nodes draw on top.
            var linkLayer = UIFactory.Rect("Links", m_Canvas);
            linkLayer.Stretch();
            foreach (var u in db.upgrades)
            {
                if (u == null) continue;
                // Draw links to prerequisites in the same branch; a node with none in its branch
                // links to its first prerequisite. Every prerequisite is still listed in the details.
                var parents = new List<UpgradeDef.Requirement>();
                if (u.requires != null)
                {
                    foreach (var r in u.requires)
                        if (byId.TryGetValue(r.upgradeId, out var p) && p.category == u.category) parents.Add(r);
                    if (parents.Count == 0 && u.requires.Length > 0 && byId.ContainsKey(u.requires[0].upgradeId)) parents.Add(u.requires[0]);
                }
                foreach (var r in parents)
                {
                    var from = byId[r.upgradeId];
                    m_Links.Add(new Link { image = Line(linkLayer, ToCanvas(from.treePosition), ToCanvas(u.treePosition), 7f), from = from, to = u, level = r.level });
                }
                if (parents.Count == 0) m_RootLinks.Add(Line(linkLayer, ToCanvas(RootPos), ToCanvas(u.treePosition), 9f));
            }

            // The trunk node everything grows from.
            var root = UIFactory.Panel(m_Canvas, "Root", new Color(0.36f, 0.26f, 0.16f), false);
            root.sprite = UIStyle.Circle;
            root.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), ToCanvas(RootPos), new Vector2(70, 70));
            var rootIn = UIFactory.Panel(root.transform, "In", new Color(0.55f, 0.40f, 0.24f), false);
            rootIn.sprite = UIStyle.Circle;
            rootIn.rectTransform.Stretch(6, 6, 6, 6);
            UIFactory.Icon(root.transform, "log", new Color(0.9f, 0.76f, 0.52f), 40f).rectTransform.anchoredPosition = Vector2.zero;

            foreach (var u in db.upgrades)
            {
                if (u == null) continue;
                m_Nodes.Add(SkillNode.Create(m_Canvas, u, ToCanvas(u.treePosition), Hover, Select, Buy));
            }
        }

        static Image Line(RectTransform parent, Vector2 a, Vector2 b, float width)
        {
            var img = UIFactory.Panel(parent, "Link", UIStyle.Dim, false);
            img.sprite = UIStyle.RoundedSmall;
            img.type = Image.Type.Sliced;
            var rt = img.rectTransform;
            Vector2 d = b - a;
            rt.Place(Vector2.zero, new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(d.magnitude, width));
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return img;
        }

        void BuildDetails(RectTransform body)
        {
            var panel = UIFactory.Surface(body, "Details", UIStyle.PanelLight);
            panel.rectTransform.anchorMin = new Vector2(1, 0);
            panel.rectTransform.anchorMax = new Vector2(1, 1);
            panel.rectTransform.pivot = new Vector2(1, 0.5f);
            panel.rectTransform.offsetMin = new Vector2(-446, 28);
            panel.rectTransform.offsetMax = new Vector2(-28, -18);
            UIFactory.VStack(panel, 12f, new RectOffset(28, 28, 28, 28));

            var head = UIFactory.Rect("Head", panel.transform);
            UIFactory.HStack(head, 16f);
            m_DIconBg = UIFactory.Panel(head, "IconBg", UIStyle.Accent);
            m_DIconBg.sprite = UIStyle.Circle;
            m_DIconBg.Layout(78, 78);
            m_DIcon = UIFactory.Icon(m_DIconBg.transform, "dot", UIStyle.AccentDark, 46f);
            m_DIcon.rectTransform.anchoredPosition = Vector2.zero;
            m_DIcon.GetComponent<LayoutElement>().ignoreLayout = true;
            var titles = UIFactory.Rect("Titles", head);
            UIFactory.VStack(titles, 2f);
            titles.Layout(-1, -1, 1);
            m_DCategory = UIFactory.Caption(titles, "", UIStyle.Accent, 14f);
            m_DName = UIFactory.Text(titles, "", 30, UIStyle.Text, true);

            var levelRow = UIFactory.Rect("Level", panel.transform);
            UIFactory.HStack(levelRow, 12f);
            m_DLevel = UIFactory.Text(levelRow, "", 18, UIStyle.Muted, true);
            m_DLevel.textWrappingMode = TextWrappingModes.NoWrap;
            m_DLevel.Layout(110);
            m_DLevelBar = UIFactory.Bar(levelRow, 10, UIStyle.Accent);

            m_DDesc = UIFactory.Text(panel.transform, "", 20, UIStyle.Muted);

            UIFactory.Caption(panel.transform, "Effect", UIStyle.Dim, 14f);
            var eff = UIFactory.Panel(panel.transform, "EffectBox", UIStyle.Inset);
            UIFactory.VStack(eff, 0f, new RectOffset(16, 16, 12, 12));
            m_DEffect = UIFactory.Text(eff.transform, "", 21, UIStyle.Text);

            UIFactory.Caption(panel.transform, "Requires", UIStyle.Dim, 14f);
            m_DReq = UIFactory.Text(panel.transform, "", 19, UIStyle.Text);

            var spacer = UIFactory.Rect("Spacer", panel.transform);
            spacer.Layout(-1, -1, -1, 1);

            var costRow = UIFactory.Rect("Cost", panel.transform);
            UIFactory.HStack(costRow, 10f, null, TextAnchor.MiddleLeft);
            UIFactory.Icon(costRow, "coin", UIStyle.Money, 30f);
            m_DCost = UIFactory.Text(costRow, "", 32, UIStyle.Money, true);
            m_DCost.textWrappingMode = TextWrappingModes.NoWrap;
            m_DStatus = UIFactory.Text(panel.transform, "", 18, UIStyle.Muted);
            m_Buy = UIFactory.Button(panel.transform, "Buy", () => Buy(m_Selected), 26, ButtonStyle.Primary, "check");
            m_Buy.Layout(-1, 66);
            var hint = UIFactory.Text(panel.transform, "Tip: double-click a node to buy it", 15, UIStyle.Dim, false, TextAlignmentOptions.Center);
            hint.Layout(-1, 22);
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            if (m_Selected == null) m_Selected = BestStartingNode();
            m_Hovered = null;
            RefreshAll();
        }

        SkillNode BestStartingNode()
        {
            SkillNode best = null;
            var up = Game.Instance.Model.Upgrades;
            foreach (var n in m_Nodes)
            {
                if (up.Blocker(n.Def) != null) continue;
                if (best == null || up.NextCost(n.Def) < up.NextCost(best.Def)) best = n;
            }
            return best ?? m_Nodes[0];
        }

        void Hover(SkillNode n)
        {
            m_Hovered = n;
            RefreshDetails();
        }

        void Select(SkillNode n)
        {
            m_Selected = n;
            m_Hovered = null;
            AudioManager.Ui("ui_click", 0.5f);
            RefreshAll();
        }

        void Buy(SkillNode n)
        {
            if (n == null) return;
            m_Selected = n;
            m_Hovered = null;
            var model = Game.Instance.Model;
            if (model.TryBuy(n.Def, out string why))
            {
                n.Pop();
                AudioManager.Ui("purchase", 0.9f);
                string name = n.Def.LevelName(model.Upgrades.Level(n.Def)) ?? n.Def.displayName;
                Toasts.Show("Unlocked " + name, Toasts.Kind.Success);
            }
            else
            {
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
            foreach (var n in m_Nodes) n.Refresh(model, n == m_Selected);

            foreach (var l in m_Links)
            {
                bool met = up.Level(l.from) >= l.level;
                bool owned = up.Level(l.to) > 0;
                Color cat = UIStyle.CategoryColor(l.to.category);
                l.image.color = owned ? new Color(cat.r, cat.g, cat.b, 0.95f) : met ? new Color(cat.r, cat.g, cat.b, 0.45f) : new Color(0.35f, 0.36f, 0.32f, 0.35f);
            }
            for (int i = 0; i < m_RootLinks.Count; i++) m_RootLinks[i].color = new Color(0.55f, 0.40f, 0.24f, 0.9f);

            var sb = new StringBuilder();
            foreach (var r in Game.Instance.Db.resources)
            {
                double g = model.Production.GrossRate(r.id);
                if (g > 0) sb.Append($"{r.displayName} <color=#86CC61>{NumberUtil.Rate(model.Production.NetRate(r.id))}</color>\n");
            }
            foreach (var m in Game.Instance.Db.machines)
            {
                int c = up.MachineCount(m.id);
                if (c > 0) sb.Append($"{c} x {m.displayName}\n");
            }
            sb.Append($"Wood sells for <color=#FFD466>{NumberUtil.Money(model.Market.Price(Game.Instance.Db.Resource(GameModel.Wood)))}</color>");
            m_Summary.text = sb.ToString();
            RefreshDetails();
        }

        void RefreshDetails()
        {
            var n = m_Hovered ?? m_Selected;
            if (n == null) return;
            var model = Game.Instance.Model;
            var up = model.Upgrades;
            var u = n.Def;
            int lv = up.Level(u);
            bool maxed = u.IsMaxed(lv);
            Color cat = UIStyle.CategoryColor(u.category);

            m_DIconBg.color = maxed ? UIStyle.Money : cat;
            m_DIcon.sprite = UIIcons.Get(string.IsNullOrEmpty(u.icon) ? "dot" : u.icon);
            m_DCategory.text = u.category.ToString().ToUpperInvariant();
            m_DCategory.color = cat;
            m_DName.text = lv > 0 && u.LevelName(lv) != null ? u.LevelName(lv) : u.displayName;
            m_DLevel.text = u.maxLevel == 1 ? (lv > 0 ? "Owned" : "Not owned") : $"Level {lv}/{u.maxLevel}";
            UIFactory.SetBar(m_DLevelBar, u.maxLevel <= 0 ? 0 : lv / (float)u.maxLevel);
            m_DLevelBar.GetComponent<Image>().color = maxed ? UIStyle.Money : cat;
            m_DDesc.text = u.description;
            m_DEffect.text = UpgradeText.Effect(model, u);

            var req = new StringBuilder();
            if (u.requires == null || u.requires.Length == 0) req.Append("<color=#86CC61>Nothing - ready from the start</color>");
            else
                foreach (var r in u.requires)
                {
                    var other = Game.Instance.Db.Upgrade(r.upgradeId);
                    if (other == null) continue;
                    bool ok = up.Level(other) >= r.level;
                    string name = other.LevelName(r.level) ?? (r.level > 1 ? $"{other.displayName} Lv {r.level}" : other.displayName);
                    req.Append(ok ? $"<color=#86CC61>✓</color>  {name}\n" : $"<color=#E8705F>✕</color>  <color=#B5AE9C>{name}</color>\n");
                }
            m_DReq.text = req.ToString().TrimEnd('\n');

            if (maxed)
            {
                m_DCost.text = u.maxLevel == 1 ? "Owned" : "Maxed";
                m_DStatus.text = "Fully upgraded.";
                m_Buy.interactable = false;
                m_Buy.Label.text = u.maxLevel == 1 ? "Owned" : "Maxed";
            }
            else
            {
                double cost = up.NextCost(u);
                m_DCost.text = NumberUtil.Money(cost);
                string missing = up.MissingRequirement(u);
                bool afford = model.State.Money + 1e-9 >= cost;
                m_Buy.interactable = missing == null && afford;
                m_Buy.Label.text = lv > 0 ? "Upgrade" : "Unlock";
                m_DStatus.text = missing != null ? $"<color=#E8705F>{missing}</color>" : afford ? "<color=#86CC61>You can afford this.</color>" : $"Need {NumberUtil.Money(cost - model.State.Money)} more.";
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh <= 0f) RefreshAll();
        }

        /// <summary>Automation hook: the node for an upgrade id (null if missing).</summary>
        public SkillNode Node(string id) => m_Nodes.Find(n => n.Def.id == id);
    }
}
