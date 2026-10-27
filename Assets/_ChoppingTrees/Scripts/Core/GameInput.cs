using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>
    /// All player input in one place, keyboard and mouse or controller, read straight from the
    /// Input System devices (no input asset needed). Also tracks which of the two was used last,
    /// so prompts show the right buttons and menus give a controller a focused element, and
    /// drives controller rumble. <see cref="Tick"/> runs once a frame (from UIRoot).
    ///
    /// Controller layout: left stick move (click: sprint), right stick look, RT chop, A jump,
    /// X use, Start pause, View/Select hide hints; in menus the D-pad or stick moves, A selects
    /// and B goes back. On phones and tablets the on-screen <see cref="TouchControls"/> feed
    /// the Touch* values below.
    /// </summary>
    public static class GameInput
    {
        /// <summary>True after the controller was used last, false after the keyboard or mouse.</summary>
        public static bool UsingGamepad { get; private set; }
        /// <summary>True while playing with the touch screen (phones and tablets start this way).</summary>
        public static bool UsingTouch { get; private set; } = Application.isMobilePlatform;
        /// <summary>Raised when <see cref="UsingGamepad"/> or <see cref="UsingTouch"/> flips (prompts re-label).</summary>
        public static event Action DeviceChanged;

        // Set every frame by TouchControls (which runs before the player scripts).
        /// <summary>On-screen stick, -1..1.</summary>
        public static Vector2 TouchMove;
        /// <summary>Degrees to turn this frame from swipes.</summary>
        public static Vector2 TouchLook;
        public static bool TouchChop, TouchSprint, TouchJump, TouchUse, TouchMenu;

        /// <summary>Resets the one-frame touch values (taps, swipes); <paramref name="all"/> also lets go of the stick.</summary>
        public static void ClearTouch(bool all = false)
        {
            TouchLook = Vector2.zero;
            TouchChop = TouchJump = TouchUse = TouchMenu = false;
            if (!all) return;
            TouchMove = Vector2.zero;
            TouchSprint = false;
        }

        static bool s_SprintToggle;
        static float s_RumbleUntil;

        static Gamepad Pad => Gamepad.current;
        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;
        static float DeadZone => GameSettings.Data.stickDeadZone;

        // ------------------------------------------------------------------ gameplay

        /// <summary>Movement (x strafe, y forward), length at most 1.</summary>
        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (Kb != null)
                {
                    if (Kb.wKey.isPressed) v.y += 1f;
                    if (Kb.sKey.isPressed) v.y -= 1f;
                    if (Kb.dKey.isPressed) v.x += 1f;
                    if (Kb.aKey.isPressed) v.x -= 1f;
                }
                if (Pad != null) v += Stick(Pad.leftStick.ReadValue());
                v += TouchMove;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool JumpPressed => Kb != null && Kb.spaceKey.wasPressedThisFrame || Pad != null && Pad.buttonSouth.wasPressedThisFrame || TouchJump;

        /// <summary>Shift held, or the left stick clicked (stays on until the stick is let go).</summary>
        public static bool Sprint => Kb != null && Kb.leftShiftKey.isPressed || s_SprintToggle || TouchSprint;

        public static bool ChopHeld => Ms != null && Ms.leftButton.isPressed && !UsingTouch || Pad != null && Pad.rightTrigger.ReadValue() > 0.4f || TouchChop;

        public static bool UsePressed => Kb != null && Kb.eKey.wasPressedThisFrame || Pad != null && Pad.buttonWest.wasPressedThisFrame || TouchUse;

        /// <summary>Esc or Start: pause in play, close the top menu otherwise.</summary>
        public static bool MenuPressed => Kb != null && Kb.escapeKey.wasPressedThisFrame || Pad != null && Pad.startButton.wasPressedThisFrame || TouchMenu;

        /// <summary>LB / RB (L1 / R1, L / R): previous and next tab.</summary>
        public static bool TabLeftPressed => Pad != null && Pad.leftShoulder.wasPressedThisFrame;
        public static bool TabRightPressed => Pad != null && Pad.rightShoulder.wasPressedThisFrame;

        /// <summary>B / Circle: back out of menus.</summary>
        public static bool BackPressed => Pad != null && Pad.buttonEast.wasPressedThisFrame;

        public static bool HintsTogglePressed => Kb != null && Kb.hKey.wasPressedThisFrame || Pad != null && Pad.selectButton.wasPressedThisFrame;

        public static bool FpsTogglePressed => Kb != null && Kb.f3Key.wasPressedThisFrame;

        /// <summary>Mouse movement this frame, in pixels.</summary>
        public static Vector2 MouseDelta => Ms != null ? Ms.delta.ReadValue() : Vector2.zero;

        /// <summary>Right stick after the dead zone and a soft response curve (-1..1).</summary>
        public static Vector2 LookStick => Pad != null ? Curve(Stick(Pad.rightStick.ReadValue())) : Vector2.zero;

        /// <summary>Right stick and trigger zoom for the skill tree view.</summary>
        public static Vector2 PanStick => Pad != null ? Stick(Pad.rightStick.ReadValue()) : Vector2.zero;
        public static float ZoomAxis => Pad != null ? Pad.rightTrigger.ReadValue() - Pad.leftTrigger.ReadValue() : 0f;

        static Vector2 Stick(Vector2 v)
        {
            float m = v.magnitude;
            if (m < DeadZone) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - DeadZone) / (1f - DeadZone));
        }

        static Vector2 Curve(Vector2 v) => v * v.magnitude; // fine aim near the centre, fast turns at the edge

        // ------------------------------------------------------------------ rumble

        /// <summary>Short controller rumble (only when a controller is in use and vibration is on);
        /// with the touch screen, a phone vibration of similar strength.</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            if (UsingTouch)
            {
                Haptics.Play(Mathf.RoundToInt(Mathf.Min(seconds, 0.2f) * 600f), Mathf.Max(low, high));
                return;
            }
            if (Pad == null || !UsingGamepad || !GameSettings.Data.vibration) return;
            Pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            s_RumbleUntil = Mathf.Max(s_RumbleUntil, Time.unscaledTime + seconds);
        }

        public static void StopRumble()
        {
            s_RumbleUntil = 0f;
            Pad?.ResetHaptics();
        }

        // ------------------------------------------------------------------ per frame

        static ControllerStyle s_LastStyle;

        public static void Tick()
        {
            DetectDevice();
            // Picking a different controller in Settings (or plugging one in) re-labels the prompts.
            var style = Style;
            if (style != s_LastStyle)
            {
                s_LastStyle = style;
                DeviceChanged?.Invoke();
            }
            if (s_RumbleUntil > 0f && Time.unscaledTime >= s_RumbleUntil) StopRumble();
            if (Pad != null)
            {
                if (Pad.leftStickButton.wasPressedThisFrame) s_SprintToggle = !s_SprintToggle;
                if (Stick(Pad.leftStick.ReadValue()).magnitude < 0.2f) s_SprintToggle = false;
            }
            else s_SprintToggle = false;
            KeepMenuFocus();
        }

        static void DetectDevice()
        {
            bool pad = false, desk = false, touch = false;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) touch = true;
            var p = Pad;
            if (p != null)
                pad = p.leftStick.ReadValue().magnitude > 0.4f || p.rightStick.ReadValue().magnitude > 0.4f || p.dpad.ReadValue().sqrMagnitude > 0.1f
                      || p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonWest.isPressed || p.buttonNorth.isPressed
                      || p.startButton.isPressed || p.selectButton.isPressed || p.leftShoulder.isPressed || p.rightShoulder.isPressed
                      || p.leftTrigger.ReadValue() > 0.3f || p.rightTrigger.ReadValue() > 0.3f;
            // (Android's back button arrives as Esc and doesn't count as switching to a keyboard.)
            if (Kb != null && Kb.anyKey.wasPressedThisFrame && !Kb.escapeKey.wasPressedThisFrame) desk = true;
            if (!touch && Ms != null && (Ms.delta.ReadValue().sqrMagnitude > 16f || Ms.leftButton.wasPressedThisFrame || Ms.rightButton.wasPressedThisFrame)) desk = true;
            bool now = UsingGamepad, nowTouch = UsingTouch;
            if (pad) { now = true; nowTouch = false; }
            else if (touch) { now = false; nowTouch = true; }
            else if (desk) { now = false; nowTouch = false; }
            if (now == UsingGamepad && nowTouch == UsingTouch) return;
            UsingTouch = nowTouch;
            if (now == UsingGamepad)
            {
                DeviceChanged?.Invoke();
                return;
            }
            UsingGamepad = now;
            // Buttons look "hovered" while selected: with the mouse nothing should stay selected.
            if (!now && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (!now) StopRumble();
            DeviceChanged?.Invoke();
        }

        /// <summary>With a controller, the top menu always has something focused so the D-pad works.</summary>
        static void KeepMenuFocus()
        {
            if (!UsingGamepad) return;
            var es = EventSystem.current;
            var top = UIScreen.TopModal;
            if (es == null || top == null) return;
            var sel = es.currentSelectedGameObject;
            if (sel != null && sel.activeInHierarchy && sel.transform.IsChildOf(top.transform)) return;
            var target = top.DefaultSelection();
            if (target != null) es.SetSelectedGameObject(target);
        }

        // ------------------------------------------------------------------ prompts and glyphs

        /// <summary>Button icons in use: the player's choice in Settings, otherwise the connected controller's family.</summary>
        public static ControllerStyle Style
        {
            get
            {
                var s = GameSettings.Data;
                if (s.controllerStyleChosen) return s.controllerStyle;
                string type = Pad != null ? Pad.GetType().Name : "";
                if (type.Contains("DualShock") || type.Contains("DualSense")) return ControllerStyle.PlayStation;
                if (type.Contains("Switch")) return ControllerStyle.Switch;
                return ControllerStyle.Xbox;
            }
        }

        public static readonly string[] StyleNames = { "Xbox", "PlayStation", "Switch" };

        /// <summary>Glyph file (Resources/Glyphs/&lt;style&gt;/&lt;file&gt;) and label per action and style.
        /// Buttons go by position, so the Switch's bottom button (B) is "jump".</summary>
        static readonly System.Collections.Generic.Dictionary<string, (string file, string label)[]> s_Buttons =
            new System.Collections.Generic.Dictionary<string, (string, string)[]>
        {
            ["chop"] = new[] { ("RT", "RT"), ("R2", "R2"), ("ZR", "ZR") },
            ["use"] = new[] { ("X", "X"), ("Square", "Square"), ("Y", "Y") },
            ["jump"] = new[] { ("A", "A"), ("Cross", "Cross"), ("B", "B") },
            ["select"] = new[] { ("A", "A"), ("Cross", "Cross"), ("B", "B") },
            ["back"] = new[] { ("B", "B"), ("Circle", "Circle"), ("A", "A") },
            ["menu"] = new[] { ("Menu", "Menu"), ("Options", "Options"), ("Plus", "+") },
            ["hints"] = new[] { ("Windows", "View"), ("Share", "Share"), ("Minus", "-") },
            ["sprint"] = new[] { ("Left_Stick_Click", "LS"), ("Left_Stick_Click", "L3"), ("Left_Stick_Click", "LS") },
            ["move"] = new[] { ("Left_Stick", "Left stick"), ("Left_Stick", "Left stick"), ("Left_Stick", "Left stick") },
            ["look"] = new[] { ("Right_Stick", "Right stick"), ("Right_Stick", "Right stick"), ("Right_Stick", "Right stick") },
            ["zoomOut"] = new[] { ("LT", "LT"), ("L2", "L2"), ("ZL", "ZL") },
            ["zoomIn"] = new[] { ("RT", "RT"), ("R2", "R2"), ("ZR", "ZR") },
            ["dpad"] = new[] { ("Dpad", "D-pad"), ("Dpad", "D-pad"), ("Dpad", "D-pad") },
            ["tabLeft"] = new[] { ("LB", "LB"), ("L1", "L1"), ("L", "L") },
            ["tabRight"] = new[] { ("RB", "RB"), ("R1", "R1"), ("R", "R") },
        };

        static readonly System.Collections.Generic.Dictionary<string, string> s_TouchNames = new System.Collections.Generic.Dictionary<string, string>
        {
            ["chop"] = "the axe button", ["use"] = "Use", ["jump"] = "Jump", ["sprint"] = "the stick forward", ["menu"] = "Menu",
        };

        static readonly string[] s_KeyboardKeys = { "chop:LMB", "use:E", "sprint:Shift", "jump:Space", "menu:Esc", "hints:H", "select:Enter", "back:Esc" };
        static readonly System.Collections.Generic.Dictionary<string, Sprite> s_Glyphs = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Prompt label for an action on the device in use ("E" on keyboard, "X" / "Square" / "Y" on a controller).</summary>
        public static string Key(string action)
        {
            if (UsingTouch && s_TouchNames.TryGetValue(action, out var touchName)) return touchName;
            if (UsingGamepad && s_Buttons.TryGetValue(action, out var b)) return b[(int)Style].label;
            foreach (var k in s_KeyboardKeys)
                if (k.StartsWith(action + ":")) return k.Substring(action.Length + 1);
            return action;
        }

        /// <summary>Button icon for an action when a controller is in use, otherwise null (show the key cap).</summary>
        public static Sprite Glyph(string action) => UsingGamepad ? Glyph(Style, action) : null;

        public static Sprite Glyph(ControllerStyle style, string action)
        {
            if (!s_Buttons.TryGetValue(action, out var b)) return null;
            return LoadGlyph(style + "/" + b[(int)style].file);
        }

        /// <summary>Picture of a controller of that family (for the Settings picker).</summary>
        public static Sprite ControllerIcon(ControllerStyle style) => LoadGlyph("Controllers/" + style);

        static Sprite LoadGlyph(string path)
        {
            if (s_Glyphs.TryGetValue(path, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("Glyphs/" + path);
            s_Glyphs[path] = s;
            return s;
        }
    }
}
