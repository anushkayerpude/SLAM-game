using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using NightWarden.Stealth;
using NightWarden.Core;

namespace NightWarden.AI
{
    public enum GuardState
    {
        Patrol,
        Investigate,
        Chase,
        Search,
        Eliminated
    }

    /// <summary>
    /// Armed thug AI executing a 4-state NavMesh state machine.
    /// Investigates acoustic pulses and footstep noise, spots player via vision cone,
    /// and can be silently eliminated from behind by the Night Warden.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour
    {
        [Header("State & Patrol")]
        [SerializeField] private GuardState currentState = GuardState.Patrol;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float waitTimeAtWaypoint = 3.0f;

        [Header("Investigation & Chase")]
        [SerializeField] private float investigateSpeed = 3.8f;
        [SerializeField] private float chaseSpeed = 5.6f;
        [SerializeField] private float searchDuration = 6.0f;
        [SerializeField] private float hearingThreshold = 0.15f;
        [SerializeField] private float attackRange = 1.6f;

        [Header("Vision Detection")]
        [SerializeField] private float visionRange = 12f;
        [SerializeField] private float visionFov = 75f;
        [SerializeField] private LayerMask visionObstacleLayers = ~0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip takedownSound;

        private NavMeshAgent _agent;
        private Transform _playerTransform;
        private Vector3 _investigationTarget;
        private Vector3 _lastKnownPlayerPos;
        private int _waypointIndex = 0;
        private float _searchTimer = 0f;
        private bool _isWaiting = false;
        private bool _isEliminated = false;

        public GuardState CurrentState => currentState;
        public bool IsEliminated => _isEliminated;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = patrolSpeed;
            _agent.stoppingDistance = 0.5f;

            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (NoiseSystem.Instance != null)
            {
                NoiseSystem.Instance.OnNoiseEmitted += HandleNoiseEvent;
            }
        }

        private void OnDisable()
        {
            if (NoiseSystem.Instance != null)
            {
                NoiseSystem.Instance.OnNoiseEmitted -= HandleNoiseEvent;
            }
        }

        private void Start()
        {
            // Ensure guard snaps to NavMesh on start
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }

            TransitionTo(GuardState.Patrol);
        }

        private void Update()
        {
            if (_isEliminated || _agent == null || !_agent.isOnNavMesh) return;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                _agent.isStopped = true;
                return;
            }

            _agent.isStopped = false;

            // Continual vision check
            if (CanSeePlayer())
            {
                if (currentState != GuardState.Chase)
                {
                    TransitionTo(GuardState.Chase);
                }
            }

            switch (currentState)
            {
                case GuardState.Patrol:
                    UpdatePatrol();
                    break;
                case GuardState.Investigate:
                    UpdateInvestigate();
                    break;
                case GuardState.Chase:
                    UpdateChase();
                    break;
                case GuardState.Search:
                    UpdateSearch();
                    break;
            }

            // Check attack contact
            if (currentState == GuardState.Chase && _playerTransform != null)
            {
                if (Vector3.Distance(transform.position, _playerTransform.position) <= attackRange)
                {
                    GameManager.Instance?.TriggerGameOver("You were apprehended by an armed guard.");
                }
            }
        }

        private void TransitionTo(GuardState newState)
        {
            if (_isEliminated) return;

            currentState = newState;
            _searchTimer = 0f;

            if (_agent == null || !_agent.isOnNavMesh) return;

            switch (newState)
            {
                case GuardState.Patrol:
                    _agent.speed = patrolSpeed;
                    SetNextPatrolDestination();
                    break;

                case GuardState.Investigate:
                    _agent.speed = investigateSpeed;
                    _agent.SetDestination(_investigationTarget);
                    break;

                case GuardState.Chase:
                    _agent.speed = chaseSpeed;
                    if (audioSource != null && alertSound != null) audioSource.PlayOneShot(alertSound);
                    break;

                case GuardState.Search:
                    _agent.speed = patrolSpeed * 1.2f;
                    _searchTimer = searchDuration;
                    PickRandomSearchPoint();
                    break;
            }
        }

        private void UpdatePatrol()
        {
            if (waypoints == null || waypoints.Length == 0 || !_agent.isOnNavMesh) return;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                if (!_isWaiting) StartCoroutine(WaitAtWaypointRoutine());
            }
        }

        private IEnumerator WaitAtWaypointRoutine()
        {
            _isWaiting = true;
            yield return new WaitForSeconds(waitTimeAtWaypoint);
            _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
            SetNextPatrolDestination();
            _isWaiting = false;
        }

        private void SetNextPatrolDestination()
        {
            if (_agent != null && _agent.isOnNavMesh && waypoints != null && waypoints.Length > 0 && waypoints[_waypointIndex] != null)
            {
                _agent.SetDestination(waypoints[_waypointIndex].position);
            }
        }

        private void UpdateInvestigate()
        {
            if (!_agent.isOnNavMesh) return;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.4f)
            {
                TransitionTo(GuardState.Search);
            }
        }

        private void UpdateChase()
        {
            if (_playerTransform == null || !_agent.isOnNavMesh) return;

            _agent.SetDestination(_playerTransform.position);
            _lastKnownPlayerPos = _playerTransform.position;

            if (!CanSeePlayer() && Vector3.Distance(transform.position, _playerTransform.position) > 14f)
            {
                _investigationTarget = _lastKnownPlayerPos;
                TransitionTo(GuardState.Search);
            }
        }

        private void UpdateSearch()
        {
            if (!_agent.isOnNavMesh) return;

            _searchTimer -= Time.deltaTime;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.4f)
            {
                PickRandomSearchPoint();
            }

            if (_searchTimer <= 0f)
            {
                TransitionTo(GuardState.Patrol);
            }
        }

        private void PickRandomSearchPoint()
        {
            if (!_agent.isOnNavMesh) return;

            Vector3 randomDir = Random.insideUnitSphere * 8f + transform.position;
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void HandleNoiseEvent(NoiseEvent evt)
        {
            if (_isEliminated) return;

            float perceivedNoise = NoiseSystem.Instance.CalculatePerceivedNoise(transform.position, evt);

            if (perceivedNoise >= hearingThreshold)
            {
                _investigationTarget = evt.Position;

                if (currentState == GuardState.Patrol || currentState == GuardState.Search)
                {
                    TransitionTo(GuardState.Investigate);
                }
            }
        }

        private bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;

            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.0f;
            Vector3 toPlayer = targetPos - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > visionRange) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > visionFov * 0.5f) return false;

            if (Physics.Raycast(eyePos, toPlayer.normalized, out RaycastHit hit, dist, visionObstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.CompareTag("Player");
            }

            return false;
        }

        /// <summary>
        /// Silently incapacitates this guard.
        /// </summary>
        public void EliminateSilently()
        {
            if (_isEliminated) return;

            _isEliminated = true;
            currentState = GuardState.Eliminated;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
            }

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Animate or ragdoll down
            transform.position += Vector3.down * 0.4f;
            transform.rotation = Quaternion.Euler(85f, transform.eulerAngles.y, 0f);

            if (audioSource != null && takedownSound != null)
            {
                audioSource.PlayOneShot(takedownSound);
            }
        }
    }
}
