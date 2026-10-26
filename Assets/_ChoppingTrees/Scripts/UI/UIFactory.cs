using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Shared look of the interface.</summary>
    public static class UIStyle
    {
        public static readonly Color Backdrop = new Color(0.05f, 0.06f, 0.05f, 0.62f);
        public static readonly Color Panel = new Color(0.11f, 0.12f, 0.10f, 0.985f);
        public static readonly Color PanelLight = new Color(0.17f, 0.18f, 0.15f, 0.99f);
        public static readonly Color Card = new Color(0.15f, 0.16f, 0.13f, 0.97f);
        public static readonly Color Hud = new Color(0.08f, 0.09f, 0.07f, 0.55f);
        public static readonly Color Text = new Color(0.96f, 0.94f, 0.88f);
        public static readonly Color Muted = new Color(0.70f, 0.68f, 0.61f);
        public static readonly Color Accent = new Color(0.98f, 0.74f, 0.32f);
        public static readonly Color Money = new Color(1f, 0.84f, 0.38f);
        public static readonly Color Good = new Color(0.50f, 0.80f, 0.38f);
        public static readonly Color Bad = new Color(0.92f, 0.40f, 0.33f);
        public static readonly Color Button = new Color(0.24f, 0.25f, 0.21f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.19f, 0.19f, 0.17f, 1f);

        public static TMP_FontAsset Body, Bold;

        static Sprite s_Rounded, s_Circle;

        /// <summary>9-sliced rounded rectangle generated once (no texture assets needed).</summary>
        public static Sprite Rounded => s_Rounded != null ? s_Rounded : s_Rounded = MakeRounded(64, 14);
        public static Sprite Circle => s_Circle != null ? s_Circle : s_Circle = MakeRounded(64, 32);

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

        public static UIButton Button(Transform parent, string label, UnityAction onClick, float fontSize = 26f, Color? color = null)
        {
            var img = Panel(parent, "Button", color ?? UIStyle.Button);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<UIButton>();
            var text = Text(img.transform, label, fontSize, UIStyle.Text, true, TextAlignmentOptions.Center);
            text.rectTransform.Stretch(12, 12, 4, 4);
            b.Init(img, text, color ?? UIStyle.Button);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Panel(root, "Track", UIStyle.Button);
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 10);
            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 10);
            var fill = Panel(fillArea, "Fill", UIStyle.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("HandleArea", root).Stretch(10, 10);
            var handle = Panel(handleArea, "Handle", UIStyle.Text);
            handle.sprite = UIStyle.Circle;
            handle.rectTransform.sizeDelta = new Vector2(24, 24);
            handle.raycastTarget = true;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange(v));
            bg.raycastTarget = true;
            return slider;
        }

        public static UIButton Toggle(Transform parent, Func<bool> get, Action<bool> set, string onText = "On", string offText = "Off")
        {
            UIButton b = null;
            b = Button(parent, get() ? onText : offText, () =>
            {
                set(!get());
                b.Label.text = get() ? onText : offText;
            }, 22f);
            return b;
        }
    }
}
