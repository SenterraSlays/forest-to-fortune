using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Discord Rich Presence over Discord's local IPC pipe (no SDK or native plugin): shows what the
    /// player is doing ("Chopping with the Lumberjack's Axe", money and rebirths, time played this
    /// session) on their Discord profile while Discord is running. Needs a Discord application id
    /// (<see cref="GameConfig.discordApplicationId"/>); does nothing without one, when Discord
    /// isn't running, or when turned off in Settings. All pipe work runs on a background thread.
    /// </summary>
    public class DiscordPresence : MonoBehaviour
    {
        public static DiscordPresence Instance { get; private set; }

        string m_AppId;
        Thread m_Thread;
        volatile bool m_Running;
        readonly object m_Lock = new object();
        string m_Pending, m_Sent;
        long m_Start;
        float m_Refresh;

        /// <summary>True while connected to Discord (shown in Settings).</summary>
        public static bool Connected { get; private set; }

        /// <summary>Starts presence once per game run (survives scene reloads).</summary>
        public static void Ensure(GameDatabase db)
        {
            if (Instance != null || db == null || db.config == null) return;
            if (Environment.CommandLine.Contains("-ct-smoke")) return; // automated runs stay offline
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return; // Discord's pipe is Windows-only here
            var go = new GameObject("DiscordPresence");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<DiscordPresence>();
            p.m_AppId = (db.config.discordApplicationId ?? "").Trim();
        }

        void Awake()
        {
            Instance = this;
            m_Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        void Start()
        {
            if (string.IsNullOrEmpty(m_AppId))
            {
                Debug.Log("[Discord] No application id set (GameConfig.discordApplicationId): Rich Presence is off.");
                return;
            }
            m_Running = true;
            m_Thread = new Thread(Run) { IsBackground = true, Name = "Discord RPC" };
            m_Thread.Start();
        }

        void Update()
        {
            m_Refresh -= Time.unscaledDeltaTime;
            if (m_Refresh > 0f || !m_Running) return;
            m_Refresh = 5f;
            string activity = GameSettings.Data.discordPresence ? BuildActivity() : null;
            lock (m_Lock) m_Pending = activity ?? "null";
        }

        /// <summary>The activity JSON for what the player is doing right now.</summary>
        string BuildActivity()
        {
            var g = Game.Instance;
            if (g == null || g.Model == null) return null;
            var s = g.Model.State;
            string details, state;
            if (g.Phase != GamePhase.Playing) { details = "In the main menu"; state = s.HasPlayed ? $"Save {SaveSystem.SlotNames[SaveSystem.CurrentSlot]}" : "Starting out"; }
            else
            {
                if (UpgradeMenu.Instance != null && UpgradeMenu.Instance.IsOpen) details = "Planning upgrades at the Workbench";
                else if (RebirthMenu.Instance != null && RebirthMenu.Instance.IsOpen) details = "Thinking about a rebirth";
                else details = "Chopping with the " + g.Model.Upgrades.AxeName;
                state = NumberUtil.Money(s.Money) + (s.Rebirths > 0 ? $"  ·  {s.Rebirths} rebirths (x{g.Model.Market.RebirthMultiplier:0.0#})" : "");
            }
            return "{\"details\":" + Json(details) + ",\"state\":" + Json(state) +
                   ",\"timestamps\":{\"start\":" + m_Start + "}" +
                   ",\"assets\":{\"large_image\":\"icon\",\"large_text\":\"Forest to Fortune\"}}";
        }

        static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // ------------------------------------------------------------------ pipe thread

        void Run()
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            while (m_Running)
            {
                NamedPipeClientStream pipe = null;
                try
                {
                    pipe = Connect();
                    if (pipe == null) { Sleep(15000); continue; }
                    Write(pipe, 0, "{\"v\":1,\"client_id\":" + Json(m_AppId) + "}");
                    Read(pipe); // READY
                    Connected = true;
                    m_Sent = null;
                    while (m_Running && pipe.IsConnected)
                    {
                        string next;
                        lock (m_Lock) next = m_Pending;
                        if (next != null && next != m_Sent)
                        {
                            Write(pipe, 1, "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + pid + ",\"activity\":" + next + "},\"nonce\":\"" + Guid.NewGuid() + "\"}");
                            Read(pipe);
                            m_Sent = next;
                        }
                        Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    if (m_Running) Debug.Log("[Discord] Connection closed: " + e.Message);
                }
                finally
                {
                    Connected = false;
                    pipe?.Dispose();
                }
                Sleep(15000);
            }
        }

        static NamedPipeClientStream Connect()
        {
            for (int i = 0; i < 10; i++)
            {
                var p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
                try
                {
                    p.Connect(150);
                    return p;
                }
                catch
                {
                    p.Dispose();
                }
            }
            return null;
        }

        /// <summary>Frame: opcode and length (little-endian int32 each), then UTF-8 JSON.</summary>
        static void Write(Stream s, int op, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            var frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            s.Write(frame, 0, frame.Length);
            s.Flush();
        }

        static string Read(Stream s)
        {
            var head = new byte[8];
            ReadExactly(s, head);
            int len = BitConverter.ToInt32(head, 4);
            if (len < 0 || len > 1 << 20) throw new IOException("bad frame");
            var body = new byte[len];
            ReadExactly(s, body);
            return Encoding.UTF8.GetString(body);
        }

        static void ReadExactly(Stream s, byte[] buf)
        {
            int got = 0;
            while (got < buf.Length)
            {
                int n = s.Read(buf, got, buf.Length - got);
                if (n <= 0) throw new IOException("pipe closed");
                got += n;
            }
        }

        void Sleep(int ms)
        {
            for (int t = 0; t < ms && m_Running; t += 100) Thread.Sleep(100);
        }

        void OnDestroy()
        {
            m_Running = false;
            if (Instance == this) Instance = null;
        }
    }
}
