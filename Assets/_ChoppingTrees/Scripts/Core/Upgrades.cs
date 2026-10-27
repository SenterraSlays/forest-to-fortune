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
        int m_AxeTier, m_BackpackTier;
        float m_DamageMult, m_SpeedMult, m_AutoSellSeconds, m_OfflineHours, m_ProductionBonus;
        float m_MagnetBonus, m_WoodYield, m_CritChance, m_MoveSpeed, m_Discount, m_SellBonus, m_LuckyChance, m_Regrowth, m_TreeCash, m_PackBonus, m_StartMoneyBase;
        int m_StartAxe, m_StartAreas, m_StartMoneyLevel;
        float m_CritDamage, m_FellChance, m_UpgradeDiscount;
        readonly Dictionary<string, int> m_StartMachines = new Dictionary<string, int>();
        readonly Dictionary<string, double> m_ProcessorYield = new Dictionary<string, double>();
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

        /// <summary>Price of the next level, after discounts (Spare Parts lowers machine prices).</summary>
        public double NextCost(UpgradeDef u)
        {
            double mult = u.legacy ? 1.0 : u.effect == UpgradeEffect.MachineCount ? 1.0 - MachineDiscount : 1.0 - UpgradeDiscount;
            return NumberUtil.Safe(u.CostForLevel(Level(u)) * mult);
        }

        /// <summary>Damage multiplier of a double-damage hit (2 plus Power Swing).</summary>
        public float CritMultiplier { get { Refresh(); return 2f + m_CritDamage; } }
        /// <summary>Chance (0..1) that a hit fells the tree outright.</summary>
        public float FellChance { get { Refresh(); return Mathf.Clamp01(m_FellChance); } }
        /// <summary>Share taken off non-machine upgrade prices (0..0.3).</summary>
        public double UpgradeDiscount { get { Refresh(); return Mathf.Clamp(m_UpgradeDiscount, 0f, 0.3f); } }

        /// <summary>Why the upgrade can't be bought right now (null when it can).</summary>
        public string Blocker(UpgradeDef u, bool checkMoney = true)
        {
            if (u == null) return "Unknown upgrade";
            if (u.IsMaxed(Level(u))) return "Maxed";
            string missing = MissingRequirement(u);
            if (missing != null) return missing;
            if (checkMoney && !CanAfford(u)) return u.legacy ? "Not enough skill points" : "Not enough money";
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
        /// <summary>Money for normal upgrades, skill points for Legacy perks.</summary>
        public bool CanAfford(UpgradeDef u) => u.legacy ? m_State.SkillPoints >= SkillPointCost(u) : m_State.Money + 1e-9 >= NextCost(u);

        /// <summary>Whole skill points for the next level of a Legacy perk.</summary>
        public int SkillPointCost(UpgradeDef u) => (int)Math.Ceiling(NextCost(u) - 1e-9);

        public bool TryBuy(UpgradeDef u, out string failure)
        {
            failure = Blocker(u);
            if (failure != null) return false;
            if (u.legacy) m_State.SkillPoints -= SkillPointCost(u);
            else m_State.Money = NumberUtil.SafePositive(m_State.Money - NextCost(u));
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

        public int BackpackTier { get { Refresh(); return m_BackpackTier; } }

        /// <summary>How much the player can carry by hand (machines fill the yard instead).</summary>
        public double BackpackCapacity => CapacityForTier(m_Db.config, BackpackTier) * BackpackMultiplier;
        public double BackpackMultiplier { get { Refresh(); return 1 + m_PackBonus; } }

        // ---------- what every run starts with (Legacy perks)

        public int StartingAxeTier { get { Refresh(); return m_StartAxe; } }
        public int StartingAreas { get { Refresh(); return m_StartAreas; } }
        public int StartingMachines(string machineId) { Refresh(); return m_StartMachines.TryGetValue(machineId, out var v) ? v : 0; }
        public double StartingMoney { get { Refresh(); return StartingMoneyFor(m_StartMoneyBase, m_StartMoneyLevel); } }
        public static double StartingMoneyFor(double basePerLevel, int level) => level <= 0 ? 0 : basePerLevel * Math.Pow(5, level - 1);

        public static double CapacityForTier(GameConfig cfg, int tier)
        {
            var c = cfg.backpackCapacity;
            if (c == null || c.Length == 0) return double.MaxValue;
            return c[Mathf.Clamp(tier, 0, c.Length - 1)];
        }

        public string BackpackName
        {
            get
            {
                var u = FirstWithEffect(UpgradeEffect.BackpackCapacity);
                return (u != null ? u.LevelName(Level(u)) : null) ?? m_Db.config.baseBackpackName;
            }
        }

        /// <summary>How close (metres) logs must be before they fly to the player.</summary>
        public float MagnetRadius { get { Refresh(); return m_Db.config.pickupMagnetRadius + m_MagnetBonus; } }
        /// <summary>Wood multiplier for trees the player fells.</summary>
        public float WoodYieldMultiplier { get { Refresh(); return 1f + m_WoodYield; } }
        /// <summary>Chance (0..1) that a hit deals double damage.</summary>
        public float CritChance { get { Refresh(); return Mathf.Clamp01(m_CritChance); } }
        public float MoveSpeedMultiplier { get { Refresh(); return 1f + m_MoveSpeed; } }
        /// <summary>Extra output share for a processor (0.2 = 20% more planks from the same wood).</summary>
        public double ProcessorYield(string machineId) { Refresh(); return m_ProcessorYield.TryGetValue(machineId, out var v) ? v : 0; }
        /// <summary>Share taken off machine prices (0..0.5).</summary>
        public double MachineDiscount { get { Refresh(); return Mathf.Clamp(m_Discount, 0f, 0.5f); } }
        public double SellMultiplier { get { Refresh(); return 1 + m_SellBonus; } }
        /// <summary>Chance (0..1) that a felled tree also drops cash.</summary>
        public float LuckyFindChance { get { Refresh(); return Mathf.Clamp01(m_LuckyChance); } }
        /// <summary>Respawn time multiplier for felled trees (smaller = faster).</summary>
        public float RegrowthTimeMultiplier { get { Refresh(); return 1f / (1f + m_Regrowth); } }
        public float TreeCashMultiplier { get { Refresh(); return 1f + m_TreeCash; } }

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
            m_BackpackTier = 0;
            m_DamageMult = 1f;
            m_SpeedMult = 1f;
            m_AutoSellSeconds = 0f;
            m_OfflineHours = 0f;
            m_ProductionBonus = 0f;
            m_MagnetBonus = m_WoodYield = m_CritChance = m_MoveSpeed = m_Discount = m_SellBonus = m_LuckyChance = m_Regrowth = m_TreeCash = m_PackBonus = m_StartMoneyBase = 0f;
            m_StartAxe = m_StartAreas = m_StartMoneyLevel = 0;
            m_CritDamage = m_FellChance = m_UpgradeDiscount = 0f;
            m_StartMachines.Clear();
            m_ProcessorYield.Clear();
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
                    case UpgradeEffect.BackpackCapacity: m_BackpackTier = Math.Max(m_BackpackTier, lv); break;
                    case UpgradeEffect.MagnetRange: m_MagnetBonus += e * lv; break;
                    case UpgradeEffect.WoodYield: m_WoodYield += e * lv; break;
                    case UpgradeEffect.CritChance: m_CritChance += e * lv; break;
                    case UpgradeEffect.MoveSpeed: m_MoveSpeed += e * lv; break;
                    case UpgradeEffect.ProcessorYield: m_ProcessorYield[t] = (m_ProcessorYield.TryGetValue(t, out var py) ? py : 0) + e * lv; break;
                    case UpgradeEffect.MachineDiscount: m_Discount += e * lv; break;
                    case UpgradeEffect.SellBonus: m_SellBonus += e * lv; break;
                    case UpgradeEffect.LuckyFind: m_LuckyChance += e * lv; break;
                    case UpgradeEffect.TreeRegrowth: m_Regrowth += e * lv; break;
                    case UpgradeEffect.TreeCashBonus: m_TreeCash += e * lv; break;
                    case UpgradeEffect.StartingAxe: m_StartAxe = Math.Max(m_StartAxe, lv); break;
                    case UpgradeEffect.StartingMoney:
                        if (lv > m_StartMoneyLevel) { m_StartMoneyLevel = lv; m_StartMoneyBase = e; }
                        break;
                    case UpgradeEffect.BackpackBonus: m_PackBonus += e * lv; break;
                    case UpgradeEffect.StartingAreas: m_StartAreas = Math.Max(m_StartAreas, lv); break;
                    case UpgradeEffect.CritDamage: m_CritDamage += e * lv; break;
                    case UpgradeEffect.FellChance: m_FellChance += e * lv; break;
                    case UpgradeEffect.UpgradeDiscount: m_UpgradeDiscount += e * lv; break;
                    case UpgradeEffect.StartingMachines: m_StartMachines[t] = Math.Max(m_StartMachines.TryGetValue(t, out var sm) ? sm : 0, lv); break;
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
