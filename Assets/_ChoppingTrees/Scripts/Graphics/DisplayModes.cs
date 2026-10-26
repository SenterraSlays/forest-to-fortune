using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Monitors and resolutions. Unity's own mode list (Screen.resolutions) only covers the output
    /// the window is on and can be capped by capture cards or virtual displays in the chain, so the
    /// list is built from every source: the monitor's native size, its reported modes and common
    /// 16:9 sizes up to the native size. Borderless and windowed modes can render any of them.
    /// </summary>
    public static class DisplayModes
    {
        static readonly Vector2Int[] s_Common =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
        };

        /// <summary>All monitors Windows reports, in Unity's order.</summary>
        public static List<DisplayInfo> Monitors()
        {
            var list = new List<DisplayInfo>();
            Screen.GetDisplayLayout(list);
            return list;
        }

        /// <summary>The monitor the game window is on.</summary>
        public static DisplayInfo Current => Screen.mainWindowDisplayInfo;

        /// <summary>Native (desktop) size of the monitor the window is on.</summary>
        public static Vector2Int Native
        {
            get
            {
                var d = Current;
                if (d.width > 0 && d.height > 0) return new Vector2Int(d.width, d.height);
                var r = Screen.currentResolution;
                return new Vector2Int(r.width, r.height);
            }
        }

        /// <summary>Distinct resolutions for the current monitor, smallest first.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var native = Native;
            var set = new HashSet<Vector2Int> { native };
            foreach (var r in Screen.resolutions)
                if (r.width >= 1024 && r.width <= native.x && r.height <= native.y) set.Add(new Vector2Int(r.width, r.height));
            foreach (var c in s_Common)
                if (c.x <= native.x && c.y <= native.y) set.Add(c);
            var list = new List<Vector2Int>(set);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        /// <summary>True when exclusive fullscreen can actually switch the monitor to this size.</summary>
        public static bool ExclusiveSupports(Vector2Int size)
        {
            foreach (var r in Screen.resolutions)
                if (r.width == size.x && r.height == size.y) return true;
            return false;
        }

        public static string Name(DisplayInfo d) => string.IsNullOrEmpty(d.name) ? $"Display {d.width}x{d.height}" : d.name;

        /// <summary>Everything Unity reports, for the player log.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            var cur = Screen.currentResolution;
            sb.Append($"window {Screen.width}x{Screen.height} {Screen.fullScreenMode}; currentResolution {cur.width}x{cur.height}@{cur.refreshRateRatio.value:0}Hz; ");
            var main = Current;
            sb.Append($"on '{main.name}' {main.width}x{main.height}@{main.refreshRate.value:0}Hz; monitors:");
            foreach (var d in Monitors()) sb.Append($" ['{d.name}' {d.width}x{d.height}@{d.refreshRate.value:0}Hz at {d.workArea.x},{d.workArea.y}]");
            sb.Append("; displays " + Display.displays.Length);
            for (int i = 0; i < Display.displays.Length; i++) sb.Append($" [{i}: system {Display.displays[i].systemWidth}x{Display.displays[i].systemHeight}]");
            sb.Append("; Screen.resolutions:");
            var seen = new HashSet<Vector2Int>();
            foreach (var r in Screen.resolutions)
                if (seen.Add(new Vector2Int(r.width, r.height))) sb.Append($" {r.width}x{r.height}");
            return sb.ToString();
        }
    }
}
