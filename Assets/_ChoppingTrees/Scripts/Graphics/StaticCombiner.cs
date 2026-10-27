using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChoppingTrees
{
    /// <summary>
    /// Merges thousands of small static renderers (grass, flowers, the forest beyond the walls)
    /// into one mesh per grid cell, with a submesh per material. Phones spend most of the frame
    /// culling and submitting draws, so ~6,400 renderers becoming a few hundred is the biggest
    /// win there. The source objects stay (colliders keep working); only their renderers are
    /// switched off. Sources need Read/Write meshes (set in ModelImport); others are left alone.
    /// </summary>
    public class StaticCombiner
    {
        readonly string m_Name;
        readonly float m_CellSize;
        readonly List<GameObject> m_Combined = new List<GameObject>();
        readonly List<MeshRenderer> m_Sources = new List<MeshRenderer>();

        public StaticCombiner(string name, float cellSize)
        {
            m_Name = name;
            m_CellSize = cellSize;
        }

        public int CombinedCount => m_Combined.Count;
        public int SourceCount => m_Sources.Count;

        /// <summary>True for renderers nothing moves: no scripts, animators or rigidbodies above them.</summary>
        public static bool IsStatic(MeshRenderer r)
        {
            if (r.GetComponentInParent<MonoBehaviour>(true) != null) return false;
            if (r.GetComponentInParent<Rigidbody>(true) != null) return false;
            if (r.GetComponentInParent<Animator>(true) != null) return false;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable;
        }

        /// <summary>Restores the source renderers and removes the merged meshes.</summary>
        public void Clear()
        {
            foreach (var go in m_Combined)
            {
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Object.Destroy(mf.sharedMesh);
                Object.Destroy(go);
            }
            m_Combined.Clear();
            foreach (var r in m_Sources)
                if (r != null) r.enabled = true;
            m_Sources.Clear();
        }

        /// <summary>
        /// Merges the given renderers that are currently shown (active, static, readable). Hidden
        /// ones are skipped, so call again after changing which props are shown.
        /// </summary>
        public void Build(IEnumerable<MeshRenderer> renderers)
        {
            Clear();
            // cell + layer + shadow mode -> material -> pieces
            var groups = new Dictionary<(int x, int z, int layer, ShadowCastingMode shadows), Dictionary<Material, List<CombineInstance>>>();
            foreach (var r in renderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy || !IsStatic(r)) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var mats = r.sharedMaterials;
                var c = r.bounds.center;
                var key = (Mathf.FloorToInt(c.x / m_CellSize), Mathf.FloorToInt(c.z / m_CellSize), r.gameObject.layer, r.shadowCastingMode);
                if (!groups.TryGetValue(key, out var byMat)) groups[key] = byMat = new Dictionary<Material, List<CombineInstance>>();
                var matrix = r.localToWorldMatrix;
                for (int i = 0; i < Mathf.Min(mats.Length, mesh.subMeshCount); i++)
                {
                    if (mats[i] == null) continue;
                    if (!byMat.TryGetValue(mats[i], out var list)) byMat[mats[i]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = matrix });
                }
                r.enabled = false;
                m_Sources.Add(r);
            }

            var root = GameObject.Find(m_Name) ?? new GameObject(m_Name);
            foreach (var g in groups)
            {
                var parts = new List<CombineInstance>();
                var materials = new List<Material>();
                foreach (var m in g.Value)
                {
                    // One submesh per material first, then those submeshes into the cell mesh.
                    var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                    part.CombineMeshes(m.Value.ToArray(), true, true);
                    parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                    materials.Add(m.Key);
                }
                var mesh = new Mesh { name = $"{m_Name}_{g.Key.x}_{g.Key.z}", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), false, false);
                foreach (var p in parts) Object.Destroy(p.mesh);
                mesh.UploadMeshData(true);

                var go = new GameObject(mesh.name) { layer = g.Key.layer };
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = materials.ToArray();
                mr.shadowCastingMode = g.Key.shadows;
                m_Combined.Add(go);
            }
        }
    }
}
