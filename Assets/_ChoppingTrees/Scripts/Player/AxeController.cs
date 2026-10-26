using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>
    /// The equipped axe: procedural swing animation, a single camera spherecast at the impact
    /// moment (so one swing can only ever hit one tree), and hit/miss feedback.
    /// Hold the left mouse button to keep swinging.
    /// </summary>
    public class AxeController : MonoBehaviour
    {
        [SerializeField] Transform m_Camera;
        [SerializeField] Transform m_Holder;
        [Tooltip("One child per axe tier (index = tier).")]
        [SerializeField] GameObject[] m_TierModels;

        // Swing timeline as fractions of the swing duration.
        const float WindupEnd = 0.34f, ImpactAt = 0.48f;

        // Poses in camera space: where the grip is, which way the handle points (towards the head)
        // and which way the blade edge faces. The strike pose puts the head on the crosshair.
        static readonly Pose Idle = Make(new Vector3(0.36f, -0.52f, 0.56f), new Vector3(-0.22f, 1f, 0.2f), new Vector3(-0.2f, 0.1f, 1f));
        static readonly Pose Windup = Make(new Vector3(0.34f, -0.32f, 0.42f), new Vector3(-0.05f, 1f, -0.25f), new Vector3(-0.3f, 0.6f, 1f));
        static readonly Pose Strike = Make(new Vector3(0.16f, -0.44f, 0.52f), new Vector3(-0.32f, 0.5f, 0.8f), new Vector3(-0.1f, -0.5f, 1f));

        static Pose Make(Vector3 grip, Vector3 handle, Vector3 edge)
        {
            handle.Normalize();
            return new Pose(grip, Quaternion.LookRotation(Vector3.ProjectOnPlane(edge, handle).normalized, handle));
        }

        float m_SwingTime = -1f;
        float m_SwingDuration = 0.6f;
        bool m_ImpactDone;
        float m_HitStop;
        float m_Recoil;
        int m_ShownTier = -1;
        Vector3 m_SwayPos;
        Upgrades m_Upgrades;
        GameConfig m_Config;

        public bool Swinging => m_SwingTime >= 0f;
        /// <summary>The tree under the crosshair within reach (for the HUD), or null.</summary>
        public ChoppableTree AimedTree { get; private set; }
        /// <summary>Raised on every swing that lands on something.</summary>
        public event System.Action<HitResult, ChoppableTree> Hit;

        void Start()
        {
            m_Upgrades = Game.Instance.Model.Upgrades;
            m_Config = Game.Instance.Db.config;
            Game.Instance.Model.Purchased += _ => RefreshModel();
            RefreshModel();
        }

        void RefreshModel()
        {
            int tier = Mathf.Clamp(m_Upgrades.AxeTier, 0, m_TierModels.Length - 1);
            if (tier == m_ShownTier) return;
            bool upgraded = m_ShownTier >= 0;
            m_ShownTier = tier;
            for (int i = 0; i < m_TierModels.Length; i++)
                if (m_TierModels[i] != null) m_TierModels[i].SetActive(i == tier);
            if (upgraded) m_Recoil = -0.6f; // little "show off" bob when a new axe is equipped
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateAim();

            bool input = UIState.GameplayInput && Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (!Swinging && input) BeginSwing();

            if (Swinging)
            {
                if (m_HitStop > 0f) m_HitStop -= dt;
                else m_SwingTime += dt;
                float t = m_SwingTime / m_SwingDuration;
                if (!m_ImpactDone && t >= ImpactAt)
                {
                    m_ImpactDone = true;
                    DoImpact();
                }
                if (t >= 1f) m_SwingTime = -1f;
            }
            m_Recoil = Mathf.MoveTowards(m_Recoil, 0f, dt * 4f);
            Animate(dt);
        }

        void BeginSwing()
        {
            m_SwingDuration = m_Upgrades.SwingSeconds;
            m_SwingTime = 0f;
            m_ImpactDone = false;
            AudioManager.Ui("axe_swing", 0.45f, Mathf.Lerp(1f, 1.25f, Mathf.InverseLerp(0.6f, 0.25f, m_SwingDuration)));
        }

        void UpdateAim()
        {
            AimedTree = null;
            if (m_Camera == null) return;
            if (Physics.SphereCast(m_Camera.position, 0.12f, m_Camera.forward, out var hit, m_Config != null ? m_Config.reach : 2.7f, GameLayers.AimMask, QueryTriggerInteraction.Ignore))
            {
                var tree = hit.collider.GetComponentInParent<ChoppableTree>();
                if (tree != null && tree.Standing) AimedTree = tree;
            }
        }

        void DoImpact()
        {
            if (!Physics.SphereCast(m_Camera.position, 0.12f, m_Camera.forward, out var hit, m_Config.reach, GameLayers.AimMask, QueryTriggerInteraction.Ignore))
                return; // clean miss: only the swing whoosh

            var tree = hit.collider.GetComponentInParent<ChoppableTree>();
            if (tree == null)
            {
                // Hit the ground, a rock or a building.
                AudioManager.Sfx("axe_thud", hit.point, 0.7f);
                Effects.Dust(hit.point, 3, 0.15f);
                m_Recoil = 0.5f;
                if (CameraFx.Instance != null) CameraFx.Instance.Kick(new Vector3(0.6f, 0f, 0f));
                return;
            }

            bool crit = Random.value < m_Upgrades.CritChance;
            float damage = m_Upgrades.AxeDamage * (crit ? 2f : 1f);
            var result = tree.ReceiveHit(damage, m_Upgrades.AxeTier, hit.point, hit.normal, m_Camera.forward);
            switch (result)
            {
                case HitResult.Damaged:
                case HitResult.Felled:
                    AudioManager.Sfx("tree_hit", hit.point, 1f, tree.Type.soundPitch);
                    Effects.WoodChips(hit.point, hit.normal, tree.Type.chipColor, result == HitResult.Felled ? 22 : 12, tree.transform.position.y);
                    if (crit) FloatingText.Show(hit.point + hit.normal * 0.2f, "CRIT -" + NumberUtil.Format(damage, true), new Color(1f, 0.72f, 0.25f), 1f);
                    else FloatingText.Show(hit.point + hit.normal * 0.2f, "-" + NumberUtil.Format(damage, true), new Color(1f, 0.95f, 0.85f), 0.7f);
                    m_HitStop = 0.045f;
                    m_Recoil = 1f;
                    if (CameraFx.Instance != null) CameraFx.Instance.Kick(new Vector3(1.4f, Random.Range(-0.4f, 0.4f), -0.8f));
                    break;
                case HitResult.TooTough:
                    AudioManager.Sfx("axe_clang", hit.point, 0.9f);
                    Effects.Sparkle(hit.point + hit.normal * 0.05f, new Color(1f, 0.85f, 0.5f), 6);
                    m_Recoil = 1.4f;
                    if (CameraFx.Instance != null) CameraFx.Instance.Kick(new Vector3(2f, 0f, 1.2f));
                    break;
            }
            if (result != HitResult.Ignored) Hit?.Invoke(result, tree);
        }

        void Animate(float dt)
        {
            Pose target;
            if (!Swinging) target = Idle;
            else
            {
                float t = m_SwingTime / m_SwingDuration;
                if (t < WindupEnd) target = Lerp(Idle, Windup, Smooth(t / WindupEnd));
                else if (t < ImpactAt) target = Lerp(Windup, Strike, EaseIn((t - WindupEnd) / (ImpactAt - WindupEnd)));
                else target = Lerp(Strike, Idle, Smooth((t - ImpactAt) / (1f - ImpactAt)));
            }

            // Idle sway follows movement; recoil pushes the axe back after an impact.
            var motor = GetComponent<PlayerMotor>();
            float moving = motor != null ? Mathf.Clamp01(motor.HorizontalSpeed / 5f) : 0f;
            float phase = motor != null ? motor.StrideDistance * 1.9f : 0f;
            Vector3 sway = new Vector3(Mathf.Cos(phase) * 0.012f, Mathf.Abs(Mathf.Sin(phase)) * -0.014f, 0f) * moving;
            sway += new Vector3(0f, Mathf.Sin(Time.time * 1.6f) * 0.004f, 0f);
            m_SwayPos = Vector3.Lerp(m_SwayPos, sway, 1f - Mathf.Exp(-12f * dt));
            Vector3 recoil = new Vector3(0f, 0.02f, -0.06f) * m_Recoil;
            m_Holder.localPosition = target.position + m_SwayPos + recoil;
            m_Holder.localRotation = target.rotation * Quaternion.Euler(-10f * m_Recoil, 0f, 0f);
        }

        static Pose Lerp(Pose a, Pose b, float t) => new Pose(Vector3.LerpUnclamped(a.position, b.position, t), Quaternion.SlerpUnclamped(a.rotation, b.rotation, t));
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }

        /// <summary>Automation hook: swing once as if the mouse was clicked.</summary>
        public void DebugSwing()
        {
            if (!Swinging) BeginSwing();
        }
    }
}
