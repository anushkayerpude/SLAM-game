using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Mapping
{
    /// <summary>
    /// Generates and renders a top-down SLAM occupancy map from the 3D voxel grid.
    /// Projects a 2D slice onto a Texture2D, displaying the player pose arrow and objective beacons.
    /// Toggled via the Tab key on the handheld terminal.
    /// </summary>
    public class MapDisplay : MonoBehaviour
    {
        [Header("UI Binding")]
        [SerializeField] private RawImage mapRawImage;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private GameObject mapTerminalPanel;

        [Header("Map Parameters")]
        [SerializeField] private int textureResolution = 256;
        [SerializeField] private float mapWorldSpan = 60f; // Total world units covered across the texture
        [SerializeField] private float sliceHeightTolerance = 1.5f;
        [SerializeField] private float updateInterval = 0.25f;

        [Header("Color Styling")]
        [SerializeField] private Color unknownColor = new Color(0.01f, 0.02f, 0.04f, 1f);
        [SerializeField] private Color freeColor = new Color(0.05f, 0.12f, 0.18f, 0.75f);
        [SerializeField] private Color occupiedColor = new Color(0f, 1f, 0.8f, 1f);

        private Texture2D _mapTexture;
        private Color32[] _pixelBuffer;
        private float _lastUpdateTime;
        private Transform _playerTransform;
        private bool _isTerminalOpen = false;

        private void Start()
        {
            _mapTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _pixelBuffer = new Color32[textureResolution * textureResolution];
            ClearTextureBuffer();

            if (mapRawImage != null)
            {
                mapRawImage.texture = _mapTexture;
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }

            if (mapTerminalPanel != null)
            {
                mapTerminalPanel.SetActive(_isTerminalOpen);
            }
        }

        private void Update()
        {
            // Toggle handheld terminal map
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M))
            {
                ToggleTerminalMap();
            }

            if (_isTerminalOpen && Time.time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.time;
                RefreshMapTexture();
            }

            UpdatePlayerMarker();
        }

        public void ToggleTerminalMap()
        {
            _isTerminalOpen = !_isTerminalOpen;
            if (mapTerminalPanel != null)
            {
                mapTerminalPanel.SetActive(_isTerminalOpen);
            }

            if (_isTerminalOpen)
            {
                RefreshMapTexture();
            }
        }

        private void ClearTextureBuffer()
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

            ClearTextureBuffer();

            Vector3 playerPos = _playerTransform.position;
            float halfSpan = mapWorldSpan * 0.5f;
            float minX = playerPos.x - halfSpan;
            float minZ = playerPos.z - halfSpan;

            Color32 freeCol = freeColor;
            Color32 occCol = occupiedColor;

            var voxels = VoxelMap.Instance.GetAllVoxels();
            foreach (var kvp in voxels)
            {
                Vector3 worldPos = VoxelMap.Instance.VoxelToWorld(kvp.Key);

                // Height slicing: only include voxels near player height level
                if (Mathf.Abs(worldPos.y - playerPos.y) > sliceHeightTolerance) continue;

                // Map world position to texture pixel coordinates
                int px = Mathf.FloorToInt(((worldPos.x - minX) / mapWorldSpan) * textureResolution);
                int py = Mathf.FloorToInt(((worldPos.z - minZ) / mapWorldSpan) * textureResolution);

                if (px >= 0 && px < textureResolution && py >= 0 && py < textureResolution)
                {
                    int idx = py * textureResolution + px;
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
            if (!_isTerminalOpen || playerMarker == null || _playerTransform == null) return;

            // Player is centered on the local map; rotate marker to match player heading
            float yaw = _playerTransform.eulerAngles.y;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }
    }
}
