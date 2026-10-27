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
    /// and B goes back.
    /// </summary>
    public static class GameInput
    {
        /// <summary>True after the controller was used last, false after the keyboard or mouse.</summary>
        public static bool UsingGamepad { get; private set; }
        /// <summary>Raised when <see cref="UsingGamepad"/> flips (prompts re-label).</summary>
        public static event Action DeviceChanged;

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
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool JumpPressed => Kb != null && Kb.spaceKey.wasPressedThisFrame || Pad != null && Pad.buttonSouth.wasPressedThisFrame;

        /// <summary>Shift held, or the left stick clicked (stays on until the stick is let go).</summary>
        public static bool Sprint => Kb != null && Kb.leftShiftKey.isPressed || s_SprintToggle;

        public static bool ChopHeld => Ms != null && Ms.leftButton.isPressed || Pad != null && Pad.rightTrigger.ReadValue() > 0.4f;

        public static bool UsePressed => Kb != null && Kb.eKey.wasPressedThisFrame || Pad != null && Pad.buttonWest.wasPressedThisFrame;

        /// <summary>Esc or Start: pause in play, close the top menu otherwise.</summary>
        public static bool MenuPressed => Kb != null && Kb.escapeKey.wasPressedThisFrame || Pad != null && Pad.startButton.wasPressedThisFrame;

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

        /// <summary>Short controller rumble (only when a controller is in use and vibration is on).</summary>
        public static void Rumble(float low, float high, float seconds)
        {
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

        public static void Tick()
        {
            DetectDevice();
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
            bool pad = false, desk = false;
            var p = Pad;
            if (p != null)
                pad = p.leftStick.ReadValue().magnitude > 0.4f || p.rightStick.ReadValue().magnitude > 0.4f || p.dpad.ReadValue().sqrMagnitude > 0.1f
                      || p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonWest.isPressed || p.buttonNorth.isPressed
                      || p.startButton.isPressed || p.selectButton.isPressed || p.leftShoulder.isPressed || p.rightShoulder.isPressed
                      || p.leftTrigger.ReadValue() > 0.3f || p.rightTrigger.ReadValue() > 0.3f;
            if (Kb != null && Kb.anyKey.wasPressedThisFrame) desk = true;
            if (Ms != null && (Ms.delta.ReadValue().sqrMagnitude > 16f || Ms.leftButton.wasPressedThisFrame || Ms.rightButton.wasPressedThisFrame)) desk = true;
            bool now = UsingGamepad;
            if (pad) now = true;
            else if (desk) now = false;
            if (now == UsingGamepad) return;
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

        /// <summary>Prompt label for an action on the device in use ("E" or "X", ...).</summary>
        public static string Key(string action) => action switch
        {
            "chop" => UsingGamepad ? "RT" : "LMB",
            "use" => UsingGamepad ? "X" : "E",
            "sprint" => UsingGamepad ? "L3" : "Shift",
            "jump" => UsingGamepad ? "A" : "Space",
            "menu" => UsingGamepad ? "Start" : "Esc",
            "hints" => UsingGamepad ? "View" : "H",
            _ => action,
        };
    }
}
