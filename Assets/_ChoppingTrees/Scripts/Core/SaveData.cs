using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Serialised save file (JsonUtility). Bump <see cref="CurrentVersion"/> and extend
    /// <see cref="SaveSystem.Migrate"/> when the layout changes.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public double money;
        public List<Amount> resources = new List<Amount>();
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public double totalEarned;
        public double totalWoodChopped;
        public long treesFelled;
        public double playSeconds;
        public int tutorialStep;
        public bool tutorialDone;
        public bool hasPlayed;
        public long lastSavedUtcTicks;

        [Serializable]
        public struct Amount
        {
            public string id;
            public double value;
        }

        [Serializable]
        public struct UpgradeLevel
        {
            public string id;
            public int level;
        }
    }

    /// <summary>Player settings, saved separately so resetting progress keeps them.</summary>
    [Serializable]
    public class SettingsData
    {
        public const int CurrentVersion = 2;

        /// <summary>0 for files from 1.1 and earlier (JsonUtility keeps this default when the key
        /// is missing); <see cref="GameSettings.Migrate"/> brings it up to date.</summary>
        public int version;
        public float mouseSensitivity = 1f;
        public bool invertY;
        public float fieldOfView = 72f;
        public float masterVolume = 0.8f;
        public int displayMode = 1; // index into GameSettings.WindowModes (0 fullscreen, 1 borderless, 2 windowed)
        /// <summary>0 = the monitor's native resolution.</summary>
        public int resolutionWidth;
        public int resolutionHeight;
        /// <summary>Monitor name to run on; empty = wherever the window opens.</summary>
        public string monitor = "";
        public bool vSync = true;
        public bool showTutorial = true;

        [Header("Graphics")]
        public UpscalerSetting upscaler = UpscalerSetting.Off;
        public UpscaleQuality upscaleQuality = UpscaleQuality.Quality;
        [Tooltip("Render scale when no upscaler (or a spatial one) is used.")]
        public float renderScale = 1f;
        public AntiAliasingSetting antiAliasing = AntiAliasingSetting.SMAA;
        /// <summary>0 = unlimited (only used with V-Sync off).</summary>
        public int frameRateCap;
        public FpsCounterMode fpsCounter = FpsCounterMode.Off;
    }

    public enum UpscalerSetting { Off, FSR1, STP, DLSS, FSR2 }
    public enum UpscaleQuality { NativeAA, Quality, Balanced, Performance }
    public enum AntiAliasingSetting { Off, FXAA, SMAA, TAA }
    public enum FpsCounterMode { Off, Fps, Detailed }
}
