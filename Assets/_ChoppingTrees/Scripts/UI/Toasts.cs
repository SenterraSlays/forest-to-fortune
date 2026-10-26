using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>Short notifications stacked on the right edge.</summary>
    public class Toasts : MonoBehaviour
    {
        public enum Kind { Info, Success, Warning, Money }

        class Item
        {
            public RectTransform rect;
            public CanvasGroup group;
            public float age;
        }

        static Toasts s_Instance;
        readonly List<Item> m_Items = new List<Item>();
        RectTransform m_Stack;
        const float Life = 3.6f;

        public void Create(RectTransform parent)
        {
            s_Instance = this;
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 40f), new Vector2(460f, 520f));
            m_Stack = rt;
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
            {
                var t = existing.rect.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null && t.text == message) { existing.age = 0f; return; }
            }
            if (m_Items.Count >= 5) Remove(m_Items[0]);
            Color accent = kind switch
            {
                Kind.Success => UIStyle.Good,
                Kind.Warning => UIStyle.Bad,
                Kind.Money => UIStyle.Money,
                _ => UIStyle.Accent,
            };
            var bg = UIFactory.Panel(m_Stack, "Toast", UIStyle.Panel);
            var item = new Item { rect = bg.rectTransform, group = bg.gameObject.AddComponent<CanvasGroup>() };
            UIFactory.HStack(bg, 12f, new RectOffset(14, 18, 10, 10));
            var bar = UIFactory.Panel(bg.transform, "Accent", accent);
            bar.Layout(6, 30);
            var text = UIFactory.Text(bg.transform, message, 22, UIStyle.Text);
            text.Layout(-1, -1, 1);
            var fit = UIFactory.Fit(bg, true, true);
            text.GetComponent<LayoutElement>().preferredWidth = Mathf.Min(380f, text.GetPreferredValues(message, 380f, 0f).x + 4f);
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
                float a = it.age < 0.2f ? it.age / 0.2f : it.age > Life - 0.4f ? (Life - it.age) / 0.4f : 1f;
                it.group.alpha = a;
                if (it.age >= Life) Remove(it);
            }
        }
    }
}
