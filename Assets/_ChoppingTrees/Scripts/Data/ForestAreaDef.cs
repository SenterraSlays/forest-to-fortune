using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>A forest area. Locked areas are opened by a <see cref="UpgradeEffect.ForestArea"/> upgrade.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Forest Area", fileName = "Area")]
    public class ForestAreaDef : ScriptableObject
    {
        public string id = "clearing";
        public string displayName = "Pinewood Clearing";
        [TextArea] public string description;
        public bool unlockedByDefault = true;
        public TreeType[] trees;
        [Tooltip("Relative spawn weights, one per tree type.")]
        public float[] weights;
        public int sortOrder;
    }
}
