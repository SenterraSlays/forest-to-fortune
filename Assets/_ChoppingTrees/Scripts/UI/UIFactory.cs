using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Shared look of the interface: a dark forest-green surface with warm amber accents.</summary>
    public static class UIStyle
    {
        public static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.03f, 0.72f);
        public static readonly Color Panel = new Color(0.10f, 0.115f, 0.095f, 0.985f);
        public static readonly Color PanelLight = new Color(0.14f, 0.16f, 0.13f, 0.99f);
        public static readonly Color Card = new Color(0.165f, 0.185f, 0.15f, 1f);
        public static readonly Color CardHover = new Color(0.21f, 0.235f, 0.19f, 1f);
        public static readonly Color Inset = new Color(0.07f, 0.08f, 0.065f, 1f);
        public static readonly Color Border = new Color(1f, 0.95f, 0.82f, 0.10f);
        public static readonly Color Hud = new Color(0.06f, 0.07f, 0.055f, 0.74f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);

        public static readonly Color Text = new Color(0.97f, 0.94f, 0.87f);
        public static readonly Color Muted = new Color(0.72f, 0.69f, 0.60f);
        public static readonly Color Dim = new Color(0.50f, 0.48f, 0.42f);
        public static readonly Color Accent = new Color(0.96f, 0.71f, 0.27f);
        public static readonly Color AccentDark = new Color(0.16f, 0.10f, 0.03f);
        public static readonly Color Money = new Color(1f, 0.83f, 0.40f);
        public static readonly Color Good = new Color(0.52f, 0.80f, 0.36f);
        public static readonly Color Bad = new Color(0.92f, 0.42f, 0.33f);
        public static readonly Color Button = new Color(0.20f, 0.225f, 0.185f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.15f, 0.16f, 0.14f, 1f);
        /// <summary>Skill points and Legacy perks.</summary>
        public static readonly Color Skill = new Color(0.74f, 0.62f, 0.92f);

        public static TMP_FontAsset Body, Bold;

        static Sprite s_Rounded, s_Circle, s_RoundedSmall;

        /// <summary>9-sliced rounded rectangle generated once (no texture assets needed).</summary>
        public static Sprite Rounded => s_Rounded != null ? s_Rounded : s_Rounded = MakeRounded(64, 14);
        public static Sprite RoundedSmall => s_RoundedSmall != null ? s_RoundedSmall : s_RoundedSmall = MakeRounded(32, 7);
        public static Sprite Circle => s_Circle != null ? s_Circle : s_Circle = MakeRounded(64, 32);

        public static Color CategoryColor(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Axe => new Color(0.93f, 0.50f, 0.32f),
            UpgradeCategory.Automation => new Color(0.97f, 0.73f, 0.27f),
            UpgradeCategory.Economy => new Color(0.42f, 0.80f, 0.62f),
            UpgradeCategory.Forest => new Color(0.45f, 0.72f, 0.92f),
            // The Legacy tree's branches: jewel tones around the skill point purple.
            UpgradeCategory.Heritage => new Color(0.80f, 0.66f, 0.98f),
            UpgradeCategory.Might => new Color(0.95f, 0.50f, 0.62f),
            UpgradeCategory.Industry => new Color(0.42f, 0.78f, 0.95f),
            UpgradeCategory.Fortune => new Color(0.98f, 0.80f, 0.40f),
            _ => new Color(0.74f, 0.62f, 0.92f), // Legacy (skill point perks)
        };

        public static string CategoryIcon(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Axe => "axe",
            UpgradeCategory.Automation => "gear",
            UpgradeCategory.Economy => "coin",
            UpgradeCategory.Forest => "pine",
            UpgradeCategory.Heritage => "crown",
            UpgradeCategory.Might => "bolt",
            UpgradeCategory.Industry => "gear",
            UpgradeCategory.Fortune => "coin",
            _ => "rebirth",
        };

        /// <summary>Heading for a skill tree branch.</summary>
        public static string CategoryTitle(UpgradeCategory c) => c == UpgradeCategory.Forest ? "Forests" : c.ToString();

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIRounded" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = Mathf.Min(radius + 1, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }

    public enum ButtonStyle { Ghost, Primary, Success, Danger, Tab }

    /// <summary>Builds uGUI elements in code with the shared style.</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Positions a rect: anchors/pivot in 0..1, position and size in canvas units.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = UIStyle.Rounded;
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A rounded surface with a faint highlight border; optionally a soft shadow under it.</summary>
        public static Image Surface(Transform parent, string name, Color color, bool shadow = false, bool border = true)
        {
            var img = Panel(parent, name, color);
            if (border) Border(img.transform, UIStyle.Border);
            if (shadow)
            {
                var sh = img.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
                sh.effectDistance = new Vector2(0f, -4f);
            }
            return img;
        }

        /// <summary>Outline drawn over a rounded panel (ignored by layout groups).</summary>
        public static Image Border(Transform panel, Color color)
        {
            var b = Panel(panel, "Border", color, false);
            b.sprite = UIIcons.Ring;
            b.type = Image.Type.Sliced;
            b.rectTransform.Stretch();
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return b;
        }

        /// <summary>Large blurred shadow placed behind a free-standing panel (same parent, drawn first).</summary>
        public static Image DropShadow(RectTransform panel, float spread = 36f, float offsetY = -14f)
        {
            var sh = Panel(panel.parent, panel.name + "Shadow", UIStyle.Shadow, false);
            sh.sprite = UIIcons.Shadow;
            sh.type = Image.Type.Sliced;
            var rt = sh.rectTransform;
            rt.anchorMin = panel.anchorMin;
            rt.anchorMax = panel.anchorMax;
            rt.pivot = panel.pivot;
            rt.anchoredPosition = panel.anchoredPosition + new Vector2(0f, offsetY);
            rt.sizeDelta = panel.sizeDelta + Vector2.one * spread * 2f;
            rt.SetSiblingIndex(panel.GetSiblingIndex());
            return sh;
        }

        /// <summary>Modal window: shadow, rounded surface, border and a header with icon and title.</summary>
        public static Image Window(Transform parent, string title, string icon, Vector2 size, out RectTransform body, string subtitle = null)
        {
            var panel = Surface(parent, "Window", UIStyle.Panel);
            panel.raycastTarget = true;
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var shadow = DropShadow(panel.rectTransform);
            panel.gameObject.AddComponent<FitToParent>().Configure(shadow.rectTransform, 16f);
            var header = Rect("Header", panel.transform);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, subtitle != null ? 96 : 84);
            header.anchoredPosition = Vector2.zero;
            var line = Panel(header, "Line", new Color(1f, 0.95f, 0.82f, 0.08f), false);
            line.rectTransform.anchorMin = new Vector2(0, 0);
            line.rectTransform.anchorMax = new Vector2(1, 0);
            line.rectTransform.sizeDelta = new Vector2(-64, 1.5f);
            if (icon != null)
            {
                var badge = Panel(header, "Badge", UIStyle.Accent);
                badge.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(46, 46));
                Icon(badge.transform, icon, UIStyle.AccentDark, 30).rectTransform.anchoredPosition = Vector2.zero;
            }
            float tx = icon != null ? 94 : 36;
            var t = Text(header, title, 34, UIStyle.Text, true);
            t.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(tx, subtitle != null ? 14 : 0), new Vector2(size.x - 200, 46));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (subtitle != null)
            {
                var st = Text(header, subtitle, 18, UIStyle.Muted);
                st.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(tx, -20), new Vector2(size.x - 200, 26));
            }
            body = Rect("Body", panel.transform);
            body.Stretch(0, 0, header.sizeDelta.y, 0);
            return panel;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? UIStyle.Bold : UIStyle.Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Small uppercase, letter-spaced label for section headings.</summary>
        public static TextMeshProUGUI Caption(Transform parent, string text, Color? color = null, float size = 16f)
        {
            var t = Text(parent, text.ToUpperInvariant(), size, color ?? UIStyle.Accent, true);
            t.characterSpacing = 8f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        public static Image Icon(Transform parent, string icon, Color color, float size)
        {
            var img = Panel(parent, "Icon_" + icon, color, false);
            img.sprite = UIIcons.Get(icon);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            return img;
        }

        /// <summary>Rounded badge with text (levels, counts, tags).</summary>
        public static TextMeshProUGUI Pill(Transform parent, string text, Color background, Color textColor, float fontSize = 16f)
        {
            var bg = Panel(parent, "Pill", background);
            bg.sprite = UIStyle.RoundedSmall;
            HStack(bg, 0f, new RectOffset(10, 10, 3, 4), TextAnchor.MiddleCenter);
            Fit(bg, true, true);
            var t = Text(bg.transform, text, fontSize, textColor, true, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>A keyboard key drawn as a small key cap.</summary>
        public static RectTransform KeyCap(Transform parent, string key, float height = 30f)
        {
            var cap = Panel(parent, "Key_" + key, new Color(0.93f, 0.90f, 0.82f));
            cap.sprite = UIStyle.RoundedSmall;
            var sh = cap.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0.35f, 0.3f, 0.22f, 1f);
            sh.effectDistance = new Vector2(0, -3);
            var t = Text(cap.transform, key, height * 0.55f, UIStyle.AccentDark, true, TextAlignmentOptions.Center);
            t.rectTransform.Stretch(4, 4, 0, 2);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Max(height, t.GetPreferredValues(key).x + 16f);
            var le = cap.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = height;
            cap.rectTransform.sizeDelta = new Vector2(w, height);
            // Controller button icon, shown instead of the key when a controller is in use.
            var glyph = Panel(cap.transform, "Glyph", Color.white, false);
            glyph.preserveAspect = true;
            glyph.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * height * 1.3f);
            glyph.gameObject.SetActive(false);
            return cap.rectTransform;
        }

        /// <summary>Changes a key cap's label and fits its width; with a glyph, shows the controller
        /// button icon instead of the cap.</summary>
        public static void SetKeyCap(RectTransform cap, string key, Sprite glyph = null)
        {
            var t = cap.GetComponentInChildren<TextMeshProUGUI>(true);
            var g = cap.Find("Glyph")?.GetComponent<Image>();
            var bg = cap.GetComponent<Image>();
            var shadow = cap.GetComponent<UnityEngine.UI.Shadow>();
            float h = cap.sizeDelta.y;
            bool icon = glyph != null && g != null;
            if (g != null)
            {
                g.gameObject.SetActive(icon);
                if (icon) g.sprite = glyph;
            }
            bg.color = icon ? Color.clear : new Color(0.93f, 0.90f, 0.82f);
            if (shadow != null) shadow.enabled = !icon;
            t.gameObject.SetActive(!icon);
            t.text = key;
            float w = icon ? h * 1.3f : Mathf.Max(h, t.GetPreferredValues(key).x + 16f);
            var le = cap.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            cap.sizeDelta = new Vector2(w, h);
        }

        public static LayoutElement Layout(this Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1, float minH = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = prefW;
            le.preferredHeight = prefH;
            le.flexibleWidth = flexW;
            le.flexibleHeight = flexH;
            le.minHeight = minH;
            return le;
        }

        public static VerticalLayoutGroup VStack(Component c, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(Component c, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static ContentSizeFitter Fit(Component c, bool width, bool height)
        {
            var f = c.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = width ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = height ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static Color StyleColor(ButtonStyle style) => style switch
        {
            ButtonStyle.Primary => UIStyle.Accent,
            ButtonStyle.Success => new Color(0.36f, 0.62f, 0.27f),
            ButtonStyle.Danger => new Color(0.74f, 0.30f, 0.24f),
            ButtonStyle.Tab => new Color(0f, 0f, 0f, 0f),
            _ => UIStyle.Button,
        };

        public static UIButton Button(Transform parent, string label, UnityAction onClick, float fontSize = 24f, ButtonStyle style = ButtonStyle.Ghost, string icon = null)
        {
            var color = StyleColor(style);
            var img = Panel(parent, "Button", color);
            img.raycastTarget = true;
            if (style == ButtonStyle.Ghost) Border(img.transform, UIStyle.Border);
            var b = img.gameObject.AddComponent<UIButton>();
            var content = Rect("Content", img.transform);
            content.Stretch(14, 14, 4, 4);
            HStack(content, 10f, null, TextAnchor.MiddleCenter);
            Color fg = style == ButtonStyle.Primary ? UIStyle.AccentDark : UIStyle.Text;
            Image iconImg = null;
            if (icon != null) iconImg = Icon(content, icon, fg, fontSize * 0.95f);
            var text = Text(content, label, fontSize, fg, true, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (string.IsNullOrEmpty(label)) text.gameObject.SetActive(false);
            b.Init(img, text, color, iconImg, style);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        /// <summary>Rounded progress bar. Returns the fill; use <see cref="SetBar"/> to show progress.</summary>
        public static RectTransform Bar(Transform parent, float height, Color fill, Color? track = null)
        {
            var bg = Panel(parent, "Bar", track ?? UIStyle.Inset);
            bg.sprite = UIStyle.RoundedSmall;
            bg.Layout(-1, height, 1);
            var f = Panel(bg.transform, "Fill", fill);
            f.sprite = UIStyle.RoundedSmall;
            var rt = f.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void SetBar(RectTransform fill, float value)
        {
            value = Mathf.Clamp01(value);
            fill.anchorMax = new Vector2(value, 1f);
            fill.gameObject.SetActive(value > 0.001f);
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Panel(root, "Track", UIStyle.Inset);
            bg.sprite = UIStyle.RoundedSmall;
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 10);
            bg.raycastTarget = true;
            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 10);
            var fill = Panel(fillArea, "Fill", UIStyle.Accent);
            fill.sprite = UIStyle.RoundedSmall;
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("HandleArea", root).Stretch(11, 11);
            var handle = Panel(handleArea, "Handle", UIStyle.Text);
            handle.sprite = UIStyle.Circle;
            handle.rectTransform.sizeDelta = new Vector2(22, 22);
            handle.raycastTarget = true;
            var ring = handle.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(0.96f, 0.71f, 0.27f, 0.9f);
            ring.effectDistance = new Vector2(2, -2);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>"&lt; value &gt;" selector. step(-1/+1) changes the value; get() supplies the text.</summary>
        public static TextMeshProUGUI Cycler(Transform parent, Func<string> get, Action<int> step, float width = 300f)
        {
            var root = Panel(parent, "Cycler", UIStyle.Inset);
            var le = root.Layout(width, 44);
            le.minWidth = width;
            le.flexibleWidth = 0;
            Border(root.transform, UIStyle.Border);
            HStack(root, 4f, new RectOffset(4, 4, 4, 4), TextAnchor.MiddleCenter);
            TextMeshProUGUI label = null;
            // The whole selector is one stop for a controller: left / right change the value,
            // up / down move on. The arrow buttons are for the mouse only.
            var sel = root.gameObject.AddComponent<CyclerSelectable>();
            sel.Init(root, d => { step(d); label.text = get(); });
            void Arrow(string icon, int dir)
            {
                var b = Button(root.transform, "", () => { step(dir); label.text = get(); }, 18, ButtonStyle.Ghost, icon);
                b.Layout(36, 36);
                b.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            Arrow("chevron_left", -1);
            label = Text(root.transform, get(), 20, UIStyle.Text, true, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // Long values (monitor names, "2560 x 1440 (native)") shrink to fit between the arrows.
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 20f;
            label.Layout(-1, 36, 1);
            Arrow("chevron_right", 1);
            label.transform.SetSiblingIndex(2);
            return label;
        }

        /// <summary>Thin line across the top of a window that glows in the middle and fades out to
        /// both ends (the accent trim used on the big screens).</summary>
        public static void TopTrim(RectTransform window, Color color, float width = 0.7f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var line = Panel(window, "Trim", color, false);
                line.sprite = UIIcons.GradientH;
                var rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -1.5f);
                rt.sizeDelta = new Vector2(window.rect.width * width * 0.5f, 2.5f);
                if (side < 0) rt.localRotation = Quaternion.Euler(0, 0, 180f);
                line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
        }

        /// <summary>Header chip with an icon and a value (money, skill points).</summary>
        public static TextMeshProUGUI Chip(Transform parent, string icon, Color color, Vector2 rightPos, float width)
        {
            var chip = Surface(parent, "Chip_" + icon, UIStyle.Inset);
            chip.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), rightPos, new Vector2(width, 52));
            HStack(chip, 10f, new RectOffset(16, 18, 6, 6), TextAnchor.MiddleRight);
            var glow = Panel(chip.transform, "Glow", new Color(color.r, color.g, color.b, 0.07f));
            glow.rectTransform.Stretch();
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Icon(chip.transform, icon, color, 28f);
            var t = Text(chip.transform, "", 30, color, true, TextAlignmentOptions.Right);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.Layout(-1, -1, 1);
            return t;
        }

        /// <summary>On/off switch styled as a button that turns green when on.</summary>
        public static UIButton Toggle(Transform parent, Func<bool> get, Action<bool> set, string onText = "On", string offText = "Off")
        {
            UIButton b = null;
            b = Button(parent, get() ? onText : offText, () =>
            {
                set(!get());
                b.Label.text = get() ? onText : offText;
                b.SetColor(get() ? StyleColor(ButtonStyle.Success) : UIStyle.Button);
            }, 20, get() ? ButtonStyle.Success : ButtonStyle.Ghost);
            return b;
        }
    }
}
