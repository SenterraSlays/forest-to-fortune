using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>The workbench that opens the upgrade menu.</summary>
    public class UpgradeStation : MonoBehaviour, IInteractable
    {
        public static UpgradeStation Instance { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public string Prompt => "Open upgrades";

        public string Detail
        {
            get
            {
                int n = AffordableCount();
                return n > 0 ? n + (n == 1 ? " upgrade" : " upgrades") + " affordable" : null;
            }
        }

        public bool CanInteract => true;

        public void Interact()
        {
            if (UpgradeMenu.Instance != null) UpgradeMenu.Instance.Open();
        }

        public static int AffordableCount()
        {
            var g = Game.Instance;
            if (g == null) return 0;
            int n = 0;
            foreach (var u in g.Db.upgrades)
                if (u != null && g.Model.Upgrades.Blocker(u) == null) n++;
            return n;
        }
    }
}
