using System;
using UnityEngine;
using NightWarden.Stealth;
using NightWarden.Core;
using NightWarden.Player;

namespace NightWarden.Objectives
{
    /// <summary>
    /// Interactive bomb objective.
    /// Player must approach and hold 'E' for 3.0 continuous seconds to defuse.
    /// Emits faint ticking noise that can be scanned as yellow points.
    /// </summary>
    public class BombObjective : MonoBehaviour
    {
        [Header("Defusal Settings")]
        [SerializeField] private float defuseTimeRequired = 2.2f;
        [SerializeField] private float interactionRange = 3.5f;

        [Header("Audio & Visual Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip defusedSound;
        [SerializeField] private Light hazardLight;

        private float _currentDefuseProgress = 0f;
        private bool _isDefused = false;
        private bool _isBeingDefused = false;
        private Transform _playerTransform;
        private float _lightPulseTimer = 0f;

        public bool IsDefused => _isDefused;
        public float DefuseProgressNormalized => Mathf.Clamp01(_currentDefuseProgress / defuseTimeRequired);
        public bool IsPlayerInRange { get; private set; }

        public void Configure(float timeRequired, float range)
        {
            defuseTimeRequired = timeRequired;
            interactionRange = range;
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (hazardLight == null) hazardLight = GetComponentInChildren<Light>();

            if (defusedSound == null)
            {
                defusedSound = CreateSuccessChimeClip();
            }
        }

        private AudioClip CreateSuccessChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int count = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[count];
            // 3-note ascending arpeggio: C5 (523Hz), E5 (659Hz), G5 (784Hz)
            float[] notes = { 523.25f, 659.25f, 783.99f };
            float noteDur = duration / notes.Length;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                int noteIdx = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / noteDur));
                float noteT = t - noteIdx * noteDur;
                float env = Mathf.Sin(Mathf.Clamp01(noteT / noteDur) * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * notes[noteIdx] * t) * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create("BombDefusedSuccess", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            if (_isDefused || _playerTransform == null) return;

            // Pulse Hazard Light to make bomb easily visible in the dark
            if (hazardLight != null)
            {
                _lightPulseTimer += Time.deltaTime * 3.5f;
                float intensityMultiplier = _isBeingDefused ? 2.5f : 1.0f;
                hazardLight.intensity = (0.8f + Mathf.PingPong(_lightPulseTimer, 0.8f)) * intensityMultiplier;
            }

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            IsPlayerInRange = distance <= interactionRange;

            if (IsPlayerInRange && WardenInput.GetDefuseHeld())
            {
                _isBeingDefused = true;
                _currentDefuseProgress += Time.deltaTime;

                // Subtle noise emission during defusal
                NoiseSystem.Instance?.EmitNoise(transform.position, 0.12f, NoiseType.BombDefusing);

                if (_currentDefuseProgress >= defuseTimeRequired)
                {
                    CompleteDefusal();
                }
            }
            else
            {
                _isBeingDefused = false;
                // Reset progress if key released or player moved away
                _currentDefuseProgress = Mathf.MoveTowards(_currentDefuseProgress, 0f, Time.deltaTime * 2.5f);
            }
        }

        private void CompleteDefusal()
        {
            _isDefused = true;
            _isBeingDefused = false;

            if (hazardLight != null)
            {
                hazardLight.color = new Color(0.1f, 1.0f, 0.4f); // Green safe beacon
                hazardLight.intensity = 0.5f;
            }

            if (audioSource != null && defusedSound != null)
            {
                audioSource.PlayOneShot(defusedSound);
            }

            // Notify GameManager
            GameManager.Instance?.OnBombDefused(this);
        }
    }
}
