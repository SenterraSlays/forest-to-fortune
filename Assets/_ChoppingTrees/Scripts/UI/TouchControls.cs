using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ChoppingTrees
{
    /// <summary>
    /// On-screen controls for phones and tablets, shown while playing with the touch screen.
    ///   * Left side: a floating move stick that appears under the thumb (or a fixed one, in
    ///     Settings). Pushing it all the way forward runs.
    ///   * Anywhere else: drag to look around.
    ///   * Right side: a big axe button (hold to chop; drag on it to aim at the same time), jump,
    ///     and a use button that appears with a label when something can be used.
    ///   * Top right: the menu.
    /// Every finger is tracked on its own, so moving, looking and chopping work together. The
    /// results go to <see cref="GameInput"/> like any other input; presses give a light buzz.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class TouchControls : MonoBehaviour
    {
        enum Role { None, Stick, Look, Chop, Jump, Use, Menu }

        class RoundButton
        {
            public RectTransform rect;
            public Image fill, ring, icon;
            public float pressed;
            public bool held;
        }

        RectTransform m_Root, m_StickArea;
        CanvasGroup m_Group;
        Interactor m_Interactor;

        // Stick
        RectTransform m_StickBase, m_Knob;
        Image m_KnobFill, m_StickRing, m_RunIcon;
        CanvasGroup m_RunGroup;
        Vector2 m_StickRest = new Vector2(250f, 230f);
        const float StickRadius = 100f;
        Vector2 m_StickCenter;
        float m_RunGlow;

        RoundButton m_Chop, m_Jump, m_Use, m_Menu;
        TextMeshProUGUI m_UseLabel;
        CanvasGroup m_UseGroup;
        float m_UseShown;

        readonly Dictionary<int, Role> m_Roles = new Dictionary<int, Role>();
        bool m_Active;
        float m_Alpha;

        public void Create(RectTransform parent, Interactor interactor)
        {
            m_Interactor = interactor;
            m_Root = (RectTransform)transform;
            m_Root.SetParent(parent, false);
            m_Root.Stretch();
            gameObject.AddComponent<SafeArea>();
            m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = false; // touches are read directly; menus stay clickable
            m_Group.interactable = false;

            // Move stick in the lower left.
            m_StickArea = UIFactory.Rect("StickArea", m_Root);
            m_StickArea.anchorMin = Vector2.zero;
            m_StickArea.anchorMax = new Vector2(0.42f, 0.78f);
            m_StickArea.offsetMin = m_StickArea.offsetMax = Vector2.zero;
            m_StickBase = UIFactory.Rect("Stick", m_Root);
            m_StickBase.Place(Vector2.zero, new Vector2(0.5f, 0.5f), m_StickRest, new Vector2(StickRadius * 2.5f, StickRadius * 2.5f));
            var baseFill = UIFactory.Panel(m_StickBase, "Fill", new Color(0.04f, 0.05f, 0.04f, 0.38f));
            baseFill.sprite = UIStyle.Circle;
            baseFill.type = Image.Type.Simple; // a true circle, not a sliced rounded box
            baseFill.rectTransform.Stretch();
            m_StickRing = Ring(m_StickBase, StickRadius * 2.5f, new Color(1f, 0.95f, 0.82f, 0.32f));
            for (int i = 0; i < 4; i++)
            {
                var tick = UIFactory.Icon(m_StickBase, "chevron_right", new Color(1f, 0.95f, 0.82f, 0.28f), 26f);
                tick.GetComponent<LayoutElement>().ignoreLayout = true;
                float a = i * 90f;
                tick.rectTransform.localRotation = Quaternion.Euler(0, 0, a);
                tick.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, a) * new Vector3(StickRadius * 1.02f, 0f);
            }
            m_Knob = UIFactory.Rect("Knob", m_StickBase);
            m_Knob.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 118));
            var knobShadow = UIFactory.Panel(m_Knob, "Shadow", new Color(0, 0, 0, 0.3f));
            knobShadow.sprite = UIStyle.Circle;
            knobShadow.type = Image.Type.Simple; // a true circle, not a sliced rounded box
            knobShadow.rectTransform.Stretch(-6, -6, -2, -10);
            m_KnobFill = UIFactory.Panel(m_Knob, "Fill", new Color(0.93f, 0.9f, 0.82f, 0.88f));
            m_KnobFill.sprite = UIStyle.Circle;
            m_KnobFill.type = Image.Type.Simple; // a true circle, not a sliced rounded box
            m_KnobFill.rectTransform.Stretch();
            Ring(m_Knob, 118f, new Color(1f, 1f, 1f, 0.55f));
            // Run indicator above the stick: lights up when pushed all the way forward.
            var run = UIFactory.Rect("Run", m_StickBase);
            run.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(150, 40));
            m_RunGroup = run.gameObject.AddComponent<CanvasGroup>();
            UIFactory.HStack(run, 6f, null, TextAnchor.MiddleCenter);
            m_RunIcon = UIFactory.Icon(run, "run", UIStyle.Accent, 28f);
            var runText = UIFactory.Text(run, "RUN", 17, UIStyle.Accent, true);
            runText.characterSpacing = 8f;

            // Action buttons in the lower right.
            m_Chop = Button("Chop", "axe", new Vector2(-205, 215), 212f, 104f);
            m_Jump = Button("Jump", "arrow_up", new Vector2(-430, 118), 128f, 58f);
            m_Use = Button("Use", "hand", new Vector2(-412, 330), 142f, 66f);
            m_Use.ring.color = UIStyle.Accent;
            m_UseGroup = m_Use.rect.gameObject.AddComponent<CanvasGroup>();
            m_UseLabel = UIFactory.Text(m_Use.rect, "", 18, UIStyle.Text, true, TextAlignmentOptions.Center);
            m_UseLabel.textWrappingMode = TextWrappingModes.NoWrap;
            m_UseLabel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(260, 26));
            var labelShadow = m_UseLabel.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.7f);
            labelShadow.effectDistance = new Vector2(0, -2);
            m_Menu = Button("Menu", "pause", Vector2.zero, 88f, 40f);
            m_Menu.rect.anchorMin = m_Menu.rect.anchorMax = Vector2.one;
            m_Menu.rect.anchoredPosition = new Vector2(-70, -70);

            ApplyScale();
            GameSettings.Changed += ApplyScale;
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyScale;
            Release();
        }

        static Image Ring(RectTransform parent, float size, Color color)
        {
            var r = UIFactory.Icon(parent, "ring", color, size);
            r.GetComponent<LayoutElement>().ignoreLayout = true;
            r.rectTransform.anchorMin = r.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            r.rectTransform.anchoredPosition = Vector2.zero;
            r.raycastTarget = false;
            return r;
        }

        RoundButton Button(string name, string icon, Vector2 pos, float size, float iconSize)
        {
            var b = new RoundButton();
            b.rect = UIFactory.Rect(name, m_Root);
            b.rect.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var shadow = UIFactory.Panel(b.rect, "Shadow", new Color(0, 0, 0, 0.28f));
            shadow.sprite = UIStyle.Circle;
            shadow.type = Image.Type.Simple; // a true circle, not a sliced rounded box
            shadow.rectTransform.Stretch(-4, -4, 2, -8);
            b.fill = UIFactory.Panel(b.rect, "Fill", new Color(0.05f, 0.06f, 0.045f, 0.62f));
            b.fill.sprite = UIStyle.Circle;
            b.fill.type = Image.Type.Simple; // a true circle, not a sliced rounded box
            b.fill.rectTransform.Stretch();
            b.ring = Ring(b.rect, size, new Color(1f, 0.95f, 0.82f, 0.5f));
            b.icon = UIFactory.Icon(b.rect, icon, UIStyle.Text, iconSize);
            b.icon.GetComponent<LayoutElement>().ignoreLayout = true;
            b.icon.rectTransform.anchorMin = b.icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            b.icon.rectTransform.anchoredPosition = Vector2.zero;
            return b;
        }

        void ApplyScale()
        {
            float s = Mathf.Clamp(GameSettings.Data.touchButtonScale, 0.75f, 1.35f);
            foreach (var b in new[] { m_Chop, m_Jump, m_Use }) b.rect.localScale = Vector3.one * s;
            m_StickBase.localScale = Vector3.one * s;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            GameInput.ClearTouch();
            bool show = GameInput.UsingTouch && UIState.GameplayInput;
            if (show != m_Active)
            {
                m_Active = show;
                if (!show) Release();
            }
            float target = show ? Mathf.Clamp(GameSettings.Data.touchOpacity, 0.3f, 1f) : 0f;
            m_Alpha = Mathf.MoveTowards(m_Alpha, target, Time.unscaledDeltaTime * 5f);
            m_Group.alpha = m_Alpha;

            if (m_Active) ReadTouches();
            Animate();
        }

        void ReadTouches()
        {
            Vector2 look = Vector2.zero;
            foreach (var t in ETouch.activeTouches)
            {
                int id = t.finger.index;
                var pos = t.screenPosition;
                if (t.phase == TouchPhase.Began)
                {
                    var role = Classify(pos);
                    m_Roles[id] = role;
                    Begin(role, pos);
                    continue;
                }
                if (!m_Roles.TryGetValue(id, out var r)) continue;
                bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                switch (r)
                {
                    case Role.Stick:
                        if (ended) EndStick();
                        else DragStick(pos);
                        break;
                    case Role.Look:
                        if (!ended) look += t.delta;
                        break;
                    case Role.Chop:
                        // Dragging on the axe button aims while chopping.
                        if (!ended) { look += t.delta; GameInput.TouchChop = true; }
                        m_Chop.held = !ended;
                        break;
                    case Role.Jump:
                        m_Jump.held = !ended;
                        break;
                    case Role.Use:
                        m_Use.held = !ended;
                        break;
                    case Role.Menu:
                        m_Menu.held = !ended;
                        break;
                }
                if (ended) m_Roles.Remove(id);
            }
            foreach (var kv in m_Roles) if (kv.Value == Role.Chop) GameInput.TouchChop = true;

            // Swipes turn the view: a full screen-height swipe is about 120 degrees at sensitivity 1.
            var s = GameSettings.Data;
            float k = 120f / Mathf.Max(1f, Screen.height) * Mathf.Clamp(s.touchLookSensitivity, 0.2f, 3f);
            look *= k;
            if (s.touchInvertY) look.y = -look.y;
            GameInput.TouchLook = look;
        }

        Role Classify(Vector2 screen)
        {
            if (Hit(m_Menu, screen)) return Role.Menu;
            if (Hit(m_Chop, screen)) return Role.Chop;
            if (Hit(m_Jump, screen)) return Role.Jump;
            if (m_UseShown > 0.5f && Hit(m_Use, screen)) return Role.Use;
            if (RectTransformUtility.RectangleContainsScreenPoint(m_StickArea, screen, null)) return Role.Stick;
            return Role.Look;
        }

        /// <summary>Round hit test, a little larger than the button so thumbs don't miss.</summary>
        static bool Hit(RoundButton b, Vector2 screen)
        {
            Vector2 c = RectTransformUtility.WorldToScreenPoint(null, b.rect.position);
            float r = b.rect.rect.width * 0.5f * b.rect.lossyScale.x * 1.15f;
            return (screen - c).sqrMagnitude <= r * r;
        }

        void Begin(Role role, Vector2 screen)
        {
            switch (role)
            {
                case Role.Stick:
                    if (!GameSettings.Data.touchFixedStick)
                    {
                        // The stick jumps under the thumb, kept fully on screen.
                        var p = ToRoot(screen);
                        float m = StickRadius * 1.3f * m_StickBase.localScale.x;
                        p.x = Mathf.Max(p.x, m);
                        p.y = Mathf.Max(p.y, m);
                        m_StickBase.anchoredPosition = p;
                    }
                    m_StickCenter = m_StickBase.anchoredPosition;
                    DragStick(screen);
                    break;
                case Role.Chop:
                    m_Chop.held = true;
                    GameInput.TouchChop = true;
                    Haptics.Play(10, 0.25f);
                    break;
                case Role.Jump:
                    m_Jump.held = true;
                    GameInput.TouchJump = true;
                    Haptics.Play(12, 0.3f);
                    break;
                case Role.Use:
                    m_Use.held = true;
                    GameInput.TouchUse = true;
                    Haptics.Play(14, 0.35f);
                    break;
                case Role.Menu:
                    m_Menu.held = true;
                    GameInput.TouchMenu = true;
                    Haptics.Play(10, 0.25f);
                    break;
            }
        }

        void DragStick(Vector2 screen)
        {
            float scale = m_StickBase.localScale.x;
            Vector2 offset = (ToRoot(screen) - m_StickCenter) / scale;
            float radius = StickRadius;
            Vector2 clamped = Vector2.ClampMagnitude(offset, radius);
            m_Knob.anchoredPosition = clamped;
            Vector2 v = clamped / radius;
            float mag = v.magnitude;
            const float dead = 0.1f;
            v = mag < dead ? Vector2.zero : v / mag * ((mag - dead) / (1f - dead));
            GameInput.TouchMove = v;
            // Pushed to the edge, mostly forward: run. A little past the edge keeps running.
            bool run = offset.magnitude > radius * 0.96f && v.y > 0.72f;
            if (run && !GameInput.TouchSprint) Haptics.Play(18, 0.4f);
            GameInput.TouchSprint = run;
        }

        void EndStick()
        {
            GameInput.TouchMove = Vector2.zero;
            GameInput.TouchSprint = false;
            m_Knob.anchoredPosition = Vector2.zero;
        }

        Vector2 ToRoot(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Root, screen, null, out var local);
            return local - m_Root.rect.min; // bottom-left origin, like the stick's anchor
        }

        void Release()
        {
            m_Roles.Clear();
            EndStick();
            foreach (var b in new[] { m_Chop, m_Jump, m_Use, m_Menu }) if (b != null) b.held = false;
            GameInput.ClearTouch(true);
        }

        // ------------------------------------------------------------------ visuals

        void Animate()
        {
            float dt = Time.unscaledDeltaTime;
            bool stickHeld = m_Roles.ContainsValue(Role.Stick);
            if (!stickHeld)
            {
                // Idle: the stick drifts back to its corner and dims.
                Vector2 rest = m_StickRest;
                m_StickBase.anchoredPosition = Vector2.Lerp(m_StickBase.anchoredPosition, rest, 1f - Mathf.Exp(-12f * dt));
                m_Knob.anchoredPosition = Vector2.Lerp(m_Knob.anchoredPosition, Vector2.zero, 1f - Mathf.Exp(-20f * dt));
            }
            m_StickRing.color = new Color(1f, 0.95f, 0.82f, stickHeld ? 0.5f : 0.26f);
            m_RunGlow = Mathf.MoveTowards(m_RunGlow, GameInput.TouchSprint ? 1f : 0f, dt * 6f);
            m_RunGroup.alpha = stickHeld ? 0.35f + 0.65f * m_RunGlow : 0f;
            m_KnobFill.color = Color.Lerp(new Color(0.93f, 0.9f, 0.82f, 0.88f), UIStyle.Accent, m_RunGlow);

            // Use appears when the crosshair is on something usable, with its prompt underneath.
            var target = m_Interactor != null ? m_Interactor.Current : null;
            bool canUse = target != null && target.CanInteract;
            if (canUse) m_UseLabel.text = target.Prompt;
            float before = m_UseShown;
            m_UseShown = Mathf.MoveTowards(m_UseShown, canUse ? 1f : 0f, dt * 7f);
            if (before < 0.5f && m_UseShown >= 0.5f && m_Active) Haptics.Play(8, 0.2f);
            m_UseGroup.alpha = m_UseShown;
            float pop = 1f + 0.12f * Mathf.Sin(m_UseShown * Mathf.PI);
            m_Use.rect.localScale = Vector3.one * Mathf.Clamp(GameSettings.Data.touchButtonScale, 0.75f, 1.35f) * Mathf.Lerp(0.6f, 1f, m_UseShown) * pop;

            foreach (var b in new[] { m_Chop, m_Jump, m_Use, m_Menu })
            {
                b.pressed = Mathf.MoveTowards(b.pressed, b.held ? 1f : 0f, dt * 14f);
                b.fill.color = Color.Lerp(new Color(0.05f, 0.06f, 0.045f, 0.62f), new Color(0.96f, 0.71f, 0.27f, 0.55f), b.pressed);
                b.icon.color = Color.Lerp(UIStyle.Text, UIStyle.AccentDark, b.pressed);
                b.icon.rectTransform.localScale = Vector3.one * (1f - 0.08f * b.pressed);
            }
            // The axe button pulses gently while a swing is held.
            m_Chop.ring.color = Color.Lerp(new Color(1f, 0.95f, 0.82f, 0.5f), UIStyle.Accent, m_Chop.pressed);
        }
    }
}
