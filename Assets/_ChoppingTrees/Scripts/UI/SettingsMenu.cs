using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Settings in five tabs. <b>Graphics</b>: Low / Medium / High / Ultra presets (Custom once
    /// anything is changed by hand), upscaler, render scale, anti-aliasing, shadows, textures,
    /// filtering, scenery detail, effects and bloom. <b>Display</b>: monitor, mode, resolution,
    /// V-Sync, frame cap and brightness. <b>Audio</b>: master and per-group volumes.
    /// <b>Controls</b>: mouse and controller look, vibration, dead zone, field of view and camera
    /// comfort, plus the controller layout. <b>Interface</b>: UI scale, FPS counter, hints and
    /// damage numbers. Changes apply at once and save on close. Upscalers the hardware check
    /// rejects are skipped when cycling and listed with the reason.
    /// </summary>
    public class SettingsMenu : UIScreen, ITabbedScreen
    {
        public static SettingsMenu Instance { get; private set; }

        static readonly string[] TabNames = { "Graphics", "Display", "Audio", "Controls", "Interface", "Account" };
        static readonly string[] TabIcons = { "star", "chart", "bolt", "axe", "leaf", "cloud" };

        TextMeshProUGUI m_CloudStatus, m_CloudDetail;
        UIButton m_CloudSignIn, m_CloudSignOut, m_CloudSync;
        Image m_CloudBadge;

        TextMeshProUGUI m_DisplayNote, m_UpscalerNote, m_PresetNote;
        readonly UIButton[] m_PresetButtons = new UIButton[4];
        readonly List<RectTransform> m_Pages = new List<RectTransform>();
        readonly List<UIButton> m_Tabs = new List<UIButton>();
        readonly List<Image> m_TabBars = new List<Image>();
        int m_Tab;
        UIButton m_Reset, m_Done;

        /// <summary>Every selector, toggle and slider with its source, refreshed whenever the menu opens or a value changes.</summary>
        static readonly List<(TextMeshProUGUI label, Func<string> get)> s_Cyclers = new List<(TextMeshProUGUI, Func<string>)>();
        static readonly List<(UIButton button, Func<bool> get, string on, string off)> s_Toggles = new List<(UIButton, Func<bool>, string, string)>();
        static readonly List<(Slider slider, TextMeshProUGUI text, Func<float> get, Func<float, string> format)> s_Sliders = new List<(Slider, TextMeshProUGUI, Func<float>, Func<float, string>)>();
        static SettingsData S => GameSettings.Data;
        static bool Mobile => Application.isMobilePlatform;

        protected override void Build()
        {
            Instance = this;
            s_Cyclers.Clear();
            s_Toggles.Clear();
            s_Sliders.Clear();
            var backdrop = UIFactory.Panel(Root, "Backdrop", UIStyle.Backdrop, false);
            backdrop.rectTransform.Stretch();
            backdrop.raycastTarget = true;
            var window = UIFactory.Window(Root, "Settings", "gear", new Vector2(1440, 980), out var body, "Graphics, display, audio, controls and interface");
            UIFactory.TopTrim(window.rectTransform, UIStyle.Accent);

            // Tab list on the left.
            var nav = UIFactory.Panel(body, "Nav", new Color(0.075f, 0.085f, 0.07f, 1f));
            nav.rectTransform.anchorMin = new Vector2(0, 0);
            nav.rectTransform.anchorMax = new Vector2(0, 1);
            nav.rectTransform.pivot = new Vector2(0, 0.5f);
            nav.rectTransform.offsetMin = new Vector2(28, 104);
            nav.rectTransform.offsetMax = new Vector2(28 + 250, -20);
            UIFactory.Border(nav.transform, UIStyle.Border);
            UIFactory.VStack(nav, 6f, new RectOffset(10, 10, 12, 12));
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var b = UIFactory.Button(nav.transform, TabNames[i], () => ShowTab(index), 21, ButtonStyle.Tab, TabIcons[i]);
                b.Layout(-1, 54);
                // A controller switches tabs with LB / RB, so the tab list isn't a navigation stop.
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                var content = b.transform.Find("Content").GetComponent<HorizontalLayoutGroup>();
                content.childAlignment = TextAnchor.MiddleLeft;
                content.padding = new RectOffset(14, 0, 0, 0);
                var bar = UIFactory.Panel(b.transform, "Bar", UIStyle.Accent);
                bar.sprite = UIStyle.RoundedSmall;
                bar.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(4, 30));
                m_Tabs.Add(b);
                m_TabBars.Add(bar);
            }

            // Pages on the right.
            var pages = UIFactory.Rect("Pages", body);
            pages.Stretch(28 + 250 + 26, 32, 20, 104);
            BuildGraphics(Page(pages));
            BuildDisplay(Page(pages));
            BuildAudio(Page(pages));
            BuildControls(Page(pages));
            BuildInterface(Page(pages));
            BuildAccount(Page(pages));

            // Footer.
            var reset = m_Reset = UIFactory.Button(body, "Reset to defaults", ResetDefaults, 20, ButtonStyle.Ghost, "refresh");
            ((RectTransform)reset.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28, 28), new Vector2(250, 56));
            var done = m_Done = UIFactory.Button(body, "Done", Close, 24, ButtonStyle.Primary, "check");
            ((RectTransform)done.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32, 28), new Vector2(300, 60));
            ShowTab(0, false);
        }

        RectTransform Page(RectTransform parent)
        {
            var p = UIFactory.Rect("Page", parent);
            p.Stretch();
            m_Pages.Add(p);
            return p;
        }

        // ------------------------------------------------------------------ pages

        void BuildGraphics(RectTransform page)
        {
            UIFactory.VStack(page, 16f);
            var preset = UIFactory.Surface(page, "Preset", UIStyle.Card);
            UIFactory.VStack(preset, 10f, new RectOffset(22, 22, 16, 18));
            var head = UIFactory.Rect("Head", preset.transform);
            UIFactory.HStack(head, 10f);
            UIFactory.Icon(head, "star", UIStyle.Accent, 20f);
            UIFactory.Caption(head, "Quality preset", UIStyle.Accent, 16f);
            var row = UIFactory.Rect("Buttons", preset.transform);
            UIFactory.HStack(row, 8f);
            for (int i = 0; i < 4; i++)
            {
                var p = (QualityPreset)i;
                var b = UIFactory.Button(row, QualityPresets.Names[i], () => SetPreset(p), 21, ButtonStyle.Ghost);
                b.Layout(-1, 50, 1);
                m_PresetButtons[i] = b;
            }
            m_PresetNote = UIFactory.Text(preset.transform, "", 16, UIStyle.Muted);

            var cols = UIFactory.Rect("Columns", page);
            var h = UIFactory.HStack(cols, 18f, null, TextAnchor.UpperLeft);
            h.childForceExpandWidth = true;
            cols.Layout(-1, -1, -1, 1);
            var left = Section(cols, "Resolution and upscaling", "chart");
            CyclerRow(left, "Upscaler", "DLSS / FSR / STP sharpen a lower resolution", () => GraphicsCapabilities.Label(S.upscaler), StepUpscaler);
            CyclerRow(left, "Upscaler quality", "Lower modes render fewer pixels", QualityText, d => { S.upscaleQuality = (UpscaleQuality)Cycle((int)S.upscaleQuality, d, 4); GameSettings.Apply(); });
            CyclerRow(left, "Render scale", "Below 100% is faster but softer", () => Mathf.RoundToInt(S.renderScale * 100) + "%", d =>
            {
                float[] steps = { 0.5f, 0.6f, 0.67f, 0.75f, 0.85f, 1f };
                int i = Array.FindIndex(steps, x => Mathf.Abs(x - S.renderScale) < 0.01f);
                S.renderScale = steps[Cycle(i < 0 ? steps.Length - 1 : i, d, steps.Length)];
                QualityChanged();
            });
            CyclerRow(left, "Anti-aliasing", "Smooths jagged edges", AaText, d => { S.antiAliasing = (AntiAliasingSetting)Cycle((int)S.antiAliasing, d, 4); QualityChanged(); });
            m_UpscalerNote = UIFactory.Text(left, "", 15, UIStyle.Muted);

            var right = Section(cols, "Detail", "pine");
            CyclerRow(right, "Shadows", "Range and sharpness of shadows", () => S.shadows.ToString(), d => { S.shadows = (ShadowQuality)Cycle((int)S.shadows, d, 5); QualityChanged(); });
            CyclerRow(right, "Textures", "Lower saves video memory", () => S.textures switch { TextureQuality.Full => "Full", TextureQuality.Half => "Half", _ => "Quarter" },
                d => { S.textures = (TextureQuality)Cycle((int)S.textures, d, 3); QualityChanged(); });
            ToggleRow(right, "Anisotropic filtering", "Sharper ground at an angle", () => S.anisotropic, v => { S.anisotropic = v; QualityChanged(); });
            CyclerRow(right, "Scenery detail", "Grass, flowers and small props", () => S.detail.ToString(), d => { S.detail = (DetailLevel)Cycle((int)S.detail, d, 4); QualityChanged(); });
            CyclerRow(right, "Effects", "Wood chips, leaves and dust", () => S.effects.ToString(), d => { S.effects = (EffectsQuality)Cycle((int)S.effects, d, 3); QualityChanged(); });
            ToggleRow(right, "Bloom", "Soft glow around bright lights", () => S.bloom, v => { S.bloom = v; QualityChanged(); });
        }

        void BuildDisplay(RectTransform page)
        {
            UIFactory.VStack(page, 16f);
            var display = Section(page, "Display", "chart");
            if (!Mobile)
            {
                CyclerRow(display, "Monitor", "Which screen the game runs on", MonitorText, StepMonitor);
                CyclerRow(display, "Display mode", "Borderless switches apps fastest", () => GameSettings.WindowModeNames[Mathf.Clamp(S.displayMode, 0, 2)],
                    d => { S.displayMode = Cycle(S.displayMode, d, GameSettings.WindowModes.Length); GameSettings.Apply(); });
                CyclerRow(display, "Resolution", "Native is the sharpest", ResText, StepResolution);
                ToggleRow(display, "V-Sync", "No tearing; caps FPS to the refresh rate", () => S.vSync, v => { S.vSync = v; GameSettings.Apply(); });
            }
            m_DisplayNote = UIFactory.Text(display, "", 16, UIStyle.Accent);
            CyclerRow(display, Mobile ? "Frame rate" : "Frame rate cap", Mobile ? "Higher is smoother; lower saves battery" : "Only used with V-Sync off",
                () => S.frameRateCap <= 0 ? "Unlimited" : S.frameRateCap.ToString(), d =>
            {
                int[] caps = Mobile ? new[] { 30, 60, 90, 120 } : new[] { 0, 30, 60, 120, 144, 165, 240 };
                int i = Array.IndexOf(caps, S.frameRateCap);
                S.frameRateCap = caps[Cycle(i < 0 ? 0 : i, d, caps.Length)];
                GameSettings.Apply();
            });
            var picture = Section(page, "Picture", "star");
            SliderRow(picture, "Brightness", "Exposure of the whole image", -1f, 1f, () => S.brightness, v => { S.brightness = v; GameSettings.Apply(); },
                v => (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100) + "%");
        }

        void BuildAudio(RectTransform page)
        {
            UIFactory.VStack(page, 16f);
            var audio = Section(page, "Volume", "bolt");
            string Pct(float v) => Mathf.RoundToInt(v * 100) + "%";
            SliderRow(audio, "Master", "Everything", 0f, 1f, () => S.masterVolume, v => { S.masterVolume = v; AudioListener.volume = v; }, Pct);
            SliderRow(audio, "Effects", "Chopping, falling trees, pickups", 0f, 1f, () => S.effectsVolume, v => { S.effectsVolume = v; GameSettings.Apply(); }, Pct);
            SliderRow(audio, "Ambience and machines", "Wind, birds and running machines", 0f, 1f, () => S.ambienceVolume, v => { S.ambienceVolume = v; GameSettings.Apply(); }, Pct);
            SliderRow(audio, "Interface", "Menus, purchases and alerts", 0f, 1f, () => S.interfaceVolume, v => { S.interfaceVolume = v; GameSettings.Apply(); }, Pct);
        }

        void BuildControls(RectTransform page)
        {
            var h = UIFactory.HStack(page, 18f, null, TextAnchor.UpperLeft);
            h.childForceExpandWidth = true;
            var left = UIFactory.Rect("Left", page);
            UIFactory.VStack(left, 16f);
            if (Mobile || UnityEngine.InputSystem.Touchscreen.current != null)
            {
                var touch = Section(left, "Touch", "hand");
                SliderRow(touch, "Look sensitivity", "How far a swipe turns the view", 0.2f, 3f, () => S.touchLookSensitivity, v => S.touchLookSensitivity = v, v => v.ToString("0.00"));
                ToggleRow(touch, "Invert Y", "Swipe up to look down", () => S.touchInvertY, v => S.touchInvertY = v);
                ToggleRow(touch, "Fixed stick", "Off: the stick appears under your thumb", () => S.touchFixedStick, v => S.touchFixedStick = v);
                SliderRow(touch, "Button size", "Size of the stick and action buttons", 0.75f, 1.35f, () => S.touchButtonScale, v => { S.touchButtonScale = Mathf.Round(v * 20f) / 20f; GameSettings.Apply(); },
                    v => Mathf.RoundToInt(Mathf.Round(v * 20f) * 5f) + "%");
                SliderRow(touch, "Button opacity", "How visible the on-screen controls are", 0.3f, 1f, () => S.touchOpacity, v => S.touchOpacity = v, v => Mathf.RoundToInt(v * 100) + "%");
                ToggleRow(touch, "Vibration", "Buzz on hits, presses and felled trees", () => S.vibration, v => S.vibration = v);
            }
            if (!Mobile)
            {
                var mouse = Section(left, "Mouse", "axe");
                SliderRow(mouse, "Sensitivity", "How fast the mouse turns the view", 0.1f, 4f, () => S.mouseSensitivity, v => S.mouseSensitivity = v, v => v.ToString("0.00"));
                ToggleRow(mouse, "Invert Y", "Push up to look down", () => S.invertY, v => S.invertY = v);
            }
            var pad = Section(left, "Controller", "gear");
            BuildStylePicker(pad);
            SliderRow(pad, "Look sensitivity", "How fast the right stick turns", 0.2f, 3f, () => S.stickSensitivity, v => S.stickSensitivity = v, v => v.ToString("0.00"));
            ToggleRow(pad, "Invert Y", "Push up to look down", () => S.invertStickY, v => S.invertStickY = v);
            ToggleRow(pad, "Vibration", "Rumble on hits and felled trees", () => S.vibration, v => S.vibration = v);
            SliderRow(pad, "Stick dead zone", "Raise if the view drifts", 0.05f, 0.4f, () => S.stickDeadZone, v => S.stickDeadZone = v, v => Mathf.RoundToInt(v * 100) + "%");

            var right = UIFactory.Rect("Right", page);
            UIFactory.VStack(right, 16f);
            var cam = Section(right, "Camera", "leaf");
            SliderRow(cam, "Field of view", "Wider shows more", 60f, 100f, () => S.fieldOfView, v => S.fieldOfView = Mathf.Round(v), v => Mathf.Round(v).ToString("0"));
            ToggleRow(cam, "Head bob", "View sways while walking", () => S.headBob, v => S.headBob = v);
            ToggleRow(cam, "Camera shake", "Kick on hits and falling trees", () => S.cameraShake, v => S.cameraShake = v);
            var layout = Section(right, "Controller layout", "gear");
            foreach (var (actions, text) in LayoutRows)
            {
                var row = UIFactory.Rect(text, layout);
                UIFactory.HStack(row, 6f);
                row.Layout(-1, 38);
                var icons = new List<Image>();
                foreach (var a in actions)
                {
                    // A light plate behind each button icon keeps the dark glyphs readable on the card.
                    var plate = UIFactory.Panel(row, "Plate_" + a, new Color(1f, 0.95f, 0.82f, 0.16f));
                    plate.sprite = UIStyle.RoundedSmall;
                    plate.Layout(38, 38);
                    var img = UIFactory.Panel(plate.transform, "Glyph", Color.white, false);
                    img.preserveAspect = true;
                    img.rectTransform.Stretch(2, 2, 2, 2);
                    icons.Add(img);
                }
                var label = UIFactory.Text(row, text, 17, UIStyle.Text);
                label.Layout(-1, -1, 1);
                label.margin = new Vector4(8, 0, 0, 0);
                m_LayoutRows.Add((icons, actions));
            }
        }

        /// <summary>Controller layout card: the buttons of each action (as icons) and what they do.</summary>
        static readonly (string[] actions, string text)[] LayoutRows =
        {
            (new[] { "move", "sprint" }, "Move  /  click to sprint"),
            (new[] { "look" }, "Look"),
            (new[] { "chop" }, "Chop (hold to keep swinging)"),
            (new[] { "jump" }, "Jump"),
            (new[] { "use" }, "Use: sell, workbench, gates"),
            (new[] { "menu" }, "Pause menu"),
            (new[] { "dpad", "select", "back" }, "Menus: move, select, back"),
            (new[] { "look", "zoomOut", "zoomIn" }, "Skill tree: pan and zoom"),
            (new[] { "hints" }, "Hide tutorial hints"),
        };

        readonly List<(List<Image> icons, string[] actions)> m_LayoutRows = new List<(List<Image>, string[])>();
        readonly UIButton[] m_StyleButtons = new UIButton[3];

        /// <summary>Three cards (Xbox, PlayStation, Switch): which button icons the game shows.</summary>
        void BuildStylePicker(Transform parent)
        {
            var box = UIFactory.Rect("Style", parent);
            UIFactory.VStack(box, 6f, new RectOffset(0, 0, 4, 6));
            var head = UIFactory.Rect("Head", box);
            UIFactory.VStack(head, 0f);
            UIFactory.Text(head, "Controller buttons", 20, UIStyle.Text);
            UIFactory.Text(head, "Which button icons the prompts show", 14, UIStyle.Dim);
            var row = UIFactory.Rect("Cards", box);
            UIFactory.HStack(row, 8f).childForceExpandWidth = true;
            for (int i = 0; i < 3; i++)
            {
                var style = (ControllerStyle)i;
                var b = UIFactory.Button(row, GameInput.StyleNames[i], () => SetStyle(style), 17, ButtonStyle.Ghost);
                b.Layout(-1, 64, 1);
                var content = b.transform.Find("Content");
                var icon = UIFactory.Panel(content, "Controller", Color.white, false);
                icon.sprite = GameInput.ControllerIcon(style);
                icon.preserveAspect = true;
                icon.Layout(38, 38);
                icon.transform.SetAsFirstSibling();
                m_StyleButtons[i] = b;
            }
        }

        void SetStyle(ControllerStyle style)
        {
            S.controllerStyle = style;
            S.controllerStyleChosen = true;
            GameSettings.Apply();
            AudioManager.Ui("ui_click", 0.5f);
            RefreshLabels();
        }

        void BuildInterface(RectTransform page)
        {
            UIFactory.VStack(page, 16f);
            var ui = Section(page, "Interface", "leaf");
            SliderRow(ui, "Interface scale", "Size of menus and the HUD", 0.75f, 1.25f, () => S.uiScale, v => S.uiScale = Mathf.Round(v * 20f) / 20f,
                v => Mathf.RoundToInt(Mathf.Round(v * 20f) * 5f) + "%");
            CyclerRow(ui, "FPS counter (F3)", "Frame rate, frame times and upscaler", () => S.fpsCounter switch { FpsCounterMode.Fps => "FPS", FpsCounterMode.Detailed => "Detailed", _ => "Off" },
                d => S.fpsCounter = (FpsCounterMode)Cycle((int)S.fpsCounter, d, 3));
            ToggleRow(ui, "Tutorial hints", "Step-by-step goals for new players", () => S.showTutorial, v => S.showTutorial = v);
            ToggleRow(ui, "Damage numbers", "Show the damage of each hit", () => S.damageNumbers, v => S.damageNumbers = v);
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                ToggleRow(ui, "Discord status", "Show what you're doing on your Discord profile", () => S.discordPresence, v => S.discordPresence = v);
        }

        void BuildAccount(RectTransform page)
        {
            UIFactory.VStack(page, 16f);
            var card = UIFactory.Surface(page, "Cloud", UIStyle.Card);
            UIFactory.VStack(card, 14f, new RectOffset(26, 26, 22, 24));
            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 16f);
            m_CloudBadge = UIFactory.Panel(head, "Badge", UIStyle.Accent);
            m_CloudBadge.sprite = UIStyle.Circle;
            m_CloudBadge.Layout(66, 66);
            var ic = UIFactory.Icon(m_CloudBadge.transform, "cloud", UIStyle.AccentDark, 40f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            var titles = UIFactory.Rect("Titles", head);
            UIFactory.VStack(titles, 4f);
            titles.Layout(-1, -1, 1);
            UIFactory.Text(titles, "Cloud saves", 30, UIStyle.Text, true);
            m_CloudStatus = UIFactory.Text(titles, "", 19, UIStyle.Muted);
            m_CloudDetail = UIFactory.Text(card.transform, "", 18, UIStyle.Muted);
            var row = UIFactory.Rect("Buttons", card.transform);
            UIFactory.HStack(row, 12f);
            m_CloudSignIn = UIFactory.Button(row, "Sign in with Unity", () => CloudSync.Instance?.SignIn(), 22, ButtonStyle.Primary, "cloud");
            m_CloudSignIn.Layout(330, 58);
            m_CloudSync = UIFactory.Button(row, "Sync now", () => { if (CloudSync.Instance != null) _ = CloudSync.Instance.SyncAll(); }, 21, ButtonStyle.Ghost, "refresh");
            m_CloudSync.Layout(220, 58);
            m_CloudSignOut = UIFactory.Button(row, "Sign out", () => ConfirmDialog.Ask("Sign out?",
                "Your saves stay on this device. Sign in again any time to keep syncing them.", "Sign out", () => CloudSync.Instance?.SignOut()), 21, ButtonStyle.Ghost, "power");
            m_CloudSignOut.Layout(200, 58);

            var opts = Section(page, "Syncing", "save");
            ToggleRow(opts, "Sync automatically", "Upload saves shortly after the game saves", () => S.cloudAutoSync, v => S.cloudAutoSync = v);
            var how = Section(page, "How it works", "leaf");
            UIFactory.Text(how, "Sign in on each device with the same Unity account (Google, Apple or email) and saves A, B and C follow you " +
                "between PC and phone. Settings stay on each device. You can play offline any time: progress uploads when you're back online. " +
                "If a save changed on two devices in the meantime, you choose which one to keep.", 18, UIStyle.Muted);
            CloudSync.Changed += RefreshCloud;
            RefreshCloud();
        }

        void RefreshCloud()
        {
            if (m_CloudStatus == null) return;
            var c = CloudSync.Instance;
            var state = c != null ? c.State : CloudState.Unavailable;
            bool signedIn = c != null && c.SignedIn;
            string account = c != null ? c.Account : "";
            m_CloudStatus.text = state switch
            {
                CloudState.Unavailable => "<color=#E8705F>Not available</color>",
                CloudState.SigningIn => "Signing in... finish in your browser",
                CloudState.Syncing => "Syncing...",
                CloudState.Synced => "<color=#86CC61>Synced</color>  ·  " + account,
                CloudState.Offline => signedIn ? "<color=#E8B04A>Offline</color>  ·  " + account : "<color=#E8B04A>Offline</color>",
                _ => "Not signed in",
            };
            string last = c != null && c.LastSync.HasValue ? "Last synced at " + c.LastSync.Value.ToString("HH:mm") + ". " : "";
            m_CloudDetail.text = c == null ? "Cloud saves are off in automated runs."
                : !string.IsNullOrEmpty(c.Message) ? c.Message
                : signedIn ? last + "Your saves are kept on every device you sign in on."
                : "Sign in to keep saves A, B and C on all your devices, PC and phone.";
            m_CloudSignIn.gameObject.SetActive(!signedIn && state != CloudState.Unavailable);
            m_CloudSignIn.interactable = state != CloudState.SigningIn;
            m_CloudSync.gameObject.SetActive(signedIn);
            m_CloudSync.interactable = state != CloudState.Syncing;
            m_CloudSignOut.gameObject.SetActive(signedIn);
            m_CloudBadge.color = signedIn ? UIStyle.Good : state == CloudState.Unavailable ? new Color(0.36f, 0.37f, 0.34f) : UIStyle.Accent;
        }

        // ------------------------------------------------------------------ behaviour

        void OnDestroy()
        {
            CloudSync.Changed -= RefreshCloud;
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

        /// <summary>A controller starts on the first control of the open tab.</summary>
        public override GameObject DefaultSelection() => m_Pages.Count > 0 ? FirstSelectable(m_Pages[m_Tab]) : base.DefaultSelection();

        protected override void Update()
        {
            base.Update();
            if (!IsOpen || TopModal != this) return;
            int dir = GameInput.TabLeftPressed ? -1 : GameInput.TabRightPressed ? 1 : 0;
            if (dir == 0) return;
            ShowTab(Cycle(m_Tab, dir, m_Pages.Count));
            Focus(DefaultSelection());
        }

        void ShowTab(int index, bool sound = true)
        {
            m_Tab = index;
            WirePage(m_Pages[index]);
            for (int i = 0; i < m_Pages.Count; i++) m_Pages[i].gameObject.SetActive(i == index);
            for (int i = 0; i < m_Tabs.Count; i++)
            {
                bool on = i == index;
                m_Tabs[i].SetColor(on ? new Color(1f, 0.95f, 0.82f, 0.07f) : new Color(0, 0, 0, 0));
                m_Tabs[i].Label.color = on ? UIStyle.Text : UIStyle.Muted;
                m_TabBars[i].gameObject.SetActive(on);
            }
            if (sound) AudioManager.Ui("ui_click", 0.4f);
            RefreshLabels();
        }

        void SetPreset(QualityPreset p)
        {
            QualityPresets.Apply(S, p);
            GameSettings.Apply();
            AudioManager.Ui("ui_click", 0.5f);
            RefreshLabels();
        }

        /// <summary>A quality option changed by hand: the preset becomes whichever one still matches, or Custom.</summary>
        static void QualityChanged()
        {
            S.qualityPreset = QualityPresets.Detect(S);
            GameSettings.Apply();
        }

        void ResetDefaults()
        {
            ConfirmDialog.Ask("Reset settings?", "Every setting goes back to its default (High quality, native resolution, all volumes at full).", "Reset", () =>
            {
                GameSettings.ResetToDefaults();
                RefreshLabels();
            });
        }

        /// <summary>
        /// Controller navigation for a page, in reading order: up / down go from row to row (left
        /// column, then right column, then Reset / Done), left / right move along a row of buttons
        /// (presets, controller styles). Unity's automatic navigation skipped rows between the
        /// offset columns.
        /// </summary>
        void WirePage(RectTransform page)
        {
            var groups = new List<List<Selectable>>();
            Transform lastParent = null;
            foreach (var sel in page.GetComponentsInChildren<Selectable>(true))
            {
                if (sel.navigation.mode == Navigation.Mode.None) continue;
                // Buttons side by side in one row container form a group.
                if (groups.Count > 0 && sel.transform.parent == lastParent && sel is UIButton) groups[groups.Count - 1].Add(sel);
                else groups.Add(new List<Selectable> { sel });
                lastParent = sel.transform.parent;
            }
            groups.Add(new List<Selectable> { m_Reset, m_Done });
            for (int g = 0; g < groups.Count; g++)
                for (int i = 0; i < groups[g].Count; i++)
                {
                    var row = groups[g];
                    var nav = new Navigation { mode = Navigation.Mode.Explicit };
                    nav.selectOnLeft = i > 0 ? row[i - 1] : null;
                    nav.selectOnRight = i < row.Count - 1 ? row[i + 1] : null;
                    if (g > 0) nav.selectOnUp = Pick(groups[g - 1], i, row.Count);
                    if (g < groups.Count - 1)
                    {
                        var next = groups[g + 1];
                        nav.selectOnDown = g + 1 == groups.Count - 1 ? m_Done : Pick(next, i, row.Count);
                    }
                    row[i].navigation = nav;
                }
        }

        /// <summary>The element of a neighbouring row at about the same horizontal place.</summary>
        static Selectable Pick(List<Selectable> row, int index, int fromCount)
        {
            if (row.Count == 1 || fromCount <= 1) return row[0];
            return row[Mathf.Clamp(Mathf.RoundToInt(index * (row.Count - 1f) / (fromCount - 1f)), 0, row.Count - 1)];
        }

        static int Cycle(int value, int dir, int count) => ((value + dir) % count + count) % count;

        static string ResText()
        {
            var r = GameSettings.ResolvedResolution(S);
            return r == DisplayModes.Native ? $"{r.x} x {r.y} (native)" : $"{r.x} x {r.y}";
        }

        static void StepResolution(int d)
        {
            // Built from the monitor's native size, not only the modes the signal chain reports.
            var list = DisplayModes.Resolutions();
            if (list.Count == 0) return;
            int i = list.IndexOf(GameSettings.ResolvedResolution(S));
            var next = list[Cycle(i < 0 ? list.Count - 1 : i, d, list.Count)];
            bool native = next == DisplayModes.Native;
            S.resolutionWidth = native ? 0 : next.x;
            S.resolutionHeight = native ? 0 : next.y;
            GameSettings.Apply();
        }

        static string MonitorText() => DisplayModes.Name(DisplayModes.Current);

        static void StepMonitor(int d)
        {
            var list = DisplayModes.Monitors();
            if (list.Count < 2) return;
            string cur = string.IsNullOrEmpty(S.monitor) ? DisplayModes.Current.name : S.monitor;
            int i = list.FindIndex(m => m.name == cur);
            S.monitor = list[Cycle(i < 0 ? 0 : i, d, list.Count)].name;
            // A new monitor may have a different native size: go back to native there.
            S.resolutionWidth = S.resolutionHeight = 0;
            GameSettings.Apply();
        }

        static string QualityText() => S.upscaler == UpscalerSetting.Off ? "-" : S.upscaleQuality switch
        {
            UpscaleQuality.NativeAA => S.upscaler == UpscalerSetting.DLSS ? "DLAA (native)" : "Native",
            _ => S.upscaleQuality.ToString(),
        };

        static string AaText() => GraphicsCapabilities.IsTemporal(GraphicsService.ActiveUpscaler) ? "By upscaler" : S.antiAliasing.ToString();

        static void StepUpscaler(int d)
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
        }

        void RefreshLabels()
        {
            foreach (var (label, get) in s_Cyclers) if (label != null) label.text = get();
            foreach (var (button, get, on, off) in s_Toggles)
            {
                if (button == null) continue;
                bool v = get();
                button.Label.text = v ? on : off;
                button.SetColor(v ? UIFactory.StyleColor(ButtonStyle.Success) : UIStyle.Button);
            }
            foreach (var (slider, text, get, format) in s_Sliders)
            {
                if (slider == null) continue;
                slider.SetValueWithoutNotify(get());
                text.text = format(get());
            }
            var style = GameInput.Style;
            for (int i = 0; i < m_StyleButtons.Length; i++)
                if (m_StyleButtons[i] != null) m_StyleButtons[i].SetStyle(style == (ControllerStyle)i ? ButtonStyle.Primary : ButtonStyle.Ghost);
            foreach (var (icons, actions) in m_LayoutRows)
                for (int i = 0; i < icons.Count; i++) icons[i].sprite = GameInput.Glyph(style, actions[i]);
            for (int i = 0; i < m_PresetButtons.Length; i++)
                m_PresetButtons[i].SetStyle((int)S.qualityPreset == i ? ButtonStyle.Primary : ButtonStyle.Ghost);
            m_PresetNote.text = S.qualityPreset == QualityPreset.Custom
                ? "<color=#F5B545>Custom</color>: you changed options below. Pick a preset to go back to one."
                : QualityPresets.Descriptions[(int)S.qualityPreset];
            if (m_DisplayNote != null)
            {
                m_DisplayNote.text = GameSettings.DisplayNotice;
                m_DisplayNote.gameObject.SetActive(!string.IsNullOrEmpty(GameSettings.DisplayNotice));
            }
            var missing = new List<string>();
            foreach (UpscalerSetting u in Enum.GetValues(typeof(UpscalerSetting)))
                if (!GraphicsCapabilities.IsSupported(u)) missing.Add(GraphicsCapabilities.Label(u));
            string active = $"Running: <color=#F5B545>{GraphicsService.Describe()}</color>.";
            string note = string.IsNullOrEmpty(GraphicsService.Notice) ? "" : "\n<color=#E8705F>" + GraphicsService.Notice + "</color>";
            m_UpscalerNote.text = (missing.Count == 0 ? active + " All upscalers work on this PC." : active + " Not available here: " + string.Join(", ", missing) + ".") + note;
        }

        // ------------------------------------------------------------------ rows

        static Transform Section(Transform parent, string title, string icon)
        {
            var card = UIFactory.Surface(parent, title, UIStyle.Card);
            UIFactory.VStack(card, 4f, new RectOffset(22, 22, 14, 16));
            var head = UIFactory.Rect("Head", card.transform);
            UIFactory.HStack(head, 10f);
            UIFactory.Icon(head, icon, UIStyle.Accent, 20f);
            UIFactory.Caption(head, title, UIStyle.Accent, 15f);
            head.Layout(-1, 32);
            return card.transform;
        }

        /// <summary>A row: label with a short hint under it on the left, the control on the right.</summary>
        static RectTransform Row(Transform parent, string label, string hint)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.HStack(row, 14f);
            row.Layout(-1, 58);
            var texts = UIFactory.Rect("Texts", row);
            UIFactory.VStack(texts, 0f, null, TextAnchor.MiddleLeft);
            texts.Layout(-1, -1, 1);
            var t = UIFactory.Text(texts, label, 20, UIStyle.Text);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(hint))
            {
                var h = UIFactory.Text(texts, hint, 14, UIStyle.Dim);
                h.textWrappingMode = TextWrappingModes.NoWrap;
                h.overflowMode = TextOverflowModes.Ellipsis;
            }
            return row;
        }

        static void SliderRow(Transform parent, string label, string hint, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
        {
            var row = Row(parent, label, hint);
            TextMeshProUGUI text = null;
            var slider = UIFactory.Slider(row, min, max, get(), v => { set(v); text.text = format(v); });
            slider.Layout(200, 30);
            text = UIFactory.Text(row, format(get()), 18, UIStyle.Muted, true, TextAlignmentOptions.Right);
            text.Layout(62);
            s_Sliders.Add((slider, text, get, format));
        }

        static void ToggleRow(Transform parent, string label, string hint, Func<bool> get, Action<bool> set)
        {
            var row = Row(parent, label, hint);
            var b = UIFactory.Toggle(row, get, v => { set(v); Instance?.RefreshLabels(); });
            b.Layout(120, 42);
            s_Toggles.Add((b, get, "On", "Off"));
        }

        static TextMeshProUGUI CyclerRow(Transform parent, string label, string hint, Func<string> get, Action<int> step)
        {
            var row = Row(parent, label, hint);
            var text = UIFactory.Cycler(row, get, d => { step(d); Instance?.RefreshLabels(); }, 250f);
            s_Cyclers.Add((text, get));
            return text;
        }
    }
}
