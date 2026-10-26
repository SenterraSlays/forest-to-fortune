using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// A log left by a felled tree. Pops out with a scripted arc (no rigidbody), then flies to the
    /// player when they come close and their backpack has room. A log that doesn't fit is split:
    /// the backpack takes what it can and the rest stays on the ground. Pooled and capped: the
    /// oldest log is auto-collected when the cap is hit, and logs collect themselves after a
    /// while if there's room in the backpack (whatever doesn't fit at the cap is lost).
    /// </summary>
    public class WoodPickup : MonoBehaviour
    {
        static readonly List<WoodPickup> s_Active = new List<WoodPickup>();
        static readonly Stack<WoodPickup> s_Pool = new Stack<WoodPickup>();
        static GameObject s_Prefab;
        static Transform s_Root;

        string m_Resource;
        double m_Amount;
        float m_Age;
        Vector3 m_Start, m_Rest;
        bool m_Flying;
        float m_FlyT;
        Quaternion m_Spin;
        Color m_Color;

        public static int ActiveCount => s_Active.Count;

        /// <summary>Last time (unscaled) a log near the player was left behind because the backpack was full.</summary>
        public static float LastRefusedTime { get; private set; } = -100f;

        /// <summary>Set by the scene (PickupSpawner) before any tree is felled.</summary>
        public static void Init(GameObject prefab, Transform root)
        {
            s_Prefab = prefab;
            s_Root = root;
            s_Active.Clear();
            s_Pool.Clear();
        }

        public static void Spawn(Vector3 position, string resource, double amount, Color chipColor)
        {
            if (s_Prefab == null || Game.Instance == null)
            {
                Game.Instance?.Model.Collect(resource, amount);
                return;
            }
            int cap = Game.Instance.Db.config.maxActivePickups;
            while (s_Active.Count >= cap) s_Active[0].Collect(false, true);

            WoodPickup p = s_Pool.Count > 0 ? s_Pool.Pop() : null;
            if (p == null)
            {
                var go = Instantiate(s_Prefab, s_Root);
                p = go.GetComponent<WoodPickup>();
                if (p == null) p = go.AddComponent<WoodPickup>();
            }
            p.gameObject.SetActive(true);
            p.m_Resource = resource;
            p.m_Amount = amount;
            p.m_Age = 0f;
            p.m_Flying = false;
            p.m_FlyT = 0f;
            p.m_Color = chipColor;
            p.m_Start = position;
            Vector2 r = Random.insideUnitCircle * 0.8f;
            p.m_Rest = new Vector3(position.x + r.x, GroundY(position), position.z + r.y);
            p.m_Spin = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            p.transform.SetPositionAndRotation(position, p.m_Spin);
            p.transform.localScale = Vector3.one;
            s_Active.Add(p);
        }

        static float GroundY(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 10f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y + 0.18f;
            return p.y - 0.15f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Age += dt;
            var cfg = Game.Instance != null ? Game.Instance.Db.config : null;
            var cam = CameraFx.Instance;

            if (!m_Flying)
            {
                // Pop out of the trunk and settle on the ground.
                float t = Mathf.Clamp01(m_Age / 0.45f);
                Vector3 p = Vector3.Lerp(m_Start, m_Rest, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.7f;
                transform.position = p;
                transform.rotation = m_Spin * Quaternion.Euler(0f, 0f, t * 180f);
                if (t >= 1f)
                {
                    // Gentle idle bob so logs read as collectable.
                    transform.position = m_Rest + Vector3.up * (Mathf.Sin(m_Age * 2.4f) * 0.05f + 0.05f);
                    transform.rotation = m_Spin * Quaternion.Euler(0f, m_Age * 40f, 0f);
                }
                bool room = Game.Instance != null && !Game.Instance.Model.BackpackFull;
                if (cfg != null && cam != null && t >= 0.6f)
                {
                    Vector3 d = cam.transform.position - transform.position;
                    d.y *= 0.5f;
                    if (d.magnitude < Game.Instance.Model.Upgrades.MagnetRadius && UIState.GameplayInput)
                    {
                        if (room) m_Flying = true;
                        else LastRefusedTime = Time.unscaledTime;
                    }
                }
                if (cfg != null && m_Age > cfg.pickupAutoCollectSeconds && room) Collect(false);
                return;
            }

            if (cam == null)
            {
                Collect(true);
                return;
            }
            m_FlyT += dt;
            Vector3 target = cam.transform.position - Vector3.up * 0.6f;
            float speed = 6f + m_FlyT * 22f;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
            transform.localScale = Vector3.one * Mathf.Clamp01(Vector3.Distance(transform.position, target) / 1.2f + 0.3f);
            if (Vector3.Distance(transform.position, target) < 0.35f) Collect(true);
        }

        void Collect(bool byPlayer, bool evicted = false)
        {
            var game = Game.Instance;
            if (game != null)
            {
                double taken = game.Model.Collect(m_Resource, m_Amount);
                if (byPlayer && taken > 0)
                {
                    AudioManager.Ui("collect", 0.55f, 1f + Mathf.Min(0.3f, s_Combo * 0.04f));
                    s_Combo++;
                    s_LastCollect = Time.time;
                    var res = game.Db.Resource(m_Resource);
                    FloatingText.Show(transform.position + Vector3.up * 0.4f, "+" + NumberUtil.Format(taken, true) + " " + (res != null ? res.displayName : m_Resource), m_Color, 0.85f);
                }
                double left = m_Amount - taken;
                // The backpack filled up: the rest drops back to the ground, unless the pickup cap
                // is pushing this log out (then the leftover is lost).
                if (left > 1e-6 && !evicted)
                {
                    m_Amount = left;
                    m_Flying = false;
                    m_Start = transform.position;
                    m_Rest = new Vector3(transform.position.x, GroundY(transform.position), transform.position.z);
                    m_Age = 0f;
                    transform.localScale = Vector3.one;
                    LastRefusedTime = Time.unscaledTime;
                    return;
                }
            }
            s_Active.Remove(this);
            gameObject.SetActive(false);
            s_Pool.Push(this);
        }

        static int s_Combo;
        static float s_LastCollect;

        void LateUpdate()
        {
            if (Time.time - s_LastCollect > 0.6f) s_Combo = 0;
        }
    }
}
