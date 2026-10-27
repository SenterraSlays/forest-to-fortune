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

        /// <summary>Phones: lighter render scales and MSAA (cheap on tile GPUs, and visible on dense
        /// screens). Tablets are GPU-bound at native resolution (2560x1600 on a Tab S7), so only
        /// Ultra renders at full size; Low aims at 120 FPS and High at 60+ on a recent tablet.</summary>
        static readonly Values[] s_MobileTable =
        {
            new Values { shadows = ShadowQuality.Low, textures = TextureQuality.Half, anisotropic = false, bloom = false, detail = DetailLevel.Low, effects = EffectsQuality.Low, renderScale = 0.5f, aa = AntiAliasingSetting.Off },
            new Values { shadows = ShadowQuality.Medium, textures = TextureQuality.Full, anisotropic = false, bloom = false, detail = DetailLevel.Medium, effects = EffectsQuality.Medium, renderScale = 0.6f, aa = AntiAliasingSetting.MSAA2x },
            new Values { shadows = ShadowQuality.High, textures = TextureQuality.Full, anisotropic = true, bloom = true, detail = DetailLevel.Medium, effects = EffectsQuality.High, renderScale = 0.75f, aa = AntiAliasingSetting.MSAA2x },
            new Values { shadows = ShadowQuality.Ultra, textures = TextureQuality.Full, anisotropic = true, bloom = true, detail = DetailLevel.Ultra, effects = EffectsQuality.High, renderScale = 1f, aa = AntiAliasingSetting.MSAA4x },
        };

        static Values[] Table => UnityEngine.Application.isMobilePlatform ? s_MobileTable : s_Table;

        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };

        public static string[] Descriptions => UnityEngine.Application.isMobilePlatform ? s_MobileDescriptions : s_DesktopDescriptions;

        static readonly string[] s_MobileDescriptions =
        {
            "For older phones, the longest battery life and 120 Hz screens: short hard shadows, half-resolution textures, fewer plants, 50% render scale.",
            "Balanced for most phones: medium-range shadows, full textures, most of the scenery, 60% render scale.",
            "For recent high-end phones and tablets: longer, softer shadows, bloom, 75% render scale.",
            "Everything on: full resolution, the sharpest shadows and every blade of grass. Needs a top phone and runs warm.",
        };

        static readonly string[] s_DesktopDescriptions =
        {
            "For older PCs: hard short-range shadows, half-resolution textures, fewer grass and flowers, 75% render scale.",
            "Balanced: soft shadows at medium range, full textures and most of the scenery.",
            "Recommended: long-range soft shadows, SMAA and nearly all the scenery.",
            "Everything on: the sharpest, farthest shadows and every blade of grass.",
        };

        public static void Apply(SettingsData d, QualityPreset preset)
        {
            if (preset == QualityPreset.Custom) return;
            var v = Table[(int)preset];
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
            var table = Table;
            for (int i = 0; i < table.Length; i++)
            {
                var v = table[i];
                if (d.shadows == v.shadows && d.textures == v.textures && d.anisotropic == v.anisotropic && d.bloom == v.bloom
                    && d.detail == v.detail && d.effects == v.effects && UnityEngine.Mathf.Abs(d.renderScale - v.renderScale) < 0.01f && d.antiAliasing == v.aa)
                    return (QualityPreset)i;
            }
            return QualityPreset.Custom;
        }
    }
}
