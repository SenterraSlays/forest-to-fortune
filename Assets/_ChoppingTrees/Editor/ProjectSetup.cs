using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Repeatable project configuration (Tools > Chopping Trees > 1. Setup Project): folders,
    /// layers, player settings, URP quality, TextMesh Pro resources and the UI font assets.
    /// Safe to run again.
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/_ChoppingTrees";
        public const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
        public const string FontDir = Root + "/Art/Fonts";

        [MenuItem("Tools/Forest to Fortune/1. Setup Project", priority = 1)]
        public static string Run()
        {
            foreach (var f in new[] { "Art/Materials", "Art/Models", "Art/Textures", "Art/Fonts", "Audio", "Data/Trees", "Data/Upgrades", "Data/Machines", "Data/Areas", "Data/Resources", "Prefabs", "Resources", "Scenes" })
                Directory.CreateDirectory(Path.Combine(Root, f));
            AssetDatabase.Refresh();
            string layers = Layers();
            Player();
            string urp = Urp();
            string tmp = Tmp();
            AssetDatabase.SaveAssets();
            string msg = $"[ProjectSetup] {layers} | {urp} | {tmp}";
            Debug.Log(msg);
            return msg;
        }

        static string Layers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < GameLayers.Names.Length; i++)
                layers.GetArrayElementAtIndex(i).stringValue = GameLayers.Names[i];
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            // The player capsule never collides with pickups.
            Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Pickup, true);
            Physics.queriesHitBackfaces = false;
            return "layers ok";
        }

        public const string IconPath = Root + "/Art/Icon/GameIcon.png";

        /// <summary>Phone and tablet player settings: landscape only, ARM64 with IL2CPP, Vulkan
        /// with OpenGL ES 3 as the fallback, ASTC textures, and the notch area left to the system
        /// so nothing is drawn under a camera cut-out.</summary>
        public static void AndroidSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.hazelwood.foresttofortune");
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            // Vulkan pre-rotation renders straight into the display's portrait buffer; with a render
            // scale below 100% that showed only a square part of the landscape picture on some
            // phones. The system compositor rotates instead (no measurable cost on modern phones).
            PlayerSettings.vulkanEnablePreTransform = false;
            MobilePipeline();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The "Mobile" quality level (Android's default) uses the mobile URP asset and renderer:
        /// no SSAO, no depth or opaque texture copies, per-vertex lantern lights without shadows,
        /// SRP batcher on. The game's graphics settings then tune shadows and resolution on top.
        /// </summary>
        public static void MobilePipeline()
        {
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (mobile == null)
            {
                Debug.LogWarning("[Setup] Assets/Settings/Mobile_RPAsset.asset is missing; Android uses the PC pipeline.");
                return;
            }
            var so = new SerializedObject(mobile);
            void Set(string name, System.Action<SerializedProperty> apply)
            {
                var p = so.FindProperty(name);
                if (p != null) apply(p);
                else Debug.LogWarning("[Setup] Mobile_RPAsset has no " + name);
            }
            Set("m_SupportsHDR", p => p.boolValue = true);
            Set("m_MSAA", p => p.intValue = 1);
            Set("m_RenderScale", p => p.floatValue = 1f);
            Set("m_RequireDepthTexture", p => p.boolValue = false);
            Set("m_RequireOpaqueTexture", p => p.boolValue = false);
            Set("m_AdditionalLightsRenderingMode", p => p.intValue = (int)LightRenderingMode.PerVertex);
            Set("m_AdditionalLightShadowsSupported", p => p.boolValue = false);
            Set("m_MainLightShadowsSupported", p => p.boolValue = true);
            Set("m_SoftShadowsSupported", p => p.boolValue = true);
            Set("m_UseSRPBatcher", p => p.boolValue = true);
            Set("m_SupportsDynamicBatching", p => p.boolValue = false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mobile);

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue != "Mobile") continue;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = mobile;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The game icon (rendered by Blender/icon_render.py, framed by icon_compose.py).</summary>
        public static string Icon()
        {
            var imp = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (imp == null) return "icon missing";
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
            return "icon ok";
        }

        static void Player()
        {
            PlayerSettings.companyName = "Hazelwood";
            PlayerSettings.productName = "Forest to Fortune";
            PlayerSettings.bundleVersion = "2.0.0";
            Icon();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.usePlayerLog = true;
            // CPU/GPU frame times for the detailed FPS counter.
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            // Full domain reload on play so static state (settings, UI state) always starts fresh.
            EditorSettings.enterPlayModeOptionsEnabled = false;
            // URP's upscaler framework (DLSS / FSR 2 / STP options) is compiled only with this define.
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (!defines.Split(';').Contains("ENABLE_UPSCALER_FRAMEWORK"))
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.IsNullOrEmpty(defines) ? "ENABLE_UPSCALER_FRAMEWORK" : defines + ";ENABLE_UPSCALER_FRAMEWORK");
                // Player settings aren't written by SaveAssets alone; without this the define is lost on the next reload.
                EditorApplication.ExecuteMenuItem("File/Save Project");
            }
        }

        static string Urp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null) return "URP asset missing";
            asset.supportsHDR = true;
            // Anti-aliasing is done per camera (SMAA/FXAA/TAA) or by the temporal upscalers.
            asset.msaaSampleCount = 1;
            // Keeps the STP shaders in builds; the graphics settings pick the real upscaler at runtime.
#if ENABLE_UPSCALER_FRAMEWORK
            asset.upscalerName = "Spatial-Temporal Post-Processing";
#endif
            asset.renderScale = 1f;
            asset.shadowDistance = 90f;
            asset.shadowCascadeCount = 3;
            asset.mainLightShadowmapResolution = 2048;
            asset.useSRPBatcher = true;
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
            return "URP ok";
        }

        static string Tmp()
        {
            string result;
            // TMP_Settings.instance throws when the essentials aren't imported yet, so look for the asset directly.
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) result = "TMP ok";
            else
            {
                string package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
                if (package == null) return "TMP essentials package not found";
                AssetDatabase.ImportPackage(package, false);
                result = "TMP essentials imported";
            }
            Font("Inter-Regular");
            Font("Inter-SemiBold");
            return result;
        }

        /// <summary>Dynamic SDF font asset for a TTF in Art/Fonts (created once).</summary>
        public static TMP_FontAsset Font(string name)
        {
            string path = $"{FontDir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf");
            if (ttf == null) return null;
            var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            if (fa.atlasTextures != null)
                foreach (var t in fa.atlasTextures)
                    if (t != null && !AssetDatabase.Contains(t)) { t.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
            if (fa.material != null && !AssetDatabase.Contains(fa.material)) { fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            AssetDatabase.SaveAssets();
            return fa;
        }
    }
}
