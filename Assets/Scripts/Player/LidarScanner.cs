using System.Collections.Generic;
using UnityEngine;
using Echo.Mapping;
using Echo.AI;

namespace Echo.Player
{
    public enum ScanMode
    {
        Sweep,
        Pulse
    }

    /// <summary>
    /// Handheld LiDAR scanner firing 3D rays and populating the PointCloudRenderer and VoxelMap.
    /// Implements continuous Sweep mode (cone scatter) and 360-degree radial Pulse mode.
    /// Manages battery energy consumption and acoustic noise generation.
    /// </summary>
    public class LidarScanner : MonoBehaviour
    {
        [Header("Scanner Configuration")]
        [SerializeField] private Transform raycastOrigin;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private float maxRange = 25f;

        [Header("Sweep Mode (Left Click)")]
        [SerializeField] private float sweepConeAngle = 18f;
        [SerializeField] private int sweepRaysPerSecond = 2000;
        [SerializeField] private float sweepNoiseLoudness = 0.25f;
        [SerializeField] private float sweepBatteryDrainPerSec = 12f;

        [Header("Pulse Mode (Right Click)")]
        [SerializeField] private int pulseRayCount = 240;
        [SerializeField] private float pulseRange = 30f;
        [SerializeField] private float pulseNoiseLoudness = 1.0f;
        [SerializeField] private float pulseBatteryCost = 25f;
        [SerializeField] private float pulseCooldown = 1.5f;

        [Header("Battery System")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float batteryRechargeRate = 8f; // When idle

        [Header("Color Classification")]
        [SerializeField] private Color wallColor = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color itemColor = new Color(0f, 0.85f, 1f, 1f);
        [SerializeField] private Color objectiveColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color monsterColor = new Color(1f, 0.15f, 0.25f, 1f);

        private float _currentBattery;
        private float _lastPulseTime = -99f;
        private Camera _playerCamera;
        private float _rayAccumulator = 0f;

        public float CurrentBattery => _currentBattery;
        public float MaxBattery => maxBattery;
        public float BatteryNormalized => Mathf.Clamp01(_currentBattery / maxBattery);
        public bool IsSweepActive { get; private set; }

        private void Awake()
        {
            _currentBattery = maxBattery;
            _playerCamera = GetComponentInChildren<Camera>();
            if (raycastOrigin == null && _playerCamera != null)
            {
                raycastOrigin = _playerCamera.transform;
            }
            if (raycastOrigin == null)
            {
                raycastOrigin = transform;
            }
        }

        private void Update()
        {
            HandleScannerInput();
            HandleBatteryRecharge();
        }

        private void HandleScannerInput()
        {
            IsSweepActive = false;

            // Sweep Mode: Hold Left Mouse Button
            if (Input.GetMouseButton(0) && _currentBattery > 0f)
            {
                IsSweepActive = true;
                ExecuteSweepMode();
            }

            // Pulse Mode: Press Right Mouse Button or Space
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space)) && 
                _currentBattery >= pulseBatteryCost && 
                Time.time - _lastPulseTime >= pulseCooldown)
            {
                ExecutePulseMode();
            }
        }

        private void ExecuteSweepMode()
        {
            float dt = Time.deltaTime;
            _currentBattery = Mathf.Max(0f, _currentBattery - sweepBatteryDrainPerSec * dt);

            _rayAccumulator += sweepRaysPerSecond * dt;
            int raysToCast = Mathf.FloorToInt(_rayAccumulator);
            _rayAccumulator -= raysToCast;

            Vector3 origin = raycastOrigin.position;
            Vector3 forward = raycastOrigin.forward;

            for (int i = 0; i < raysToCast; i++)
            {
                // Uniform random sampling within a cone
                Quaternion spread = Quaternion.AngleAxis(Random.Range(0f, sweepConeAngle), Random.insideUnitSphere.normalized);
                Vector3 rayDir = spread * forward;

                CastSingleRay(origin, rayDir, maxRange);
            }

            // Emit continuous low acoustic noise
            NoiseSystem.Instance?.EmitNoise(origin, sweepNoiseLoudness, NoiseType.ScanSweep);
        }

        private void ExecutePulseMode()
        {
            _lastPulseTime = Time.time;
            _currentBattery = Mathf.Max(0f, _currentBattery - pulseBatteryCost);

            Vector3 origin = raycastOrigin.position;

            // Fire 360-degree horizontal + spherical burst
            for (int i = 0; i < pulseRayCount; i++)
            {
                float azimuth = (float)i / pulseRayCount * Mathf.PI * 2f;
                float elevation = Random.Range(-0.35f, 0.35f); // Expanded vertical dispersion

                Vector3 rayDir = new Vector3(
                    Mathf.Cos(azimuth) * Mathf.Cos(elevation),
                    Mathf.Sin(elevation),
                    Mathf.Sin(azimuth) * Mathf.Cos(elevation)
                ).normalized;

                CastSingleRay(origin, rayDir, pulseRange);
            }

            // Emit loud spike noise
            NoiseSystem.Instance?.EmitNoise(origin, pulseNoiseLoudness, NoiseType.ScanPulse);
        }

        private void CastSingleRay(Vector3 origin, Vector3 direction, float range)
        {
            bool didHit = Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers);

            Vector3 hitPoint = didHit ? hit.point : (origin + direction * range);
            Color pointColor = wallColor;

            if (didHit)
            {
                // Classify color based on tag or layer
                if (hit.collider.CompareTag("Monster"))
                {
                    pointColor = monsterColor;
                }
                else if (hit.collider.CompareTag("Objective") || hit.collider.CompareTag("Fuse"))
                {
                    pointColor = objectiveColor;
                }
                else if (hit.collider.CompareTag("Item"))
                {
                    pointColor = itemColor;
                }

                PointCloudRenderer.Instance?.AddPoint(hitPoint, pointColor);
            }

            VoxelMap.Instance?.IntegrateRay(origin, hitPoint, didHit);
        }

        private void HandleBatteryRecharge()
        {
            if (!IsSweepActive && _currentBattery < maxBattery)
            {
                _currentBattery = Mathf.Min(maxBattery, _currentBattery + batteryRechargeRate * Time.deltaTime);
            }
        }

        public void SetDifficultyParameters(float range, float batteryDrainMultiplier)
        {
            maxRange = range;
            sweepBatteryDrainPerSec *= batteryDrainMultiplier;
        }
    }
}
