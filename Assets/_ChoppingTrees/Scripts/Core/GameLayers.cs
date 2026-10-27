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
        /// <summary>Small scattered props (grass, flowers, ferns, small rocks): drawn only close by.</summary>
        public const int Scenery = 13;
        /// <summary>Large props and the decorative forest beyond the walls: drawn further out.</summary>
        public const int Decor = 14;

        public static readonly string[] Names = { null, null, null, null, null, null, null, null, "Ground", "Tree", "Interactable", "Player", "Pickup", "Scenery", "Decor" };

        public const int GroundMask = 1 << Ground | 1 << Default | 1 << Decor;
        /// <summary>What the axe and the interaction ray can hit (pickups and the player are ignored).</summary>
        public const int AimMask = 1 << Default | 1 << Ground | 1 << Tree | 1 << Interactable;
    }
}
