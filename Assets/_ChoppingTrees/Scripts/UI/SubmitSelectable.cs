using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Makes a panel (a card) a controller stop: it can be focused with the D-pad whether or not
    /// its action is possible right now, and A runs the action (which explains when it can't).
    /// </summary>
    public class SubmitSelectable : Selectable, ISubmitHandler
    {
        Action m_OnSubmit;

        public void Init(Image background, Action onSubmit)
        {
            targetGraphic = background;
            transition = Transition.None;
            m_OnSubmit = onSubmit;
        }

        public void OnSubmit(BaseEventData e) => m_OnSubmit?.Invoke();
    }
}
