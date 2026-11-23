using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.AI
{
    public enum NoiseType
    {
        ScanSweep,
        ScanPulse,
        FootstepWalk,
        FootstepSprint,
        FootstepCrouch,
        ObjectDrop,
        GeneratorActivated
    }

    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Loudness; // 0.0 to 1.0+ normalized intensity
        public NoiseType Type;
        public float Timestamp;
    }

    /// <summary>
    /// Centralized acoustic propagation system.
    /// Captures scanner pulses, footsteps, and environmental noises,
    /// notifying AI listeners (such as MonsterAI) within hearing radius.
    /// </summary>
    public class NoiseSystem : MonoBehaviour
    {
        public static NoiseSystem Instance { get; private set; }

        public event Action<NoiseEvent> OnNoiseEmitted;

        [Header("Acoustic Propagation Parameters")]
        [SerializeField] private float baseAudibilityRadius = 35f;

        private NoiseEvent _lastLoudestNoise;
        private float _currentNoiseLevel = 0f;

        public float CurrentNoiseLevel => _currentNoiseLevel;
        public NoiseEvent LastLoudestNoise => _lastLoudestNoise;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Decay current noise meter for UI display
            if (_currentNoiseLevel > 0f)
            {
                _currentNoiseLevel = Mathf.MoveTowards(_currentNoiseLevel, 0f, Time.deltaTime * 1.5f);
            }
        }

        /// <summary>
        /// Broadcasts an acoustic event across the facility.
        /// </summary>
        public void EmitNoise(Vector3 position, float loudness, NoiseType type)
        {
            var evt = new NoiseEvent
            {
                Position = position,
                Loudness = loudness,
                Type = type,
                Timestamp = Time.time
            };

            _currentNoiseLevel = Mathf.Max(_currentNoiseLevel, loudness);

            if (loudness >= _lastLoudestNoise.Loudness || Time.time - _lastLoudestNoise.Timestamp > 2f)
            {
                _lastLoudestNoise = evt;
            }

            OnNoiseEmitted?.Invoke(evt);
        }

        /// <summary>
        /// Calculates the perceived loudness of a noise event at a listener's position.
        /// </summary>
        public float CalculatePerceivedNoise(Vector3 listenerPosition, NoiseEvent evt, float hearingMultiplier = 1f)
        {
            float distance = Vector3.Distance(listenerPosition, evt.Position);
            float maxHearDist = baseAudibilityRadius * evt.Loudness * hearingMultiplier;

            if (distance >= maxHearDist) return 0f;

            // Quadratic acoustic falloff
            float falloff = 1f - (distance / maxHearDist);
            return evt.Loudness * falloff * falloff;
        }
    }
}
