using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Creates or updates all game data assets (Tools > Chopping Trees > 5. Build Game Data):
    /// resources, tree species, machines, forest areas, upgrades, the config and the database.
    /// The values here are the starting balance; they can be tuned afterwards in the Inspector
    /// (re-running this overwrites them with the values below).
    /// </summary>
    public static class ContentBuilder
    {
        const string Data = ProjectSetup.Root + "/Data";
        public const string DatabasePath = ProjectSetup.Root + "/Resources/GameDatabase.asset";

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            EditorUtility.SetDirty(a);
            return a;
        }

        [MenuItem("Tools/Chopping Trees/5. Build Game Data", priority = 5)]
        public static string BuildAll()
        {
            foreach (var d in new[] { "Trees", "Upgrades", "Machines", "Areas", "Resources" })
                System.IO.Directory.CreateDirectory($"{Data}/{d}");
            System.IO.Directory.CreateDirectory(ProjectSetup.Root + "/Resources");

            // ---- resources
            var wood = Asset<ResourceDef>($"{Data}/Resources/Wood.asset");
            wood.id = "wood"; wood.displayName = "Wood"; wood.basePrice = 1; wood.color = new Color(0.78f, 0.56f, 0.33f); wood.sortOrder = 0; wood.hiddenUntilProduced = false;
            var planks = Asset<ResourceDef>($"{Data}/Resources/Planks.asset");
            planks.id = "planks"; planks.displayName = "Planks"; planks.basePrice = 3.5; planks.color = new Color(0.95f, 0.80f, 0.55f); planks.sortOrder = 1; planks.hiddenUntilProduced = true;

            // ---- trees: id, name, model, stump, hp, wood, cash, tier, respawn, radius, height, notch, logs, pitch, chip, leaf
            var pine = Tree("pine", "Pine", "Tree_Pine", "Stump_Bark", 3, 4, 0, 0, 25, 0.28f, 7.0f, 0.55f, 2, 1.1f, new Color(0.9f, 0.75f, 0.5f), new Color(0.16f, 0.38f, 0.25f));
            var birch = Tree("birch", "Birch", "Tree_Birch", "Stump_Birch", 5, 7, 0, 0, 28, 0.22f, 7.4f, 0.5f, 2, 1.15f, new Color(0.93f, 0.85f, 0.68f), new Color(0.55f, 0.72f, 0.3f));
            var oak = Tree("oak", "Oak", "Tree_Oak", "Stump_Bark", 16, 18, 0, 1, 40, 0.45f, 7.5f, 0.6f, 3, 0.95f, new Color(0.85f, 0.68f, 0.45f), new Color(0.33f, 0.58f, 0.24f));
            var maple = Tree("maple", "Maple", "Tree_Maple", "Stump_Bark", 70, 55, 0, 2, 45, 0.5f, 9.0f, 0.65f, 3, 0.9f, new Color(0.9f, 0.72f, 0.5f), new Color(0.88f, 0.38f, 0.16f));
            var redwood = Tree("redwood", "Redwood", "Tree_Redwood", "Stump_Red", 170, 150, 0, 2, 60, 0.85f, 17f, 0.8f, 5, 0.75f, new Color(0.8f, 0.5f, 0.38f), new Color(0.19f, 0.42f, 0.21f));
            var iron = Tree("ironwood", "Ironwood", "Tree_Ironwood", "Stump_Iron", 650, 560, 0, 3, 60, 0.75f, 10.5f, 0.75f, 4, 0.8f, new Color(0.62f, 0.64f, 0.68f), new Color(0.22f, 0.54f, 0.56f));
            var gold = Tree("goldbark", "Goldbark", "Tree_Goldbark", "Stump_Gold", 2600, 1500, 12000, 4, 120, 0.6f, 9.5f, 0.7f, 4, 1.0f, new Color(1f, 0.8f, 0.35f), new Color(1f, 0.8f, 0.3f));
            pine.scaleRange = new Vector2(0.85f, 1.2f);
            redwood.scaleRange = new Vector2(0.9f, 1.1f);

            // ---- machines
            var chopper = Asset<MachineDef>($"{Data}/Machines/AutoChopper.asset");
            chopper.id = "chopper"; chopper.displayName = "Auto-Chopper"; chopper.kind = MachineKind.Producer; chopper.baseRate = 0.8; chopper.output = wood; chopper.input = null;
            chopper.description = "Cuts logs on its own and adds the wood to your stock.";
            chopper.tierModels = new[] { "AutoChopper", "AutoChopper_Mk2", "AutoChopper_Mk3" }; chopper.order = 0;
            var sawmill = Asset<MachineDef>($"{Data}/Machines/Sawmill.asset");
            sawmill.id = "sawmill"; sawmill.displayName = "Sawmill"; sawmill.kind = MachineKind.Processor; sawmill.baseRate = 2.5; sawmill.input = wood; sawmill.inputPerOutput = 2; sawmill.output = planks;
            sawmill.description = "Turns wood into planks, which sell for more.";
            sawmill.tierModels = new[] { "Sawmill" }; sawmill.order = 1;

            // ---- areas
            var clearing = Area("clearing", "Pinewood Clearing", "Where it all begins.", true, 0, new[] { pine, birch, oak }, new[] { 0.5f, 0.32f, 0.18f });
            var grove = Area("grove", "Old Grove", "Ancient maples and towering redwoods.", false, 1, new[] { maple, redwood, oak }, new[] { 0.45f, 0.3f, 0.25f });
            var ridge = Area("ridge", "Ironwood Ridge", "Iron-hard trees and the legendary Goldbark.", false, 2, new[] { iron, redwood, gold }, new[] { 0.72f, 0.18f, 0.1f });

            // ---- upgrades
            var ups = new List<UpgradeDef>
            {
                Up("axe_tier", "Better Axe", "Forge a stronger axe. Each one hits harder and can cut tougher trees.", UpgradeCategory.Axe, UpgradeEffect.AxeTier, null, 4, 0, 1, new double[] { 50, 600, 7500, 120000 }, 1, 0,
                    new[] { "Steel Axe", "Lumberjack's Axe", "Tempered Broadaxe", "Ironbreaker" }),
                Up("axe_sharpen", "Whetstone", "Keep the edge keen: more damage with every swing.", UpgradeCategory.Axe, UpgradeEffect.AxeDamage, null, 15, 25, 1.55, null, 0.2f, 1,
                    null, Req("axe_tier", 1)),
                Up("axe_speed", "Grip Training", "Faster, steadier swings.", UpgradeCategory.Axe, UpgradeEffect.AxeSpeed, null, 10, 60, 1.7, null, 0.08f, 2,
                    null, Req("axe_sharpen", 1)),
                Up("backpack", "Backpack", "Carry more wood on each trip to the market. Machine output goes to the yard, never your pack.", UpgradeCategory.Axe, UpgradeEffect.BackpackCapacity, null, 6, 0, 6,
                    new double[] { 50, 350, 2200, 15000, 100000, 700000 }, 1, 3,
                    new[] { "Canvas Satchel", "Leather Rucksack", "Frame Pack", "Logger's Pack", "Expedition Pack", "Hauler's Harness" }, Req("axe_tier", 1)),

                Up("chopper_count", "Auto-Chopper", "A petrol-powered saw that cuts logs on its own. More machines, more wood.", UpgradeCategory.Automation, UpgradeEffect.MachineCount, "chopper", 8, 0, 2.4,
                    new double[] { 200, 1200, 3200, 7500, 16000, 36000, 80000, 180000 }, 1, 0),
                Up("chopper_speed", "Chopper Motors", "Bigger engines for every Auto-Chopper.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "chopper", 12, 500, 1.8, null, 0.3f, 1,
                    null, Req("chopper_count", 1)),
                Up("sawmill_count", "Sawmill", "Turns 2 wood into 1 plank. Planks sell for much more than the wood they use.", UpgradeCategory.Automation, UpgradeEffect.MachineCount, "sawmill", 3, 0, 8,
                    new double[] { 5000, 45000, 400000 }, 1, 2, null, Req("chopper_count", 1)),
                Up("sawmill_speed", "Sawmill Blades", "Sharper, faster blades for every sawmill.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "sawmill", 10, 8000, 2.1, null, 0.3f, 3,
                    null, Req("sawmill_count", 1)),
                Up("foreman", "Foreman's Whistle", "Every machine works a little harder.", UpgradeCategory.Automation, UpgradeEffect.ProductionBonus, null, 10, 2500, 2.3, null, 0.1f, 4,
                    null, Req("chopper_count", 1)),
                Up("chopper_tier", "Harvester Line", "Rebuild every Auto-Chopper as a heavier, faster model.", UpgradeCategory.Automation, UpgradeEffect.MachineTier, "chopper", 2, 0, 16,
                    new double[] { 40000, 650000 }, 3, 5, new[] { "Heavy Harvester", "Titan Harvester" }, Req("chopper_speed", 1), Req("area_grove", 1)),

                Up("wood_price", "Market Contacts", "Better buyers pay more for your wood.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "wood", 15, 100, 1.75, null, 0.2f, 0),
                Up("plank_price", "Furniture Buyers", "Carpenters pay a premium for your planks.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "planks", 10, 7500, 1.9, null, 0.25f, 1,
                    null, Req("sawmill_count", 1)),
                Up("magnet", "Log Magnet", "Logs fly into your backpack from further away. Each level adds range.", UpgradeCategory.Economy, UpgradeEffect.MagnetRange, null, 10, 1000, 1.7, null, 1.5f, 2,
                    null, Req("wood_price", 1)),
                Up("haggling", "Haggling", "Talk the buyers up: every sale pays a little more, wood and planks alike.", UpgradeCategory.Economy, UpgradeEffect.SellBonus, null, 8, 1500, 1.9, null, 0.05f, 4,
                    null, Req("wood_price", 2)),
                Up("lucky_finds", "Lucky Finds", "Sometimes a felled tree hides a forgotten stash of cash.", UpgradeCategory.Economy, UpgradeEffect.LuckyFind, null, 5, 5000, 2.3, null, 0.03f, 5,
                    null, Req("haggling", 1)),

                Up("clean_cuts", "Clean Cuts", "Less waste: every tree you fell gives more wood.", UpgradeCategory.Axe, UpgradeEffect.WoodYield, null, 10, 300, 1.9, null, 0.1f, 4,
                    null, Req("backpack", 1)),
                Up("trail_boots", "Trail Boots", "Sturdy boots for quicker trips between the trees and the market.", UpgradeCategory.Axe, UpgradeEffect.MoveSpeed, null, 5, 400, 2.2, null, 0.05f, 5,
                    null, Req("clean_cuts", 1)),
                Up("lucky_strike", "Lucky Strike", "A chance for any swing to land perfectly and deal double damage.", UpgradeCategory.Axe, UpgradeEffect.CritChance, null, 10, 800, 1.9, null, 0.03f, 6,
                    null, Req("axe_speed", 1)),

                Up("spare_parts", "Spare Parts", "A stock of parts makes every new machine cheaper to build.", UpgradeCategory.Automation, UpgradeEffect.MachineDiscount, null, 6, 3000, 2.0, null, 0.05f, 6,
                    null, Req("foreman", 1)),
                Up("precision_saws", "Precision Saws", "Thinner kerfs: sawmills get more planks out of the same wood.", UpgradeCategory.Automation, UpgradeEffect.ProcessorYield, "sawmill", 5, 25000, 2.1, null, 0.1f, 7,
                    null, Req("sawmill_speed", 1)),

                Up("fertile_soil", "Fertile Soil", "Felled trees grow back faster in every forest.", UpgradeCategory.Forest, UpgradeEffect.TreeRegrowth, null, 5, 6000, 2.2, null, 0.15f, 2,
                    null, Req("area_grove", 1)),
                Up("gold_prospector", "Gold Prospector", "Know where to look: Goldbark trees drop more cash.", UpgradeCategory.Forest, UpgradeEffect.TreeCashBonus, null, 5, 400000, 2.0, null, 0.25f, 3,
                    null, Req("area_ridge", 1)),

                // ---- More perks for every branch (mid and late game).
                Up("honed_edge", "Honed Edge", "A mirror-polished edge: much more damage per swing.", UpgradeCategory.Axe, UpgradeEffect.AxeDamage, null, 10, 2500, 1.8, null, 0.3f, 7,
                    null, Req("axe_sharpen", 5)),
                Up("steady_rhythm", "Steady Rhythm", "Find the rhythm: swing faster still.", UpgradeCategory.Axe, UpgradeEffect.AxeSpeed, null, 8, 4000, 1.9, null, 0.06f, 8,
                    null, Req("axe_speed", 3)),
                Up("cleave", "Cleave", "Now and then a single perfect swing brings the whole tree down.", UpgradeCategory.Axe, UpgradeEffect.FellChance, null, 5, 30000, 2.4, null, 0.01f, 9,
                    null, Req("steady_rhythm", 1)),
                Up("lucky_blade", "Lucky Blade", "Even more swings land perfectly.", UpgradeCategory.Axe, UpgradeEffect.CritChance, null, 10, 6000, 1.9, null, 0.02f, 10,
                    null, Req("lucky_strike", 5)),
                Up("power_swing", "Power Swing", "Perfect swings hit even harder.", UpgradeCategory.Axe, UpgradeEffect.CritDamage, null, 5, 20000, 2.2, null, 0.5f, 11,
                    null, Req("lucky_blade", 1)),
                Up("pack_mule", "Pack Mule", "Pack smarter: every backpack holds more.", UpgradeCategory.Axe, UpgradeEffect.BackpackBonus, null, 5, 5000, 2.2, null, 0.25f, 12,
                    null, Req("backpack", 3)),
                Up("timber_sense", "Timber Sense", "Read the grain: still more wood from every tree.", UpgradeCategory.Axe, UpgradeEffect.WoodYield, null, 5, 8000, 2.1, null, 0.15f, 13,
                    null, Req("clean_cuts", 5)),
                Up("long_strides", "Long Strides", "Cover ground faster between trees and the market.", UpgradeCategory.Axe, UpgradeEffect.MoveSpeed, null, 5, 6000, 2.0, null, 0.04f, 14,
                    null, Req("trail_boots", 3)),

                Up("old_growth", "Old Growth", "Ancient trees: more wood from every tree you fell.", UpgradeCategory.Forest, UpgradeEffect.WoodYield, null, 5, 30000, 2.0, null, 0.1f, 4,
                    null, Req("area_grove", 1)),
                Up("timber_rights", "Timber Rights", "Logging rights for the old forests raise what wood is worth.", UpgradeCategory.Forest, UpgradeEffect.ResourcePrice, "wood", 10, 40000, 1.8, null, 0.1f, 5,
                    null, Req("old_growth", 1)),
                Up("pathfinder", "Pathfinder", "Know the trails: move faster everywhere.", UpgradeCategory.Forest, UpgradeEffect.MoveSpeed, null, 3, 50000, 2.5, null, 0.05f, 6,
                    null, Req("timber_rights", 1)),
                Up("rangers_eye", "Ranger's Eye", "Spot every log: a longer magnet range.", UpgradeCategory.Forest, UpgradeEffect.MagnetRange, null, 5, 60000, 2.0, null, 1f, 7,
                    null, Req("pathfinder", 1)),
                Up("deep_roots", "Deep Roots", "Stumps sprout back faster still.", UpgradeCategory.Forest, UpgradeEffect.TreeRegrowth, null, 5, 40000, 2.2, null, 0.15f, 8,
                    null, Req("fertile_soil", 3)),
                Up("forest_spirits", "Forest Spirits", "The woods reward you: more lucky finds.", UpgradeCategory.Forest, UpgradeEffect.LuckyFind, null, 5, 80000, 2.2, null, 0.02f, 9,
                    null, Req("deep_roots", 1)),
                Up("golden_canopy", "Golden Canopy", "Goldbark trees glitter with even more cash.", UpgradeCategory.Forest, UpgradeEffect.TreeCashBonus, null, 5, 1500000, 2.0, null, 0.25f, 10,
                    null, Req("gold_prospector", 3)),

                Up("twin_engines", "Twin Engines", "Two engines in every Auto-Chopper.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "chopper", 5, 150000, 2.3, null, 0.5f, 8,
                    null, Req("chopper_tier", 1)),
                Up("turbo_choppers", "Turbo Choppers", "Turbochargers for the whole fleet.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "chopper", 10, 400000, 1.9, null, 0.25f, 9,
                    null, Req("twin_engines", 1)),
                Up("fuel_efficiency", "Fuel Efficiency", "Better fuel: every machine works harder.", UpgradeCategory.Automation, UpgradeEffect.ProductionBonus, null, 10, 12000, 1.9, null, 0.08f, 10,
                    null, Req("spare_parts", 2)),
                Up("assembly_line", "Assembly Line", "Build machines on a line: they cost less.", UpgradeCategory.Automation, UpgradeEffect.MachineDiscount, null, 5, 30000, 2.2, null, 0.03f, 11,
                    null, Req("fuel_efficiency", 1)),
                Up("overtime_crew", "Overtime Crew", "A crew keeps the machines going longer while you're away.", UpgradeCategory.Automation, UpgradeEffect.OfflineHours, null, 5, 50000, 2.2, null, 1f, 12,
                    null, Req("assembly_line", 1)),
                Up("kiln_drying", "Kiln Drying", "Dried wood splits cleaner: more planks per cut.", UpgradeCategory.Automation, UpgradeEffect.ProcessorYield, "sawmill", 5, 120000, 2.1, null, 0.08f, 13,
                    null, Req("precision_saws", 3)),
                Up("overdrive_saws", "Overdrive Saws", "Sawmills run in overdrive.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "sawmill", 10, 250000, 1.9, null, 0.25f, 14,
                    null, Req("kiln_drying", 1)),

                Up("wood_brokers", "Wood Brokers", "Brokers find buyers who pay top price for wood.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "wood", 15, 3000, 1.7, null, 0.2f, 6,
                    null, Req("wood_price", 10)),
                Up("premium_planks", "Premium Planks", "Graded planks sell at a premium.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "planks", 10, 60000, 1.9, null, 0.25f, 7,
                    null, Req("plank_price", 5)),
                Up("strong_magnet", "Strong Magnet", "A bigger magnet: even longer pickup range.", UpgradeCategory.Economy, UpgradeEffect.MagnetRange, null, 10, 8000, 1.8, null, 1f, 8,
                    null, Req("magnet", 5)),
                Up("long_nights", "Long Nights", "Machines keep earning far longer while the game is closed.", UpgradeCategory.Economy, UpgradeEffect.OfflineHours, null, 5, 20000, 2.3, null, 2f, 9,
                    null, Req("night_shift", 5)),
                Up("export_deals", "Export Deals", "Sell abroad: every sale pays more.", UpgradeCategory.Economy, UpgradeEffect.SellBonus, null, 10, 25000, 1.9, null, 0.05f, 10,
                    null, Req("haggling", 5)),
                Up("fortunes_favor", "Fortune's Favor", "Lucky finds turn up more often.", UpgradeCategory.Economy, UpgradeEffect.LuckyFind, null, 5, 40000, 2.2, null, 0.02f, 11,
                    null, Req("lucky_finds", 3)),
                Up("bulk_buyer", "Bulk Buyer", "Bulk orders make every other upgrade cheaper.", UpgradeCategory.Economy, UpgradeEffect.UpgradeDiscount, null, 10, 50000, 1.9, null, 0.02f, 12,
                    null, Req("fortunes_favor", 1)),

                // ---- Legacy perks: bought with skill points (1 per rebirth), kept through rebirths.
                // Costs are in skill points (rounded up).
                Legacy(Up("heirloom_axe", "Heirloom Axe", "Start every run with a better axe already forged.", UpgradeCategory.Legacy, UpgradeEffect.StartingAxe, null, 3, 0, 1,
                    new double[] { 2, 4, 8 }, 1, 0)),
                Legacy(Up("head_start", "Head Start", "Start every run with money in your pocket.", UpgradeCategory.Legacy, UpgradeEffect.StartingMoney, null, 4, 0, 1,
                    new double[] { 1, 2, 4, 8 }, 500, 1)),
                Legacy(Up("starter_kit", "Starter Kit", "Start every run with Auto-Choppers already running.", UpgradeCategory.Legacy, UpgradeEffect.StartingMachines, "chopper", 4, 0, 1,
                    new double[] { 2, 3, 5, 8 }, 1, 2)),
                Legacy(Up("forest_charter", "Forest Charter", "Start every run with the forest gates already open.", UpgradeCategory.Legacy, UpgradeEffect.StartingAreas, null, 2, 0, 1,
                    new double[] { 4, 10 }, 1, 3)),
                Legacy(Up("titan_grip", "Titan Grip", "A grip that never tires: far more damage with every swing.", UpgradeCategory.Legacy, UpgradeEffect.AxeDamage, null, 10, 1, 1.3, null, 0.5f, 4)),
                Legacy(Up("quick_hands", "Quick Hands", "Swing faster, forever.", UpgradeCategory.Legacy, UpgradeEffect.AxeSpeed, null, 5, 1, 1.5, null, 0.1f, 5)),
                Legacy(Up("deep_pockets", "Deep Pockets", "Every backpack holds much more.", UpgradeCategory.Legacy, UpgradeEffect.BackpackBonus, null, 5, 1, 1.5, null, 0.5f, 6)),
                Legacy(Up("lodestone", "Lodestone", "A permanently stronger log magnet.", UpgradeCategory.Legacy, UpgradeEffect.MagnetRange, null, 5, 1, 1.4, null, 3f, 7)),
                Legacy(Up("master_engineer", "Master Engineer", "Every machine you build runs harder.", UpgradeCategory.Legacy, UpgradeEffect.ProductionBonus, null, 10, 1, 1.3, null, 0.25f, 8)),
                Legacy(Up("silver_tongue", "Silver Tongue", "Buyers trust you: everything sells for more.", UpgradeCategory.Legacy, UpgradeEffect.SellBonus, null, 10, 1, 1.3, null, 0.15f, 9)),
                Up("night_shift", "Night Shift", "Machines keep working longer while the game is closed.", UpgradeCategory.Economy, UpgradeEffect.OfflineHours, null, 5, 2000, 3, null, 2f, 3,
                    null, Req("wood_price", 1), Req("chopper_count", 1)),

                Up("area_grove", "Old Grove", "Open the gate to the Old Grove: maples and towering redwoods.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "grove", 1, 0, 1, new double[] { 25000 }, 1, 0,
                    null, Req("axe_tier", 2)),
                Up("area_ridge", "Ironwood Ridge", "Open the gate to Ironwood Ridge: iron-hard trees and the legendary Goldbark.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "ridge", 1, 0, 1, new double[] { 350000 }, 1, 1,
                    null, Req("area_grove", 1), Req("axe_tier", 3)),
            };

            // ---- skill tree layout (x = -8..7 left to right, y = 0..8 from the trunk up) and icons.
            // Branches read left to right: Forests, Axe, Automation, Economy; each grows upwards
            // in columns from its first upgrade (rows 1.35 apart), with links curving between rows.
            // The tree is bigger than the window: it is dragged and zoomed to explore.
            var layout = new Dictionary<string, (float x, float y, string icon)>
            {
                // Forests: three columns on the far left, opened from the axe.
                ["area_grove"] = (-6.6f, 2.65f, "pine"),
                ["area_ridge"] = (-6.6f, 3.95f, "mountain"),
                ["gold_prospector"] = (-6.6f, 5.25f, "nugget"),
                ["golden_canopy"] = (-6.6f, 6.55f, "star"),
                ["fertile_soil"] = (-7.7f, 3.95f, "sprout"),
                ["deep_roots"] = (-7.7f, 5.25f, "leaf"),
                ["forest_spirits"] = (-7.7f, 6.55f, "clover"),
                ["old_growth"] = (-5.5f, 3.95f, "pine"),
                ["timber_rights"] = (-5.5f, 5.25f, "tag"),
                ["pathfinder"] = (-5.5f, 6.55f, "boot"),
                ["rangers_eye"] = (-5.5f, 7.85f, "magnet"),

                // Axe: the axe line in the middle, power on the left, carrying on the right.
                ["axe_tier"] = (-3.3f, 1.35f, "axe"),
                ["axe_sharpen"] = (-3.3f, 2.65f, "gem"),
                ["axe_speed"] = (-3.3f, 3.95f, "bolt"),
                ["lucky_strike"] = (-3.3f, 5.25f, "target"),
                ["lucky_blade"] = (-3.3f, 6.55f, "clover"),
                ["power_swing"] = (-3.3f, 7.85f, "star"),
                ["honed_edge"] = (-4.4f, 3.95f, "gem"),
                ["steady_rhythm"] = (-4.4f, 5.25f, "bolt"),
                ["cleave"] = (-4.4f, 6.55f, "axe"),
                ["backpack"] = (-2.2f, 2.65f, "backpack"),
                ["clean_cuts"] = (-2.2f, 3.95f, "log"),
                ["trail_boots"] = (-2.2f, 5.25f, "boot"),
                ["long_strides"] = (-2.2f, 6.55f, "boot"),
                ["pack_mule"] = (-1.1f, 3.95f, "backpack"),
                ["timber_sense"] = (-1.1f, 5.25f, "log"),

                // Automation: choppers, the workshop, sawmills.
                ["chopper_count"] = (1.1f, 1.35f, "saw"),
                ["chopper_speed"] = (0.0f, 2.65f, "gear"),
                ["chopper_tier"] = (0.0f, 3.95f, "crown"),
                ["twin_engines"] = (0.0f, 5.25f, "gear"),
                ["turbo_choppers"] = (0.0f, 6.55f, "bolt"),
                ["foreman"] = (1.1f, 2.65f, "chart"),
                ["spare_parts"] = (1.1f, 3.95f, "wrench"),
                ["fuel_efficiency"] = (1.1f, 5.25f, "chart"),
                ["assembly_line"] = (1.1f, 6.55f, "wrench"),
                ["overtime_crew"] = (1.1f, 7.85f, "clock"),
                ["sawmill_count"] = (2.2f, 2.65f, "plank"),
                ["sawmill_speed"] = (2.2f, 3.95f, "bolt"),
                ["precision_saws"] = (2.2f, 5.25f, "saw"),
                ["kiln_drying"] = (2.2f, 6.55f, "plank"),
                ["overdrive_saws"] = (2.2f, 7.85f, "saw"),

                // Economy: prices, pickup, offline time, sales and luck.
                ["wood_price"] = (4.7f, 1.35f, "coin"),
                ["haggling"] = (5.8f, 1.35f, "tag"),
                ["export_deals"] = (6.9f, 1.35f, "cart"),
                ["wood_brokers"] = (3.6f, 2.65f, "coin"),
                ["plank_price"] = (3.6f, 3.95f, "star"),
                ["premium_planks"] = (3.6f, 5.25f, "plank"),
                ["magnet"] = (4.7f, 2.65f, "magnet"),
                ["strong_magnet"] = (4.7f, 3.95f, "magnet"),
                ["night_shift"] = (5.8f, 2.65f, "moon"),
                ["long_nights"] = (5.8f, 3.95f, "moon"),
                ["lucky_finds"] = (6.9f, 2.65f, "clover"),
                ["fortunes_favor"] = (6.9f, 3.95f, "clover"),
                ["bulk_buyer"] = (6.9f, 5.25f, "tag"),
            };
            foreach (var u in ups)
                if (layout.TryGetValue(u.id, out var l)) { u.treePosition = new Vector2(l.x, l.y); u.icon = l.icon; }

            // Legacy perks are shown as cards in the Rebirth screen (no tree position).
            var legacyIcons = new Dictionary<string, string>
            {
                ["heirloom_axe"] = "axe", ["head_start"] = "coin", ["starter_kit"] = "saw", ["forest_charter"] = "pine",
                ["titan_grip"] = "gem", ["quick_hands"] = "bolt", ["deep_pockets"] = "backpack", ["lodestone"] = "magnet",
                ["master_engineer"] = "gear", ["silver_tongue"] = "tag",
            };
            foreach (var u in ups)
                if (legacyIcons.TryGetValue(u.id, out var icon)) { u.icon = icon; u.treePosition = Vector2.zero; }

            // ---- config and database
            var cfg = Asset<GameConfig>($"{Data}/GameConfig.asset");
            // Wood carried by hand per backpack tier (machine output goes to the yard instead).
            cfg.backpackCapacity = new double[] { 50, 150, 400, 1000, 2500, 6000, 15000 };
            cfg.baseBackpackName = "Burlap Sack";
            // Rebirths: first one $5,000, each next one x1.08; every rebirth adds +10% to all earnings.
            cfg.rebirthBaseCost = 5000;
            cfg.rebirthCostGrowth = 1.08;
            cfg.rebirthBonus = 0.1;
            cfg.rebirthSkillPoints = 1;
            var db = Asset<GameDatabase>(DatabasePath);
            db.config = cfg;
            db.resources = new[] { wood, planks };
            db.trees = new[] { pine, birch, oak, maple, redwood, iron, gold };
            db.machines = new[] { chopper, sawmill };
            db.areas = new[] { clearing, grove, ridge };
            db.upgrades = ups.ToArray();
            db.bodyFont = ProjectSetup.Font("Inter-Regular");
            db.boldFont = ProjectSetup.Font("Inter-SemiBold");
            db.audio = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioSynth.LibraryPath);
            AssetDatabase.SaveAssets();
            return $"data: {db.trees.Length} trees, {db.upgrades.Length} upgrades, {db.machines.Length} machines, {db.areas.Length} areas, audio {(db.audio != null ? "ok" : "MISSING")}";
        }

        static UpgradeDef.Requirement Req(string id, int level) => new UpgradeDef.Requirement(id, level);

        static TreeType Tree(string id, string name, string model, string stump, double hp, double wood, double cash, int tier, float respawn,
            float radius, float height, float notch, int logs, float pitch, Color chip, Color leaf)
        {
            var t = Asset<TreeType>($"{Data}/Trees/{name}.asset");
            t.id = id; t.displayName = name; t.modelName = model; t.stumpModel = stump;
            t.maxHealth = hp; t.woodReward = wood; t.cashBonus = cash; t.requiredAxeTier = tier; t.respawnSeconds = respawn;
            t.trunkRadius = radius; t.trunkHeight = height; t.notchHeight = notch; t.logPieces = logs; t.soundPitch = pitch;
            t.chipColor = chip; t.leafColor = leaf; t.scaleRange = new Vector2(0.9f, 1.15f);
            return t;
        }

        static ForestAreaDef Area(string id, string name, string desc, bool unlocked, int order, TreeType[] trees, float[] weights)
        {
            var a = Asset<ForestAreaDef>($"{Data}/Areas/{name.Replace("'", "")}.asset");
            a.id = id; a.displayName = name; a.description = desc; a.unlockedByDefault = unlocked; a.sortOrder = order; a.trees = trees; a.weights = weights;
            return a;
        }

        static UpgradeDef Up(string id, string name, string desc, UpgradeCategory cat, UpgradeEffect effect, string target, int max, double baseCost, double growth,
            double[] costs, float eff, int sort, string[] levelNames = null, params UpgradeDef.Requirement[] requires)
        {
            var u = Asset<UpgradeDef>($"{Data}/Upgrades/{id}.asset");
            u.id = id; u.displayName = name; u.description = desc; u.category = cat; u.effect = effect; u.targetId = target;
            u.maxLevel = max; u.baseCost = baseCost; u.costGrowth = growth; u.explicitCosts = costs; u.effectPerLevel = eff; u.sortOrder = sort;
            u.levelNames = levelNames; u.requires = requires; u.legacy = false;
            return u;
        }

        static UpgradeDef Legacy(UpgradeDef u)
        {
            u.legacy = true;
            return u;
        }
    }
}
