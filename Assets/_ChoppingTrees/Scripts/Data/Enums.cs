namespace ChoppingTrees
{
    public enum UpgradeCategory
    {
        Axe,
        Automation,
        Economy,
        Forest,
        /// <summary>Rebirth perks bought with skill points (older saves; the Legacy tree uses the four branches below).</summary>
        Legacy,
        /// <summary>Legacy tree: head starts for every run and the rebirth itself.</summary>
        Heritage,
        /// <summary>Legacy tree: the axe and the player.</summary>
        Might,
        /// <summary>Legacy tree: machines and offline time.</summary>
        Industry,
        /// <summary>Legacy tree: prices, luck and the forests.</summary>
        Fortune,
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
        /// <summary>Level = axe tier every run starts with.</summary>
        StartingAxe,
        /// <summary>Money every run starts with: effectPerLevel * 5^(level - 1).</summary>
        StartingMoney,
        /// <summary>+effectPerLevel backpack capacity multiplier.</summary>
        BackpackBonus,
        /// <summary>Level = how many locked forests (in order) every run starts with open.</summary>
        StartingAreas,
        /// <summary>Level = machines of <c>targetId</c> every run starts with.</summary>
        StartingMachines,
        /// <summary>+effectPerLevel to the double-damage multiplier (base x2).</summary>
        CritDamage,
        /// <summary>+effectPerLevel chance per hit to fell the tree outright.</summary>
        FellChance,
        /// <summary>-effectPerLevel price for every skill tree upgrade that isn't a machine (capped at 30%).</summary>
        UpgradeDiscount,
        /// <summary>Level = backpack tier every run starts with.</summary>
        StartingBackpack,
        /// <summary>+effectPerLevel extra skill points for every rebirth.</summary>
        ExtraSkillPoints,
        /// <summary>+effectPerLevel to the earnings bonus each rebirth gives (base <see cref="GameConfig.rebirthBonus"/>).</summary>
        RebirthBonus,
        /// <summary>-effectPerLevel price for rebirths (capped at 50%).</summary>
        RebirthDiscount,
    }

    public enum MachineKind
    {
        /// <summary>Produces its output resource from nothing.</summary>
        Producer,
        /// <summary>Converts its input resource into its output resource.</summary>
        Processor,
    }
}
