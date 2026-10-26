using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>A sellable resource (wood, planks, ...). New resources only need a new asset.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Resource", fileName = "Resource")]
    public class ResourceDef : ScriptableObject
    {
        public string id = "wood";
        public string displayName = "Wood";
        [Tooltip("Money earned per unit before upgrades.")]
        public double basePrice = 1;
        public Color color = new Color(0.72f, 0.5f, 0.3f);
        [Tooltip("Hidden in the HUD until the player owns some or a machine produces it.")]
        public bool hiddenUntilProduced;
        public int sortOrder;
    }
}
