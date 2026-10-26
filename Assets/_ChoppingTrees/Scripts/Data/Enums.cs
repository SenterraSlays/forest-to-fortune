namespace ChoppingTrees
{
    public enum UpgradeCategory
    {
        Axe,
        Automation,
        Economy,
        Forest,
    }

    /// <summary>What an upgrade level changes. Effects are aggregated by <see cref="Upgrades"/>.</summary>
    public enum UpgradeEffect
    {
        /// <summary>Level = axe tier. Tier sets base damage, the axe model and which trees can be chopped.</summary>
        AxeTier,
        /// <summary>+effectPerLevel damage multiplier per level.</summary>
        AxeDamage,
        /// <summary>+effectPerLevel swing speed multiplier per level.</summary>
        AxeSpeed,
        /// <summary>Level = number of owned machines of <c>targetId</c>.</summary>
        MachineCount,
        /// <summary>+effectPerLevel output multiplier for machine <c>targetId</c>.</summary>
        MachineSpeed,
        /// <summary>Output x effectPerLevel per level for machine <c>targetId</c>; also swaps its model.</summary>
        MachineTier,
        /// <summary>+effectPerLevel sale price multiplier for resource <c>targetId</c>.</summary>
        ResourcePrice,
        /// <summary>Any level enables automatic selling every effectPerLevel seconds.</summary>
        AutoSell,
        /// <summary>+effectPerLevel hours of offline production cap per level.</summary>
        OfflineHours,
        /// <summary>Level 1 unlocks forest area <c>targetId</c>.</summary>
        ForestArea,
        /// <summary>+effectPerLevel output multiplier for every machine.</summary>
        ProductionBonus,
        /// <summary>Level = backpack tier; the capacity per tier is <see cref="GameConfig.backpackCapacity"/>.</summary>
        BackpackCapacity,
        /// <summary>+effectPerLevel metres of log magnet (pickup) range per level.</summary>
        MagnetRange,
        /// <summary>+effectPerLevel wood multiplier for trees the player fells.</summary>
        WoodYield,
        /// <summary>+effectPerLevel chance per hit to deal double damage.</summary>
        CritChance,
        /// <summary>+effectPerLevel walking and sprinting speed multiplier.</summary>
        MoveSpeed,
        /// <summary>+effectPerLevel output multiplier for processor machine <c>targetId</c> (same input).</summary>
        ProcessorYield,
        /// <summary>-effectPerLevel price for every machine purchase (capped at 50%).</summary>
        MachineDiscount,
        /// <summary>+effectPerLevel sale price multiplier for every resource.</summary>
        SellBonus,
        /// <summary>+effectPerLevel chance that a felled tree also drops cash (see <see cref="GameConfig.luckyFindValue"/>).</summary>
        LuckyFind,
        /// <summary>+effectPerLevel regrowth speed for felled trees.</summary>
        TreeRegrowth,
        /// <summary>+effectPerLevel multiplier for the cash some trees (Goldbark) drop.</summary>
        TreeCashBonus,
    }

    public enum MachineKind
    {
        /// <summary>Produces its output resource from nothing.</summary>
        Producer,
        /// <summary>Converts its input resource into its output resource.</summary>
        Processor,
    }
}
