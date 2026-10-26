using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Controls, audio, display, graphics (upscaler, quality, render scale, anti-aliasing) and
    /// gameplay settings. Changes apply at once and save on close. Upscalers the hardware check
    /// rejects are skipped when cycling and listed with the reason.
    /// </summary>
    public class SettingsMenu : UIScreen
    {
        public static SettingsMenu Instance { get; private set; }

        List<Vector2Int> m_Resolutions;
        int m_ResIndex;
        TextMeshProUGUI m_UpscalerNote, m_Quality, m_Scale, m_Aa;
        /// <summary>Every selector label with its source, refreshed whenever the menu opens or a value changes.</summary>
        static readonly List<(TextMeshProUGUI label, Func<string> get)> s_Cyclers = new List<(TextMeshProUGUI, Func<string>)>();
        static SettingsData S => GameSettings.Data;

        protected override void Build()
        {
            Instance = this;
            s_Cyclers.Clear();
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            UIFactory.Window(Root, "Settings", "gear", new Vector2(1320, 900), out var body);

            var cols = UIFactory.Rect("Columns", body);
            cols.Stretch(32, 32, 24, 104);
            var h = UIFactory.HStack(cols, 24f);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            var left = Column(cols);
            var right = Column(cols);

            var controls = Section(left, "Controls", "axe");
            SliderRow(controls, "Mouse sensitivity", 0.1f, 4f, S.mouseSensitivity, v => S.mouseSensitivity = v, v => v.ToString("0.00"));
            ToggleRow(controls, "Invert mouse Y", () => S.invertY, v => S.invertY = v);
            SliderRow(controls, "Field of view", 60f, 100f, S.fieldOfView, v => S.fieldOfView = Mathf.Round(v), v => Mathf.Round(v).ToString("0"));

            var audio = Section(left, "Audio", "bolt");
            SliderRow(audio, "Master volume", 0f, 1f, S.masterVolume, v => { S.masterVolume = v; AudioListener.volume = v; }, v => Mathf.RoundToInt(v * 100) + "%");

            var gameplay = Section(left, "Interface", "leaf");
            ToggleRow(gameplay, "Tutorial hints", () => S.showTutorial, v => S.showTutorial = v);
            CyclerRow(gameplay, "FPS counter (F3)", () => S.fpsCounter switch { FpsCounterMode.Fps => "FPS", FpsCounterMode.Detailed => "Detailed", _ => "Off" },
                d => S.fpsCounter = (FpsCounterMode)Cycle((int)S.fpsCounter, d, 3));

            var display = Section(right, "Display", "chart");
            CyclerRow(display, "Display mode", () => GameSettings.DisplayModeNames[Mathf.Clamp(S.displayMode, 0, 2)],
                d => { S.displayMode = Cycle(S.displayMode, d, GameSettings.DisplayModes.Length); GameSettings.Apply(); });
            m_Resolutions = GameSettings.Resolutions();
            m_ResIndex = m_Resolutions.FindIndex(r => r.x == (S.resolutionWidth > 0 ? S.resolutionWidth : Screen.currentResolution.width) && r.y == (S.resolutionHeight > 0 ? S.resolutionHeight : Screen.currentResolution.height));
            if (m_ResIndex < 0) m_ResIndex = m_Resolutions.Count - 1;
            CyclerRow(display, "Resolution", ResText, d =>
            {
                if (m_Resolutions.Count == 0) return;
                m_ResIndex = Cycle(m_ResIndex, d, m_Resolutions.Count);
                S.resolutionWidth = m_Resolutions[m_ResIndex].x;
                S.resolutionHeight = m_Resolutions[m_ResIndex].y;
                GameSettings.Apply();
            });
            ToggleRow(display, "V-Sync", () => S.vSync, v => { S.vSync = v; GameSettings.Apply(); });
            CyclerRow(display, "Frame rate cap", () => S.frameRateCap <= 0 ? "Unlimited" : S.frameRateCap.ToString(), d =>
            {
                int[] caps = { 0, 30, 60, 120, 144, 165, 240 };
                int i = Array.IndexOf(caps, S.frameRateCap);
                S.frameRateCap = caps[Cycle(i < 0 ? 0 : i, d, caps.Length)];
                GameSettings.Apply();
            });

            var gfx = Section(right, "Graphics", "star");
            CyclerRow(gfx, "Upscaler", () => GraphicsCapabilities.Label(S.upscaler), StepUpscaler);
            m_Quality = CyclerRow(gfx, "Upscaler quality", QualityText, d => { S.upscaleQuality = (UpscaleQuality)Cycle((int)S.upscaleQuality, d, 4); GameSettings.Apply(); RefreshLabels(); });
            m_Scale = CyclerRow(gfx, "Render scale", () => Mathf.RoundToInt(S.renderScale * 100) + "%", d =>
            {
                float[] steps = { 0.5f, 0.6f, 0.67f, 0.75f, 0.85f, 1f };
                int i = Array.FindIndex(steps, x => Mathf.Abs(x - S.renderScale) < 0.01f);
                S.renderScale = steps[Cycle(i < 0 ? steps.Length - 1 : i, d, steps.Length)];
                GameSettings.Apply();
            });
            m_Aa = CyclerRow(gfx, "Anti-aliasing", AaText, d => { S.antiAliasing = (AntiAliasingSetting)Cycle((int)S.antiAliasing, d, 4); GameSettings.Apply(); });
            m_UpscalerNote = UIFactory.Text(gfx, "", 16, UIStyle.Muted);
            m_UpscalerNote.Layout(-1, 66);

            var back = UIFactory.Button(body, "Done", Close, 24, ButtonStyle.Primary, "check");
            ((RectTransform)back.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(320, 60));
            RefreshLabels();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            transform.SetAsLastSibling();
            RefreshLabels();
        }

        protected override void OnClosed()
        {
            GameSettings.Apply();
            GameSettings.Save();
        }

        static int Cycle(int value, int dir, int count) => ((value + dir) % count + count) % count;

        string ResText() => m_Resolutions.Count > 0 ? $"{m_Resolutions[m_ResIndex].x} x {m_Resolutions[m_ResIndex].y}" : "Default";

        static string QualityText() => S.upscaler == UpscalerSetting.Off ? "-" : S.upscaleQuality switch
        {
            UpscaleQuality.NativeAA => S.upscaler == UpscalerSetting.DLSS ? "DLAA (native)" : "Native",
            _ => S.upscaleQuality.ToString(),
        };

        static string AaText() => GraphicsCapabilities.IsTemporal(GraphicsService.ActiveUpscaler) ? "By upscaler" : S.antiAliasing.ToString();

        void StepUpscaler(int d)
        {
            var values = (UpscalerSetting[])Enum.GetValues(typeof(UpscalerSetting));
            int i = Array.IndexOf(values, S.upscaler);
            // Skip upscalers this PC can't run.
            for (int k = 0; k < values.Length; k++)
            {
                i = Cycle(i, d, values.Length);
                if (GraphicsCapabilities.IsSupported(values[i])) break;
            }
            S.upscaler = values[i];
            GameSettings.Apply();
            RefreshLabels();
        }

        void RefreshLabels()
        {
            foreach (var (label, get) in s_Cyclers) if (label != null) label.text = get();
            if (m_UpscalerNote == null) return;
            var missing = new List<string>();
            foreach (UpscalerSetting u in Enum.GetValues(typeof(UpscalerSetting)))
                if (!GraphicsCapabilities.IsSupported(u)) missing.Add(GraphicsCapabilities.Label(u));
            string active = $"Running: <color=#F5B545>{GraphicsService.Describe()}</color>.";
            string note = string.IsNullOrEmpty(GraphicsService.Notice) ? "" : "\n<color=#E8705F>" + GraphicsService.Notice + "</color>";
            m_UpscalerNote.text = (missing.Count == 0 ? active + " All upscalers are available on this PC." : active + " Not available on this PC: " + string.Join(", ", missing) + ".") + note;
        }

        static RectTransform Column(Transform parent)
        {
            var col = UIFactory.Rect("Column", parent);
            UIFactory.VStack(col, 18f);
            return col;
        }

        static Transform Section(Transform parent, string title, string icon)
        {
            var card = UIFactory.Surface(parent, title, UIStyle.Card);
            UIFactory.VStack(card, 8f, new RectOffset(22, 22, 16, 18));
            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 10f);
            UIFactory.Icon(head, icon, UIStyle.Accent, 20f);
            UIFactory.Caption(head, title, UIStyle.Accent, 16f);
            head.Layout(-1, 30);
            return card.transform;
        }

        static RectTransform Row(Transform parent, string label)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.HStack(row, 16f);
            row.Layout(-1, 50);
            var t = UIFactory.Text(row, label, 21, UIStyle.Text);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.Layout(-1, -1, 1);
            return row;
        }

        static void SliderRow(Transform parent, string label, float min, float max, float value, Action<float> set, Func<float, string> format)
        {
            var row = Row(parent, label);
            TextMeshProUGUI text = null;
            var slider = UIFactory.Slider(row, min, max, value, v => { set(v); text.text = format(v); });
            slider.Layout(220, 30);
            text = UIFactory.Text(row, format(value), 19, UIStyle.Muted, true, TextAlignmentOptions.Right);
            text.Layout(64);
        }

        static void ToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = Row(parent, label);
            UIFactory.Toggle(row, get, set).Layout(130, 42);
        }

        static TextMeshProUGUI CyclerRow(Transform parent, string label, Func<string> get, Action<int> step)
        {
            var row = Row(parent, label);
            var text = UIFactory.Cycler(row, get, d => { step(d); Instance?.RefreshLabels(); }, 280f);
            s_Cyclers.Add((text, get));
            return text;
        }
    }
}
