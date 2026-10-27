using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChoppingTrees
{
    /// <summary>
    /// Applies the graphics settings: render scale and the upscaler (AMD FSR 1, Unity STP,
    /// NVIDIA DLSS or AMD FSR 2 through URP's upscaler framework, each only when the hardware
    /// check passes) plus camera anti-aliasing, and the quality options: shadows, bloom,
    /// brightness and how much small scenery (grass, flowers, mushrooms, twigs) is shown. If an upscaler throws while rendering, the game
    /// switches back to native rendering by itself. In the editor, changes to the shared URP asset
    /// are undone when play mode ends.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class GraphicsService : MonoBehaviour
    {
        /// <summary>Raised after every apply; cameras re-read their anti-aliasing here.</summary>
        public static event Action Applied;
        public static UpscalerSetting ActiveUpscaler { get; private set; } = UpscalerSetting.Off;
        public static string Notice { get; private set; } = "";

        UniversalRenderPipelineAsset m_Asset;
        bool m_FellBack;
        int m_RetryFrames;
        string m_OrigUpscaler;
        float m_OrigRenderScale, m_OrigShadowDistance;
        int m_OrigShadowRes, m_OrigCascades;
        Bloom m_Bloom;
        ColorAdjustments m_Color;
        float m_BaseExposure;
        readonly System.Collections.Generic.List<GameObject> m_Scenery = new System.Collections.Generic.List<GameObject>();
        DetailLevel m_SceneryLevel = (DetailLevel)(-1);

        void Awake()
        {
            m_Asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (m_Asset != null)
            {
                m_OrigUpscaler = m_Asset.upscalerName;
                m_OrigRenderScale = m_Asset.renderScale;
                m_OrigShadowDistance = m_Asset.shadowDistance;
                m_OrigShadowRes = m_Asset.mainLightShadowmapResolution;
                m_OrigCascades = m_Asset.shadowCascadeCount;
            }
            else Debug.LogWarning("[Graphics] The active render pipeline is not URP; upscaling is unavailable.");
            Debug.Log("[Display] " + DisplayModes.Describe());
            Application.logMessageReceived += OnLog;
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            GameSettings.Changed -= Apply;
#if UNITY_EDITOR && ENABLE_UPSCALER_FRAMEWORK
            if (m_Asset == null) return;
            m_Asset.upscalerName = m_OrigUpscaler;
            m_Asset.renderScale = m_OrigRenderScale;
#endif
#if UNITY_EDITOR
            if (m_Asset == null) return;
            m_Asset.shadowDistance = m_OrigShadowDistance;
            m_Asset.mainLightShadowmapResolution = m_OrigShadowRes;
            m_Asset.shadowCascadeCount = m_OrigCascades;
#endif
        }

        void Update()
        {
            // URP builds its upscaler registry on the first rendered frame; options are retried briefly.
            if (m_RetryFrames <= 0) return;
            m_RetryFrames--;
            var s = GameSettings.Data;
            if (ActiveUpscaler == UpscalerSetting.DLSS && ConfigureDlss(s.upscaleQuality)) m_RetryFrames = 0;
            else if (ActiveUpscaler == UpscalerSetting.FSR2 && ConfigureFsr2(s.upscaleQuality)) m_RetryFrames = 0;
            else if (m_RetryFrames == 0) Debug.LogWarning($"[Graphics] {ActiveUpscaler} options were not found; it runs with its default quality mode.");
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (m_FellBack || type != LogType.Exception || stack == null) return;
            if (!(stack.Contains("Upscal") || stack.Contains("STP.") || stack.Contains("FSR") || stack.Contains("DLSS"))) return;
            var s = GameSettings.Data;
            if (s.upscaler == UpscalerSetting.Off) return;
            m_FellBack = true;
            Notice = $"{GraphicsCapabilities.Label(ActiveUpscaler)} failed on this PC and was turned off.";
            Debug.LogWarning("[Graphics] " + Notice + " (" + message + ")");
            s.upscaler = UpscalerSetting.Off;
            GameSettings.Save();
            GameSettings.Apply();
        }

        public void Apply()
        {
            ApplyUpscaling(GameSettings.Data);
            ApplyQuality(GameSettings.Data);
            Applied?.Invoke();
        }

        void ApplyQuality(SettingsData s)
        {
            if (m_Asset != null)
            {
                (float dist, int res, int cascades) = s.shadows switch
                {
                    ShadowQuality.Off => (0f, 512, 1),
                    ShadowQuality.Low => (35f, 1024, 1),
                    ShadowQuality.Medium => (60f, 2048, 2),
                    ShadowQuality.High => (90f, 2048, 3),
                    _ => (140f, 4096, 4),
                };
                m_Asset.shadowDistance = dist;
                m_Asset.mainLightShadowmapResolution = res;
                m_Asset.shadowCascadeCount = cascades;
            }
            var sun = RenderSettings.sun;
            if (sun != null)
                sun.shadows = s.shadows == ShadowQuality.Off ? LightShadows.None : s.shadows == ShadowQuality.Low ? LightShadows.Hard : LightShadows.Soft;

            // Post-processing on a runtime copy of the profile (the asset is never changed).
            if (m_Bloom == null && m_Color == null)
            {
                var volume = FindAnyObjectByType<Volume>();
                if (volume != null)
                {
                    var profile = volume.profile;
                    profile.TryGet(out m_Bloom);
                    if (profile.TryGet(out m_Color)) m_BaseExposure = m_Color.postExposure.value;
                }
            }
            if (m_Bloom != null) m_Bloom.active = s.bloom;
            if (m_Color != null) m_Color.postExposure.Override(m_BaseExposure + s.brightness * 1.2f);

            ApplyScenery(s.detail);
        }

        /// <summary>Shows a share of the small props: Low 35%, Medium 60%, High 85%, Ultra all.
        /// The same props are always kept for a level (picked by position), so nothing flickers.</summary>
        void ApplyScenery(DetailLevel level)
        {
            if (level == m_SceneryLevel) return;
            m_SceneryLevel = level;
            if (m_Scenery.Count == 0)
            {
                var props = GameObject.Find("Props");
                if (props == null) return;
                foreach (Transform t in props.transform)
                    if (t.name.StartsWith("Grass") || t.name.StartsWith("Flower") || t.name.StartsWith("Mushrooms") || t.name.StartsWith("Branch"))
                        m_Scenery.Add(t.gameObject);
            }
            float keep = level switch { DetailLevel.Low => 0.35f, DetailLevel.Medium => 0.6f, DetailLevel.High => 0.85f, _ => 1f };
            foreach (var go in m_Scenery)
            {
                var p = go.transform.position;
                float h = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f) * 43758.5453f) % 1f;
                bool on = h < keep;
                if (go.activeSelf != on) go.SetActive(on);
            }
        }

        void ApplyUpscaling(SettingsData s)
        {
            if (m_Asset == null) return;
            UpscalerSetting choice = s.upscaler;
            Notice = m_FellBack ? Notice : "";
            if (!GraphicsCapabilities.IsSupported(choice))
            {
                Notice = $"{GraphicsCapabilities.Label(choice)} is unavailable: {GraphicsCapabilities.UnavailableReason(choice)}";
                choice = UpscalerSetting.Off;
            }
            ActiveUpscaler = choice;
            float scale = Mathf.Clamp(s.renderScale, 0.5f, 1f);
#if ENABLE_UPSCALER_FRAMEWORK
            switch (choice)
            {
                case UpscalerSetting.DLSS:
                    m_Asset.upscalerName = GraphicsCapabilities.DlssName;
                    m_Asset.renderScale = 1f;
                    if (!ConfigureDlss(s.upscaleQuality)) m_RetryFrames = 120;
                    break;
                case UpscalerSetting.FSR2:
                    m_Asset.upscalerName = GraphicsCapabilities.Fsr2Name;
                    m_Asset.renderScale = 1f;
                    if (!ConfigureFsr2(s.upscaleQuality)) m_RetryFrames = 120;
                    break;
                case UpscalerSetting.STP:
                    m_Asset.upscalerName = GraphicsCapabilities.StpName;
                    m_Asset.renderScale = QualityToScale(s.upscaleQuality, scale);
                    break;
                case UpscalerSetting.FSR1:
                    m_Asset.upscalerName = GraphicsCapabilities.Fsr1Name;
                    m_Asset.renderScale = QualityToScale(s.upscaleQuality, scale);
                    break;
                default:
                    m_Asset.upscalerName = GraphicsCapabilities.LinearName;
                    m_Asset.renderScale = scale;
                    break;
            }
#else
            m_Asset.renderScale = scale;
#endif
        }

        /// <summary>STP / FSR 1 render scale from the shared quality ladder; the slider can only lower it.</summary>
        static float QualityToScale(UpscaleQuality q, float custom) => Mathf.Min(custom, q switch
        {
            UpscaleQuality.NativeAA => 1f,
            UpscaleQuality.Quality => 0.67f,
            UpscaleQuality.Balanced => 0.58f,
            _ => 0.5f,
        });

        /// <summary>Camera anti-aliasing for the current settings (temporal upscalers do their own).</summary>
        public static AntialiasingMode CameraAntialiasing()
        {
            if (GraphicsCapabilities.IsTemporal(ActiveUpscaler)) return AntialiasingMode.None;
            return GameSettings.Data.antiAliasing switch
            {
                AntiAliasingSetting.FXAA => AntialiasingMode.FastApproximateAntialiasing,
                AntiAliasingSetting.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                AntiAliasingSetting.TAA => AntialiasingMode.TemporalAntiAliasing,
                _ => AntialiasingMode.None,
            };
        }

        /// <summary>Short label for the FPS counter, e.g. "DLSS Quality" or "Native 100%".</summary>
        public static string Describe()
        {
            var s = GameSettings.Data;
            if (ActiveUpscaler == UpscalerSetting.Off) return $"Native {Mathf.RoundToInt(s.renderScale * 100)}%";
            string q = s.upscaleQuality switch
            {
                UpscaleQuality.NativeAA => ActiveUpscaler == UpscalerSetting.DLSS ? "DLAA" : "Native",
                _ => s.upscaleQuality.ToString(),
            };
            return GraphicsCapabilities.Label(ActiveUpscaler).Replace("NVIDIA ", "").Replace("AMD ", "").Replace("Unity ", "") + " " + q;
        }

        bool ConfigureDlss(UpscaleQuality q)
        {
#if ENABLE_NVIDIA && ENABLE_UPSCALER_FRAMEWORK
            if (!(FindOptions(GraphicsCapabilities.DlssName) is DLSSOptions o)) return false;
            o.resolutionMode = UpscalerResolutionMode.QualityMode;
            o.dlssQualityMode = q switch
            {
                UpscaleQuality.NativeAA => UnityEngine.NVIDIA.DLSSQuality.DLAA,
                UpscaleQuality.Quality => UnityEngine.NVIDIA.DLSSQuality.MaximumQuality,
                UpscaleQuality.Balanced => UnityEngine.NVIDIA.DLSSQuality.Balanced,
                _ => UnityEngine.NVIDIA.DLSSQuality.MaximumPerformance,
            };
            return true;
#else
            return false;
#endif
        }

        bool ConfigureFsr2(UpscaleQuality q)
        {
#if ENABLE_AMD && ENABLE_UPSCALER_FRAMEWORK
            if (!(FindOptions(GraphicsCapabilities.Fsr2Name) is FSR2Options o)) return false;
            o.resolutionMode = UpscalerResolutionMode.QualityMode;
            o.fsr2QualityMode = q switch
            {
                UpscaleQuality.Balanced => UnityEngine.AMD.FSR2Quality.Balanced,
                UpscaleQuality.Performance => UnityEngine.AMD.FSR2Quality.Performance,
                _ => UnityEngine.AMD.FSR2Quality.Quality,
            };
            return true;
#else
            return false;
#endif
        }

#if ENABLE_UPSCALER_FRAMEWORK
        UpscalerOptions FindOptions(string name)
        {
            var fromAsset = m_Asset.GetUpscalerOptions(name);
            if (fromAsset != null) return fromAsset;
            var field = typeof(UniversalRenderPipeline).GetField("upscaling", BindingFlags.Static | BindingFlags.NonPublic);
            if (field?.GetValue(null) is Upscaling upscaling)
            {
                IUpscaler upscaler = upscaling.GetIUpscalerByName(name);
                if (upscaler != null) return upscaling.GetGlobalOptions(upscaler) as UpscalerOptions;
            }
            return null;
        }
#endif
    }
}
