using UnityEngine;
using UnityEngine.UI;
using NightWarden.Core;
using NightWarden.SLAM;
using NightWarden.Stealth;
using NightWarden.Player;

namespace NightWarden.UI
{
    /// <summary>
    /// In-game HUD controller.
    /// Displays the echo pulse battery bar, shared bomb timer, bomb counter,
    /// dynamic noise level meter, contextual interaction prompts, and endgame overlays.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Telemetry Bars & Text")]
        [SerializeField] private Slider batterySlider;
        [SerializeField] private Slider noiseMeterSlider;
        [SerializeField] private Text bombTimerText;
        [SerializeField] private Text bombCountText;
        [SerializeField] private Text promptText;

        [Header("Menu Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Text gameOverReasonText;
        [SerializeField] private Text victoryStatsText;

        private EchoPulse _echoPulse;
        private TakedownAction _takedownAction;

        private void Start()
        {
            _echoPulse = FindFirstObjectByType<EchoPulse>();
            _takedownAction = FindFirstObjectByType<TakedownAction>();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                GameManager.Instance.OnBombProgressChanged += HandleBombProgress;
                UpdateBombText(GameManager.Instance.BombsDefused, GameManager.Instance.TotalBombs);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnBombProgressChanged -= HandleBombProgress;
            }
        }

        private void Update()
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            // 1. Battery Gauge
            if (_echoPulse != null && batterySlider != null)
            {
                batterySlider.value = _echoPulse.BatteryNormalized;
            }

            // 2. Noise Meter
            if (NoiseSystem.Instance != null && noiseMeterSlider != null)
            {
                noiseMeterSlider.value = Mathf.Clamp01(NoiseSystem.Instance.CurrentDecibelLevel);
            }

            // 3. Shared Bomb Timer
            if (GameManager.Instance != null && bombTimerText != null)
            {
                float t = GameManager.Instance.TimeRemaining;
                int mins = Mathf.FloorToInt(t / 60f);
                int secs = Mathf.FloorToInt(t % 60f);
                bombTimerText.text = $"{mins:00}:{secs:00}";

                // Urgent color pulse when under 60 seconds
                if (t <= 60f)
                {
                    bombTimerText.color = (Mathf.PingPong(Time.time * 2f, 1f) > 0.5f) ? Color.red : Color.yellow;
                }
                else
                {
                    bombTimerText.color = Color.white;
                }
            }

            // 4. Contextual Prompts (Takedown or Defuse)
            if (promptText != null)
            {
                if (_takedownAction != null && _takedownAction.CanTakedown)
                {
                    promptText.text = "[F] SILENT TAKEDOWN";
                    promptText.color = Color.red;
                    promptText.gameObject.SetActive(true);
                }
                else
                {
                    promptText.text = "[Q] Echo Pulse  |  [Tab] MiniMap  |  [C] Crouch";
                    promptText.color = new Color(0.6f, 0.8f, 1.0f, 0.8f);
                    promptText.gameObject.SetActive(true);
                }
            }
        }

        private void HandleBombProgress(int defused, int total)
        {
            UpdateBombText(defused, total);
        }

        private void UpdateBombText(int defused, int total)
        {
            if (bombCountText != null)
            {
                bombCountText.text = $"BOMBS DEFUSED: {defused} / {total}";
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(state == GameState.GameOver);
                if (state == GameState.GameOver && gameOverReasonText != null && GameManager.Instance != null)
                {
                    gameOverReasonText.text = GameManager.Instance.EndGameReason;
                }
            }

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(state == GameState.Victory);
                if (state == GameState.Victory && victoryStatsText != null && GameManager.Instance != null)
                {
                    victoryStatsText.text = GameManager.Instance.EndGameReason;
                }
            }
        }

        public void OnResumeClicked()
        {
            GameManager.Instance?.SetGameState(GameState.Playing);
        }

        public void OnRetryClicked()
        {
            GameManager.Instance?.RestartMission();
        }

        public void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}
