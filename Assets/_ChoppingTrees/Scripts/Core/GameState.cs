using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>
    /// The player's progress in memory. Holds plain values only (no scene references), so it can
    /// be saved, loaded and rebuilt after a scene reload without duplicating anything.
    /// Resources are kept in two places: <see cref="Resources"/> is the yard stock that machines
    /// fill, <see cref="Backpack"/> is what the player carries by hand.
    /// </summary>
    public class GameState
    {
        public double Money;
        /// <summary>Yard stock: everything the machines produce.</summary>
        public readonly Dictionary<string, double> Resources = new Dictionary<string, double>();
        /// <summary>What the player carries; limited by the backpack's capacity.</summary>
        public readonly Dictionary<string, double> Backpack = new Dictionary<string, double>();
        public readonly Dictionary<string, int> Levels = new Dictionary<string, int>();

        public double TotalEarned;
        public double TotalWoodChopped;
        public long TreesFelled;
        public double PlaySeconds;
        public int TutorialStep;
        public bool TutorialDone;
        public bool HasPlayed;
        public DateTime LastSavedUtc = DateTime.UtcNow;

        public double Get(string id) => id != null && Resources.TryGetValue(id, out var v) ? v : 0;

        public void Set(string id, double value)
        {
            if (string.IsNullOrEmpty(id)) return;
            Resources[id] = NumberUtil.SafePositive(value);
        }

        public void Add(string id, double amount) => Set(id, Get(id) + amount);

        public double Carried(string id) => id != null && Backpack.TryGetValue(id, out var v) ? v : 0;

        public void SetCarried(string id, double value)
        {
            if (string.IsNullOrEmpty(id)) return;
            Backpack[id] = NumberUtil.SafePositive(value);
        }

        public void AddCarried(string id, double amount) => SetCarried(id, Carried(id) + amount);

        /// <summary>Total amount in the backpack (all resources).</summary>
        public double CarriedTotal
        {
            get
            {
                double t = 0;
                foreach (var kv in Backpack) t += kv.Value;
                return NumberUtil.SafePositive(t);
            }
        }

        public int Level(string id) => id != null && Levels.TryGetValue(id, out var v) ? v : 0;

        public void SetLevel(string id, int level)
        {
            if (string.IsNullOrEmpty(id)) return;
            Levels[id] = Math.Max(0, level);
        }

        // ---------- save conversion ----------

        public SaveData ToSave()
        {
            var d = new SaveData
            {
                version = SaveData.CurrentVersion,
                money = NumberUtil.SafePositive(Money),
                totalEarned = NumberUtil.SafePositive(TotalEarned),
                totalWoodChopped = NumberUtil.SafePositive(TotalWoodChopped),
                treesFelled = Math.Max(0, TreesFelled),
                playSeconds = NumberUtil.SafePositive(PlaySeconds),
                tutorialStep = TutorialStep,
                tutorialDone = TutorialDone,
                hasPlayed = HasPlayed,
                lastSavedUtcTicks = LastSavedUtc.Ticks,
            };
            foreach (var kv in Resources) d.resources.Add(new SaveData.Amount { id = kv.Key, value = NumberUtil.SafePositive(kv.Value) });
            foreach (var kv in Backpack) d.backpack.Add(new SaveData.Amount { id = kv.Key, value = NumberUtil.SafePositive(kv.Value) });
            foreach (var kv in Levels) d.upgrades.Add(new SaveData.UpgradeLevel { id = kv.Key, level = Math.Max(0, kv.Value) });
            return d;
        }

        /// <summary>Builds state from save data, sanitising every value. Unknown ids are kept so
        /// content removed in one version doesn't destroy progress if it returns.</summary>
        public static GameState FromSave(SaveData d)
        {
            var s = new GameState();
            if (d == null) return s;
            s.Money = NumberUtil.SafePositive(d.money);
            s.TotalEarned = NumberUtil.SafePositive(d.totalEarned);
            s.TotalWoodChopped = NumberUtil.SafePositive(d.totalWoodChopped);
            s.TreesFelled = Math.Max(0, d.treesFelled);
            s.PlaySeconds = NumberUtil.SafePositive(d.playSeconds);
            s.TutorialStep = Math.Max(0, d.tutorialStep);
            s.TutorialDone = d.tutorialDone;
            s.HasPlayed = d.hasPlayed;
            s.LastSavedUtc = d.lastSavedUtcTicks > DateTime.MinValue.Ticks && d.lastSavedUtcTicks < DateTime.MaxValue.Ticks
                ? new DateTime(d.lastSavedUtcTicks, DateTimeKind.Utc) : DateTime.UtcNow;
            if (d.resources != null)
                foreach (var a in d.resources) s.Set(a.id, a.value);
            if (d.backpack != null)
                foreach (var a in d.backpack) s.SetCarried(a.id, a.value);
            if (d.upgrades != null)
                foreach (var u in d.upgrades) s.SetLevel(u.id, u.level);
            return s;
        }
    }
}
