using UnityEngine;

namespace NightWarden.Player
{
    /// <summary>
    /// Smooth third-person orbital camera with wall collision detection and avoidance.
    /// Follows the player character while orbiting via mouse input.
    /// </summary>
    public class ThirdPersonCameraFollow : MonoBehaviour
    {
        [Header("Target & Offsets")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0.25f, 1.35f, 0f);

        [Header("Distance & Orbit")]
        [SerializeField] private float defaultDistance = 3.5f;
        [SerializeField] private float minDistance = 0.6f;
        [SerializeField] private float mouseSensitivity = 2.4f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        private float _yaw = 0f;
        private float _pitch = 15f;
        private float _currentDistance;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }

            _currentDistance = defaultDistance;
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Handle mouse orbit input via unified input handler
            Vector2 mouseDelta = WardenInput.GetMouseDelta();
            _yaw += mouseDelta.x * mouseSensitivity;
            _pitch -= mouseDelta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focusPoint = target.position + targetOffset;

            // Collision check: prevent camera from clipping inside walls
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * defaultDistance);
            float targetDist = defaultDistance;

            if (Physics.SphereCast(focusPoint, collisionRadius, (desiredPosition - focusPoint).normalized, out RaycastHit hit, defaultDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                targetDist = Mathf.Clamp(hit.distance, minDistance, defaultDistance);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * 12f);
            Vector3 finalPosition = focusPoint - (rotation * Vector3.forward * _currentDistance);

            transform.rotation = rotation;
            transform.position = finalPosition;
        }
    }
}
