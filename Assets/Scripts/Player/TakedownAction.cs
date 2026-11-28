using UnityEngine;
using NightWarden.Stealth;
using NightWarden.AI;

namespace NightWarden.Player
{
    /// <summary>
    /// Silent takedown mechanic.
    /// When the player is crouched and sneaks up behind an unaware guard within range,
    /// pressing 'F' silently eliminates the enemy without alerting the district.
    /// </summary>
    public class TakedownAction : MonoBehaviour
    {
        [Header("Takedown Settings")]
        [SerializeField] private float takedownDistance = 2.2f;
        [SerializeField] private float behindAngleThreshold = 0.35f; // Dot product check
        [SerializeField] private LayerMask guardLayer = ~0;

        [Header("Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip takedownSound;

        private ThirdPersonPlayerController _playerController;
        private GuardAI _currentTargetGuard;

        public bool CanTakedown => _currentTargetGuard != null;
        public GuardAI TargetGuard => _currentTargetGuard;

        private void Awake()
        {
            _playerController = GetComponent<ThirdPersonPlayerController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            DetectTakedownTarget();

            if (CanTakedown && WardenInput.GetTakedownDown())
            {
                ExecuteTakedown();
            }
        }

        private void DetectTakedownTarget()
        {
            _currentTargetGuard = null;

            // Takedown requires crouching for stealth execution
            if (_playerController != null && !_playerController.IsCrouching) return;

            Collider[] hits = Physics.OverlapSphere(transform.position, takedownDistance, guardLayer);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Guard")) continue;

                var guard = hit.GetComponent<GuardAI>();
                if (guard == null || guard.IsEliminated) continue;

                // Check if player is behind the guard
                Vector3 toGuard = (guard.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(guard.transform.forward, toGuard);

                // Both facing roughly the same direction means player is behind guard
                if (dot > behindAngleThreshold)
                {
                    _currentTargetGuard = guard;
                    break;
                }
            }
        }

        private void ExecuteTakedown()
        {
            if (_currentTargetGuard == null) return;

            // Face guard
            Vector3 lookDir = (_currentTargetGuard.transform.position - transform.position);
            lookDir.y = 0;
            if (lookDir.magnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Eliminate guard silently
            _currentTargetGuard.EliminateSilently();

            if (audioSource != null && takedownSound != null)
            {
                audioSource.PlayOneShot(takedownSound);
            }

            // Emit minimal quiet noise
            NoiseSystem.Instance?.EmitNoise(transform.position, 0.08f, NoiseType.Takedown);

            _currentTargetGuard = null;
        }
    }
}
