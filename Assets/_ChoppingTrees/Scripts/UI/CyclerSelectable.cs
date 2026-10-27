using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// Makes a "&lt; value &gt;" selector a single controller stop: left / right step the value
    /// (with a click), up / down navigate as usual, and A steps forward.
    /// </summary>
    public class CyclerSelectable : Selectable, ISubmitHandler
    {
        Action<int> m_Step;

        public void Init(Image background, Action<int> step)
        {
            targetGraphic = background;
            transition = Transition.None;
            m_Step = step;
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left || e.moveDir == MoveDirection.Right)
            {
                m_Step?.Invoke(e.moveDir == MoveDirection.Left ? -1 : 1);
                AudioManager.Ui("ui_click", 0.45f);
                return;
            }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e)
        {
            m_Step?.Invoke(1);
            AudioManager.Ui("ui_click", 0.45f);
        }
    }
}
