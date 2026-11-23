using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Mapping
{
    public enum VoxelState : byte
    {
        Unknown = 0,
        Free = 1,
        Occupied = 2
    }

    /// <summary>
    /// Discretized 3D Voxel Occupancy Grid implementing the SLAM world model.
    /// Uses 3D Bresenham/DDA line traversal to mark free space along rays and occupied space at termination.
    /// Sparse spatial hashing ensures unbounded scale and memory efficiency.
    /// </summary>
    public class VoxelMap : MonoBehaviour
    {
        public static VoxelMap Instance { get; private set; }

        [Header("Grid Configuration")]
        [SerializeField] private float voxelSize = 0.5f;
        [SerializeField] private int maxRaySteps = 100;

        private readonly Dictionary<Vector3Int, VoxelState> _voxels = new Dictionary<Vector3Int, VoxelState>();

        public event Action<Vector3Int, VoxelState> OnVoxelUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public float VoxelSize => voxelSize;
        public int TotalExploredVoxels => _voxels.Count;

        /// <summary>
        /// Converts continuous world coordinates to discrete voxel coordinates.
        /// </summary>
        public Vector3Int WorldToVoxel(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / voxelSize),
                Mathf.FloorToInt(worldPos.y / voxelSize),
                Mathf.FloorToInt(worldPos.z / voxelSize)
            );
        }

        /// <summary>
        /// Converts discrete voxel coordinates to continuous center world coordinates.
        /// </summary>
        public Vector3 VoxelToWorld(Vector3Int voxelCoord)
        {
            return new Vector3(
                (voxelCoord.x + 0.5f) * voxelSize,
                (voxelCoord.y + 0.5f) * voxelSize,
                (voxelCoord.z + 0.5f) * voxelSize
            );
        }

        /// <summary>
        /// Ray integration using 3D Digital Differential Analyzer (3D-DDA).
        /// Marks all cells traversed by the beam as Free, and the terminal point as Occupied (if hit).
        /// </summary>
        public void IntegrateRay(Vector3 origin, Vector3 hitPoint, bool didHit)
        {
            Vector3Int current = WorldToVoxel(origin);
            Vector3Int target = WorldToVoxel(hitPoint);

            Vector3 direction = (hitPoint - origin);
            float distance = direction.magnitude;
            if (distance < 0.001f) return;
            direction /= distance;

            int stepX = direction.x > 0 ? 1 : (direction.x < 0 ? -1 : 0);
            int stepY = direction.y > 0 ? 1 : (direction.y < 0 ? -1 : 0);
            int stepZ = direction.z > 0 ? 1 : (direction.z < 0 ? -1 : 0);

            float tDeltaX = stepX != 0 ? Mathf.Abs(voxelSize / direction.x) : float.MaxValue;
            float tDeltaY = stepY != 0 ? Mathf.Abs(voxelSize / direction.y) : float.MaxValue;
            float tDeltaZ = stepZ != 0 ? Mathf.Abs(voxelSize / direction.z) : float.MaxValue;

            Vector3 voxelOrigin = VoxelToWorld(current) - Vector3.one * (voxelSize * 0.5f);
            float tMaxX = stepX > 0 ? (voxelOrigin.x + voxelSize - origin.x) / direction.x : (origin.x - voxelOrigin.x) / -direction.x;
            float tMaxY = stepY > 0 ? (voxelOrigin.y + voxelSize - origin.y) / direction.y : (origin.y - voxelOrigin.y) / -direction.y;
            float tMaxZ = stepZ > 0 ? (voxelOrigin.z + voxelSize - origin.z) / direction.z : (origin.z - voxelOrigin.z) / -direction.z;

            if (stepX == 0) tMaxX = float.MaxValue;
            if (stepY == 0) tMaxY = float.MaxValue;
            if (stepZ == 0) tMaxZ = float.MaxValue;

            int steps = 0;
            while (steps++ < maxRaySteps)
            {
                if (current == target)
                {
                    if (didHit)
                    {
                        SetVoxel(current, VoxelState.Occupied);
                    }
                    else
                    {
                        SetVoxel(current, VoxelState.Free);
                    }
                    break;
                }

                SetVoxel(current, VoxelState.Free);

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        current.x += stepX;
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        current.z += stepZ;
                        tMaxZ += tDeltaZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        current.y += stepY;
                        tMaxY += tDeltaY;
                    }
                    else
                    {
                        current.z += stepZ;
                        tMaxZ += tDeltaZ;
                    }
                }
            }
        }

        public void SetVoxel(Vector3Int coord, VoxelState state)
        {
            if (_voxels.TryGetValue(coord, out var current) && current == VoxelState.Occupied && state == VoxelState.Free)
            {
                // Once confirmed as an occupied wall, keep as occupied
                return;
            }

            _voxels[coord] = state;
            OnVoxelUpdated?.Invoke(coord, state);
        }

        public VoxelState GetVoxel(Vector3Int coord)
        {
            return _voxels.TryGetValue(coord, out var state) ? state : VoxelState.Unknown;
        }

        public Dictionary<Vector3Int, VoxelState> GetAllVoxels() => _voxels;

        public void Clear()
        {
            _voxels.Clear();
        }
    }
}
