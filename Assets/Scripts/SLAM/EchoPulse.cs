using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightWarden.Stealth;
using NightWarden.Player;

namespace NightWarden.SLAM
{
    /// <summary>
    /// Echolocation LiDAR sensor mechanism for the Night Warden.
    /// Fires radial rays in a 360-degree sphere, expanding outward over 1 second.
    /// Performs semantic point coloring (Environment, Guards, Bombs, Ledges)
    /// and emits loud acoustic noise events that nearby guards investigate.
    /// </summary>
    public class EchoPulse : MonoBehaviour
    {
        [Header("Pulse Parameters")]
        [SerializeField] private int rayCount = 1500;
        [SerializeField] private float maxRange = 32f;
        [SerializeField] private float waveSpeed = 32f; // Travels 32m over ~1 second
        [SerializeField] private float cooldownDuration = 1.6f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Battery & Energy")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float pulseBatteryCost = 20f;
        [SerializeField] private float batteryRechargeRate = 18f;

        [Header("Semantic Color Classification")]
        [SerializeField] private Color environmentColor = new Color(0.55f, 0.72f, 0.95f, 0.9f); // Blue-grey
        [SerializeField] private Color bombColor = new Color(1.0f, 0.85f, 0.1f, 1.0f);        // Yellow
        [SerializeField] private Color guardColor = new Color(1.0f, 0.15f, 0.2f, 1.0f);        // Red
        [SerializeField] private Color ledgeColor = new Color(0.0f, 0.9f, 1.0f, 1.0f);         // Cyan

        [Header("Audio & Wave VFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pulseSound;
        [SerializeField] private float noiseLoudness = 0.30f; // Softened noise: ~7m guard hearing radius

        [Header("Passive Footstep Micro-Sonar (Accessibility)")]
        [SerializeField] private bool enablePassiveSonar = true;
        [SerializeField] private float passiveSonarInterval = 1.3f;
        [SerializeField] private float passiveSonarRange = 9.5f;
        [SerializeField] private int passiveSonarRays = 36;

        private float _currentBattery;
        private float _lastPulseTime = -99f;
        private bool _isPulseActive = false;
        private float _passiveSonarTimer = 0f;
        private Vector3 _lastPosition;

        public float CurrentBattery => _currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryNormalized => Mathf.Clamp01(_currentBattery / maxBattery);
        public float CooldownRemaining => Mathf.Max(0f, cooldownDuration - (Time.time - _lastPulseTime));
        public float CooldownNormalized => Mathf.Clamp01((Time.time - _lastPulseTime) / cooldownDuration);
        public bool CanPulse => CooldownRemaining <= 0f && _currentBattery >= pulseBatteryCost;

        private void Awake()
        {
            _currentBattery = maxBattery;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _lastPosition = transform.position;

            if (pulseSound == null)
            {
                pulseSound = CreateChirpClip("EchoPulseRadar", 0.32f, 820f, 240f);
            }
        }

        private AudioClip CreateChirpClip(string clipName, float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int count = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;
                float freq = Mathf.Lerp(startFreq, endFreq, p * p);
                float envelope = Mathf.Sin(p * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.42f;
            }
            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            // Trigger Echo Pulse on Q via unified input handler
            if (WardenInput.GetPulseDown() && CanPulse)
            {
                TriggerPulse();
            }

            // Battery recharge over time
            if (_currentBattery < maxBattery)
            {
                _currentBattery = Mathf.Min(maxBattery, _currentBattery + batteryRechargeRate * Time.deltaTime);
            }

            // Passive silent micro-sonar while moving
            if (enablePassiveSonar)
            {
                _passiveSonarTimer -= Time.deltaTime;
                float distMoved = Vector3.Distance(transform.position, _lastPosition);
                _lastPosition = transform.position;

                if (_passiveSonarTimer <= 0f && (distMoved > 0.04f || Input.anyKey))
                {
                    _passiveSonarTimer = passiveSonarInterval;
                    TriggerPassiveMicroSonar();
                }
            }
        }

        private void TriggerPassiveMicroSonar()
        {
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            float angleStep = 360f / passiveSonarRays;

            for (int i = 0; i < passiveSonarRays; i++)
            {
                float rad = i * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), -0.15f, Mathf.Sin(rad)).normalized;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, passiveSonarRange, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    Color pointColor = environmentColor;
                    if (hit.collider.CompareTag("Bomb")) pointColor = bombColor;
                    else if (hit.collider.CompareTag("Guard")) pointColor = guardColor;
                    else if (hit.collider.CompareTag("Ledge") || hit.collider.CompareTag("Climbable")) pointColor = ledgeColor;

                    PointCloudBuffer.Instance?.AddPoint(hit.point, pointColor);
                }
            }
        }

        public void TriggerPulse()
        {
            _lastPulseTime = Time.time;
            _currentBattery = Mathf.Max(0f, _currentBattery - pulseBatteryCost);

            Vector3 origin = transform.position + Vector3.up * 1.0f;

            // Emit acoustic noise event (20m hearing radius)
            NoiseSystem.Instance?.EmitNoise(origin, noiseLoudness, NoiseType.EchoPulse);

            // Play pulse sound
            if (audioSource != null && pulseSound != null)
            {
                audioSource.PlayOneShot(pulseSound);
            }

            // Execute expanding spherical raycast
            StartCoroutine(ExecuteExpandingPulseRoutine(origin));
        }

        private IEnumerator ExecuteExpandingPulseRoutine(Vector3 origin)
        {
            _isPulseActive = true;
            float startTime = Time.time;

            // Precompute raycast hits using Fibonacci Sphere distribution
            List<ScannedPoint> pendingPoints = new List<ScannedPoint>(rayCount);
            float phi = (1 + Mathf.Sqrt(5)) / 2; // Golden ratio

            for (int i = 0; i < rayCount; i++)
            {
                float theta = 2 * Mathf.PI * i / phi;
                float z = 1 - (2f * i + 1) / rayCount;
                float radius = Mathf.Sqrt(1 - z * z);

                Vector3 direction = new Vector3(
                    radius * Mathf.Cos(theta),
                    z,
                    radius * Mathf.Sin(theta)
                ).normalized;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    Color pointColor = environmentColor;

                    if (hit.collider.CompareTag("Bomb"))
                    {
                        pointColor = bombColor;
                    }
                    else if (hit.collider.CompareTag("Guard"))
                    {
                        pointColor = guardColor;
                    }
                    else if (hit.collider.CompareTag("Ledge") || hit.collider.CompareTag("Climbable"))
                    {
                        pointColor = ledgeColor;
                    }

                    pendingPoints.Add(new ScannedPoint
                    {
                        Position = hit.point,
                        Color = pointColor,
                        Distance = hit.distance
                    });

                    // Integrate into VoxelMap
                    VoxelMap.Instance?.IntegrateRay(origin, hit.point, true);
                }
                else
                {
                    Vector3 endPoint = origin + direction * maxRange;
                    VoxelMap.Instance?.IntegrateRay(origin, endPoint, false);
                }
            }

            // Reveal points progressively as the wavefront expands outwards
            float maxTime = maxRange / waveSpeed;
            float elapsed = 0f;
            int pointIdx = 0;

            // Sort points by distance for natural radial wave expansion
            pendingPoints.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            while (elapsed < maxTime && pointIdx < pendingPoints.Count)
            {
                elapsed = Time.time - startTime;
                float currentWaveDistance = elapsed * waveSpeed;

                while (pointIdx < pendingPoints.Count && pendingPoints[pointIdx].Distance <= currentWaveDistance)
                {
                    var pt = pendingPoints[pointIdx];
                    PointCloudBuffer.Instance?.AddPoint(pt.Position, pt.Color);
                    pointIdx++;
                }

                yield return null;
            }

            // Flush any remaining points
            while (pointIdx < pendingPoints.Count)
            {
                var pt = pendingPoints[pointIdx];
                PointCloudBuffer.Instance?.AddPoint(pt.Position, pt.Color);
                pointIdx++;
            }

            _isPulseActive = false;
        }

        private struct ScannedPoint
        {
            public Vector3 Position;
            public Color Color;
            public float Distance;
        }
    }
}
