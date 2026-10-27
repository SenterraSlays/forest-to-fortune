using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// One upgrade in the skill tree: a metal-rimmed round node with icon, a progress arc for
    /// multi-level upgrades, a corner badge for its state, and a level counter and name beneath.
    /// Milestones (new axes, machines, forests, harvesters) are larger and get a
    /// second outer ring. States differ by shape as well as colour: a lock badge (locked), an
    /// up-arrow badge and a breathing ring (affordable), the progress arc (owned) and a gold check
    /// badge (maxed). Hover previews it in the detail panel, click selects it, double-click buys.
    /// </summary>
    public class SkillNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public enum State { Locked, Available, Affordable, Owned, Maxed }

        public UpgradeDef Def { get; private set; }
        public State Current { get; private set; }
        public RectTransform Rect { get; private set; }
        public bool Milestone { get; private set; }
        public bool Selected { get; private set; }
        public float Size => Milestone ? MilestoneDiameter : Diameter;

        public const float Diameter = 84f, MilestoneDiameter = 104f;

        RectTransform m_Body;
        Image m_Halo, m_Glow, m_Outer, m_Breath, m_Select, m_Bezel, m_Arc, m_Face, m_Icon, m_BadgeBg, m_BadgeIcon, m_Pill;
        TextMeshProUGUI m_Level, m_Name;
        Action<SkillNode> m_OnHover, m_OnClick, m_OnDouble;
        float m_Pop, m_Hover, m_HoverTarget, m_SelectT, m_Phase;
        float m_LastClick = -10f;

        /// <summary>Upgrades that open something new (a new axe, machine, forest or harvester)
        /// rather than adding a percentage.</summary>
        public static bool IsMilestone(UpgradeDef u) =>
            u.effect == UpgradeEffect.AxeTier || u.effect == UpgradeEffect.ForestArea || u.effect == UpgradeEffect.MachineTier
            || u.effect == UpgradeEffect.AutoSell || u.effect == UpgradeEffect.MachineCount;

        public static SkillNode Create(RectTransform parent, UpgradeDef def, Vector2 pos, Action<SkillNode> hover, Action<SkillNode> click, Action<SkillNode> doubleClick)
        {
            bool milestone = IsMilestone(def);
            float d = milestone ? MilestoneDiameter : Diameter;
            var rt = UIFactory.Rect(def.id, parent);
            rt.Place(Vector2.zero, new Vector2(0.5f, 0.5f), pos, new Vector2(d, d));
            var n = rt.gameObject.AddComponent<SkillNode>();
            n.Rect = rt;
            n.Def = def;
            n.Milestone = milestone;
            n.m_OnHover = hover;
            n.m_OnClick = click;
            n.m_OnDouble = doubleClick;
            n.m_Phase = UnityEngine.Random.value * 10f;
            // Selectable so a controller can move between nodes with the D-pad (A selects, A again buys).
            var sel = rt.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;

            // Everything that scales on hover and purchase lives under Body; the counter and name stay put.
            n.m_Body = UIFactory.Rect("Body", rt);
            n.m_Body.Stretch();
            var body = n.m_Body;

            n.m_Halo = Layer(body, "Halo", UIIcons.Glow, d * 2.0f);
            n.m_Glow = Layer(body, "Glow", UIIcons.Glow, d * 1.55f);
            var shadow = Layer(body, "Shadow", UIIcons.Glow, d * 1.18f);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -6);
            shadow.color = new Color(0, 0, 0, 0.8f);
            n.m_Outer = Layer(body, "Outer", UIIcons.ThinRing, d + 14f);
            n.m_Outer.gameObject.SetActive(milestone);
            n.m_Breath = Layer(body, "Breath", UIIcons.ThinRing, d + 9f);
            n.m_Select = Layer(body, "Select", UIIcons.ThinRing, d + 22f);
            n.m_Bezel = Layer(body, "Bezel", UIIcons.Bezel, d);
            n.m_Bezel.raycastTarget = true;
            n.m_Arc = Layer(body, "Arc", UIIcons.ArcRing, d);
            n.m_Arc.type = Image.Type.Filled;
            n.m_Arc.fillMethod = Image.FillMethod.Radial360;
            n.m_Arc.fillOrigin = (int)Image.Origin360.Top;
            n.m_Arc.fillClockwise = true;
            n.m_Face = Layer(body, "Face", UIIcons.Face, d * 0.8f);
            n.m_Icon = UIFactory.Icon(body, string.IsNullOrEmpty(def.icon) ? "dot" : def.icon, UIStyle.Text, d * 0.45f);
            n.m_Icon.rectTransform.anchoredPosition = Vector2.zero;

            // State badge on the upper right of the rim.
            float bs = milestone ? 32f : 28f;
            n.m_BadgeBg = UIFactory.Panel(body, "Badge", UIStyle.Inset, false);
            n.m_BadgeBg.sprite = UIIcons.Face;
            n.m_BadgeBg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(d * 0.354f, d * 0.354f), new Vector2(bs, bs));
            Layer(n.m_BadgeBg.rectTransform, "Rim", UIIcons.ThinRing, bs).color = new Color(0, 0, 0, 0.65f);
            n.m_BadgeIcon = UIFactory.Icon(n.m_BadgeBg.transform, "lock", UIStyle.Muted, bs * 0.58f);
            n.m_BadgeIcon.rectTransform.anchoredPosition = Vector2.zero;

            // Level counter just under the rim (clear of the icon), name under that.
            n.m_Level = UIFactory.Pill(rt, "", UIStyle.Inset, UIStyle.Text, 14f);
            n.m_Pill = n.m_Level.transform.parent.GetComponent<Image>();
            var pillRt = n.m_Pill.rectTransform;
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0.5f, 0f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.anchoredPosition = new Vector2(0, -2f);
            n.m_Pill.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(9, 9, 2, 3);
            UIFactory.Border(pillRt, new Color(1f, 0.95f, 0.82f, 0.12f));

            // The name sits on a dark plate, so links passing behind it never cut through the text.
            var plate = UIFactory.Panel(rt, "NamePlate", new Color(0.035f, 0.045f, 0.038f, 0.8f));
            plate.sprite = UIStyle.RoundedSmall;
            plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -16f), Vector2.zero);
            UIFactory.HStack(plate, 0f, new RectOffset(8, 8, 1, 3), TextAnchor.MiddleCenter);
            UIFactory.Fit(plate, true, true);
            n.m_Name = UIFactory.Text(plate.transform, def.displayName, milestone ? 17f : 16f, UIStyle.Text, true, TextAlignmentOptions.Center);
            n.m_Name.textWrappingMode = TextWrappingModes.NoWrap;
            return n;
        }

        static Image Layer(RectTransform parent, string name, Sprite sprite, float size)
        {
            var img = UIFactory.Panel(parent, name, Color.white, false);
            img.sprite = sprite;
            img.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            return img;
        }

        public void Refresh(GameModel model, bool selected)
        {
            var up = model.Upgrades;
            int lv = up.Level(Def);
            bool maxed = Def.IsMaxed(lv);
            bool locked = !maxed && up.IsLocked(Def);
            bool affordable = !maxed && !locked && model.State.Money + 1e-9 >= up.NextCost(Def);
            Current = maxed ? State.Maxed : locked ? State.Locked : affordable ? State.Affordable : lv > 0 ? State.Owned : State.Available;
            Selected = selected;

            Color cat = UIStyle.CategoryColor(Def.category);
            Color gold = UIStyle.Money;
            Color steel = new Color(0.42f, 0.43f, 0.40f);
            Color face = new Color(0.12f, 0.135f, 0.11f);
            switch (Current)
            {
                case State.Locked:
                    m_Bezel.color = new Color(0.30f, 0.31f, 0.29f);
                    m_Face.color = new Color(0.085f, 0.095f, 0.08f);
                    m_Icon.color = new Color(UIStyle.Dim.r, UIStyle.Dim.g, UIStyle.Dim.b, 0.6f);
                    break;
                case State.Available:
                    m_Bezel.color = Color.Lerp(steel, cat, lv > 0 ? 0.75f : 0.4f);
                    m_Face.color = Color.Lerp(face, cat, lv > 0 ? 0.2f : 0.07f);
                    m_Icon.color = Color.Lerp(UIStyle.Text, UIStyle.Dim, lv > 0 ? 0.05f : 0.3f);
                    break;
                case State.Affordable:
                    m_Bezel.color = Color.Lerp(cat, Color.white, 0.08f);
                    m_Face.color = Color.Lerp(face, cat, lv > 0 ? 0.24f : 0.13f);
                    m_Icon.color = UIStyle.Text;
                    break;
                case State.Owned:
                    m_Bezel.color = cat;
                    m_Face.color = Color.Lerp(face, cat, 0.26f);
                    m_Icon.color = UIStyle.Text;
                    break;
                default:
                    m_Bezel.color = gold;
                    m_Face.color = Color.Lerp(new Color(0.13f, 0.12f, 0.08f), gold, 0.28f);
                    m_Icon.color = Color.Lerp(gold, Color.white, 0.35f);
                    break;
            }

            // Progress arc for levelled upgrades (single ones are simply owned or not).
            bool multi = Def.maxLevel > 1;
            m_Arc.gameObject.SetActive(multi && lv > 0);
            m_Arc.fillAmount = Def.maxLevel <= 0 ? 0f : lv / (float)Def.maxLevel;
            m_Arc.color = maxed ? Color.Lerp(gold, Color.white, 0.3f) : Color.Lerp(cat, Color.white, 0.35f);

            m_Outer.color = Current == State.Locked ? new Color(0.4f, 0.4f, 0.37f, 0.25f)
                : maxed ? new Color(gold.r, gold.g, gold.b, 0.6f) : new Color(cat.r, cat.g, cat.b, lv > 0 ? 0.55f : 0.3f);

            // Badge: lock / can-buy arrow / completed check; none for the in-between states.
            bool badge = Current == State.Locked || Current == State.Affordable || Current == State.Maxed;
            m_BadgeBg.gameObject.SetActive(badge);
            if (badge)
            {
                string icon = Current == State.Locked ? "lock" : Current == State.Maxed ? "check" : "arrow_up";
                if (m_BadgeIcon.sprite == null || m_BadgeIcon.sprite.name != icon) m_BadgeIcon.sprite = UIIcons.Get(icon);
                m_BadgeBg.color = Current == State.Locked ? new Color(0.13f, 0.14f, 0.12f) : Current == State.Maxed ? gold : UIStyle.Accent;
                m_BadgeIcon.color = Current == State.Locked ? UIStyle.Muted : UIStyle.AccentDark;
            }

            // Counter: "3/15" for levelled upgrades, the price for single ones, OWNED/MAX when done.
            m_Level.text = maxed ? (multi ? "MAX" : "OWNED") : multi ? $"{lv}/{Def.maxLevel}" : NumberUtil.Money(up.NextCost(Def));
            m_Pill.color = maxed ? gold : Current == State.Affordable ? UIStyle.Accent : new Color(0.07f, 0.08f, 0.065f, 0.96f);
            m_Level.color = maxed || Current == State.Affordable ? UIStyle.AccentDark : Current == State.Locked ? UIStyle.Dim : UIStyle.Muted;
            m_Name.color = Current == State.Locked ? Color.Lerp(UIStyle.Dim, UIStyle.Muted, 0.35f) : UIStyle.Text;
        }

        public void Pop() => m_Pop = 1f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime + m_Phase;
            float ease = 1f - Mathf.Exp(-14f * dt);
            m_Hover = Mathf.Lerp(m_Hover, m_HoverTarget, ease);
            m_SelectT = Mathf.Lerp(m_SelectT, Selected ? 1f : 0f, ease);
            m_Pop = Mathf.MoveTowards(m_Pop, 0f, dt * 2.2f);

            Color cat = Current == State.Maxed ? UIStyle.Money : UIStyle.CategoryColor(Def.category);
            bool lit = Current == State.Owned || Current == State.Maxed || Current == State.Affordable;

            // Restrained glow: steady on owned/maxed nodes only; brighter on hover and purchase.
            float glow = (Current == State.Owned || Current == State.Maxed ? 0.16f : 0f) + (lit ? 0.12f : 0.05f) * m_Hover + 0.7f * m_Pop;
            m_Glow.color = new Color(cat.r, cat.g, cat.b, glow);

            // Selected: a soft breathing halo and a crisp outline ring that eases in.
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            m_Halo.color = new Color(cat.r, cat.g, cat.b, m_SelectT * (0.22f + 0.12f * breathe));
            m_Halo.rectTransform.localScale = Vector3.one * (0.92f + 0.08f * breathe);
            m_Select.color = new Color(1f, 0.93f, 0.78f, 0.9f * m_SelectT);
            m_Select.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, m_SelectT);

            // Affordable: a thin ring that slowly breathes, rather than a constant glow.
            float br = Current == State.Affordable ? 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 3f)) : 0f;
            m_Breath.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, br * (1f - m_SelectT * 0.6f));
            m_Breath.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 3f));

            m_Body.localScale = Vector3.one * (1f + 0.07f * m_Hover + 0.16f * Mathf.Sin(m_Pop * Mathf.PI) * m_Pop);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            m_HoverTarget = 1f;
            AudioManager.Ui("ui_hover", 0.25f);
            m_OnHover?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData e)
        {
            m_HoverTarget = 0f;
            m_OnHover?.Invoke(null);
        }

        public void OnSelect(BaseEventData e)
        {
            m_HoverTarget = 1f;
            if (GameInput.UsingGamepad) AudioManager.Ui("ui_hover", 0.25f);
            m_OnHover?.Invoke(this);
        }

        public void OnDeselect(BaseEventData e)
        {
            m_HoverTarget = 0f;
            m_OnHover?.Invoke(null);
        }

        public void OnSubmit(BaseEventData e)
        {
            if (Selected) m_OnDouble?.Invoke(this);
            else m_OnClick?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Time.unscaledTime - m_LastClick < 0.35f) m_OnDouble?.Invoke(this);
            else m_OnClick?.Invoke(this);
            m_LastClick = Time.unscaledTime;
        }
    }
}
