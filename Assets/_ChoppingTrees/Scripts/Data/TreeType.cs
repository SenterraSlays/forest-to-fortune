using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>A choppable tree species: toughness, rewards and look.</summary>
    [CreateAssetMenu(menuName = "Forest to Fortune/Tree Type", fileName = "Tree")]
    public class TreeType : ScriptableObject
    {
        public string id = "pine";
        public string displayName = "Pine";
        [Tooltip("Model name in Art/Models (without .fbx).")]
        public string modelName = "Tree_Pine";
        public string stumpModel = "Stump_Small";
        public double maxHealth = 4;
        public double woodReward = 3;
        [Tooltip("Bonus money paid instantly when felled (rare trees).")]
        public double cashBonus;
        [Tooltip("Lowest axe tier that can damage this tree.")]
        public int requiredAxeTier;
        public float respawnSeconds = 25f;
        [Tooltip("Trunk collider radius and height at scale 1.")]
        public float trunkRadius = 0.3f;
        public float trunkHeight = 6f;
        [Tooltip("Height of the chop notch above the ground at scale 1.")]
        public float notchHeight = 0.9f;
        public Vector2 scaleRange = new Vector2(0.9f, 1.15f);
        [Tooltip("Log pickups spawned when felled (the reward is split between them).")]
        public int logPieces = 2;
        [Tooltip("Pitch multiplier for chop sounds: bigger trees sound deeper.")]
        public float soundPitch = 1f;
        public Color chipColor = new Color(0.86f, 0.7f, 0.48f);
        public Color leafColor = new Color(0.3f, 0.55f, 0.25f);
    }
}
