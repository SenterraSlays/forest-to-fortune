using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ChoppingTrees
{
    /// <summary>
    /// Graphics benchmark for built players (never active in normal play):
    ///   -ct-bench -ct-nosave   (on Android pass them in the intent's "unity" extra, see LaunchArgs)
    /// Starts a game at the spawn and, for each preset plus a few single-change variants of it,
    /// turns the camera through a full circle at 120 FPS max and logs "[Bench]" lines with the
    /// average FPS and CPU / GPU frame times, then quits. The player's settings are not saved.
    /// </summary>
    public class Bench : MonoBehaviour
    {
        const float Settle = 2.5f, Measure = 8f;

        struct Variant
        {
            public string name;
            public QualityPreset preset;
            public Action<SettingsData> tweak;
            public int renderer;
            public bool noPost;
        }

        public static void StartIfRequested()
        {
            if (LaunchArgs.Has("-ct-bench")) new GameObject("Bench").AddComponent<Bench>();
        }

        IEnumerator Start()
        {
            yield return null;
            var game = Game.Instance;
            game.StartPlaying();
            yield return new WaitForSeconds(2f);
            var motor = FindAnyObjectByType<PlayerMotor>();
            var look = motor.GetComponent<PlayerLook>();
            var cam = Camera.main.GetUniversalAdditionalCameraData();
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int rendererCount = asset != null ? asset.rendererDataList.Length : 1;
            Debug.Log($"[Bench] device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} screen={Screen.width}x{Screen.height} renderers={rendererCount}");

            var variants = new[]
            {
                new Variant { name = "Low", preset = QualityPreset.Low },
                new Variant { name = "Medium", preset = QualityPreset.Medium },
                new Variant { name = "High", preset = QualityPreset.High },
                new Variant { name = "Ultra", preset = QualityPreset.Ultra },
                new Variant { name = "High no-shadows", preset = QualityPreset.High, tweak = d => d.shadows = ShadowQuality.Off },
                new Variant { name = "High no-post", preset = QualityPreset.High, noPost = true },
                new Variant { name = "High no-msaa", preset = QualityPreset.High, tweak = d => d.antiAliasing = AntiAliasingSetting.Off },
                new Variant { name = "High scale75", preset = QualityPreset.High, tweak = d => d.renderScale = 0.75f },
                new Variant { name = "High Forward", preset = QualityPreset.High, renderer = 1 },
                new Variant { name = "Low Forward", preset = QualityPreset.Low, renderer = 1 },
                new Variant { name = "Low again", preset = QualityPreset.Low },
            };

            var timings = new FrameTiming[1];
            foreach (var v in variants)
            {
                if (v.renderer >= rendererCount) continue;
                var d = GameSettings.Data;
                QualityPresets.Apply(d, v.preset);
                v.tweak?.Invoke(d);
                d.frameRateCap = 120;
                GameSettings.Apply();
                cam.SetRenderer(v.renderer);
                cam.renderPostProcessing = !v.noPost;

                float yaw = PlayerSpawn.Yaw;
                motor.Teleport(PlayerSpawn.Position + Vector3.up * 0.1f, yaw);
                look.SetPitch(4f);
                yield return new WaitForSeconds(Settle);

                int frames = 0, timed = 0;
                double cpu = 0, render = 0, gpu = 0;
                float t = 0f, worst = 0f;
                while (t < Measure)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime;
                    t += dt;
                    frames++;
                    worst = Mathf.Max(worst, dt);
                    motor.Teleport(motor.transform.position, yaw + 360f * t / Measure);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                    {
                        cpu += timings[0].cpuMainThreadFrameTime;
                        render += timings[0].cpuRenderThreadFrameTime;
                        gpu += timings[0].gpuFrameTime;
                        timed++;
                    }
                }
                var size = GraphicsService.RenderSize();
                string ms = timed > 0 ? $"main={cpu / timed:0.0} render={render / timed:0.0} gpu={gpu / timed:0.0}" : "timings=n/a";
                Debug.Log($"[Bench] {v.name,-16} fps={frames / t:0.0} worst={worst * 1000f:0}ms {ms} size={size.x}x{size.y}");
            }
            cam.SetRenderer(-1);
            cam.renderPostProcessing = true;
            Debug.Log("[Bench] done");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
    }
}
