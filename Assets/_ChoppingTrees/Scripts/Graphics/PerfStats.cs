using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Measured frame statistics for the FPS counter: average FPS over half a
    /// second, the worst frame in that window, and CPU / GPU times from FrameTimingManager only
    /// when the platform reports them. Nothing is estimated.
    /// </summary>
    public class PerfStats : MonoBehaviour
    {
        public static PerfStats Instance { get; private set; }

        public float Fps { get; private set; }
        public float FrameMs { get; private set; }
        public float WorstFrameMs { get; private set; }
        public float CpuMs { get; private set; }
        public float GpuMs { get; private set; }
        public bool HasCpuTime { get; private set; }
        public bool HasGpuTime { get; private set; }

        readonly FrameTiming[] m_Timings = new FrameTiming[1];
        float m_Accum, m_Worst;
        int m_Frames;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_Accum += dt;
            m_Frames++;
            m_Worst = Mathf.Max(m_Worst, dt);
            if (m_Accum >= 0.5f)
            {
                Fps = m_Frames / m_Accum;
                FrameMs = 1000f * m_Accum / m_Frames;
                WorstFrameMs = m_Worst * 1000f;
                m_Accum = 0f;
                m_Frames = 0;
                m_Worst = 0f;
            }

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, m_Timings) > 0)
            {
                var t = m_Timings[0];
                HasCpuTime = t.cpuFrameTime > 0.0;
                HasGpuTime = t.gpuFrameTime > 0.0;
                if (HasCpuTime) CpuMs = Mathf.Lerp(CpuMs, (float)t.cpuFrameTime, 0.1f);
                if (HasGpuTime) GpuMs = Mathf.Lerp(GpuMs, (float)t.gpuFrameTime, 0.1f);
            }
        }
    }
}
