using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>The lumber market: sells everything in stock on E.</summary>
    public class SellPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform m_EffectPoint;

        public static SellPoint Instance { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public string Prompt => "Sell wood";

        public string Detail
        {
            get
            {
                var m = Game.Instance.Model;
                double value = m.Market.StockValue();
                return value > 0 ? "Stock worth " + NumberUtil.Money(value) : "Nothing to sell yet";
            }
        }

        public bool CanInteract => true;

        public void Interact()
        {
            var receipt = Game.Instance.Model.Sell();
            Vector3 p = m_EffectPoint != null ? m_EffectPoint.position : transform.position + Vector3.up;
            if (receipt.Empty)
            {
                AudioManager.Ui("error", 0.5f);
                Toasts.Show("Nothing to sell - chop some trees first!", Toasts.Kind.Warning);
                return;
            }
            AudioManager.Sfx("sell", p, 1f);
            Effects.Sparkle(p, new Color(1f, 0.85f, 0.3f), 24);
            FloatingText.Show(p + Vector3.up * 0.4f, "+" + NumberUtil.Money(receipt.total), new Color(1f, 0.86f, 0.35f), 1.3f);
        }
    }
}
