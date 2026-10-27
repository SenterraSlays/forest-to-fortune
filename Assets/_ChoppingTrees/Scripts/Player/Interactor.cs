using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>Finds the interactable under the crosshair and uses it on E.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] Transform m_Camera;
        [SerializeField] float m_Reach = 3.4f;

        public IInteractable Current { get; private set; }

        void Update()
        {
            Current = null;
            if (!UIState.GameplayInput) return;
            if (Physics.SphereCast(m_Camera.position, 0.2f, m_Camera.forward, out var hit, m_Reach, GameLayers.AimMask, QueryTriggerInteraction.Collide))
                Current = hit.collider.GetComponentInParent<IInteractable>();
            if (Current != null && Current.Prompt == null) Current = null;
            // Interact even when it can't succeed: each interactable explains why it failed.
            if (Current != null && GameInput.UsePressed) Current.Interact();
        }
    }
}
