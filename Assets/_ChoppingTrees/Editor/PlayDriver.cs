using UnityEditor;
using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Automated-testing helper: keeps play mode advancing while the editor is in the background
    /// (Unity throttles an inactive editor's player loop). Each editor tick steps one frame.
    /// EditorApplication.Step advances by Time.fixedDeltaTime (captureFramerate alone is ignored),
    /// so both are set to the requested frame rate; tests that vary the frame rate set both too.
    /// Never used by players — only by the test scripts.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayDriver
    {
        public static bool Active { get; private set; }
        static int s_StepsPerTick = 1;
        static float s_PreviousFixed;

        static PlayDriver()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) Enable(false); };
        }

        public static string Enable(bool on, int stepsPerTick = 1, int captureFps = 60)
        {
            s_StepsPerTick = Mathf.Clamp(stepsPerTick, 1, 8);
            if (on == Active) return "unchanged";
            Active = on;
            if (on)
            {
                Time.captureFramerate = captureFps;
                s_PreviousFixed = Time.fixedDeltaTime;
                Time.fixedDeltaTime = 1f / captureFps;
                EditorApplication.update += Tick;
            }
            else
            {
                Time.captureFramerate = 0;
                if (s_PreviousFixed > 0f) Time.fixedDeltaTime = s_PreviousFixed;
                EditorApplication.update -= Tick;
                if (EditorApplication.isPlaying) EditorApplication.isPaused = false;
            }
            return on ? "driving" : "stopped";
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
            for (int i = 0; i < s_StepsPerTick; i++) EditorApplication.Step();
        }
    }
}
