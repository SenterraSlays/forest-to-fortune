using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// A full-screen layer that fades in and out. Modal screens block gameplay input while open.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        protected RectTransform Root;
        protected CanvasGroup Group;
        float m_Target;

        protected virtual bool Modal => true;

        static readonly List<UIScreen> s_Stack = new List<UIScreen>();

        /// <summary>The most recently opened modal screen that is still open (controller focus goes here).</summary>
        public static UIScreen TopModal
        {
            get
            {
                for (int i = s_Stack.Count - 1; i >= 0; i--)
                    if (s_Stack[i] != null && s_Stack[i].IsOpen) return s_Stack[i];
                return null;
            }
        }

        /// <summary>What a controller focuses when this screen opens: by default the first
        /// interactable control (top-left first).</summary>
        public virtual GameObject DefaultSelection()
        {
            Selectable best = null;
            foreach (var s in GetComponentsInChildren<Selectable>())
            {
                if (!s.IsInteractable() || !s.isActiveAndEnabled) continue;
                if (best == null) { best = s; continue; }
                Vector3 a = s.transform.position, b = best.transform.position;
                if (a.y > b.y + 1f || Mathf.Abs(a.y - b.y) <= 1f && a.x < b.x) best = s;
            }
            return best != null ? best.gameObject : null;
        }
        protected virtual float FadeSpeed => 8f;
        public bool IsOpen { get; private set; }

        /// <summary>Builds the screen's content under <see cref="Root"/>.</summary>
        protected abstract void Build();

        public void Create(RectTransform parent)
        {
            Root = (RectTransform)transform;
            Root.SetParent(parent, false);
            Root.Stretch();
            Group = GetComponent<CanvasGroup>();
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Build();
            gameObject.SetActive(false);
        }

        public virtual void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            m_Target = 1f;
            Group.interactable = true;
            Group.blocksRaycasts = Modal;
            transform.SetAsLastSibling();
            if (Modal)
            {
                UIState.SetOpen(this, true);
                s_Stack.Remove(this);
                s_Stack.Add(this);
            }
            OnOpened();
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            m_Target = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            if (Modal)
            {
                UIState.SetOpen(this, false);
                s_Stack.Remove(this);
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(transform))
                    es.SetSelectedGameObject(null);
            }
            OnClosed();
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        protected virtual void Update()
        {
            Group.alpha = Mathf.MoveTowards(Group.alpha, m_Target, Time.unscaledDeltaTime * FadeSpeed);
            if (!IsOpen && Group.alpha <= 0f) gameObject.SetActive(false);
        }
    }
}
