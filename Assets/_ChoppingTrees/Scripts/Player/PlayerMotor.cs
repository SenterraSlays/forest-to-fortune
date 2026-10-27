using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoppingTrees
{
    /// <summary>WASD movement with sprint, jumping and gravity on a CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        CharacterController m_Controller;
        GameConfig m_Config;
        Vector3 m_Velocity;
        float m_StepDistance;
        bool m_WasGrounded = true;
        float m_AirTime;

        public float HorizontalSpeed { get; private set; }
        public bool Grounded => m_Controller.isGrounded;
        public bool Sprinting { get; private set; }
        /// <summary>Distance walked on the ground (drives head bob).</summary>
        public float StrideDistance { get; private set; }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            m_Config = Game.Instance.Db.config;
        }

        /// <summary>Moves the player without the controller fighting the teleport.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            m_Controller.enabled = true;
            m_Velocity = Vector3.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector2 input = Vector2.zero;
            bool jump = false;
            Sprinting = false;
            if (UIState.GameplayInput)
            {
                // Keyboard or left stick (see GameInput).
                input = GameInput.Move;
                jump = GameInput.JumpPressed;
                Sprinting = GameInput.Sprint && input.y > 0.1f;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            bool grounded = m_Controller.isGrounded;
            float speed = (Sprinting ? m_Config.sprintSpeed : m_Config.walkSpeed) * Game.Instance.Model.Upgrades.MoveSpeedMultiplier;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;
            Vector3 horizontal = new Vector3(m_Velocity.x, 0f, m_Velocity.z);
            float accel = m_Config.acceleration * (grounded ? 1f : m_Config.airControl);
            horizontal = Vector3.MoveTowards(horizontal, wish, accel * speed * dt);

            if (grounded)
            {
                if (m_Velocity.y < 0f) m_Velocity.y = -2f; // keeps the controller snapped to slopes
                if (jump)
                {
                    m_Velocity.y = Mathf.Sqrt(2f * m_Config.jumpHeight * -m_Config.gravity);
                    AudioManager.Ui("jump", 0.5f);
                }
            }
            m_Velocity.y += m_Config.gravity * dt;
            m_Velocity = new Vector3(horizontal.x, m_Velocity.y, horizontal.z);

            var flags = m_Controller.Move(m_Velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && m_Velocity.y > 0f) m_Velocity.y = 0f;

            HorizontalSpeed = new Vector3(m_Controller.velocity.x, 0f, m_Controller.velocity.z).magnitude;
            grounded = m_Controller.isGrounded;
            if (grounded)
            {
                StrideDistance += HorizontalSpeed * dt;
                m_StepDistance += HorizontalSpeed * dt;
                if (m_StepDistance > (Sprinting ? 2.4f : 1.9f))
                {
                    m_StepDistance = 0f;
                    AudioManager.Ui("footstep", Sprinting ? 0.35f : 0.25f);
                }
                if (!m_WasGrounded && m_AirTime > 0.25f) AudioManager.Ui("land", Mathf.Clamp01(m_AirTime) * 0.6f);
                m_AirTime = 0f;
            }
            else m_AirTime += dt;
            m_WasGrounded = grounded;

            // Safety net: never let the player fall out of the world.
            if (transform.position.y < -20f) Teleport(PlayerSpawn.Position, PlayerSpawn.Yaw);
        }
    }
}
