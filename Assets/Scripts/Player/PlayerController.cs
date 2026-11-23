using UnityEngine;
using Echo.AI;

namespace Echo.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3.8f;
        [SerializeField] private float sprintSpeed = 6.2f;
        [SerializeField] private float crouchSpeed = 1.8f;
        [SerializeField] private float gravity = -15f;

        [Header("Mouse Look")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("Head Bobbing & Footsteps")]
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobHorizontalAmplitude = 0.04f;
        [SerializeField] private float bobVerticalAmplitude = 0.06f;
        [SerializeField] private float footstepInterval = 0.45f;

        private CharacterController _characterController;
        private float _cameraPitch = 0f;
        private Vector3 _velocity;
        private Vector3 _cameraDefaultLocalPos;
        private float _bobTimer;
        private float _stepTimer;
        private bool _isCrouching;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching => _isCrouching;
        public float CurrentSpeed { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
            if (cameraTransform != null)
            {
                _cameraDefaultLocalPos = cameraTransform.localPosition;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleCrouch();
            HandleMovement();
            HandleHeadBobAndFootsteps();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            // Horizontal rotation on the player body
            transform.Rotate(Vector3.up * mouseX);

            // Vertical pitch on the camera
            _cameraPitch -= mouseY;
            _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);

            if (cameraTransform != null)
            {
                cameraTransform.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
            }
        }

        private void HandleCrouch()
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
            {
                _isCrouching = !_isCrouching;
            }

            float targetHeight = _isCrouching ? crouchHeight : standingHeight;
            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _characterController.center = Vector3.up * (_characterController.height * 0.5f);
        }

        private void HandleMovement()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            Vector3 moveDir = (transform.right * moveX + transform.forward * moveZ).normalized;

            // Determine speed
            IsSprinting = Input.GetKey(KeyCode.LeftShift) && !_isCrouching && moveZ > 0.1f;
            float targetSpeed = _isCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            if (moveDir.magnitude > 0.1f)
            {
                CurrentSpeed = targetSpeed;
                Vector3 horizontalVelocity = moveDir * targetSpeed;
                _velocity.x = horizontalVelocity.x;
                _velocity.z = horizontalVelocity.z;
            }
            else
            {
                CurrentSpeed = 0f;
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, 15f * Time.deltaTime);
                _velocity.z = Mathf.MoveTowards(_velocity.z, 0f, 15f * Time.deltaTime);
            }

            // Gravity & Vertical physics
            if (_characterController.isGrounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }

            _characterController.Move(_velocity * Time.deltaTime);
        }

        private void HandleHeadBobAndFootsteps()
        {
            if (cameraTransform == null) return;

            Vector3 horizontalVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
            float speed = horizontalVelocity.magnitude;

            if (speed > 0.5f && _characterController.isGrounded)
            {
                float freq = bobFrequency * (speed / walkSpeed);
                _bobTimer += Time.deltaTime * freq;

                float bobX = Mathf.Sin(_bobTimer * 0.5f) * bobHorizontalAmplitude;
                float bobY = Mathf.Sin(_bobTimer) * bobVerticalAmplitude;

                cameraTransform.localPosition = _cameraDefaultLocalPos + new Vector3(bobX, bobY, 0f);

                // Footstep noise generation
                _stepTimer += Time.deltaTime;
                float interval = footstepInterval / (speed / walkSpeed);
                if (_stepTimer >= interval)
                {
                    _stepTimer = 0f;
                    EmitFootstepNoise();
                }
            }
            else
            {
                _bobTimer = 0f;
                _stepTimer = 0f;
                cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, _cameraDefaultLocalPos, Time.deltaTime * 6f);
            }
        }

        private void EmitFootstepNoise()
        {
            if (NoiseSystem.Instance == null) return;

            float loudness;
            NoiseType type;

            if (_isCrouching)
            {
                loudness = 0.04f;
                type = NoiseType.FootstepCrouch;
            }
            else if (IsSprinting)
            {
                loudness = 0.65f;
                type = NoiseType.FootstepSprint;
            }
            else
            {
                loudness = 0.22f;
                type = NoiseType.FootstepWalk;
            }

            NoiseSystem.Instance.EmitNoise(transform.position, loudness, type);
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }
    }
}
