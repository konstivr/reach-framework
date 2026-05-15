using UnityEngine;
using Reach.Framework.InputSys;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Player movement when this character is controlled.
    /// Reads filtered input from GameContext.Input.
    /// Disabled by PossessableCharacter when the character is uncontrolled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [Header("Speed")]
        public float walkSpeed   = 2.0f;
        public float sprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction (deg/sec).")]
        public float turnSpeed = 540f;

        [Tooltip("Acceleration / deceleration smoothing (higher = snappier).")]
        public float speedChangeRate = 10f;

        [Header("Gravity")]
        public float gravity = -15f;

        [Header("Grounding")]
        public float groundedOffset = -0.14f;
        public float groundedRadius = 0.28f;
        public LayerMask groundLayers = ~0;

        [Header("Camera")]
        [Tooltip("If true: movement is relative to the main camera's yaw (classic 3rd-person).")]
        public bool cameraRelative = true;

        [Header("Animator (auto-resolved)")]
        public Animator animator;

        // Animator parameter hashes
        int _animIDSpeed;
        int _animIDGrounded;
        int _animIDMotionSpeed;

        CharacterController _controller;
        Camera _mainCamera;
        float _currentSpeed;
        float _animSpeedBlend;
        float _verticalVelocity;
        bool _grounded;

        const float _threshold = 0.0001f;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            AssignAnimationIDs();
        }

        void OnEnable()
        {
            _currentSpeed = 0f;
            _animSpeedBlend = 0f;
            _verticalVelocity = 0f;
        }

        void AssignAnimationIDs()
        {
            _animIDSpeed       = Animator.StringToHash("Speed");
            _animIDGrounded    = Animator.StringToHash("Grounded");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        void Update()
        {
            var input = GameContext.Instance?.Input;
            if (input == null) return;

            GroundedCheck();
            ApplyGravity();
            ApplyMovement(input);
        }

        void GroundedCheck()
        {
            Vector3 spherePos = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            _grounded = Physics.CheckSphere(spherePos, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

            if (animator != null)
                animator.SetBool(_animIDGrounded, _grounded);
        }

        void ApplyGravity()
        {
            if (_grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;
        }

        void ApplyMovement(InputReader input)
        {
            Vector2 m = input.Move;

            float targetSpeed = input.Sprint ? sprintSpeed : walkSpeed;
            if (m == Vector2.zero) targetSpeed = 0f;

            float currentHoriz = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMag = m.magnitude;

            if (currentHoriz < targetSpeed - speedOffset || currentHoriz > targetSpeed + speedOffset)
            {
                _currentSpeed = Mathf.Lerp(currentHoriz, targetSpeed * inputMag, Time.deltaTime * speedChangeRate);
                _currentSpeed = Mathf.Round(_currentSpeed * 1000f) / 1000f;
            }
            else
            {
                _currentSpeed = targetSpeed;
            }

            // Smoothed animation blend
            _animSpeedBlend = Mathf.Lerp(_animSpeedBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (_animSpeedBlend < 0.01f) _animSpeedBlend = 0f;

            Vector3 moveWorld;
            if (cameraRelative && _mainCamera != null)
            {
                Vector3 camForward = _mainCamera.transform.forward;
                Vector3 camRight   = _mainCamera.transform.right;
                camForward.y = 0f;
                camRight.y   = 0f;
                camForward.Normalize();
                camRight.Normalize();

                moveWorld = camForward * m.y + camRight * m.x;
            }
            else
            {
                moveWorld = transform.TransformDirection(new Vector3(m.x, 0f, m.y));
            }

            if (moveWorld.sqrMagnitude > _threshold)
            {
                float desiredYaw = Mathf.Atan2(moveWorld.x, moveWorld.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredYaw, turnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            Vector3 moveDir = (moveWorld.sqrMagnitude > _threshold) ? moveWorld.normalized : Vector3.zero;

            _controller.Move(
                moveDir * (_currentSpeed * Time.deltaTime) +
                Vector3.up * (_verticalVelocity * Time.deltaTime)
            );

            // Animator updates
            if (animator != null)
            {
                animator.SetFloat(_animIDSpeed, _animSpeedBlend);
                animator.SetFloat(_animIDMotionSpeed, inputMag);
            }
        }
    }
}
