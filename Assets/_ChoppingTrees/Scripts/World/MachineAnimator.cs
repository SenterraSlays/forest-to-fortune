using System;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Drives a machine model's moving parts (spinning blades, swinging arms, sliding logs) and
    /// its work cycle effects. Purely visual: production is computed by <see cref="Production"/>.
    /// Speed follows the machine's real output multiplier so upgrades are visible.
    /// </summary>
    public class MachineAnimator : MonoBehaviour
    {
        public enum Motion { Spin, Swing, Slide, Bob }

        [Serializable]
        public class Part
        {
            public Transform transform;
            public Motion motion;
            [Tooltip("Axis in the part's parent space.")]
            public Vector3 axis = Vector3.up;
            [Tooltip("Spin: degrees/s. Swing: degrees. Slide/Bob: metres.")]
            public float amount = 360f;
            [Tooltip("Phase offset in cycles.")]
            public float phase;
            [HideInInspector] public Vector3 restPos;
            [HideInInspector] public Quaternion restRot;
            [HideInInspector] public float angle;
        }

        [SerializeField] string m_MachineId;
        [SerializeField] Part[] m_Parts;
        [Tooltip("Where chips or sawdust spray at the moment of each work cycle.")]
        [SerializeField] Transform m_WorkPoint;
        [SerializeField] Color m_ChipColor = new Color(0.86f, 0.7f, 0.48f);
        [SerializeField] float m_BaseCycleSeconds = 3f;
        [SerializeField] string m_LoopSound = "chopper_loop";
        [SerializeField] string m_CycleSound = "machine_cut";

        float m_Cycle;
        float m_Build = 1f;
        AudioSource m_Loop;
        Production m_Production;
        Upgrades m_Upgrades;
        MachineDef m_Def;

        public void Configure(string machineId, Part[] parts, Transform workPoint, Color chipColor, string loopSound, string cycleSound, float cycleSeconds)
        {
            m_MachineId = machineId;
            m_Parts = parts;
            m_WorkPoint = workPoint;
            m_ChipColor = chipColor;
            m_LoopSound = loopSound;
            m_CycleSound = cycleSound;
            m_BaseCycleSeconds = cycleSeconds;
        }

        /// <summary>Plays the "just built" pop-in.</summary>
        public void PlayBuild()
        {
            m_Build = 0f;
            transform.localScale = Vector3.one * 0.01f;
        }

        void Start()
        {
            foreach (var p in m_Parts)
            {
                if (p.transform == null) continue;
                p.restPos = p.transform.localPosition;
                p.restRot = p.transform.localRotation;
            }
            var g = Game.Instance;
            m_Production = g.Model.Production;
            m_Upgrades = g.Model.Upgrades;
            m_Def = g.Db.Machine(m_MachineId);
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(m_LoopSound))
                m_Loop = AudioManager.Instance.CreateLoop(m_LoopSound, transform, 0.35f, 22f);
            m_Cycle = UnityEngine.Random.value;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (m_Build < 1f)
            {
                m_Build = Mathf.Min(1f, m_Build + dt / 0.6f);
                float u = m_Build - 1f;
                transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f + 2.6f * u * u * u + 1.6f * u * u);
            }

            double mult = m_Upgrades != null ? m_Upgrades.MachineMultiplier(m_MachineId) : 1;
            // Visual speed grows with the square root so heavily upgraded machines stay readable.
            float speed = Mathf.Clamp((float)Math.Sqrt(Math.Max(1, mult)), 1f, 3.5f);
            float cycleSeconds = m_BaseCycleSeconds / speed;
            float prev = m_Cycle;
            m_Cycle += dt / cycleSeconds;
            if (Mathf.Floor(m_Cycle) != Mathf.Floor(prev)) OnCycle(cycleSeconds);
            float c = m_Cycle - Mathf.Floor(m_Cycle);

            foreach (var p in m_Parts)
            {
                if (p.transform == null) continue;
                float pc = (c + p.phase) % 1f;
                switch (p.motion)
                {
                    case Motion.Spin:
                        p.angle = (p.angle + p.amount * speed * dt) % 360f;
                        p.transform.localRotation = Quaternion.AngleAxis(p.angle, p.axis) * p.restRot;
                        break;
                    case Motion.Swing:
                        // Slow lift, quick cut, short pause.
                        float s = pc < 0.6f ? Mathf.SmoothStep(0f, 1f, pc / 0.6f) : pc < 0.75f ? 1f - (pc - 0.6f) / 0.15f : 0f;
                        p.transform.localRotation = Quaternion.AngleAxis(s * p.amount, p.axis) * p.restRot;
                        break;
                    case Motion.Slide:
                        p.transform.localPosition = p.restPos + p.axis.normalized * (pc * p.amount);
                        break;
                    case Motion.Bob:
                        p.transform.localPosition = p.restPos + p.axis.normalized * (Mathf.Sin(pc * Mathf.PI * 2f) * p.amount);
                        break;
                }
            }
            if (m_Loop != null) m_Loop.pitch = 0.9f + speed * 0.1f;
        }

        void OnCycle(float cycleSeconds)
        {
            if (m_WorkPoint == null) return;
            var cam = CameraFx.Instance;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, m_WorkPoint.position) : 999f;
            if (dist > 40f) return; // effects only near the player
            Effects.WoodChips(m_WorkPoint.position, transform.forward, m_ChipColor, 8, transform.position.y);
            if (!string.IsNullOrEmpty(m_CycleSound)) AudioManager.Sfx(m_CycleSound, m_WorkPoint.position, 0.5f);
            if (dist < 16f && m_Def != null && m_Production != null)
            {
                double amount = m_Production.RatePerMachine(m_Def) * cycleSeconds;
                FloatingText.Show(m_WorkPoint.position + Vector3.up * 1.2f, "+" + NumberUtil.Format(amount, true) + " " + (m_Def.output != null ? m_Def.output.displayName : ""), m_ChipColor, 0.75f);
            }
        }
    }
}
