using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Shown when a save slot was played on this device and on another one since they last
    /// synced: both versions side by side (when, money, rebirths, trees, wood, time played) and
    /// the player keeps one. Closing it decides later and leaves both untouched.
    /// </summary>
    public class CloudConflictDialog : UIScreen
    {
        public enum Choice { Later, Device, Cloud }

        public static CloudConflictDialog Instance { get; private set; }

        TextMeshProUGUI m_Title;
        readonly TextMeshProUGUI[] m_When = new TextMeshProUGUI[2];
        readonly List<TextMeshProUGUI>[] m_Values = { new List<TextMeshProUGUI>(), new List<TextMeshProUGUI>() };
        readonly Image[] m_Newer = new Image[2];
        UIButton m_KeepDevice;
        TaskCompletionSource<Choice> m_Pending;

        protected override float FadeSpeed => 12f;

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", new Color(0, 0, 0, 0.7f), false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Which save do you keep?", "save", new Vector2(980, 690), out var body,
                "This save was played on another device too since they last synced");
            UIFactory.TopTrim(window.rectTransform, UIStyle.Accent);
            UIFactory.VStack(body, 18f, new RectOffset(36, 36, 22, 30));
            m_Title = UIFactory.Text(body, "", 21, UIStyle.Muted);

            var row = UIFactory.Rect("Versions", body);
            var h = UIFactory.HStack(row, 20f, null, TextAnchor.UpperCenter);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            row.Layout(-1, -1, -1, 1);
            BuildCard(row, 0, "This device", "chart");
            BuildCard(row, 1, "Cloud", "cloud");

            var buttons = UIFactory.Rect("Buttons", body);
            var bh = UIFactory.HStack(buttons, 20f);
            bh.childForceExpandWidth = true;
            m_KeepDevice = UIFactory.Button(buttons, "Keep this device's", () => Decide(Choice.Device), 22, ButtonStyle.Primary, "chart");
            m_KeepDevice.Layout(-1, 60, 1);
            UIFactory.Button(buttons, "Use the cloud save", () => Decide(Choice.Cloud), 22, ButtonStyle.Primary, "cloud").Layout(-1, 60, 1);
            var later = UIFactory.Text(body, "Not sure? Press Back to decide later; nothing is overwritten until you choose.", 16, UIStyle.Dim, false, TextAlignmentOptions.Center);
            later.Layout(-1, 22);
        }

        void BuildCard(RectTransform parent, int index, string title, string icon)
        {
            var card = UIFactory.Surface(parent, title, UIStyle.Card);
            UIFactory.VStack(card, 8f, new RectOffset(22, 22, 18, 18));
            card.Layout(1, -1, 1);
            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 10f);
            UIFactory.Icon(head, icon, UIStyle.Accent, 24f);
            UIFactory.Text(head, title, 26, UIStyle.Text, true).Layout(-1, -1, 1);
            var newer = UIFactory.Panel(head, "Newer", new Color(0.52f, 0.8f, 0.36f, 0.2f));
            UIFactory.HStack(newer, 0f, new RectOffset(10, 10, 3, 4));
            UIFactory.Fit(newer, true, true);
            UIFactory.Text(newer.transform, "Newer", 15, UIStyle.Good, true).textWrappingMode = TextWrappingModes.NoWrap;
            m_Newer[index] = newer;
            m_When[index] = UIFactory.Text(card.transform, "", 17, UIStyle.Muted);
            foreach (var (ic, color, label) in new[]
                     {
                         ("coin", UIStyle.Money, "Money"), ("rebirth", UIStyle.Skill, "Rebirths"), ("pine", UIStyle.Good, "Trees felled"),
                         ("log", new Color(0.78f, 0.56f, 0.33f), "Wood chopped"), ("clock", UIStyle.CategoryColor(UpgradeCategory.Forest), "Time played"),
                     })
            {
                var r = UIFactory.Panel(card.transform, label, new Color(1f, 0.95f, 0.82f, 0.035f));
                UIFactory.HStack(r, 10f, new RectOffset(12, 14, 7, 7));
                UIFactory.Icon(r.transform, ic, color, 20f);
                var l = UIFactory.Text(r.transform, label, 17, UIStyle.Muted);
                l.textWrappingMode = TextWrappingModes.NoWrap;
                l.Layout(-1, -1, 1);
                var v = UIFactory.Text(r.transform, "-", 19, UIStyle.Text, true, TextAlignmentOptions.Right);
                v.textWrappingMode = TextWrappingModes.NoWrap;
                m_Values[index].Add(v);
            }
        }

        void Fill(int index, SaveData d, bool newer)
        {
            var when = new DateTime(Math.Max(DateTime.MinValue.Ticks, Math.Min(DateTime.MaxValue.Ticks, d.lastSavedUtcTicks)), DateTimeKind.Utc).ToLocalTime();
            m_When[index].text = "Last saved " + when.ToString("MMM d, HH:mm");
            m_Newer[index].gameObject.SetActive(newer);
            var v = m_Values[index];
            v[0].text = NumberUtil.Money(d.money);
            v[1].text = d.rebirths.ToString();
            v[2].text = NumberUtil.Format(d.treesFelled);
            v[3].text = NumberUtil.Format(d.totalWoodChopped);
            v[4].text = NumberUtil.Duration(d.playSeconds);
        }

        /// <summary>Asks which version of a slot to keep; resolves when the player decides.</summary>
        public static Task<Choice> Ask(int slot, SaveData device, SaveData cloud)
        {
            var d = Instance;
            if (d == null || device == null || cloud == null) return Task.FromResult(Choice.Later);
            d.m_Pending?.TrySetResult(Choice.Later);
            d.m_Pending = new TaskCompletionSource<Choice>();
            d.m_Title.text = $"<b><color=#F5B545>Save {SaveSystem.SlotNames[slot]}</color></b> has different progress here and in the cloud. Pick the one to keep on every device.";
            bool deviceNewer = device.lastSavedUtcTicks >= cloud.lastSavedUtcTicks;
            d.Fill(0, device, deviceNewer);
            d.Fill(1, cloud, !deviceNewer);
            d.Open();
            return d.m_Pending.Task;
        }

        void Decide(Choice c)
        {
            var p = m_Pending;
            m_Pending = null;
            Close();
            p?.TrySetResult(c);
        }

        protected override void OnOpened()
        {
            transform.SetAsLastSibling();
            AudioManager.Ui("menu_open", 0.6f);
        }

        protected override void OnClosed()
        {
            var p = m_Pending;
            m_Pending = null;
            p?.TrySetResult(Choice.Later);
        }

        public override GameObject DefaultSelection() => m_KeepDevice.gameObject;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            m_Pending?.TrySetResult(Choice.Later);
        }
    }
}
