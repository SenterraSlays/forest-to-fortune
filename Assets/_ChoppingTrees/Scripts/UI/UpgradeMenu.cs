using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// The Workbench skill tree. Every upgrade is a node placed by its data
    /// (<see cref="UpgradeDef.treePosition"/>); curved links follow the prerequisites, so the tree
    /// grows from the trunk (your first axe, machine and buyer) up to the forests and harvesters.
    /// Branch headings are placed over each category's nodes, so a new category only needs data.
    /// The tree is laid out at a fixed size and can be dragged and zoomed (<see cref="SkillTreeView"/>);
    /// zoom 1 fits the whole tree in the window.
    /// Owned paths carry a slow flow of light; buying an upgrade sends a pulse along its links.
    /// The detail panel shows the hovered or selected node with its effect, requirements and price.
    /// Everything refreshes immediately after a purchase and a few times a second otherwise.
    /// <see cref="LegacyMenu"/> is the same screen for the Legacy perks (paid in skill points).
    /// </summary>
    public class UpgradeMenu : UIScreen
    {
        public static UpgradeMenu Instance { get; private set; }

        // Size of the content in canvas units per tree unit. The band below the trunk leaves room
        // for the overlays.
        const float UnitX = 136f, UnitY = 132f;
        static readonly Color WoodColor = new Color(0.62f, 0.45f, 0.27f);

        /// <summary>True for the Legacy tree: shows the Legacy perks and prices them in skill points.</summary>
        protected virtual bool Legacy => false;
        /// <summary>Tree units the content covers (positions are x = -11..9, y = 0..9).</summary>
        protected virtual Rect Bounds => Rect.MinMaxRect(-11.7f, -1.25f, 10.0f, 10.4f);
        protected virtual Vector2 RootPos => new Vector2(0.7f, 0.05f);
        /// <summary>Where the default view looks: the trunk and the first rows of every branch.</summary>
        protected virtual Vector2 HomePos => new Vector2(0.2f, 3.3f);
        protected virtual Color RootColor => WoodColor;
        protected virtual Color RootFace => new Color(0.30f, 0.21f, 0.12f);
        protected virtual string RootIcon => "log";
        /// <summary>Scale of the default view (0.92 is about full size).</summary>
        protected virtual float HomeScale => 0.92f;
        protected virtual Color Trim => UIStyle.Accent;
        Vector2 ContentSize => new Vector2(Bounds.width * UnitX, Bounds.height * UnitY);

        protected virtual void Register() => Instance = this;

        protected virtual void Unregister()
        {
            if (Instance == this) Instance = null;
        }

        class Pulse
        {
            public SkillLink link;
            public SkillNode target;
            public float t, duration;
            public Image[] dots;
        }

        class Heading
        {
            public UpgradeCategory category;
            public RectTransform rect, fill;
            public TextMeshProUGUI count;
        }

        readonly List<SkillNode> m_Nodes = new List<SkillNode>();
        readonly List<SkillLink> m_Links = new List<SkillLink>();
        readonly List<SkillLink> m_RootLinks = new List<SkillLink>();
        readonly Dictionary<SkillLink, Image> m_Flow = new Dictionary<SkillLink, Image>();
        readonly Dictionary<SkillLink, float> m_FlowPhase = new Dictionary<SkillLink, float>();
        readonly List<Pulse> m_Pulses = new List<Pulse>();
        readonly Stack<Image[]> m_DotPool = new Stack<Image[]>();
        readonly List<Heading> m_Headings = new List<Heading>();
        RectTransform m_Window, m_WindowShadow, m_TreeArea, m_Content, m_LinkLayer, m_FxLayer, m_RootNode, m_RootRings, m_RootLight;
        SkillTreeView m_View;
        Vector2 m_LastArea;
        SkillNode m_Selected, m_Hovered;
        float m_Refresh;

        // detail panel
        Image m_DBezel, m_DFace, m_DIcon;
        TextMeshProUGUI m_DName, m_DCategory, m_DLevel, m_DDesc, m_DEffect, m_DEffectCaption, m_DCost, m_DStatus;
        Image m_DEffectBar;
        RectTransform m_DLevelBar;
        UIButton m_Buy;
        TextMeshProUGUI m_Money, m_SkillPoints, m_DTag, m_Hint;
        Image m_DBanner, m_DHalo;
        GameObject m_DPipRow, m_DBarRow;
        readonly List<Image> m_DPips = new List<Image>();
        TextMeshProUGUI m_DPipText;
        class ReqRow
        {
            public GameObject go;
            public Image icon;
            public TextMeshProUGUI text;
        }
        readonly List<ReqRow> m_DReqRows = new List<ReqRow>();
        const int MaxPips = 15;

        // production panel
        class Stat
        {
            public GameObject go;
            public TextMeshProUGUI value;
        }
        readonly Dictionary<string, Stat> m_ResourceStats = new Dictionary<string, Stat>();
        readonly Dictionary<string, Stat> m_MachineStats = new Dictionary<string, Stat>();
        Stat m_PriceStat;

        protected override void Build()
        {
            Register();
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;

            RectTransform body;
            var window = Legacy
                ? UIFactory.Window(Root, "Legacy Tree", "star", new Vector2(1840, 1000), out body, "Perks bought with skill points: never reset, rebirth after rebirth")
                : UIFactory.Window(Root, "Workbench", "leaf", new Vector2(1840, 1000), out body, "Skill tree: spend money to grow your operation, one branch at a time");
            m_Window = window.rectTransform;
            m_WindowShadow = (RectTransform)Root.Find("WindowShadow");
            UIFactory.TopTrim(m_Window, Trim);
            var header = window.transform.Find("Header");
            var badge = header.Find("Badge");
            if (badge != null) badge.GetComponent<Image>().color = Trim;

            // Currency chips, Rebirth and close in the header (the Legacy tree leads with skill points).
            if (Legacy)
            {
                m_SkillPoints = UIFactory.Chip(header, "star", UIStyle.Skill, new Vector2(-104, 0), 200);
                m_Money = UIFactory.Chip(header, "coin", UIStyle.Money, new Vector2(-320, 0), 230);
            }
            else
            {
                m_Money = UIFactory.Chip(header, "coin", UIStyle.Money, new Vector2(-104, 0), 260);
                m_SkillPoints = UIFactory.Chip(header, "star", UIStyle.Skill, new Vector2(-380, 0), 190);
            }
            var rebirth = UIFactory.Button(header, "Rebirth", () => RebirthMenu.Instance.Open(), 20, ButtonStyle.Ghost, "rebirth");
            ((RectTransform)rebirth.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(Legacy ? -566 : -586, 0), new Vector2(170, 52));
            var close = UIFactory.Button(header, "", Close, 22, ButtonStyle.Ghost, "close");
            close.navigation = new Navigation { mode = Navigation.Mode.None }; // B closes
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));

            // Tree area: atmosphere behind, the tree on top, clipped to the rounded frame.
            var treeArea = UIFactory.Panel(body, "TreeArea", new Color(0.05f, 0.068f, 0.055f, 1f));
            m_TreeArea = treeArea.rectTransform;
            m_TreeArea.Stretch(28, 470, 18, 28);
            treeArea.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(6, 6);
            treeArea.raycastTarget = true; // empty space takes the drags
            BuildBackground();
            m_Content = UIFactory.Rect("Content", m_TreeArea);
            m_Content.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, ContentSize);
            m_View = treeArea.gameObject.AddComponent<SkillTreeView>();
            m_View.Init(m_TreeArea, m_Content);
            m_View.HomeScale = HomeScale;
            BuildTree();
            UIFactory.Border(m_TreeArea, new Color(1f, 0.95f, 0.82f, 0.12f));

            BuildLegend();
            if (Legacy) BuildPointsPanel();
            else BuildProduction();
            BuildDetails(body);

            Game.Instance.Model.Purchased += _ => { if (IsOpen) RefreshAll(); };
            Game.Instance.PhaseChanged += _ => Close();
        }

        void OnDestroy() => Unregister();

        // ------------------------------------------------------------------ layout

        Vector2 ToCanvas(Vector2 tree) => new Vector2((tree.x - Bounds.xMin) * UnitX, (tree.y - Bounds.yMin) * UnitY);

        /// <summary>Sizes the window to the screen, places the tree and fits the view to the tree
        /// area. Cheap (a few dozen rects), and only runs when the size changes.</summary>
        void Relayout()
        {
            var screen = Root.rect.size;
            var win = new Vector2(Mathf.Min(1840f, screen.x - 48f), Mathf.Min(1000f, screen.y - 40f));
            m_Window.sizeDelta = win;
            if (m_WindowShadow != null) m_WindowShadow.sizeDelta = win + Vector2.one * 72f;
            Canvas.ForceUpdateCanvases();

            Vector2 area = m_TreeArea.rect.size;
            m_LastArea = area;
            Vector2 avail = area - new Vector2(16f, 16f);
            m_View.Home = ToCanvas(HomePos);
            m_View.Configure(ContentSize, Mathf.Min(1f, avail.x / ContentSize.x, avail.y / ContentSize.y));

            foreach (var n in m_Nodes) n.Rect.anchoredPosition = ToCanvas(n.Def.treePosition);
            foreach (var l in m_Links) l.SetEnds(ToCanvas(l.From.treePosition), ToCanvas(l.To.treePosition));
            foreach (var l in m_RootLinks) l.SetEnds(ToCanvas(RootPos), ToCanvas(l.To.treePosition));
            m_RootNode.anchoredPosition = ToCanvas(RootPos);

            foreach (var h in m_Headings)
            {
                float sum = 0f;
                int count = 0;
                foreach (var n in m_Nodes)
                    if (n.Def.category == h.category) { sum += n.Def.treePosition.x; count++; }
                h.rect.anchoredPosition = ToCanvas(new Vector2(count > 0 ? sum / count : 0f, Bounds.yMax - 0.3f));
            }

            // Growth rings and lantern light sit in the content, centred on the trunk.
            m_RootRings.anchoredPosition = ToCanvas(RootPos);
            m_RootRings.sizeDelta = Vector2.one * ContentSize.y * 1.9f;
            m_RootLight.anchoredPosition = ToCanvas(RootPos) + new Vector2(0f, 40f);
            m_RootLight.sizeDelta = new Vector2(ContentSize.x * 0.75f, ContentSize.y * 0.7f);
        }

        // ------------------------------------------------------------------ background

        void BuildBackground()
        {
            var bg = UIFactory.Rect("Background", m_TreeArea);
            bg.Stretch();

            // Deep pine green at the top easing into charcoal below.
            var top = UIFactory.Panel(bg, "TopTint", new Color(0.09f, 0.15f, 0.11f, 0.55f), false);
            top.sprite = UIIcons.GradientV;
            top.rectTransform.Stretch();
            top.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);

            // Two rows of forest silhouettes along the bottom for depth.
            Silhouette(bg, UIIcons.ForestFar, new Color(0.10f, 0.15f, 0.12f, 0.55f), 210f, 0f);
            var mist = UIFactory.Panel(bg, "Mist", new Color(0.05f, 0.068f, 0.055f, 0.6f), false);
            mist.sprite = UIIcons.GradientV;
            mist.rectTransform.anchorMin = Vector2.zero;
            mist.rectTransform.anchorMax = new Vector2(1f, 0f);
            mist.rectTransform.pivot = new Vector2(0.5f, 0f);
            mist.rectTransform.sizeDelta = new Vector2(0f, 150f);
            Silhouette(bg, UIIcons.ForestNear, new Color(0.025f, 0.035f, 0.03f, 0.92f), 120f, 0f);

            var vignette = UIFactory.Panel(bg, "Vignette", new Color(0f, 0f, 0f, 0.6f), false);
            vignette.sprite = UIIcons.Vignette;
            vignette.rectTransform.Stretch();
        }

        static void Silhouette(RectTransform parent, Sprite sprite, Color color, float height, float y)
        {
            var img = UIFactory.Panel(parent, "Forest", color, false);
            img.sprite = sprite;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, height);
        }

        // ------------------------------------------------------------------ tree

        void BuildTree()
        {
            var db = Game.Instance.Db;
            var byId = new Dictionary<string, UpgradeDef>();
            foreach (var u in db.upgrades) if (u != null) byId[u.id] = u;

            // Growth rings of a cut log and a warm lantern light, centred on the trunk (they move
            // with the tree; the gradient, silhouettes and vignette stay put behind it).
            m_RootRings = UIFactory.Panel(m_Content, "Rings", new Color(1f, 0.88f, 0.66f, 0.011f), false).rectTransform;
            m_RootRings.GetComponent<Image>().sprite = UIIcons.TreeRings;
            m_RootRings.anchorMin = m_RootRings.anchorMax = Vector2.zero;
            m_RootLight = UIFactory.Panel(m_Content, "Light", new Color(1f, 0.68f, 0.3f, 0.09f), false).rectTransform;
            m_RootLight.GetComponent<Image>().sprite = UIIcons.Glow;
            m_RootLight.anchorMin = m_RootLight.anchorMax = Vector2.zero;

            // Branch headings, one per category in use.
            var headingLayer = UIFactory.Rect("Headings", m_Content);
            headingLayer.Stretch();
            var cats = new List<UpgradeCategory>();
            foreach (var u in db.upgrades) if (u != null && u.legacy == Legacy && !cats.Contains(u.category)) cats.Add(u.category);
            foreach (var c in cats) m_Headings.Add(HeadingFor(headingLayer, c));

            // Links first so nodes (and their labels) draw on top.
            m_LinkLayer = UIFactory.Rect("Links", m_Content);
            m_LinkLayer.Stretch();
            foreach (var u in db.upgrades)
            {
                if (u == null || u.legacy != Legacy) continue;
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
                    var link = SkillLink.Create(m_LinkLayer);
                    link.From = byId[r.upgradeId];
                    link.To = u;
                    link.RequiredLevel = r.level;
                    m_Links.Add(link);
                }
                if (parents.Count == 0)
                {
                    var link = SkillLink.Create(m_LinkLayer);
                    link.To = u;
                    m_RootLinks.Add(link);
                }
            }
            m_FxLayer = UIFactory.Rect("Flow", m_Content);
            m_FxLayer.Stretch();
            foreach (var l in m_Links) AddFlowDot(l);
            foreach (var l in m_RootLinks) AddFlowDot(l);

            // The trunk everything grows from: a larger wooden node.
            m_RootNode = UIFactory.Rect("Trunk", m_Content);
            m_RootNode.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112));
            var glow = UIFactory.Panel(m_RootNode, "Glow", new Color(1f, 0.7f, 0.35f, 0.18f), false);
            glow.sprite = UIIcons.Glow;
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 230));
            if (Legacy) glow.color = new Color(RootColor.r, RootColor.g, RootColor.b, 0.2f);
            var bezel = UIFactory.Panel(m_RootNode, "Bezel", RootColor, false);
            bezel.sprite = UIIcons.Bezel;
            bezel.rectTransform.Stretch();
            var face = UIFactory.Panel(m_RootNode, "Face", RootFace, false);
            face.sprite = UIIcons.Face;
            face.rectTransform.Stretch(11, 11, 11, 11);
            var rings = UIFactory.Panel(m_RootNode, "Rings", new Color(0.12f, 0.08f, 0.04f, Legacy ? 0.25f : 0.5f), false);
            rings.sprite = UIIcons.TreeRings;
            rings.rectTransform.Stretch(13, 13, 13, 13);
            UIFactory.Icon(m_RootNode, RootIcon, Legacy ? new Color(0.94f, 0.88f, 1f) : new Color(0.95f, 0.8f, 0.55f), 50f).rectTransform.anchoredPosition = Vector2.zero;

            var nodeLayer = UIFactory.Rect("Nodes", m_Content);
            nodeLayer.Stretch();
            foreach (var u in db.upgrades)
            {
                if (u == null || u.legacy != Legacy) continue;
                m_Nodes.Add(SkillNode.Create(nodeLayer, u, Vector2.zero, Hover, Select, Buy));
            }
        }

        /// <summary>Branch heading: icon, letter-spaced title, levels owned and a progress bar.</summary>
        Heading HeadingFor(RectTransform parent, UpgradeCategory c)
        {
            Color col = UIStyle.CategoryColor(c);
            var rt = UIFactory.Rect("Heading_" + c, parent);
            rt.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 52));
            var row = UIFactory.Rect("Row", rt);
            row.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(260, 26));
            UIFactory.HStack(row, 9f, null, TextAnchor.MiddleCenter);
            UIFactory.Icon(row, UIStyle.CategoryIcon(c), col, 20f);
            var t = UIFactory.Caption(row, UIStyle.CategoryTitle(c), col, 16f);
            t.characterSpacing = 12f;
            var count = UIFactory.Text(row, "", 15, UIStyle.Muted, true);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            // Progress bar: how many of the branch's levels are owned.
            var track = UIFactory.Panel(rt, "Track", new Color(col.r, col.g, col.b, 0.16f));
            track.sprite = UIStyle.RoundedSmall;
            track.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(170f, 4f));
            var fill = UIFactory.Panel(track.transform, "Fill", col);
            fill.sprite = UIStyle.RoundedSmall;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return new Heading { category = c, rect = rt, fill = fill.rectTransform, count = count };
        }

        void AddFlowDot(SkillLink l)
        {
            var dot = UIFactory.Panel(m_FxLayer, "FlowDot", Color.clear, false);
            dot.sprite = UIIcons.Glow;
            dot.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            dot.gameObject.SetActive(false);
            m_Flow[l] = dot;
            m_FlowPhase[l] = Random.value;
        }

        // ------------------------------------------------------------------ overlays

        /// <summary>State legend plus the view controls (zoom out, zoom in, show the whole tree).</summary>
        void BuildLegend()
        {
            var legend = UIFactory.Surface(m_TreeArea, "Legend", new Color(0.04f, 0.05f, 0.04f, 0.82f));
            legend.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(16, 16), Vector2.zero);
            UIFactory.HStack(legend, 14f, new RectOffset(14, 6, 5, 5));
            UIFactory.Fit(legend, true, true);
            void Item(string icon, Color bg, Color fg, string text)
            {
                var item = UIFactory.Rect("Item", legend.transform);
                UIFactory.HStack(item, 6f);
                var b = UIFactory.Panel(item, "Badge", bg, false);
                b.sprite = UIIcons.Face;
                b.Layout(20, 20);
                UIFactory.Icon(b.transform, icon, fg, 12f).rectTransform.anchoredPosition = Vector2.zero;
                b.transform.GetChild(0).GetComponent<LayoutElement>().ignoreLayout = true;
                var t = UIFactory.Text(item, text, 15, UIStyle.Muted);
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Item("lock", new Color(0.16f, 0.17f, 0.15f), UIStyle.Muted, "Locked");
            Item("arrow_up", UIStyle.Accent, UIStyle.AccentDark, "Can buy");
            Item("dot", new Color(0.16f, 0.17f, 0.15f), UIStyle.Text, "Owned");
            Item("check", UIStyle.Money, UIStyle.AccentDark, "Maxed");
            var hint = UIFactory.Text(legend.transform, "Drag to move  ·  Scroll to zoom", 15, UIStyle.Dim);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            var buttons = UIFactory.Rect("View", legend.transform);
            UIFactory.HStack(buttons, 4f);
            // Mouse tools: a controller zooms with the triggers, so these aren't navigation stops.
            void Btn(string icon, UnityEngine.Events.UnityAction a)
            {
                var b = UIFactory.Button(buttons, "", a, 15, ButtonStyle.Ghost, icon);
                b.Layout(34, 30);
                b.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            Btn("minus", () => m_View.ZoomBy(1f / 1.25f, Vector2.zero));
            Btn("plus", () => m_View.ZoomBy(1.25f, Vector2.zero));
            Btn("refresh", () => m_View.ResetView());
        }

        void BuildProduction()
        {
            var panel = UIFactory.Surface(m_TreeArea, "Production", new Color(0.04f, 0.05f, 0.04f, 0.86f));
            panel.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), Vector2.zero);
            UIFactory.VStack(panel, 6f, new RectOffset(14, 14, 9, 11));
            UIFactory.Fit(panel, true, true);

            var head = UIFactory.Rect("Head", panel.transform);
            UIFactory.HStack(head, 7f);
            UIFactory.Icon(head, "chart", UIStyle.Muted, 14f);
            UIFactory.Caption(head, "Production", UIStyle.Muted, 13f);

            var grid = UIFactory.Rect("Grid", panel.transform);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(176, 24);
            g.spacing = new Vector2(18, 2);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;

            var db = Game.Instance.Db;
            foreach (var r in db.resources)
                if (r != null) m_ResourceStats[r.id] = StatCell(grid, r.icon, r.color, r.displayName + "/s");
            foreach (var m in db.machines)
                if (m != null) m_MachineStats[m.id] = StatCell(grid, m.kind == MachineKind.Producer ? "saw" : "gear", UIStyle.CategoryColor(UpgradeCategory.Automation), m.displayName);
            m_PriceStat = StatCell(grid, "coin", UIStyle.Money, "Wood price");
        }

        Stat[] m_PointsStats;

        /// <summary>Legacy tree: skill points on hand, per rebirth and in total, and the rebirth bonus.</summary>
        void BuildPointsPanel()
        {
            var panel = UIFactory.Surface(m_TreeArea, "Points", new Color(0.04f, 0.05f, 0.04f, 0.86f));
            panel.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), Vector2.zero);
            UIFactory.VStack(panel, 6f, new RectOffset(14, 14, 9, 11));
            UIFactory.Fit(panel, true, true);
            var head = UIFactory.Rect("Head", panel.transform);
            UIFactory.HStack(head, 7f);
            UIFactory.Icon(head, "star", UIStyle.Skill, 14f);
            UIFactory.Caption(head, "Skill points", UIStyle.Muted, 13f);
            var grid = UIFactory.Rect("Grid", panel.transform);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(196, 24);
            g.spacing = new Vector2(18, 2);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            m_PointsStats = new[]
            {
                StatCell(grid, "star", UIStyle.Skill, "To spend"),
                StatCell(grid, "rebirth", UIStyle.Skill, "Per rebirth"),
                StatCell(grid, "chart", UIStyle.Skill, "Earned in total"),
                StatCell(grid, "coin", UIStyle.Money, "Rebirth bonus"),
            };
        }

        void RefreshPoints(GameModel model)
        {
            m_PointsStats[0].value.text = model.State.SkillPoints.ToString("N0");
            m_PointsStats[1].value.text = "+" + model.SkillPointsPerRebirth;
            m_PointsStats[2].value.text = model.State.SkillPointsEarned.ToString("N0");
            m_PointsStats[3].value.text = $"x{model.Market.RebirthMultiplier:0.0#}";
            m_PointsStats[3].value.color = UIStyle.Money;
        }

        static Stat StatCell(RectTransform grid, string icon, Color iconColor, string label)
        {
            var cell = UIFactory.Rect("Stat", grid);
            UIFactory.HStack(cell, 6f);
            UIFactory.Icon(cell, icon, iconColor, 16f);
            var l = UIFactory.Text(cell, label, 15.5f, UIStyle.Muted);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            l.overflowMode = TextOverflowModes.Ellipsis;
            l.Layout(-1, -1, 1);
            var v = UIFactory.Text(cell, "", 15.5f, UIStyle.Text, true, TextAlignmentOptions.Right);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            return new Stat { go = cell.gameObject, value = v };
        }

        // ------------------------------------------------------------------ details

        /// <summary>
        /// Detail panel: a hero banner tinted with the branch colour (large rimmed icon, branch,
        /// name, milestone tag), level pips, then cards for the description, the next level and
        /// the requirements, and a price footer with the buy button.
        /// </summary>
        void BuildDetails(RectTransform body)
        {
            var panel = UIFactory.Surface(body, "Details", new Color(0.1f, 0.115f, 0.095f, 0.99f));
            panel.rectTransform.anchorMin = new Vector2(1, 0);
            panel.rectTransform.anchorMax = new Vector2(1, 1);
            panel.rectTransform.pivot = new Vector2(1, 0.5f);
            panel.rectTransform.offsetMin = new Vector2(-446, 28);
            panel.rectTransform.offsetMax = new Vector2(-28, -18);
            UIFactory.VStack(panel, 10f, new RectOffset(24, 24, 22, 22));

            // Banner behind the hero: branch colour fading down into the panel.
            m_DBanner = UIFactory.Panel(panel.transform, "Banner", UIStyle.Accent);
            m_DBanner.sprite = UIIcons.GradientV;
            m_DBanner.type = Image.Type.Simple;
            var br = m_DBanner.rectTransform;
            br.anchorMin = new Vector2(0, 1);
            br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(0.5f, 0.5f); // flipped about its centre so it stays inside the panel
            br.sizeDelta = new Vector2(-4, 230);
            br.anchoredPosition = new Vector2(0, -2 - 115);
            br.localRotation = Quaternion.Euler(0, 0, 180f);
            m_DBanner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var hero = UIFactory.Rect("Hero", panel.transform);
            UIFactory.VStack(hero, 4f, new RectOffset(0, 0, 4, 2), TextAnchor.UpperCenter, false);
            var badge = UIFactory.Rect("Badge", hero);
            badge.Layout(108, 108);
            m_DHalo = UIFactory.Panel(badge, "Halo", UIStyle.Accent, false);
            m_DHalo.sprite = UIIcons.Glow;
            m_DHalo.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 220));
            m_DBezel = UIFactory.Panel(badge, "Bezel", UIStyle.Accent, false);
            m_DBezel.sprite = UIIcons.Bezel;
            m_DBezel.rectTransform.Stretch();
            m_DFace = UIFactory.Panel(badge, "Face", UIStyle.Card, false);
            m_DFace.sprite = UIIcons.Face;
            m_DFace.rectTransform.Stretch(11, 11, 11, 11);
            m_DIcon = UIFactory.Icon(badge, "dot", UIStyle.Text, 50f);
            m_DIcon.rectTransform.anchoredPosition = Vector2.zero;
            m_DCategory = UIFactory.Caption(hero, "", UIStyle.Accent, 13f);
            m_DCategory.alignment = TextAlignmentOptions.Center;
            m_DName = UIFactory.Text(hero, "", 30, UIStyle.Text, true, TextAlignmentOptions.Center);
            m_DTag = UIFactory.Pill(hero, "MILESTONE", new Color(1f, 0.83f, 0.4f, 0.16f), UIStyle.Money, 13f);
            m_DTag.characterSpacing = 6f;

            // Level: pips for up to 15 levels, a bar beyond that.
            var level = UIFactory.Rect("Level", panel.transform);
            UIFactory.HStack(level, 10f, new RectOffset(0, 0, 4, 2), TextAnchor.MiddleCenter);
            m_DPipRow = UIFactory.Rect("Pips", level).gameObject;
            UIFactory.HStack(m_DPipRow.GetComponent<RectTransform>(), 4f, null, TextAnchor.MiddleCenter);
            for (int i = 0; i < MaxPips; i++)
            {
                var pip = UIFactory.Panel(m_DPipRow.transform, "Pip", UIStyle.Inset);
                pip.sprite = UIStyle.RoundedSmall;
                pip.Layout(14, 8);
                m_DPips.Add(pip);
            }
            var barRow = UIFactory.Rect("BarRow", level);
            barRow.Layout(200, 8);
            UIFactory.HStack(barRow, 0f);
            m_DLevelBar = UIFactory.Bar(barRow, 8, UIStyle.Accent);
            m_DBarRow = barRow.gameObject;
            m_DLevel = UIFactory.Text(level, "", 16, UIStyle.Muted, true);
            m_DLevel.textWrappingMode = TextWrappingModes.NoWrap;

            var about = Card(panel.transform);
            m_DDesc = UIFactory.Text(about, "", 18, UIStyle.Muted);
            m_DDesc.lineSpacing = 4f;

            m_DEffectCaption = UIFactory.Caption(panel.transform, "Next level", UIStyle.Dim, 13f);
            var eff = UIFactory.Panel(panel.transform, "EffectBox", new Color(0.055f, 0.065f, 0.052f, 1f));
            UIFactory.Border(eff.transform, new Color(1f, 0.95f, 0.82f, 0.07f));
            UIFactory.VStack(eff, 0f, new RectOffset(20, 16, 13, 14));
            m_DEffectBar = UIFactory.Panel(eff.transform, "Accent", UIStyle.Accent, false);
            m_DEffectBar.rectTransform.anchorMin = new Vector2(0, 0);
            m_DEffectBar.rectTransform.anchorMax = new Vector2(0, 1);
            m_DEffectBar.rectTransform.pivot = new Vector2(0, 0.5f);
            m_DEffectBar.rectTransform.sizeDelta = new Vector2(3, -16);
            m_DEffectBar.rectTransform.anchoredPosition = new Vector2(7, 0);
            m_DEffectBar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            m_DEffect = UIFactory.Text(eff.transform, "", 20, UIStyle.Text);
            m_DEffect.lineSpacing = 6f;

            UIFactory.Caption(panel.transform, "Requirements", UIStyle.Dim, 13f);
            var reqs = UIFactory.Rect("Requirements", panel.transform);
            UIFactory.VStack(reqs, 6f);
            for (int i = 0; i < 3; i++)
            {
                var row = UIFactory.Panel(reqs, "Req", new Color(0.055f, 0.065f, 0.052f, 1f));
                UIFactory.HStack(row, 10f, new RectOffset(12, 12, 7, 7));
                var ic = UIFactory.Icon(row.transform, "check", UIStyle.Good, 18f);
                var t = UIFactory.Text(row.transform, "", 17, UIStyle.Text);
                t.Layout(-1, -1, 1);
                m_DReqRows.Add(new ReqRow { go = row.gameObject, icon = ic, text = t });
            }

            var spacer = UIFactory.Rect("Spacer", panel.transform);
            spacer.Layout(-1, -1, -1, 1);

            // Price footer.
            var footer = UIFactory.Panel(panel.transform, "Footer", new Color(0.055f, 0.065f, 0.052f, 1f));
            UIFactory.Border(footer.transform, new Color(1f, 0.83f, 0.4f, 0.12f));
            UIFactory.VStack(footer, 8f, new RectOffset(18, 18, 14, 16));
            var costRow = UIFactory.Rect("Cost", footer.transform);
            UIFactory.HStack(costRow, 10f, null, TextAnchor.MiddleLeft);
            UIFactory.Icon(costRow, Legacy ? "star" : "coin", Legacy ? UIStyle.Skill : UIStyle.Money, 30f);
            m_DCost = UIFactory.Text(costRow, "", 34, Legacy ? UIStyle.Skill : UIStyle.Money, true);
            m_DCost.textWrappingMode = TextWrappingModes.NoWrap;
            m_DStatus = UIFactory.Text(footer.transform, "", 16, UIStyle.Muted);
            m_Buy = UIFactory.Button(footer.transform, "Buy", () => Buy(m_Selected), 26, ButtonStyle.Primary, "check");
            m_Buy.Layout(-1, 62);
            m_Hint = UIFactory.Text(panel.transform, "", 14, UIStyle.Dim, false, TextAlignmentOptions.Center);
            m_Hint.Layout(-1, 18);
        }

        static RectTransform Card(Transform parent)
        {
            var c = UIFactory.Panel(parent, "Card", new Color(1f, 0.95f, 0.82f, 0.035f));
            UIFactory.VStack(c, 0f, new RectOffset(16, 16, 12, 13));
            return c.rectTransform;
        }

        static void Rule(Transform parent)
        {
            var r = UIFactory.Panel(parent, "Rule", new Color(1f, 0.95f, 0.82f, 0.08f), false);
            r.Layout(-1, 1.5f);
        }

        // ------------------------------------------------------------------ interaction

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            if (m_Selected == null) m_Selected = BestStartingNode();
            m_Hovered = null;
            Relayout();
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

        /// <summary>Hover previews a node in the details; leaving it (null) shows the selection again.</summary>
        void Hover(SkillNode n)
        {
            m_Hovered = n;
            // Controller focus can move off-screen: keep the focused node in view.
            if (n != null && GameInput.UsingGamepad) m_View.EnsureVisible(n.Rect.anchoredPosition);
            RefreshDetails();
        }

        /// <summary>A controller starts on the selected node.</summary>
        public override GameObject DefaultSelection() => m_Selected != null ? m_Selected.gameObject : base.DefaultSelection();

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
            // Remember which links were locked, to light up the paths this purchase opens.
            var wasLocked = new HashSet<SkillLink>();
            foreach (var l in m_Links) if (l.Current == SkillLink.Style.Locked) wasLocked.Add(l);
            if (model.TryBuy(n.Def, out string why))
            {
                AudioManager.Ui("purchase", 0.9f);
                string name = n.Def.LevelName(model.Upgrades.Level(n.Def)) ?? n.Def.displayName;
                Toasts.Show("Unlocked " + name, Toasts.Kind.Success);
                RefreshAll();
                bool pulsed = false;
                foreach (var l in m_Links) if (l.To == n.Def) { StartPulse(l, n); pulsed = true; }
                foreach (var l in m_RootLinks) if (l.To == n.Def) { StartPulse(l, n); pulsed = true; }
                if (!pulsed) n.Pop();
                foreach (var l in m_Links) if (wasLocked.Contains(l) && l.Current != SkillLink.Style.Locked) l.Flash = 1f;
                return;
            }
            AudioManager.Ui("error", 0.7f);
            Toasts.Show(why, Toasts.Kind.Warning);
            RefreshAll();
        }

        void StartPulse(SkillLink link, SkillNode target)
        {
            var dots = m_DotPool.Count > 0 ? m_DotPool.Pop() : null;
            if (dots == null)
            {
                dots = new Image[4];
                for (int i = 0; i < dots.Length; i++)
                {
                    dots[i] = UIFactory.Panel(m_FxLayer, "Pulse", Color.clear, false);
                    dots[i].sprite = UIIcons.Glow;
                    float s = i == 0 ? 34f : 26f - i * 5f;
                    dots[i].rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s));
                }
            }
            foreach (var d in dots) d.gameObject.SetActive(true);
            link.Flash = 1f;
            m_Pulses.Add(new Pulse { link = link, target = target, t = 0f, duration = Mathf.Clamp(link.Length / 900f, 0.28f, 0.6f), dots = dots });
        }

        void RefreshAll()
        {
            m_Refresh = 0.2f;
            var model = Game.Instance.Model;
            var up = model.Upgrades;
            m_Money.text = NumberUtil.Money(model.State.Money);
            m_SkillPoints.text = model.State.SkillPoints + " SP";
            m_Hint.text = GameInput.UsingGamepad
                ? $"{GameInput.Key("select")}: select, again: buy  ·  {GameInput.Key("look")}: pan  ·  {GameInput.Key("zoomOut")} / {GameInput.Key("zoomIn")}: zoom"
                : "Double-click a node to buy it";
            foreach (var n in m_Nodes) n.Refresh(model, n == m_Selected);
            foreach (var h in m_Headings)
            {
                int owned = 0, total = 0;
                foreach (var n in m_Nodes)
                    if (n.Def.category == h.category) { owned += up.Level(n.Def); total += n.Def.maxLevel; }
                h.count.text = $"{owned}/{total}";
                h.fill.anchorMax = new Vector2(total > 0 ? owned / (float)total : 0f, 1f);
            }

            foreach (var l in m_Links)
            {
                bool met = up.Level(l.From) >= l.RequiredLevel;
                bool owned = up.Level(l.To) > 0;
                Color cat = UIStyle.CategoryColor(l.To.category);
                if (met && owned) l.SetStyle(SkillLink.Style.Owned, new Color(cat.r, cat.g, cat.b, 0.95f));
                else if (met) l.SetStyle(SkillLink.Style.Available, new Color(cat.r, cat.g, cat.b, 0.6f));
                else l.SetStyle(SkillLink.Style.Locked, new Color(0.46f, 0.47f, 0.43f, 0.45f));
            }
            foreach (var l in m_RootLinks)
                l.SetStyle(up.Level(l.To) > 0 ? SkillLink.Style.Owned : SkillLink.Style.Available, up.Level(l.To) > 0 ? RootColor : new Color(RootColor.r, RootColor.g, RootColor.b, 0.6f));

            if (Legacy) RefreshPoints(model);
            else RefreshProduction(model);
            RefreshDetails();
        }

        void RefreshProduction(GameModel model)
        {
            var db = Game.Instance.Db;
            foreach (var r in db.resources)
            {
                if (r == null || !m_ResourceStats.TryGetValue(r.id, out var s)) continue;
                bool show = model.Production.GrossRate(r.id) > 0;
                if (s.go.activeSelf != show) s.go.SetActive(show);
                if (!show) continue;
                double net = model.Production.NetRate(r.id);
                s.value.text = NumberUtil.Rate(net);
                s.value.color = net >= 0 ? UIStyle.Good : UIStyle.Bad;
            }
            foreach (var m in db.machines)
            {
                if (m == null || !m_MachineStats.TryGetValue(m.id, out var s)) continue;
                int c = model.Upgrades.MachineCount(m.id);
                if (s.go.activeSelf != c > 0) s.go.SetActive(c > 0);
                if (c > 0) s.value.text = "x" + c;
            }
            m_PriceStat.value.text = NumberUtil.Money(model.Market.Price(db.Resource(GameModel.Wood)));
            m_PriceStat.value.color = UIStyle.Money;
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
            bool locked = !maxed && up.IsLocked(u);
            Color cat = UIStyle.CategoryColor(u.category);

            Color tone = maxed ? UIStyle.Money : locked ? new Color(0.45f, 0.46f, 0.42f) : cat;
            m_DBanner.color = new Color(tone.r, tone.g, tone.b, 0.22f);
            m_DHalo.color = new Color(tone.r, tone.g, tone.b, locked ? 0.08f : 0.25f);
            m_DBezel.color = maxed ? UIStyle.Money : locked ? new Color(0.36f, 0.37f, 0.34f) : cat;
            m_DFace.color = Color.Lerp(new Color(0.1f, 0.115f, 0.095f), maxed ? UIStyle.Money : cat, locked ? 0.04f : 0.2f);
            m_DIcon.sprite = UIIcons.Get(string.IsNullOrEmpty(u.icon) ? "dot" : u.icon);
            m_DIcon.color = locked ? UIStyle.Muted : maxed ? Color.Lerp(UIStyle.Money, Color.white, 0.35f) : UIStyle.Text;
            m_DCategory.text = UIStyle.CategoryTitle(u.category).ToUpperInvariant() + (locked ? "  ·  LOCKED" : "");
            m_DCategory.color = cat;
            m_DName.text = lv > 0 && u.LevelName(lv) != null ? u.LevelName(lv) : u.displayName;
            m_DTag.transform.parent.gameObject.SetActive(SkillNode.IsMilestone(u));

            bool pips = u.maxLevel > 1 && u.maxLevel <= MaxPips;
            m_DPipRow.SetActive(pips);
            m_DBarRow.SetActive(!pips && u.maxLevel > 1);
            for (int i = 0; i < m_DPips.Count; i++)
            {
                m_DPips[i].gameObject.SetActive(i < u.maxLevel);
                m_DPips[i].color = i < lv ? (maxed ? UIStyle.Money : cat) : new Color(1f, 0.95f, 0.82f, 0.1f);
            }
            m_DLevel.text = u.maxLevel == 1 ? (lv > 0 ? "Owned" : "Not owned") : $"Level {lv} / {u.maxLevel}";
            UIFactory.SetBar(m_DLevelBar, u.maxLevel <= 0 ? 0 : lv / (float)u.maxLevel);
            m_DLevelBar.GetComponent<Image>().color = maxed ? UIStyle.Money : cat;
            m_DDesc.text = u.description;
            m_DEffectCaption.text = maxed ? "CURRENT EFFECT" : lv > 0 ? "NEXT LEVEL" : "WHAT YOU GET";
            m_DEffect.text = UpgradeText.Effect(model, u);
            m_DEffectBar.color = maxed ? UIStyle.Money : cat;

            // Requirement rows: a check or a cross, then the upgrade (and level) needed.
            int shown = 0;
            void Row(bool ok, string text)
            {
                if (shown >= m_DReqRows.Count) return;
                var row = m_DReqRows[shown++];
                row.go.SetActive(true);
                row.icon.sprite = UIIcons.Get(ok ? "check" : "close");
                row.icon.color = ok ? UIStyle.Good : UIStyle.Bad;
                row.text.text = text;
                row.text.color = ok ? UIStyle.Text : UIStyle.Muted;
            }
            if (u.requires == null || u.requires.Length == 0) Row(true, "Nothing - ready from the start");
            else
                foreach (var r in u.requires)
                {
                    var other = Game.Instance.Db.Upgrade(r.upgradeId);
                    if (other == null) continue;
                    string name = other.LevelName(r.level) ?? (r.level > 1 ? $"{other.displayName} Lv {r.level}" : other.displayName);
                    Row(up.Level(other) >= r.level, name);
                }
            for (int i = shown; i < m_DReqRows.Count; i++) m_DReqRows[i].go.SetActive(false);

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
                string missing = up.MissingRequirement(u);
                bool afford = up.CanAfford(u);
                m_Buy.interactable = missing == null && afford;
                m_Buy.Label.text = lv > 0 ? "Upgrade" : "Unlock";
                string need;
                if (u.legacy)
                {
                    int points = up.SkillPointCost(u);
                    m_DCost.text = points == 1 ? "1 skill point" : points + " skill points";
                    int more = points - model.State.SkillPoints;
                    need = $"Need {more} more skill point{(more == 1 ? "" : "s")}. Each rebirth gives {model.SkillPointsPerRebirth}.";
                }
                else
                {
                    m_DCost.text = NumberUtil.Money(cost);
                    need = $"Need {NumberUtil.Money(cost - model.State.Money)} more.";
                }
                m_DStatus.text = missing != null ? $"<color=#E8705F>{missing}</color>" : afford ? "<color=#86CC61>You can afford this.</color>" : need;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            if (m_TreeArea.rect.size != m_LastArea) Relayout();
            m_Refresh -= dt;
            if (m_Refresh <= 0f) RefreshAll();
            AnimateLinks(dt);
        }

        /// <summary>Slow light travelling along owned paths, purchase pulses and link flashes.</summary>
        void AnimateLinks(float dt)
        {
            float time = Time.unscaledTime;
            foreach (var kv in m_Flow)
            {
                var link = kv.Key;
                var dot = kv.Value;
                bool on = link.Current == SkillLink.Style.Owned && link.Length > 1f;
                if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
                if (!on) continue;
                float period = Mathf.Max(2.2f, link.Length / 70f);
                float f = Mathf.Repeat(time / period + m_FlowPhase[link], 1f);
                dot.rectTransform.anchoredPosition = link.PointAt(f);
                var c = Color.Lerp(link.color, Color.white, 0.45f);
                c.a = 0.55f * Mathf.Sin(f * Mathf.PI);
                dot.color = c;
            }

            foreach (var l in m_Links) if (l.Flash > 0f) l.Flash -= dt * 1.6f;
            foreach (var l in m_RootLinks) if (l.Flash > 0f) l.Flash -= dt * 1.6f;

            for (int i = m_Pulses.Count - 1; i >= 0; i--)
            {
                var p = m_Pulses[i];
                p.t += dt / p.duration;
                p.link.Flash = 1f;
                float head = Mathf.SmoothStep(0f, 1f, p.t);
                var c = Color.Lerp(p.link.color, Color.white, 0.6f);
                for (int k = 0; k < p.dots.Length; k++)
                {
                    float f = Mathf.Clamp01(head - k * 0.06f);
                    p.dots[k].rectTransform.anchoredPosition = p.link.PointAt(f);
                    p.dots[k].color = new Color(c.r, c.g, c.b, (1f - k * 0.24f) * Mathf.Clamp01((1f - p.t) * 6f));
                }
                if (p.t < 1f) continue;
                if (p.target != null) p.target.Pop();
                foreach (var d in p.dots) d.gameObject.SetActive(false);
                m_DotPool.Push(p.dots);
                m_Pulses.RemoveAt(i);
            }
        }

        /// <summary>Automation hook: the node for an upgrade id (null if missing).</summary>
        public SkillNode Node(string id) => m_Nodes.Find(n => n.Def.id == id);
    }
}
