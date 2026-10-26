using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Pooled screen-space labels that rise from a world position and fade ("+3 Wood").</summary>
    public class FloatingText : MonoBehaviour
    {
        class Item
        {
            public TextMeshProUGUI text;
            public Vector3 world;
            public float age, life;
            public Vector2 drift;
        }

        static FloatingText s_Instance;
        readonly List<Item> m_Active = new List<Item>();
        readonly Stack<Item> m_Pool = new Stack<Item>();
        RectTransform m_Rect;
        Canvas m_Canvas;

        public void Create(RectTransform parent, Canvas canvas)
        {
            s_Instance = this;
            m_Canvas = canvas;
            m_Rect = (RectTransform)transform;
            m_Rect.SetParent(parent, false);
            m_Rect.Stretch();
        }

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        public static void Show(Vector3 world, string text, Color color, float scale = 1f)
        {
            if (s_Instance == null) return;
            s_Instance.Add(world, text, color, scale);
        }

        void Add(Vector3 world, string text, Color color, float scale)
        {
            if (m_Active.Count > 40) Recycle(m_Active[0]);
            var it = m_Pool.Count > 0 ? m_Pool.Pop() : null;
            if (it == null)
            {
                it = new Item { text = UIFactory.Text(m_Rect, "", 30, Color.white, true, TextAlignmentOptions.Center) };
                it.text.rectTransform.sizeDelta = new Vector2(400, 60);
                it.text.textWrappingMode = TextWrappingModes.NoWrap;
                it.text.outlineWidth = 0.2f;
                it.text.outlineColor = new Color32(20, 16, 10, 200);
            }
            it.text.gameObject.SetActive(true);
            it.text.text = text;
            it.text.color = color;
            it.text.fontSize = 30 * scale;
            it.world = world;
            it.age = 0f;
            it.life = 1.1f;
            it.drift = new Vector2(Random.Range(-30f, 30f), 0f);
            m_Active.Add(it);
            Position(it);
        }

        void Recycle(Item it)
        {
            it.text.gameObject.SetActive(false);
            m_Active.Remove(it);
            m_Pool.Push(it);
        }

        void LateUpdate()
        {
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                var it = m_Active[i];
                it.age += Time.deltaTime;
                if (it.age >= it.life) { Recycle(it); continue; }
                Position(it);
            }
        }

        void Position(Item it)
        {
            var cam = Camera.main;
            if (cam == null) { it.text.alpha = 0f; return; }
            Vector3 sp = cam.WorldToScreenPoint(it.world);
            if (sp.z < 0.2f) { it.text.alpha = 0f; return; }
            float t = it.age / it.life;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, sp, null, out var local);
            it.text.rectTransform.anchoredPosition = local + it.drift * t + new Vector2(0f, 90f * (1f - (1f - t) * (1f - t)));
            it.text.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            float pop = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.12f) / 0.15f));
            it.text.rectTransform.localScale = Vector3.one * pop;
        }
    }
}
