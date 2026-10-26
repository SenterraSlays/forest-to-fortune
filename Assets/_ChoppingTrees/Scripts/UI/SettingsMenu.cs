using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Mouse, audio, display and gameplay settings. Changes apply at once and save on close.</summary>
    public class SettingsMenu : UIScreen
    {
        public static SettingsMenu Instance { get; private set; }

        UIButton m_Display, m_Resolution;
        List<Vector2Int> m_Resolutions;
        int m_ResIndex;

        protected override void Build()
        {
            Instance = this;
            var s = GameSettings.Data;
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var panel = UIFactory.Panel(Root, "Panel", UIStyle.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 0));
            panel.raycastTarget = true;
            UIFactory.VStack(panel, 14f, new RectOffset(40, 40, 32, 32));
            UIFactory.Fit(panel, false, true);
            UIFactory.Text(panel.transform, "Settings", 42, UIStyle.Text, true).Layout(-1, 60);

            Section(panel.transform, "Controls");
            SliderRow(panel.transform, "Mouse sensitivity", 0.1f, 4f, s.mouseSensitivity, v => s.mouseSensitivity = v, v => v.ToString("0.00"));
            ToggleRow(panel.transform, "Invert mouse Y", () => s.invertY, v => s.invertY = v);
            SliderRow(panel.transform, "Field of view", 60f, 100f, s.fieldOfView, v => s.fieldOfView = Mathf.Round(v), v => Mathf.Round(v).ToString("0"));

            Section(panel.transform, "Audio");
            SliderRow(panel.transform, "Master volume", 0f, 1f, s.masterVolume, v => { s.masterVolume = v; AudioListener.volume = v; }, v => Mathf.RoundToInt(v * 100) + "%");

            Section(panel.transform, "Display");
            var displayRow = Row(panel.transform, "Display mode");
            m_Display = UIFactory.Button(displayRow, GameSettings.DisplayModeNames[Mathf.Clamp(s.displayMode, 0, 2)], CycleDisplay, 22);
            m_Display.Layout(300, 48);
            m_Resolutions = GameSettings.Resolutions();
            m_ResIndex = m_Resolutions.FindIndex(r => r.x == (s.resolutionWidth > 0 ? s.resolutionWidth : Screen.currentResolution.width) && r.y == (s.resolutionHeight > 0 ? s.resolutionHeight : Screen.currentResolution.height));
            if (m_ResIndex < 0) m_ResIndex = m_Resolutions.Count - 1;
            var resRow = Row(panel.transform, "Resolution");
            m_Resolution = UIFactory.Button(resRow, ResText(), CycleResolution, 22);
            m_Resolution.Layout(300, 48);
            ToggleRow(panel.transform, "V-Sync", () => s.vSync, v => { s.vSync = v; GameSettings.Apply(); });

            Section(panel.transform, "Gameplay");
            ToggleRow(panel.transform, "Tutorial hints", () => s.showTutorial, v => s.showTutorial = v);

            var back = UIFactory.Button(panel.transform, "Back", Close, 26);
            back.Layout(-1, 60);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        string ResText() => m_Resolutions.Count > 0 ? $"{m_Resolutions[m_ResIndex].x} x {m_Resolutions[m_ResIndex].y}" : "Default";

        void CycleDisplay()
        {
            var s = GameSettings.Data;
            s.displayMode = (s.displayMode + 1) % GameSettings.DisplayModes.Length;
            m_Display.Label.text = GameSettings.DisplayModeNames[s.displayMode];
            GameSettings.Apply();
        }

        void CycleResolution()
        {
            if (m_Resolutions.Count == 0) return;
            m_ResIndex = (m_ResIndex + 1) % m_Resolutions.Count;
            var s = GameSettings.Data;
            s.resolutionWidth = m_Resolutions[m_ResIndex].x;
            s.resolutionHeight = m_Resolutions[m_ResIndex].y;
            m_Resolution.Label.text = ResText();
            GameSettings.Apply();
        }

        protected override void OnClosed()
        {
            GameSettings.Apply();
            GameSettings.Save();
        }

        static void Section(Transform parent, string title)
        {
            var t = UIFactory.Text(parent, title.ToUpperInvariant(), 18, UIStyle.Accent, true);
            t.characterSpacing = 6f;
            t.Layout(-1, 34);
        }

        static RectTransform Row(Transform parent, string label)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.HStack(row, 16f);
            row.Layout(-1, 50);
            UIFactory.Text(row, label, 23, UIStyle.Text).Layout(-1, -1, 1);
            return row;
        }

        static void SliderRow(Transform parent, string label, float min, float max, float value, System.Action<float> set, System.Func<float, string> format)
        {
            var row = Row(parent, label);
            TextMeshProUGUI text = null;
            var slider = UIFactory.Slider(row, min, max, value, v => { set(v); text.text = format(v); });
            slider.Layout(240, 30);
            text = UIFactory.Text(row, format(value), 21, UIStyle.Muted, false, TextAlignmentOptions.Right);
            text.Layout(70);
        }

        static void ToggleRow(Transform parent, string label, System.Func<bool> get, System.Action<bool> set)
        {
            var row = Row(parent, label);
            UIFactory.Toggle(row, get, set).Layout(140, 46);
        }
    }
}
