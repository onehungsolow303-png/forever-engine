using Unity.Collections;
using System;

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

            Instance = this;
            _disposed = false;
        }

        public void LoadWalkability(int z, int[] flatData)
        {
            int size = Width * Height;
            int len = Math.Min(flatData.Length, size);
            for (int i = 0; i < len; i++)
                Walkability[i] = flatData[i] != 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (Walkability.IsCreated) Walkability.Dispose();
            if (FogGrid.IsCreated) FogGrid.Dispose();
            if (Elevation.IsCreated) Elevation.Dispose();
            _disposed = true;
            if (Instance == this) Instance = null;
        }
    }
}
