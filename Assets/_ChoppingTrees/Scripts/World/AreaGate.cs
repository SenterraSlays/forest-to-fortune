using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// The gate into a locked forest area. Can be bought here or in the upgrade menu; once the
    /// area is unlocked the doors swing open and the barrier disappears.
    /// </summary>
    public class AreaGate : MonoBehaviour, IInteractable
    {
        [SerializeField] ForestAreaDef m_Area;
        [SerializeField] Transform m_LeftDoor, m_RightDoor;
        [SerializeField] Collider m_Barrier;
        [SerializeField] TMP_Text m_Sign;

        bool m_Open;
        float m_OpenT;
        Quaternion m_LeftRest = Quaternion.identity, m_RightRest = Quaternion.identity;
        UpgradeDef m_Upgrade;

        public ForestAreaDef Area => m_Area;

        void Start()
        {
            if (m_LeftDoor != null) m_LeftRest = m_LeftDoor.localRotation;
            if (m_RightDoor != null) m_RightRest = m_RightDoor.localRotation;
            var g = Game.Instance;
            m_Upgrade = g.Db.AreaUpgradeFor(m_Area.id);
            g.Model.Purchased += OnPurchased;
            if (g.Model.Upgrades.AreaUnlocked(m_Area)) SetOpen(true, true);
            RefreshSign();
        }

        void OnDestroy()
        {
            if (Game.Instance != null) Game.Instance.Model.Purchased -= OnPurchased;
        }

        void OnPurchased(UpgradeDef u)
        {
            if (u == m_Upgrade && !m_Open)
            {
                SetOpen(true, false);
                AudioManager.Sfx("gate_open", transform.position + Vector3.up, 1f);
                AudioManager.Ui("unlock", 0.8f);
                Effects.Sparkle(transform.position + Vector3.up * 2f, new Color(0.7f, 1f, 0.6f), 40);
                Toasts.Show(m_Area.displayName + " unlocked! New trees await.", Toasts.Kind.Success);
            }
            RefreshSign();
        }

        void SetOpen(bool open, bool instant)
        {
            m_Open = open;
            if (m_Barrier != null) m_Barrier.enabled = !open;
            if (instant) m_OpenT = open ? 1f : 0f;
            enabled = true;
        }

        void RefreshSign()
        {
            if (m_Sign == null) return;
            if (m_Open) { m_Sign.text = m_Area.displayName; return; }
            string req = m_Upgrade != null ? Game.Instance.Model.Upgrades.MissingRequirement(m_Upgrade) : null;
            string cost = m_Upgrade != null ? NumberUtil.Money(Game.Instance.Model.Upgrades.NextCost(m_Upgrade)) : "";
            m_Sign.text = $"{m_Area.displayName}\n<size=70%>{cost}{(req != null ? "\n" + req : "")}</size>";
        }

        void Update()
        {
            float target = m_Open ? 1f : 0f;
            m_OpenT = Mathf.MoveTowards(m_OpenT, target, Time.deltaTime * 0.6f);
            float a = Mathf.SmoothStep(0f, 1f, m_OpenT) * 100f;
            // Rotate about the parent's vertical axis, keeping each door's imported rest orientation.
            if (m_LeftDoor != null) m_LeftDoor.localRotation = Quaternion.AngleAxis(-a, Vector3.up) * m_LeftRest;
            if (m_RightDoor != null) m_RightDoor.localRotation = Quaternion.AngleAxis(a, Vector3.up) * m_RightRest;
            if (Mathf.Approximately(m_OpenT, target)) enabled = false;
        }

        public string Prompt => m_Open ? null : "Unlock " + m_Area.displayName;

        public string Detail
        {
            get
            {
                if (m_Open || m_Upgrade == null) return null;
                var up = Game.Instance.Model.Upgrades;
                string req = up.MissingRequirement(m_Upgrade);
                return NumberUtil.Money(up.NextCost(m_Upgrade)) + (req != null ? "  -  " + req : "");
            }
        }

        public bool CanInteract => !m_Open && m_Upgrade != null && Game.Instance.Model.Upgrades.Blocker(m_Upgrade) == null;

        public void Interact()
        {
            if (m_Open || m_Upgrade == null) return;
            if (!Game.Instance.Model.TryBuy(m_Upgrade, out string why))
            {
                AudioManager.Ui("error", 0.6f);
                Toasts.Show(why, Toasts.Kind.Warning);
            }
        }
    }
}
