using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Head bob, impact kicks and FOV (settings plus a small sprint widening).</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFx : MonoBehaviour
    {
        [SerializeField] PlayerMotor m_Motor;
        [SerializeField] float m_BobAmount = 0.035f;
        [SerializeField] float m_SprintFov = 5f;

        Camera m_Camera;
        Vector3 m_BasePos;
        Vector3 m_Kick, m_KickVel;
        float m_FovExtra;

        public static CameraFx Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();
            m_BasePos = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Small rotational kick in degrees (x = pitch, y = yaw, z = roll).</summary>
        public void Kick(Vector3 degrees) => m_KickVel += degrees * 18f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            // Critically damped spring back to rest.
            m_KickVel += (-m_Kick * 260f - m_KickVel * 30f) * dt;
            m_Kick += m_KickVel * dt;

            float bobWeight = m_Motor != null && m_Motor.Grounded ? Mathf.Clamp01(m_Motor.HorizontalSpeed / 5f) : 0f;
            float phase = m_Motor != null ? m_Motor.StrideDistance * 1.9f : 0f;
            Vector3 bob = new Vector3(Mathf.Cos(phase) * m_BobAmount * 0.6f, Mathf.Abs(Mathf.Sin(phase)) * m_BobAmount, 0f) * bobWeight;
            transform.localPosition = m_BasePos + bob;
            transform.localRotation = Quaternion.Euler(m_Kick);

            float target = m_Motor != null && m_Motor.Sprinting && m_Motor.HorizontalSpeed > 5f ? m_SprintFov : 0f;
            m_FovExtra = Mathf.Lerp(m_FovExtra, target, 1f - Mathf.Exp(-8f * dt));
            m_Camera.fieldOfView = GameSettings.Data.fieldOfView + m_FovExtra;
        }
    }
}
