using System.Collections.Generic;
using UnityEngine;
using NightWarden.Objectives;
using NightWarden.Core;

namespace NightWarden.UI
{
    /// <summary>
    /// Tactical in-world objective waypoint tracker and navigation assist.
    /// Projects 3D markers and distance telemetry for active bombs and the extraction point
    /// directly onto the player's viewport, making mission goals immediately understandable.
    /// </summary>
    public class ObjectiveWaypointTracker : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showWaypoints = true;
        [SerializeField] private float maxMarkerDistance = 120f;
        [SerializeField] private Color bombMarkerColor = new Color(1.0f, 0.85f, 0.15f, 0.95f); // High-vis tactical yellow
        [SerializeField] private Color extractionMarkerColor = new Color(0.1f, 1.0f, 0.5f, 0.95f); // Bright extraction green/cyan
        [SerializeField] private Color defusedColor = new Color(0.4f, 0.5f, 0.6f, 0.6f);

        [Header("Banner Settings")]
        [SerializeField] private float bannerDuration = 7.0f;

        private Camera _mainCamera;
        private Transform _playerTransform;
        private List<BombObjective> _bombs = new List<BombObjective>();
        private ExtractionPoint _extraction;
        private float _bannerTimer = 0f;
        private GUIStyle _markerStyle;
        private GUIStyle _bgStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _defusePromptStyle;
        private GUIStyle _defuseProgressStyle;
        private Texture2D _bgTexture;
        private Texture2D _progressFgTexture;
        private Texture2D _progressBgTexture;

        private void Start()
        {
            _mainCamera = Camera.main;
            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;

            RefreshObjectives();
            _bannerTimer = bannerDuration;
            InitStyles();
        }

        private void InitStyles()
        {
            _bgTexture = MakeSolidTexture(2, 2, new Color(0.04f, 0.07f, 0.12f, 0.82f));
            _progressBgTexture = MakeSolidTexture(2, 2, new Color(0.1f, 0.15f, 0.22f, 0.88f));
            _progressFgTexture = MakeSolidTexture(2, 2, new Color(1.0f, 0.85f, 0.15f, 0.95f));

            _bgStyle = new GUIStyle
            {
                normal = { background = _bgTexture },
                alignment = TextAnchor.MiddleCenter
            };

            _markerStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _bannerStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.95f, 1.0f, 1.0f), background = _bgTexture }
            };

            _defusePromptStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.85f, 0.15f, 1.0f), background = _bgTexture }
            };
        }

        private Texture2D MakeSolidTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = color;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public void RefreshObjectives()
        {
            _bombs.Clear();
            _bombs.AddRange(FindObjectsByType<BombObjective>(FindObjectsSortMode.None));
            _extraction = FindFirstObjectByType<ExtractionPoint>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _playerTransform = player.transform;
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (!showWaypoints || _mainCamera == null || _playerTransform == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;

            DrawMissionBanner();
            DrawObjectiveWaypoints();
            DrawDefusalInteractionPrompt();
        }

        private void DrawMissionBanner()
        {
            if (_bannerTimer > 0f)
            {
                float alpha = Mathf.Clamp01(_bannerTimer / 1.0f);
                Color c = _bannerStyle.normal.textColor;
                c.a = alpha;
                _bannerStyle.normal.textColor = c;

                float width = 640f;
                float height = 44f;
                Rect rect = new Rect((Screen.width - width) * 0.5f, 75f, width, height);
                GUI.Box(rect, "MISSION: Track down & defuse 3 explosive devices. Press [Q] to scan.", _bannerStyle);
            }
        }

        private void DrawObjectiveWaypoints()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;
            float margin = 45f;

            // Draw Bomb Waypoints
            int bombIndex = 1;
            foreach (var bomb in _bombs)
            {
                if (bomb == null) continue;

                Vector3 bombPos = bomb.transform.position + Vector3.up * 0.75f;
                float distance = Vector3.Distance(_playerTransform.position, bomb.transform.position);

                if (distance > maxMarkerDistance) continue;

                Vector3 screenPos = _mainCamera.WorldToScreenPoint(bombPos);
                bool isBehind = screenPos.z < 0;

                if (isBehind)
                {
                    screenPos.x = screenW - screenPos.x;
                    screenPos.y = screenH - screenPos.y;
                }

                // Invert Y for GUI coordinates
                screenPos.y = screenH - screenPos.y;

                // Clamp to screen bounds with margin
                screenPos.x = Mathf.Clamp(screenPos.x, margin + 40f, screenW - margin - 40f);
                screenPos.y = Mathf.Clamp(screenPos.y, margin + 20f, screenH - margin - 20f);

                bool defused = bomb.IsDefused;
                Color col = defused ? defusedColor : bombMarkerColor;
                _markerStyle.normal.textColor = col;

                string label = defused ? $"✓ BOMB #{bombIndex} (DEFUSED)" : $"◆ BOMB #{bombIndex} [{distance:0}m]";
                Vector2 size = _markerStyle.CalcSize(new GUIContent(label)) + new Vector2(16, 8);
                Rect boxRect = new Rect(screenPos.x - size.x * 0.5f, screenPos.y - size.y * 0.5f, size.x, size.y);

                GUI.Box(boxRect, GUIContent.none, _bgStyle);
                GUI.Label(boxRect, label, _markerStyle);

                bombIndex++;
            }

            // Draw Extraction Waypoint if unlocked
            if (GameManager.Instance != null && GameManager.Instance.AreAllBombsDefused && _extraction != null)
            {
                Vector3 extPos = _extraction.transform.position + Vector3.up * 1.5f;
                float dist = Vector3.Distance(_playerTransform.position, _extraction.transform.position);

                Vector3 screenPos = _mainCamera.WorldToScreenPoint(extPos);
                if (screenPos.z < 0)
                {
                    screenPos.x = screenW - screenPos.x;
                    screenPos.y = screenH - screenPos.y;
                }
                screenPos.y = screenH - screenPos.y;
                screenPos.x = Mathf.Clamp(screenPos.x, margin + 50f, screenW - margin - 50f);
                screenPos.y = Mathf.Clamp(screenPos.y, margin + 25f, screenH - margin - 25f);

                _markerStyle.normal.textColor = extractionMarkerColor;
                string label = $"★ EXTRACTION POINT [{dist:0}m]";
                Vector2 size = _markerStyle.CalcSize(new GUIContent(label)) + new Vector2(20, 10);
                Rect boxRect = new Rect(screenPos.x - size.x * 0.5f, screenPos.y - size.y * 0.5f, size.x, size.y);

                GUI.Box(boxRect, GUIContent.none, _bgStyle);
                GUI.Label(boxRect, label, _markerStyle);
            }
        }

        private void DrawDefusalInteractionPrompt()
        {
            foreach (var bomb in _bombs)
            {
                if (bomb != null && !bomb.IsDefused && bomb.IsPlayerInRange)
                {
                    float width = 360f;
                    float height = 70f;
                    Rect centerRect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.65f, width, height);

                    GUI.Box(centerRect, GUIContent.none, _bgStyle);

                    Rect textRect = new Rect(centerRect.x, centerRect.y + 6f, centerRect.width, 26f);
                    GUI.Label(textRect, "HOLD [E] TO DEFUSE EXPLOSIVE", _defusePromptStyle);

                    // Progress Bar
                    float progress = bomb.DefuseProgressNormalized;
                    Rect barBg = new Rect(centerRect.x + 20f, centerRect.y + 38f, centerRect.width - 40f, 18f);
                    GUI.DrawTexture(barBg, _progressBgTexture);

                    Rect barFg = new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * progress, barBg.height - 4f);
                    GUI.DrawTexture(barFg, _progressFgTexture);

                    break;
                }
            }
        }
    }
}
