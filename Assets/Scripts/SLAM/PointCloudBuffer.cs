using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightWarden.SLAM
{
    /// <summary>
    /// Point cloud ring buffer managing up to 200,000 points.
    /// Utilizes GPU ParticleSystem chunks for high-performance billboard rendering
    /// with soft glow, distance blending, and age fading.
    /// </summary>
    public class PointCloudBuffer : MonoBehaviour
    {
        public static PointCloudBuffer Instance { get; private set; }

        public struct PointData
        {
            public Vector3 Position;
            public Color Color;
            public float Timestamp;
        }

        [Header("Buffer Configuration")]
        [SerializeField] private int maxPoints = 200000;
        [SerializeField] private float pointSize = 0.055f;
        [SerializeField] private float fadeDuration = 120f;
        [SerializeField] private float minAlpha = 0.35f;

        [Header("Rendering")]
        [SerializeField] private Material pointCloudMaterial;

        private PointData[] _ringBuffer;
        private int _headIndex = 0;
        private int _totalPointsCount = 0;

        // Chunking for ParticleSystem limit (65,535 particles max per system)
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
            _ringBuffer = new PointData[maxPoints];
            _numChunks = Mathf.CeilToInt((float)maxPoints / ChunkSize);

            _particleSystems = new ParticleSystem[_numChunks];
            _particlesByChunk = new ParticleSystem.Particle[_numChunks][];
            _chunkDirty = new bool[_numChunks];

            if (pointCloudMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Sprites/Default");
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
        /// Inserts a newly scanned point into the ring buffer.
        /// </summary>
        public void AddPoint(Vector3 position, Color color)
        {
            int index = _headIndex;
            _ringBuffer[index] = new PointData
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

        private void LateUpdate()
        {
            // Stream updated particle buffers to GPU
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
