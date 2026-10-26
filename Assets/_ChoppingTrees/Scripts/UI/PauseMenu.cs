using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Esc menu: resume, settings, save, reset, main menu, quit. Pauses the game while open.</summary>
    public class PauseMenu : UIScreen
    {
        public static PauseMenu Instance { get; private set; }

        protected override void Build()
        {
            Instance = this;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            UIFactory.Window(Root, "Paused", "clock", new Vector2(500, 566), out var body);
            UIFactory.VStack(body, 12f, new RectOffset(36, 36, 26, 32));
            UIFactory.Button(body, "Resume", Close, 26, ButtonStyle.Primary, "play").Layout(-1, 64);
            UIFactory.Button(body, "Settings", () => SettingsMenu.Instance.Open(), 22, ButtonStyle.Ghost, "gear").Layout(-1, 56);
            UIFactory.Button(body, "Save Game", SaveNow, 22, ButtonStyle.Ghost, "save").Layout(-1, 56);
            UIFactory.Button(body, "Main Menu", () => Game.Instance.ReturnToMenu(), 22, ButtonStyle.Ghost, "leaf").Layout(-1, 56);
            UIFactory.Button(body, "Reset Progress", ResetProgress, 22, ButtonStyle.Danger, "refresh").Layout(-1, 56);
            UIFactory.Button(body, "Quit to Desktop", () => Game.Instance.Quit(), 22, ButtonStyle.Ghost, "power").Layout(-1, 56);
            Game.Instance.PhaseChanged += _ => Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            Time.timeScale = 0f;
            AudioManager.Ui("menu_open", 0.5f);
        }

        protected override void OnClosed() => Time.timeScale = 1f;

        void SaveNow()
        {
            if (Game.Instance.Save()) Toasts.Show("Game saved", Toasts.Kind.Success);
            else Toasts.Show(Game.SavingEnabled ? "Could not save!" : "Saving is turned off for this session", Toasts.Kind.Warning);
        }

        void ResetProgress()
        {
            ConfirmDialog.Ask("Reset all progress?", "Your money, upgrades, machines and unlocked forests will be deleted. This can't be undone.", "Reset",
                () => Game.Instance.ResetProgress());
        }
    }
}
