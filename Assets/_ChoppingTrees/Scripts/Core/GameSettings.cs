using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Player settings: loading, applying to the engine and saving.</summary>
    public static class GameSettings
    {
        public static readonly FullScreenMode[] DisplayModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        public static readonly string[] DisplayModeNames = { "Fullscreen", "Borderless", "Windowed" };

        static SettingsData s_Data;

        public static SettingsData Data => s_Data ??= SaveSystem.LoadSettings();

        /// <summary>Raised after settings change (camera FOV, sensitivity readers listen).</summary>
        public static event Action Changed;

        public static void Apply()
        {
            var d = Data;
            d.mouseSensitivity = Mathf.Clamp(d.mouseSensitivity, 0.1f, 4f);
            d.fieldOfView = Mathf.Clamp(d.fieldOfView, 55f, 100f);
            d.masterVolume = Mathf.Clamp01(d.masterVolume);
            d.displayMode = Mathf.Clamp(d.displayMode, 0, DisplayModes.Length - 1);
            AudioListener.volume = d.masterVolume;
            d.renderScale = Mathf.Clamp(d.renderScale, 0.5f, 1f);
            d.frameRateCap = Mathf.Clamp(d.frameRateCap, 0, 360);
            QualitySettings.vSyncCount = d.vSync ? 1 : 0;
            Application.targetFrameRate = d.vSync || d.frameRateCap <= 0 ? -1 : d.frameRateCap;
            if (!Application.isEditor)
            {
                var mode = DisplayModes[d.displayMode];
                if (d.resolutionWidth > 0 && d.resolutionHeight > 0)
                    Screen.SetResolution(d.resolutionWidth, d.resolutionHeight, mode);
                else
                    Screen.fullScreenMode = mode;
            }
            Changed?.Invoke();
        }

        public static void Save()
        {
            // Automated runs (-ct-nosave) must not overwrite the player's settings either.
            if (Game.SavingEnabled) SaveSystem.SaveSettings(Data);
        }

        /// <summary>Distinct resolutions the display supports (largest last).</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 800 && !list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            return list;
        }
    }
}
