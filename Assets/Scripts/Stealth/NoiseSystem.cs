using System;
using UnityEngine;

namespace NightWarden.Stealth
{
    public enum NoiseType
    {
        EchoPulse,
        FootstepSprint,
        FootstepWalk,
        FootstepCrouch,
        Takedown,
        BombDefusing
    }

    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Loudness; // 0.0 to 1.0 normalized
        public NoiseType Type;
        public float Timestamp;
    }

    /// <summary>
    /// Acoustic propagation system calculating realistic noise attenuation.
    /// Notifies AI guards when pulses, sprinting, or actions occur within their hearing range.
    /// </summary>
    public class NoiseSystem : MonoBehaviour
    {
        public static NoiseSystem Instance { get; private set; }

        public event Action<NoiseEvent> OnNoiseEmitted;

        [Header("Propagation Settings")]
        [SerializeField] private float baseRadius = 20f; // 20m for max loudness (EchoPulse)

        private float _currentDecibelLevel = 0f;
        private NoiseEvent _lastEvent;

        public float CurrentDecibelLevel => _currentDecibelLevel;
        public NoiseEvent LastEvent => _lastEvent;

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
            if (_currentDecibelLevel > 0f)
            {
                _currentDecibelLevel = Mathf.MoveTowards(_currentDecibelLevel, 0f, Time.deltaTime * 1.8f);
            }
        }

        public void EmitNoise(Vector3 position, float loudness, NoiseType type)
        {
            var evt = new NoiseEvent
            {
                Position = position,
                Loudness = loudness,
                Type = type,
                Timestamp = Time.time
            };

            _lastEvent = evt;
            _currentDecibelLevel = Mathf.Max(_currentDecibelLevel, loudness);

            OnNoiseEmitted?.Invoke(evt);
        }

        /// <summary>
        /// Computes perceived noise at listener position. Returns 0 if outside hearing range.
        /// </summary>
        public float CalculatePerceivedNoise(Vector3 listenerPosition, NoiseEvent evt, float hearingMultiplier = 1.0f)
        {
            float distance = Vector3.Distance(listenerPosition, evt.Position);
            float maxDistance = baseRadius * evt.Loudness * hearingMultiplier;

            if (distance >= maxDistance) return 0f;

            float falloff = 1f - (distance / maxDistance);
            return evt.Loudness * falloff * falloff;
        }
    }
}
