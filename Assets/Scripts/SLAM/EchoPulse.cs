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
        [SerializeField] private float maxRange = 30f;
        [SerializeField] private float waveSpeed = 30f; // Travels 30m over ~1 second
        [SerializeField] private float cooldownDuration = 3.0f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Battery & Energy")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float pulseBatteryCost = 25f;
        [SerializeField] private float batteryRechargeRate = 12f;

        [Header("Semantic Color Classification")]
        [SerializeField] private Color environmentColor = new Color(0.55f, 0.72f, 0.95f, 0.9f); // Blue-grey
        [SerializeField] private Color bombColor = new Color(1.0f, 0.85f, 0.1f, 1.0f);        // Yellow
        [SerializeField] private Color guardColor = new Color(1.0f, 0.15f, 0.2f, 1.0f);        // Red
        [SerializeField] private Color ledgeColor = new Color(0.0f, 0.9f, 1.0f, 1.0f);         // Cyan

        [Header("Audio & Wave VFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pulseSound;
        [SerializeField] private float noiseLoudness = 1.0f; // 20m guard hearing radius

        private float _currentBattery;
        private float _lastPulseTime = -99f;
        private bool _isPulseActive = false;

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
