using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// The banner that drops in at the top of the screen when an achievement unlocks: trophy,
    /// name and reward. Several in a row queue up; a big batch at once (an older save collecting
    /// what its progress already earned) becomes a single summary banner.
    /// </summary>
    public class AchievementPopup : MonoBehaviour
    {
        const float Life = 4.2f;
        /// <summary>This many or more unlocked in one check are shown as one summary.</summary>
        const int SummaryAt = 4;

        struct Entry
        {
            public string title, name, reward, icon;
        }

        readonly Queue<Entry> m_Queue = new Queue<Entry>();
        readonly List<AchievementDef> m_Batch = new List<AchievementDef>();
        RectTransform m_Banner;
        CanvasGroup m_Group;
        TextMeshProUGUI m_Title, m_Name, m_Reward;
        Image m_Icon, m_Glow;
        float m_Age = -1f;

        public void Create(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.Stretch();
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 55;
            gameObject.AddComponent<SafeArea>();

            var bg = UIFactory.Surface(rt, "Banner", UIStyle.Panel, true);
            m_Banner = bg.rectTransform;
            m_Banner.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(640, 112));
            m_Group = bg.gameObject.AddComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = m_Group.interactable = false;
            UIFactory.Border(bg.transform, new Color(UIStyle.Money.r, UIStyle.Money.g, UIStyle.Money.b, 0.45f));
            UIFactory.TopTrim(m_Banner, UIStyle.Money);
            UIFactory.HStack(bg, 18f, new RectOffset(20, 24, 14, 14));

            var badge = UIFactory.Rect("Badge", bg.transform);
            badge.Layout(80, 80);
            m_Glow = UIFactory.Panel(badge, "Glow", new Color(UIStyle.Money.r, UIStyle.Money.g, UIStyle.Money.b, 0.35f), false);
            m_Glow.sprite = UIIcons.Glow;
            m_Glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
            var bezel = UIFactory.Panel(badge, "Bezel", UIStyle.Money, false);
            bezel.sprite = UIIcons.Bezel;
            bezel.rectTransform.Stretch();
            var face = UIFactory.Panel(badge, "Face", Color.Lerp(new Color(0.13f, 0.12f, 0.08f), UIStyle.Money, 0.25f), false);
            face.sprite = UIIcons.Face;
            face.rectTransform.Stretch(8, 8, 8, 8);
            m_Icon = UIFactory.Icon(badge, "trophy", Color.Lerp(UIStyle.Money, Color.white, 0.4f), 40f);
            m_Icon.rectTransform.anchoredPosition = Vector2.zero;

            var text = UIFactory.Rect("Text", bg.transform);
            text.Layout(-1, -1, 1);
            UIFactory.VStack(text, 2f, null, TextAnchor.MiddleLeft);
            m_Title = UIFactory.Caption(text, "Achievement unlocked", UIStyle.Money, 13f);
            m_Title.characterSpacing = 8f;
            m_Name = UIFactory.Text(text, "", 26, UIStyle.Text, true);
            m_Name.textWrappingMode = TextWrappingModes.NoWrap;
            m_Name.overflowMode = TextOverflowModes.Ellipsis;
            m_Reward = UIFactory.Text(text, "", 17, UIStyle.Good);
            m_Reward.textWrappingMode = TextWrappingModes.NoWrap;
            m_Reward.overflowMode = TextOverflowModes.Ellipsis;

            Game.Instance.Model.AchievementUnlocked += a => m_Batch.Add(a);
        }

        void Update()
        {
            // Unlocks raised in the same check arrive together: show them one by one, or as a summary.
            if (m_Batch.Count > 0)
            {
                var db = Game.Instance.Db;
                if (m_Batch.Count >= SummaryAt)
                    m_Queue.Enqueue(new Entry
                    {
                        title = "Achievements unlocked",
                        name = $"{m_Batch.Count} achievements",
                        reward = "Earned by your progress so far. Rewards collected!",
                        icon = "trophy",
                    });
                else
                    foreach (var a in m_Batch)
                        m_Queue.Enqueue(new Entry { title = "Achievement unlocked", name = a.name, reward = "Reward: " + Achievements.RewardText(db, a), icon = a.icon });
                m_Batch.Clear();
            }

            if (m_Age < 0f)
            {
                if (m_Queue.Count == 0) return;
                var e = m_Queue.Dequeue();
                m_Title.text = e.title.ToUpperInvariant();
                m_Name.text = e.name;
                m_Reward.text = e.reward;
                m_Icon.sprite = UIIcons.Get(e.icon);
                m_Age = 0f;
                AudioManager.Ui("unlock", 0.9f);
            }

            m_Age += Time.unscaledDeltaTime;
            // Drops in with a little overshoot, glows, then slides back up.
            float inT = Mathf.Clamp01(m_Age / 0.35f);
            float outT = Mathf.Clamp01((m_Age - (Life - 0.4f)) / 0.4f);
            float drop = 1f - Mathf.Pow(1f - inT, 3f);
            float y = Mathf.Lerp(80f, -30f, drop) + Mathf.Sin(inT * Mathf.PI) * -8f + outT * 120f;
            m_Banner.anchoredPosition = new Vector2(0f, y);
            m_Group.alpha = Mathf.Min(inT * 1.5f, 1f - outT);
            m_Glow.color = new Color(UIStyle.Money.r, UIStyle.Money.g, UIStyle.Money.b, 0.25f + 0.15f * Mathf.Sin(m_Age * 5f));
            if (m_Age >= Life) m_Age = -1f;
        }
    }
}
