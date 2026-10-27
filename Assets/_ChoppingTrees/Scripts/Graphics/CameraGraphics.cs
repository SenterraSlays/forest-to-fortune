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
            // Trees past the fog are invisible anyway: skip drawing them (the world is 500 m long).
            var cam = GetComponent<Camera>();
            var cull = new float[32];
            cull[GameLayers.Tree] = 345f;
            cam.layerCullDistances = cull;
            cam.layerCullSpherical = true;
        }
    }
}
