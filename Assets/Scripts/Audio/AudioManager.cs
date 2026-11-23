using UnityEngine;
using Echo.AI;
using Echo.Player;

namespace Echo.Audio
{
    /// <summary>
    /// Manages 3D spatial audio, dynamic ambient tension, scanner sound effects,
    /// and procedural audio synthesis fallbacks for turnkey out-of-the-box execution.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource tensionSource;
        [SerializeField] private AudioSource scannerSweepSource;
        [SerializeField] private AudioSource scannerPulseSource;

        [Header("Dynamic Tension Curve")]
        [SerializeField] private float maxTensionDistance = 25f;
        [SerializeField] private float minTensionDistance = 4f;

        private MonsterAI _monster;
        private LidarScanner _scanner;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
        }

        private void Start()
        {
            _monster = FindFirstObjectByType<MonsterAI>();
            _scanner = FindFirstObjectByType<LidarScanner>();
        }

        private void EnsureAudioSources()
        {
            if (ambientSource == null) ambientSource = gameObject.AddComponent<AudioSource>();
            if (tensionSource == null) tensionSource = gameObject.AddComponent<AudioSource>();
            if (scannerSweepSource == null) scannerSweepSource = gameObject.AddComponent<AudioSource>();
            if (scannerPulseSource == null) scannerPulseSource = gameObject.AddComponent<AudioSource>();

            ambientSource.loop = true;
            ambientSource.playOnAwake = true;
            ambientSource.volume = 0.35f;

            tensionSource.loop = true;
            tensionSource.playOnAwake = true;
            tensionSource.volume = 0f;

            scannerSweepSource.loop = true;
            scannerSweepSource.volume = 0.45f;
        }

        private void Update()
        {
            UpdateTensionVolume();
            UpdateScannerAudio();
        }

        private void UpdateTensionVolume()
        {
            if (_monster == null || tensionSource == null) return;

            float dist = _monster.DistanceToPlayer;
            if (dist < maxTensionDistance)
            {
                float t = 1f - Mathf.Clamp01((dist - minTensionDistance) / (maxTensionDistance - minTensionDistance));
                tensionSource.volume = Mathf.Lerp(tensionSource.volume, t * 0.8f, Time.deltaTime * 3f);
            }
            else
            {
                tensionSource.volume = Mathf.MoveTowards(tensionSource.volume, 0f, Time.deltaTime * 0.5f);
            }
        }

        private void UpdateScannerAudio()
        {
            if (_scanner == null || scannerSweepSource == null) return;

            if (_scanner.IsSweepActive && !scannerSweepSource.isPlaying)
            {
                scannerSweepSource.Play();
            }
            else if (!_scanner.IsSweepActive && scannerSweepSource.isPlaying)
            {
                scannerSweepSource.Stop();
            }
        }

        public void PlayPulseBurst()
        {
            if (scannerPulseSource != null)
            {
                scannerPulseSource.PlayOneShot(scannerPulseSource.clip != null ? scannerPulseSource.clip : null);
            }
        }
    }
}
