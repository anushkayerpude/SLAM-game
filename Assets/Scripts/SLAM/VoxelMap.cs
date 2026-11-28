using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightWarden.SLAM
{
    public enum VoxelState : byte
    {
        Unknown = 0,
        Free = 1,
        Occupied = 2
    }

    /// <summary>
    /// Volumetric 3D Occupancy Grid (0.5m voxel resolution).
    /// Tracks explored free space and obstacle boundaries using 3D-DDA line traversal.
    /// Provides spatial mapping data for the top-down MiniMap.
    /// </summary>
    public class VoxelMap : MonoBehaviour
    {
        public static VoxelMap Instance { get; private set; }

        [Header("Voxel Configuration")]
        [SerializeField] private float voxelSize = 0.5f;
        [SerializeField] private int maxRaySteps = 80;

        private readonly Dictionary<Vector3Int, VoxelState> _grid = new Dictionary<Vector3Int, VoxelState>();

        public event Action<Vector3Int, VoxelState> OnVoxelChanged;

        public float VoxelSize => voxelSize;
        public int TotalExploredVoxels => _grid.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public Vector3Int WorldToVoxel(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x / voxelSize),
                Mathf.FloorToInt(worldPos.y / voxelSize),
                Mathf.FloorToInt(worldPos.z / voxelSize)
            );
        }

        public Vector3 VoxelToWorld(Vector3Int voxelCoord)
        {
            return new Vector3(
                (voxelCoord.x + 0.5f) * voxelSize,
                (voxelCoord.y + 0.5f) * voxelSize,
                (voxelCoord.z + 0.5f) * voxelSize
            );
        }

        /// <summary>
        /// Integrates raycast measurement into the 3D grid via 3D Digital Differential Analyzer (3D-DDA).
        /// </summary>
        public void IntegrateRay(Vector3 origin, Vector3 hitPoint, bool didHit)
        {
            Vector3Int current = WorldToVoxel(origin);
            Vector3Int target = WorldToVoxel(hitPoint);

            Vector3 direction = hitPoint - origin;
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
                    SetVoxel(current, didHit ? VoxelState.Occupied : VoxelState.Free);
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
            if (_grid.TryGetValue(coord, out var current) && current == VoxelState.Occupied && state == VoxelState.Free)
            {
                return;
            }

            _grid[coord] = state;
            OnVoxelChanged?.Invoke(coord, state);
        }

        public VoxelState GetVoxel(Vector3Int coord)
        {
            return _grid.TryGetValue(coord, out var state) ? state : VoxelState.Unknown;
        }

        public Dictionary<Vector3Int, VoxelState> GetAllVoxels() => _grid;

        public void Clear()
        {
            _grid.Clear();
        }
    }
}
