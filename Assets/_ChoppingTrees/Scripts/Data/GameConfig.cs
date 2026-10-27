using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Tunable gameplay numbers that aren't tied to a single tree, upgrade or machine.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Movement")]
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.4f;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;
        public float acceleration = 14f;
        public float airControl = 0.35f;

        [Header("Chopping")]
        [Tooltip("Base damage per hit for each axe tier (index = tier).")]
        public float[] axeTierDamage = { 1f, 2.5f, 6f, 16f, 45f };
        [Tooltip("Swing speed multiplier for each axe tier.")]
        public float[] axeTierSpeed = { 1f, 1.05f, 1.1f, 1.15f, 1.2f };
        [Tooltip("Axe model per tier (index = tier).")]
        public string[] axeModels = { "Axe_0", "Axe_1", "Axe_2", "Axe_3", "Axe_4" };
        public string baseAxeName = "Rusty Hatchet";
        [Tooltip("Seconds per full swing at speed multiplier 1.")]
        public float swingSeconds = 0.55f;
        [Tooltip("Fastest allowed swing, whatever the upgrades.")]
        public float minSwingSeconds = 0.22f;
        public float reach = 2.7f;

        [Header("Pickups")]
        [Tooltip("Log magnet range before Log Magnet upgrades.")]
        public float pickupMagnetRadius = 4f;
        [Tooltip("A Lucky Find is worth this many times the felled tree's wood at the current price.")]
        public float luckyFindValue = 3f;
        public float pickupAutoCollectSeconds = 25f;
        public int maxActivePickups = 48;

        [Header("Backpack")]
        [Tooltip("Wood the player can carry for each backpack tier (index = tier). Machine output goes to the yard instead.")]
        public double[] backpackCapacity = { 50, 150, 400, 1000, 2500, 6000, 15000 };
        public string baseBackpackName = "Burlap Sack";

        [Header("Rebirth")]
        [Tooltip("Price of the first rebirth.")]
        public double rebirthBaseCost = 5000;
        [Tooltip("Each rebirth costs this many times the previous one.")]
        public double rebirthCostGrowth = 1.08;
        [Tooltip("Extra share of all money earned per rebirth (0.1 = +10% each).")]
        public double rebirthBonus = 0.1;

        [Header("Economy")]
        public double startingMoney;
        public float autosaveSeconds = 30f;
        public float baseOfflineHours = 2f;
        [Tooltip("Share of normal machine output earned while the game is closed.")]
        [Range(0f, 1f)] public float offlineEfficiency = 1f;
        [Tooltip("Offline time shorter than this isn't reported.")]
        public float minOfflineSecondsToReport = 60f;
    }
}
