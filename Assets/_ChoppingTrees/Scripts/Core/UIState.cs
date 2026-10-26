using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Tracks open menus. While any is open, gameplay input is blocked and the cursor is free.
    /// </summary>
    public static class UIState
    {
        static readonly HashSet<object> s_Open = new HashSet<object>();

        public static bool AnyMenuOpen => s_Open.Count > 0;
        public static bool GameplayInput => !AnyMenuOpen && Game.Instance != null && Game.Instance.Phase == GamePhase.Playing;

        public static void SetOpen(object menu, bool open)
        {
            if (open) s_Open.Add(menu);
            else s_Open.Remove(menu);
            ApplyCursor();
        }

        public static bool IsOpen(object menu) => s_Open.Contains(menu);

        public static void Clear()
        {
            s_Open.Clear();
            ApplyCursor();
        }

        public static void ApplyCursor()
        {
            bool locked = GameplayInput;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
