using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using UnityEngine;

namespace ChoppingTrees
{
    public enum CloudState { Unavailable, SignedOut, SigningIn, Syncing, Synced, Offline }

    /// <summary>
    /// Cross-device saves with Unity Cloud Save. The player signs in with a Unity Player Account
    /// (browser sign-in; Google, Apple or email) and save slots A, B and C are kept in their
    /// cloud data as the same JSON the save files hold. Settings stay per device.
    ///
    /// Each slot is compared by a fingerprint of its progress (the save without its timestamp,
    /// so re-saving unchanged progress doesn't count as a change) against the version both sides
    /// last agreed on (<see cref="SettingsData.cloudSynced"/>): whichever side moved on is copied
    /// over, and when both did, the player chooses (<see cref="CloudConflictDialog"/>). Saves
    /// upload shortly after the game saves, when the app goes to the background and on quit.
    /// Offline, the game plays from its local files and catches up when the connection is back.
    /// </summary>
    public class CloudSync : MonoBehaviour
    {
        public static CloudSync Instance { get; private set; }
        /// <summary>Raised whenever the state, account or last sync time changes.</summary>
        public static event Action Changed;

        public CloudState State { get; private set; } = CloudState.SignedOut;
        /// <summary>Email or name of the signed-in account ("" when signed out).</summary>
        public string Account => GameSettings.Data.cloudAccount ?? "";
        public DateTime? LastSync { get; private set; }
        /// <summary>Why the last action failed, or empty.</summary>
        public string Message { get; private set; } = "";
        /// <summary>Signed in to an account (possibly offline right now).</summary>
        public bool SignedIn => !string.IsNullOrEmpty(Account) && State != CloudState.SignedOut && State != CloudState.Unavailable && State != CloudState.SigningIn;

        const float UploadDelay = 45f, RetryDelay = 60f;
        static string Key(int slot) => "slot_" + SaveSystem.SlotNames[slot];

        bool m_Initialized;
        Task<bool> m_Init;
        bool m_Busy;
        readonly bool[] m_Dirty = new bool[3];
        float m_UploadTimer, m_RetryTimer;
        bool AuthReady => m_Initialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>Starts cloud saves once per run (survives scene reloads).</summary>
        public static void Ensure()
        {
            if (Instance != null) return;
            if (Environment.CommandLine.Contains("-ct-smoke") || !Game.SavingEnabled) return;
            var go = new GameObject("CloudSync");
            DontDestroyOnLoad(go);
            go.AddComponent<CloudSync>();
        }

        /// <summary>True when this slot's progress on this device is the same as in the cloud.</summary>
        public static bool IsSlotSynced(int slot)
        {
            if (Instance == null || !Instance.SignedIn || Instance.m_Dirty[slot]) return false;
            long h = Fingerprint(SaveSystem.ReadRaw(slot));
            return h != 0 && h == Synced()[slot];
        }

        void Awake() => Instance = this;

        async void Start()
        {
            if (!await Init()) return;
            // A cached session signs straight back in (it belongs to the Unity account used last time).
            if (AuthenticationService.Instance.SessionTokenExists && !string.IsNullOrEmpty(Account))
            {
                SetState(CloudState.Syncing);
                await Reconnect();
            }
            else SetState(CloudState.SignedOut);
        }

        Task<bool> Init() => m_Init ??= InitAsync();

        async Task<bool> InitAsync()
        {
            if (!PlayerAccountsConfigured())
            {
                Message = "Cloud saves aren't set up in this build yet.";
                SetState(CloudState.Unavailable);
                return false;
            }
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                PlayerAccountService.Instance.SignedIn += OnAccountSignedIn;
                PlayerAccountService.Instance.SignInFailed += e => Fail(e, CloudState.SignedOut);
                m_Initialized = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Cloud] Unity Services did not start: " + e.Message);
                Message = "Cloud saves are unavailable right now.";
                SetState(CloudState.Unavailable);
                m_Init = null; // try again next time
                return false;
            }
        }

        /// <summary>The Player Accounts settings (Project Settings > Services > Authentication)
        /// are in the build with a client id. The settings class is internal to the package, so
        /// its serialized fields are read through JSON.</summary>
        static bool PlayerAccountsConfigured()
        {
            var settings = Resources.Load<ScriptableObject>("UnityPlayerAccountSettings");
            if (settings == null) return false;
            var json = JsonUtility.ToJson(settings);
            return !json.Contains("\"clientId\":\"\"") && json.Contains("\"clientId\"");
        }

        // ------------------------------------------------------------------ sign in and out

        /// <summary>Opens the browser for Unity Player Account sign-in.</summary>
        public async void SignIn()
        {
            if (State == CloudState.SigningIn) return;
            if (!await Init()) return;
            Message = "";
            SetState(CloudState.SigningIn);
            try
            {
                if (PlayerAccountService.Instance.IsSignedIn) OnAccountSignedIn();
                else await PlayerAccountService.Instance.StartSignInAsync(); // finishes in OnAccountSignedIn
            }
            catch (Exception e)
            {
                Fail(e, CloudState.SignedOut);
            }
        }

        async void OnAccountSignedIn()
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut();
                await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                var claims = PlayerAccountService.Instance.IdTokenClaims;
                string who = claims != null && !string.IsNullOrEmpty(claims.Email) ? claims.Email : AuthenticationService.Instance.PlayerName;
                if (string.IsNullOrEmpty(who)) who = "Unity account";
                // A different account than last time: nothing on this device has been synced with it.
                if (GameSettings.Data.cloudAccount != who) GameSettings.Data.cloudSynced = new long[3];
                GameSettings.Data.cloudAccount = who;
                GameSettings.Save();
                Toasts.Show("Signed in as " + who, Toasts.Kind.Success);
                SetState(CloudState.Syncing);
                await SyncAll();
            }
            catch (Exception e)
            {
                Fail(e, CloudState.SignedOut);
            }
        }

        public void SignOut()
        {
            if (m_Initialized)
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
                if (PlayerAccountService.Instance.IsSignedIn) PlayerAccountService.Instance.SignOut();
            }
            GameSettings.Data.cloudAccount = "";
            GameSettings.Data.cloudSynced = new long[3];
            GameSettings.Save();
            LastSync = null;
            Message = "";
            SetState(CloudState.SignedOut);
        }

        async Task Reconnect()
        {
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await SyncAll();
            }
            catch (Exception e)
            {
                Fail(e, CloudState.Offline);
            }
        }

        // ------------------------------------------------------------------ syncing

        /// <summary>Called after the game writes a slot: uploads it a little later.</summary>
        public static void NotifySaved(int slot)
        {
            if (Instance == null || slot < 0 || slot >= 3) return;
            if (Instance.m_Dirty[slot]) return;
            Instance.m_Dirty[slot] = true;
            Changed?.Invoke();
        }

        /// <summary>A slot was deleted on this device: delete it in the cloud too.</summary>
        public async void DeleteSlot(int slot)
        {
            m_Dirty[slot] = false;
            Synced()[slot] = 0;
            GameSettings.Save();
            if (!AuthReady) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(Key(slot));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Cloud] Could not delete " + Key(slot) + ": " + e.Message);
            }
            Changed?.Invoke();
        }

        void Update()
        {
            if (!m_Initialized || m_Busy) return;
            float dt = Time.unscaledDeltaTime;
            if (State == CloudState.Offline && !string.IsNullOrEmpty(Account))
            {
                m_RetryTimer += dt;
                if (m_RetryTimer >= RetryDelay)
                {
                    m_RetryTimer = 0f;
                    _ = Reconnect();
                }
                return;
            }
            if (!AuthReady || !GameSettings.Data.cloudAutoSync) return;
            if (!m_Dirty[0] && !m_Dirty[1] && !m_Dirty[2]) { m_UploadTimer = 0f; return; }
            m_UploadTimer += dt;
            if (m_UploadTimer >= UploadDelay) _ = SyncDirty();
        }

        void OnApplicationPause(bool paused)
        {
            // The game saves on pause first, then the save goes up.
            if (paused && AuthReady && GameSettings.Data.cloudAutoSync) _ = SyncDirty();
        }

        void OnApplicationQuit()
        {
            if (AuthReady && GameSettings.Data.cloudAutoSync) _ = SyncDirty();
        }

        async Task SyncDirty()
        {
            m_UploadTimer = 0f;
            var slots = new List<int>();
            for (int i = 0; i < 3; i++) if (m_Dirty[i]) slots.Add(i);
            if (slots.Count > 0) await Sync(slots);
        }

        /// <summary>Compares every slot with the cloud and copies whichever side is newer.</summary>
        public Task SyncAll() => Sync(new List<int> { 0, 1, 2 });

        async Task Sync(List<int> slots)
        {
            if (!AuthReady || m_Busy) return;
            m_Busy = true;
            SetState(CloudState.Syncing);
            try
            {
                // While playing, the game in memory is ahead of its file: write it first.
                var game = Game.Instance;
                if (game != null && game.Phase == GamePhase.Playing && slots.Contains(SaveSystem.CurrentSlot)) game.Save();
                var keys = new HashSet<string>();
                foreach (int s in slots) keys.Add(Key(s));
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);
                foreach (int slot in slots)
                {
                    items.TryGetValue(Key(slot), out var item);
                    await Reconcile(slot, item);
                }
                LastSync = DateTime.Now;
                Message = "";
                SetState(CloudState.Synced);
            }
            catch (Exception e)
            {
                Fail(e, CloudState.Offline);
            }
            finally
            {
                m_Busy = false;
            }
        }

        async Task Reconcile(int slot, Item item)
        {
            m_Dirty[slot] = false;
            var synced = Synced();
            string localJson = SaveSystem.ReadRaw(slot);
            string cloudJson = item != null ? item.Value.GetAsString() : null;
            long lh = Fingerprint(localJson), ch = Fingerprint(cloudJson);

            if (lh == ch) { synced[slot] = lh; GameSettings.Save(); return; }
            if (ch == 0) { await Upload(slot, localJson, lh); return; }
            if (lh == 0) { Download(slot, cloudJson, ch); return; }
            if (lh == synced[slot]) { Download(slot, cloudJson, ch); return; }   // only the cloud moved on
            if (ch == synced[slot]) { await Upload(slot, localJson, lh); return; } // only this device moved on

            // Both changed since they last agreed (played on two devices while apart): ask.
            var choice = await CloudConflictDialog.Ask(slot, Parse(localJson), Parse(cloudJson));
            if (choice == CloudConflictDialog.Choice.Device) await Upload(slot, localJson, lh);
            else if (choice == CloudConflictDialog.Choice.Cloud) Download(slot, cloudJson, ch);
        }

        async Task Upload(int slot, string json, long fingerprint)
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { [Key(slot)] = json });
            Synced()[slot] = fingerprint;
            GameSettings.Save();
            Debug.Log($"[Cloud] Uploaded save {SaveSystem.SlotNames[slot]}");
        }

        void Download(int slot, string json, long fingerprint)
        {
            if (!SaveSystem.WriteRaw(slot, json)) return;
            Synced()[slot] = fingerprint;
            GameSettings.Save();
            Debug.Log($"[Cloud] Downloaded save {SaveSystem.SlotNames[slot]}");
            Toasts.Show($"Save {SaveSystem.SlotNames[slot]} updated from the cloud", Toasts.Kind.Info);
            // The open slot reloads so the game shows the newer progress.
            if (slot == SaveSystem.CurrentSlot && Game.Instance != null) Game.Instance.ReloadSlot();
        }

        static SaveData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d != null) SaveSystem.Migrate(d);
                return d;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A hash of a save's progress without its timestamp (0 = no progress).</summary>
        public static long Fingerprint(string json)
        {
            var d = Parse(json);
            if (d == null || !d.hasPlayed) return 0;
            d.lastSavedUtcTicks = 0;
            string s = JsonUtility.ToJson(d);
            ulong h = 14695981039346656037UL; // FNV-1a
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h == 0 ? 1 : (long)h;
        }

        static long[] Synced()
        {
            var d = GameSettings.Data;
            if (d.cloudSynced == null || d.cloudSynced.Length != 3) d.cloudSynced = new long[3];
            return d.cloudSynced;
        }

        void Fail(Exception e, CloudState state)
        {
            Debug.LogWarning("[Cloud] " + e.GetType().Name + ": " + e.Message);
            m_RetryTimer = 0f;
            Message = e is RequestFailedException r && r.ErrorCode == CommonErrorCodes.TransportError
                ? "No connection. Your saves stay on this device and sync when you're back online."
                : state == CloudState.SignedOut ? "Sign-in didn't finish. Try again." : "Couldn't reach the cloud. Trying again in a minute.";
            SetState(state);
        }

        void SetState(CloudState s)
        {
            State = s;
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
