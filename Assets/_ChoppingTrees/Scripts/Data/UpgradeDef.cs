using System;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// One purchasable upgrade line. Cost of the next level comes from <see cref="explicitCosts"/>
    /// when given, otherwise baseCost * costGrowth^level.
    /// </summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Upgrade", fileName = "Upgrade")]
    public class UpgradeDef : ScriptableObject
    {
        public string id = "axe_tier";
        public string displayName = "Better Axe";
        [TextArea] public string description;
        public UpgradeCategory category;
        public UpgradeEffect effect;
        [Tooltip("Machine, resource or area id this upgrade applies to (when the effect needs one).")]
        public string targetId;
        public int maxLevel = 1;
        public double baseCost = 100;
        public double costGrowth = 1.6;
        [Tooltip("Optional per-level costs; overrides the formula for the levels it covers.")]
        public double[] explicitCosts;
        public float effectPerLevel = 0.2f;
        [Tooltip("Optional name per level (index = level after purchase - 1), e.g. axe names.")]
        public string[] levelNames;
        [Tooltip("Other upgrades that must reach a level before this one can be bought.")]
        public Requirement[] requires;
        public int sortOrder;

        [Serializable]
        public struct Requirement
        {
            public string upgradeId;
            public int level;

            public Requirement(string upgradeId, int level)
            {
                this.upgradeId = upgradeId;
                this.level = level;
            }
        }

        public bool IsMaxed(int level) => level >= maxLevel;

        /// <summary>Price of going from <paramref name="level"/> to level + 1.</summary>
        public double CostForLevel(int level)
        {
            level = Math.Max(0, level);
            if (explicitCosts != null && level < explicitCosts.Length) return explicitCosts[level];
            if (explicitCosts != null && explicitCosts.Length > 0)
            {
                // Continue geometrically from the last explicit price.
                int extra = level - explicitCosts.Length + 1;
                return NumberUtil.Safe(explicitCosts[explicitCosts.Length - 1] * Math.Pow(costGrowth, extra));
            }
            return NumberUtil.Safe(baseCost * Math.Pow(costGrowth, level));
        }

        public string LevelName(int level)
        {
            if (levelNames == null || levelNames.Length == 0 || level <= 0) return null;
            return levelNames[Mathf.Clamp(level - 1, 0, levelNames.Length - 1)];
        }
    }
}
