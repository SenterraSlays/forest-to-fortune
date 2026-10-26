using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>An automated machine type. Counts and upgrades live in <see cref="UpgradeDef"/>s that target its id.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Machine", fileName = "Machine")]
    public class MachineDef : ScriptableObject
    {
        public string id = "chopper";
        public string displayName = "Auto-Chopper";
        [TextArea] public string description;
        public MachineKind kind = MachineKind.Producer;
        [Tooltip("Producer: output units per second per machine. Processor: input units consumed per second per machine.")]
        public double baseRate = 0.6;
        public ResourceDef input;
        [Tooltip("Processor only: input units needed per output unit.")]
        public double inputPerOutput = 2;
        public ResourceDef output;
        [Tooltip("Models per tier (index = tier level). Missing entries fall back to the last one.")]
        public string[] tierModels = { "AutoChopper" };
        [Tooltip("Processing order: producers run before processors that consume their output.")]
        public int order;
    }
}
