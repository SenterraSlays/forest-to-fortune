using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>
    /// One achievement: a goal measured on the game (trees felled, money earned, machines owned...)
    /// and the reward it gives once: money, skill points, or a permanent bonus for this save that
    /// works like one level of an upgrade effect. Achievements belong to a save: they stay through
    /// rebirths, and every save slot earns its own.
    /// </summary>
    public sealed class AchievementDef
    {
        public string id, name, description, icon, group;
        public double goal;
        public Func<GameModel, double> progress;
        /// <summary>How the progress is written: counts, money or hours played.</summary>
        public AchievementUnit unit;

        public double money;
        public int skillPoints;
        public UpgradeEffect? bonus;
        public string bonusTarget;
        public float bonusAmount;

        public double Progress(GameModel m)
        {
            double v = progress != null ? progress(m) : 0;
            return double.IsNaN(v) || v < 0 ? 0 : v;
        }
    }

    public enum AchievementUnit { Count, Money, Hours }

    /// <summary>The list of achievements and how their rewards read.</summary>
    public static class Achievements
    {
        /// <summary>Tabs of the achievements screen, in order.</summary>
        public static readonly string[] Groups = { "Chopping", "Wealth", "Rebirth", "Gear", "Industry", "Explorer", "Milestones" };

        public static readonly string[] GroupIcons = { "axe", "coin", "rebirth", "backpack", "gear", "pine", "star" };

        public static readonly AchievementDef[] All = Build();

        static Dictionary<string, AchievementDef> s_ById;

        public static AchievementDef Get(string id)
        {
            if (s_ById == null)
            {
                s_ById = new Dictionary<string, AchievementDef>();
                foreach (var a in All) s_ById[a.id] = a;
            }
            return id != null && s_ById.TryGetValue(id, out var d) ? d : null;
        }

        // ---------------------------------------------------------------- definitions

        static AchievementDef[] Build()
        {
            var list = new List<AchievementDef>();
            string group = null, icon = null;

            AchievementDef Add(string id, string name, string desc, double goal, Func<GameModel, double> progress, AchievementUnit unit = AchievementUnit.Count)
            {
                var a = new AchievementDef { id = id, name = name, description = desc, goal = goal, progress = progress, unit = unit, group = group, icon = icon };
                list.Add(a);
                return a;
            }
            AchievementDef Money(AchievementDef a, double amount) { a.money = amount; return a; }
            AchievementDef Points(AchievementDef a, int n) { a.skillPoints = n; return a; }
            AchievementDef Bonus(AchievementDef a, UpgradeEffect e, float amount, string target = null)
            {
                a.bonus = e;
                a.bonusAmount = amount;
                a.bonusTarget = target;
                return a;
            }

            Func<GameModel, double> trees = m => m.State.TreesFelled;
            Func<GameModel, double> wood = m => m.State.TotalWoodChopped;
            Func<GameModel, double> earned = m => m.State.TotalEarned;
            Func<GameModel, double> rebirths = m => m.State.Rebirths;
            Func<GameModel, double> axe = m => m.Upgrades.AxeTier;
            Func<GameModel, double> pack = m => m.Upgrades.BackpackTier;
            Func<GameModel, double> areas = m => m.AreasUnlocked;
            Func<GameModel, double> Machines(string id) => m => m.Upgrades.MachineCount(id);
            Func<GameModel, double> Felled(string id) => m => m.State.FelledOf(id);

            group = "Chopping"; icon = "axe";
            Money(Add("first_tree", "First Timber", "Fell your first tree.", 1, trees), 50);
            Bonus(Add("trees_25", "Getting the Hang of It", "Fell 25 trees.", 25, trees), UpgradeEffect.AxeDamage, 0.05f);
            Money(Add("trees_100", "Lumberjack", "Fell 100 trees.", 100, trees), 1500);
            Bonus(Add("trees_500", "Forest Feller", "Fell 500 trees.", 500, trees), UpgradeEffect.AxeDamage, 0.1f);
            Points(Add("trees_2500", "Timber Titan", "Fell 2,500 trees.", 2500, trees), 3);
            Bonus(Add("trees_10000", "Legend of the Woods", "Fell 10,000 trees.", 10000, trees), UpgradeEffect.WoodYield, 0.15f);
            icon = "log";
            Bonus(Add("wood_1k", "Kindling", "Chop 1,000 wood by hand.", 1e3, wood), UpgradeEffect.WoodYield, 0.05f);
            Bonus(Add("wood_100k", "Woodpile", "Chop 100,000 wood by hand.", 1e5, wood), UpgradeEffect.BackpackBonus, 0.1f);
            Points(Add("wood_10m", "Lumber Mountain", "Chop 10 million wood by hand.", 1e7, wood), 3);
            Bonus(Add("wood_1b", "Sea of Sawdust", "Chop 1 billion wood by hand.", 1e9, wood), UpgradeEffect.WoodYield, 0.2f);

            group = "Wealth"; icon = "coin";
            Bonus(Add("earn_1k", "Pocket Change", "Earn $1,000 in total.", 1e3, earned, AchievementUnit.Money), UpgradeEffect.SellBonus, 0.03f);
            Bonus(Add("earn_1m", "Millionaire", "Earn $1 million in total.", 1e6, earned, AchievementUnit.Money), UpgradeEffect.SellBonus, 0.05f);
            Points(Add("earn_1b", "Billionaire", "Earn $1 billion in total.", 1e9, earned, AchievementUnit.Money), 3);
            Bonus(Add("earn_1t", "Trillionaire", "Earn $1 trillion in total.", 1e12, earned, AchievementUnit.Money), UpgradeEffect.SellBonus, 0.1f);
            Points(Add("earn_1qa", "Lumber Tycoon", "Earn $1 quadrillion in total.", 1e15, earned, AchievementUnit.Money), 5);
            icon = "nugget";
            Bonus(Add("bank_100k", "Rainy Day Fund", "Hold $100,000 at once.", 1e5, m => m.State.Money, AchievementUnit.Money), UpgradeEffect.OfflineHours, 2f);
            Bonus(Add("bank_1b", "Vault Keeper", "Hold $1 billion at once.", 1e9, m => m.State.Money, AchievementUnit.Money), UpgradeEffect.LuckyFind, 0.02f);

            group = "Rebirth"; icon = "rebirth";
            Points(Add("rebirth_1", "Born Again", "Rebirth once.", 1, rebirths), 2);
            Bonus(Add("rebirth_5", "Cycle of Growth", "Rebirth 5 times.", 5, rebirths), UpgradeEffect.ProductionBonus, 0.05f);
            Points(Add("rebirth_25", "Phoenix", "Rebirth 25 times.", 25, rebirths), 3);
            Bonus(Add("rebirth_100", "Eternal Forest", "Rebirth 100 times.", 100, rebirths), UpgradeEffect.SellBonus, 0.1f);
            Points(Add("rebirth_250", "Ancient Soul", "Rebirth 250 times.", 250, rebirths), 5);
            Bonus(Add("rebirth_500", "Timeless", "Rebirth 500 times.", 500, rebirths), UpgradeEffect.ProductionBonus, 0.15f);
            icon = "star";
            Bonus(Add("legacy_10", "Heirloom", "Own 10 Legacy perk levels.", 10, m => m.UpgradeLevels(true)), UpgradeEffect.SellBonus, 0.05f);
            Points(Add("legacy_50", "Dynasty", "Own 50 Legacy perk levels.", 50, m => m.UpgradeLevels(true)), 5);
            Bonus(Add("legacy_150", "Living Legend", "Own 150 Legacy perk levels.", 150, m => m.UpgradeLevels(true)), UpgradeEffect.RebirthBonus, 0.01f);

            group = "Gear"; icon = "axe";
            Money(Add("axe_1", "Steel Yourself", "Forge the Steel Axe.", 1, axe), 150);
            Bonus(Add("axe_3", "Broad Shoulders", "Forge the Tempered Broadaxe.", 3, axe), UpgradeEffect.AxeSpeed, 0.05f);
            Bonus(Add("axe_5", "Frostbitten", "Forge the Frostbite Axe.", 5, axe), UpgradeEffect.AxeDamage, 0.1f);
            Points(Add("axe_7", "Starsteel", "Forge the Starsteel Axe.", 7, axe), 3);
            icon = "backpack";
            Bonus(Add("pack_3", "Pack Rat", "Get the Frame Pack.", 3, pack), UpgradeEffect.MoveSpeed, 0.05f);
            Bonus(Add("pack_6", "Beast of Burden", "Get the Hauler's Harness.", 6, pack), UpgradeEffect.BackpackBonus, 0.15f);
            Points(Add("pack_9", "Bottomless Bag", "Get the Starlight Satchel.", 9, pack), 2);
            icon = "wrench";
            Bonus(Add("upg_25", "Tinkerer", "Own 25 upgrade levels at once.", 25, m => m.UpgradeLevels(false)), UpgradeEffect.UpgradeDiscount, 0.02f);
            Bonus(Add("upg_100", "Engineer", "Own 100 upgrade levels at once.", 100, m => m.UpgradeLevels(false)), UpgradeEffect.MachineDiscount, 0.03f);
            Points(Add("upg_250", "Grand Designer", "Own 250 upgrade levels at once.", 250, m => m.UpgradeLevels(false)), 3);

            group = "Industry"; icon = "saw";
            Money(Add("chopper_1", "Automation", "Build an Auto-Chopper.", 1, Machines("chopper")), 300);
            Bonus(Add("chopper_8", "Full Fleet", "Own 8 Auto-Choppers.", 8, Machines("chopper")), UpgradeEffect.MachineSpeed, 0.1f, "chopper");
            icon = "plank";
            Bonus(Add("sawmill_1", "Sawdust", "Build a Sawmill.", 1, Machines("sawmill")), UpgradeEffect.ResourcePrice, 0.05f, "planks");
            Bonus(Add("sawmill_3", "Mill Town", "Own 3 Sawmills.", 3, Machines("sawmill")), UpgradeEffect.MachineSpeed, 0.1f, "sawmill");
            icon = "chair";
            Bonus(Add("workshop_1", "Craftsman", "Build a Carpentry Workshop.", 1, Machines("workshop")), UpgradeEffect.ResourcePrice, 0.1f, "furniture");
            icon = "house";
            Bonus(Add("builder_1", "Architect", "Build a Cabin Builder.", 1, Machines("builder")), UpgradeEffect.ResourcePrice, 0.1f, "cabins");
            Points(Add("builder_3", "Housing Boom", "Own 3 Cabin Builders.", 3, Machines("builder")), 3);
            icon = "gear";
            Bonus(Add("machines_17", "Industrial Revolution", "Own 17 machines at once.", 17, m => m.MachinesOwned), UpgradeEffect.ProductionBonus, 0.1f);

            group = "Explorer"; icon = "mountain";
            Bonus(Add("area_1", "Into the Grove", "Open the Old Grove.", 1, areas), UpgradeEffect.MoveSpeed, 0.05f);
            Money(Add("area_2", "Iron Will", "Open Ironwood Ridge.", 2, areas), 10000);
            Bonus(Add("area_3", "Peak Performance", "Open Frostpine Peaks.", 3, areas), UpgradeEffect.AxeDamage, 0.1f);
            Bonus(Add("area_4", "In Full Bloom", "Open Blossom Vale.", 4, areas), UpgradeEffect.SellBonus, 0.1f);
            Points(Add("area_5", "Crystal Clear", "Open the Crystal Wilds.", 5, areas), 5);
            icon = "pine";
            Bonus(Add("gold_1", "Golden Opportunity", "Fell a Goldbark.", 1, Felled("goldbark")), UpgradeEffect.TreeCashBonus, 0.1f);
            Bonus(Add("frost_10", "Frostbite", "Fell 10 Frostpines.", 10, Felled("frostpine")), UpgradeEffect.WoodYield, 0.05f);
            Bonus(Add("cherry_25", "Hanami", "Fell 25 Cherry trees.", 25, Felled("cherry")), UpgradeEffect.SellBonus, 0.05f);
            Bonus(Add("ember_25", "Fire Starter", "Fell 25 Emberwoods.", 25, Felled("emberwood")), UpgradeEffect.ProductionBonus, 0.1f);
            Bonus(Add("crystal_1", "Starstruck", "Fell a Crystalwood.", 1, Felled("crystalwood")), UpgradeEffect.TreeCashBonus, 0.25f);

            group = "Milestones"; icon = "clock";
            Money(Add("play_1h", "Settling In", "Play for 1 hour.", 1, m => m.State.PlaySeconds / 3600.0, AchievementUnit.Hours), 1000);
            Bonus(Add("play_10h", "Regular", "Play for 10 hours.", 10, m => m.State.PlaySeconds / 3600.0, AchievementUnit.Hours), UpgradeEffect.ProductionBonus, 0.05f);
            Points(Add("play_50h", "Lifer", "Play for 50 hours.", 50, m => m.State.PlaySeconds / 3600.0, AchievementUnit.Hours), 3);
            icon = "star";
            Points(Add("ach_25", "Overachiever", "Unlock 25 other achievements.", 25, m => m.State.Achievements.Count), 5);
            Bonus(Add("ach_50", "Hall of Fame", "Unlock 50 other achievements.", 50, m => m.State.Achievements.Count), UpgradeEffect.SellBonus, 0.1f);
            return list.ToArray();
        }

        // ---------------------------------------------------------------- text

        /// <summary>"+5% axe damage", "$1,500", "3 skill points".</summary>
        public static string RewardText(GameDatabase db, AchievementDef a)
        {
            if (a.skillPoints > 0) return a.skillPoints == 1 ? "1 skill point" : a.skillPoints + " skill points";
            if (a.money > 0) return NumberUtil.Money(a.money);
            if (!a.bonus.HasValue) return "";
            string pct = "+" + (a.bonusAmount * 100f).ToString("0") + "%";
            string Name(string id) => db != null && db.Machine(id) != null ? db.Machine(id).displayName : id;
            string Res(string id) => db != null && db.Resource(id) != null ? db.Resource(id).displayName.ToLower() : id;
            switch (a.bonus.Value)
            {
                case UpgradeEffect.AxeDamage: return pct + " axe damage";
                case UpgradeEffect.AxeSpeed: return pct + " swing speed";
                case UpgradeEffect.WoodYield: return pct + " wood per tree";
                case UpgradeEffect.BackpackBonus: return pct + " backpack capacity";
                case UpgradeEffect.SellBonus: return pct + " on all sales";
                case UpgradeEffect.ProductionBonus: return pct + " machine output";
                case UpgradeEffect.MachineSpeed: return pct + " " + Name(a.bonusTarget) + " output";
                case UpgradeEffect.ResourcePrice: return pct + " " + Res(a.bonusTarget) + " price";
                case UpgradeEffect.MoveSpeed: return pct + " move speed";
                case UpgradeEffect.TreeCashBonus: return pct + " tree cash";
                case UpgradeEffect.LuckyFind: return pct + " lucky find chance";
                case UpgradeEffect.OfflineHours: return "+" + a.bonusAmount.ToString("0") + "h offline limit";
                case UpgradeEffect.UpgradeDiscount: return "-" + (a.bonusAmount * 100f).ToString("0") + "% upgrade prices";
                case UpgradeEffect.MachineDiscount: return "-" + (a.bonusAmount * 100f).ToString("0") + "% machine prices";
                case UpgradeEffect.RebirthBonus: return pct + " earnings per rebirth";
            }
            return pct;
        }

        /// <summary>"340 / 500", "$12.5K / $100K", "3.2h / 10h".</summary>
        public static string ProgressText(AchievementDef a, double value)
        {
            value = Math.Min(value, a.goal);
            switch (a.unit)
            {
                case AchievementUnit.Money: return NumberUtil.Money(value) + " / " + NumberUtil.Money(a.goal);
                case AchievementUnit.Hours: return value.ToString("0.0") + "h / " + a.goal.ToString("0") + "h";
                default: return NumberUtil.Format(Math.Floor(value)) + " / " + NumberUtil.Format(a.goal);
            }
        }
    }
}
