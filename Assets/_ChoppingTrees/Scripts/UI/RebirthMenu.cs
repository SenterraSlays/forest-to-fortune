using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Rebirth screen: shows the rebirth count, the earnings bonus and the price, lets the player
    /// buy 1, 5, 10 or as many rebirths as they can afford at once, and lists what resets and what
    /// stays. Buying asks for confirmation, then <see cref="Game.Rebirth"/> restarts the run.
    /// Opened from the skill tree header and the pause menu.
    /// </summary>
    public class RebirthMenu : UIScreen
    {
        public static RebirthMenu Instance { get; private set; }

        /// <summary>Quantity choices; 0 means "as many as you can afford".</summary>
        static readonly int[] Choices = { 1, 5, 10, 0 };

        TextMeshProUGUI m_Count, m_Bonus, m_Next, m_Title, m_Cost, m_After, m_Status;
        readonly UIButton[] m_ChoiceButtons = new UIButton[Choices.Length];
        UIButton m_Go;
        int m_Choice;
        float m_Refresh;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Rebirth", "rebirth", new Vector2(880, 700), out var body, "Start a new run with a permanent boost to everything you earn");
            var close = UIFactory.Button(window.transform.Find("Header"), "", Close, 22, ButtonStyle.Ghost, "close");
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));

            var col = UIFactory.Rect("Column", body);
            col.Stretch(36, 36, 22, 30);
            UIFactory.VStack(col, 16f);

            // Current state: rebirths, bonus, price of the next one.
            var stats = UIFactory.Rect("Stats", col);
            UIFactory.HStack(stats, 14f);
            stats.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            m_Count = Stat(stats, "rebirth", "Rebirths", UIStyle.Text);
            m_Bonus = Stat(stats, "coin", "Earnings", UIStyle.Money);
            m_Next = Stat(stats, "arrow_up", "Next rebirth", UIStyle.Accent);

            // How many at once.
            UIFactory.Caption(col, "How many at once", UIStyle.Dim, 13f);
            var choices = UIFactory.Rect("Choices", col);
            UIFactory.HStack(choices, 10f);
            for (int i = 0; i < Choices.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(choices, Choices[i] == 0 ? "Max" : "x" + Choices[i], () => Choose(index), 22, ButtonStyle.Ghost);
                b.Layout(-1, 52, 1);
                m_ChoiceButtons[i] = b;
            }

            // Summary of the chosen purchase.
            var card = UIFactory.Panel(col, "Summary", new Color(0.06f, 0.07f, 0.058f, 1f));
            UIFactory.Border(card.transform, UIStyle.Border);
            UIFactory.VStack(card, 6f, new RectOffset(22, 22, 16, 18));
            m_Title = UIFactory.Text(card.transform, "", 24, UIStyle.Text, true);
            var costRow = UIFactory.Rect("Cost", card.transform);
            UIFactory.HStack(costRow, 10f);
            UIFactory.Icon(costRow, "coin", UIStyle.Money, 30f);
            m_Cost = UIFactory.Text(costRow, "", 34, UIStyle.Money, true);
            m_Cost.textWrappingMode = TextWrappingModes.NoWrap;
            m_After = UIFactory.Text(card.transform, "", 20, UIStyle.Text);
            m_Status = UIFactory.Text(card.transform, "", 18, UIStyle.Muted);

            // What resets and what stays.
            var lists = UIFactory.Rect("Lists", col);
            UIFactory.HStack(lists, 18f, null, TextAnchor.UpperLeft);
            lists.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            List(lists, "Resets", UIStyle.Bad, "Money, axes and backpack\nAll skill tree upgrades\nMachines and opened forests\nWood in the yard and backpack");
            List(lists, "You keep", UIStyle.Good, "Every rebirth bonus\nLifetime stats\nYour settings");

            var spacer = UIFactory.Rect("Spacer", col);
            spacer.Layout(-1, -1, -1, 1);
            var buttons = UIFactory.Rect("Buttons", col);
            UIFactory.HStack(buttons, 14f, null, TextAnchor.MiddleRight);
            UIFactory.Button(buttons, "Cancel", Close, 22, ButtonStyle.Ghost).Layout(180, 60);
            m_Go = UIFactory.Button(buttons, "Rebirth", Confirm, 24, ButtonStyle.Primary, "rebirth");
            m_Go.Layout(300, 60);

            Game.Instance.PhaseChanged += _ => Close();
        }

        static TextMeshProUGUI Stat(RectTransform parent, string icon, string label, Color color)
        {
            var card = UIFactory.Surface(parent, label, UIStyle.Card);
            card.Layout(-1, 96, 1);
            UIFactory.VStack(card, 2f, new RectOffset(18, 18, 12, 12));
            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 7f);
            UIFactory.Icon(head, icon, UIStyle.Muted, 16f);
            UIFactory.Caption(head, label, UIStyle.Muted, 13f);
            var v = UIFactory.Text(card.transform, "", 32, color, true);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            return v;
        }

        static void List(RectTransform parent, string title, Color color, string lines)
        {
            var box = UIFactory.Rect(title, parent);
            box.Layout(-1, -1, 1);
            UIFactory.VStack(box, 6f);
            UIFactory.Caption(box, title, color, 13f);
            var t = UIFactory.Text(box, lines, 18, UIStyle.Muted);
            t.lineSpacing = 8f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            Choose(0, false);
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
            int done = m.State.Rebirths, n = Count(m);
            double cost = m.RebirthCost(n);
            bool afford = m.State.Money + 1e-9 >= cost;

            m_Count.text = done.ToString("N0");
            m_Bonus.text = $"x{Rebirth.Multiplier(cfg, done):0.0#}";
            m_Next.text = NumberUtil.Money(m.RebirthCost(1));
            m_Title.text = n > 1 ? $"Rebirth {n} times" : "Rebirth once";
            m_Cost.text = NumberUtil.Money(cost);
            m_After.text = $"Earnings <color=#B5AE9C>x{Rebirth.Multiplier(cfg, done):0.0#}</color> <color=#8F8A7C>-></color> <color=#FFD466><b>x{Rebirth.Multiplier(cfg, done + n):0.0#}</b></color>   " +
                           $"<color=#8F8A7C>(+{cfg.rebirthBonus * 100:0}% per rebirth)</color>";
            int max = m.MaxRebirths();
            m_Status.text = afford
                ? $"<color=#86CC61>You can afford this.</color>  You have {NumberUtil.Money(m.State.Money)}" + (max > n ? $" (enough for {max})" : "")
                : $"<color=#E8705F>Need {NumberUtil.Money(cost - m.State.Money)} more.</color>  You have {NumberUtil.Money(m.State.Money)}";
            m_Go.interactable = afford;
            m_Go.Label.text = n > 1 ? $"Rebirth x{n}" : "Rebirth";
        }

        void Confirm()
        {
            var m = Game.Instance.Model;
            int n = Count(m);
            double cost = m.RebirthCost(n);
            string times = n > 1 ? $"{n} times" : "once";
            ConfirmDialog.Ask($"Rebirth {times}?",
                $"You pay {NumberUtil.Money(cost)} and start over: money, upgrades, machines, forests and all wood reset. " +
                $"Everything you earn afterwards is worth x{Rebirth.Multiplier(m.Db.config, m.State.Rebirths + n):0.0#}.",
                "Rebirth", () =>
                {
                    if (!Game.Instance.Rebirth(n, out var why))
                    {
                        AudioManager.Ui("error", 0.6f);
                        Toasts.Show(why, Toasts.Kind.Warning);
                    }
                });
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh <= 0f) Refresh();
        }
    }
}
