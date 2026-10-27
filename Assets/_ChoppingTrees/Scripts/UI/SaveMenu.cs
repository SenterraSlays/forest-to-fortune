using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// The three save slots (A, B, C) side by side, opened from Play on the main menu. Each card
    /// shows that save's stats (money, rebirths, trees, wood, axe, time played, last played) so
    /// they're easy to tell apart; a used slot can be continued or deleted, an empty one starts a
    /// new game. The slot played last is marked.
    /// </summary>
    public class SaveMenu : UIScreen
    {
        public static SaveMenu Instance { get; private set; }

        class Card
        {
            public Image badge, glow;
            public TextMeshProUGUI tag, title, empty;
            public GameObject stats;
            public readonly List<TextMeshProUGUI> values = new List<TextMeshProUGUI>();
            public UIButton play, delete;
        }

        readonly Card[] m_Cards = new Card[3];
        TextMeshProUGUI m_CloudText;
        UIButton m_CloudButton;
        Image m_CloudIcon;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Saves", "save", new Vector2(1240, 760), out var body, "Pick a save to continue, or start a new game in an empty slot");
            UIFactory.TopTrim(window.rectTransform, UIStyle.Accent);
            var close = UIFactory.Button(window.transform.Find("Header"), "", Close, 22, ButtonStyle.Ghost, "close");
            ((RectTransform)close.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(56, 52));
            close.navigation = new Navigation { mode = Navigation.Mode.None }; // B closes

            var row = UIFactory.Rect("Slots", body);
            row.Stretch(36, 36, 26, 110);
            var h = UIFactory.HStack(row, 22f, null, TextAnchor.UpperCenter);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++) m_Cards[i] = BuildCard(row, i);
            BuildCloudBar(body);
            Game.Instance.PhaseChanged += _ => Close();
            CloudSync.Changed += OnCloudChanged;
        }

        Card BuildCard(RectTransform parent, int slot)
        {
            var c = new Card();
            var card = UIFactory.Surface(parent, "Slot" + SaveSystem.SlotNames[slot], new Color(0.085f, 0.098f, 0.08f, 1f));
            UIFactory.VStack(card, 10f, new RectOffset(24, 24, 22, 22));
            card.Layout(1, -1, 1); // equal widths whatever the content
            c.glow = UIFactory.Panel(card.transform, "Glow", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0f));
            c.glow.sprite = UIIcons.GradientV;
            c.glow.type = Image.Type.Simple;
            c.glow.rectTransform.Stretch();
            c.glow.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            c.glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 14f);
            var badgeRoot = UIFactory.Rect("Badge", head);
            badgeRoot.Layout(64, 64);
            c.badge = UIFactory.Panel(badgeRoot, "Bezel", UIStyle.Accent, false);
            c.badge.sprite = UIIcons.Bezel;
            c.badge.rectTransform.Stretch();
            var face = UIFactory.Panel(badgeRoot, "Face", new Color(0.12f, 0.1f, 0.06f), false);
            face.sprite = UIIcons.Face;
            face.rectTransform.Stretch(7, 7, 7, 7);
            var letter = UIFactory.Text(badgeRoot, SaveSystem.SlotNames[slot], 30, UIStyle.Money, true, TextAlignmentOptions.Center);
            letter.rectTransform.Stretch();
            var titles = UIFactory.Rect("Titles", head);
            UIFactory.VStack(titles, 2f);
            titles.Layout(-1, -1, 1);
            c.title = UIFactory.Text(titles, "Save " + SaveSystem.SlotNames[slot], 28, UIStyle.Text, true);
            c.tag = UIFactory.Text(titles, "", 15, UIStyle.Muted);

            var stats = UIFactory.Rect("Stats", card.transform);
            UIFactory.VStack(stats, 6f, new RectOffset(0, 0, 6, 0));
            stats.Layout(-1, -1, -1, 1);
            foreach (var (icon, color, label) in new[]
                     {
                         ("coin", UIStyle.Money, "Money"), ("rebirth", UIStyle.Skill, "Rebirths"), ("pine", UIStyle.Good, "Trees felled"),
                         ("log", new Color(0.78f, 0.56f, 0.33f), "Wood chopped"), ("axe", UIStyle.CategoryColor(UpgradeCategory.Axe), "Axe"),
                         ("clock", UIStyle.CategoryColor(UpgradeCategory.Forest), "Time played"), ("trophy", UIStyle.Money, "Achievements"),
                     })
                c.values.Add(StatRow(stats, icon, color, label));
            c.stats = stats.gameObject;
            c.empty = UIFactory.Text(card.transform, "Empty slot\n<size=70%><color=#8F8A7C>Start a new game here. Your other saves are not touched.</color></size>", 26, UIStyle.Muted, true, TextAlignmentOptions.Center);
            c.empty.Layout(-1, -1, -1, 1);

            c.play = UIFactory.Button(card.transform, "Continue", () => Play(slot), 24, ButtonStyle.Primary, "play");
            c.play.Layout(-1, 62);
            c.delete = UIFactory.Button(card.transform, "Delete save", () => Delete(slot), 18, ButtonStyle.Ghost, "close");
            c.delete.Layout(-1, 44);
            return c;
        }

        /// <summary>Cloud status along the bottom: synced account, or an invitation to sign in.</summary>
        void BuildCloudBar(RectTransform body)
        {
            var bar = UIFactory.Surface(body, "Cloud", new Color(0.075f, 0.085f, 0.07f, 1f));
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0);
            bar.rectTransform.offsetMin = new Vector2(36, 28);
            bar.rectTransform.offsetMax = new Vector2(-36, 28 + 64);
            UIFactory.HStack(bar, 14f, new RectOffset(18, 10, 6, 6));
            m_CloudIcon = UIFactory.Icon(bar.transform, "cloud", UIStyle.Accent, 30f);
            m_CloudText = UIFactory.Text(bar.transform, "", 18, UIStyle.Muted);
            m_CloudText.Layout(-1, -1, 1);
            m_CloudButton = UIFactory.Button(bar.transform, "Sign in", CloudAction, 19, ButtonStyle.Ghost, "cloud");
            m_CloudButton.Layout(200, 50);
        }

        void CloudAction()
        {
            var c = CloudSync.Instance;
            if (c == null) return;
            if (c.SignedIn) _ = c.SyncAll();
            else c.SignIn();
        }

        void OnCloudChanged()
        {
            if (IsOpen) Refresh();
        }

        void RefreshCloud()
        {
            var c = CloudSync.Instance;
            var state = c != null ? c.State : CloudState.Unavailable;
            bool signedIn = c != null && c.SignedIn;
            m_CloudButton.gameObject.SetActive(state != CloudState.Unavailable && state != CloudState.SigningIn);
            m_CloudButton.Label.text = signedIn ? "Sync now" : "Sign in";
            m_CloudButton.interactable = state != CloudState.Syncing;
            m_CloudIcon.color = signedIn && state == CloudState.Synced ? UIStyle.Good : state == CloudState.Unavailable ? UIStyle.Dim : UIStyle.Accent;
            m_CloudText.text = state switch
            {
                CloudState.Unavailable => "Cloud saves aren't available in this version.",
                CloudState.SigningIn => "Finish signing in in your browser...",
                CloudState.Syncing => "Syncing your saves...",
                CloudState.Synced => $"<color=#F2EEDF>Cloud saves on</color>  ·  {c.Account}",
                CloudState.Offline => signedIn ? "Offline: saves stay on this device and sync when you're back online." : "Offline.",
                _ => "<color=#F2EEDF>Play on PC and phone:</color> sign in to keep these saves on all your devices.",
            };
        }

        static TextMeshProUGUI StatRow(RectTransform parent, string icon, Color color, string label)
        {
            var row = UIFactory.Panel(parent, label, new Color(1f, 0.95f, 0.82f, 0.035f));
            UIFactory.HStack(row, 10f, new RectOffset(12, 14, 7, 7));
            UIFactory.Icon(row.transform, icon, color, 20f);
            var l = UIFactory.Text(row.transform, label, 17, UIStyle.Muted);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            l.Layout(-1, -1, 1);
            var v = UIFactory.Text(row.transform, "-", 19, UIStyle.Text, true, TextAlignmentOptions.Right);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            return v;
        }

        void OnDestroy()
        {
            CloudSync.Changed -= OnCloudChanged;
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            AudioManager.Ui("menu_open", 0.6f);
            Refresh();
        }

        /// <summary>The save's numbers: the live game for the loaded slot, the file for the others.</summary>
        static SaveData Data(int slot)
        {
            var g = Game.Instance;
            if (slot == SaveSystem.CurrentSlot) return g.Model.State.HasPlayed ? g.Model.State.ToSave() : null;
            return Game.SavingEnabled ? SaveSystem.Peek(slot) : null;
        }

        void Refresh()
        {
            var db = Game.Instance.Db;
            var axe = db.Upgrade("axe_tier");
            int last = GameSettings.Data.lastSlot;
            RefreshCloud();
            for (int i = 0; i < 3; i++)
            {
                var c = m_Cards[i];
                var d = Data(i);
                bool used = d != null && d.hasPlayed;
                c.stats.SetActive(used);
                c.empty.gameObject.SetActive(!used);
                c.delete.gameObject.SetActive(used);
                c.play.Label.text = used ? "Continue" : "New Game";
                c.badge.color = used ? UIStyle.Accent : new Color(0.36f, 0.37f, 0.34f);
                bool isLast = used && i == last;
                c.glow.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, isLast ? 0.12f : 0f);
                if (!used)
                {
                    c.tag.text = "Empty";
                    continue;
                }
                var when = new DateTime(Math.Max(DateTime.MinValue.Ticks, Math.Min(DateTime.MaxValue.Ticks, d.lastSavedUtcTicks)), DateTimeKind.Utc).ToLocalTime();
                c.tag.text = (isLast ? "<color=#F5B545>Last played</color>  ·  " : "") + when.ToString("MMM d, HH:mm")
                             + (CloudSync.IsSlotSynced(i) ? "  ·  <color=#86CC61>In the cloud</color>" : "");
                int axeLevel = 0;
                foreach (var u in d.upgrades) if (u.id == "axe_tier") axeLevel = u.level;
                c.values[0].text = NumberUtil.Money(d.money);
                c.values[1].text = d.rebirths > 0 ? $"{d.rebirths}  <size=80%><color=#B49EE6>x{Rebirth.MultiplierFor(db, d):0.0#}</color></size>" : "0";
                c.values[2].text = NumberUtil.Format(d.treesFelled);
                c.values[3].text = NumberUtil.Format(d.totalWoodChopped);
                c.values[4].text = (axe != null ? axe.LevelName(axeLevel) : null) ?? db.config.baseAxeName;
                c.values[5].text = NumberUtil.Duration(d.playSeconds);
                c.values[6].text = $"{(d.achievements != null ? d.achievements.Count : 0)} / {Achievements.All.Length}";
            }
        }

        void Play(int slot)
        {
            Close();
            Game.Instance.PlaySlot(slot);
        }

        void Delete(int slot)
        {
            ConfirmDialog.Ask($"Delete save {SaveSystem.SlotNames[slot]}?", "All progress in this slot is erased: money, upgrades, machines, rebirths and Legacy perks. Your other saves and settings are kept.",
                "Delete", () =>
                {
                    Game.Instance.DeleteSlot(slot);
                    if (IsOpen) Refresh();
                    Toasts.Show($"Save {SaveSystem.SlotNames[slot]} deleted", Toasts.Kind.Info);
                });
        }

        /// <summary>A controller starts on the last-played slot's button.</summary>
        public override GameObject DefaultSelection() => m_Cards[Mathf.Clamp(GameSettings.Data.lastSlot, 0, 2)].play.gameObject;
    }
}
