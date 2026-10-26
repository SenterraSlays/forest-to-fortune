using UnityEngine;

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
            if (Modal) UIState.SetOpen(this, true);
            OnOpened();
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            m_Target = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            if (Modal) UIState.SetOpen(this, false);
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
