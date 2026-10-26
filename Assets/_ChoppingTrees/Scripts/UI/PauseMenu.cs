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
            var panel = UIFactory.Panel(Root, "Panel", UIStyle.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 0));
            panel.raycastTarget = true;
            UIFactory.VStack(panel, 12f, new RectOffset(40, 40, 32, 36));
            UIFactory.Fit(panel, false, true);
            UIFactory.Text(panel.transform, "Paused", 44, UIStyle.Text, true, TMPro.TextAlignmentOptions.Center).Layout(-1, 70);
            UIFactory.Button(panel.transform, "Resume", Close, 28, new Color(0.34f, 0.47f, 0.24f)).Layout(-1, 64);
            UIFactory.Button(panel.transform, "Settings", () => SettingsMenu.Instance.Open(), 26).Layout(-1, 58);
            UIFactory.Button(panel.transform, "Save Game", SaveNow, 26).Layout(-1, 58);
            UIFactory.Button(panel.transform, "Reset Progress", ResetProgress, 26).Layout(-1, 58);
            UIFactory.Button(panel.transform, "Main Menu", () => Game.Instance.ReturnToMenu(), 26).Layout(-1, 58);
            UIFactory.Button(panel.transform, "Quit to Desktop", () => Game.Instance.Quit(), 26).Layout(-1, 58);
            Game.Instance.PhaseChanged += _ => Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened() => Time.timeScale = 0f;
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
