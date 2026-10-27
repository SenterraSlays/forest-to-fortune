using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>
    /// All game rules in one place, with no scene dependencies: state, upgrades, market and
    /// production, plus the events the scene listens to. <see cref="Game"/> owns one instance.
    /// </summary>
    public class GameModel
    {
        public const string Wood = "wood";

        public readonly GameDatabase Db;
        public readonly GameState State;
        public readonly Upgrades Upgrades;
        public readonly Market Market;
        public readonly Production Production;

        /// <summary>An upgrade level was bought.</summary>
        public event Action<UpgradeDef> Purchased;
        /// <summary>Resources were sold (bool = automatic sale).</summary>
        public event Action<Market.Receipt, bool> Sold;
        /// <summary>The player collected resources by hand into the backpack (id, amount).</summary>
        public event Action<string, double> Collected;
        /// <summary>The backpack just became full (raised once per fill, not on every refused log).</summary>
        public event Action BackpackFilled;
        /// <summary>A tree was felled by the player.</summary>
        public event Action<TreeType> TreeFelled;
        /// <summary>Progress was reset by a rebirth (number of rebirths bought).</summary>
        public event Action<int> Reborn;
        /// <summary>An achievement was unlocked and its reward granted.</summary>
        public event Action<AchievementDef> AchievementUnlocked;

        float m_AutoSellTimer;

        public GameModel(GameDatabase db, GameState state)
        {
            Db = db;
            State = state ?? new GameState();
            if (State.Money <= 0 && !State.HasPlayed) State.Money = db.config.startingMoney;
            Upgrades = new Upgrades(State, db);
            Market = new Market(State, db, Upgrades);
            Production = new Production(State, db, Upgrades);
            // Saves from before skill points existed get the points their rebirths earned.
            int owed = State.Rebirths * db.config.rebirthSkillPoints - State.SkillPointsEarned;
            if (owed > 0)
            {
                State.SkillPoints += owed;
                State.SkillPointsEarned += owed;
            }
        }

        /// <summary>Real-time update: production, play time and automatic selling (any AutoSell
        /// upgrade; it only takes the yard stock, the backpack is sold by hand at the market).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            Production.Advance(dt);
            State.PlaySeconds += dt;
            float interval = Upgrades.AutoSellSeconds;
            if (interval > 0)
            {
                m_AutoSellTimer += dt;
                if (m_AutoSellTimer >= interval)
                {
                    m_AutoSellTimer = 0;
                    Sell(true);
                }
            }
            else m_AutoSellTimer = 0;
        }

        public float AutoSellProgress => Upgrades.AutoSellSeconds > 0 ? m_AutoSellTimer / Upgrades.AutoSellSeconds : 0;

        public double BackpackCapacity => Upgrades.BackpackCapacity;
        public double BackpackFree => Math.Max(0, BackpackCapacity - State.CarriedTotal);
        /// <summary>Less than one unit of room left.</summary>
        public bool BackpackFull => BackpackFree < 1e-6;
        public float BackpackFill => (float)Math.Min(1, State.CarriedTotal / Math.Max(1e-9, BackpackCapacity));

        /// <summary>Puts hand-collected resources in the backpack, up to its capacity.
        /// Returns the amount taken; the rest is left where it was.</summary>
        public double Collect(string resourceId, double amount)
        {
            amount = Math.Min(NumberUtil.SafePositive(amount), BackpackFree);
            if (amount <= 0) return 0;
            State.AddCarried(resourceId, amount);
            if (resourceId == Wood) State.TotalWoodChopped = NumberUtil.Add(State.TotalWoodChopped, amount);
            Collected?.Invoke(resourceId, amount);
            if (BackpackFull) BackpackFilled?.Invoke();
            return amount;
        }

        public void AddMoney(double amount)
        {
            amount = NumberUtil.SafePositive(amount);
            State.Money = NumberUtil.Add(State.Money, amount);
            State.TotalEarned = NumberUtil.Add(State.TotalEarned, amount);
        }

        public void NotifyTreeFelled(TreeType t)
        {
            State.TreesFelled++;
            if (t != null && !string.IsNullOrEmpty(t.id)) State.Felled[t.id] = State.FelledOf(t.id) + 1;
            TreeFelled?.Invoke(t);
        }

        // ---------- achievements

        /// <summary>Locked forests opened so far.</summary>
        public int AreasUnlocked
        {
            get
            {
                int n = 0;
                foreach (var a in Db.areas) if (a != null && !a.unlockedByDefault && Upgrades.AreaUnlocked(a)) n++;
                return n;
            }
        }

        /// <summary>Levels owned across Workbench upgrades (or Legacy perks).</summary>
        public int UpgradeLevels(bool legacy)
        {
            int n = 0;
            foreach (var u in Db.upgrades) if (u != null && u.legacy == legacy) n += Upgrades.Level(u);
            return n;
        }

        public int MachinesOwned
        {
            get
            {
                int n = 0;
                foreach (var m in Db.machines) if (m != null) n += Upgrades.MachineCount(m.id);
                return n;
            }
        }

        /// <summary>
        /// Unlocks every achievement whose goal is met and grants its reward: money, skill points,
        /// or a bonus that lasts for the rest of this save. Saves from before achievements earn
        /// everything their progress already meets the first time this runs.
        /// Returns the achievements unlocked now (null when none).
        /// </summary>
        public List<AchievementDef> CheckAchievements()
        {
            List<AchievementDef> unlocked = null;
            foreach (var a in Achievements.All)
            {
                if (State.Achievements.Contains(a.id) || a.Progress(this) + 1e-9 < a.goal) continue;
                State.Achievements.Add(a.id);
                if (a.money > 0) State.Money = NumberUtil.Add(State.Money, a.money);
                if (a.skillPoints > 0)
                {
                    State.SkillPoints += a.skillPoints;
                    State.SkillPointsEarned += a.skillPoints;
                }
                (unlocked ??= new List<AchievementDef>()).Add(a);
            }
            if (unlocked == null) return null;
            Upgrades.MarkDirty();
            foreach (var a in unlocked) AchievementUnlocked?.Invoke(a);
            return unlocked;
        }

        public bool TryBuy(UpgradeDef u, out string failure)
        {
            if (!Upgrades.TryBuy(u, out failure)) return false;
            // Legacy perks that grant a head start apply right away, not only after the next rebirth.
            if (u.legacy) GrantRunStart(false);
            Purchased?.Invoke(u);
            return true;
        }

        /// <summary>
        /// Gives what Legacy perks promise every run: the heirloom axe, free machines and open
        /// forests (raised to at least those levels), and the starting money when a run begins.
        /// </summary>
        public void GrantRunStart(bool money)
        {
            void AtLeast(UpgradeDef u, int level)
            {
                if (u == null || level <= 0) return;
                level = Math.Min(level, u.maxLevel);
                if (State.Level(u.id) < level) State.SetLevel(u.id, level);
            }
            AtLeast(Upgrades.FirstWithEffect(UpgradeEffect.AxeTier), Upgrades.StartingAxeTier);
            AtLeast(Upgrades.FirstWithEffect(UpgradeEffect.BackpackCapacity), Upgrades.StartingBackpackTier);
            foreach (var m in Db.machines)
                if (m != null) AtLeast(Db.CountUpgradeFor(m.id), Upgrades.StartingMachines(m.id));
            int areas = Upgrades.StartingAreas;
            var locked = new List<ForestAreaDef>();
            foreach (var a in Db.areas) if (a != null && !a.unlockedByDefault) locked.Add(a);
            locked.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            for (int i = 0; i < Math.Min(areas, locked.Count); i++) AtLeast(Db.AreaUpgradeFor(locked[i].id), 1);
            Upgrades.MarkDirty();
            if (money) State.Money = NumberUtil.Add(State.Money, Upgrades.StartingMoney);
        }

        /// <summary>Sells the yard stock, plus the backpack unless it's an automatic sale.</summary>
        public Market.Receipt Sell(bool automatic = false)
        {
            var r = Market.SellAll(backpack: !automatic, yard: true);
            if (!r.Empty) Sold?.Invoke(r, automatic);
            return r;
        }

        /// <summary>Total price of the next <paramref name="count"/> rebirths (after Phoenix Feather).</summary>
        public double RebirthCost(int count = 1) => NumberUtil.Safe(Rebirth.Cost(Db.config, State.Rebirths, count) * (1 - Upgrades.RebirthDiscount));

        /// <summary>How many rebirths the current money pays for at once.</summary>
        public int MaxRebirths()
        {
            int n = Rebirth.MaxAffordable(Db.config, State.Rebirths, State.Money / (1 - Upgrades.RebirthDiscount));
            while (n > 0 && RebirthCost(n) > State.Money + 1e-9) n--;
            return n;
        }

        /// <summary>Skill points each rebirth gives (base plus Scholar).</summary>
        public int SkillPointsPerRebirth => Db.config.rebirthSkillPoints + Upgrades.ExtraSkillPoints;

        /// <summary>Earnings multiplier after <paramref name="rebirths"/> rebirths, with Ancestral Wisdom.</summary>
        public double RebirthMultiplier(int rebirths) => Rebirth.Multiplier(Db.config, rebirths, Upgrades.RebirthBonusExtra);

        /// <summary>
        /// Buys <paramref name="count"/> rebirths at once: resets money, upgrades (so machines and
        /// forests), the yard stock and the backpack, and adds to the rebirth count, which raises
        /// all earnings and gives skill points. Legacy perks, achievements, lifetime stats and the
        /// tutorial state are kept, and the Legacy head starts are granted to the new run.
        /// </summary>
        public bool TryRebirth(int count, out string failure)
        {
            failure = null;
            if (count < 1) { failure = "Choose how many rebirths"; return false; }
            double cost = RebirthCost(count);
            if (State.Money + 1e-9 < cost) { failure = "Need " + NumberUtil.Money(cost - State.Money) + " more"; return false; }
            State.Rebirths += count;
            int points = count * SkillPointsPerRebirth;
            State.SkillPoints += points;
            State.SkillPointsEarned += points;
            State.Money = Db.config.startingMoney;
            State.Resources.Clear();
            State.Backpack.Clear();
            // Legacy perks are kept; everything else in the skill tree starts over.
            var keep = new Dictionary<string, int>();
            foreach (var kv in State.Levels)
            {
                var u = Db.Upgrade(kv.Key);
                if (u != null && u.legacy) keep[kv.Key] = kv.Value;
            }
            State.Levels.Clear();
            foreach (var kv in keep) State.Levels[kv.Key] = kv.Value;
            State.LastSavedUtc = DateTime.UtcNow;
            m_AutoSellTimer = 0;
            Upgrades.MarkDirty();
            GrantRunStart(true);
            Reborn?.Invoke(count);
            return true;
        }

        public class OfflineReport
        {
            public double awaySeconds;
            public double creditedSeconds;
            public Dictionary<string, double> gained = new Dictionary<string, double>();
            public bool Capped => creditedSeconds + 0.5 < awaySeconds;
        }

        /// <summary>
        /// Grants machine output for the time since the last save, capped by the offline limit.
        /// Clock changes that move time backwards give nothing.
        /// </summary>
        public OfflineReport ApplyOffline(DateTime nowUtc)
        {
            var report = new OfflineReport();
            double away = (nowUtc - State.LastSavedUtc).TotalSeconds;
            if (double.IsNaN(away) || away <= 0) return report;
            report.awaySeconds = away;
            double cap = Upgrades.OfflineCapHours * 3600.0;
            report.creditedSeconds = Math.Min(away, cap);
            if (!Production.ProducesAnything) return report;
            report.gained = Production.Advance(report.creditedSeconds * Db.config.offlineEfficiency);
            return report;
        }
    }
}
