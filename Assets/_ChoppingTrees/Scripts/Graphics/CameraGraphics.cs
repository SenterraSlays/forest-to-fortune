using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ChoppingTrees
{
    /// <summary>Keeps a camera's anti-aliasing in step with the graphics settings.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraGraphics : MonoBehaviour
    {
        UniversalAdditionalCameraData m_Data;

        void Awake() => m_Data = GetComponent<Camera>().GetUniversalAdditionalCameraData();

        void OnEnable()
        {
            GraphicsService.Applied += Apply;
            Apply();
        }

        void OnDisable() => GraphicsService.Applied -= Apply;

        void Apply()
        {
            if (m_Data == null) return;
            var mode = GraphicsService.CameraAntialiasing();
            m_Data.antialiasing = mode;
            if (mode == AntialiasingMode.SubpixelMorphologicalAntiAliasing) m_Data.antialiasingQuality = AntialiasingQuality.High;
            // Trees, props and scenery past their draw distance (inside the fog) aren't drawn at all.
            var cam = GetComponent<Camera>();
            // Phones draw with plain Forward (renderer 1 of Mobile_RPAsset): with one sun and a few
            // lights, Forward+'s light clustering is pure GPU overhead on a tile GPU.
            if (Application.isMobilePlatform && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1)
                m_Data.SetRenderer(1);
            // (layerCullSpherical is built-in-renderer only; URP warns on every apply if it's set.)
            cam.layerCullDistances = GraphicsService.LayerCullDistances();
        }
    }
}
