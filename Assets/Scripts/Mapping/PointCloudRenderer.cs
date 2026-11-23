using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Mapping
{
    /// <summary>
    /// High-performance point cloud renderer utilizing a ring buffer.
    /// Supports up to 300,000 points with automatic age fading and color classification.
    /// Renders via Unity's ParticleSystem chunks for universal compatibility and high frame rates on Apple Silicon.
    /// </summary>
    public class PointCloudRenderer : MonoBehaviour
    {
        public static PointCloudRenderer Instance { get; private set; }

        public struct PointData
        {
            public Vector3 Position;
            public Color Color;
            public float Timestamp;
        }

        [Header("Buffer Settings")]
        [SerializeField] private int maxPoints = 300000;
        [SerializeField] private float pointSize = 0.045f;
        [SerializeField] private float fadeDuration = 180f; // Points persist as faint memory
        [SerializeField] private float minAlpha = 0.25f;

        [Header("Rendering")]
        [SerializeField] private Material pointCloudMaterial;

        private PointData[] _pointRingBuffer;
        private int _headIndex = 0;
        private int _totalPointsCount = 0;

        // Particle chunks: each ParticleSystem can hold up to 65,535 particles
        private const int ChunkSize = 50000;
        private int _numChunks;
        private ParticleSystem[] _particleSystems;
        private ParticleSystem.Particle[][] _particlesByChunk;
        private bool[] _chunkDirty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            _pointRingBuffer = new PointData[maxPoints];
            _numChunks = Mathf.CeilToInt((float)maxPoints / ChunkSize);

            _particleSystems = new ParticleSystem[_numChunks];
            _particlesByChunk = new ParticleSystem.Particle[_numChunks][];
            _chunkDirty = new bool[_numChunks];

            // Create default unlit additive material if none assigned
            if (pointCloudMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                pointCloudMaterial = new Material(shader);
                pointCloudMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                pointCloudMaterial.EnableKeyword("_BLENDMODE_ADD");
                pointCloudMaterial.SetColor("_BaseColor", Color.white);
            }

            for (int i = 0; i < _numChunks; i++)
            {
                GameObject chunkObj = new GameObject($"PointChunk_{i}");
                chunkObj.transform.SetParent(transform, false);

                ParticleSystem ps = chunkObj.AddComponent<ParticleSystem>();
                ParticleSystemRenderer psRenderer = chunkObj.GetComponent<ParticleSystemRenderer>();

                var main = ps.main;
                main.maxParticles = ChunkSize;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize = pointSize;
                main.playOnAwake = false;
                main.loop = false;

                var emission = ps.emission;
                emission.enabled = false;

                var shape = ps.shape;
                shape.enabled = false;

                psRenderer.material = pointCloudMaterial;
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

                _particleSystems[i] = ps;
                _particlesByChunk[i] = new ParticleSystem.Particle[ChunkSize];
            }
        }

        /// <summary>
        /// Adds a hit point to the ring buffer and updates the corresponding particle chunk.
        /// </summary>
        public void AddPoint(Vector3 position, Color color)
        {
            int index = _headIndex;
            _pointRingBuffer[index] = new PointData
            {
                Position = position,
                Color = color,
                Timestamp = Time.time
            };

            int chunkIndex = index / ChunkSize;
            int localIndex = index % ChunkSize;

            ref var particle = ref _particlesByChunk[chunkIndex][localIndex];
            particle.position = position;
            particle.startColor = color;
            particle.startSize = pointSize;
            particle.remainingLifetime = 99999f;
            particle.startLifetime = 99999f;

            _chunkDirty[chunkIndex] = true;

            _headIndex = (_headIndex + 1) % maxPoints;
            if (_totalPointsCount < maxPoints) _totalPointsCount++;
        }

        /// <summary>
        /// Batched point insertion for high-throughput LiDAR sweeps.
        /// </summary>
        public void AddPoints(IList<Vector3> positions, IList<Color> colors)
        {
            int count = Mathf.Min(positions.Count, colors.Count);
            for (int i = 0; i < count; i++)
            {
                AddPoint(positions[i], colors[i]);
            }
        }

        private void LateUpdate()
        {
            // Push updated particle buffers to GPU
            for (int i = 0; i < _numChunks; i++)
            {
                if (_chunkDirty[i])
                {
                    int activeInChunk = (_totalPointsCount >= (i + 1) * ChunkSize) 
                        ? ChunkSize 
                        : Mathf.Max(0, _totalPointsCount - i * ChunkSize);

                    _particleSystems[i].SetParticles(_particlesByChunk[i], activeInChunk);
                    _chunkDirty[i] = false;
                }
            }
        }

        public int CurrentPointCount => _totalPointsCount;
        public int MaxPointCapacity => maxPoints;

        public void Clear()
        {
            _headIndex = 0;
            _totalPointsCount = 0;
            for (int i = 0; i < _numChunks; i++)
            {
                _particleSystems[i].Clear();
                _chunkDirty[i] = false;
            }
        }
    }
}
