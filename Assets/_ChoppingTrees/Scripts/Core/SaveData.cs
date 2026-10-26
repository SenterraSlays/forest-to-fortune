using System;
using System.Collections.Generic;

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
        public float mouseSensitivity = 1f;
        public bool invertY;
        public float fieldOfView = 72f;
        public float masterVolume = 0.8f;
        public int displayMode = 1; // FullScreenMode index into GameSettings.DisplayModes
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vSync = true;
        public bool showTutorial = true;
    }
}
