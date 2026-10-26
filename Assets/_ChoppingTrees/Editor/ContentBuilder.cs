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
                Up("courier", "Wood Courier", "A courier collects and sells all your stock every 15 seconds, wherever you are.", UpgradeCategory.Economy, UpgradeEffect.AutoSell, null, 1, 8000, 1, null, 15f, 2,
                    null, Req("wood_price", 1), Req("chopper_count", 2)),
                Up("night_shift", "Night Shift", "Machines keep working longer while the game is closed.", UpgradeCategory.Economy, UpgradeEffect.OfflineHours, null, 5, 2000, 3, null, 2f, 3,
                    null, Req("wood_price", 1), Req("chopper_count", 1)),

                Up("area_grove", "Old Grove", "Open the gate to the Old Grove: maples and towering redwoods.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "grove", 1, 0, 1, new double[] { 25000 }, 1, 0,
                    null, Req("axe_tier", 2)),
                Up("area_ridge", "Ironwood Ridge", "Open the gate to Ironwood Ridge: iron-hard trees and the legendary Goldbark.", UpgradeCategory.Forest, UpgradeEffect.ForestArea, "ridge", 1, 0, 1, new double[] { 350000 }, 1, 1,
                    null, Req("area_grove", 1), Req("axe_tier", 3)),
            };

            // ---- skill tree layout (x = -4..4 left to right, y = 0..5 from the trunk up) and icons
            var layout = new Dictionary<string, (float x, float y, string icon)>
            {
                ["axe_tier"] = (-2.3f, 1.0f, "axe"),
                ["axe_sharpen"] = (-3.5f, 1.95f, "gem"),
                ["axe_speed"] = (-3.7f, 3.2f, "bolt"),
                ["area_grove"] = (-2.1f, 3.0f, "pine"),
                ["area_ridge"] = (-2.0f, 4.55f, "mountain"),
                ["chopper_count"] = (0.15f, 1.3f, "saw"),
                ["chopper_speed"] = (-0.75f, 2.5f, "gear"),
                ["foreman"] = (0.2f, 2.75f, "chart"),
                ["chopper_tier"] = (-0.75f, 4.1f, "crown"),
                ["sawmill_count"] = (1.3f, 2.45f, "plank"),
                ["sawmill_speed"] = (0.95f, 3.85f, "bolt"),
                ["plank_price"] = (2.2f, 3.7f, "star"),
                ["wood_price"] = (2.45f, 1.0f, "coin"),
                ["night_shift"] = (3.65f, 2.0f, "moon"),
                ["courier"] = (3.45f, 3.2f, "cart"),
            };
            foreach (var u in ups)
                if (layout.TryGetValue(u.id, out var l)) { u.treePosition = new Vector2(l.x, l.y); u.icon = l.icon; }

            // ---- config and database
            var cfg = Asset<GameConfig>($"{Data}/GameConfig.asset");
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
            u.levelNames = levelNames; u.requires = requires;
            return u;
        }
    }
}
