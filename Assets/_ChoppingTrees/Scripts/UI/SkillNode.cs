using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>One upgrade in the skill tree: a round node with icon, level badge and name.
    /// Hover previews it in the detail panel, click selects it, double-click buys a level.</summary>
    public class SkillNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum State { Locked, Available, Affordable, Owned, Maxed }

        public UpgradeDef Def { get; private set; }
        public State Current { get; private set; }
        public RectTransform Rect { get; private set; }

        Image m_Glow, m_Ring, m_Fill, m_Icon, m_Select;
        TextMeshProUGUI m_Level, m_Name;
        Image m_LockBadge;
        Action<SkillNode> m_OnHover, m_OnClick, m_OnDouble;
        float m_Pop, m_Hover;
        float m_LastClick = -10f;

        public const float Diameter = 86f;

        public static SkillNode Create(RectTransform parent, UpgradeDef def, Vector2 pos, Action<SkillNode> hover, Action<SkillNode> click, Action<SkillNode> doubleClick)
        {
            var rt = UIFactory.Rect(def.id, parent);
            rt.Place(new Vector2(0, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(Diameter, Diameter));
            var n = rt.gameObject.AddComponent<SkillNode>();
            n.Rect = rt;
            n.Def = def;
            n.m_OnHover = hover;
            n.m_OnClick = click;
            n.m_OnDouble = doubleClick;

            n.m_Glow = UIFactory.Panel(rt, "Glow", Color.clear, false);
            n.m_Glow.sprite = UIIcons.Glow;
            n.m_Glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * Diameter * 2.1f);

            n.m_Select = UIFactory.Panel(rt, "Select", UIStyle.Text, false);
            n.m_Select.sprite = UIStyle.Circle;
            n.m_Select.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (Diameter + 14f));

            n.m_Ring = UIFactory.Panel(rt, "Ring", UIStyle.Dim, false);
            n.m_Ring.sprite = UIStyle.Circle;
            n.m_Ring.rectTransform.Stretch();
            n.m_Ring.raycastTarget = true;
            var sh = n.m_Ring.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.5f);
            sh.effectDistance = new Vector2(0, -4);

            n.m_Fill = UIFactory.Panel(rt, "Fill", UIStyle.Card, false);
            n.m_Fill.sprite = UIStyle.Circle;
            n.m_Fill.rectTransform.Stretch(6, 6, 6, 6);

            n.m_Icon = UIFactory.Icon(rt, string.IsNullOrEmpty(def.icon) ? "dot" : def.icon, UIStyle.Text, 40f);
            n.m_Icon.rectTransform.anchoredPosition = Vector2.zero;

            var badge = UIFactory.Rect("Badge", rt);
            badge.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(80, 26));
            UIFactory.HStack(badge, 0f, null, TextAnchor.MiddleCenter);
            n.m_Level = UIFactory.Pill(badge, "", UIStyle.Inset, UIStyle.Text, 15f);

            n.m_LockBadge = UIFactory.Panel(rt, "Lock", UIStyle.Inset, false);
            n.m_LockBadge.sprite = UIStyle.Circle;
            n.m_LockBadge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(34, 34));
            UIFactory.Border(n.m_LockBadge.transform, UIStyle.Border);
            UIFactory.Icon(n.m_LockBadge.transform, "lock", UIStyle.Muted, 22f).rectTransform.anchoredPosition = Vector2.zero;

            n.m_Name = UIFactory.Text(rt, def.displayName, 17, UIStyle.Text, true, TextAlignmentOptions.Top);
            n.m_Name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(170, 44));
            n.m_Name.outlineWidth = 0.18f;
            n.m_Name.outlineColor = new Color32(10, 12, 9, 220);
            return n;
        }

        public void Refresh(GameModel model, bool selected)
        {
            var up = model.Upgrades;
            int lv = up.Level(Def);
            bool maxed = Def.IsMaxed(lv);
            bool locked = !maxed && up.IsLocked(Def);
            bool affordable = !maxed && !locked && model.State.Money + 1e-9 >= up.NextCost(Def);
            Current = maxed ? State.Maxed : locked ? State.Locked : affordable ? State.Affordable : lv > 0 ? State.Owned : State.Available;

            Color cat = UIStyle.CategoryColor(Def.category);
            Color ring = Current switch
            {
                State.Maxed => UIStyle.Money,
                State.Locked => new Color(0.28f, 0.29f, 0.26f),
                State.Available => Color.Lerp(cat, UIStyle.Card, 0.45f),
                _ => cat,
            };
            m_Ring.color = ring;
            m_Fill.color = Current == State.Maxed ? Color.Lerp(UIStyle.Card, UIStyle.Money, 0.18f)
                : Current == State.Locked ? UIStyle.Inset : Color.Lerp(UIStyle.Card, cat, lv > 0 ? 0.22f : 0.08f);
            m_Icon.color = Current == State.Locked ? UIStyle.Dim : Current == State.Maxed ? UIStyle.Money : lv > 0 || affordable ? UIStyle.Text : Color.Lerp(UIStyle.Text, UIStyle.Dim, 0.4f);
            m_Name.color = Current == State.Locked ? UIStyle.Dim : UIStyle.Text;
            m_LockBadge.gameObject.SetActive(Current == State.Locked);

            string level = maxed ? (Def.maxLevel == 1 ? "OWNED" : "MAX") : Def.maxLevel == 1 ? NumberUtil.Money(up.NextCost(Def)) : $"{lv}/{Def.maxLevel}";
            m_Level.text = level;
            var pill = m_Level.transform.parent.GetComponent<Image>();
            pill.color = maxed ? UIStyle.Money : affordable ? UIStyle.Accent : UIStyle.Inset;
            m_Level.color = maxed || affordable ? UIStyle.AccentDark : UIStyle.Muted;
            m_Select.color = selected ? new Color(1f, 0.95f, 0.85f, 0.9f) : Color.clear;
        }

        public void Pop() => m_Pop = 1f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_Pop = Mathf.MoveTowards(m_Pop, 0f, dt * 3f);
            float pulse = Current == State.Affordable ? 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f) : 0f;
            var cat = UIStyle.CategoryColor(Def.category);
            m_Glow.color = new Color(cat.r, cat.g, cat.b, pulse + m_Pop * 0.8f);
            float s = 1f + 0.06f * m_Hover + 0.18f * Mathf.Sin(m_Pop * Mathf.PI);
            Rect.localScale = Vector3.one * s;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            m_Hover = 1f;
            AudioManager.Ui("ui_hover", 0.25f);
            m_OnHover?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData e) => m_Hover = 0f;

        public void OnPointerClick(PointerEventData e)
        {
            if (Time.unscaledTime - m_LastClick < 0.35f) m_OnDouble?.Invoke(this);
            else m_OnClick?.Invoke(this);
            m_LastClick = Time.unscaledTime;
        }
    }
}
