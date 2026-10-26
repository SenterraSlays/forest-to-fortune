namespace ChoppingTrees
{
    /// <summary>Physics layers (set up by the editor's project setup).</summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Ground = 8;
        public const int Tree = 9;
        public const int Interactable = 10;
        public const int Player = 11;
        public const int Pickup = 12;

        public static readonly string[] Names = { null, null, null, null, null, null, null, null, "Ground", "Tree", "Interactable", "Player", "Pickup" };

        public const int GroundMask = 1 << Ground | 1 << Default;
        /// <summary>What the axe and the interaction ray can hit (pickups and the player are ignored).</summary>
        public const int AimMask = 1 << Default | 1 << Ground | 1 << Tree | 1 << Interactable;
    }
}
