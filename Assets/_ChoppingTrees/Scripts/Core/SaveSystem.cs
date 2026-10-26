using System;
using System.IO;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// JSON saves in persistentDataPath. Writes go to a temp file first and the previous save is
    /// kept as .bak, so a crash mid-write or a corrupt file falls back to the last good save.
    /// </summary>
    public static class SaveSystem
    {
        public const string SaveFile = "progress.json";
        public const string SettingsFile = "settings.json";

        /// <summary>Overridable for tests.</summary>
        public static string Directory
        {
            get => s_Directory ??= Application.persistentDataPath;
            set => s_Directory = value;
        }
        static string s_Directory;

        public static string SavePath => Path.Combine(Directory, SaveFile);
        public static string SettingsPath => Path.Combine(Directory, SettingsFile);

        public static bool HasSave => File.Exists(SavePath) || File.Exists(SavePath + ".bak");

        public static bool Save(GameState state)
        {
            state.LastSavedUtc = DateTime.UtcNow;
            return WriteJson(SavePath, JsonUtility.ToJson(state.ToSave(), true));
        }

        /// <summary>Loads progress; returns null when there is no usable save.</summary>
        public static GameState Load(out string problem)
        {
            problem = null;
            var data = ReadJson<SaveData>(SavePath, out string p1);
            if (data == null && File.Exists(SavePath + ".bak"))
            {
                data = ReadJson<SaveData>(SavePath + ".bak", out _);
                if (data != null) problem = "Save file was damaged; restored the backup. (" + p1 + ")";
            }
            else if (data == null && p1 != null) problem = p1;
            if (data == null) return null;
            Migrate(data);
            return GameState.FromSave(data);
        }

        public static void Delete()
        {
            foreach (var p in new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        public static SettingsData LoadSettings() => ReadJson<SettingsData>(SettingsPath, out _) ?? new SettingsData();

        public static void SaveSettings(SettingsData s) => WriteJson(SettingsPath, JsonUtility.ToJson(s, true));

        /// <summary>Upgrades older save layouts in place.</summary>
        public static void Migrate(SaveData d)
        {
            if (d.version <= 0) d.version = 1;
            // Future: if (d.version < 2) { ...; d.version = 2; }
            if (d.resources == null) d.resources = new System.Collections.Generic.List<SaveData.Amount>();
            if (d.upgrades == null) d.upgrades = new System.Collections.Generic.List<SaveData.UpgradeLevel>();
        }

        static bool WriteJson(string path, string json)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                {
                    string bak = path + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(path, bak);
                }
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not write " + path + ": " + e.Message);
                return false;
            }
        }

        static T ReadJson<T>(string path, out string problem) where T : class
        {
            problem = null;
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) { problem = "empty file"; return null; }
                var data = JsonUtility.FromJson<T>(json);
                if (data == null) problem = "unreadable file";
                return data;
            }
            catch (Exception e)
            {
                problem = e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[Save] Could not read " + path + ": " + e.Message);
                return null;
            }
        }
    }
}
