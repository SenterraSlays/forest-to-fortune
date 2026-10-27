using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// The Legacy tree: the Workbench's skill tree screen showing the Legacy perks instead, paid
    /// with skill points and kept through every rebirth. Four branches (Heritage, Might, Industry,
    /// Fortune) grow from a rebirth emblem. Opened from the Rebirth screen.
    /// </summary>
    public class LegacyMenu : UpgradeMenu
    {
        public new static LegacyMenu Instance { get; private set; }

        protected override bool Legacy => true;
        protected override Rect Bounds => Rect.MinMaxRect(-7.9f, -1.25f, 7.9f, 8.1f);
        protected override Vector2 RootPos => new Vector2(0f, 0.05f);
        protected override Vector2 HomePos => new Vector2(0f, 3.4f);
        protected override float HomeScale => 0.62f;
        protected override Color RootColor => UIStyle.Skill;
        protected override Color RootFace => new Color(0.17f, 0.13f, 0.23f);
        protected override string RootIcon => "rebirth";
        protected override Color Trim => UIStyle.Skill;

        protected override void Register() => Instance = this;

        protected override void Unregister()
        {
            if (Instance == this) Instance = null;
        }
    }
}
