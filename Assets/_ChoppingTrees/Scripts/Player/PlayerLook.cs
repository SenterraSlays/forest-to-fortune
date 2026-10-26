using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>Mouse look: yaw turns the body, pitch tilts the camera rig.</summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform m_Pitch;
        [SerializeField] float m_BaseSensitivity = 0.085f;

        float m_PitchAngle;

        public Transform PitchRoot => m_Pitch;

        void Update()
        {
            if (!UIState.GameplayInput || Mouse.current == null) return;
            var s = GameSettings.Data;
            Vector2 d = Mouse.current.delta.ReadValue() * (m_BaseSensitivity * s.mouseSensitivity);
            transform.Rotate(0f, d.x, 0f, Space.Self);
            m_PitchAngle = Mathf.Clamp(m_PitchAngle + (s.invertY ? d.y : -d.y), -86f, 86f);
            m_Pitch.localRotation = Quaternion.Euler(m_PitchAngle, 0f, 0f);
        }

        public void SetPitch(float pitch)
        {
            m_PitchAngle = Mathf.Clamp(pitch, -86f, 86f);
            m_Pitch.localRotation = Quaternion.Euler(m_PitchAngle, 0f, 0f);
        }
    }
}
