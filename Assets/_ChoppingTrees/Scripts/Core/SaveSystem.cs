using System;
using System.IO;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// JSON saves in persistentDataPath, in three slots (A, B, C: progress_A.json ...). Writes go
    /// to a temp file first and the previous save is kept as .bak, so a crash mid-write or a
    /// corrupt file falls back to the last good save. Older layouts are moved over on first use:
    /// the single progress.json becomes slot A, and saves from the game's old name
    /// ("A Game About Chopping Trees") are copied into the new folder.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Single save file used before slots existed (moved to slot A).</summary>
        public const string LegacySaveFile = "progress.json";
        public const string SettingsFile = "settings.json";
        public const string OldProductFolder = "A Game About Chopping Trees";
        public static readonly string[] SlotNames = { "A", "B", "C" };

        /// <summary>The slot being played (0..2).</summary>
        public static int CurrentSlot
        {
            get => s_Slot;
            set => s_Slot = Mathf.Clamp(value, 0, SlotNames.Length - 1);
        }
        static int s_Slot;

        /// <summary>Overridable for tests.</summary>
        public static string Directory
        {
            get
            {
                if (s_Directory != null) return s_Directory;
                s_Directory = Application.persistentDataPath;
                CopyFromOldProductFolder(s_Directory);
                return s_Directory;
            }
            set => s_Directory = value;
        }
        static string s_Directory;

        public static string SlotPath(int slot) => Path.Combine(Directory, $"progress_{SlotNames[Mathf.Clamp(slot, 0, SlotNames.Length - 1)]}.json");
        public static string SavePath => SlotPath(CurrentSlot);
        public static string SettingsPath => Path.Combine(Directory, SettingsFile);

        public static bool SlotHasSave(int slot)
        {
            MigrateLegacyFile();
            return File.Exists(SlotPath(slot)) || File.Exists(SlotPath(slot) + ".bak");
        }

        public static bool HasSave => SlotHasSave(CurrentSlot);

        /// <summary>A slot's save without loading it into the game (for the save screen), or null.</summary>
        public static SaveData Peek(int slot)
        {
            MigrateLegacyFile();
            string path = SlotPath(slot);
            var data = ReadJson<SaveData>(path, out _) ?? ReadJson<SaveData>(path + ".bak", out _);
            if (data != null) Migrate(data);
            return data;
        }

        /// <summary>The pre-slot progress.json becomes slot A (only if slot A is still empty).</summary>
        public static void MigrateLegacyFile()
        {
            string legacy = Path.Combine(Directory, LegacySaveFile), a = SlotPath(0);
            try
            {
                if (File.Exists(a) || !File.Exists(legacy)) return;
                File.Move(legacy, a);
                if (File.Exists(legacy + ".bak") && !File.Exists(a + ".bak")) File.Move(legacy + ".bak", a + ".bak");
                Debug.Log("[Save] Moved progress.json to slot A");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not move progress.json to slot A: " + e.Message);
            }
        }

        /// <summary>The game used to be called "A Game About Chopping Trees": copy its saves and settings
        /// into the new folder once, when the new folder has none.</summary>
        static void CopyFromOldProductFolder(string dir)
        {
            try
            {
                var parent = System.IO.Directory.GetParent(dir);
                if (parent == null) return;
                string old = Path.Combine(parent.FullName, OldProductFolder);
                if (!System.IO.Directory.Exists(old) || string.Equals(Path.GetFullPath(old), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) return;
                System.IO.Directory.CreateDirectory(dir);
                if (System.IO.Directory.GetFiles(dir, "progress*").Length > 0 || File.Exists(Path.Combine(dir, SettingsFile))) return;
                int n = 0;
                foreach (var f in System.IO.Directory.GetFiles(old))
                {
                    string name = Path.GetFileName(f);
                    if (!name.StartsWith("progress") && !name.StartsWith("settings")) continue;
                    File.Copy(f, Path.Combine(dir, name));
                    n++;
                }
                if (n > 0) Debug.Log($"[Save] Copied {n} save files from the old game folder");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] Could not copy saves from the old game folder: " + e.Message);
            }
        }

        public static bool Save(GameState state)
        {
            state.LastSavedUtc = DateTime.UtcNow;
            return WriteJson(SavePath, JsonUtility.ToJson(state.ToSave(), true));
        }

        /// <summary>Loads progress; returns null when there is no usable save.</summary>
        public static GameState Load(out string problem)
        {
            problem = null;
            MigrateLegacyFile();
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

        /// <summary>A slot's save file as text (the backup if the file is damaged), or null.</summary>
        public static string ReadRaw(int slot)
        {
            MigrateLegacyFile();
            string path = SlotPath(slot);
            foreach (var p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    string json = File.ReadAllText(p);
                    if (!string.IsNullOrWhiteSpace(json) && JsonUtility.FromJson<SaveData>(json) != null) return json;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Save] Could not read " + p + ": " + e.Message);
                }
            }
            return null;
        }

        /// <summary>Replaces a slot's save with this text (from the cloud), keeping the old file as the backup.</summary>
        public static bool WriteRaw(int slot, string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || JsonUtility.FromJson<SaveData>(json) == null) return false;
            }
            catch
            {
                return false;
            }
            return WriteJson(SlotPath(slot), json);
        }

        public static void Delete() => Delete(CurrentSlot);

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        public static SettingsData LoadSettings() => ReadJson<SettingsData>(SettingsPath, out _) ?? new SettingsData();

        public static void SaveSettings(SettingsData s) => WriteJson(SettingsPath, JsonUtility.ToJson(s, true));

        /// <summary>Upgrades older save layouts in place.</summary>
        public static void Migrate(SaveData d)
        {
            if (d.version <= 0) d.version = 1;
            // Version 1 had a single stock. It stays in the yard: moving it into the backpack
            // could overflow the new capacity, and the market sells both anyway.
            if (d.version < 2) d.version = 2;
            // Version 3 added achievements and per-species counts; they start empty and the game
            // grants every achievement the save already qualifies for when it loads.
            if (d.version < 3) d.version = 3;
            if (d.achievements == null) d.achievements = new System.Collections.Generic.List<string>();
            if (d.felled == null) d.felled = new System.Collections.Generic.List<SaveData.Amount>();
            if (d.resources == null) d.resources = new System.Collections.Generic.List<SaveData.Amount>();
            if (d.backpack == null) d.backpack = new System.Collections.Generic.List<SaveData.Amount>();
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
