using UnityEngine;
using NightWarden.Stealth;

namespace NightWarden.Player
{
    /// <summary>
    /// Third-person character controller for the Night Warden vigilante.
    /// Supports walk, sprint, crouch, jump, and variable footstep noise emissions.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonPlayerController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3.6f;
        [SerializeField] private float sprintSpeed = 6.4f;
        [SerializeField] private float crouchSpeed = 1.8f;
        [SerializeField] private float jumpHeight = 1.3f;
        [SerializeField] private float gravity = -18f;

        [Header("Crouch Configuration")]
        [SerializeField] private float standingHeight = 1.55f;
        [SerializeField] private float crouchHeight = 0.90f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("Rotation & Camera")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float rotationSmoothTime = 0.12f;

        [Header("Footsteps & Noise")]
        [SerializeField] private float footstepInterval = 0.45f;

        private CharacterController _controller;
        private float _rotationVelocity;
        private Vector3 _velocity;
        private bool _isCrouching = false;
        private float _stepTimer = 0f;

        public static ThirdPersonPlayerController Instance { get; private set; }
        public bool IsCrouching => _isCrouching;
        public bool IsSprinting { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => _controller.isGrounded;

        private void Awake()
        {
            Instance = this;
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleCrouch();
            HandleMovement();
        }

        private void HandleCrouch()
        {
            if (WardenInput.GetCrouchDown())
            {
                _isCrouching = !_isCrouching;
            }

            float targetHeight = _isCrouching ? crouchHeight : standingHeight;
            _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _controller.center = Vector3.up * (_controller.height * 0.5f);
        }

        private void HandleMovement()
        {
            float horizontal = WardenInput.GetHorizontal();
            float vertical = WardenInput.GetVertical();
            Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

            IsSprinting = WardenInput.GetSprint() && !_isCrouching && inputDir.magnitude > 0.1f;
            float targetSpeed = _isCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            if (inputDir.magnitude >= 0.1f)
            {
                CurrentSpeed = targetSpeed;

                // Rotate player relative to camera facing angle
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + (cameraTransform != null ? cameraTransform.eulerAngles.y : 0f);
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                _controller.Move(moveDir.normalized * (targetSpeed * Time.deltaTime));

                // Process footsteps and noise
                HandleFootstepNoise(targetSpeed);
            }
            else
            {
                CurrentSpeed = 0f;
                _stepTimer = 0f;
            }

            // Jump handling
            if (_controller.isGrounded)
            {
                if (_velocity.y < 0f) _velocity.y = -2f;

                if (WardenInput.GetJumpDown() && !_isCrouching)
                {
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }

            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleFootstepNoise(float speed)
        {
            if (!_controller.isGrounded) return;

            _stepTimer += Time.deltaTime;
            float interval = footstepInterval / (speed / walkSpeed);

            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;

                float loudness;
                NoiseType type;

                if (_isCrouching)
                {
                    loudness = 0.05f; // Near silent
                    type = NoiseType.FootstepCrouch;
                }
                else if (IsSprinting)
                {
                    loudness = 0.55f; // 8m guard hearing radius
                    type = NoiseType.FootstepSprint;
                }
                else
                {
                    loudness = 0.20f;
                    type = NoiseType.FootstepWalk;
                }

                NoiseSystem.Instance?.EmitNoise(transform.position, loudness, type);
            }
        }
    }
}
