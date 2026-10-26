using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Gives <see cref="WoodPickup"/> its prefab and pool parent for this scene.</summary>
    public class PickupSpawner : MonoBehaviour
    {
        [SerializeField] GameObject m_Prefab;

        void Awake() => WoodPickup.Init(m_Prefab, transform);
    }
}
