namespace ChoppingTrees
{
    /// <summary>Something the player can use with E while looking at it.</summary>
    public interface IInteractable
    {
        /// <summary>Prompt shown under the crosshair, e.g. "Sell wood".</summary>
        string Prompt { get; }
        /// <summary>Optional second line (prices, requirements). Null for none.</summary>
        string Detail { get; }
        bool CanInteract { get; }
        void Interact();
    }
}
