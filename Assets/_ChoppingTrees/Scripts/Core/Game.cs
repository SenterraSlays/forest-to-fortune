using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChoppingTrees
{
    public enum GamePhase
    {
        MainMenu,
        Playing,
    }

    /// <summary>
    /// Scene entry point. Loads data and progress, runs the model, autosaves and switches between
    /// the main menu and play. Lives in the scene (not DontDestroyOnLoad): reloading the scene
    /// builds a fresh instance from the save file, so nothing is ever duplicated.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        [SerializeField] GameDatabase m_Database;

        public GameDatabase Db => m_Database;
        public GameModel Model { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
        public GameModel.OfflineReport PendingOffline { get; private set; }
        public string LoadProblem { get; private set; }
        public bool HadSave { get; private set; }

        /// <summary>Tests and automation can turn saving off with -ct-nosave.</summary>
        public static bool SavingEnabled = true;

        public event Action<GamePhase> PhaseChanged;
        /// <summary>Raised after every successful save (for the HUD save indicator).</summary>
        public event Action Saved;

        float m_AutosaveTimer;
        bool m_Quitting;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (m_Database == null) m_Database = GameDatabase.Load();
            if (Environment.CommandLine.Contains("-ct-nosave")) SavingEnabled = false;

            GameSettings.Apply();
            // With saving off (automated runs) start from a clean slate instead of the player's save.
            // After a rebirth the reset state is handed over directly (it was saved too, if saving is on).
            string problem = null;
            bool carried = s_CarryState != null;
            GameState state = carried ? s_CarryState : SavingEnabled ? SaveSystem.Load(out problem) : null;
            s_CarryState = null;
            LoadProblem = problem;
            HadSave = state != null;
            Model = new GameModel(m_Database, state);
            if (HadSave && !carried)
            {
                var report = Model.ApplyOffline(DateTime.UtcNow);
                if (report.awaySeconds >= m_Database.config.minOfflineSecondsToReport && report.gained.Count > 0)
                    PendingOffline = report;
            }
            Model.Purchased += _ => Save();
            Model.TreeFelled += _ => { if (Model.State.TreesFelled <= 3) Save(); };
            UIState.Clear();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Model.Tick(Time.deltaTime);
            if (Phase != GamePhase.Playing) return;
            m_AutosaveTimer += Time.unscaledDeltaTime;
            if (m_AutosaveTimer >= m_Database.config.autosaveSeconds) Save();
        }

        public void StartPlaying()
        {
            Model.State.HasPlayed = true;
            SetPhase(GamePhase.Playing);
            Save();
        }

        public void ReturnToMenu()
        {
            Save();
            SetPhase(GamePhase.MainMenu);
        }

        /// <summary>The offline summary is shown once; the UI calls this after displaying it.</summary>
        public void ConsumeOfflineReport() => PendingOffline = null;

        void SetPhase(GamePhase p)
        {
            Phase = p;
            Time.timeScale = 1f;
            UIState.Clear();
            PhaseChanged?.Invoke(p);
            UIState.ApplyCursor();
        }

        public bool Save()
        {
            m_AutosaveTimer = 0f;
            if (!SavingEnabled || Model == null) return false;
            bool ok = SaveSystem.Save(Model.State);
            if (ok) Saved?.Invoke();
            return ok;
        }

        /// <summary>Deletes progress and restarts from a clean scene. Settings are kept.</summary>
        public void ResetProgress()
        {
            if (SavingEnabled) SaveSystem.Delete();
            Time.timeScale = 1f;
            m_Quitting = true; // don't write the old state back on unload
            UIState.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Starts a fresh game from the main menu (used by "New Game").</summary>
        public void NewGame()
        {
            if (SavingEnabled) SaveSystem.Delete();
            m_Quitting = true;
            s_StartImmediately = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static bool s_StartImmediately;
        static GameState s_CarryState;
        static int s_RebornCount;

        /// <summary>
        /// Buys <paramref name="count"/> rebirths, saves, and restarts the run in a fresh scene so
        /// machines, gates and the player are rebuilt from the reset progress.
        /// </summary>
        public bool Rebirth(int count, out string failure)
        {
            if (!Model.TryRebirth(count, out failure)) return false;
            Save();
            s_CarryState = Model.State;
            s_RebornCount = count;
            s_StartImmediately = true;
            m_Quitting = true; // nothing else to write on unload
            Time.timeScale = 1f;
            UIState.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return true;
        }

        void Start()
        {
            SmokeTest.StartIfRequested();
            if (s_StartImmediately)
            {
                s_StartImmediately = false;
                StartPlaying();
                if (s_RebornCount > 0)
                {
                    string times = s_RebornCount > 1 ? $" x{s_RebornCount}" : "";
                    Toasts.Show($"Reborn{times}! All earnings are now x{Model.Market.RebirthMultiplier:0.0#}", Toasts.Kind.Success);
                    AudioManager.Ui("unlock", 0.9f);
                    s_RebornCount = 0;
                }
            }
            else SetPhase(GamePhase.MainMenu);
        }

        public void Quit()
        {
            Save();
            m_Quitting = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !m_Quitting) Save();
        }

        void OnApplicationQuit()
        {
            if (!m_Quitting && (Model.State.HasPlayed || HadSave)) Save();
            m_Quitting = true;
        }
    }
}
