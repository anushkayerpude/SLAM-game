using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Echo.AI;
using Echo.Player;

namespace Echo.Gameplay
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// Master GameManager orchestrating game loop states, difficulty configurations,
    /// and scene restarts.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Playing;
        [SerializeField] private DifficultyLevel currentDifficulty = DifficultyLevel.Normal;

        public event Action<GameState> OnGameStateChanged;
        public event Action<DifficultyLevel> OnDifficultyChanged;

        public GameState CurrentState => currentState;
        public DifficultyLevel CurrentDifficulty => currentDifficulty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ApplyDifficulty(currentDifficulty);
            SetGameState(GameState.Playing);
        }

        private void Update()
        {
            // Toggle pause menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentState == GameState.Playing)
                {
                    SetGameState(GameState.Paused);
                }
                else if (currentState == GameState.Paused)
                {
                    SetGameState(GameState.Playing);
                }
            }
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
                case GameState.MainMenu:
                case GameState.GameOver:
                case GameState.Victory:
                    Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }

            OnGameStateChanged?.Invoke(currentState);
        }

        public void ApplyDifficulty(DifficultyLevel difficulty)
        {
            currentDifficulty = difficulty;

            var monster = FindFirstObjectByType<MonsterAI>();
            var scanner = FindFirstObjectByType<LidarScanner>();
            var objectives = FindFirstObjectByType<ObjectiveManager>();

            switch (difficulty)
            {
                case DifficultyLevel.Easy:
                    monster?.SetDifficultyParameters(4.8f, 0.75f);
                    scanner?.SetDifficultyParameters(25f, 0.8f);
                    objectives?.SetRequiredFuses(3);
                    break;

                case DifficultyLevel.Normal:
                    monster?.SetDifficultyParameters(6.0f, 1.0f);
                    scanner?.SetDifficultyParameters(20f, 1.0f);
                    objectives?.SetRequiredFuses(3);
                    break;

                case DifficultyLevel.Hard:
                    monster?.SetDifficultyParameters(7.2f, 1.4f);
                    scanner?.SetDifficultyParameters(15f, 1.3f);
                    objectives?.SetRequiredFuses(4);
                    break;
            }

            OnDifficultyChanged?.Invoke(currentDifficulty);
        }

        public void TriggerGameOver()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
            SetGameState(GameState.GameOver);
        }

        public void TriggerVictory()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
            SetGameState(GameState.Victory);
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
