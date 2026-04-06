using Unity.Collections;
using System;
using System.Collections.Generic;

namespace ForeverEngine.ECS.Data
{
    public class MapDataStore : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int CurrentZ { get; set; }

        public NativeArray<bool> Walkability { get; private set; }
        public NativeArray<byte> FogGrid { get; private set; }
        public NativeArray<float> Elevation { get; private set; }

        // Per-z storage for level swapping
        private Dictionary<int, int[]> _rawWalkByZ = new();

        private bool _disposed;

        public static MapDataStore Instance { get; private set; }

        public void Initialize(int width, int height)
        {
            Dispose();
            Width = width;
            Height = height;
            int size = width * height;

            Walkability = new NativeArray<bool>(size, Allocator.Persistent);
            FogGrid = new NativeArray<byte>(size, Allocator.Persistent);
            Elevation = new NativeArray<float>(size, Allocator.Persistent);
            _rawWalkByZ = new Dictionary<int, int[]>();

            Instance = this;
            _disposed = false;
        }

        public void LoadWalkability(int z, int[] flatData)
        {
            int size = Width * Height;
            int len = Math.Min(flatData.Length, size);

            // Store raw data for later level swapping
            var copy = new int[size];
            Array.Copy(flatData, copy, len);
            _rawWalkByZ[z] = copy;

            // If this is the current z-level, also load into active array
            if (z == CurrentZ)
            {
                var walk = Walkability;
                for (int i = 0; i < len; i++)
                    walk[i] = flatData[i] != 0;
                Walkability = walk;
            }
        }

        /// <summary>
        /// Swap active walkability and fog to a different z-level.
        /// Resets fog to unexplored for the new level.
        /// </summary>
        public bool SwapToLevel(int z)
        {
            if (!_rawWalkByZ.TryGetValue(z, out var rawWalk))
                return false;

            int size = Width * Height;
            var walk = Walkability;
            for (int i = 0; i < size && i < rawWalk.Length; i++)
                walk[i] = rawWalk[i] != 0;
            Walkability = walk;

            // Reset fog to unexplored for new level
            var fog = FogGrid;
            for (int i = 0; i < size; i++)
                fog[i] = 0; // unexplored
            FogGrid = fog;

            CurrentZ = z;
            return true;
        }

        public bool HasLevel(int z) => _rawWalkByZ.ContainsKey(z);

        public void Dispose()
        {
            if (_disposed) return;
            if (Walkability.IsCreated) Walkability.Dispose();
            if (FogGrid.IsCreated) FogGrid.Dispose();
            if (Elevation.IsCreated) Elevation.Dispose();
            _rawWalkByZ?.Clear();
            _disposed = true;
            if (Instance == this) Instance = null;
        }
    }
}
