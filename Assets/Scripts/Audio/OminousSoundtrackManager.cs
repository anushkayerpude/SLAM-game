using System.Collections.Generic;
using UnityEngine;
using NightWarden.Core;
using NightWarden.AI;

namespace NightWarden.Audio
{
    /// <summary>
    /// Master coordinator for dark, ominous ambient background music in Night Warden: Echo Protocol.
    /// Manages seamless looping, 2D spatialization, and real-time dynamic tension scaling:
    /// subtle ominous stealth drone when undetected, accelerating heartbeat and pitch during detection,
    /// and full dark vigilante pulse during active pursuit.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class OminousSoundtrackManager : MonoBehaviour
    {
        public static OminousSoundtrackManager Instance { get; private set; }

        [Header("Audio Configuration")]
        [SerializeField] private AudioClip ambientMusicClip;
        [SerializeField] private float baseVolume = 0.55f;
        [SerializeField] private float detectionVolume = 0.72f;
        [SerializeField] private float chaseVolume = 0.88f;
        [SerializeField] private float volumeTransitionSpeed = 1.8f;
        [SerializeField] private float pitchTransitionSpeed = 1.2f;

        private AudioSource _audioSource;
        private List<GuardAI> _activeGuards = new List<GuardAI>();
        private float _targetVolume = 0.55f;
        private float _targetPitch = 1.0f;

        public AudioSource Source => _audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 0.0f; // 2D global background track
            _audioSource.priority = 48; // High priority, never culled by SFX

            if (ambientMusicClip != null)
            {
                _audioSource.clip = ambientMusicClip;
            }
            else
            {
                // Fallback: generate procedural ambient drone if no clip is assigned
                _audioSource.clip = GenerateProceduralAmbientDrone();
            }

            _audioSource.volume = baseVolume;
            _audioSource.pitch = 1.0f;
            _audioSource.Play();
        }

        private void Start()
        {
            RefreshGuardCache();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        public void RefreshGuardCache()
        {
            _activeGuards.Clear();
            _activeGuards.AddRange(FindObjectsByType<GuardAI>(FindObjectsSortMode.None));
        }

        private void Update()
        {
            UpdateTensionLevel();

            // Smoothly interpolate volume and pitch towards target
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, _targetVolume, volumeTransitionSpeed * Time.deltaTime);
                _audioSource.pitch = Mathf.MoveTowards(_audioSource.pitch, _targetPitch, pitchTransitionSpeed * Time.deltaTime);
            }
        }

        private void UpdateTensionLevel()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.GameOver)
                {
                    _targetVolume = 0.20f;
                    _targetPitch = 0.70f;
                    return;
                }
                if (GameManager.Instance.CurrentState == GameState.Victory)
                {
                    _targetVolume = 0.35f;
                    _targetPitch = 1.0f;
                    return;
                }
                if (GameManager.Instance.CurrentState == GameState.Paused)
                {
                    _targetVolume = baseVolume * 0.5f;
                    _targetPitch = 0.95f;
                    return;
                }
            }

            bool anyGuardInChase = false;
            float maxDetectionProgress = 0f;

            for (int i = 0; i < _activeGuards.Count; i++)
            {
                var guard = _activeGuards[i];
                if (guard == null || guard.IsEliminated) continue;

                if (guard.CurrentState == GuardState.Chase)
                {
                    anyGuardInChase = true;
                    break;
                }

                if (guard.DetectionProgress > maxDetectionProgress)
                {
                    maxDetectionProgress = guard.DetectionProgress;
                }
            }

            if (anyGuardInChase)
            {
                _targetVolume = chaseVolume;
                _targetPitch = 1.06f; // Elevated pitch accelerates tension pulse
            }
            else if (maxDetectionProgress > 0.15f)
            {
                _targetVolume = Mathf.Lerp(baseVolume, detectionVolume, maxDetectionProgress);
                _targetPitch = Mathf.Lerp(1.0f, 1.03f, maxDetectionProgress);
            }
            else
            {
                _targetVolume = baseVolume;
                _targetPitch = 1.0f;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            UpdateTensionLevel();
        }

        /// <summary>
        /// Generates a smooth, seamless 16-second D-minor sub-bass ambient drone in memory
        /// if no external WAV asset is present.
        /// </summary>
        private AudioClip GenerateProceduralAmbientDrone()
        {
            int sampleRate = 44100;
            float duration = 16.0f;
            int count = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[count];

            float d1 = 36.71f;
            float d2 = 73.42f;
            float a2 = 110.0f;
            float f3 = 174.61f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                // Seamless cyclic phase
                float phaseMod = t / duration * 2.0f * Mathf.PI;
                float lfo = 0.5f + 0.5f * Mathf.Sin(phaseMod);

                float s1 = Mathf.Sin(2f * Mathf.PI * d1 * t) * 0.35f;
                float s2 = Mathf.Sin(2f * Mathf.PI * d2 * t) * 0.25f;
                float s3 = Mathf.Sin(2f * Mathf.PI * a2 * t) * (0.15f * lfo);
                float s4 = Mathf.Sin(2f * Mathf.PI * f3 * t) * (0.10f * (1f - lfo));

                samples[i] = (s1 + s2 + s3 + s4) * 0.7f;
            }

            AudioClip clip = AudioClip.Create("ProceduralOminousDrone", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void SetAudioClip(AudioClip clip)
        {
            ambientMusicClip = clip;
            if (_audioSource != null)
            {
                _audioSource.clip = clip;
                if (!_audioSource.isPlaying) _audioSource.Play();
            }
        }
    }
}
