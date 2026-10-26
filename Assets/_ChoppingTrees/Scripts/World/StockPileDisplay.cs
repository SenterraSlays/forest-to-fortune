using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// A pile next to the market that grows with the amount of a resource in stock
    /// (a fixed set of pre-placed pieces is shown progressively; nothing is spawned).
    /// </summary>
    public class StockPileDisplay : MonoBehaviour
    {
        [SerializeField] string m_ResourceId = "wood";
        [SerializeField] Transform[] m_Pieces;
        [Tooltip("Stock needed for the first piece; each further piece needs this much more times the growth.")]
        [SerializeField] float m_FirstAt = 5f;
        [SerializeField] float m_Growth = 1.35f;

        int m_Shown = -1;
        float m_Check;

        public void Configure(string resourceId, Transform[] pieces, float firstAt, float growth)
        {
            m_ResourceId = resourceId;
            m_Pieces = pieces;
            m_FirstAt = firstAt;
            m_Growth = growth;
        }

        void Update()
        {
            m_Check -= Time.deltaTime;
            if (m_Check > 0f) return;
            m_Check = 0.25f;
            double stock = Game.Instance.Model.State.Get(m_ResourceId);
            int n = 0;
            double need = m_FirstAt;
            while (n < m_Pieces.Length && stock >= need)
            {
                n++;
                need *= m_Growth;
            }
            if (n == m_Shown) return;
            for (int i = 0; i < m_Pieces.Length; i++) m_Pieces[i].gameObject.SetActive(i < n);
            m_Shown = n;
        }
    }
}
