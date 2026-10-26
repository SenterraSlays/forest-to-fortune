using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Index of all game data. Lives in Resources so runtime and tests can load it by name.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "GameDatabase";

        public GameConfig config;
        public ResourceDef[] resources;
        public TreeType[] trees;
        public MachineDef[] machines;
        public ForestAreaDef[] areas;
        public UpgradeDef[] upgrades;

        [Header("Presentation")]
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset boldFont;
        public AudioLibrary audio;

        Dictionary<string, ResourceDef> m_Resources;
        Dictionary<string, UpgradeDef> m_Upgrades;
        Dictionary<string, MachineDef> m_Machines;

        public static GameDatabase Load() => Resources.Load<GameDatabase>(ResourcePath);

        void OnEnable()
        {
            m_Resources = null;
            m_Upgrades = null;
            m_Machines = null;
        }

        public ResourceDef Resource(string id)
        {
            if (m_Resources == null)
            {
                m_Resources = new Dictionary<string, ResourceDef>();
                foreach (var r in resources) if (r != null) m_Resources[r.id] = r;
            }
            return id != null && m_Resources.TryGetValue(id, out var d) ? d : null;
        }

        public UpgradeDef Upgrade(string id)
        {
            if (m_Upgrades == null)
            {
                m_Upgrades = new Dictionary<string, UpgradeDef>();
                foreach (var u in upgrades) if (u != null) m_Upgrades[u.id] = u;
            }
            return id != null && m_Upgrades.TryGetValue(id, out var d) ? d : null;
        }

        public MachineDef Machine(string id)
        {
            if (m_Machines == null)
            {
                m_Machines = new Dictionary<string, MachineDef>();
                foreach (var m in machines) if (m != null) m_Machines[m.id] = m;
            }
            return id != null && m_Machines.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>The upgrade that controls how many of a machine the player owns.</summary>
        public UpgradeDef CountUpgradeFor(string machineId)
        {
            foreach (var u in upgrades)
                if (u != null && u.effect == UpgradeEffect.MachineCount && u.targetId == machineId) return u;
            return null;
        }

        public UpgradeDef AreaUpgradeFor(string areaId)
        {
            foreach (var u in upgrades)
                if (u != null && u.effect == UpgradeEffect.ForestArea && u.targetId == areaId) return u;
            return null;
        }
    }
}
