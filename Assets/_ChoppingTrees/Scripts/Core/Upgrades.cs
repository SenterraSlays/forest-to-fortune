using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Upgrade levels, purchase rules and the aggregated effects every other system reads
    /// (axe damage, machine counts and multipliers, prices, area unlocks...).
    /// Aggregates are cached and rebuilt only after a level changes.
    /// </summary>
    public class Upgrades
    {
        readonly GameState m_State;
        readonly GameDatabase m_Db;
        int m_CachedVersion = -1;
        int m_Version;

        // cached aggregates
        int m_AxeTier;
        float m_DamageMult, m_SpeedMult, m_AutoSellSeconds, m_OfflineHours, m_ProductionBonus;
        readonly Dictionary<string, int> m_MachineCounts = new Dictionary<string, int>();
        readonly Dictionary<string, double> m_MachineMult = new Dictionary<string, double>();
        readonly Dictionary<string, int> m_MachineTier = new Dictionary<string, int>();
        readonly Dictionary<string, double> m_PriceMult = new Dictionary<string, double>();

        public Upgrades(GameState state, GameDatabase db)
        {
            m_State = state;
            m_Db = db;
        }

        public int Level(UpgradeDef u) => u == null ? 0 : Mathf.Min(m_State.Level(u.id), u.maxLevel);
        public int Level(string id) => Level(m_Db.Upgrade(id));

        public double NextCost(UpgradeDef u) => u.CostForLevel(Level(u));

        /// <summary>Why the upgrade can't be bought right now (null when it can).</summary>
        public string Blocker(UpgradeDef u, bool checkMoney = true)
        {
            if (u == null) return "Unknown upgrade";
            if (u.IsMaxed(Level(u))) return "Maxed";
            string missing = MissingRequirement(u);
            if (missing != null) return missing;
            if (checkMoney && m_State.Money + 1e-9 < NextCost(u)) return "Not enough money";
            return null;
        }

        /// <summary>The first unmet prerequisite, phrased for the UI ("Requires Steel Axe").</summary>
        public string MissingRequirement(UpgradeDef u)
        {
            if (u.requires == null) return null;
            foreach (var r in u.requires)
            {
                var other = m_Db.Upgrade(r.upgradeId);
                if (other == null || Level(other) >= r.level) continue;
                string name = other.LevelName(r.level) ?? (r.level > 1 ? $"{other.displayName} Lv {r.level}" : other.displayName);
                return "Requires " + name;
            }
            return null;
        }

        public bool IsLocked(UpgradeDef u) => MissingRequirement(u) != null;

        /// <summary>Buys one level. Money and level change together or not at all.</summary>
        public bool TryBuy(UpgradeDef u, out string failure)
        {
            failure = Blocker(u);
            if (failure != null) return false;
            double cost = NextCost(u);
            m_State.Money = NumberUtil.SafePositive(m_State.Money - cost);
            m_State.SetLevel(u.id, Level(u) + 1);
            m_Version++;
            return true;
        }

        /// <summary>Call after levels change outside TryBuy (loading, cheats, tests).</summary>
        public void MarkDirty() => m_Version++;

        // ---------- aggregated effects ----------

        public int AxeTier { get { Refresh(); return m_AxeTier; } }

        public float AxeDamage
        {
            get
            {
                Refresh();
                var t = m_Db.config.axeTierDamage;
                return t[Mathf.Clamp(m_AxeTier, 0, t.Length - 1)] * m_DamageMult;
            }
        }

        public float SwingSpeedMultiplier
        {
            get
            {
                Refresh();
                var t = m_Db.config.axeTierSpeed;
                return t[Mathf.Clamp(m_AxeTier, 0, t.Length - 1)] * m_SpeedMult;
            }
        }

        public float SwingSeconds => Mathf.Max(m_Db.config.minSwingSeconds, m_Db.config.swingSeconds / SwingSpeedMultiplier);

        public string AxeName
        {
            get
            {
                var u = FirstWithEffect(UpgradeEffect.AxeTier);
                return (u != null ? u.LevelName(Level(u)) : null) ?? m_Db.config.baseAxeName;
            }
        }

        public int MachineCount(string machineId) { Refresh(); return m_MachineCounts.TryGetValue(machineId, out var v) ? v : 0; }
        public int MachineTier(string machineId) { Refresh(); return m_MachineTier.TryGetValue(machineId, out var v) ? v : 0; }

        /// <summary>Output multiplier for one machine of this type (speed, tier and global bonuses).</summary>
        public double MachineMultiplier(string machineId)
        {
            Refresh();
            double m = m_MachineMult.TryGetValue(machineId, out var v) ? v : 1;
            return m * (1 + m_ProductionBonus);
        }

        public double PriceMultiplier(string resourceId) { Refresh(); return m_PriceMult.TryGetValue(resourceId, out var v) ? v : 1; }

        /// <summary>Seconds between automatic sales, or 0 when auto-selling isn't owned.</summary>
        public float AutoSellSeconds { get { Refresh(); return m_AutoSellSeconds; } }

        public float OfflineCapHours { get { Refresh(); return m_Db.config.baseOfflineHours + m_OfflineHours; } }

        public bool AreaUnlocked(ForestAreaDef area)
        {
            if (area == null) return false;
            if (area.unlockedByDefault) return true;
            var u = m_Db.AreaUpgradeFor(area.id);
            return u != null && Level(u) >= 1;
        }

        public UpgradeDef FirstWithEffect(UpgradeEffect e)
        {
            foreach (var u in m_Db.upgrades) if (u != null && u.effect == e) return u;
            return null;
        }

        void Refresh()
        {
            if (m_CachedVersion == m_Version) return;
            m_CachedVersion = m_Version;
            m_AxeTier = 0;
            m_DamageMult = 1f;
            m_SpeedMult = 1f;
            m_AutoSellSeconds = 0f;
            m_OfflineHours = 0f;
            m_ProductionBonus = 0f;
            m_MachineCounts.Clear();
            m_MachineMult.Clear();
            m_MachineTier.Clear();
            m_PriceMult.Clear();
            var speedSums = new Dictionary<string, double>();
            var tierProducts = new Dictionary<string, double>();

            foreach (var u in m_Db.upgrades)
            {
                if (u == null) continue;
                int lv = Level(u);
                float e = u.effectPerLevel;
                string t = u.targetId ?? "";
                switch (u.effect)
                {
                    case UpgradeEffect.AxeTier: m_AxeTier = Math.Max(m_AxeTier, lv); break;
                    case UpgradeEffect.AxeDamage: m_DamageMult += e * lv; break;
                    case UpgradeEffect.AxeSpeed: m_SpeedMult += e * lv; break;
                    case UpgradeEffect.MachineCount: m_MachineCounts[t] = (m_MachineCounts.TryGetValue(t, out var c) ? c : 0) + lv; break;
                    case UpgradeEffect.MachineSpeed: speedSums[t] = (speedSums.TryGetValue(t, out var s) ? s : 0) + e * lv; break;
                    case UpgradeEffect.MachineTier:
                        m_MachineTier[t] = (m_MachineTier.TryGetValue(t, out var mt) ? mt : 0) + lv;
                        tierProducts[t] = (tierProducts.TryGetValue(t, out var p) ? p : 1) * Math.Pow(e, lv);
                        break;
                    case UpgradeEffect.ResourcePrice: m_PriceMult[t] = (m_PriceMult.TryGetValue(t, out var pm) ? pm : 1) + e * lv; break;
                    case UpgradeEffect.AutoSell:
                        if (lv > 0) m_AutoSellSeconds = m_AutoSellSeconds <= 0 ? e : Mathf.Min(m_AutoSellSeconds, e);
                        break;
                    case UpgradeEffect.OfflineHours: m_OfflineHours += e * lv; break;
                    case UpgradeEffect.ProductionBonus: m_ProductionBonus += e * lv; break;
                }
            }

            foreach (var m in m_Db.machines)
            {
                if (m == null) continue;
                double speed = 1 + (speedSums.TryGetValue(m.id, out var s) ? s : 0);
                double tier = tierProducts.TryGetValue(m.id, out var p) ? p : 1;
                m_MachineMult[m.id] = NumberUtil.Safe(speed * tier);
            }
        }
    }
}
