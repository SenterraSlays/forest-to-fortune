using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>Mouse or right-stick look: yaw turns the body, pitch tilts the camera rig.</summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform m_Pitch;
        [SerializeField] float m_BaseSensitivity = 0.085f;
        [Tooltip("Degrees per second at full stick deflection and sensitivity 1.")]
        [SerializeField] float m_StickSpeed = 190f;

        float m_PitchAngle;

        public Transform PitchRoot => m_Pitch;

        void Update()
        {
            if (!UIState.GameplayInput) return;
            var s = GameSettings.Data;
            Vector2 d = GameInput.MouseDelta * (m_BaseSensitivity * s.mouseSensitivity);
            if (s.invertY) d.y = -d.y;
            Vector2 stick = GameInput.LookStick * (m_StickSpeed * s.stickSensitivity * Time.deltaTime);
            stick.y *= 0.75f; // pitch a little slower than yaw feels natural on a stick
            if (s.invertStickY) stick.y = -stick.y;
            d += stick;
            transform.Rotate(0f, d.x, 0f, Space.Self);
            m_PitchAngle = Mathf.Clamp(m_PitchAngle - d.y, -86f, 86f);
            m_Pitch.localRotation = Quaternion.Euler(m_PitchAngle, 0f, 0f);
        }

        public void SetPitch(float pitch)
        {
            m_PitchAngle = Mathf.Clamp(pitch, -86f, 86f);
            m_Pitch.localRotation = Quaternion.Euler(m_PitchAngle, 0f, 0f);
        }
    }
}
