using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// The project's shared URP materials. Blender models use slots with the same names (M_*);
    /// the model importer swaps them for these, so every model shares one consistent palette.
    /// </summary>
    public static class MaterialLibrary
    {
        public const string Dir = ProjectSetup.Root + "/Art/Materials";

        struct Spec
        {
            public Color color;
            public float smooth, metal;
            public Color emission;

            public Spec(float r, float g, float b, float smooth = 0.12f, float metal = 0f, Color? emission = null)
            {
                color = new Color(r, g, b);
                this.smooth = smooth;
                this.metal = metal;
                this.emission = emission ?? Color.black;
            }
        }

        // Linear-ish albedo values tuned for a soft, readable low-poly look.
        static readonly Dictionary<string, Spec> s_Specs = new Dictionary<string, Spec>
        {
            ["M_Bark"] = new Spec(0.36f, 0.24f, 0.15f),
            ["M_BarkDark"] = new Spec(0.16f, 0.13f, 0.11f),
            ["M_BarkBirch"] = new Spec(0.86f, 0.84f, 0.78f),
            ["M_BarkRed"] = new Spec(0.50f, 0.24f, 0.16f),
            ["M_BarkIron"] = new Spec(0.27f, 0.29f, 0.33f, 0.25f),
            ["M_BarkGold"] = new Spec(0.80f, 0.58f, 0.22f, 0.45f, 0.4f),
            ["M_WoodCut"] = new Spec(0.92f, 0.75f, 0.50f),
            ["M_WoodCutDark"] = new Spec(0.60f, 0.42f, 0.24f),
            ["M_Leaf"] = new Spec(0.33f, 0.58f, 0.24f),
            ["M_LeafDark"] = new Spec(0.19f, 0.42f, 0.21f),
            ["M_LeafPine"] = new Spec(0.16f, 0.38f, 0.25f),
            ["M_LeafBirch"] = new Spec(0.55f, 0.72f, 0.30f),
            ["M_LeafMaple"] = new Spec(0.88f, 0.36f, 0.15f),
            ["M_LeafAutumn"] = new Spec(0.95f, 0.65f, 0.20f),
            ["M_LeafIron"] = new Spec(0.22f, 0.54f, 0.56f, 0.2f),
            ["M_LeafGold"] = new Spec(1.0f, 0.80f, 0.32f, 0.35f, 0f, new Color(0.35f, 0.24f, 0.05f)),
            ["M_Wood"] = new Spec(0.62f, 0.42f, 0.24f),
            ["M_WoodDark"] = new Spec(0.36f, 0.23f, 0.14f),
            ["M_Plank"] = new Spec(0.80f, 0.62f, 0.40f),
            ["M_Metal"] = new Spec(0.55f, 0.56f, 0.58f, 0.45f, 0.6f),
            ["M_MetalDark"] = new Spec(0.20f, 0.21f, 0.23f, 0.35f, 0.5f),
            ["M_Steel"] = new Spec(0.80f, 0.82f, 0.85f, 0.65f, 0.85f),
            ["M_Rust"] = new Spec(0.45f, 0.27f, 0.17f, 0.2f, 0.3f),
            ["M_Paint"] = new Spec(0.95f, 0.68f, 0.14f, 0.35f),
            ["M_PaintRed"] = new Spec(0.78f, 0.20f, 0.16f, 0.35f),
            ["M_PaintGreen"] = new Spec(0.27f, 0.50f, 0.30f, 0.35f),
            ["M_PaintBlue"] = new Spec(0.20f, 0.38f, 0.66f, 0.4f),
            ["M_Rubber"] = new Spec(0.10f, 0.10f, 0.10f, 0.1f),
            ["M_Stone"] = new Spec(0.55f, 0.55f, 0.52f),
            ["M_StoneDark"] = new Spec(0.38f, 0.38f, 0.38f),
            ["M_Grass"] = new Spec(0.40f, 0.62f, 0.25f),
            ["M_Canvas"] = new Spec(0.92f, 0.88f, 0.78f),
            ["M_Rope"] = new Spec(0.72f, 0.62f, 0.42f),
            ["M_Gold"] = new Spec(1.0f, 0.78f, 0.25f, 0.7f, 0.9f),
            ["M_Glow"] = new Spec(1.0f, 0.62f, 0.20f, 0.5f, 0f, new Color(2.2f, 1.2f, 0.35f)),
            ["M_Leather"] = new Spec(0.40f, 0.24f, 0.14f, 0.25f),
            ["M_Sign"] = new Spec(0.30f, 0.20f, 0.12f),
            ["M_Roof"] = new Spec(0.44f, 0.22f, 0.18f),
            ["M_Glass"] = new Spec(0.60f, 0.80f, 0.90f, 0.85f, 0f, new Color(0.08f, 0.1f, 0.12f)),
            ["M_Snow"] = new Spec(0.95f, 0.96f, 0.98f, 0.3f),
            ["M_Berry"] = new Spec(0.80f, 0.10f, 0.18f, 0.5f),
            ["M_FlowerYellow"] = new Spec(1.0f, 0.85f, 0.25f),
            ["M_FlowerPurple"] = new Spec(0.62f, 0.42f, 0.85f),
            ["M_Dirt"] = new Spec(0.45f, 0.34f, 0.22f),
        };

        public static bool Exists(string name) => s_Specs.ContainsKey(name);

        public static Material Get(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{name}.mat");

        [MenuItem("Tools/Forest to Fortune/2. Build Materials", priority = 2)]
        public static string BuildAll()
        {
            System.IO.Directory.CreateDirectory(Dir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (var kv in s_Specs)
            {
                var m = Get(kv.Key);
                if (m == null)
                {
                    m = new Material(lit) { name = kv.Key };
                    AssetDatabase.CreateAsset(m, $"{Dir}/{kv.Key}.mat");
                }
                m.shader = lit;
                m.SetColor("_BaseColor", kv.Value.color);
                m.SetFloat("_Smoothness", kv.Value.smooth);
                m.SetFloat("_Metallic", kv.Value.metal);
                if (kv.Value.emission.maxColorComponent > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", kv.Value.emission);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else m.DisableKeyword("_EMISSION");
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                n++;
            }

            // Particles: lit, vertex-coloured, soft alpha for dust and chips.
            var p = Get("M_Particle");
            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (p == null)
            {
                p = new Material(particleShader) { name = "M_Particle" };
                AssetDatabase.CreateAsset(p, $"{Dir}/M_Particle.mat");
            }
            p.shader = particleShader;
            p.SetFloat("_Surface", 1f); // transparent
            p.SetFloat("_Blend", 0f);
            p.SetOverrideTag("RenderType", "Transparent");
            p.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            p.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            p.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            p.SetInt("_ZWrite", 0);
            p.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            p.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(p);

            AssetDatabase.SaveAssets();
            return $"{n + 1} materials";
        }
    }
}
