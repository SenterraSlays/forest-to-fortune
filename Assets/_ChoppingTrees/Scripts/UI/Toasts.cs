using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Short notifications stacked on the right edge; they pop in and fade out.</summary>
    public class Toasts : MonoBehaviour
    {
        public enum Kind { Info, Success, Warning, Money }

        class Item
        {
            public RectTransform rect;
            public CanvasGroup group;
            public TextMeshProUGUI text;
            public float age;
        }

        static Toasts s_Instance;
        readonly List<Item> m_Items = new List<Item>();
        RectTransform m_Stack;
        const float Life = 3.8f;

        public void Create(RectTransform parent)
        {
            s_Instance = this;
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 20f), new Vector2(480f, 560f));
            m_Stack = rt;
            // Own sorting layer so notifications stay above every menu.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            var v = UIFactory.VStack(rt, 10f, null, TextAnchor.LowerRight);
            v.childForceExpandWidth = false;
        }

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        public static void Show(string message, Kind kind = Kind.Info)
        {
            if (s_Instance != null) s_Instance.Add(message, kind);
        }

        void Add(string message, Kind kind)
        {
            // Merge exact repeats instead of stacking them.
            foreach (var existing in m_Items)
                if (existing.text.text == message) { existing.age = 0.25f; return; }
            if (m_Items.Count >= 5) Remove(m_Items[0]);
            Color accent = kind switch
            {
                Kind.Success => UIStyle.Good,
                Kind.Warning => UIStyle.Bad,
                Kind.Money => UIStyle.Money,
                _ => UIStyle.Accent,
            };
            string icon = kind switch
            {
                Kind.Success => "check",
                Kind.Warning => "lock",
                Kind.Money => "coin",
                _ => "leaf",
            };
            var bg = UIFactory.Surface(m_Stack, "Toast", UIStyle.Panel, true);
            var item = new Item { rect = bg.rectTransform, group = bg.gameObject.AddComponent<CanvasGroup>() };
            UIFactory.HStack(bg, 12f, new RectOffset(12, 20, 10, 10));
            var badge = UIFactory.Panel(bg.transform, "Badge", new Color(accent.r, accent.g, accent.b, 0.2f));
            badge.sprite = UIStyle.Circle;
            badge.Layout(36, 36);
            var ic = UIFactory.Icon(badge.transform, icon, accent, 20f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            item.text = UIFactory.Text(bg.transform, message, 21, UIStyle.Text);
            item.text.Layout(Mathf.Min(380f, item.text.GetPreferredValues(message, 380f, 0f).x + 4f), -1);
            UIFactory.Fit(bg, true, true);
            item.group.alpha = 0f;
            m_Items.Add(item);
        }

        void Remove(Item it)
        {
            m_Items.Remove(it);
            if (it.rect != null) Destroy(it.rect.gameObject);
        }

        void Update()
        {
            for (int i = m_Items.Count - 1; i >= 0; i--)
            {
                var it = m_Items[i];
                it.age += Time.unscaledDeltaTime;
                float a = it.age < 0.25f ? it.age / 0.25f : it.age > Life - 0.5f ? (Life - it.age) / 0.5f : 1f;
                it.group.alpha = a;
                // Pops in slightly small and settles.
                it.rect.localScale = Vector3.one * (0.94f + 0.06f * Mathf.Clamp01(it.age / 0.25f));
                if (it.age >= Life) Remove(it);
            }
        }
    }
}
