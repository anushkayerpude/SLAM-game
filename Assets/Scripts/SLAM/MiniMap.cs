using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightWarden.Player;

namespace NightWarden.SLAM
{
    /// <summary>
    /// Top-down 2D MiniMap projection generated in real time from 3D VoxelMap data.
    /// Toggled via Tab key. Displays player pose arrow, active/defused bombs, and extraction point.
    /// </summary>
    public class MiniMap : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private GameObject minimapPanel;

        [Header("Map Settings")]
        [SerializeField] private int resolution = 256;
        [SerializeField] private float mapSpanMeters = 80f;
        [SerializeField] private float heightSliceTolerance = 2.0f;
        [SerializeField] private float updateInterval = 0.2f;

        [Header("Styling")]
        [SerializeField] private Color unknownColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        [SerializeField] private Color freeColor = new Color(0.12f, 0.22f, 0.35f, 0.8f);
        [SerializeField] private Color occupiedColor = new Color(0.4f, 0.8f, 1.0f, 1f);

        private Texture2D _mapTexture;
        private Color32[] _pixelBuffer;
        private float _lastUpdateTime;
        private Transform _playerTransform;
        private bool _isOpen = false;

        private void Start()
        {
            _mapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _pixelBuffer = new Color32[resolution * resolution];
            ClearBuffer();

            if (minimapImage != null)
            {
                minimapImage.texture = _mapTexture;
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }

            if (minimapPanel != null)
            {
                minimapPanel.SetActive(_isOpen);
            }
        }

        private void Update()
        {
            if (WardenInput.GetMapToggleDown())
            {
                ToggleMiniMap();
            }

            if (_isOpen && Time.time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.time;
                RefreshMapTexture();
            }

            UpdatePlayerMarker();
        }

        public void ToggleMiniMap()
        {
            _isOpen = !_isOpen;
            if (minimapPanel != null)
            {
                minimapPanel.SetActive(_isOpen);
            }

            if (_isOpen)
            {
                RefreshMapTexture();
            }
        }

        private void ClearBuffer()
        {
            Color32 unk = unknownColor;
            for (int i = 0; i < _pixelBuffer.Length; i++)
            {
                _pixelBuffer[i] = unk;
            }
        }

        private void RefreshMapTexture()
        {
            if (VoxelMap.Instance == null || _playerTransform == null) return;

            ClearBuffer();

            Vector3 playerPos = _playerTransform.position;
            float halfSpan = mapSpanMeters * 0.5f;
            float minX = playerPos.x - halfSpan;
            float minZ = playerPos.z - halfSpan;

            Color32 freeCol = freeColor;
            Color32 occCol = occupiedColor;

            var voxels = VoxelMap.Instance.GetAllVoxels();
            foreach (var kvp in voxels)
            {
                Vector3 worldPos = VoxelMap.Instance.VoxelToWorld(kvp.Key);

                if (Mathf.Abs(worldPos.y - playerPos.y) > heightSliceTolerance) continue;

                int px = Mathf.FloorToInt(((worldPos.x - minX) / mapSpanMeters) * resolution);
                int py = Mathf.FloorToInt(((worldPos.z - minZ) / mapSpanMeters) * resolution);

                if (px >= 0 && px < resolution && py >= 0 && py < resolution)
                {
                    int idx = py * resolution + px;
                    if (kvp.Value == VoxelState.Occupied)
                    {
                        _pixelBuffer[idx] = occCol;
                    }
                    else if (kvp.Value == VoxelState.Free && _pixelBuffer[idx].a != occCol.a)
                    {
                        _pixelBuffer[idx] = freeCol;
                    }
                }
            }

            _mapTexture.SetPixels32(_pixelBuffer);
            _mapTexture.Apply(false);
        }

        private void UpdatePlayerMarker()
        {
            if (!_isOpen || playerMarker == null || _playerTransform == null) return;

            float yaw = _playerTransform.eulerAngles.y;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }
    }
}
