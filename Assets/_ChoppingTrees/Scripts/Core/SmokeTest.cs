using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Automated play-through for built players (never active in normal play):
    ///   Game.exe -ct-smoke [-ct-quit-after 90] [-ct-screenshot path.png] -ct-nosave -logFile log.txt
    /// Starts a game, walks up to trees and chops them with the real axe code, collects logs,
    /// sells at the market, buys upgrades and machines, then logs a "[Smoke]" summary
    /// (trees, money, purchases, frame rate, errors) and quits.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        int m_Errors;
        float m_Quit = 90f;
        string m_Shot;
        int m_Frames;
        float m_FpsTime, m_WorstFrame;
        int m_Purchases;
        UpscalerSetting? m_Upscaler;

        public static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-ct-smoke") < 0) return;
            var go = new GameObject("SmokeTest");
            var s = go.AddComponent<SmokeTest>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ct-quit-after" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var q)) s.m_Quit = q;
                if (args[i] == "-ct-screenshot") s.m_Shot = args[i + 1];
                if (args[i] == "-ct-upscaler" && Enum.TryParse(args[i + 1], true, out UpscalerSetting u)) s.m_Upscaler = u;
            }
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) m_Errors++;
        }

        IEnumerator Start()
        {
            Debug.Log("[Smoke] start");
            yield return null;
            GameSettings.Data.fpsCounter = FpsCounterMode.Detailed;
            if (m_Upscaler.HasValue)
            {
                GameSettings.Data.upscaler = m_Upscaler.Value;
                GameSettings.Data.upscaleQuality = UpscaleQuality.Quality;
                GameSettings.Apply();
            }
            var game = Game.Instance;
            game.StartPlaying();
            var motor = FindAnyObjectByType<PlayerMotor>();
            var look = motor.GetComponent<PlayerLook>();
            var axe = motor.GetComponent<AxeController>();
            var model = game.Model;
            float start = Time.realtimeSinceStartup;
            model.Purchased += _ => m_Purchases++;

            while (Time.realtimeSinceStartup - start < m_Quit - 8f)
            {
                var tree = NearestChoppable(motor.transform.position, model.Upgrades.AxeTier);
                if (tree == null) { yield return new WaitForSeconds(1f); continue; }
                Vector3 tp = tree.transform.position;
                Vector3 dir = motor.transform.position - tp;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
                dir.Normalize();
                motor.Teleport(tp + dir * (tree.Type.trunkRadius * tree.Scale + 1.3f) + Vector3.up * 0.1f, Quaternion.LookRotation(-dir).eulerAngles.y);
                look.SetPitch(18f);
                float t0 = Time.realtimeSinceStartup;
                while (tree.Standing && Time.realtimeSinceStartup - t0 < 30f)
                {
                    axe.DebugSwing();
                    yield return null;
                }
                // Let the tree fall and the logs fly to the player.
                yield return new WaitForSeconds(3.5f);

                if (model.Market.StockValue() > 0 && SellPoint.Instance != null && model.State.TreesFelled % 2 == 0)
                {
                    var sp = SellPoint.Instance.transform;
                    Vector3 front = sp.forward;
                    motor.Teleport(sp.position + front * 2.5f + Vector3.up * 0.1f, Quaternion.LookRotation(-front).eulerAngles.y);
                    look.SetPitch(10f);
                    yield return new WaitForSeconds(0.3f);
                    SellPoint.Instance.Interact();
                    BuyWhatWeCan(model);
                    yield return new WaitForSeconds(0.5f);
                }
            }

            if (!string.IsNullOrEmpty(m_Shot))
            {
                motor.Teleport(PlayerSpawn.Position + new Vector3(0f, 0f, -10f), 180f);
                look.SetPitch(10f);
                yield return new WaitForSeconds(3f);
                ScreenCapture.CaptureScreenshot(m_Shot);
                yield return new WaitForSeconds(1f);
                // The skill tree and settings at full resolution, for visual review.
                model.AddMoney(5000);
                if (UpgradeMenu.Instance != null)
                {
                    UpgradeMenu.Instance.Open();
                    yield return new WaitForSeconds(1.5f);
                    ScreenCapture.CaptureScreenshot(m_Shot.Replace(".png", "_tree.png"));
                    yield return new WaitForSeconds(1f);
                    UpgradeMenu.Instance.Close();
                }
                if (SettingsMenu.Instance != null)
                {
                    SettingsMenu.Instance.Open();
                    yield return new WaitForSeconds(1.5f);
                    ScreenCapture.CaptureScreenshot(m_Shot.Replace(".png", "_settings.png"));
                    yield return new WaitForSeconds(1f);
                    SettingsMenu.Instance.Close();
                }
            }

            var s = model.State;
            float fps = m_FpsTime > 0 ? m_Frames / m_FpsTime : 0;
            Debug.Log($"[Smoke] trees={s.TreesFelled} wood={s.TotalWoodChopped:0} money={s.Money:0.0} earned={s.TotalEarned:0.0} purchases={m_Purchases} " +
                      $"axe={model.Upgrades.AxeName} choppers={model.Upgrades.MachineCount("chopper")} woodPerSec={model.Production.GrossRate(GameModel.Wood):0.00} " +
                      $"pickupsActive={WoodPickup.ActiveCount} avgFps={fps:0} worstFrameMs={m_WorstFrame * 1000f:0} errors={m_Errors} " +
                      $"upscaler={GraphicsService.Describe()} gpu={SystemInfo.graphicsDeviceName} notice='{GraphicsService.Notice}' " +
                      $"screen={Screen.width}x{Screen.height} {Screen.fullScreenMode} native={DisplayModes.Native.x}x{DisplayModes.Native.y} resolutions={DisplayModes.Resolutions().Count}");
            Debug.Log(m_Errors == 0 && s.TreesFelled > 0 && s.TotalEarned > 0 ? "[Smoke] PASS" : "[Smoke] FAIL");
            Application.Quit(m_Errors == 0 ? 0 : 1);
        }

        void BuyWhatWeCan(GameModel model)
        {
            string[] order = { "axe_tier", "chopper_count", "axe_sharpen", "wood_price", "axe_speed", "chopper_speed" };
            foreach (var id in order)
            {
                var u = model.Db.Upgrade(id);
                if (u != null && model.Upgrades.Blocker(u) == null) model.TryBuy(u, out _);
            }
        }

        static ChoppableTree NearestChoppable(Vector3 from, int tier)
        {
            ChoppableTree best = null;
            float bd = float.MaxValue;
            foreach (var t in FindObjectsByType<ChoppableTree>())
            {
                if (!t.Standing || t.Type.requiredAxeTier > tier) continue;
                if (t.transform.position.z > 48f) continue; // stay in the starting area
                float d = (t.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < 5f) return;
            m_Frames++;
            m_FpsTime += Time.unscaledDeltaTime;
            m_WorstFrame = Mathf.Max(m_WorstFrame, Time.unscaledDeltaTime);
        }
    }
}
