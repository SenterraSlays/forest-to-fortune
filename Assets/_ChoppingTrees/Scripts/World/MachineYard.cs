using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Shows exactly as many machines as the player owns, in fixed slots, using the model for the
    /// machine's current tier. Rebuilt from state on load (never duplicated on scene reload) and
    /// updated after purchases, with a build animation for newly bought machines.
    /// </summary>
    public class MachineYard : MonoBehaviour
    {
        [Serializable]
        public class Group
        {
            public string machineId;
            public Transform[] slots;
            [Tooltip("Prefab per tier (index = tier level).")]
            public GameObject[] tierPrefabs;
            [HideInInspector] public List<GameObject> spawned = new List<GameObject>();
            [HideInInspector] public int shownTier = -1;
        }

        [SerializeField] Group[] m_Groups;
        [Tooltip("Shown on empty slots that can be filled next.")]
        [SerializeField] GameObject m_SlotMarkerPrefab;

        readonly List<GameObject> m_Markers = new List<GameObject>();

        public int Spawned(string machineId)
        {
            foreach (var g in m_Groups) if (g.machineId == machineId) return g.spawned.Count;
            return 0;
        }

        void Start()
        {
            Game.Instance.Model.Purchased += _ => Sync(true);
            Sync(false);
        }

        void Sync(bool animate)
        {
            var up = Game.Instance.Model.Upgrades;
            foreach (var m in m_Markers) if (m != null) Destroy(m);
            m_Markers.Clear();

            foreach (var g in m_Groups)
            {
                int want = Mathf.Min(up.MachineCount(g.machineId), g.slots.Length);
                int tier = Mathf.Clamp(up.MachineTier(g.machineId), 0, g.tierPrefabs.Length - 1);
                if (tier != g.shownTier)
                {
                    // New tier: replace every model.
                    foreach (var go in g.spawned) if (go != null) Destroy(go);
                    g.spawned.Clear();
                    bool tierUpgrade = g.shownTier >= 0;
                    g.shownTier = tier;
                    for (int i = 0; i < want; i++) Spawn(g, i, animate && tierUpgrade);
                }
                while (g.spawned.Count > want)
                {
                    Destroy(g.spawned[g.spawned.Count - 1]);
                    g.spawned.RemoveAt(g.spawned.Count - 1);
                }
                while (g.spawned.Count < want) Spawn(g, g.spawned.Count, animate);

                // Mark the next free slot whenever another machine of this type can be bought.
                var countUpgrade = Game.Instance.Db.CountUpgradeFor(g.machineId);
                bool available = countUpgrade != null && !up.IsLocked(countUpgrade);
                if (m_SlotMarkerPrefab != null && available && want < g.slots.Length)
                    m_Markers.Add(Instantiate(m_SlotMarkerPrefab, g.slots[want].position, g.slots[want].rotation, transform));
            }
        }

        void Spawn(Group g, int index, bool animate)
        {
            var slot = g.slots[index];
            var prefab = g.tierPrefabs[Mathf.Clamp(g.shownTier, 0, g.tierPrefabs.Length - 1)];
            var go = Instantiate(prefab, slot.position, slot.rotation, transform);
            go.name = $"{g.machineId}_{index}";
            g.spawned.Add(go);
            if (animate)
            {
                var anim = go.GetComponent<MachineAnimator>();
                if (anim != null) anim.PlayBuild();
                Effects.Dust(slot.position, 18, 2f);
                AudioManager.Sfx("build", slot.position + Vector3.up, 1f);
            }
        }
    }
}
