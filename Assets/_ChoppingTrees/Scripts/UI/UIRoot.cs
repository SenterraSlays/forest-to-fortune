using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Creates the whole interface at startup (canvas, event system and every screen) and routes
    /// the Esc key: closes the top-most menu, or opens the pause menu during play.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UIRoot : MonoBehaviour
    {
        [SerializeField] AxeController m_Axe;
        [SerializeField] Interactor m_Interactor;

        Hud m_Hud;
        MainMenu m_Main;
        PauseMenu m_Pause;
        SettingsMenu m_Settings;
        UpgradeMenu m_Upgrades;
        ConfirmDialog m_Confirm;
        OfflinePopup m_Offline;

        void Awake()
        {
            var db = Game.Instance.Db;
            UIStyle.Body = db.bodyFont;
            UIStyle.Bold = db.boldFont != null ? db.boldFont : db.bodyFont;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            m_Hud = Screen<Hud>(root, "HUD");
            m_Hud.Bind(m_Axe, m_Interactor);
            new GameObject("FloatingText", typeof(RectTransform)).AddComponent<FloatingText>().Create(root, canvas);
            new GameObject("Tutorial", typeof(RectTransform)).AddComponent<Tutorial>().Create(root);
            new GameObject("Toasts", typeof(RectTransform)).AddComponent<Toasts>().Create(root);
            m_Upgrades = Screen<UpgradeMenu>(root, "UpgradeMenu");
            m_Main = Screen<MainMenu>(root, "MainMenu");
            m_Pause = Screen<PauseMenu>(root, "PauseMenu");
            m_Offline = Screen<OfflinePopup>(root, "OfflinePopup");
            m_Settings = Screen<SettingsMenu>(root, "SettingsMenu");
            m_Confirm = Screen<ConfirmDialog>(root, "ConfirmDialog");
            // Toasts stay above menus.
            root.Find("Toasts").SetAsLastSibling();

            Game.Instance.PhaseChanged += p =>
            {
                if (p == GamePhase.Playing) m_Hud.Open();
                else m_Hud.Close();
            };
        }

        static T Screen<T>(RectTransform root, string name) where T : UIScreen
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var s = go.AddComponent<T>();
            s.Create(root);
            return s;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (m_Confirm.IsOpen) m_Confirm.Close();
            else if (m_Settings.IsOpen) m_Settings.Close();
            else if (m_Offline.IsOpen) m_Offline.Close();
            else if (m_Upgrades.IsOpen) m_Upgrades.Close();
            else if (m_Pause.IsOpen) m_Pause.Close();
            else if (Game.Instance.Phase == GamePhase.Playing) m_Pause.Open();
        }

        void OnApplicationFocus(bool focus)
        {
            // Re-lock the cursor when coming back to the window.
            if (focus) UIState.ApplyCursor();
        }
    }
}
