using UnityEngine;
using UnityEngine.UI;
using Echo.Gameplay;
using Echo.Player;
using Echo.AI;

namespace Echo.UI
{
    /// <summary>
    /// Master UI Manager controlling the minimal in-game HUD (battery bar, objective counter, noise meter),
    /// tutorial prompts, pause menu, and game over/victory overlays.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD Elements")]
        [SerializeField] private Slider batterySlider;
        [SerializeField] private Slider noiseMeterSlider;
        [SerializeField] private Text fusesText;
        [SerializeField] private Text generatorStatusText;
        [SerializeField] private GameObject crosshair;

        [Header("Menu Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;

        [Header("Tutorial Prompts")]
        [SerializeField] private GameObject tutorialPrompt;
        [SerializeField] private float tutorialDisplayDuration = 6.0f;

        private LidarScanner _scanner;
        private ObjectiveManager _objectives;

        private void Start()
        {
            _scanner = FindFirstObjectByType<LidarScanner>();
            _objectives = FindFirstObjectByType<ObjectiveManager>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            if (_objectives != null)
            {
                _objectives.OnFuseCountChanged += HandleFuseCountChanged;
                _objectives.OnPowerRestored += HandlePowerRestored;
            }

            UpdateFuseUI(0, 0);

            if (tutorialPrompt != null)
            {
                tutorialPrompt.SetActive(true);
                Destroy(tutorialPrompt, tutorialDisplayDuration);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Update()
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (_scanner != null && batterySlider != null)
            {
                batterySlider.value = _scanner.BatteryNormalized;
            }

            if (NoiseSystem.Instance != null && noiseMeterSlider != null)
            {
                noiseMeterSlider.value = Mathf.Clamp01(NoiseSystem.Instance.CurrentNoiseLevel);
            }
        }

        private void HandleFuseCountChanged(int carried, int inserted)
        {
            UpdateFuseUI(carried, inserted);
        }

        private void UpdateFuseUI(int carried, int inserted)
        {
            if (fusesText != null && _objectives != null)
            {
                fusesText.text = $"FUSES: {inserted}/{_objectives.RequiredFuses} (Carrying: {carried})";
            }
        }

        private void HandlePowerRestored()
        {
            if (generatorStatusText != null)
            {
                generatorStatusText.text = "GENERATOR: ONLINE (EXIT UNLOCKED)";
                generatorStatusText.color = Color.green;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(state == GameState.Paused);
            if (gameOverPanel != null) gameOverPanel.SetActive(state == GameState.GameOver);
            if (victoryPanel != null) victoryPanel.SetActive(state == GameState.Victory);
        }

        public void OnResumeClicked()
        {
            GameManager.Instance?.SetGameState(GameState.Playing);
        }

        public void OnRestartClicked()
        {
            GameManager.Instance?.RestartGame();
        }

        public void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}
