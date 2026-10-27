namespace ChoppingTrees
{
    /// <summary>
    /// The Low / Medium / High / Ultra graphics presets. A preset sets shadows, texture quality,
    /// anisotropic filtering, detail density, bloom, effects, render scale and anti-aliasing; the
    /// upscaler stays the player's own choice. Changing any of those afterwards makes it Custom.
    /// </summary>
    public static class QualityPresets
    {
        struct Values
        {
            public ShadowQuality shadows;
            public TextureQuality textures;
            public bool anisotropic, bloom;
            public DetailLevel detail;
            public EffectsQuality effects;
            public float renderScale;
            public AntiAliasingSetting aa;
        }

        static readonly Values[] s_Table =
        {
            new Values { shadows = ShadowQuality.Low, textures = TextureQuality.Half, anisotropic = false, bloom = false, detail = DetailLevel.Low, effects = EffectsQuality.Low, renderScale = 0.75f, aa = AntiAliasingSetting.FXAA },
            new Values { shadows = ShadowQuality.Medium, textures = TextureQuality.Full, anisotropic = true, bloom = true, detail = DetailLevel.Medium, effects = EffectsQuality.Medium, renderScale = 1f, aa = AntiAliasingSetting.FXAA },
            new Values { shadows = ShadowQuality.High, textures = TextureQuality.Full, anisotropic = true, bloom = true, detail = DetailLevel.High, effects = EffectsQuality.High, renderScale = 1f, aa = AntiAliasingSetting.SMAA },
            new Values { shadows = ShadowQuality.Ultra, textures = TextureQuality.Full, anisotropic = true, bloom = true, detail = DetailLevel.Ultra, effects = EffectsQuality.High, renderScale = 1f, aa = AntiAliasingSetting.SMAA },
        };

        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };

        public static readonly string[] Descriptions =
        {
            "For older PCs: hard short-range shadows, half-resolution textures, fewer grass and flowers, 75% render scale.",
            "Balanced: soft shadows at medium range, full textures and most of the scenery.",
            "Recommended: long-range soft shadows, SMAA and nearly all the scenery.",
            "Everything on: the sharpest, farthest shadows and every blade of grass.",
        };

        public static void Apply(SettingsData d, QualityPreset preset)
        {
            if (preset == QualityPreset.Custom) return;
            var v = s_Table[(int)preset];
            d.shadows = v.shadows;
            d.textures = v.textures;
            d.anisotropic = v.anisotropic;
            d.bloom = v.bloom;
            d.detail = v.detail;
            d.effects = v.effects;
            d.renderScale = v.renderScale;
            d.antiAliasing = v.aa;
            d.qualityPreset = preset;
        }

        /// <summary>The preset the current values match, or Custom.</summary>
        public static QualityPreset Detect(SettingsData d)
        {
            for (int i = 0; i < s_Table.Length; i++)
            {
                var v = s_Table[i];
                if (d.shadows == v.shadows && d.textures == v.textures && d.anisotropic == v.anisotropic && d.bloom == v.bloom
                    && d.detail == v.detail && d.effects == v.effects && UnityEngine.Mathf.Abs(d.renderScale - v.renderScale) < 0.01f && d.antiAliasing == v.aa)
                    return (QualityPreset)i;
            }
            return QualityPreset.Custom;
        }
    }
}
