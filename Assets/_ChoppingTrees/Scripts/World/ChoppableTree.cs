using UnityEngine;

namespace ChoppingTrees
{
    public enum HitResult
    {
        Ignored,
        Damaged,
        Felled,
        TooTough,
    }

    /// <summary>
    /// A tree the player can chop. Layout (built by the scene builder):
    /// root (this + trunk capsule) / Stump (always there, own small collider) /
    /// Hinge at notch height (the falling part, holds the tree model) / Notch (the cut, grows with damage).
    /// Falling is animated (no rigidbody), so it can't tumble or sink through the ground.
    /// The component only updates while something is animating.
    /// </summary>
    public class ChoppableTree : MonoBehaviour
    {
        enum Phase { Standing, Falling, Fallen, Waiting, Regrowing }

        [SerializeField] TreeType m_Type;
        [SerializeField] Transform m_Hinge;
        [SerializeField] Transform m_Notch;
        [SerializeField] CapsuleCollider m_Trunk;
        [SerializeField] float m_Scale = 1f;

        Phase m_Phase = Phase.Standing;
        double m_Health;
        float m_Timer;
        Vector3 m_FallAxis, m_FallDir, m_Pivot, m_HingeRest;
        float m_FallDuration, m_FallAngle;
        bool m_Landed;
        Vector3 m_WobbleAxis;
        float m_Wobble, m_WobbleVel;
        Vector3 m_NotchDir = Vector3.forward;
        float m_Lean;
        Vector3 m_LeanAxis = Vector3.right;

        public TreeType Type => m_Type;
        public double Health => m_Health;
        public double MaxHealth => m_Type.maxHealth;
        public bool Standing => m_Phase == Phase.Standing;
        public float Scale => m_Scale;

        /// <summary>Scene builder hook.</summary>
        public void Setup(TreeType type, Transform hinge, Transform notch, CapsuleCollider trunk, float scale)
        {
            m_Type = type;
            m_Hinge = hinge;
            m_Notch = notch;
            m_Trunk = trunk;
            m_Scale = scale;
        }

        void Awake()
        {
            m_Health = m_Type.maxHealth;
            m_HingeRest = m_Hinge.localPosition;
            if (m_Notch != null) m_Notch.gameObject.SetActive(false);
            enabled = false;
        }

        public HitResult ReceiveHit(float damage, int axeTier, Vector3 point, Vector3 normal, Vector3 fromDir)
        {
            if (m_Phase != Phase.Standing) return HitResult.Ignored;
            if (axeTier < m_Type.requiredAxeTier)
            {
                Wobble(fromDir, 0.4f);
                return HitResult.TooTough;
            }

            bool first = m_Health >= m_Type.maxHealth;
            m_Health = System.Math.Max(0, m_Health - damage);
            Vector3 flat = new Vector3(fromDir.x, 0f, fromDir.z);
            if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
            flat.Normalize();
            if (first) m_NotchDir = -flat;
            UpdateNotch();
            Wobble(flat, 1f);
            Effects.Leaves(m_Hinge.position + Vector3.up * m_Type.trunkHeight * m_Scale * 0.7f, m_Type.leafColor, 3, 1.4f * m_Scale);

            if (m_Health <= 0)
            {
                Fell(flat);
                return HitResult.Felled;
            }
            return HitResult.Damaged;
        }

        void UpdateNotch()
        {
            if (m_Notch == null) return;
            float damaged = 1f - (float)(m_Health / m_Type.maxHealth);
            m_Notch.gameObject.SetActive(damaged > 0f);
            m_Notch.rotation = Quaternion.LookRotation(m_NotchDir, Vector3.up);
            float r = m_Type.trunkRadius * m_Scale;
            // The cut (an exposed-wood band on the bark) widens and grows taller with damage.
            m_Notch.localScale = new Vector3(r * Mathf.Lerp(0.75f, 1.08f, damaged), r * Mathf.Lerp(0.25f, 0.9f, damaged), r);
            // Badly damaged trees start to lean away from the axe, hinting they're about to go.
            m_Lean = Mathf.Lerp(0f, 3.5f, Mathf.InverseLerp(0.5f, 1f, damaged));
            m_LeanAxis = Vector3.Cross(Vector3.up, -m_NotchDir).normalized;
        }

        void Wobble(Vector3 dir, float strength)
        {
            m_WobbleAxis = Vector3.Cross(Vector3.up, dir).normalized;
            m_WobbleVel += 35f * strength / Mathf.Max(0.6f, m_Scale);
            if (m_Phase == Phase.Standing) enabled = true;
        }

        void Fell(Vector3 dir)
        {
            m_Phase = Phase.Falling;
            m_FallDir = dir;
            m_FallAxis = Vector3.Cross(Vector3.up, dir).normalized;
            float radius = m_Type.trunkRadius * m_Scale;
            m_Pivot = m_Hinge.position + dir * radius;
            float length = m_Type.trunkHeight * m_Scale;
            float hingeHeight = m_Hinge.position.y - transform.position.y;
            m_FallAngle = 90f - Mathf.Asin(Mathf.Clamp01(hingeHeight / Mathf.Max(0.5f, length))) * Mathf.Rad2Deg;
            m_FallDuration = Mathf.Lerp(1.0f, 2.0f, Mathf.InverseLerp(4f, 18f, length));
            m_Timer = 0f;
            m_Landed = false;
            m_Wobble = 0f;
            m_WobbleVel = 0f;
            if (m_Trunk != null) m_Trunk.enabled = false;
            if (m_Notch != null) m_Notch.gameObject.SetActive(false);
            AudioManager.Sfx("tree_falling", m_Hinge.position, 1f, m_Type.soundPitch);
            enabled = true;

            var game = Game.Instance;
            if (game != null)
            {
                var up = game.Model.Upgrades;
                if (m_Type.cashBonus > 0)
                {
                    double cash = m_Type.cashBonus * up.TreeCashMultiplier * game.Model.Market.RebirthMultiplier;
                    game.Model.AddMoney(cash);
                    FloatingText.Show(m_Hinge.position + Vector3.up * 1.5f, "+" + NumberUtil.Money(cash), new Color(1f, 0.85f, 0.3f), 1.4f);
                    Effects.Sparkle(m_Hinge.position + Vector3.up, new Color(1f, 0.85f, 0.3f), 30);
                }
                if (up.LuckyFindChance > 0f && Random.value < up.LuckyFindChance)
                {
                    double find = m_Type.woodReward * game.Model.Market.Price(game.Db.Resource(GameModel.Wood)) * game.Db.config.luckyFindValue;
                    game.Model.AddMoney(find);
                    FloatingText.Show(m_Hinge.position + Vector3.up * 2.2f, "Lucky find! +" + NumberUtil.Money(find), new Color(0.55f, 0.95f, 0.6f), 1.4f);
                    Effects.Sparkle(m_Hinge.position + Vector3.up * 1.4f, new Color(0.6f, 1f, 0.6f), 24);
                    AudioManager.Sfx("sell", m_Hinge.position, 0.6f, 1.2f);
                }
                game.Model.NotifyTreeFelled(m_Type);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (m_Phase)
            {
                case Phase.Standing:
                    if (StepWobble(dt)) enabled = false;
                    break;
                case Phase.Falling:
                    UpdateFall(dt);
                    break;
                case Phase.Fallen:
                    m_Timer += dt;
                    if (m_Timer > 0.9f) BreakUp();
                    break;
                case Phase.Waiting:
                    m_Timer += dt;
                    if (m_Timer >= m_Type.respawnSeconds * RegrowthMultiplier && !PlayerTooClose()) BeginRegrow();
                    break;
                case Phase.Regrowing:
                    m_Timer += dt;
                    float t = Mathf.Clamp01(m_Timer / 2.2f);
                    float s = EaseOutBack(t);
                    m_Hinge.localScale = Vector3.one * Mathf.Max(0.01f, s);
                    if (t >= 1f)
                    {
                        m_Hinge.localScale = Vector3.one;
                        m_Phase = Phase.Standing;
                        enabled = false;
                    }
                    break;
            }
        }

        bool StepWobble(float dt)
        {
            m_WobbleVel += (-m_Wobble * 120f - m_WobbleVel * 9f) * dt;
            m_Wobble += m_WobbleVel * dt;
            var lean = Quaternion.AngleAxis(m_Lean, transform.InverseTransformDirection(m_LeanAxis));
            m_Hinge.localRotation = lean * Quaternion.AngleAxis(m_Wobble, transform.InverseTransformDirection(m_WobbleAxis));
            bool settled = Mathf.Abs(m_Wobble) < 0.01f && Mathf.Abs(m_WobbleVel) < 0.01f;
            if (settled) m_Hinge.localRotation = lean;
            return settled;
        }

        void UpdateFall(float dt)
        {
            m_Timer += dt;
            float t = Mathf.Clamp01(m_Timer / m_FallDuration);
            float angle;
            if (!m_Landed)
            {
                // Accelerates like a toppling pole: slow start, fast finish.
                angle = m_FallAngle * t * t * t;
                if (t >= 1f)
                {
                    m_Landed = true;
                    m_Timer = 0f;
                    Land();
                }
            }
            else
            {
                // Small bounce after hitting the ground.
                float b = Mathf.Clamp01(m_Timer / 0.45f);
                angle = m_FallAngle - Mathf.Sin(b * Mathf.PI) * 4f * (1f - b);
                if (b >= 1f)
                {
                    m_Phase = Phase.Fallen;
                    m_Timer = 0f;
                }
            }
            var rot = Quaternion.AngleAxis(angle, m_FallAxis);
            Vector3 restWorld = transform.TransformPoint(m_HingeRest);
            m_Hinge.SetPositionAndRotation(m_Pivot + rot * (restWorld - m_Pivot), rot * transform.rotation);
        }

        void Land()
        {
            Vector3 mid = m_Pivot + m_FallDir * m_Type.trunkHeight * m_Scale * 0.5f;
            AudioManager.Sfx("tree_land", mid, Mathf.Clamp(m_Scale, 0.8f, 1.3f), m_Type.soundPitch);
            float len = m_Type.trunkHeight * m_Scale;
            for (float d = 0.5f; d < len; d += 1.5f)
                Effects.Dust(new Vector3(m_Pivot.x, transform.position.y, m_Pivot.z) + m_FallDir * d, 3, 0.6f);
            Effects.Leaves(m_Pivot + m_FallDir * len * 0.75f + Vector3.up, m_Type.leafColor, 18, 2f * m_Scale);
            if (CameraFx.Instance != null)
            {
                float dist = Vector3.Distance(CameraFx.Instance.transform.position, mid);
                float k = Mathf.Clamp01(1f - dist / 25f) * Mathf.Clamp(m_Scale, 0.7f, 1.6f);
                if (k > 0f) CameraFx.Instance.Kick(new Vector3(-1.2f * k, 0f, 0.6f * k));
            }
        }

        void BreakUp()
        {
            float len = m_Type.trunkHeight * m_Scale;
            int pieces = Mathf.Max(1, m_Type.logPieces);
            double yield = Game.Instance != null ? Game.Instance.Model.Upgrades.WoodYieldMultiplier : 1f;
            double each = m_Type.woodReward * yield / pieces;
            float groundY = transform.position.y;
            for (int i = 0; i < pieces; i++)
            {
                float d = len * (0.15f + 0.7f * (i + 0.5f) / pieces);
                Vector3 p = new Vector3(m_Pivot.x, groundY + 0.35f, m_Pivot.z) + m_FallDir * d;
                WoodPickup.Spawn(p, GameModel.Wood, each, m_Type.chipColor);
            }
            Effects.Leaves(m_Pivot + m_FallDir * len * 0.7f + Vector3.up * 0.5f, m_Type.leafColor, 12, 2f * m_Scale);
            Effects.Dust(new Vector3(m_Pivot.x, groundY, m_Pivot.z) + m_FallDir * len * 0.5f, 10, len * 0.4f);
            AudioManager.Sfx("log_break", m_Pivot + m_FallDir * len * 0.5f, 0.9f);
            m_Hinge.gameObject.SetActive(false);
            m_Phase = Phase.Waiting;
            m_Timer = 0f;
        }

        static float RegrowthMultiplier => Game.Instance != null ? Game.Instance.Model.Upgrades.RegrowthTimeMultiplier : 1f;

        bool PlayerTooClose()
        {
            var cam = CameraFx.Instance;
            if (cam == null) return false;
            Vector3 d = cam.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude < m_Type.trunkRadius * m_Scale + 1.2f;
        }

        void BeginRegrow()
        {
            m_Phase = Phase.Regrowing;
            m_Timer = 0f;
            m_Health = m_Type.maxHealth;
            m_Lean = 0f;
            m_Hinge.localPosition = m_HingeRest;
            m_Hinge.localRotation = Quaternion.identity;
            m_Hinge.localScale = Vector3.one * 0.01f;
            m_Hinge.gameObject.SetActive(true);
            if (m_Trunk != null) m_Trunk.enabled = true;
            Effects.Leaves(transform.position + Vector3.up * 1.2f, m_Type.leafColor, 6, 0.8f);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Automation/test helper: fells the tree immediately.</summary>
        public void DebugFell(Vector3 dir)
        {
            if (m_Phase != Phase.Standing) return;
            m_Health = 0;
            Fell(new Vector3(dir.x, 0f, dir.z).normalized);
        }
    }
}
