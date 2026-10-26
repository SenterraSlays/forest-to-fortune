using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Shared particle bursts. Each effect is one particle system emitted into with EmitParams,
    /// so any number of bursts costs no extra objects (pooled by design).
    /// </summary>
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }

        [SerializeField] Material m_ParticleMaterial;

        ParticleSystem m_Chips, m_Dust, m_Leaves, m_Sparkle;
        readonly System.Collections.Generic.Dictionary<ParticleSystem, Vector2> m_Sizes = new System.Collections.Generic.Dictionary<ParticleSystem, Vector2>();

        void Awake()
        {
            Instance = this;
            m_Chips = Make("Chips", 600, 1.1f, 9f, true, 0.05f, 0.11f, ParticleSystemRenderMode.Mesh);
            m_Dust = Make("Dust", 300, 1.6f, -0.3f, false, 0.25f, 0.6f, ParticleSystemRenderMode.Billboard);
            m_Leaves = Make("Leaves", 400, 3.5f, 0.6f, true, 0.07f, 0.13f, ParticleSystemRenderMode.Mesh);
            m_Sparkle = Make("Sparkle", 300, 0.9f, -0.5f, false, 0.05f, 0.12f, ParticleSystemRenderMode.Billboard);
            var noise = m_Leaves.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.6f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        ParticleSystem Make(string name, int max, float life, float gravity, bool spin, float sizeMin, float sizeMax, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.startLifetime = life;
            main.gravityModifier = gravity / 9.81f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation3D = spin;
            main.startSize3D = spin;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (spin)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            }
            var collision = ps.collision;
            if (gravity > 1f)
            {
                // Chips settle on the ground instead of falling through it.
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.bounce = 0.2f;
                collision.dampen = 0.6f;
                collision.lifetimeLoss = 0f;
                var plane = new GameObject("Ground").transform;
                plane.SetParent(go.transform, false);
                collision.AddPlane(plane);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Mesh) r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            r.sharedMaterial = m_ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = mode == ParticleSystemRenderMode.Mesh ? ParticleSystemRenderSpace.Local : ParticleSystemRenderSpace.View;
            ps.Play();
            m_Sizes[ps] = new Vector2(sizeMin, sizeMax);
            return ps;
        }

        /// <summary>Sets the ground plane height used by chip collisions.</summary>
        void Ground(ParticleSystem ps, float y)
        {
            var c = ps.collision;
            if (!c.enabled || c.planeCount == 0) return;
            var p = c.GetPlane(0);
            p.position = new Vector3(0f, y + 0.02f, 0f);
        }

        static void Emit(ParticleSystem ps, int count, Vector3 pos, Vector3 dir, float speedMin, float speedMax, float spread, Color color, float sizeMul = 1f)
        {
            var size = Instance.m_Sizes.TryGetValue(ps, out var sz) ? sz : new Vector2(0.1f, 0.1f);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 d = (dir + Random.insideUnitSphere * spread).normalized;
                ep.position = pos + Random.insideUnitSphere * 0.05f;
                ep.velocity = d * Random.Range(speedMin, speedMax);
                ep.startColor = Color.Lerp(color, color * 0.75f, Random.value);
                ep.startSize = Random.Range(size.x, size.y) * sizeMul;
                if (ps.main.startSize3D)
                {
                    ep.rotation3D = Random.insideUnitSphere * 180f;
                    ep.startSize3D = new Vector3(ep.startSize, ep.startSize * 0.35f, ep.startSize * 0.6f);
                }
                ps.Emit(ep, 1);
            }
        }

        public static void WoodChips(Vector3 pos, Vector3 normal, Color color, int count = 12, float groundY = 0f)
        {
            if (Instance == null) return;
            Instance.Ground(Instance.m_Chips, groundY);
            Emit(Instance.m_Chips, count, pos, normal + Vector3.up * 0.6f, 2f, 5f, 0.7f, color);
        }

        public static void Dust(Vector3 pos, int count = 10, float radius = 1f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(Random.Range(-radius, radius), 0.1f, Random.Range(-radius, radius));
                Emit(Instance.m_Dust, 1, p, Vector3.up, 0.3f, 1.2f, 1f, new Color(0.62f, 0.55f, 0.44f, 0.55f), 1.4f);
            }
        }

        public static void Leaves(Vector3 pos, Color color, int count = 8, float radius = 1.5f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
                Emit(Instance.m_Leaves, 1, pos + Random.insideUnitSphere * radius, Vector3.down, 0.2f, 1f, 1f, color);
        }

        public static void Sparkle(Vector3 pos, Color color, int count = 14)
        {
            if (Instance == null) return;
            Emit(Instance.m_Sparkle, count, pos, Vector3.up, 1f, 3f, 1f, color);
        }
    }
}
