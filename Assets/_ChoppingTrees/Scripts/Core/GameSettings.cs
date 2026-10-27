using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Player settings: loading, applying to the engine and saving.</summary>
    public static class GameSettings
    {
        public static readonly FullScreenMode[] WindowModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        public static readonly string[] WindowModeNames = { "Fullscreen", "Borderless", "Windowed" };

        static SettingsData s_Data;

        public static SettingsData Data => s_Data ??= Migrate(SaveSystem.LoadSettings());

        /// <summary>Upgrades settings saved by older versions.</summary>
        public static SettingsData Migrate(SettingsData d)
        {
            if (d.version < 2)
            {
                // Before 1.2 the resolution list came only from the modes the signal chain
                // reported, which capture cards can cap at 1080p, so a saved size may not have
                // been a real choice. Start from the monitor's native resolution instead.
                d.resolutionWidth = d.resolutionHeight = 0;
                d.version = 2;
            }
            return d;
        }

        /// <summary>Raised after settings change (camera FOV, sensitivity readers listen).</summary>
        public static event Action Changed;

        /// <summary>Why the display didn't do exactly what was asked (shown in Settings), or empty.</summary>
        public static string DisplayNotice { get; private set; } = "";

        public static void Apply()
        {
            var d = Data;
            d.mouseSensitivity = Mathf.Clamp(d.mouseSensitivity, 0.1f, 4f);
            d.fieldOfView = Mathf.Clamp(d.fieldOfView, 55f, 100f);
            d.masterVolume = Mathf.Clamp01(d.masterVolume);
            d.displayMode = Mathf.Clamp(d.displayMode, 0, WindowModes.Length - 1);
            AudioListener.volume = d.masterVolume;
            d.renderScale = Mathf.Clamp(d.renderScale, 0.5f, 1f);
            d.brightness = Mathf.Clamp(d.brightness, -1f, 1f);
            d.uiScale = Mathf.Clamp(d.uiScale, 0.75f, 1.25f);
            d.effectsVolume = Mathf.Clamp01(d.effectsVolume);
            d.ambienceVolume = Mathf.Clamp01(d.ambienceVolume);
            d.interfaceVolume = Mathf.Clamp01(d.interfaceVolume);
            d.stickSensitivity = Mathf.Clamp(d.stickSensitivity, 0.2f, 3f);
            d.stickDeadZone = Mathf.Clamp(d.stickDeadZone, 0.05f, 0.4f);
            QualitySettings.globalTextureMipmapLimit = (int)d.textures;
            QualitySettings.anisotropicFiltering = d.anisotropic ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
            QualitySettings.lodBias = d.detail switch { DetailLevel.Low => 0.6f, DetailLevel.Medium => 1f, DetailLevel.High => 1.5f, _ => 2.5f };
            Effects.Density = d.effects switch { EffectsQuality.Low => 0.4f, EffectsQuality.Medium => 0.7f, _ => 1f };
            d.frameRateCap = Mathf.Clamp(d.frameRateCap, 0, 360);
            QualitySettings.vSyncCount = d.vSync ? 1 : 0;
            Application.targetFrameRate = d.vSync || d.frameRateCap <= 0 ? -1 : d.frameRateCap;
            ApplyDisplay(d);
            Changed?.Invoke();
        }

        /// <summary>Size the game will use: the chosen resolution, or the monitor's native size when
        /// none was chosen (or the choice doesn't fit this monitor).</summary>
        public static Vector2Int ResolvedResolution(SettingsData d)
        {
            var native = DisplayModes.Native;
            if (d.resolutionWidth <= 0 || d.resolutionHeight <= 0) return native;
            if (d.resolutionWidth > native.x || d.resolutionHeight > native.y) return native;
            return new Vector2Int(d.resolutionWidth, d.resolutionHeight);
        }

        static bool SizeFromCommandLine()
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-screen-width" || a == "-screen-height" || a == "-screen-fullscreen") return true;
            return false;
        }

        static void ApplyDisplay(SettingsData d)
        {
            DisplayNotice = "";
            // Unity remembers the last window size in the registry; the game always sets its own
            // so it starts at the monitor's native resolution. Command-line sizes (tests) win.
            if (Application.isEditor || SizeFromCommandLine()) return;

            if (!string.IsNullOrEmpty(d.monitor) && d.monitor != DisplayModes.Current.name)
            {
                foreach (var m in DisplayModes.Monitors())
                {
                    if (m.name != d.monitor) continue;
                    var op = Screen.MoveMainWindowTo(m, Vector2Int.zero);
                    op.completed += _ => ApplyResolution(d);
                    return;
                }
            }
            ApplyResolution(d);
        }

        static void ApplyResolution(SettingsData d)
        {
            var mode = WindowModes[d.displayMode];
            var size = ResolvedResolution(d);
            if (mode == FullScreenMode.ExclusiveFullScreen && !DisplayModes.ExclusiveSupports(size))
            {
                // Some signal chains (capture cards, virtual displays) don't advertise the monitor's
                // real modes; exclusive fullscreen can't switch to them, borderless can.
                DisplayNotice = $"Exclusive fullscreen can't use {size.x}x{size.y} on this monitor, so borderless is used.";
                mode = FullScreenMode.FullScreenWindow;
            }
            if (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode)
                Screen.SetResolution(size.x, size.y, mode);
        }

        /// <summary>Every setting back to its default (High preset, native resolution).</summary>
        public static void ResetToDefaults()
        {
            s_Data = new SettingsData { version = SettingsData.CurrentVersion };
            Apply();
        }

        public static void Save()
        {
            // Automated runs (-ct-nosave) must not overwrite the player's settings either.
            if (Game.SavingEnabled) SaveSystem.SaveSettings(Data);
        }
    }
}
