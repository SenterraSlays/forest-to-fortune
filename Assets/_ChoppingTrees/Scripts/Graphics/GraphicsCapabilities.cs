using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChoppingTrees
{
    /// <summary>
    /// What this PC and build can actually do. An upscaler is only offered when its runtime check
    /// passes (plugin loaded and the feature reported available by the driver); nothing is
    /// assumed from the GPU name.
    /// </summary>
    public static class GraphicsCapabilities
    {
        // Names of URP's registered upscalers (UniversalRenderPipelineAsset.upscalerName).
        public const string DlssName = "Deep Learning Super Sampling 4";
        public const string Fsr2Name = "FidelityFX Super Resolution 2";
        public const string StpName = "Spatial-Temporal Post-Processing";
        public const string Fsr1Name = "FidelityFX Super Resolution 1.0";
        public const string LinearName = "Bilinear";

        static bool? s_Dlss, s_Fsr2;

        public static string GpuName => SystemInfo.graphicsDeviceName;

        /// <summary>Which vendor upscaler code paths were compiled into this build (for diagnostics).</summary>
        public static string CompiledPaths
        {
            get
            {
                string s = "";
#if ENABLE_UPSCALER_FRAMEWORK
                s += "framework ";
#endif
#if ENABLE_NVIDIA
                s += "nvidia ";
#endif
#if ENABLE_AMD
                s += "amd ";
#endif
                return s.Trim();
            }
        }
        public static GraphicsDeviceType Api => SystemInfo.graphicsDeviceType;

        public static bool DlssSupported => s_Dlss ??= CheckDlss();
        public static bool Fsr2Supported => s_Fsr2 ??= CheckFsr2();
        public static bool StpSupported => SystemInfo.supportsComputeShaders;

        static bool CheckDlss()
        {
#if ENABLE_NVIDIA && ENABLE_UPSCALER_FRAMEWORK
            try
            {
                if (!UnityEngine.NVIDIA.NVUnityPlugin.IsLoaded()) return false;
                var device = UnityEngine.NVIDIA.GraphicsDevice.device ?? UnityEngine.NVIDIA.GraphicsDevice.CreateGraphicsDevice();
                return device != null && device.IsFeatureAvailable(UnityEngine.NVIDIA.GraphicsDeviceFeature.DLSS);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] DLSS capability check failed: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        static bool CheckFsr2()
        {
#if ENABLE_AMD && ENABLE_UPSCALER_FRAMEWORK
            try
            {
                if (!SystemInfo.supportsComputeShaders) return false;
                if (Api != GraphicsDeviceType.Direct3D12 && Api != GraphicsDeviceType.Direct3D11 && Api != GraphicsDeviceType.Vulkan) return false;
                if (!UnityEngine.AMD.AMDUnityPlugin.IsLoaded()) return false;
                var device = UnityEngine.AMD.GraphicsDevice.device ?? UnityEngine.AMD.GraphicsDevice.CreateGraphicsDevice();
                return device != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] FSR 2 capability check failed: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        public static bool IsSupported(UpscalerSetting u) => u switch
        {
            UpscalerSetting.Off => true,
            UpscalerSetting.FSR1 => true,
            UpscalerSetting.STP => StpSupported,
            UpscalerSetting.DLSS => DlssSupported,
            UpscalerSetting.FSR2 => Fsr2Supported,
            _ => false,
        };

        public static string UnavailableReason(UpscalerSetting u) => u switch
        {
            UpscalerSetting.DLSS => "Needs an NVIDIA RTX GPU with a current driver (DLSS was not reported available).",
            UpscalerSetting.FSR2 => "Needs DirectX 11/12 or Vulkan with compute shaders and the AMD plugin.",
            UpscalerSetting.STP => "Needs compute shader support.",
            _ => "",
        };

        public static bool IsTemporal(UpscalerSetting u) => u == UpscalerSetting.DLSS || u == UpscalerSetting.FSR2 || u == UpscalerSetting.STP;

        public static string Label(UpscalerSetting u) => u switch
        {
            UpscalerSetting.Off => "Off",
            UpscalerSetting.FSR1 => "AMD FSR 1",
            UpscalerSetting.STP => "Unity STP",
            UpscalerSetting.DLSS => "NVIDIA DLSS",
            UpscalerSetting.FSR2 => "AMD FSR 2",
            _ => u.ToString(),
        };
    }
}
