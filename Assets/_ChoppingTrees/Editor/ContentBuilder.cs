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

        [MenuItem("Tools/Forest to Fortune/5. Build Game Data", priority = 5)]
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
            wood.icon = "log"; planks.icon = "plank";
            // The production chain goes on: planks -> furniture (Carpentry Workshop) -> cabin kits (Cabin Builder).
            var furniture = Asset<ResourceDef>($"{Data}/Resources/Furniture.asset");
            furniture.id = "furniture"; furniture.displayName = "Furniture"; furniture.basePrice = 16; furniture.color = new Color(0.86f, 0.58f, 0.36f); furniture.sortOrder = 2; furniture.hiddenUntilProduced = true; furniture.icon = "chair";
            var cabins = Asset<ResourceDef>($"{Data}/Resources/CabinKits.asset");
            cabins.id = "cabins"; cabins.displayName = "Cabin Kits"; cabins.basePrice = 130; cabins.color = new Color(0.93f, 0.47f, 0.33f); cabins.sortOrder = 3; cabins.hiddenUntilProduced = true; cabins.icon = "house";

            // ---- trees: id, name, model, stump, hp, wood, cash, tier, respawn, radius, height, notch, logs, pitch, chip, leaf
            var pine = Tree("pine", "Pine", "Tree_Pine", "Stump_Bark", 3, 4, 0, 0, 25, 0.28f, 7.0f, 0.55f, 2, 1.1f, new Color(0.9f, 0.75f, 0.5f), new Color(0.16f, 0.38f, 0.25f));
            var birch = Tree("birch", "Birch", "Tree_Birch", "Stump_Birch", 5, 7, 0, 0, 28, 0.22f, 7.4f, 0.5f, 2, 1.15f, new Color(0.93f, 0.85f, 0.68f), new Color(0.55f, 0.72f, 0.3f));
            var oak = Tree("oak", "Oak", "Tree_Oak", "Stump_Bark", 16, 18, 0, 1, 40, 0.45f, 7.5f, 0.6f, 3, 0.95f, new Color(0.85f, 0.68f, 0.45f), new Color(0.33f, 0.58f, 0.24f));
            var maple = Tree("maple", "Maple", "Tree_Maple", "Stump_Bark", 70, 55, 0, 2, 45, 0.5f, 9.0f, 0.65f, 3, 0.9f, new Color(0.9f, 0.72f, 0.5f), new Color(0.88f, 0.38f, 0.16f));
            var redwood = Tree("redwood", "Redwood", "Tree_Redwood", "Stump_Red", 170, 150, 0, 2, 60, 0.85f, 17f, 0.8f, 5, 0.75f, new Color(0.8f, 0.5f, 0.38f), new Color(0.19f, 0.42f, 0.21f));
            var iron = Tree("ironwood", "Ironwood", "Tree_Ironwood", "Stump_Iron", 650, 560, 0, 3, 60, 0.75f, 10.5f, 0.75f, 4, 0.8f, new Color(0.62f, 0.64f, 0.68f), new Color(0.22f, 0.54f, 0.56f));
            var gold = Tree("goldbark", "Goldbark", "Tree_Goldbark", "Stump_Gold", 2600, 1500, 12000, 4, 120, 0.6f, 9.5f, 0.7f, 4, 1.0f, new Color(1f, 0.8f, 0.35f), new Color(1f, 0.8f, 0.3f));
            // The three far forests: snowy peaks, a blossom valley and the crystal wilds.
            var frost = Tree("frostpine", "Frostpine", "Tree_Frostpine", "Stump_Bark", 8000, 4800, 0, 5, 60, 0.42f, 9.5f, 0.6f, 3, 0.85f, new Color(0.92f, 0.84f, 0.72f), new Color(0.82f, 0.9f, 0.96f));
            var cherry = Tree("cherry", "Cherry", "Tree_Cherry", "Stump_Cherry", 26000, 15000, 0, 6, 60, 0.42f, 7.5f, 0.6f, 3, 0.95f, new Color(0.93f, 0.72f, 0.62f), new Color(1f, 0.72f, 0.84f));
            var ember = Tree("emberwood", "Emberwood", "Tree_Emberwood", "Stump_Ember", 42000, 24000, 0, 6, 70, 0.55f, 9.0f, 0.65f, 4, 0.8f, new Color(0.96f, 0.56f, 0.3f), new Color(1f, 0.46f, 0.18f));
            var crystal = Tree("crystalwood", "Crystalwood", "Tree_Crystalwood", "Stump_Crystal", 140000, 60000, 450000, 7, 120, 0.55f, 9.5f, 0.7f, 4, 1.1f, new Color(0.72f, 0.86f, 1f), new Color(0.56f, 0.86f, 1f));
            pine.scaleRange = new Vector2(0.85f, 1.2f);
            redwood.scaleRange = new Vector2(0.9f, 1.1f);
            frost.scaleRange = new Vector2(0.85f, 1.2f);

            // ---- machines
            var chopper = Asset<MachineDef>($"{Data}/Machines/AutoChopper.asset");
            chopper.id = "chopper"; chopper.displayName = "Auto-Chopper"; chopper.kind = MachineKind.Producer; chopper.baseRate = 0.8; chopper.output = wood; chopper.input = null;
            chopper.description = "Cuts logs on its own and adds the wood to your stock.";
            chopper.tierModels = new[] { "AutoChopper", "AutoChopper_Mk2", "AutoChopper_Mk3" }; chopper.order = 0;
            var sawmill = Asset<MachineDef>($"{Data}/Machines/Sawmill.asset");
            sawmill.id = "sawmill"; sawmill.displayName = "Sawmill"; sawmill.kind = MachineKind.Processor; sawmill.baseRate = 2.5; sawmill.input = wood; sawmill.inputPerOutput = 2; sawmill.output = planks;
            sawmill.description = "Turns wood into planks, which sell for more.";
            sawmill.tierModels = new[] { "Sawmill" }; sawmill.order = 1;
            var workshop = Asset<MachineDef>($"{Data}/Machines/Workshop.asset");
            workshop.id = "workshop"; workshop.displayName = "Carpentry Workshop"; workshop.kind = MachineKind.Processor; workshop.baseRate = 1.5; workshop.input = planks; workshop.inputPerOutput = 3; workshop.output = furniture;
            workshop.description = "Turns planks into chairs and tables, which sell for more than the planks they use.";
            workshop.tierModels = new[] { "Workshop" }; workshop.order = 2;
            var builder = Asset<MachineDef>($"{Data}/Machines/CabinBuilder.asset");
            builder.id = "builder"; builder.displayName = "Cabin Builder"; builder.kind = MachineKind.Processor; builder.baseRate = 0.6; builder.input = furniture; builder.inputPerOutput = 5; builder.output = cabins;
            builder.description = "Builds furnished flat-pack log cabins: the most valuable thing you can sell.";
            builder.tierModels = new[] { "CabinBuilder" }; builder.order = 3;

            // ---- areas
            var clearing = Area("clearing", "Pinewood Clearing", "Where it all begins.", true, 0, new[] { pine, birch, oak }, new[] { 0.5f, 0.32f, 0.18f });
            var grove = Area("grove", "Old Grove", "Ancient maples and towering redwoods.", false, 1, new[] { maple, redwood, oak }, new[] { 0.45f, 0.3f, 0.25f });
            var ridge = Area("ridge", "Ironwood Ridge", "Iron-hard trees and the legendary Goldbark.", false, 2, new[] { iron, redwood, gold }, new[] { 0.72f, 0.18f, 0.1f });
            var peaks = Area("peaks", "Frostpine Peaks", "Snowy slopes of frost-hardened pines.", false, 3, new[] { frost, iron, redwood }, new[] { 0.62f, 0.23f, 0.15f });
            var vale = Area("vale", "Blossom Vale", "A valley of cherry blossom and smouldering emberwood.", false, 4, new[] { cherry, ember, frost }, new[] { 0.52f, 0.3f, 0.18f });
            var wilds = Area("wilds", "Crystal Wilds", "Glowing crystalwood, the rarest timber of all.", false, 5, new[] { ember, cherry, crystal }, new[] { 0.55f, 0.33f, 0.12f });

            // ---- upgrades
            var ups = new List<UpgradeDef>
            {
                Up("axe_tier", "Better Axe", "Forge a stronger axe. Each one hits harder and can cut tougher trees.", UpgradeCategory.Axe, UpgradeEffect.AxeTier, null, 7, 0, 1,
                    new double[] { 50, 600, 7500, 120000, 2500000, 60000000, 1500000000 }, 1, 0,
                    new[] { "Steel Axe", "Lumberjack's Axe", "Tempered Broadaxe", "Ironbreaker", "Frostbite Axe", "Emberforge Axe", "Starsteel Axe" }),
                Up("axe_sharpen", "Whetstone", "Keep the edge keen: more damage with every swing.", UpgradeCategory.Axe, UpgradeEffect.AxeDamage, null, 15, 25, 1.55, null, 0.2f, 1,
                    null, Req("axe_tier", 1)),
                Up("axe_speed", "Grip Training", "Faster, steadier swings.", UpgradeCategory.Axe, UpgradeEffect.AxeSpeed, null, 10, 60, 1.7, null, 0.08f, 2,
                    null, Req("axe_sharpen", 1)),
                Up("backpack", "Backpack", "Carry more wood on each trip to the market. Machine output goes to the yard, never your pack.", UpgradeCategory.Axe, UpgradeEffect.BackpackCapacity, null, 9, 0, 6,
                    new double[] { 50, 350, 2200, 15000, 100000, 700000, 5000000, 40000000, 300000000 }, 1, 3,
                    new[] { "Canvas Satchel", "Leather Rucksack", "Frame Pack", "Logger's Pack", "Expedition Pack", "Hauler's Harness", "Glacier Pack", "Ember Hauler", "Starlight Satchel" }, Req("axe_tier", 1)),

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

                // ---- Furniture and cabins: the next steps of the production chain.
                Up("workshop_count", "Carpentry Workshop", "Turns 3 planks into a set of furniture. Furniture sells for more than the planks it uses.", UpgradeCategory.Automation, UpgradeEffect.MachineCount, "workshop", 3, 0, 5,
                    new double[] { 120000, 600000, 3000000 }, 1, 15, null, Req("sawmill_count", 1)),
                Up("workshop_speed", "Workshop Tools", "Better chisels, planes and lathes for every workshop.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "workshop", 10, 180000, 2.0, null, 0.3f, 16,
                    null, Req("workshop_count", 1)),
                Up("joinery", "Fine Joinery", "Tighter joints, less waste: more furniture from the same planks.", UpgradeCategory.Automation, UpgradeEffect.ProcessorYield, "workshop", 5, 400000, 2.1, null, 0.1f, 17,
                    null, Req("workshop_speed", 2)),
                Up("builder_count", "Cabin Builder", "Builds 5 sets of furniture into a log cabin kit, the most valuable thing you can sell.", UpgradeCategory.Automation, UpgradeEffect.MachineCount, "builder", 3, 0, 5,
                    new double[] { 1500000, 8000000, 40000000 }, 1, 18, null, Req("workshop_count", 1)),
                Up("builder_speed", "Power Cranes", "Bigger cranes put cabins up faster.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "builder", 10, 2500000, 2.0, null, 0.3f, 19,
                    null, Req("builder_count", 1)),
                Up("master_builders", "Master Builders", "A seasoned crew gets more cabins out of the same furniture.", UpgradeCategory.Automation, UpgradeEffect.ProcessorYield, "builder", 5, 6000000, 2.1, null, 0.1f, 20,
                    null, Req("builder_speed", 2)),
                Up("furniture_price", "Furniture Showroom", "A showroom in town: furniture sells for more.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "furniture", 10, 250000, 1.9, null, 0.25f, 13,
                    null, Req("workshop_count", 1)),
                Up("cabin_price", "Cabin Realtors", "Realtors sell your cabins as holiday homes, for a lot more.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "cabins", 10, 3000000, 1.9, null, 0.25f, 14,
                    null, Req("builder_count", 1)),

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

                // ---- Legacy tree: perks bought with skill points (3 per rebirth), kept through rebirths.
                // Costs are in skill points (rounded up). Four branches; the first ten perks are the
                // ones from before the tree, so they stay free of prerequisites.
                // Heritage: head starts and the rebirth itself.
                Legacy(Up("heirloom_axe", "Heirloom Axe", "Start every run with a better axe already forged.", UpgradeCategory.Heritage, UpgradeEffect.StartingAxe, null, 6, 0, 1,
                    new double[] { 2, 4, 8, 16, 30, 50 }, 1, 0)),
                Legacy(Up("head_start", "Head Start", "Start every run with money in your pocket.", UpgradeCategory.Heritage, UpgradeEffect.StartingMoney, null, 6, 0, 1,
                    new double[] { 1, 2, 4, 8, 15, 25 }, 500, 1)),
                Legacy(Up("starter_kit", "Starter Kit", "Start every run with Auto-Choppers already running.", UpgradeCategory.Heritage, UpgradeEffect.StartingMachines, "chopper", 6, 0, 1,
                    new double[] { 2, 3, 5, 8, 12, 18 }, 1, 2)),
                Legacy(Up("forest_charter", "Forest Charter", "Start every run with the forest gates already open.", UpgradeCategory.Heritage, UpgradeEffect.StartingAreas, null, 5, 0, 1,
                    new double[] { 4, 10, 20, 35, 55 }, 1, 3)),
                Legacy(Up("heirloom_pack", "Heirloom Pack", "Start every run with a bigger backpack already on your back.", UpgradeCategory.Heritage, UpgradeEffect.StartingBackpack, null, 6, 0, 1,
                    new double[] { 3, 6, 10, 16, 24, 35 }, 1, 10, null, Req("heirloom_axe", 1))),
                Legacy(Up("sawmill_deed", "Sawmill Deed", "Start every run with Sawmills already turning.", UpgradeCategory.Heritage, UpgradeEffect.StartingMachines, "sawmill", 3, 0, 1,
                    new double[] { 6, 12, 20 }, 1, 11, null, Req("starter_kit", 2))),
                Legacy(Up("workshop_deed", "Workshop Deed", "Start every run with Carpentry Workshops already built.", UpgradeCategory.Heritage, UpgradeEffect.StartingMachines, "workshop", 3, 0, 1,
                    new double[] { 15, 25, 40 }, 1, 12, null, Req("sawmill_deed", 1))),
                Legacy(Up("builder_charter", "Builder's Charter", "Start every run with Cabin Builders already on site.", UpgradeCategory.Heritage, UpgradeEffect.StartingMachines, "builder", 3, 0, 1,
                    new double[] { 30, 50, 80 }, 1, 13, null, Req("workshop_deed", 1))),
                Legacy(Up("scholar", "Scholar", "Learn from every life: each rebirth gives an extra skill point.", UpgradeCategory.Heritage, UpgradeEffect.ExtraSkillPoints, null, 5, 0, 1,
                    new double[] { 10, 25, 50, 90, 150 }, 1, 14, null, Req("head_start", 3))),
                Legacy(Up("ancestral_wisdom", "Ancestral Wisdom", "Every rebirth you have ever done counts for more.", UpgradeCategory.Heritage, UpgradeEffect.RebirthBonus, null, 5, 8, 1.6, null, 0.02f, 15,
                    null, Req("scholar", 1))),
                Legacy(Up("phoenix_feather", "Phoenix Feather", "Rise again for less: rebirths cost less.", UpgradeCategory.Heritage, UpgradeEffect.RebirthDiscount, null, 10, 3, 1.4, null, 0.05f, 16,
                    null, Req("forest_charter", 1))),

                // Might: the axe and the lumberjack.
                Legacy(Up("titan_grip", "Titan Grip", "A grip that never tires: far more damage with every swing.", UpgradeCategory.Might, UpgradeEffect.AxeDamage, null, 10, 1, 1.3, null, 0.5f, 4)),
                Legacy(Up("quick_hands", "Quick Hands", "Swing faster, forever.", UpgradeCategory.Might, UpgradeEffect.AxeSpeed, null, 5, 1, 1.5, null, 0.1f, 5)),
                Legacy(Up("deep_pockets", "Deep Pockets", "Every backpack holds much more.", UpgradeCategory.Might, UpgradeEffect.BackpackBonus, null, 5, 1, 1.5, null, 0.5f, 6)),
                Legacy(Up("lodestone", "Lodestone", "A permanently stronger log magnet.", UpgradeCategory.Might, UpgradeEffect.MagnetRange, null, 5, 1, 1.4, null, 3f, 7)),
                Legacy(Up("keen_eye", "Keen Eye", "You always find the weak spot: more double-damage hits.", UpgradeCategory.Might, UpgradeEffect.CritChance, null, 10, 2, 1.35, null, 0.02f, 20,
                    null, Req("titan_grip", 2))),
                Legacy(Up("crushing_blows", "Crushing Blows", "Double-damage hits land harder still.", UpgradeCategory.Might, UpgradeEffect.CritDamage, null, 5, 5, 1.5, null, 0.5f, 21,
                    null, Req("keen_eye", 3))),
                Legacy(Up("legends_swing", "Legend's Swing", "The stories are true: sometimes one swing fells the tree.", UpgradeCategory.Might, UpgradeEffect.FellChance, null, 5, 10, 1.6, null, 0.01f, 22,
                    null, Req("crushing_blows", 2))),
                Legacy(Up("giants_strength", "Giant's Strength", "The strength of a giant in every swing.", UpgradeCategory.Might, UpgradeEffect.AxeDamage, null, 10, 15, 1.4, null, 1f, 23,
                    null, Req("legends_swing", 1))),
                Legacy(Up("woodsmans_instinct", "Woodsman's Instinct", "You know where to cut: more wood from every tree.", UpgradeCategory.Might, UpgradeEffect.WoodYield, null, 10, 2, 1.35, null, 0.2f, 24,
                    null, Req("quick_hands", 2))),
                Legacy(Up("fleet_foot", "Fleet Foot", "Light on your feet: move faster, forever.", UpgradeCategory.Might, UpgradeEffect.MoveSpeed, null, 5, 3, 1.5, null, 0.05f, 25,
                    null, Req("woodsmans_instinct", 1))),

                // Industry: machines, offline time and selling.
                Legacy(Up("master_engineer", "Master Engineer", "Every machine you build runs harder.", UpgradeCategory.Industry, UpgradeEffect.ProductionBonus, null, 10, 1, 1.3, null, 0.25f, 8)),
                Legacy(Up("ancestral_engines", "Ancestral Engines", "Engines passed down the line: Auto-Choppers run much faster.", UpgradeCategory.Industry, UpgradeEffect.MachineSpeed, "chopper", 10, 3, 1.35, null, 0.5f, 30,
                    null, Req("master_engineer", 2))),
                Legacy(Up("family_sawmill", "Family Sawmill", "Generations of sawyers: Sawmills run much faster.", UpgradeCategory.Industry, UpgradeEffect.MachineSpeed, "sawmill", 10, 4, 1.35, null, 0.5f, 31,
                    null, Req("master_engineer", 2))),
                Legacy(Up("guild_carpenters", "Guild Carpenters", "The carpenters' guild works for you: Workshops run much faster.", UpgradeCategory.Industry, UpgradeEffect.MachineSpeed, "workshop", 10, 6, 1.35, null, 0.5f, 32,
                    null, Req("family_sawmill", 2))),
                Legacy(Up("master_architects", "Master Architects", "Famous architects: Cabin Builders run much faster.", UpgradeCategory.Industry, UpgradeEffect.MachineSpeed, "builder", 10, 10, 1.35, null, 0.5f, 33,
                    null, Req("guild_carpenters", 2))),
                Legacy(Up("perfect_joinery", "Perfect Joinery", "Not a splinter wasted: more furniture from the same planks.", UpgradeCategory.Industry, UpgradeEffect.ProcessorYield, "workshop", 5, 8, 1.5, null, 0.15f, 34,
                    null, Req("guild_carpenters", 3))),
                Legacy(Up("blueprints", "Blueprints", "Plans kept from every run make machines cheaper to build.", UpgradeCategory.Industry, UpgradeEffect.MachineDiscount, null, 5, 4, 1.5, null, 0.05f, 35,
                    null, Req("master_engineer", 3))),
                Legacy(Up("night_watch", "Night Watch", "A watchman keeps the machines running far longer while you're away.", UpgradeCategory.Industry, UpgradeEffect.OfflineHours, null, 5, 4, 1.5, null, 2f, 36,
                    null, Req("blueprints", 1))),
                Legacy(Up("market_courier", "Market Courier", "A courier takes the yard stock to market every 30 seconds, every run.", UpgradeCategory.Industry, UpgradeEffect.AutoSell, null, 1, 0, 1,
                    new double[] { 12 }, 30f, 37, null, Req("night_watch", 1))),
                Legacy(Up("express_courier", "Express Courier", "A faster courier: the yard stock sells every 10 seconds.", UpgradeCategory.Industry, UpgradeEffect.AutoSell, null, 1, 0, 1,
                    new double[] { 40 }, 10f, 38, null, Req("market_courier", 1))),

                // Fortune: prices, luck and the forests.
                Legacy(Up("silver_tongue", "Silver Tongue", "Buyers trust you: everything sells for more.", UpgradeCategory.Fortune, UpgradeEffect.SellBonus, null, 10, 1, 1.3, null, 0.15f, 9)),
                Legacy(Up("timber_baron", "Timber Baron", "Your name sells wood: it's worth far more.", UpgradeCategory.Fortune, UpgradeEffect.ResourcePrice, "wood", 10, 2, 1.35, null, 0.5f, 40,
                    null, Req("silver_tongue", 2))),
                Legacy(Up("plank_empire", "Plank Empire", "Planks with your stamp sell for far more.", UpgradeCategory.Fortune, UpgradeEffect.ResourcePrice, "planks", 10, 4, 1.35, null, 0.5f, 41,
                    null, Req("timber_baron", 2))),
                Legacy(Up("furniture_dynasty", "Furniture Dynasty", "A household name in furniture: it sells for far more.", UpgradeCategory.Fortune, UpgradeEffect.ResourcePrice, "furniture", 10, 7, 1.35, null, 0.5f, 42,
                    null, Req("plank_empire", 2))),
                Legacy(Up("cabin_tycoon", "Cabin Tycoon", "Everyone wants one of your cabins: they sell for far more.", UpgradeCategory.Fortune, UpgradeEffect.ResourcePrice, "cabins", 10, 12, 1.35, null, 0.5f, 43,
                    null, Req("furniture_dynasty", 2))),
                Legacy(Up("fortunes_child", "Fortune's Child", "Luck follows you: more lucky finds.", UpgradeCategory.Fortune, UpgradeEffect.LuckyFind, null, 5, 4, 1.5, null, 0.02f, 44,
                    null, Req("silver_tongue", 3))),
                Legacy(Up("golden_touch", "Golden Touch", "Goldbark and Crystalwood drop much more cash.", UpgradeCategory.Fortune, UpgradeEffect.TreeCashBonus, null, 5, 6, 1.5, null, 0.5f, 45,
                    null, Req("fortunes_child", 2))),
                Legacy(Up("old_connections", "Old Connections", "Friends in every town make upgrades cheaper.", UpgradeCategory.Fortune, UpgradeEffect.UpgradeDiscount, null, 5, 5, 1.5, null, 0.02f, 46,
                    null, Req("silver_tongue", 5))),
                Legacy(Up("druids_blessing", "Druid's Blessing", "The forest favours you: felled trees grow back faster.", UpgradeCategory.Fortune, UpgradeEffect.TreeRegrowth, null, 5, 4, 1.5, null, 0.2f, 47,
                    null, Req("old_connections", 1))),
                Up("night_shift", "Night Shift", "Machines keep working longer while the game is closed.", UpgradeCategory.Economy, UpgradeEffect.OfflineHours, null, 5, 2000, 3, null, 2f, 3,
                    null, Req("wood_price", 1), Req("chopper_count", 1)),

                Up("area_grove", "Old Grove", "Open the gate to the Old Grove: maples and towering redwoods.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "grove", 1, 0, 1, new double[] { 25000 }, 1, 0,
                    null, Req("axe_tier", 2)),
                Up("area_ridge", "Ironwood Ridge", "Open the gate to Ironwood Ridge: iron-hard trees and the legendary Goldbark.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "ridge", 1, 0, 1, new double[] { 350000 }, 1, 1,
                    null, Req("area_grove", 1), Req("axe_tier", 3)),
                Up("area_peaks", "Frostpine Peaks", "Open the gate to Frostpine Peaks: snowy slopes of frost-hardened pines.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "peaks", 1, 0, 1, new double[] { 8000000 }, 1, 11,
                    null, Req("area_ridge", 1), Req("axe_tier", 4)),
                Up("area_vale", "Blossom Vale", "Open the gate to Blossom Vale: cherry blossom and smouldering emberwood.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "vale", 1, 0, 1, new double[] { 150000000 }, 1, 12,
                    null, Req("area_peaks", 1), Req("axe_tier", 5)),
                Up("area_wilds", "Crystal Wilds", "Open the gate to the Crystal Wilds: glowing crystalwood, the rarest timber of all.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "wilds", 1, 0, 1, new double[] { 4000000000 }, 1, 13,
                    null, Req("area_vale", 1), Req("axe_tier", 6)),
                Up("frost_tempering", "Frost Tempering", "Quench your blade in glacier water: more damage with every swing.", UpgradeCategory.Forest, UpgradeEffect.AxeDamage, null, 10, 12000000, 1.8, null, 0.4f, 14,
                    null, Req("area_peaks", 1)),
                Up("snow_chains", "Snow Chains", "Grip on ice: move faster everywhere.", UpgradeCategory.Forest, UpgradeEffect.MoveSpeed, null, 3, 20000000, 2.5, null, 0.05f, 15,
                    null, Req("frost_tempering", 1)),
                Up("blossom_festival", "Blossom Festival", "Visitors pour in for the blossom: every sale pays more.", UpgradeCategory.Forest, UpgradeEffect.SellBonus, null, 10, 200000000, 1.9, null, 0.06f, 16,
                    null, Req("area_vale", 1)),
                Up("ember_kilns", "Ember Kilns", "Emberwood burns hot for days: every machine works harder.", UpgradeCategory.Forest, UpgradeEffect.ProductionBonus, null, 10, 300000000, 1.9, null, 0.12f, 17,
                    null, Req("blossom_festival", 1)),
                Up("crystal_resonance", "Crystal Resonance", "Crystalwood hums with value: rich trees drop much more cash.", UpgradeCategory.Forest, UpgradeEffect.TreeCashBonus, null, 5, 5000000000, 2.0, null, 0.5f, 18,
                    null, Req("area_wilds", 1)),
                Up("starlight_harvest", "Starlight Harvest", "Cut by starlight: far more wood from every tree.", UpgradeCategory.Forest, UpgradeEffect.WoodYield, null, 5, 8000000000, 2.1, null, 0.25f, 19,
                    null, Req("crystal_resonance", 1)),

                Up("master_smith", "Master Smith", "A master smith reworks your axe: much more damage.", UpgradeCategory.Axe, UpgradeEffect.AxeDamage, null, 10, 200000, 1.9, null, 0.5f, 15,
                    null, Req("cleave", 2)),
                Up("timberfall", "Timberfall", "More often still, one swing brings the whole tree down.", UpgradeCategory.Axe, UpgradeEffect.FellChance, null, 5, 1500000, 2.4, null, 0.01f, 16,
                    null, Req("master_smith", 1)),
                Up("devastating_blows", "Devastating Blows", "Perfect swings hit harder than ever.", UpgradeCategory.Axe, UpgradeEffect.CritDamage, null, 5, 600000, 2.2, null, 0.75f, 17,
                    null, Req("power_swing", 3)),
                Up("second_wind", "Second Wind", "Never tire: swing faster.", UpgradeCategory.Axe, UpgradeEffect.AxeSpeed, null, 6, 300000, 2.0, null, 0.05f, 18,
                    null, Req("long_strides", 1)),
                Up("sherpa_frame", "Sherpa Frame", "A sturdier frame: every backpack holds more.", UpgradeCategory.Axe, UpgradeEffect.BackpackBonus, null, 5, 100000, 2.2, null, 0.3f, 19,
                    null, Req("timber_sense", 1)),

                Up("hydraulic_rams", "Hydraulic Rams", "Hydraulics drive every Auto-Chopper harder.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "chopper", 10, 3000000, 1.9, null, 0.4f, 21,
                    null, Req("turbo_choppers", 3)),
                Up("graveyard_shift", "Graveyard Shift", "A crew that works all night: machines keep going even longer while you're away.", UpgradeCategory.Automation, UpgradeEffect.OfflineHours, null, 5, 5000000, 2.3, null, 2f, 22,
                    null, Req("hydraulic_rams", 1)),
                Up("conveyor_belts", "Conveyor Belts", "Belts link the yard together: every machine works harder.", UpgradeCategory.Automation, UpgradeEffect.ProductionBonus, null, 10, 2000000, 1.9, null, 0.12f, 23,
                    null, Req("overtime_crew", 1)),
                Up("band_saws", "Band Saws", "Endless band saws for every sawmill.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "sawmill", 10, 4000000, 1.9, null, 0.4f, 24,
                    null, Req("overdrive_saws", 3)),
                Up("power_lathes", "Power Lathes", "Powered lathes for every workshop.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "workshop", 10, 8000000, 1.9, null, 0.4f, 25,
                    null, Req("joinery", 2)),
                Up("prefab_panels", "Prefab Panels", "Pre-built wall panels: cabins go up faster.", UpgradeCategory.Automation, UpgradeEffect.MachineSpeed, "builder", 10, 60000000, 1.9, null, 0.4f, 26,
                    null, Req("master_builders", 2)),

                Up("designer_furniture", "Designer Furniture", "Famous designers sign your furniture: it sells for more.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "furniture", 10, 2000000, 1.9, null, 0.3f, 15,
                    null, Req("furniture_price", 5)),
                Up("luxury_cabins", "Luxury Cabins", "Hot tubs and log fires: cabins sell for more.", UpgradeCategory.Economy, UpgradeEffect.ResourcePrice, "cabins", 10, 30000000, 1.9, null, 0.3f, 16,
                    null, Req("cabin_price", 5)),
                Up("global_trade", "Global Trade", "Ship to every continent: every sale pays more.", UpgradeCategory.Economy, UpgradeEffect.SellBonus, null, 10, 500000, 1.9, null, 0.08f, 17,
                    null, Req("bulk_buyer", 1)),
                Up("treasure_hunter", "Treasure Hunter", "You can smell a hidden stash: more lucky finds.", UpgradeCategory.Economy, UpgradeEffect.LuckyFind, null, 5, 1000000, 2.2, null, 0.02f, 18,
                    null, Req("global_trade", 1)),
            };

            // ---- skill tree layout (x = -11..9 left to right, y = 0..9 from the trunk up) and icons.
            // Branches read left to right: Forests, Axe, Automation (choppers, sawmills, workshops,
            // cabin builders), Economy; each grows upwards
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
                // The far forests climb a new column on the far left, each with two perks beside it.
                ["area_peaks"] = (-8.8f, 5.25f, "mountain"),
                ["area_vale"] = (-8.8f, 6.55f, "leaf"),
                ["area_wilds"] = (-8.8f, 7.85f, "gem"),
                ["frost_tempering"] = (-9.9f, 6.55f, "axe"),
                ["snow_chains"] = (-11.0f, 6.55f, "boot"),
                ["blossom_festival"] = (-9.9f, 7.85f, "tag"),
                ["ember_kilns"] = (-11.0f, 7.85f, "bolt"),
                ["crystal_resonance"] = (-9.9f, 9.15f, "gem"),
                ["starlight_harvest"] = (-11.0f, 9.15f, "star"),

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
                ["master_smith"] = (-4.4f, 7.85f, "wrench"),
                ["timberfall"] = (-4.4f, 9.15f, "axe"),
                ["devastating_blows"] = (-3.3f, 9.15f, "bolt"),
                ["second_wind"] = (-2.2f, 7.85f, "run"),
                ["sherpa_frame"] = (-1.1f, 6.55f, "backpack"),

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
                ["workshop_count"] = (3.3f, 3.95f, "chair"),
                ["workshop_speed"] = (3.3f, 5.25f, "wrench"),
                ["joinery"] = (3.3f, 6.55f, "gem"),
                ["builder_count"] = (4.4f, 5.25f, "house"),
                ["builder_speed"] = (4.4f, 6.55f, "gear"),
                ["master_builders"] = (4.4f, 7.85f, "crown"),
                ["hydraulic_rams"] = (0.0f, 7.85f, "gear"),
                ["graveyard_shift"] = (0.0f, 9.15f, "moon"),
                ["conveyor_belts"] = (1.1f, 9.15f, "chart"),
                ["band_saws"] = (2.2f, 9.15f, "saw"),
                ["power_lathes"] = (3.3f, 7.85f, "gear"),
                ["prefab_panels"] = (4.4f, 9.15f, "house"),

                // Economy: prices, pickup, offline time, sales and luck.
                ["wood_price"] = (6.9f, 1.35f, "coin"),
                ["haggling"] = (8.0f, 1.35f, "tag"),
                ["export_deals"] = (9.1f, 1.35f, "cart"),
                ["wood_brokers"] = (5.8f, 2.65f, "coin"),
                ["plank_price"] = (5.8f, 3.95f, "star"),
                ["premium_planks"] = (5.8f, 5.25f, "plank"),
                ["magnet"] = (6.9f, 2.65f, "magnet"),
                ["strong_magnet"] = (6.9f, 3.95f, "magnet"),
                ["night_shift"] = (8.0f, 2.65f, "moon"),
                ["long_nights"] = (8.0f, 3.95f, "moon"),
                ["lucky_finds"] = (9.1f, 2.65f, "clover"),
                ["fortunes_favor"] = (9.1f, 3.95f, "clover"),
                ["bulk_buyer"] = (9.1f, 5.25f, "tag"),
                ["furniture_price"] = (5.8f, 6.55f, "chair"),
                ["cabin_price"] = (5.8f, 7.85f, "house"),
                ["designer_furniture"] = (6.9f, 6.55f, "chair"),
                ["luxury_cabins"] = (6.9f, 7.85f, "crown"),
                ["global_trade"] = (9.1f, 6.55f, "cart"),
                ["treasure_hunter"] = (9.1f, 7.85f, "nugget"),

                // ---- Legacy tree (its own screen): 14 columns, x = -7.15..7.15, from the rebirth emblem.
                // Heritage
                ["heirloom_axe"] = (-7.15f, 1.35f, "axe"),
                ["heirloom_pack"] = (-7.15f, 2.65f, "backpack"),
                ["starter_kit"] = (-6.05f, 1.35f, "saw"),
                ["sawmill_deed"] = (-6.05f, 2.65f, "plank"),
                ["workshop_deed"] = (-6.05f, 3.95f, "chair"),
                ["builder_charter"] = (-6.05f, 5.25f, "house"),
                ["head_start"] = (-4.95f, 1.35f, "coin"),
                ["scholar"] = (-4.95f, 2.65f, "star"),
                ["ancestral_wisdom"] = (-4.95f, 3.95f, "crown"),
                ["forest_charter"] = (-3.85f, 1.35f, "pine"),
                ["phoenix_feather"] = (-3.85f, 2.65f, "rebirth"),
                // Might
                ["titan_grip"] = (-2.75f, 1.35f, "gem"),
                ["keen_eye"] = (-2.75f, 2.65f, "target"),
                ["crushing_blows"] = (-2.75f, 3.95f, "bolt"),
                ["legends_swing"] = (-2.75f, 5.25f, "axe"),
                ["giants_strength"] = (-2.75f, 6.55f, "mountain"),
                ["quick_hands"] = (-1.65f, 1.35f, "bolt"),
                ["woodsmans_instinct"] = (-1.65f, 2.65f, "log"),
                ["fleet_foot"] = (-1.65f, 3.95f, "boot"),
                ["deep_pockets"] = (-0.55f, 1.35f, "backpack"),
                ["lodestone"] = (0.55f, 1.35f, "magnet"),
                // Industry
                ["master_engineer"] = (2.75f, 1.35f, "gear"),
                ["ancestral_engines"] = (1.65f, 2.65f, "saw"),
                ["family_sawmill"] = (2.75f, 2.65f, "plank"),
                ["guild_carpenters"] = (2.75f, 3.95f, "chair"),
                ["master_architects"] = (2.75f, 5.25f, "house"),
                ["perfect_joinery"] = (1.65f, 3.95f, "gem"),
                ["blueprints"] = (3.85f, 2.65f, "wrench"),
                ["night_watch"] = (3.85f, 3.95f, "moon"),
                ["market_courier"] = (3.85f, 5.25f, "cart"),
                ["express_courier"] = (3.85f, 6.55f, "run"),
                // Fortune
                ["silver_tongue"] = (6.05f, 1.35f, "tag"),
                ["timber_baron"] = (4.95f, 2.65f, "coin"),
                ["plank_empire"] = (4.95f, 3.95f, "plank"),
                ["furniture_dynasty"] = (4.95f, 5.25f, "chair"),
                ["cabin_tycoon"] = (4.95f, 6.55f, "house"),
                ["fortunes_child"] = (6.05f, 2.65f, "clover"),
                ["golden_touch"] = (6.05f, 3.95f, "nugget"),
                ["old_connections"] = (7.15f, 2.65f, "chart"),
                ["druids_blessing"] = (7.15f, 3.95f, "sprout"),
            };
            foreach (var u in ups)
                if (layout.TryGetValue(u.id, out var l)) { u.treePosition = new Vector2(l.x, l.y); u.icon = l.icon; }

            // ---- config and database
            // Everything in the skill tree costs half of the prices above (Legacy perks are skill points).
            foreach (var u in ups)
            {
                if (u.legacy) continue;
                u.baseCost *= SkillTreePriceScale;
                if (u.explicitCosts != null) u.explicitCosts = System.Array.ConvertAll(u.explicitCosts, c => c * SkillTreePriceScale);
            }

            var cfg = Asset<GameConfig>($"{Data}/GameConfig.asset");
            // Wood carried by hand per backpack tier (machine output goes to the yard instead).
            cfg.backpackCapacity = new double[] { 50, 150, 400, 1000, 2500, 6000, 15000, 40000, 100000, 250000 };
            // Axe tiers 5-7 cut the far forests' trees.
            cfg.axeTierDamage = new[] { 1f, 2.5f, 6f, 16f, 45f, 130f, 380f, 1100f };
            cfg.axeTierSpeed = new[] { 1f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f };
            cfg.axeModels = new[] { "Axe_0", "Axe_1", "Axe_2", "Axe_3", "Axe_4", "Axe_5", "Axe_6", "Axe_7" };
            cfg.baseBackpackName = "Burlap Sack";
            // Rebirths: first one $5,000, each next one x1.03; every rebirth adds +15% to all earnings
            // and gives 3 skill points.
            cfg.rebirthBaseCost = 5000;
            cfg.rebirthCostGrowth = 1.03;
            cfg.rebirthBonus = 0.15;
            cfg.rebirthSkillPoints = 3;
            var db = Asset<GameDatabase>(DatabasePath);
            db.config = cfg;
            db.resources = new[] { wood, planks, furniture, cabins };
            db.trees = new[] { pine, birch, oak, maple, redwood, iron, gold, frost, cherry, ember, crystal };
            db.machines = new[] { chopper, sawmill, workshop, builder };
            db.areas = new[] { clearing, grove, ridge, peaks, vale, wilds };
            db.upgrades = ups.ToArray();
            db.bodyFont = ProjectSetup.Font("Inter-Regular");
            db.boldFont = ProjectSetup.Font("Inter-SemiBold");
            db.audio = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioSynth.LibraryPath);
            AssetDatabase.SaveAssets();
            return $"data: {db.trees.Length} trees, {db.upgrades.Length} upgrades, {db.machines.Length} machines, {db.areas.Length} areas, audio {(db.audio != null ? "ok" : "MISSING")}";
        }

        /// <summary>Multiplier on every skill tree price written above.</summary>
        public const double SkillTreePriceScale = 0.5;

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
