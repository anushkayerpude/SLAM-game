using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Echo.Gameplay;

namespace Echo.AI
{
    public enum MonsterState
    {
        Patrol,
        Investigate,
        Hunt,
        Search
    }

    /// <summary>
    /// Adversarial Monster AI executing a 4-state machine using Unity NavMesh.
    /// Responds dynamically to acoustic events propagated by the NoiseSystem.
    /// The creature remains invisible in the pitch-black facility, revealing its form
    /// exclusively as red point clusters when impacted by LiDAR rays.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("State & Patrol")]
        [SerializeField] private MonsterState currentState = MonsterState.Patrol;
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private float patrolSpeed = 2.4f;
        [SerializeField] private float waypointWaitTime = 2.5f;

        [Header("Investigation & Hunting")]
        [SerializeField] private float investigateSpeed = 4.2f;
        [SerializeField] private float huntSpeed = 6.0f;
        [SerializeField] private float searchDuration = 8.0f;
        [SerializeField] private float hearingThreshold = 0.12f;
        [SerializeField] private float hearingMultiplier = 1.0f;
        [SerializeField] private float catchDistance = 1.8f;

        [Header("Sensory Checks")]
        [SerializeField] private float visualSightRange = 8.0f;
        [SerializeField] private float visualFov = 75f;
        [SerializeField] private LayerMask visionObstacleLayers = ~0;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource vocalAudioSource;
        [SerializeField] private AudioSource footstepAudioSource;

        private NavMeshAgent _agent;
        private Transform _playerTransform;
        private Vector3 _investigationTarget;
        private Vector3 _lastKnownPlayerPosition;
        private int _currentWaypointIndex = 0;
        private float _stateTimer = 0f;
        private bool _isWaitingAtWaypoint = false;

        public MonsterState CurrentState => currentState;
        public float DistanceToPlayer => _playerTransform != null 
            ? Vector3.Distance(transform.position, _playerTransform.position) 
            : 999f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = patrolSpeed;
            _agent.stoppingDistance = 0.5f;

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
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
            TransitionTo(MonsterState.Patrol);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                _agent.isStopped = true;
                return;
            }

            _agent.isStopped = false;

            // Direct sensory check for line-of-sight
            if (CanSeePlayer())
            {
                TransitionTo(MonsterState.Hunt);
            }

            switch (currentState)
            {
                case MonsterState.Patrol:
                    UpdatePatrolState();
                    break;
                case MonsterState.Investigate:
                    UpdateInvestigateState();
                    break;
                case MonsterState.Hunt:
                    UpdateHuntState();
                    break;
                case MonsterState.Search:
                    UpdateSearchState();
                    break;
            }

            // Check lethal catch proximity
            if (_playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) <= catchDistance)
            {
                GameManager.Instance?.TriggerGameOver();
            }
        }

        private void TransitionTo(MonsterState newState)
        {
            currentState = newState;
            _stateTimer = 0f;

            switch (newState)
            {
                case MonsterState.Patrol:
                    _agent.speed = patrolSpeed;
                    SetNextPatrolDestination();
                    break;

                case MonsterState.Investigate:
                    _agent.speed = investigateSpeed;
                    _agent.SetDestination(_investigationTarget);
                    break;

                case MonsterState.Hunt:
                    _agent.speed = huntSpeed;
                    if (_playerTransform != null)
                    {
                        _agent.SetDestination(_playerTransform.position);
                    }
                    break;

                case MonsterState.Search:
                    _agent.speed = patrolSpeed * 1.2f;
                    _stateTimer = searchDuration;
                    PickNearbySearchPoint();
                    break;
            }
        }

        private void UpdatePatrolState()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                if (!_isWaitingAtWaypoint)
                {
                    StartCoroutine(WaitAtWaypointRoutine());
                }
            }
        }

        private IEnumerator WaitAtWaypointRoutine()
        {
            _isWaitingAtWaypoint = true;
            yield return new WaitForSeconds(waypointWaitTime);
            _currentWaypointIndex = (_currentWaypointIndex + 1) % patrolWaypoints.Length;
            SetNextPatrolDestination();
            _isWaitingAtWaypoint = false;
        }

        private void SetNextPatrolDestination()
        {
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                _agent.SetDestination(patrolWaypoints[_currentWaypointIndex].position);
            }
        }

        private void UpdateInvestigateState()
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                // Reached noise origin, transition to searching nearby rooms
                TransitionTo(MonsterState.Search);
            }
        }

        private void UpdateHuntState()
        {
            if (_playerTransform == null) return;

            _agent.SetDestination(_playerTransform.position);
            _lastKnownPlayerPosition = _playerTransform.position;

            // If player broke line-of-sight and is no longer making noise
            if (!CanSeePlayer() && Vector3.Distance(transform.position, _playerTransform.position) > 10f)
            {
                _investigationTarget = _lastKnownPlayerPosition;
                TransitionTo(MonsterState.Search);
            }
        }

        private void UpdateSearchState()
        {
            _stateTimer -= Time.deltaTime;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                PickNearbySearchPoint();
            }

            if (_stateTimer <= 0f)
            {
                TransitionTo(MonsterState.Patrol);
            }
        }

        private void PickNearbySearchPoint()
        {
            Vector3 randomDirection = Random.insideUnitSphere * 10f;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void HandleNoiseEvent(NoiseEvent evt)
        {
            if (NoiseSystem.Instance == null) return;

            float perceivedLoudness = NoiseSystem.Instance.CalculatePerceivedNoise(
                transform.position, 
                evt, 
                hearingMultiplier
            );

            if (perceivedLoudness >= hearingThreshold)
            {
                _investigationTarget = evt.Position;

                // Louder noises or direct pulse scans immediate trigger aggressive investigation or hunt
                if (perceivedLoudness > 0.5f || currentState == MonsterState.Patrol || currentState == MonsterState.Search)
                {
                    TransitionTo(MonsterState.Investigate);
                }
            }
        }

        private bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;

            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = (targetPos - eyePos);
            float dist = dirToPlayer.magnitude;

            if (dist > visualSightRange) return false;

            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > visualFov * 0.5f) return false;

            if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, dist, visionObstacleLayers))
            {
                return hit.collider.CompareTag("Player");
            }

            return false;
        }

        public void SetDifficultyParameters(float speed, float hearingMult)
        {
            huntSpeed = speed;
            patrolSpeed = speed * 0.45f;
            investigateSpeed = speed * 0.75f;
            hearingMultiplier = hearingMult;
        }
    }
}
