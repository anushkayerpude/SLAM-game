using System;
using System.Collections.Generic;
using UnityEngine;
using Echo.AI;

namespace Echo.Gameplay
{
    /// <summary>
    /// Tracks mission progression: discovering and inserting fuses, restoring generator power,
    /// triggering facility emergency lighting, and unlocking the exit door.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Objective Setup")]
        [SerializeField] private int requiredFuses = 3;
        [SerializeField] private float interactionDistance = 2.4f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Facility Power Systems")]
        [SerializeField] private GameObject[] emergencyLights;
        [SerializeField] private GameObject exitDoor;
        [SerializeField] private Transform generatorTransform;
        [SerializeField] private AudioSource generatorHumSource;

        private int _fusesCarried = 0;
        private int _fusesInserted = 0;
        private bool _isPowerRestored = false;
        private Camera _playerCamera;

        public event Action<int, int> OnFuseCountChanged; // (carried, inserted)
        public event Action OnPowerRestored;
        public event Action OnEscapeSuccess;

        public int FusesCarried => _fusesCarried;
        public int FusesInserted => _fusesInserted;
        public int RequiredFuses => requiredFuses;
        public bool IsPowerRestored => _isPowerRestored;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _playerCamera = Camera.main;

            // Ensure emergency lights are off initially
            SetEmergencyLights(false);
        }

        private void Start()
        {
            if (_playerCamera == null)
            {
                _playerCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void Update()
        {
            HandleInteractionRaycast();
        }

        private void HandleInteractionRaycast()
        {
            if (_playerCamera == null) return;

            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
            {
                // Inspecting a loose fuse
                if (hit.collider.CompareTag("Fuse"))
                {
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        CollectFuse(hit.collider.gameObject);
                    }
                }
                // Inspecting the central generator
                else if (hit.collider.CompareTag("Generator"))
                {
                    if (Input.GetKeyDown(KeyCode.E) && _fusesCarried > 0)
                    {
                        InsertFusesIntoGenerator();
                    }
                }
                // Inspecting the exit door
                else if (hit.collider.CompareTag("ExitDoor") || hit.collider.gameObject == exitDoor)
                {
                    if (Input.GetKeyDown(KeyCode.E) && _isPowerRestored)
                    {
                        TriggerEscape();
                    }
                }
            }
        }

        private void CollectFuse(GameObject fuseObj)
        {
            _fusesCarried++;
            Destroy(fuseObj);

            OnFuseCountChanged?.Invoke(_fusesCarried, _fusesInserted);

            // Emit subtle interaction sound/noise
            NoiseSystem.Instance?.EmitNoise(transform.position, 0.15f, NoiseType.ObjectDrop);
        }

        private void InsertFusesIntoGenerator()
        {
            _fusesInserted += _fusesCarried;
            _fusesCarried = 0;

            OnFuseCountChanged?.Invoke(_fusesCarried, _fusesInserted);

            if (_fusesInserted >= requiredFuses && !_isPowerRestored)
            {
                RestoreFacilityPower();
            }
        }

        private void RestoreFacilityPower()
        {
            _isPowerRestored = true;

            // Turn on emergency amber/red lights
            SetEmergencyLights(true);

            // Activate generator hum sound and broadcast noise
            if (generatorHumSource != null)
            {
                generatorHumSource.Play();
            }

            Vector3 genPos = generatorTransform != null ? generatorTransform.position : transform.position;
            NoiseSystem.Instance?.EmitNoise(genPos, 0.95f, NoiseType.GeneratorActivated);

            // Escalate Monster speed & aggression
            var monster = FindFirstObjectByType<MonsterAI>();
            if (monster != null)
            {
                monster.SetDifficultyParameters(7.2f, 1.4f);
            }

            OnPowerRestored?.Invoke();
        }

        private void SetEmergencyLights(bool active)
        {
            if (emergencyLights == null) return;
            foreach (var lightObj in emergencyLights)
            {
                if (lightObj != null) lightObj.SetActive(active);
            }
        }

        public void TriggerEscape()
        {
            OnEscapeSuccess?.Invoke();
            GameManager.Instance?.TriggerVictory();
        }

        public void SetRequiredFuses(int count)
        {
            requiredFuses = count;
        }
    }
}
