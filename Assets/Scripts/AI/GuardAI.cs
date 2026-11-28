using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using NightWarden.Stealth;
using NightWarden.Core;
using NightWarden.Player;

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
        [SerializeField] private float investigateSpeed = 3.2f;
        [SerializeField] private float chaseSpeed = 3.8f; // Non-gamer friendly (player walks 3.6f, sprints 6.4f)
        [SerializeField] private float searchDuration = 5.0f;
        [SerializeField] private float hearingThreshold = 0.20f;
        [SerializeField] private float attackRange = 1.6f;

        [Header("Vision & Detection Grace Period")]
        [SerializeField] private float visionRange = 12f;
        [SerializeField] private float visionFov = 75f;
        [SerializeField] private LayerMask visionObstacleLayers = ~0;
        [SerializeField] private float detectionTimeToChase = 2.8f; // 2.8s grace period before chase initiates
        [SerializeField] private float stunDuration = 4.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip takedownSound;
        [SerializeField] private AudioClip stunSound;

        private NavMeshAgent _agent;
        private Transform _playerTransform;
        private Vector3 _investigationTarget;
        private Vector3 _lastKnownPlayerPos;
        private int _waypointIndex = 0;
        private float _searchTimer = 0f;
        private bool _isWaiting = false;
        private bool _isEliminated = false;
        private bool _isStunned = false;
        private float _stunTimer = 0f;
        private float _detectionProgress = 0f;
        private Light _flashlight;

        public GuardState CurrentState => currentState;
        public bool IsEliminated => _isEliminated;
        public bool IsStunned => _isStunned;
        public float DetectionProgress => _detectionProgress;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = patrolSpeed;
            _agent.stoppingDistance = 0.5f;

            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            _flashlight = GetComponentInChildren<Light>();

            InitProceduralAudio();
        }

        private void InitProceduralAudio()
        {
            if (alertSound == null) alertSound = CreateToneClip("GuardAlert", 0.35f, 320f, 680f, 0.45f);
            if (takedownSound == null) takedownSound = CreateToneClip("GuardTakedown", 0.4f, 180f, 90f, 0.5f);
            if (stunSound == null) stunSound = CreateToneClip("GuardStun", 0.6f, 500f, 120f, 0.4f);
        }

        private AudioClip CreateToneClip(string name, float duration, float startFreq, float endFreq, float volume)
        {
            int sampleRate = 44100;
            int count = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;
                float freq = Mathf.Lerp(startFreq, endFreq, p);
                float envelope = Mathf.Sin(p * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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

            // Handle Stun state
            if (_isStunned)
            {
                _stunTimer -= Time.deltaTime;
                _agent.isStopped = true;
                if (_flashlight != null)
                {
                    _flashlight.intensity = (Mathf.PingPong(Time.time * 8f, 1f) > 0.5f) ? 0.3f : 1.2f;
                    _flashlight.color = new Color(0.4f, 0.6f, 1.0f); // Cold electric blue during stun
                }

                if (_stunTimer <= 0f)
                {
                    _isStunned = false;
                    _detectionProgress = 0f;
                    _agent.isStopped = false;
                    if (_flashlight != null)
                    {
                        _flashlight.intensity = 2.4f;
                        _flashlight.color = new Color(1f, 0.85f, 0.4f);
                    }
                    TransitionTo(GuardState.Search);
                }
                return;
            }

            _agent.isStopped = false;

            // Continual vision & gradual detection check
            bool seesPlayer = CanSeePlayer();
            if (seesPlayer)
            {
                bool isCrouched = ThirdPersonPlayerController.Instance != null && ThirdPersonPlayerController.Instance.IsCrouching;
                float rate = isCrouched ? 0.45f : 1.0f; // 55% slower detection when crouched
                _detectionProgress = Mathf.Clamp01(_detectionProgress + (rate * Time.deltaTime / detectionTimeToChase));

                if (_detectionProgress >= 1.0f && currentState != GuardState.Chase)
                {
                    TransitionTo(GuardState.Chase);
                }
            }
            else
            {
                if (currentState != GuardState.Chase)
                {
                    _detectionProgress = Mathf.Max(0f, _detectionProgress - Time.deltaTime * 0.5f);
                }
            }

            // Update Flashlight color based on awareness state
            UpdateFlashlightColor();

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
                    // Second chance check: Smoke Pellet
                    if (GameManager.Instance != null && GameManager.Instance.TryDeploySmokePellet())
                    {
                        StunGuard(stunDuration);
                        // Knockback guard away from player
                        Vector3 pushDir = (transform.position - _playerTransform.position);
                        pushDir.y = 0;
                        if (pushDir.sqrMagnitude < 0.01f) pushDir = -transform.forward;
                        Vector3 knockback = transform.position + pushDir.normalized * 3.2f;
                        if (NavMesh.SamplePosition(knockback, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                        {
                            transform.position = hit.position;
                        }
                    }
                    else
                    {
                        GameManager.Instance?.TriggerGameOver("You were apprehended by an armed guard.");
                    }
                }
            }
        }

        private void UpdateFlashlightColor()
        {
            if (_flashlight == null) return;

            if (currentState == GuardState.Chase)
            {
                _flashlight.color = new Color(1.0f, 0.15f, 0.15f); // Red
                _flashlight.intensity = 2.8f;
            }
            else if (currentState == GuardState.Investigate)
            {
                _flashlight.color = new Color(1.0f, 0.6f, 0.1f); // Orange
                _flashlight.intensity = 2.5f;
            }
            else
            {
                if (_detectionProgress > 0.05f)
                {
                    // Smoothly transition from yellow to red as detection fills
                    _flashlight.color = Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(1.0f, 0.15f, 0.15f), _detectionProgress);
                    _flashlight.intensity = 2.4f + _detectionProgress * 0.6f;
                }
                else
                {
                    _flashlight.color = new Color(1f, 0.88f, 0.45f); // Soft warm ambient yellow
                    _flashlight.intensity = 2.4f;
                }
            }
        }

        public void StunGuard(float duration)
        {
            if (_isEliminated) return;

            _isStunned = true;
            _stunTimer = duration;
            _detectionProgress = 0f;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            if (audioSource != null && stunSound != null)
            {
                audioSource.PlayOneShot(stunSound);
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
