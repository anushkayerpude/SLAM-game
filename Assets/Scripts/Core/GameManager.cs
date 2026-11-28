using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightWarden.Objectives;
using NightWarden.Player;

namespace NightWarden.Core
{
    public enum GameState
    {
        Playing,
        Paused,
        GameOver,
        Victory
    }

    /// <summary>
    /// Master coordinator for Night Warden: Echo Protocol.
    /// Manages the shared 6-minute bomb timer, defusal counting, extraction point activation,
    /// and win/loss state transitions.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Mission Parameters")]
        [SerializeField] private float totalTimeSeconds = 600f; // 10:00 relaxed timer for non-gamers
        [SerializeField] private bool detectiveMode = false; // When true, timer is paused for casual free-roam
        [SerializeField] private int totalBombsRequired = 3;
        [SerializeField] private GameObject extractionPoint;

        [Header("Tactical Accessibility")]
        [SerializeField] private int maxSmokePellets = 3; // Second chances on guard contact
        private int _smokePelletsRemaining = 3;

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Playing;

        private float _timeRemaining;
        private int _bombsDefusedCount = 0;
        private string _endGameReason = "";

        public event Action<GameState> OnGameStateChanged;
        public event Action<int, int> OnBombProgressChanged; // (defused, total)
        public event Action<int> OnSmokePelletUsed; // (charges remaining)

        public GameState CurrentState => currentState;
        public float TimeRemaining => Mathf.Max(0f, _timeRemaining);
        public int BombsDefused => _bombsDefusedCount;
        public int TotalBombs => totalBombsRequired;
        public bool AreAllBombsDefused => _bombsDefusedCount >= totalBombsRequired;
        public string EndGameReason => _endGameReason;
        public int SmokePelletsRemaining => _smokePelletsRemaining;
        public bool IsDetectiveMode => detectiveMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _timeRemaining = totalTimeSeconds;
            if (extractionPoint != null) extractionPoint.SetActive(false);
        }

        private void Start()
        {
            SetGameState(GameState.Playing);
        }

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                if (!detectiveMode)
                {
                    _timeRemaining -= Time.deltaTime;

                    if (_timeRemaining <= 0f)
                    {
                        TriggerGameOver("The explosive devices detonated across the district.");
                    }
                }

                // Pause toggle on Escape
                if (WardenInput.GetEscapeDown())
                {
                    SetGameState(GameState.Paused);
                }
            }
            else if (currentState == GameState.Paused)
            {
                if (WardenInput.GetEscapeDown())
                {
                    SetGameState(GameState.Playing);
                }
            }
        }

        public bool TryDeploySmokePellet()
        {
            if (_smokePelletsRemaining > 0)
            {
                _smokePelletsRemaining--;
                OnSmokePelletUsed?.Invoke(_smokePelletsRemaining);
                return true;
            }
            return false;
        }

        public void RestoreSmokePellets(int count)
        {
            _smokePelletsRemaining = Mathf.Min(maxSmokePellets, _smokePelletsRemaining + count);
            OnSmokePelletUsed?.Invoke(_smokePelletsRemaining);
        }

        public void SetGameState(GameState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;

                case GameState.Paused:
                case GameState.GameOver:
                case GameState.Victory:
                    Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }

            OnGameStateChanged?.Invoke(currentState);
        }

        public void OnBombDefused(BombObjective bomb)
        {
            _bombsDefusedCount++;
            OnBombProgressChanged?.Invoke(_bombsDefusedCount, totalBombsRequired);

            if (_bombsDefusedCount >= totalBombsRequired)
            {
                ActivateExtraction();
            }
        }

        private void ActivateExtraction()
        {
            if (extractionPoint != null)
            {
                extractionPoint.SetActive(true);
            }
        }

        public void TriggerExtractionReached()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
            _endGameReason = "All bombs defused. Extraction successful. District secured.";
            SetGameState(GameState.Victory);
        }

        public void TriggerGameOver(string reason)
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
            _endGameReason = reason;
            SetGameState(GameState.GameOver);
        }

        public void RestartMission()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
