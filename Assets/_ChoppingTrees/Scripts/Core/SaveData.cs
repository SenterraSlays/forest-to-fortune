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
        /// <summary>2: added <see cref="backpack"/> (1.2). Version 1 stock all stays in the yard.
        /// 3: added <see cref="achievements"/> and <see cref="felled"/> (2.1); older saves earn the
        /// achievements their progress already meets the first time they load.</summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public double money;
        /// <summary>Yard stock (machine output).</summary>
        public List<Amount> resources = new List<Amount>();
        /// <summary>Wood carried by the player.</summary>
        public List<Amount> backpack = new List<Amount>();
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public double totalEarned;
        public double totalWoodChopped;
        public long treesFelled;
        /// <summary>Added in 1.4; older files load as 0.</summary>
        public int rebirths;
        /// <summary>Added in 1.5; older files get the points their rebirths earned (see GameModel).</summary>
        public int skillPoints;
        public int skillPointsEarned;
        public double playSeconds;
        public int tutorialStep;
        public bool tutorialDone;
        public bool hasPlayed;
        public long lastSavedUtcTicks;
        /// <summary>Unlocked achievement ids. Achievements belong to this save only.</summary>
        public List<string> achievements = new List<string>();
        /// <summary>Trees felled per species (counted from 2.1).</summary>
        public List<Amount> felled = new List<Amount>();

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
        /// <summary>Save slot played last (0 = A); the main menu shows its stats.</summary>
        public int lastSlot;
        public bool discordPresence = true;

        [Header("Graphics")]
        public UpscalerSetting upscaler = UpscalerSetting.Off;
        public UpscaleQuality upscaleQuality = UpscaleQuality.Quality;
        [Tooltip("Render scale when no upscaler (or a spatial one) is used.")]
        public float renderScale = 1f;
        public AntiAliasingSetting antiAliasing = AntiAliasingSetting.SMAA;
        /// <summary>0 = unlimited (only used with V-Sync off).</summary>
        public int frameRateCap;
        public FpsCounterMode fpsCounter = FpsCounterMode.Off;

        [Header("Quality (see QualityPresets)")]
        public QualityPreset qualityPreset = QualityPreset.High;
        public ShadowQuality shadows = ShadowQuality.High;
        public TextureQuality textures = TextureQuality.Full;
        public bool anisotropic = true;
        public DetailLevel detail = DetailLevel.High;
        public bool bloom = true;
        public EffectsQuality effects = EffectsQuality.High;
        [Tooltip("Exposure offset, -1..1.")]
        public float brightness;

        [Header("Audio")]
        public float effectsVolume = 1f;
        public float ambienceVolume = 1f;
        public float interfaceVolume = 1f;

        [Header("Controller")]
        public float stickSensitivity = 1f;
        public bool invertStickY;
        public bool vibration = true;
        public float stickDeadZone = 0.15f;
        [Tooltip("Which button icons to show. Until the player picks one, the connected controller decides.")]
        public ControllerStyle controllerStyle = ControllerStyle.Xbox;
        public bool controllerStyleChosen;

        [Header("Comfort and interface")]
        public bool headBob = true;
        public bool cameraShake = true;
        public bool damageNumbers = true;
        public float uiScale = 1f;

        [Header("Touch")]
        public float touchLookSensitivity = 1f;
        public bool touchInvertY;
        public float touchButtonScale = 1f;
        public float touchOpacity = 0.85f;
        [Tooltip("Floating: the stick appears where the thumb lands. Fixed: it stays in the corner.")]
        public bool touchFixedStick;

        [Header("Cloud saves")]
        [Tooltip("Account shown in Settings; empty when signed out.")]
        public string cloudAccount = "";
        [Tooltip("Per slot: fingerprint of the progress this device and the cloud last agreed on.")]
        public long[] cloudSynced = new long[3];
        public bool cloudAutoSync = true;
    }

    public enum QualityPreset { Low, Medium, High, Ultra, Custom }
    public enum ControllerStyle { Xbox, PlayStation, Switch }
    public enum ShadowQuality { Off, Low, Medium, High, Ultra }
    public enum TextureQuality { Full, Half, Quarter }
    public enum DetailLevel { Low, Medium, High, Ultra }
    public enum EffectsQuality { Low, Medium, High }

    public enum UpscalerSetting { Off, FSR1, STP, DLSS, FSR2 }
    public enum UpscaleQuality { NativeAA, Quality, Balanced, Performance }
    public enum AntiAliasingSetting { Off, FXAA, SMAA, TAA }
    public enum FpsCounterMode { Off, Fps, Detailed }
}
