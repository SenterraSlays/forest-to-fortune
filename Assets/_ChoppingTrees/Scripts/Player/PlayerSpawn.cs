using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Marks where the player starts and respawns.</summary>
    public class PlayerSpawn : MonoBehaviour
    {
        static PlayerSpawn s_Instance;

        public static Vector3 Position => s_Instance != null ? s_Instance.transform.position : Vector3.up;
        public static float Yaw => s_Instance != null ? s_Instance.transform.eulerAngles.y : 0f;

        void Awake() => s_Instance = this;

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }
    }
}
