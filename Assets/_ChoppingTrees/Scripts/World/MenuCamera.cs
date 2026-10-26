using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// The camera used behind the main menu: drifts slowly around the clearing. Switches with the
    /// player camera when the game phase changes (only one camera and audio listener is ever on).
    /// </summary>
    public class MenuCamera : MonoBehaviour
    {
        [SerializeField] GameObject m_PlayerCamera;
        [SerializeField] Vector3 m_Focus = new Vector3(0f, 2f, 8f);
        [SerializeField] float m_Radius = 24f;
        [SerializeField] float m_Height = 9f;
        [SerializeField] float m_Speed = 2.5f;

        float m_Angle = 200f;

        void Start()
        {
            Game.Instance.PhaseChanged += OnPhase;
            OnPhase(Game.Instance.Phase);
        }

        void OnDestroy()
        {
            if (Game.Instance != null) Game.Instance.PhaseChanged -= OnPhase;
        }

        void OnPhase(GamePhase p)
        {
            bool menu = p == GamePhase.MainMenu;
            gameObject.SetActive(menu);
            if (m_PlayerCamera != null) m_PlayerCamera.SetActive(!menu);
        }

        void LateUpdate()
        {
            m_Angle += m_Speed * Time.unscaledDeltaTime;
            float a = m_Angle * Mathf.Deg2Rad;
            transform.position = m_Focus + new Vector3(Mathf.Sin(a) * m_Radius, m_Height, Mathf.Cos(a) * m_Radius);
            transform.LookAt(m_Focus);
        }
    }
}
